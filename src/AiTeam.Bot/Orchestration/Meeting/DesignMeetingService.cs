using System.Text;
using System.Text.Json;
using AiTeam.Bot.Configuration;
using AiTeam.Bot.GitHub;
using AiTeam.Bot.Services;
using AiTeam.Bot.Workflows.Design;
using AiTeam.Bot.Workflows.Kickoff;
using AiTeam.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace AiTeam.Bot.Orchestration.Meeting;

/// <summary>
/// Stage 34：設計規劃會議引擎（從 MeetingService 拆解而來）。
/// 包含前置作業（Rosa Issues + 條件式 Demi UI 規格）+ 設計會議（最多 DesignMeetingMaxRounds 輪）。
/// consensus → 產出設計規劃書；escalate → 上呈 Christ。
/// </summary>
public class DesignMeetingService(
    GitHubService gitHubService,
    WorkflowSettingsResolver workflowResolver,
    IOptions<GitHubSettings> gitHubSettings,
    IConfiguration configuration,
    MeetingCommons meetingCommons,
    TokenLogService tokenLogService,
    DesignSplitProposalEvaluator splitProposalEvaluator,
    ILogger<DesignMeetingService> logger)
{
    private readonly GitHubSettings _gitHub = gitHubSettings.Value;

    // ---- 設計規劃階段 ----

    /// <summary>
    /// 執行設計規劃階段完整流程。
    /// 包含前置作業（Rosa Issues + 條件式 Demi UI 規格）+ 設計會議（最多 DesignMeetingMaxRounds 輪）。
    /// consensus → 產出設計規劃書；escalate → 上呈 Christ。
    /// </summary>
    public async Task<DesignMeetingResult> RunDesignMeetingAsync(
        TaskGroup group,
        string owner,
        string repo,
        CancellationToken ct = default)
    {
        var apiKey     = GetApiKey();
        var logBuilder = new StringBuilder();
        var totalRounds = 0;

        // Stage 32：方法開頭讀一次設計會議輪次上限（AppSettings 優先、appsettings.json fallback）。
        var designMaxRounds = await workflowResolver.GetDesignMeetingMaxRoundsAsync(ct);

        // Session IDs：Petra UUID（不能用 group.Id，避免 resume Kickoff session）
        // Rosa/Demi session 跨前置作業 + 會議 + 調整全程使用
        var sessions = new DesignSessionState
        {
            PetraSessionId = Guid.NewGuid().ToString(),
            RosaSessionId  = Guid.NewGuid().ToString(),
            DemiSessionId  = null,                        // 條件式，需要時才建立
            CodySessionId  = Guid.NewGuid().ToString(),
            QuinnSessionId = Guid.NewGuid().ToString(),
        };

        var workingDir = "";
        try
        {
            var cloneSuffix = "design-" + group.Id.ToString("N")[..8];
            workingDir = gitHubService.CloneOrPull(owner, repo, cloneSuffix);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "DesignMeetingService：Design clone repo 失敗，使用 workspace fallback");
            workingDir = Path.Combine(_gitHub.WorkspacePath, repo);
        }

        logBuilder.AppendLine("# 設計會議紀錄");
        logBuilder.AppendLine();

        var taskPlan = group.TaskPlan ?? "";

        // 以下用於傳遞設計成果
        string? issueUrls   = null;  // JSON string[]，GitHub Issue URLs
        string issuesJson   = "[]";  // Rosa 產出的 Issues（供 Cody/Quinn prompt 參考）
        string? uiSpecContent = null;

        try
        {
            // ─────────────────────────────────────────────────────────
            // ── 前置作業 ──
            // ─────────────────────────────────────────────────────────
            logBuilder.AppendLine("## 前置作業");
            logBuilder.AppendLine();

            // Petra 判斷是否需要 Demi（isFirstMessage: true）
            // Stage 44：所有 Design RunAgentTurnAsync 都帶 meetingType="Design" + round=group.DesignRound + tokenLogService
            var petraJudgeOutput = await meetingCommons.RunAgentTurnAsync("Petra", sessions.PetraSessionId,
                DesignPrompts.BuildDesignPetraJudgePrompt(taskPlan),
                GetModel("PM"), apiKey, isFirstMessage: true, workingDir, MeetingCommons.ReadOnlyTools, ct,
                meetingType: "Design", round: group.DesignRound, tokenLogService: tokenLogService);

            logBuilder.AppendLine("### Petra — 設計需求判斷");
            logBuilder.AppendLine(petraJudgeOutput);
            logBuilder.AppendLine();

            var needsDemi = DesignPrompts.TryParseNeedsDemi(petraJudgeOutput);
            logger.LogInformation("DesignMeetingService：設計階段 needsDemi={NeedsDemi}（groupId={Id}）", needsDemi, group.Id);

            // Rosa 產出 Issues（isFirstMessage: true，maxTurns: 25）
            var rosaPreWorkOutput = await meetingCommons.RunAgentTurnAsync("Rosa", sessions.RosaSessionId,
                DesignPrompts.BuildDesignRosaPreWorkPrompt(taskPlan),
                GetModel("Requirements"), apiKey, isFirstMessage: true, workingDir, MeetingCommons.ReadOnlyTools, ct,
                maxTurns: 25,
                meetingType: "Design", round: group.DesignRound, tokenLogService: tokenLogService);

            logBuilder.AppendLine("### Rosa — GitHub Issues");
            logBuilder.AppendLine(rosaPreWorkOutput);
            logBuilder.AppendLine();

            // 解析 Issues 並建立 GitHub Issues（MockMode fallback: 解析失敗則跳過）
            var parsedIssues = DesignPrompts.TryParseDesignIssues(rosaPreWorkOutput);
            if (parsedIssues is { Count: > 0 })
            {
                issuesJson = JsonSerializer.Serialize(parsedIssues);
                var issueUrlList = new List<string>();
                foreach (var issue in parsedIssues)
                {
                    try
                    {
                        var url = await gitHubService.CreateIssueAsync(owner, repo, issue.Title, issue.Body, issue.Labels);
                        issueUrlList.Add(url);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "DesignMeetingService：建立 GitHub Issue 失敗：{Title}", issue.Title);
                    }
                }
                if (issueUrlList.Count > 0)
                    issueUrls = JsonSerializer.Serialize(issueUrlList);
            }
            else
            {
                logger.LogWarning("DesignMeetingService：Rosa Issues 無法解析，跳過 GitHub Issue 建立（groupId={Id}）", group.Id);
            }

            // Demi 產出 UI 規格（條件式，isFirstMessage: true，maxTurns: 25）
            if (needsDemi)
            {
                sessions.DemiSessionId = Guid.NewGuid().ToString();
                var demiPreWorkOutput = await meetingCommons.RunAgentTurnAsync("Demi", sessions.DemiSessionId,
                    DesignPrompts.BuildDesignDemiPreWorkPrompt(taskPlan, issuesJson),
                    GetModel("Designer"), apiKey, isFirstMessage: true, workingDir, MeetingCommons.ReadOnlyTools, ct,
                    maxTurns: 25,
                    meetingType: "Design", round: group.DesignRound, tokenLogService: tokenLogService);

                logBuilder.AppendLine("### Demi — UI/UX 規格");
                logBuilder.AppendLine(demiPreWorkOutput);
                logBuilder.AppendLine();
                uiSpecContent = demiPreWorkOutput;
            }
            else
            {
                logBuilder.AppendLine("### Demi — 此任務不需要 UI 設計");
                logBuilder.AppendLine();
            }

            // ─────────────────────────────────────────────────────────
            // ── 設計會議輪次 ──
            // ─────────────────────────────────────────────────────────
            string? lastPetraOutput = null;
            string? finalDesignPlan = null;
            var finalDecision = "consensus";
            string? escalateReason = null;
            for (var round = 1; round <= designMaxRounds; round++)
            {
                totalRounds = round;
                logger.LogInformation("DesignMeetingService：設計會議第 {Round} 輪開始（groupId={Id}）", round, group.Id);

                logBuilder.AppendLine($"## Round {round}");
                logBuilder.AppendLine();

                // Rosa/Demi：resume 前置作業 session（isFirstMessage: false）
                // Cody/Quinn：第 1 輪新建（isFirstMessage: true），後續輪 resume（false）
                var isFirstRound = round == 1;

                var rosaMeetingTask = meetingCommons.RunAgentTurnAsync("Rosa", sessions.RosaSessionId,
                    DesignPrompts.BuildDesignRosaMeetingPrompt(issuesJson, round, lastPetraOutput),
                    GetModel("Requirements"), apiKey, isFirstMessage: false, workingDir, MeetingCommons.ReadOnlyTools, ct,
                    meetingType: "Design", round: round, tokenLogService: tokenLogService);

                Task<string>? demiMeetingTask = null;
                if (sessions.DemiSessionId is not null)
                    demiMeetingTask = meetingCommons.RunAgentTurnAsync("Demi", sessions.DemiSessionId,
                        DesignPrompts.BuildDesignDemiMeetingPrompt(uiSpecContent ?? "", round, lastPetraOutput),
                        GetModel("Designer"), apiKey, isFirstMessage: false, workingDir, MeetingCommons.ReadOnlyTools, ct,
                        meetingType: "Design", round: round, tokenLogService: tokenLogService);

                var codyMeetingTask = meetingCommons.RunAgentTurnAsync("Cody", sessions.CodySessionId,
                    DesignPrompts.BuildDesignCodyPrompt(issuesJson, uiSpecContent, round, lastPetraOutput),
                    GetModel("Dev"), apiKey, isFirstMessage: isFirstRound, workingDir, allowedTools: null, ct,
                    meetingType: "Design", round: round, tokenLogService: tokenLogService);
                var quinnMeetingTask = meetingCommons.RunAgentTurnAsync("Quinn", sessions.QuinnSessionId,
                    DesignPrompts.BuildDesignQuinnPrompt(issuesJson, uiSpecContent, round, lastPetraOutput),
                    GetModel("QA"), apiKey, isFirstMessage: isFirstRound, workingDir, MeetingCommons.ReadOnlyTools, ct,
                    meetingType: "Design", round: round, tokenLogService: tokenLogService);

                var parallelTasks = new List<Task<string>> { rosaMeetingTask, codyMeetingTask, quinnMeetingTask };
                if (demiMeetingTask is not null) parallelTasks.Add(demiMeetingTask);
                await Task.WhenAll(parallelTasks);

                var rosaOutput  = rosaMeetingTask.Result;
                var demiOutput  = demiMeetingTask?.Result ?? "";
                var codyOutput  = codyMeetingTask.Result;
                var quinnOutput = quinnMeetingTask.Result;

                logBuilder.AppendLine("### Rosa（需求分析）");
                logBuilder.AppendLine(rosaOutput);
                logBuilder.AppendLine();
                if (sessions.DemiSessionId is not null)
                {
                    logBuilder.AppendLine("### Demi（UI/UX 設計）");
                    logBuilder.AppendLine(demiOutput);
                    logBuilder.AppendLine();
                }
                logBuilder.AppendLine("### Cody（技術可行性）");
                logBuilder.AppendLine(codyOutput);
                logBuilder.AppendLine();
                logBuilder.AppendLine("### Quinn（測試規劃）");
                logBuilder.AppendLine(quinnOutput);
                logBuilder.AppendLine();

                // Petra 整理（resume session）
                var petraRoundPrompt = DesignPrompts.BuildDesignPetraRoundPrompt(rosaOutput, demiOutput, codyOutput, quinnOutput, round, sessions.DemiSessionId is not null);
                var petraOutput = await meetingCommons.RunAgentTurnAsync("Petra", sessions.PetraSessionId,
                    petraRoundPrompt, GetModel("PM"), apiKey, isFirstMessage: false, workingDir, MeetingCommons.ReadOnlyTools, ct,
                    meetingType: "Design", round: round, tokenLogService: tokenLogService);
                lastPetraOutput = petraOutput;

                logBuilder.AppendLine("### Petra（綜合整理）");
                logBuilder.AppendLine(petraOutput);
                logBuilder.AppendLine();

                var decision = DesignPrompts.TryParseDesignPetraDecision(petraOutput);

                if (decision is null)
                {
                    // 無法解析 → 視同 consensus（MockMode fallback）
                    logger.LogWarning("DesignMeetingService：設計會議 Petra 第 {Round} 輪無法解析 decision，假設 consensus", round);
                    finalDesignPlan = await GenerateDesignPlanAsync(
                        sessions.PetraSessionId, taskPlan, issuesJson, uiSpecContent, workingDir, apiKey, round, ct);
                    logBuilder.AppendLine("## 設計規劃書");
                    logBuilder.AppendLine(finalDesignPlan);
                    break;
                }

                logger.LogInformation("DesignMeetingService：設計會議第 {Round} 輪 decision={Decision}", round, decision.Decision);

                if (decision.Decision == "consensus")
                {
                    finalDesignPlan = await GenerateDesignPlanAsync(
                        sessions.PetraSessionId, taskPlan, issuesJson, uiSpecContent, workingDir, apiKey, round, ct);
                    logBuilder.AppendLine("## 設計規劃書");
                    logBuilder.AppendLine(finalDesignPlan);
                    break;
                }

                if (decision.Decision == "escalate")
                {
                    finalDecision  = "escalate";
                    escalateReason = decision.EscalateReason;
                    logger.LogWarning("DesignMeetingService：設計會議需上呈 Christ（groupId={Id}）", group.Id);
                    break;
                }

                if (decision.Decision == "needs_adjustment")
                {
                    var adjResult = await RunDesignAdjustmentAsync(
                        sessions, group, owner, repo, workingDir, apiKey,
                        taskPlan, decision, logBuilder, ct);

                    // 合併更新後的 issue/uiSpec 資料
                    if (!string.IsNullOrEmpty(adjResult.UpdatedIssueUrls)) issueUrls     = adjResult.UpdatedIssueUrls;
                    if (!string.IsNullOrEmpty(adjResult.UpdatedIssuesJson)) issuesJson   = adjResult.UpdatedIssuesJson;
                    if (!string.IsNullOrEmpty(adjResult.UpdatedUiSpec))    uiSpecContent = adjResult.UpdatedUiSpec;

                    if (adjResult.Approved)
                    {
                        finalDesignPlan = adjResult.DesignPlan;
                        logBuilder.AppendLine("## 設計規劃書");
                        logBuilder.AppendLine(finalDesignPlan);
                        break;
                    }

                    // needs_meeting：遞增輪次，判斷是否超上限
                    totalRounds++;
                    if (totalRounds > designMaxRounds)
                    {
                        finalDecision  = "escalate";
                        escalateReason = "多輪調整後仍需重開會議，已達設計會議上限";
                        logger.LogInformation("DesignMeetingService：設計會議調整後超過上限，escalate（groupId={Id}）", group.Id);
                        break;
                    }

                    // 繼續外層迴圈（以更新後的 issuesJson/uiSpec 重開下一輪）
                    lastPetraOutput = null;
                    continue;
                }

                // needs_discussion：繼續下一輪
                if (round == designMaxRounds)
                {
                    logger.LogInformation("DesignMeetingService：設計會議已達最大輪次 {Max}，強制結束", designMaxRounds);
                    finalDesignPlan = await GenerateDesignPlanAsync(
                        sessions.PetraSessionId, taskPlan, issuesJson, uiSpecContent, workingDir, apiKey, round, ct);
                    logBuilder.AppendLine("## 設計規劃書");
                    logBuilder.AppendLine(finalDesignPlan);
                }
            }

            // ── Stage 46-FF 三十五：consensus 路徑下評估是否提案拆 task ──
            // 規則層（Issue 數 ≥ 8 / 預估行數 ≥ 500 / 跨多 Phase 標記任一）→ Petra 細化拆法
            // escalate 路徑不觸發（任務本身需老闆裁決，不適合再丟拆 task 提案）
            SplitProposal? splitProposal = null;
            if (finalDecision == "consensus" && !string.IsNullOrWhiteSpace(finalDesignPlan))
            {
                splitProposal = await splitProposalEvaluator.EvaluateAndProposeSplitAsync(
                    sessions.PetraSessionId, finalDesignPlan!, issuesJson,
                    workingDir, apiKey, totalRounds, tokenLogService, ct);
                if (splitProposal is not null)
                {
                    logBuilder.AppendLine("## 拆 task 提案（Stage 46-FF 三十五）");
                    logBuilder.AppendLine($"should_split={splitProposal.ShouldSplit}，phases={splitProposal.Phases.Count}，rationale={splitProposal.Rationale}");
                }
            }

            return new DesignMeetingResult(
                Success:        true,
                MeetingLog:     logBuilder.ToString(),
                DesignPlan:     finalDesignPlan,
                IssueUrls:      issueUrls,
                UiSpecContent:  uiSpecContent,
                TotalRounds:    totalRounds,
                FinalDecision:  finalDecision,
                PetraSessionId: sessions.PetraSessionId,
                EscalateReason: escalateReason,
                SplitProposal:  splitProposal);
        }
        finally
        {
            if (!string.IsNullOrEmpty(workingDir))
            {
                try { gitHubService.CleanupLocalRepo(workingDir); }
                catch (Exception ex) { logger.LogWarning(ex, "DesignMeetingService：Design cleanup workingDir 失敗"); }
            }
        }
    }

    /// <summary>
    /// Christ 要求修改設計規劃書時，resume Petra 的設計 session（含完整設計 context）。
    /// petraSessionId 由呼叫方傳入（從 _pendingDesignConfirmations dictionary 取出）。
    /// </summary>
    public async Task<ModifyResult> ModifyDesignPlanAsync(
        TaskGroup group,
        string christFeedback,
        string petraSessionId,
        string owner,
        string repo,
        CancellationToken ct = default)
    {
        var apiKey = GetApiKey();
        var workingDir = "";
        try
        {
            var cloneSuffix = "design-modify-" + group.Id.ToString("N")[..8];
            workingDir = gitHubService.CloneOrPull(owner, repo, cloneSuffix);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "DesignMeetingService：ModifyDesignPlan clone repo 失敗，使用 workspace fallback");
            workingDir = Path.Combine(_gitHub.WorkspacePath, repo);
        }

        try
        {
            var prompt =
                $"老闆要求修改設計規劃書，修改意見如下：\n\n{christFeedback}\n\n" +
                $"請基於完整的設計會議 context 評估修改影響，並在回應最後輸出以下 JSON（不要有其他格式）：\n" +
                $"{{\"impact\":\"small|large\",\"revised_plan\":\"（small 時輸出完整修改後設計規劃書，large 時留空）\"}}";

            var petraOutput = await meetingCommons.RunAgentTurnAsync("Petra", petraSessionId,
                prompt, GetModel("PM"), apiKey, isFirstMessage: false, workingDir, MeetingCommons.ReadOnlyTools, ct,
                meetingType: "Design", round: group.DesignRound, tokenLogService: tokenLogService);

            logger.LogInformation("DesignMeetingService：ModifyDesignPlan Petra 回應完成（groupId={Id}）", group.Id);

            var modifyDecision = KickoffPrompts.TryParseModifyDecision(petraOutput);

            return new ModifyResult(
                PetraFullOutput: petraOutput,
                Impact:          modifyDecision?.Impact ?? "small",
                RevisedPlan:     modifyDecision?.RevisedPlan ?? petraOutput);
        }
        finally
        {
            if (!string.IsNullOrEmpty(workingDir))
            {
                try { gitHubService.CleanupLocalRepo(workingDir); }
                catch (Exception ex) { logger.LogWarning(ex, "DesignMeetingService：ModifyDesignPlan cleanup 失敗"); }
            }
        }
    }

    // ---- 設計會議調整流程 ----

    /// <summary>
    /// 執行設計會議調整流程（needs_adjustment 路徑）。
    /// Rosa/Demi 依 Petra 指示修改，Petra 評估後決定 approved 或 needs_meeting。
    /// </summary>
    private async Task<DesignAdjustmentResult> RunDesignAdjustmentAsync(
        DesignSessionState sessions,
        TaskGroup group,
        string owner,
        string repo,
        string workingDir,
        string apiKey,
        string taskPlan,
        DesignPetraDecision decision,
        StringBuilder logBuilder,
        CancellationToken ct)
    {
        // Stage 37 搭車修：Petra JSON 回應可能漏填 AdjustmentTargets / AdjustmentInstructions
        // （record 欄位雖非 nullable，但 System.Text.Json 不強制 runtime non-null），
        // 用 defensive defaults 避免 null 時 .Any() / .GetValueOrDefault 炸掉整場會議。
        var adjustmentTargets      = decision.AdjustmentTargets      ?? [];
        var adjustmentInstructions = decision.AdjustmentInstructions ?? new Dictionary<string, string>();

        if (decision.AdjustmentTargets is null || decision.AdjustmentInstructions is null)
            logger.LogWarning("DesignMeetingService：Petra 回應 needs_adjustment 但缺 AdjustmentTargets/Instructions（Group={Id}），已退化為 no-op 調整輪",
                group.Id);

        logBuilder.AppendLine("## 調整紀錄");
        logBuilder.AppendLine();
        logBuilder.AppendLine("### Petra 修改指示");
        logBuilder.AppendLine(JsonSerializer.Serialize(adjustmentInstructions,
            new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
        logBuilder.AppendLine();

        var updatedRosaOutput = "";
        var updatedDemiOutput = "";
        var updatedIssuesJson = "";
        var updatedIssueUrls  = "";

        // Rosa 調整
        if (adjustmentTargets.Any(t => t.Equals("rosa", StringComparison.OrdinalIgnoreCase)))
        {
            var instruction = adjustmentInstructions.GetValueOrDefault("rosa", "請根據會議討論修改 Issues");
            var prompt = $"Petra 的修改指示：\n\n{instruction}\n\n請調整你的 GitHub Issues，在回應最後以 JSON Array 格式輸出更新後的完整 Issues 清單（格式同前置作業）。";
            updatedRosaOutput = await meetingCommons.RunAgentTurnAsync("Rosa", sessions.RosaSessionId,
                prompt, GetModel("Requirements"), apiKey, isFirstMessage: false, workingDir, MeetingCommons.ReadOnlyTools, ct,
                meetingType: "Design", round: group.DesignRound, tokenLogService: tokenLogService);

            logBuilder.AppendLine("### Rosa 調整結果");
            logBuilder.AppendLine(updatedRosaOutput);
            logBuilder.AppendLine();

            // 更新 GitHub Issues
            var updatedParsed = DesignPrompts.TryParseDesignIssues(updatedRosaOutput);
            if (updatedParsed is { Count: > 0 })
            {
                updatedIssuesJson = JsonSerializer.Serialize(updatedParsed);
                var urlList = new List<string>();
                foreach (var issue in updatedParsed)
                {
                    try
                    {
                        var url = await gitHubService.CreateIssueAsync(owner, repo, issue.Title, issue.Body, issue.Labels);
                        urlList.Add(url);
                    }
                    catch (Exception ex) { logger.LogWarning(ex, "DesignMeetingService：調整後 Issue 建立失敗：{Title}", issue.Title); }
                }
                if (urlList.Count > 0)
                    updatedIssueUrls = JsonSerializer.Serialize(urlList);
            }
        }

        // Demi 調整
        if (adjustmentTargets.Any(t => t.Equals("demi", StringComparison.OrdinalIgnoreCase)))
        {
            var instruction = adjustmentInstructions.GetValueOrDefault("demi", "請根據會議討論修改 UI 規格");

            if (sessions.DemiSessionId is null)
            {
                // 邊界案例：初始不需要 Demi，但設計會議中發現需要 UI 規格
                sessions.DemiSessionId = Guid.NewGuid().ToString();
                var createPrompt =
                    $"你是 Demi，負責 UI/UX 設計。設計會議發現此任務需要 UI 規格。\n\n" +
                    $"## 任務計劃書\n{taskPlan}\n\n" +
                    $"## Petra 的指示\n{instruction}\n\n" +
                    $"請探索相關 codebase 後，產出完整的 UI/UX 規格（Markdown 格式）。";
                updatedDemiOutput = await meetingCommons.RunAgentTurnAsync("Demi", sessions.DemiSessionId,
                    createPrompt, GetModel("Designer"), apiKey, isFirstMessage: true, workingDir, MeetingCommons.ReadOnlyTools, ct,
                    maxTurns: 25,
                    meetingType: "Design", round: group.DesignRound, tokenLogService: tokenLogService);
            }
            else
            {
                var adjustPrompt = $"Petra 的修改指示：\n\n{instruction}\n\n請調整你的 UI 規格，回應更新後的完整 UI/UX 規格（Markdown 格式）。";
                updatedDemiOutput = await meetingCommons.RunAgentTurnAsync("Demi", sessions.DemiSessionId,
                    adjustPrompt, GetModel("Designer"), apiKey, isFirstMessage: false, workingDir, MeetingCommons.ReadOnlyTools, ct,
                    meetingType: "Design", round: group.DesignRound, tokenLogService: tokenLogService);
            }

            logBuilder.AppendLine("### Demi 調整結果");
            logBuilder.AppendLine(updatedDemiOutput);
            logBuilder.AppendLine();
        }

        // Petra 評估修改幅度
        var sb = new StringBuilder();
        sb.AppendLine("Rosa 和 Demi 已完成修改。以下是調整後的內容：");
        sb.AppendLine();
        if (!string.IsNullOrEmpty(updatedRosaOutput))
        {
            sb.AppendLine("### Rosa 調整後的 Issues");
            sb.AppendLine(updatedRosaOutput);
            sb.AppendLine();
        }
        if (!string.IsNullOrEmpty(updatedDemiOutput))
        {
            sb.AppendLine("### Demi 調整後的 UI 規格");
            sb.AppendLine(updatedDemiOutput);
            sb.AppendLine();
        }
        sb.AppendLine("請評估修改幅度，在回應最後輸出以下 JSON（單獨一行，不加 code block）：");
        sb.AppendLine("{\"evaluation\":\"approved|needs_meeting\",\"design_plan\":\"（approved 時輸出完整設計規劃書，needs_meeting 時留空）\",\"reason\":\"reason\"}");
        sb.AppendLine("- approved：修改幅度小，Petra 自審通過 → 輸出完整設計規劃書");
        sb.AppendLine("- needs_meeting：修改幅度大，需重開設計會議");

        var petraEvalOutput = await meetingCommons.RunAgentTurnAsync("Petra", sessions.PetraSessionId,
            sb.ToString(), GetModel("PM"), apiKey, isFirstMessage: false, workingDir, MeetingCommons.ReadOnlyTools, ct,
            meetingType: "Design", round: group.DesignRound, tokenLogService: tokenLogService);

        logBuilder.AppendLine("### Petra 評估");
        logBuilder.AppendLine(petraEvalOutput);
        logBuilder.AppendLine();

        var evalDecision = DesignPrompts.TryParseDesignAdjustmentEvaluation(petraEvalOutput);
        var isApproved   = evalDecision is null || evalDecision.Evaluation == "approved";

        if (isApproved)
        {
            var designPlan = evalDecision?.DesignPlan ?? "";
            if (string.IsNullOrWhiteSpace(designPlan))
                designPlan = await GenerateDesignPlanAsync(
                    sessions.PetraSessionId, taskPlan, updatedIssuesJson, updatedDemiOutput, workingDir, apiKey, group.DesignRound, ct);

            return new DesignAdjustmentResult(
                Approved:          true,
                Escalate:          false,
                DesignPlan:        designPlan,
                UpdatedIssueUrls:  updatedIssueUrls,
                UpdatedIssuesJson: updatedIssuesJson,
                UpdatedUiSpec:     string.IsNullOrEmpty(updatedDemiOutput) ? null : updatedDemiOutput);
        }

        return new DesignAdjustmentResult(
            Approved:          false,
            Escalate:          false,
            DesignPlan:        null,
            UpdatedIssueUrls:  updatedIssueUrls,
            UpdatedIssuesJson: updatedIssuesJson,
            UpdatedUiSpec:     string.IsNullOrEmpty(updatedDemiOutput) ? null : updatedDemiOutput);
    }

    /// <summary>Petra 產出最終設計規劃書。</summary>
    /// <param name="round">Stage 44：當前 Design 輪次（供 token_logs Round 欄位）。</param>
    private async Task<string> GenerateDesignPlanAsync(
        string petraSessionId,
        string taskPlan,
        string issuesJson,
        string? uiSpecContent,
        string workingDir,
        string apiKey,
        int round,
        CancellationToken ct)
    {
        var prompt = DesignPrompts.BuildDesignPetraPlanPrompt(taskPlan, issuesJson, uiSpecContent);
        return await meetingCommons.RunAgentTurnAsync("Petra", petraSessionId,
            prompt, GetModel("PM"), apiKey, isFirstMessage: false, workingDir, MeetingCommons.ReadOnlyTools, ct,
            meetingType: "Design", round: round, tokenLogService: tokenLogService);
    }

    // ---- Stage 46-FF 三十五：拆 task 雙層判斷 ----
    // Stage 52：抽出至 src/AiTeam.Bot/Orchestration/Meeting/DesignSplitProposalEvaluator.cs
    // （feature flag legacy + framework 共用 SoT；scoped service，DesignMeetingService ctor 注入）

    // ---- 設計會議 Prompt 建立 + JSON 解析 ----
    // Stage 52：抽出至 src/AiTeam.Bot/Workflows/Design/DesignPrompts.cs（feature flag legacy + framework 共用 SoT）。
    // ModifyDecision parser 沿用 KickoffPrompts.TryParseModifyDecision（不重抽，避免雙寫漂移）。

    // ---- 設定輔助 ----

    private string GetApiKey()
        => configuration["AITEAM_ANTHROPIC_KEY"]
        ?? configuration["Anthropic:ApiKey"]
        ?? "";

    private string GetModel(string agentKey)
        => configuration[$"Agents:{agentKey}:Model"]
        ?? configuration["Anthropic:DefaultModel"]
        ?? "claude-sonnet-4-6";
}

// ---- Stage 25b：Design Phase Internal DTOs（legacy 自用，未抽到 DesignPrompts.cs）----

/// <summary>設計會議 session 狀態，跨前置作業 → 會議 → 調整全程持有。</summary>
internal class DesignSessionState
{
    public string  PetraSessionId { get; set; } = "";
    public string  RosaSessionId  { get; set; } = "";
    public string? DemiSessionId  { get; set; }
    public string  CodySessionId  { get; set; } = "";
    public string  QuinnSessionId { get; set; } = "";
}

internal record DesignAdjustmentResult(
    bool    Approved,
    bool    Escalate,
    string? DesignPlan,
    string  UpdatedIssueUrls,
    string  UpdatedIssuesJson,
    string? UpdatedUiSpec);
