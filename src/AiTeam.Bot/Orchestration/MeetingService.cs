using System.Text;
using System.Text.Json;
using AiTeam.Bot.Agents;
using AiTeam.Bot.Configuration;
using AiTeam.Bot.GitHub;
using AiTeam.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace AiTeam.Bot.Orchestration;

/// <summary>
/// Stage 25a：多 Agent Kick-off 會議引擎。
/// 負責協調 5 位 Agent（Petra/Rosa/Demi/Cody/Quinn）的 Claude Code 持續對話 session，
/// 實現「開工前全員對齊需求」的多輪討論流程，並由 Petra 產出任務計劃書。
/// </summary>
public class MeetingService(
    IClaudeCodeService claudeCode,
    GitHubService gitHubService,
    IOptions<WorkflowSettings> workflowSettings,
    IOptions<GitHubSettings> gitHubSettings,
    IConfiguration configuration,
    ILogger<MeetingService> logger)
{
    private readonly WorkflowSettings _workflow = workflowSettings.Value;
    private readonly GitHubSettings   _gitHub   = gitHubSettings.Value;

    private static readonly string[] ReadOnlyTools = ["Glob", "Grep", "Read"];

    // ---- 會議執行 ----

    /// <summary>
    /// 執行 Kick-off 會議完整流程（最多 KickoffMaxRounds 輪）。
    /// Rosa/Demi/Cody/Quinn 並行發言，Petra 串行整理。
    /// 會議結束後由 Petra 產出任務計劃書。
    /// </summary>
    /// <param name="group">TaskGroup（用於取得 session ID 與任務資訊）。</param>
    /// <param name="proposalContent">Victoria 提案完整內容（需求說明）。</param>
    /// <param name="owner">GitHub owner。</param>
    /// <param name="repo">GitHub repo name。</param>
    /// <param name="ct">CancellationToken。</param>
    public async Task<MeetingResult> RunKickoffMeetingAsync(
        TaskGroup group,
        string proposalContent,
        string owner,
        string repo,
        CancellationToken ct = default)
    {
        var apiKey  = GetApiKey();
        var logBuilder = new StringBuilder();
        var totalRounds = 0;
        string? lastPetraOutput = null;

        // 各 Agent 的 session ID：
        // Petra 使用 group.Id（固定，供後續 Christ 修改流程 resume 使用）
        // Rosa/Demi/Cody/Quinn 使用臨時 GUID（本次會議獨用，不需 resume）
        var petraSessionId = group.Id.ToString();
        var rosaSessionId  = Guid.NewGuid().ToString();
        var demiSessionId  = Guid.NewGuid().ToString();
        var codySessionId  = Guid.NewGuid().ToString();
        var quinnSessionId = Guid.NewGuid().ToString();

        // Clone repo 供 Agent 探索 codebase（read-only clone）
        var workingDir = "";
        try
        {
            var cloneSuffix = "kickoff-" + group.Id.ToString("N")[..8];
            workingDir = gitHubService.CloneOrPull(owner, repo, cloneSuffix);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "MeetingService：Clone repo 失敗，使用 workspace 路徑作為 fallback");
            workingDir = Path.Combine(_gitHub.WorkspacePath, repo);
        }

        // 開始會議紀錄
        logBuilder.AppendLine("# Kick-off 會議紀錄");
        logBuilder.AppendLine();
        logBuilder.AppendLine("## 需求說明");
        logBuilder.AppendLine(proposalContent);
        logBuilder.AppendLine();

        try
        {
            for (var round = 1; round <= _workflow.KickoffMaxRounds; round++)
            {
                totalRounds = round;
                var isFirstMessage = round == 1;
                logger.LogInformation("MeetingService：Kick-off 第 {Round} 輪開始（groupId={Id}）", round, group.Id);

                logBuilder.AppendLine($"## Round {round}");
                logBuilder.AppendLine();

                // ── 步驟 1：Rosa/Demi/Cody/Quinn 並行發言 ──
                var rosaTask  = RunAgentTurnAsync("Rosa",  rosaSessionId,
                    BuildRosaPrompt(proposalContent, round, lastPetraOutput),
                    GetModel("Requirements"), apiKey, isFirstMessage, workingDir, ReadOnlyTools, ct);
                var demiTask  = RunAgentTurnAsync("Demi",  demiSessionId,
                    BuildDemiPrompt(proposalContent, round, lastPetraOutput),
                    GetModel("Designer"), apiKey, isFirstMessage, workingDir, ReadOnlyTools, ct);
                var codyTask  = RunAgentTurnAsync("Cody",  codySessionId,
                    BuildCodyPrompt(proposalContent, round, lastPetraOutput),
                    GetModel("Dev"), apiKey, isFirstMessage, workingDir, allowedTools: null, ct);
                var quinnTask = RunAgentTurnAsync("Quinn", quinnSessionId,
                    BuildQuinnPrompt(proposalContent, round, lastPetraOutput),
                    GetModel("QA"), apiKey, isFirstMessage, workingDir, ReadOnlyTools, ct);

                await Task.WhenAll(rosaTask, demiTask, codyTask, quinnTask);

                var rosaOutput  = rosaTask.Result;
                var demiOutput  = demiTask.Result;
                var codyOutput  = codyTask.Result;
                var quinnOutput = quinnTask.Result;

                // 記錄各 Agent 意見
                logBuilder.AppendLine("### Rosa（需求分析）");
                logBuilder.AppendLine(rosaOutput);
                logBuilder.AppendLine();
                logBuilder.AppendLine("### Demi（UI/UX 設計）");
                logBuilder.AppendLine(demiOutput);
                logBuilder.AppendLine();
                logBuilder.AppendLine("### Cody（技術可行性）");
                logBuilder.AppendLine(codyOutput);
                logBuilder.AppendLine();
                logBuilder.AppendLine("### Quinn（測試規劃）");
                logBuilder.AppendLine(quinnOutput);
                logBuilder.AppendLine();

                // ── 步驟 2：Petra 整理並判斷 ──
                var petraPrompt = BuildPetraRoundPrompt(rosaOutput, demiOutput, codyOutput, quinnOutput, round);
                var petraOutput = await RunAgentTurnAsync("Petra", petraSessionId,
                    petraPrompt, GetModel("PM"), apiKey, isFirstMessage, workingDir, ReadOnlyTools, ct);
                lastPetraOutput = petraOutput;

                logBuilder.AppendLine("### Petra（綜合整理）");
                logBuilder.AppendLine(petraOutput);
                logBuilder.AppendLine();

                // 解析 Petra 的判斷 JSON
                var decision = TryParsePetraDecision(petraOutput);

                if (decision is null)
                {
                    logger.LogWarning("MeetingService：Petra 第 {Round} 輪回應無法解析 decision JSON，假設 consensus", round);
                    break;
                }

                logger.LogInformation("MeetingService：Petra 第 {Round} 輪 decision={Decision}", round, decision.Decision);

                if (decision.Decision == "consensus")
                    break;

                if (decision.Decision == "escalate")
                {
                    // 上呈 Christ 處理分歧（但繼續產出計劃書）
                    logger.LogWarning("MeetingService：Petra 判斷需上呈 Christ（groupId={Id}）", group.Id);
                    break;
                }

                // needs_discussion → 繼續下一輪
                if (round == _workflow.KickoffMaxRounds)
                {
                    logger.LogInformation("MeetingService：已達最大輪次 {Max}，強制結束", _workflow.KickoffMaxRounds);
                }
            }

            // ── 步驟 3：Petra 產出任務計劃書（Petra session 保留，供 Christ 修改流程使用）──
            var planPrompt = BuildPetraPlanPrompt();
            var taskPlan = await RunAgentTurnAsync("Petra", petraSessionId,
                planPrompt, GetModel("PM"), apiKey, isFirstMessage: false, workingDir, ReadOnlyTools, ct);

            logBuilder.AppendLine("## 任務計劃書");
            logBuilder.AppendLine(taskPlan);
            logBuilder.AppendLine();

            logger.LogInformation("MeetingService：Kick-off 會議完成（groupId={Id}，rounds={Rounds}）",
                group.Id, totalRounds);

            return new MeetingResult(
                Success:      true,
                MeetingLog:   logBuilder.ToString(),
                TaskPlan:     taskPlan,
                TotalRounds:  totalRounds);
        }
        finally
        {
            // 清理 clone（Petra session 資料由 Claude Code 本機管理，不在 workingDir 中）
            if (!string.IsNullOrEmpty(workingDir))
            {
                try { gitHubService.CleanupLocalRepo(workingDir); }
                catch (Exception ex) { logger.LogWarning(ex, "MeetingService：cleanup workingDir 失敗"); }
            }
        }
    }

    // ---- 計劃書修改 ----

    /// <summary>
    /// Stage 25a：Christ 要求修改計劃書時，resume Petra 的既有 session（含完整會議 context）。
    /// Petra session ID = group.Id，不需額外儲存。
    /// </summary>
    public async Task<ModifyResult> ModifyTaskPlanAsync(
        TaskGroup group,
        string christFeedback,
        string owner,
        string repo,
        CancellationToken ct = default)
    {
        var petraSessionId = group.Id.ToString();
        var apiKey = GetApiKey();

        // 使用一個短暫的 workingDir（Petra 可能要讀 code 驗證修改影響）
        var workingDir = "";
        try
        {
            var cloneSuffix = "kickoff-modify-" + group.Id.ToString("N")[..8];
            workingDir = gitHubService.CloneOrPull(owner, repo, cloneSuffix);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "MeetingService：ModifyTaskPlan clone repo 失敗，使用 workspace fallback");
            workingDir = Path.Combine(_gitHub.WorkspacePath, repo);
        }

        try
        {
            var prompt =
                $"老闆要求修改任務計劃書，修改意見如下：\n\n{christFeedback}\n\n" +
                $"請基於完整的會議討論 context 評估修改影響，並在回應最後輸出以下 JSON（不要有其他格式）：\n" +
                $"{{\"impact\":\"small|large\",\"revised_plan\":\"（small 時輸出完整修改後計劃書，large 時留空）\"}}";

            var petraOutput = await RunAgentTurnAsync("Petra", petraSessionId,
                prompt, GetModel("PM"), apiKey, isFirstMessage: false, workingDir, ReadOnlyTools, ct);

            logger.LogInformation("MeetingService：ModifyTaskPlan Petra 回應完成（groupId={Id}）", group.Id);

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
                catch (Exception ex) { logger.LogWarning(ex, "MeetingService：ModifyTaskPlan cleanup 失敗"); }
            }
        }
    }

    // ---- 設計規劃階段 ----

    /// <summary>
    /// Stage 25b：執行設計規劃階段完整流程。
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
            logger.LogWarning(ex, "MeetingService：Design clone repo 失敗，使用 workspace fallback");
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
            var petraJudgeOutput = await RunAgentTurnAsync("Petra", sessions.PetraSessionId,
                BuildDesignPetraJudgePrompt(taskPlan),
                GetModel("PM"), apiKey, isFirstMessage: true, workingDir, ReadOnlyTools, ct);

            logBuilder.AppendLine("### Petra — 設計需求判斷");
            logBuilder.AppendLine(petraJudgeOutput);
            logBuilder.AppendLine();

            var needsDemi = TryParseNeedsDemi(petraJudgeOutput);
            logger.LogInformation("MeetingService：設計階段 needsDemi={NeedsDemi}（groupId={Id}）", needsDemi, group.Id);

            // Rosa 產出 Issues（isFirstMessage: true，maxTurns: 25）
            var rosaPreWorkOutput = await RunAgentTurnAsync("Rosa", sessions.RosaSessionId,
                BuildDesignRosaPreWorkPrompt(taskPlan),
                GetModel("Requirements"), apiKey, isFirstMessage: true, workingDir, ReadOnlyTools, ct,
                maxTurns: 25);

            logBuilder.AppendLine("### Rosa — GitHub Issues");
            logBuilder.AppendLine(rosaPreWorkOutput);
            logBuilder.AppendLine();

            // 解析 Issues 並建立 GitHub Issues（MockMode fallback: 解析失敗則跳過）
            var parsedIssues = TryParseDesignIssues(rosaPreWorkOutput);
            if (parsedIssues is { Count: > 0 })
            {
                issuesJson = System.Text.Json.JsonSerializer.Serialize(parsedIssues);
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
                        logger.LogWarning(ex, "MeetingService：建立 GitHub Issue 失敗：{Title}", issue.Title);
                    }
                }
                if (issueUrlList.Count > 0)
                    issueUrls = System.Text.Json.JsonSerializer.Serialize(issueUrlList);
            }
            else
            {
                logger.LogWarning("MeetingService：Rosa Issues 無法解析，跳過 GitHub Issue 建立（groupId={Id}）", group.Id);
            }

            // Demi 產出 UI 規格（條件式，isFirstMessage: true，maxTurns: 25）
            if (needsDemi)
            {
                sessions.DemiSessionId = Guid.NewGuid().ToString();
                var demiPreWorkOutput = await RunAgentTurnAsync("Demi", sessions.DemiSessionId,
                    BuildDesignDemiPreWorkPrompt(taskPlan, issuesJson),
                    GetModel("Designer"), apiKey, isFirstMessage: true, workingDir, ReadOnlyTools, ct,
                    maxTurns: 25);

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
            for (var round = 1; round <= _workflow.DesignMeetingMaxRounds; round++)
            {
                totalRounds = round;
                logger.LogInformation("MeetingService：設計會議第 {Round} 輪開始（groupId={Id}）", round, group.Id);

                logBuilder.AppendLine($"## Round {round}");
                logBuilder.AppendLine();

                // Rosa/Demi：resume 前置作業 session（isFirstMessage: false）
                // Cody/Quinn：第 1 輪新建（isFirstMessage: true），後續輪 resume（false）
                var isFirstRound = round == 1;

                var rosaMeetingTask = RunAgentTurnAsync("Rosa", sessions.RosaSessionId,
                    BuildDesignRosaMeetingPrompt(issuesJson, round, lastPetraOutput),
                    GetModel("Requirements"), apiKey, isFirstMessage: false, workingDir, ReadOnlyTools, ct);

                Task<string>? demiMeetingTask = null;
                if (sessions.DemiSessionId is not null)
                    demiMeetingTask = RunAgentTurnAsync("Demi", sessions.DemiSessionId,
                        BuildDesignDemiMeetingPrompt(uiSpecContent ?? "", round, lastPetraOutput),
                        GetModel("Designer"), apiKey, isFirstMessage: false, workingDir, ReadOnlyTools, ct);

                var codyMeetingTask = RunAgentTurnAsync("Cody", sessions.CodySessionId,
                    BuildDesignCodyPrompt(issuesJson, uiSpecContent, round, lastPetraOutput),
                    GetModel("Dev"), apiKey, isFirstMessage: isFirstRound, workingDir, allowedTools: null, ct);
                var quinnMeetingTask = RunAgentTurnAsync("Quinn", sessions.QuinnSessionId,
                    BuildDesignQuinnPrompt(issuesJson, uiSpecContent, round, lastPetraOutput),
                    GetModel("QA"), apiKey, isFirstMessage: isFirstRound, workingDir, ReadOnlyTools, ct);

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
                var petraOutput = await RunAgentTurnAsync("Petra", sessions.PetraSessionId,
                    petraRoundPrompt, GetModel("PM"), apiKey, isFirstMessage: false, workingDir, ReadOnlyTools, ct);
                lastPetraOutput = petraOutput;

                logBuilder.AppendLine("### Petra（綜合整理）");
                logBuilder.AppendLine(petraOutput);
                logBuilder.AppendLine();

                var decision = TryParseDesignPetraDecision(petraOutput);

                if (decision is null)
                {
                    // 無法解析 → 視同 consensus（MockMode fallback）
                    logger.LogWarning("MeetingService：設計會議 Petra 第 {Round} 輪無法解析 decision，假設 consensus", round);
                    finalDesignPlan = await GenerateDesignPlanAsync(
                        sessions.PetraSessionId, taskPlan, issuesJson, uiSpecContent, workingDir, apiKey, ct);
                    logBuilder.AppendLine("## 設計規劃書");
                    logBuilder.AppendLine(finalDesignPlan);
                    break;
                }

                logger.LogInformation("MeetingService：設計會議第 {Round} 輪 decision={Decision}", round, decision.Decision);

                if (decision.Decision == "consensus")
                {
                    finalDesignPlan = await GenerateDesignPlanAsync(
                        sessions.PetraSessionId, taskPlan, issuesJson, uiSpecContent, workingDir, apiKey, ct);
                    logBuilder.AppendLine("## 設計規劃書");
                    logBuilder.AppendLine(finalDesignPlan);
                    break;
                }

                if (decision.Decision == "escalate")
                {
                    finalDecision  = "escalate";
                    escalateReason = decision.EscalateReason;
                    logger.LogWarning("MeetingService：設計會議需上呈 Christ（groupId={Id}）", group.Id);
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
                    if (totalRounds > _workflow.DesignMeetingMaxRounds)
                    {
                        finalDecision  = "escalate";
                        escalateReason = "多輪調整後仍需重開會議，已達設計會議上限";
                        logger.LogInformation("MeetingService：設計會議調整後超過上限，escalate（groupId={Id}）", group.Id);
                        break;
                    }

                    // 繼續外層迴圈（以更新後的 issuesJson/uiSpec 重開下一輪）
                    lastPetraOutput = null;
                    continue;
                }

                // needs_discussion：繼續下一輪
                if (round == _workflow.DesignMeetingMaxRounds)
                {
                    logger.LogInformation("MeetingService：設計會議已達最大輪次 {Max}，強制結束", _workflow.DesignMeetingMaxRounds);
                    finalDesignPlan = await GenerateDesignPlanAsync(
                        sessions.PetraSessionId, taskPlan, issuesJson, uiSpecContent, workingDir, apiKey, ct);
                    logBuilder.AppendLine("## 設計規劃書");
                    logBuilder.AppendLine(finalDesignPlan);
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
                EscalateReason: escalateReason);
        }
        finally
        {
            if (!string.IsNullOrEmpty(workingDir))
            {
                try { gitHubService.CleanupLocalRepo(workingDir); }
                catch (Exception ex) { logger.LogWarning(ex, "MeetingService：Design cleanup workingDir 失敗"); }
            }
        }
    }

    /// <summary>
    /// Stage 25b：執行設計會議調整流程（needs_adjustment 路徑）。
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
        logBuilder.AppendLine("## 調整紀錄");
        logBuilder.AppendLine();
        logBuilder.AppendLine("### Petra 修改指示");
        logBuilder.AppendLine(System.Text.Json.JsonSerializer.Serialize(decision.AdjustmentInstructions,
            new System.Text.Json.JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
        logBuilder.AppendLine();

        var updatedRosaOutput = "";
        var updatedDemiOutput = "";
        var updatedIssuesJson = "";
        var updatedIssueUrls  = "";

        // Rosa 調整
        if (decision.AdjustmentTargets.Any(t => t.Equals("rosa", StringComparison.OrdinalIgnoreCase)))
        {
            var instruction = decision.AdjustmentInstructions.GetValueOrDefault("rosa", "請根據會議討論修改 Issues");
            var prompt = $"Petra 的修改指示：\n\n{instruction}\n\n請調整你的 GitHub Issues，在回應最後以 JSON Array 格式輸出更新後的完整 Issues 清單（格式同前置作業）。";
            updatedRosaOutput = await RunAgentTurnAsync("Rosa", sessions.RosaSessionId,
                prompt, GetModel("Requirements"), apiKey, isFirstMessage: false, workingDir, ReadOnlyTools, ct);

            logBuilder.AppendLine("### Rosa 調整結果");
            logBuilder.AppendLine(updatedRosaOutput);
            logBuilder.AppendLine();

            // 更新 GitHub Issues
            var updatedParsed = TryParseDesignIssues(updatedRosaOutput);
            if (updatedParsed is { Count: > 0 })
            {
                updatedIssuesJson = System.Text.Json.JsonSerializer.Serialize(updatedParsed);
                var urlList = new List<string>();
                foreach (var issue in updatedParsed)
                {
                    try
                    {
                        var url = await gitHubService.CreateIssueAsync(owner, repo, issue.Title, issue.Body, issue.Labels);
                        urlList.Add(url);
                    }
                    catch (Exception ex) { logger.LogWarning(ex, "MeetingService：調整後 Issue 建立失敗：{Title}", issue.Title); }
                }
                if (urlList.Count > 0)
                    updatedIssueUrls = System.Text.Json.JsonSerializer.Serialize(urlList);
            }
        }

        // Demi 調整
        if (decision.AdjustmentTargets.Any(t => t.Equals("demi", StringComparison.OrdinalIgnoreCase)))
        {
            var instruction = decision.AdjustmentInstructions.GetValueOrDefault("demi", "請根據會議討論修改 UI 規格");

            if (sessions.DemiSessionId is null)
            {
                // 邊界案例：初始不需要 Demi，但設計會議中發現需要 UI 規格
                sessions.DemiSessionId = Guid.NewGuid().ToString();
                var createPrompt =
                    $"你是 Demi，負責 UI/UX 設計。設計會議發現此任務需要 UI 規格。\n\n" +
                    $"## 任務計劃書\n{taskPlan}\n\n" +
                    $"## Petra 的指示\n{instruction}\n\n" +
                    $"請探索相關 codebase 後，產出完整的 UI/UX 規格（Markdown 格式）。";
                updatedDemiOutput = await RunAgentTurnAsync("Demi", sessions.DemiSessionId,
                    createPrompt, GetModel("Designer"), apiKey, isFirstMessage: true, workingDir, ReadOnlyTools, ct,
                    maxTurns: 25);
            }
            else
            {
                var adjustPrompt = $"Petra 的修改指示：\n\n{instruction}\n\n請調整你的 UI 規格，回應更新後的完整 UI/UX 規格（Markdown 格式）。";
                updatedDemiOutput = await RunAgentTurnAsync("Demi", sessions.DemiSessionId,
                    adjustPrompt, GetModel("Designer"), apiKey, isFirstMessage: false, workingDir, ReadOnlyTools, ct);
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

        var petraEvalOutput = await RunAgentTurnAsync("Petra", sessions.PetraSessionId,
            sb.ToString(), GetModel("PM"), apiKey, isFirstMessage: false, workingDir, ReadOnlyTools, ct);

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
                    sessions.PetraSessionId, taskPlan, updatedIssuesJson, updatedDemiOutput, workingDir, apiKey, ct);

            return new DesignAdjustmentResult(
                Approved:         true,
                Escalate:         false,
                DesignPlan:       designPlan,
                UpdatedIssueUrls: updatedIssueUrls,
                UpdatedIssuesJson: updatedIssuesJson,
                UpdatedUiSpec:    string.IsNullOrEmpty(updatedDemiOutput) ? null : updatedDemiOutput);
        }

        return new DesignAdjustmentResult(
            Approved:         false,
            Escalate:         false,
            DesignPlan:       null,
            UpdatedIssueUrls: updatedIssueUrls,
            UpdatedIssuesJson: updatedIssuesJson,
            UpdatedUiSpec:    string.IsNullOrEmpty(updatedDemiOutput) ? null : updatedDemiOutput);
    }

    /// <summary>
    /// Stage 25b：Christ 要求修改設計規劃書時，resume Petra 的設計 session（含完整設計 context）。
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
            logger.LogWarning(ex, "MeetingService：ModifyDesignPlan clone repo 失敗，使用 workspace fallback");
            workingDir = Path.Combine(_gitHub.WorkspacePath, repo);
        }

        try
        {
            var prompt =
                $"老闆要求修改設計規劃書，修改意見如下：\n\n{christFeedback}\n\n" +
                $"請基於完整的設計會議 context 評估修改影響，並在回應最後輸出以下 JSON（不要有其他格式）：\n" +
                $"{{\"impact\":\"small|large\",\"revised_plan\":\"（small 時輸出完整修改後設計規劃書，large 時留空）\"}}";

            var petraOutput = await RunAgentTurnAsync("Petra", petraSessionId,
                prompt, GetModel("PM"), apiKey, isFirstMessage: false, workingDir, ReadOnlyTools, ct);

            logger.LogInformation("MeetingService：ModifyDesignPlan Petra 回應完成（groupId={Id}）", group.Id);

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
                catch (Exception ex) { logger.LogWarning(ex, "MeetingService：ModifyDesignPlan cleanup 失敗"); }
            }
        }
    }

    /// <summary>Petra 產出最終設計規劃書。</summary>
    private async Task<string> GenerateDesignPlanAsync(
        string petraSessionId,
        string taskPlan,
        string issuesJson,
        string? uiSpecContent,
        string workingDir,
        string apiKey,
        CancellationToken ct)
    {
        var prompt = BuildDesignPetraPlanPrompt(taskPlan, issuesJson, uiSpecContent);
        return await RunAgentTurnAsync("Petra", petraSessionId,
            prompt, GetModel("PM"), apiKey, isFirstMessage: false, workingDir, ReadOnlyTools, ct);
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
                using var doc = System.Text.Json.JsonDocument.Parse(line);
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
            return System.Text.Json.JsonSerializer.Deserialize<List<DesignIssueDto>>(json,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch { return null; }
    }

    private DesignPetraDecision? TryParseDesignPetraDecision(string output)
    {
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        for (var i = lines.Length - 1; i >= Math.Max(0, lines.Length - 10); i--)
        {
            var line = lines[i].Trim();
            if (!line.StartsWith('{')) continue;
            try
            {
                return System.Text.Json.JsonSerializer.Deserialize<DesignPetraDecision>(line,
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch { /* 繼續往上找 */ }
        }
        return null;
    }

    private DesignAdjustmentEvaluation? TryParseDesignAdjustmentEvaluation(string output)
    {
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        for (var i = lines.Length - 1; i >= Math.Max(0, lines.Length - 5); i--)
        {
            var line = lines[i].Trim();
            if (!line.StartsWith('{')) continue;
            try
            {
                return System.Text.Json.JsonSerializer.Deserialize<DesignAdjustmentEvaluation>(line,
                    new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch { /* 繼續往上找 */ }
        }
        return null;
    }

    /// <summary>
    /// Stage 25a：Christ 確認（繼續/停止）後呼叫，記錄 Petra session 已完成。
    /// Claude Code session 資料由本機自行管理（不需要主動刪除）。
    /// </summary>
    public Task CloseAllSessionsAsync(Guid groupId)
    {
        // Claude Code session 資料儲存於本機（~/.claude/sessions/），不需要主動清理
        // 僅記錄 log 供 debug 追蹤
        logger.LogInformation("MeetingService：Petra session 關閉（groupId={Id}，sessionId={SessionId}）",
            groupId, groupId.ToString());
        return Task.CompletedTask;
    }

    // ---- 輔助方法 ----

    private async Task<string> RunAgentTurnAsync(
        string agentDisplayName,
        string sessionId,
        string prompt,
        string model,
        string apiKey,
        bool isFirstMessage,
        string workingDir,
        string[]? allowedTools,
        CancellationToken ct,
        int maxTurns = 12)
    {
        try
        {
            var result = await claudeCode.RunMeetingSessionAsync(
                workingDir, sessionId, prompt, model, apiKey,
                isFirstMessage, maxTurns, allowedTools, ct);

            if (!result.Success)
                logger.LogWarning("MeetingService：{Agent} session 執行失敗（sessionId={Id}）", agentDisplayName, sessionId);

            return string.IsNullOrWhiteSpace(result.Output)
                ? $"（{agentDisplayName} 無回應）"
                : result.Output;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "MeetingService：{Agent} session 例外（sessionId={Id}）", agentDisplayName, sessionId);
            return $"（{agentDisplayName} 執行失敗：{ex.Message}）";
        }
    }

    // ---- Prompt 建立 ----

    private static string BuildRosaPrompt(string proposal, int round, string? previousPetraOutput)
    {
        var sb = new StringBuilder();
        sb.AppendLine("你是 Rosa，負責需求分析的 AI 團隊成員，正在參加 Kick-off 會議。");
        sb.AppendLine();
        sb.AppendLine("## 任務需求說明");
        sb.AppendLine(proposal);
        sb.AppendLine();

        if (round > 1 && !string.IsNullOrWhiteSpace(previousPetraOutput))
        {
            sb.AppendLine("## Petra 上一輪整理的討論點");
            sb.AppendLine(previousPetraOutput);
            sb.AppendLine();
            sb.AppendLine("## 本輪請回應");
            sb.AppendLine("針對上述討論點，補充或修正你的需求分析意見。");
        }
        else
        {
            sb.AppendLine("## 你的職責");
            sb.AppendLine("從需求分析師角度，評估此需求的完整性。");
            sb.AppendLine("請指出：有哪些模糊之處？有哪些矛盾？缺少什麼關鍵資訊？");
            sb.AppendLine("你可以讀取 codebase 中的相關檔案來了解現有設計。");
        }

        sb.AppendLine();
        sb.AppendLine("請直接列出你的意見，不需要執行任何實作工作。");
        return sb.ToString();
    }

    private static string BuildDemiPrompt(string proposal, int round, string? previousPetraOutput)
    {
        var sb = new StringBuilder();
        sb.AppendLine("你是 Demi，負責 UI/UX 設計的 AI 團隊成員，正在參加 Kick-off 會議。");
        sb.AppendLine();
        sb.AppendLine("## 任務需求說明");
        sb.AppendLine(proposal);
        sb.AppendLine();

        if (round > 1 && !string.IsNullOrWhiteSpace(previousPetraOutput))
        {
            sb.AppendLine("## Petra 上一輪整理的討論點");
            sb.AppendLine(previousPetraOutput);
            sb.AppendLine();
            sb.AppendLine("## 本輪請回應");
            sb.AppendLine("針對上述討論點，補充或修正你的 UI/UX 評估意見。");
        }
        else
        {
            sb.AppendLine("## 你的職責");
            sb.AppendLine("從 UI/UX 設計師角度，評估此需求對現有 Dashboard 的影響。");
            sb.AppendLine("請指出：會影響哪些現有頁面或元件？有哪些設計疑慮？");
            sb.AppendLine("你可以讀取 Dashboard 相關的 Blazor 元件檔案來了解現有設計。");
        }

        sb.AppendLine();
        sb.AppendLine("請直接列出你的意見，不需要執行任何實作工作。");
        return sb.ToString();
    }

    private static string BuildCodyPrompt(string proposal, int round, string? previousPetraOutput)
    {
        var sb = new StringBuilder();
        sb.AppendLine("你是 Cody，負責後端開發的 AI 團隊成員，正在參加 Kick-off 會議。");
        sb.AppendLine();
        sb.AppendLine("## 任務需求說明");
        sb.AppendLine(proposal);
        sb.AppendLine();

        if (round > 1 && !string.IsNullOrWhiteSpace(previousPetraOutput))
        {
            sb.AppendLine("## Petra 上一輪整理的討論點");
            sb.AppendLine(previousPetraOutput);
            sb.AppendLine();
            sb.AppendLine("## 本輪請回應");
            sb.AppendLine("針對上述討論點，補充或修正你的技術可行性評估。如需讀取 code 確認，請直接讀取。");
        }
        else
        {
            sb.AppendLine("## 你的職責");
            sb.AppendLine("從開發者角度，評估此需求的技術可行性。");
            sb.AppendLine("請讀取相關 codebase 確認現有架構是否支援此功能，指出技術風險與實作難點。");
        }

        sb.AppendLine();
        sb.AppendLine("請直接列出你的評估，不需要執行任何實作工作（不要寫程式碼）。");
        return sb.ToString();
    }

    private static string BuildQuinnPrompt(string proposal, int round, string? previousPetraOutput)
    {
        var sb = new StringBuilder();
        sb.AppendLine("你是 Quinn，負責 QA 測試的 AI 團隊成員，正在參加 Kick-off 會議。");
        sb.AppendLine();
        sb.AppendLine("## 任務需求說明");
        sb.AppendLine(proposal);
        sb.AppendLine();

        if (round > 1 && !string.IsNullOrWhiteSpace(previousPetraOutput))
        {
            sb.AppendLine("## Petra 上一輪整理的討論點");
            sb.AppendLine(previousPetraOutput);
            sb.AppendLine();
            sb.AppendLine("## 本輪請回應");
            sb.AppendLine("針對上述討論點，補充或修正你的測試可行性評估。");
        }
        else
        {
            sb.AppendLine("## 你的職責");
            sb.AppendLine("從 QA 角度，評估此需求的可測試性。");
            sb.AppendLine("請指出：哪些部分難以自動化測試？需要什麼測試策略？有什麼潛在的測試盲點？");
        }

        sb.AppendLine();
        sb.AppendLine("請直接列出你的評估，不需要執行任何實作工作。");
        return sb.ToString();
    }

    private static string BuildPetraRoundPrompt(
        string rosaOutput, string demiOutput, string codyOutput, string quinnOutput, int round)
    {
        var sb = new StringBuilder();
        sb.AppendLine("你是 Petra，AI 團隊的 PM，正在主持 Kick-off 會議。");
        sb.AppendLine();
        sb.AppendLine($"## 第 {round} 輪各角色意見");
        sb.AppendLine();
        sb.AppendLine("### Rosa（需求分析）");
        sb.AppendLine(rosaOutput);
        sb.AppendLine();
        sb.AppendLine("### Demi（UI/UX 設計）");
        sb.AppendLine(demiOutput);
        sb.AppendLine();
        sb.AppendLine("### Cody（技術可行性）");
        sb.AppendLine(codyOutput);
        sb.AppendLine();
        sb.AppendLine("### Quinn（測試規劃）");
        sb.AppendLine(quinnOutput);
        sb.AppendLine();
        sb.AppendLine("## 你的職責");
        sb.AppendLine("整理以上所有意見，判斷是否有需要進一步討論的重大分歧。");
        sb.AppendLine("你可以讀取 codebase 確認技術細節的準確性。");
        sb.AppendLine();
        sb.AppendLine("在回應最後，輸出以下 JSON（單獨一行，不要包在 code block 中）：");
        sb.AppendLine("{\"decision\":\"consensus|needs_discussion|escalate\",\"summary\":\"整理摘要\",\"discussion_points\":[\"需要進一步討論的點\"]}");
        sb.AppendLine();
        sb.AppendLine("decision 說明：");
        sb.AppendLine("- consensus：沒有重大分歧，可以繼續");
        sb.AppendLine("- needs_discussion：有需要討論的分歧，進行下一輪");
        sb.AppendLine("- escalate：有無法在團隊內解決的問題，需要老闆決定");
        return sb.ToString();
    }

    private static string BuildPetraPlanPrompt()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Kick-off 會議已結束。請基於完整的會議討論，產出任務計劃書。");
        sb.AppendLine();
        sb.AppendLine("格式如下：");
        sb.AppendLine("# 任務計劃書");
        sb.AppendLine("## 任務摘要");
        sb.AppendLine("{一段話描述要做什麼}");
        sb.AppendLine("## 關鍵決策");
        sb.AppendLine("- {Kick-off 中達成的共識}");
        sb.AppendLine("## 各角色意見摘要");
        sb.AppendLine("| 角色 | 主要意見 | 結論 |");
        sb.AppendLine("|------|---------|------|");
        sb.AppendLine("| Rosa | ... | 已確認 / 待 Christ 決定 |");
        sb.AppendLine("## 風險與注意事項");
        sb.AppendLine("- {Kick-off 中提出但未完全解決的項目}");
        sb.AppendLine("## 建議實作方向");
        sb.AppendLine("{基於討論結果的技術方向建議}");
        return sb.ToString();
    }

    // ---- JSON 解析 ----

    private PetraDecision? TryParsePetraDecision(string output)
    {
        // 從輸出的最後幾行中尋找 JSON
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        for (var i = lines.Length - 1; i >= Math.Max(0, lines.Length - 5); i--)
        {
            var line = lines[i].Trim();
            if (!line.StartsWith('{')) continue;
            try
            {
                return JsonSerializer.Deserialize<PetraDecision>(line,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch { /* 繼續往上找 */ }
        }
        return null;
    }

    private ModifyDecision? TryParseModifyDecision(string output)
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

// ---- Result Records ----

/// <summary>Stage 25a：Kick-off 會議執行結果。</summary>
public record MeetingResult(
    bool    Success,
    string  MeetingLog,
    string  TaskPlan,
    int     TotalRounds,
    string? EscalationReason = null);

/// <summary>Stage 25a：Christ 修改計劃書的 Petra 回應結果。</summary>
public record ModifyResult(
    string PetraFullOutput,
    string Impact,        // "small" | "large"
    string RevisedPlan);

// ---- Internal DTOs ----

internal record PetraDecision(
    string   Decision,
    string   Summary,
    string[] DiscussionPoints);

internal record ModifyDecision(
    string Impact,
    string RevisedPlan);

// ---- Stage 25b：Design Phase Records ----

/// <summary>Stage 25b：設計會議執行結果。</summary>
public record DesignMeetingResult(
    bool    Success,
    string  MeetingLog,
    string? DesignPlan,
    string? IssueUrls,
    string? UiSpecContent,
    int     TotalRounds,
    string  FinalDecision,   // "consensus" | "escalate"
    string  PetraSessionId,  // 供 escalate 路徑的 modify 流程 resume
    string? EscalateReason);

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

// ---- Stage 25b：Internal DTOs ----

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
