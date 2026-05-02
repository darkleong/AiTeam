using System.Text;
using AiTeam.Bot.Configuration;
using AiTeam.Bot.GitHub;
using AiTeam.Bot.Services;
using AiTeam.Bot.Workflows.Kickoff;
using AiTeam.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace AiTeam.Bot.Orchestration.Meeting;

/// <summary>
/// Stage 34：Kick-off 會議引擎（從 MeetingService 拆解而來）。
/// 負責協調 5 位 Agent（Petra/Rosa/Demi/Cody/Quinn）的 Claude Code 持續對話 session，
/// 實現「開工前全員對齊需求」的多輪討論流程，並由 Petra 產出任務計劃書。
/// </summary>
public class KickoffMeetingService(
    GitHubService gitHubService,
    WorkflowSettingsResolver workflowResolver,
    IOptions<GitHubSettings> gitHubSettings,
    IConfiguration configuration,
    MeetingCommons meetingCommons,
    TokenLogService tokenLogService,
    ILogger<KickoffMeetingService> logger)
{
    private readonly GitHubSettings _gitHub = gitHubSettings.Value;

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

        // Stage 32：方法開頭讀一次輪次上限（AppSettings 優先、appsettings.json fallback），
        // 避免每輪都 await AppSettings；執行中任務沿用開始時的數值。
        var kickoffMaxRounds = await workflowResolver.GetKickoffMaxRoundsAsync(ct);

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
            logger.LogWarning(ex, "KickoffMeetingService：Clone repo 失敗，使用 workspace 路徑作為 fallback");
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
            for (var round = 1; round <= kickoffMaxRounds; round++)
            {
                totalRounds = round;
                var isFirstMessage = round == 1;
                logger.LogInformation("KickoffMeetingService：Kick-off 第 {Round} 輪開始（groupId={Id}）", round, group.Id);

                logBuilder.AppendLine($"## Round {round}");
                logBuilder.AppendLine();

                // ── 步驟 1：Rosa/Demi/Cody/Quinn 並行發言 ──
                // Stage 44：4 處 RunAgentTurnAsync 都帶 meetingType="Kickoff" + round + tokenLogService
                // Stage 50：prompt builders 抽到 KickoffPrompts（feature flag 兩條路徑共用同 SoT）
                var rosaTask  = meetingCommons.RunAgentTurnAsync("Rosa",  rosaSessionId,
                    KickoffPrompts.BuildRosaPrompt(proposalContent, round, lastPetraOutput),
                    GetModel("Requirements"), apiKey, isFirstMessage, workingDir, MeetingCommons.ReadOnlyTools, ct,
                    meetingType: "Kickoff", round: round, tokenLogService: tokenLogService);
                var demiTask  = meetingCommons.RunAgentTurnAsync("Demi",  demiSessionId,
                    KickoffPrompts.BuildDemiPrompt(proposalContent, round, lastPetraOutput),
                    GetModel("Designer"), apiKey, isFirstMessage, workingDir, MeetingCommons.ReadOnlyTools, ct,
                    meetingType: "Kickoff", round: round, tokenLogService: tokenLogService);
                var codyTask  = meetingCommons.RunAgentTurnAsync("Cody",  codySessionId,
                    KickoffPrompts.BuildCodyPrompt(proposalContent, round, lastPetraOutput),
                    GetModel("Dev"), apiKey, isFirstMessage, workingDir, allowedTools: null, ct,
                    meetingType: "Kickoff", round: round, tokenLogService: tokenLogService);
                var quinnTask = meetingCommons.RunAgentTurnAsync("Quinn", quinnSessionId,
                    KickoffPrompts.BuildQuinnPrompt(proposalContent, round, lastPetraOutput),
                    GetModel("QA"), apiKey, isFirstMessage, workingDir, MeetingCommons.ReadOnlyTools, ct,
                    meetingType: "Kickoff", round: round, tokenLogService: tokenLogService);

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
                var petraPrompt = KickoffPrompts.BuildPetraRoundPrompt(rosaOutput, demiOutput, codyOutput, quinnOutput, round);
                var petraOutput = await meetingCommons.RunAgentTurnAsync("Petra", petraSessionId,
                    petraPrompt, GetModel("PM"), apiKey, isFirstMessage, workingDir, MeetingCommons.ReadOnlyTools, ct,
                    meetingType: "Kickoff", round: round, tokenLogService: tokenLogService);
                lastPetraOutput = petraOutput;

                logBuilder.AppendLine("### Petra（綜合整理）");
                logBuilder.AppendLine(petraOutput);
                logBuilder.AppendLine();

                // 解析 Petra 的判斷 JSON
                var decision = KickoffPrompts.TryParsePetraDecision(petraOutput);

                if (decision is null)
                {
                    logger.LogWarning("KickoffMeetingService：Petra 第 {Round} 輪回應無法解析 decision JSON，假設 consensus", round);
                    break;
                }

                logger.LogInformation("KickoffMeetingService：Petra 第 {Round} 輪 decision={Decision}", round, decision.Decision);

                if (decision.Decision == "consensus")
                    break;

                if (decision.Decision == "escalate")
                {
                    // 上呈 Christ 處理分歧（但繼續產出計劃書）
                    logger.LogWarning("KickoffMeetingService：Petra 判斷需上呈 Christ（groupId={Id}）", group.Id);
                    break;
                }

                // needs_discussion → 繼續下一輪
                if (round == kickoffMaxRounds)
                {
                    logger.LogInformation("KickoffMeetingService：已達最大輪次 {Max}，強制結束", kickoffMaxRounds);
                }
            }

            // ── 步驟 3：Petra 產出任務計劃書（Petra session 保留，供 Christ 修改流程使用）──
            var planPrompt = KickoffPrompts.BuildPetraPlanPrompt();
            var taskPlan = await meetingCommons.RunAgentTurnAsync("Petra", petraSessionId,
                planPrompt, GetModel("PM"), apiKey, isFirstMessage: false, workingDir, MeetingCommons.ReadOnlyTools, ct,
                meetingType: "Kickoff", round: totalRounds, tokenLogService: tokenLogService);

            logBuilder.AppendLine("## 任務計劃書");
            logBuilder.AppendLine(taskPlan);
            logBuilder.AppendLine();

            logger.LogInformation("KickoffMeetingService：Kick-off 會議完成（groupId={Id}，rounds={Rounds}）",
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
                catch (Exception ex) { logger.LogWarning(ex, "KickoffMeetingService：cleanup workingDir 失敗"); }
            }
        }
    }

    // ---- 計劃書修改 ----

    /// <summary>
    /// Christ 要求修改計劃書時，resume Petra 的既有 session（含完整會議 context）。
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
            logger.LogWarning(ex, "KickoffMeetingService：ModifyTaskPlan clone repo 失敗，使用 workspace fallback");
            workingDir = Path.Combine(_gitHub.WorkspacePath, repo);
        }

        try
        {
            var prompt =
                $"老闆要求修改任務計劃書，修改意見如下：\n\n{christFeedback}\n\n" +
                $"請基於完整的會議討論 context 評估修改影響，並在回應最後輸出以下 JSON（不要有其他格式）：\n" +
                $"{{\"impact\":\"small|large\",\"revised_plan\":\"（small 時輸出完整修改後計劃書，large 時留空）\"}}";

            var petraOutput = await meetingCommons.RunAgentTurnAsync("Petra", petraSessionId,
                prompt, GetModel("PM"), apiKey, isFirstMessage: false, workingDir, MeetingCommons.ReadOnlyTools, ct,
                meetingType: "Kickoff", round: group.KickoffRound, tokenLogService: tokenLogService);

            logger.LogInformation("KickoffMeetingService：ModifyTaskPlan Petra 回應完成（groupId={Id}）", group.Id);

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
                catch (Exception ex) { logger.LogWarning(ex, "KickoffMeetingService：ModifyTaskPlan cleanup 失敗"); }
            }
        }
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
