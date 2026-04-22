using System.Text;
using System.Text.Json;
using AiTeam.Bot.Configuration;
using AiTeam.Bot.GitHub;
using AiTeam.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace AiTeam.Bot.Orchestration;

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
                var rosaTask  = meetingCommons.RunAgentTurnAsync("Rosa",  rosaSessionId,
                    BuildRosaPrompt(proposalContent, round, lastPetraOutput),
                    GetModel("Requirements"), apiKey, isFirstMessage, workingDir, MeetingCommons.ReadOnlyTools, ct);
                var demiTask  = meetingCommons.RunAgentTurnAsync("Demi",  demiSessionId,
                    BuildDemiPrompt(proposalContent, round, lastPetraOutput),
                    GetModel("Designer"), apiKey, isFirstMessage, workingDir, MeetingCommons.ReadOnlyTools, ct);
                var codyTask  = meetingCommons.RunAgentTurnAsync("Cody",  codySessionId,
                    BuildCodyPrompt(proposalContent, round, lastPetraOutput),
                    GetModel("Dev"), apiKey, isFirstMessage, workingDir, allowedTools: null, ct);
                var quinnTask = meetingCommons.RunAgentTurnAsync("Quinn", quinnSessionId,
                    BuildQuinnPrompt(proposalContent, round, lastPetraOutput),
                    GetModel("QA"), apiKey, isFirstMessage, workingDir, MeetingCommons.ReadOnlyTools, ct);

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
                var petraOutput = await meetingCommons.RunAgentTurnAsync("Petra", petraSessionId,
                    petraPrompt, GetModel("PM"), apiKey, isFirstMessage, workingDir, MeetingCommons.ReadOnlyTools, ct);
                lastPetraOutput = petraOutput;

                logBuilder.AppendLine("### Petra（綜合整理）");
                logBuilder.AppendLine(petraOutput);
                logBuilder.AppendLine();

                // 解析 Petra 的判斷 JSON
                var decision = TryParsePetraDecision(petraOutput);

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
            var planPrompt = BuildPetraPlanPrompt();
            var taskPlan = await meetingCommons.RunAgentTurnAsync("Petra", petraSessionId,
                planPrompt, GetModel("PM"), apiKey, isFirstMessage: false, workingDir, MeetingCommons.ReadOnlyTools, ct);

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
                prompt, GetModel("PM"), apiKey, isFirstMessage: false, workingDir, MeetingCommons.ReadOnlyTools, ct);

            logger.LogInformation("KickoffMeetingService：ModifyTaskPlan Petra 回應完成（groupId={Id}）", group.Id);

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
                catch (Exception ex) { logger.LogWarning(ex, "KickoffMeetingService：ModifyTaskPlan cleanup 失敗"); }
            }
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

    private static PetraDecision? TryParsePetraDecision(string output)
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

// ---- Internal DTOs ----

internal record PetraDecision(
    string   Decision,
    string   Summary,
    string[] DiscussionPoints);

internal record ModifyDecision(
    string Impact,
    string RevisedPlan);
