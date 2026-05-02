using System.Text;
using System.Text.Json;
using AiTeam.Bot.Configuration;
using AiTeam.Bot.GitHub;
using AiTeam.Bot.Services;
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
    AppSettingsService appSettings,
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
                BuildDesignPetraJudgePrompt(taskPlan),
                GetModel("PM"), apiKey, isFirstMessage: true, workingDir, MeetingCommons.ReadOnlyTools, ct,
                meetingType: "Design", round: group.DesignRound, tokenLogService: tokenLogService);

            logBuilder.AppendLine("### Petra — 設計需求判斷");
            logBuilder.AppendLine(petraJudgeOutput);
            logBuilder.AppendLine();

            var needsDemi = TryParseNeedsDemi(petraJudgeOutput);
            logger.LogInformation("DesignMeetingService：設計階段 needsDemi={NeedsDemi}（groupId={Id}）", needsDemi, group.Id);

            // Rosa 產出 Issues（isFirstMessage: true，maxTurns: 25）
            var rosaPreWorkOutput = await meetingCommons.RunAgentTurnAsync("Rosa", sessions.RosaSessionId,
                BuildDesignRosaPreWorkPrompt(taskPlan),
                GetModel("Requirements"), apiKey, isFirstMessage: true, workingDir, MeetingCommons.ReadOnlyTools, ct,
                maxTurns: 25,
                meetingType: "Design", round: group.DesignRound, tokenLogService: tokenLogService);

            logBuilder.AppendLine("### Rosa — GitHub Issues");
            logBuilder.AppendLine(rosaPreWorkOutput);
            logBuilder.AppendLine();

            // 解析 Issues 並建立 GitHub Issues（MockMode fallback: 解析失敗則跳過）
            var parsedIssues = TryParseDesignIssues(rosaPreWorkOutput);
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
                    BuildDesignDemiPreWorkPrompt(taskPlan, issuesJson),
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
                    BuildDesignRosaMeetingPrompt(issuesJson, round, lastPetraOutput),
                    GetModel("Requirements"), apiKey, isFirstMessage: false, workingDir, MeetingCommons.ReadOnlyTools, ct,
                    meetingType: "Design", round: round, tokenLogService: tokenLogService);

                Task<string>? demiMeetingTask = null;
                if (sessions.DemiSessionId is not null)
                    demiMeetingTask = meetingCommons.RunAgentTurnAsync("Demi", sessions.DemiSessionId,
                        BuildDesignDemiMeetingPrompt(uiSpecContent ?? "", round, lastPetraOutput),
                        GetModel("Designer"), apiKey, isFirstMessage: false, workingDir, MeetingCommons.ReadOnlyTools, ct,
                        meetingType: "Design", round: round, tokenLogService: tokenLogService);

                var codyMeetingTask = meetingCommons.RunAgentTurnAsync("Cody", sessions.CodySessionId,
                    BuildDesignCodyPrompt(issuesJson, uiSpecContent, round, lastPetraOutput),
                    GetModel("Dev"), apiKey, isFirstMessage: isFirstRound, workingDir, allowedTools: null, ct,
                    meetingType: "Design", round: round, tokenLogService: tokenLogService);
                var quinnMeetingTask = meetingCommons.RunAgentTurnAsync("Quinn", sessions.QuinnSessionId,
                    BuildDesignQuinnPrompt(issuesJson, uiSpecContent, round, lastPetraOutput),
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
                var petraRoundPrompt = BuildDesignPetraRoundPrompt(rosaOutput, demiOutput, codyOutput, quinnOutput, round, sessions.DemiSessionId is not null);
                var petraOutput = await meetingCommons.RunAgentTurnAsync("Petra", sessions.PetraSessionId,
                    petraRoundPrompt, GetModel("PM"), apiKey, isFirstMessage: false, workingDir, MeetingCommons.ReadOnlyTools, ct,
                    meetingType: "Design", round: round, tokenLogService: tokenLogService);
                lastPetraOutput = petraOutput;

                logBuilder.AppendLine("### Petra（綜合整理）");
                logBuilder.AppendLine(petraOutput);
                logBuilder.AppendLine();

                var decision = TryParseDesignPetraDecision(petraOutput);

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
                splitProposal = await EvaluateAndProposeSplitAsync(
                    sessions.PetraSessionId, finalDesignPlan!, issuesJson,
                    workingDir, apiKey, totalRounds, ct);
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

            var modifyDecision = TryParseModifyDecision(petraOutput);

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
            var updatedParsed = TryParseDesignIssues(updatedRosaOutput);
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

        var evalDecision = TryParseDesignAdjustmentEvaluation(petraEvalOutput);
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
        var prompt = BuildDesignPetraPlanPrompt(taskPlan, issuesJson, uiSpecContent);
        return await meetingCommons.RunAgentTurnAsync("Petra", petraSessionId,
            prompt, GetModel("PM"), apiKey, isFirstMessage: false, workingDir, MeetingCommons.ReadOnlyTools, ct,
            meetingType: "Design", round: round, tokenLogService: tokenLogService);
    }

    // ---- Stage 46-FF 三十五：拆 task 雙層判斷（規則層 + Petra 層） ----

    /// <summary>
    /// Stage 46-FF 三十五：規則層 + Petra 層混合（議題 1 C）。
    /// 規則層：判「要不要觸發拆 task 提案」（Issue 數 / 預估行數 / Phase 標記任一觸發）。
    /// Petra 層：判「怎麼拆」（resume PetraSessionId 問細化拆法）。
    /// 不觸發 → 回 null；觸發但 Petra 認定不該拆 → 回 ShouldSplit=false 的 SplitProposal。
    /// </summary>
    private async Task<SplitProposal?> EvaluateAndProposeSplitAsync(
        string petraSessionId,
        string designPlan,
        string issuesJson,
        string workingDir,
        string apiKey,
        int round,
        CancellationToken ct)
    {
        var minIssues = await GetSplitTaskAppSettingIntAsync("Stage46:SplitTaskMinIssueCount", 8, ct);
        var minLines  = await GetSplitTaskAppSettingIntAsync("Stage46:SplitTaskMinEstimatedLines", 500, ct);

        var issueCount        = TryCountIssues(issuesJson);
        var estimatedLines    = EstimateDesignPlanLines(designPlan);
        var hasPhaseMarkers   = ContainsPhaseMarkers(designPlan);

        var triggered = issueCount >= minIssues
                     || estimatedLines >= minLines
                     || hasPhaseMarkers;

        if (!triggered)
        {
            logger.LogInformation(
                "DesignMeetingService：拆 task 規則層未觸發（issueCount={IC}<{Min1}, estLines={EL}<{Min2}, phaseMarkers={PM}）",
                issueCount, minIssues, estimatedLines, minLines, hasPhaseMarkers);
            return null;
        }

        logger.LogInformation(
            "DesignMeetingService：拆 task 規則層觸發（issueCount={IC}, estLines={EL}, phaseMarkers={PM}），呼叫 Petra 細化拆法",
            issueCount, estimatedLines, hasPhaseMarkers);

        return await RunPetraSplitTaskProposalAsync(
            petraSessionId, designPlan, issuesJson, workingDir, apiKey, round, ct);
    }

    /// <summary>
    /// Stage 46-FF 三十五：Petra 細化拆法 — resume Design Petra session 問拆 phases。
    /// 復用 GenerateDesignPlanAsync 的 sessionId（session 內已有 DesignPlan + 五人發言 context）。
    /// </summary>
    private async Task<SplitProposal?> RunPetraSplitTaskProposalAsync(
        string petraSessionId,
        string designPlan,
        string issuesJson,
        string workingDir,
        string apiKey,
        int round,
        CancellationToken ct)
    {
        var prompt = BuildSplitTaskPetraPrompt(designPlan, issuesJson);
        var output = await meetingCommons.RunAgentTurnAsync("Petra", petraSessionId,
            prompt, GetModel("PM"), apiKey, isFirstMessage: false, workingDir, MeetingCommons.ReadOnlyTools, ct,
            meetingType: "Design", round: round, tokenLogService: tokenLogService);

        var parsed = TryParseSplitProposal(output);
        if (parsed is null)
        {
            logger.LogWarning("DesignMeetingService：Petra 拆 task 提案 JSON 解析失敗，視為不拆（output 前 200 字={Output})",
                output.Length > 200 ? output[..200] : output);
            return null;
        }
        return parsed;
    }

    /// <summary>Stage 46-FF 三十五：AppSettingsService 只有 GetAsync(string?)，自己 int.TryParse。</summary>
    private async Task<int> GetSplitTaskAppSettingIntAsync(string key, int defaultValue, CancellationToken ct)
    {
        var raw = await appSettings.GetAsync(key, ct);
        if (string.IsNullOrWhiteSpace(raw)) return defaultValue;
        return int.TryParse(raw, out var value) ? value : defaultValue;
    }

    private static int TryCountIssues(string issuesJson)
    {
        if (string.IsNullOrWhiteSpace(issuesJson)) return 0;
        try
        {
            using var doc = JsonDocument.Parse(issuesJson);
            return doc.RootElement.ValueKind == JsonValueKind.Array
                ? doc.RootElement.GetArrayLength()
                : 0;
        }
        catch { return 0; }
    }

    private static int EstimateDesignPlanLines(string designPlan)
    {
        if (string.IsNullOrWhiteSpace(designPlan)) return 0;
        // 抓「預估 N 行」/「預計 N 行」/「~ N 行」等字樣，取最大值
        var matches = System.Text.RegularExpressions.Regex.Matches(
            designPlan, @"(?:預估|預計|約|~)\s*(\d+)\s*行");
        var max = 0;
        foreach (System.Text.RegularExpressions.Match m in matches)
        {
            if (int.TryParse(m.Groups[1].Value, out var n) && n > max) max = n;
        }
        return max;
    }

    private static bool ContainsPhaseMarkers(string designPlan)
    {
        if (string.IsNullOrWhiteSpace(designPlan)) return false;
        // Phase 1/2/3 標記（含中英）
        return System.Text.RegularExpressions.Regex.IsMatch(
            designPlan, @"Phase\s*[1-9]", System.Text.RegularExpressions.RegexOptions.IgnoreCase)
            || designPlan.Contains("第一階段") || designPlan.Contains("第二階段");
    }

    private static string BuildSplitTaskPetraPrompt(string designPlan, string issuesJson)
    {
        var sb = new StringBuilder();
        sb.AppendLine("[SPLIT-TASK] 你是 Petra，剛產出 DesignPlan。Orchestrator 規則層判定此任務值得評估拆 task。");
        sb.AppendLine();
        sb.AppendLine("## 你剛產出的 DesignPlan");
        sb.AppendLine(designPlan);
        sb.AppendLine();
        sb.AppendLine("## Rosa 拆出的 Issues（JSON）");
        sb.AppendLine(issuesJson);
        sb.AppendLine();
        sb.AppendLine("## 你的職責");
        sb.AppendLine("依 DesignPlan 與 Issues 評估是否拆成 2-3 個 Phase（依賴鏈 Sequential 執行，各自獨立 PR）。");
        sb.AppendLine("判準與不該拆 case 詳見 CLAUDE_Petra.md「Design 階段拆 task 判準（Stage 46-FF 三十五）」段。");
        sb.AppendLine();
        sb.AppendLine("## 輸出格式（嚴格 JSON，不加 code block）");
        sb.AppendLine();
        sb.AppendLine("若該拆：");
        sb.AppendLine("{\"should_split\":true,\"rationale\":\"...\",\"phases\":[{\"phase\":1,\"description\":\"基礎結構\",\"issues\":[2],\"estimated_minutes\":30},{\"phase\":2,\"description\":\"元件遷移\",\"issues\":[3,4,5,6,7,8,9],\"estimated_minutes\":120}]}");
        sb.AppendLine();
        sb.AppendLine("若不該拆（規則層觸發但 Issue 緊密耦合等）：");
        sb.AppendLine("{\"should_split\":false,\"rationale\":\"...\"}");
        return sb.ToString();
    }

    /// <summary>
    /// Stage 46-FF 三十五：解析 Petra 的拆 task JSON，失敗回 null。
    /// 對齊 Stage 44 TryParseSageEscalate try-catch fallback 風格。
    /// </summary>
    public static SplitProposal? TryParseSplitProposal(string output)
    {
        if (string.IsNullOrWhiteSpace(output)) return null;
        try
        {
            // 找 root JSON 物件邊界：第一個 { 是 root start（output 可能含解說文字）；
            // 不能用 LastIndexOf('{')，會抓到 phases 內最後一個 PhaseSpec 的 { 而非 root（驗收期實證 bug）
            var startIdx = output.IndexOf('{');
            var endIdx   = output.LastIndexOf('}');
            if (startIdx < 0 || endIdx <= startIdx) return null;
            var jsonStr  = output[startIdx..(endIdx + 1)];

            using var doc = JsonDocument.Parse(jsonStr);
            var root      = doc.RootElement;
            var shouldSplit = root.TryGetProperty("should_split", out var ss) && ss.GetBoolean();
            var rationale   = root.TryGetProperty("rationale", out var r) ? (r.GetString() ?? "") : "";

            var phases = new List<PhaseSpec>();
            if (shouldSplit && root.TryGetProperty("phases", out var phasesEl) && phasesEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var p in phasesEl.EnumerateArray())
                {
                    var phaseNum   = p.TryGetProperty("phase", out var pn) ? pn.GetInt32() : 0;
                    var phaseDesc  = p.TryGetProperty("description", out var pd) ? (pd.GetString() ?? "") : "";
                    var issueIds   = new List<int>();
                    if (p.TryGetProperty("issues", out var iss) && iss.ValueKind == JsonValueKind.Array)
                        foreach (var id in iss.EnumerateArray())
                            if (id.TryGetInt32(out var n)) issueIds.Add(n);
                    var minutes    = p.TryGetProperty("estimated_minutes", out var em) ? em.GetInt32() : 0;
                    phases.Add(new PhaseSpec(phaseNum, phaseDesc, issueIds, minutes));
                }
            }

            return new SplitProposal(shouldSplit, rationale, phases);
        }
        catch { return null; }
    }

    // ---- 設計會議 Prompt 建立 ----

    private static string BuildDesignPetraJudgePrompt(string taskPlan)
    {
        var sb = new StringBuilder();
        sb.AppendLine("你是 Petra，AI 團隊的 PM，正在判斷設計階段是否需要 Demi 參與 UI/UX 設計。");
        sb.AppendLine();
        sb.AppendLine("## 任務計劃書");
        sb.AppendLine(taskPlan);
        sb.AppendLine();
        sb.AppendLine("## 你的職責");
        sb.AppendLine("根據任務計劃書，判斷此功能是否需要 Dashboard UI 設計（例如：新頁面、新元件、Layout 調整等）。");
        sb.AppendLine("你可以讀取現有 Dashboard 相關的 Blazor 元件檔案來輔助判斷。");
        sb.AppendLine();
        sb.AppendLine("請在回應最後輸出以下 JSON（單獨一行，不加 code block）：");
        sb.AppendLine("{\"needs_demi\":true,\"reason\":\"判斷依據\"}");
        sb.AppendLine("- needs_demi: true = 有 Dashboard UI 變更（新頁面/元件/Layout）；false = 純後端/API/DB 調整");
        return sb.ToString();
    }

    private static string BuildDesignRosaPreWorkPrompt(string taskPlan)
    {
        var sb = new StringBuilder();
        sb.AppendLine("你是 Rosa，負責需求分析的 AI 團隊成員，正在進行設計前置作業。");
        sb.AppendLine();
        sb.AppendLine("## 任務計劃書（Kick-off 會議產出）");
        sb.AppendLine(taskPlan);
        sb.AppendLine();
        sb.AppendLine("## 你的職責");
        sb.AppendLine("基於任務計劃書，探索 codebase 並拆解出具體的 GitHub Issues。");
        sb.AppendLine("每個 Issue 代表一個可獨立執行的功能或任務，粒度適中。");
        sb.AppendLine();
        sb.AppendLine("請在回應最後輸出 Issues JSON Array（格式如下，不加 code block）：");
        sb.AppendLine("[{\"title\":\"動詞開頭的具體標題（繁體中文）\",\"body\":\"## 背景\\n...\\n## 驗收條件\\n- [ ] 條件一\",\"labels\":[\"feature\",\"P1\"]}]");
        return sb.ToString();
    }

    private static string BuildDesignDemiPreWorkPrompt(string taskPlan, string issuesJson)
    {
        var sb = new StringBuilder();
        sb.AppendLine("你是 Demi，負責 UI/UX 設計的 AI 團隊成員，正在進行設計前置作業。");
        sb.AppendLine();
        sb.AppendLine("## 任務計劃書（Kick-off 會議產出）");
        sb.AppendLine(taskPlan);
        sb.AppendLine();
        sb.AppendLine("## Rosa 拆解的 Issues");
        sb.AppendLine(issuesJson);
        sb.AppendLine();
        sb.AppendLine("## 你的職責");
        sb.AppendLine("基於任務計劃書和 Issues，探索現有 Dashboard（Blazor/MudBlazor）元件後，");
        sb.AppendLine("產出完整的 UI/UX 規格文件（Markdown 格式）。");
        sb.AppendLine("需包含：頁面結構、元件清單、互動說明、MudBlazor 元件建議。");
        return sb.ToString();
    }

    private static string BuildDesignRosaMeetingPrompt(string issuesJson, int round, string? lastPetraOutput)
    {
        var sb = new StringBuilder();
        if (round == 1)
        {
            sb.AppendLine("設計會議第 1 輪開始。你在前置作業中產出了以下 Issues：");
            sb.AppendLine(issuesJson);
            sb.AppendLine();
            sb.AppendLine("請簡要說明你的需求拆分理由，以及對整體設計方向的想法。");
        }
        else if (!string.IsNullOrWhiteSpace(lastPetraOutput))
        {
            sb.AppendLine($"設計會議第 {round} 輪。Petra 上一輪整理的討論點：");
            sb.AppendLine(lastPetraOutput);
            sb.AppendLine();
            sb.AppendLine("請針對以上討論點，補充或修正你對 Issues 拆分的說明。");
        }
        sb.AppendLine();
        sb.AppendLine("請直接發表意見，不需要執行任何修改。");
        return sb.ToString();
    }

    private static string BuildDesignDemiMeetingPrompt(string uiSpec, int round, string? lastPetraOutput)
    {
        var sb = new StringBuilder();
        if (round == 1)
        {
            sb.AppendLine("設計會議第 1 輪開始。你在前置作業中產出了 UI/UX 規格。");
            sb.AppendLine("請簡要說明你的設計決策理由，以及對 UI 設計方向的想法。");
        }
        else if (!string.IsNullOrWhiteSpace(lastPetraOutput))
        {
            sb.AppendLine($"設計會議第 {round} 輪。Petra 上一輪整理的討論點：");
            sb.AppendLine(lastPetraOutput);
            sb.AppendLine();
            sb.AppendLine("請針對以上討論點，補充或修正你對 UI 規格的說明。");
        }
        sb.AppendLine();
        sb.AppendLine("請直接發表意見，不需要執行任何修改。");
        return sb.ToString();
    }

    private static string BuildDesignCodyPrompt(string issuesJson, string? uiSpec, int round, string? lastPetraOutput)
    {
        var sb = new StringBuilder();
        sb.AppendLine("你是 Cody，負責後端開發，正在參加設計會議。");
        sb.AppendLine();
        if (round == 1)
        {
            sb.AppendLine("## Rosa 拆解的 Issues");
            sb.AppendLine(issuesJson);
            sb.AppendLine();
            if (!string.IsNullOrWhiteSpace(uiSpec))
            {
                sb.AppendLine("## Demi 的 UI/UX 規格");
                sb.AppendLine(uiSpec);
                sb.AppendLine();
            }
            sb.AppendLine("## 你的職責");
            sb.AppendLine("從開發者角度，評估 Issues 拆分的合理性和技術可行性。");
            sb.AppendLine("請讀取相關 codebase 確認現有架構，指出 Issues 間的依賴關係、技術風險、潛在實作困難。");
        }
        else if (!string.IsNullOrWhiteSpace(lastPetraOutput))
        {
            sb.AppendLine("## Petra 上一輪整理的討論點");
            sb.AppendLine(lastPetraOutput);
            sb.AppendLine();
            sb.AppendLine("請針對以上討論點，補充你的技術評估意見。如需讀取 code 確認，請直接讀取。");
        }
        sb.AppendLine();
        sb.AppendLine("請直接列出你的評估，不需要執行任何實作工作（不要寫程式碼）。");
        return sb.ToString();
    }

    private static string BuildDesignQuinnPrompt(string issuesJson, string? uiSpec, int round, string? lastPetraOutput)
    {
        var sb = new StringBuilder();
        sb.AppendLine("你是 Quinn，負責 QA 測試，正在參加設計會議。");
        sb.AppendLine();
        if (round == 1)
        {
            sb.AppendLine("## Rosa 拆解的 Issues");
            sb.AppendLine(issuesJson);
            sb.AppendLine();
            if (!string.IsNullOrWhiteSpace(uiSpec))
            {
                sb.AppendLine("## Demi 的 UI/UX 規格");
                sb.AppendLine(uiSpec);
                sb.AppendLine();
            }
            sb.AppendLine("## 你的職責");
            sb.AppendLine("從 QA 角度，評估 Issues 的可測試性。");
            sb.AppendLine("請指出：哪些 Issues 難以自動化測試？需要什麼測試策略？有什麼潛在的測試盲點？");
        }
        else if (!string.IsNullOrWhiteSpace(lastPetraOutput))
        {
            sb.AppendLine("## Petra 上一輪整理的討論點");
            sb.AppendLine(lastPetraOutput);
            sb.AppendLine();
            sb.AppendLine("請針對以上討論點，補充你的測試規劃意見。");
        }
        sb.AppendLine();
        sb.AppendLine("請直接列出你的評估，不需要執行任何實作工作。");
        return sb.ToString();
    }

    private static string BuildDesignPetraRoundPrompt(
        string rosaOutput, string demiOutput, string codyOutput, string quinnOutput,
        int round, bool hasDemi)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"你是 Petra，正在主持設計會議第 {round} 輪。");
        sb.AppendLine();
        sb.AppendLine($"## 第 {round} 輪各角色意見");
        sb.AppendLine();
        sb.AppendLine("### Rosa（需求分析）");
        sb.AppendLine(rosaOutput);
        sb.AppendLine();
        if (hasDemi && !string.IsNullOrEmpty(demiOutput))
        {
            sb.AppendLine("### Demi（UI/UX 設計）");
            sb.AppendLine(demiOutput);
            sb.AppendLine();
        }
        sb.AppendLine("### Cody（技術可行性）");
        sb.AppendLine(codyOutput);
        sb.AppendLine();
        sb.AppendLine("### Quinn（測試規劃）");
        sb.AppendLine(quinnOutput);
        sb.AppendLine();
        sb.AppendLine("## 你的職責");
        sb.AppendLine("整理以上所有意見，評估設計成果的合理性、可行性、可測試性。");
        sb.AppendLine("你可以讀取 codebase 確認技術細節。");
        sb.AppendLine();
        sb.AppendLine("在回應最後，輸出以下 JSON（單獨一行，不加 code block）：");
        sb.AppendLine("{\"decision\":\"consensus|needs_discussion|needs_adjustment|escalate\",\"summary\":\"整理摘要\",\"adjustment_targets\":[],\"adjustment_instructions\":{},\"escalate_reason\":\"\"}");
        sb.AppendLine();
        sb.AppendLine("decision 說明：");
        sb.AppendLine("- consensus：設計成果沒有重大問題，可繼續 → 不需要填 adjustment 欄位");
        sb.AppendLine("- needs_discussion：有重大分歧，需要再討論 → 不需要填 adjustment 欄位");
        sb.AppendLine("- needs_adjustment：Issues 或 UI 規格需要修改 → 填 adjustment_targets（\"rosa\"/\"demi\"）和 adjustment_instructions（key 為 \"rosa\"/\"demi\"，value 為修改指示）");
        sb.AppendLine("- escalate：發現根本性問題無法在團隊內解決，需要老闆介入 → 填 escalate_reason");
        return sb.ToString();
    }

    private static string BuildDesignPetraPlanPrompt(string taskPlan, string issuesJson, string? uiSpec)
    {
        var sb = new StringBuilder();
        sb.AppendLine("設計會議已結束。請基於完整的討論 context，產出設計規劃書。");
        sb.AppendLine();
        sb.AppendLine("格式如下（Markdown）：");
        sb.AppendLine("# 設計規劃書");
        sb.AppendLine("## 需求摘要");
        sb.AppendLine("{來自 TaskPlan 的任務摘要}");
        sb.AppendLine("## GitHub Issues 清單");
        sb.AppendLine("| # | Issue | 標題 | 說明 |");
        sb.AppendLine("|---|-------|------|------|");
        sb.AppendLine("## UI/UX 規格摘要（如適用）");
        sb.AppendLine("{Demi 的 UI 規格重點}");
        sb.AppendLine("## 設計決策");
        sb.AppendLine("- {設計會議中達成的共識}");
        sb.AppendLine("## 各角色意見摘要");
        sb.AppendLine("| 角色 | 主要意見 | 結論 |");
        sb.AppendLine("|------|---------|------|");
        sb.AppendLine("## 風險與注意事項");
        sb.AppendLine("- {設計會議中提出但未完全解決的項目}");
        sb.AppendLine("## 開發建議");
        sb.AppendLine("{基於設計審查的技術方向建議}");
        return sb.ToString();
    }

    // ---- 設計會議 JSON 解析 ----

    private static bool TryParseNeedsDemi(string output)
    {
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        for (var i = lines.Length - 1; i >= Math.Max(0, lines.Length - 5); i--)
        {
            var line = lines[i].Trim();
            if (!line.StartsWith('{')) continue;
            try
            {
                using var doc = JsonDocument.Parse(line);
                if (doc.RootElement.TryGetProperty("needs_demi", out var prop))
                    return prop.GetBoolean();
            }
            catch { /* 繼續往上找 */ }
        }
        return true; // 解析失敗時預設需要 Demi（保守策略）
    }

    private static List<DesignIssueDto>? TryParseDesignIssues(string content)
    {
        try
        {
            var start = content.IndexOf('[');
            var end   = content.LastIndexOf(']');
            if (start < 0 || end < 0) return null;
            var json = content[start..(end + 1)];
            return JsonSerializer.Deserialize<List<DesignIssueDto>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch { return null; }
    }

    private static DesignPetraDecision? TryParseDesignPetraDecision(string output)
    {
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        for (var i = lines.Length - 1; i >= Math.Max(0, lines.Length - 10); i--)
        {
            var line = lines[i].Trim();
            if (!line.StartsWith('{')) continue;
            try
            {
                return JsonSerializer.Deserialize<DesignPetraDecision>(line,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch { /* 繼續往上找 */ }
        }
        return null;
    }

    private static DesignAdjustmentEvaluation? TryParseDesignAdjustmentEvaluation(string output)
    {
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        for (var i = lines.Length - 1; i >= Math.Max(0, lines.Length - 5); i--)
        {
            var line = lines[i].Trim();
            if (!line.StartsWith('{')) continue;
            try
            {
                return JsonSerializer.Deserialize<DesignAdjustmentEvaluation>(line,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch { /* 繼續往上找 */ }
        }
        return null;
    }

    private static ModifyDecision? TryParseModifyDecision(string output)
    {
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        for (var i = lines.Length - 1; i >= Math.Max(0, lines.Length - 5); i--)
        {
            var line = lines[i].Trim();
            if (!line.StartsWith('{')) continue;
            try
            {
                return JsonSerializer.Deserialize<ModifyDecision>(line,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch { /* 繼續往上找 */ }
        }
        return null;
    }

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

// ---- Stage 25b：Design Phase Internal DTOs ----

internal record DesignPetraDecision(
    string   Decision,       // "consensus" | "needs_discussion" | "needs_adjustment" | "escalate"
    string   Summary,
    string[] AdjustmentTargets,
    Dictionary<string, string> AdjustmentInstructions,
    string?  EscalateReason);

internal record DesignAdjustmentEvaluation(
    string  Evaluation,      // "approved" | "needs_meeting"
    string? DesignPlan,
    string? Reason);

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

internal class DesignIssueDto
{
    public string       Title  { get; set; } = "";
    public string       Body   { get; set; } = "";
    public List<string> Labels { get; set; } = [];
}
