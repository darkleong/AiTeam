using System.Text;
using System.Text.Json;
using AiTeam.Data;

namespace AiTeam.Bot.Agents.Pm;

/// <summary>
/// Stage 35：Petra 路由判斷（Dev blocker / QA 失敗 / QA 無測試）。
/// 三個方法都是純 LLM 文字判斷，不需 Claude Code CLI 也不需 codebase context。
/// </summary>
public class PmRoutingService(
    LlmProviderFactory providerFactory,
    ILogger<PmRoutingService> logger)
{
    private const string AgentName = "PM";

    // ────────────── 23-3：Blocker 評估 ──────────────

    /// <summary>
    /// 評估 Cody 回報的開發阻礙，決定路由：continue / escalate_victoria / escalate_boss。
    /// </summary>
    public async Task<BlockerDecision> AssessBlockerAsync(
        string blockerJson,
        string taskTitle,
        CancellationToken ct = default)
    {
        // Stage 53B：dev_blocker_appeal 場景 — Petra 回 continue（讓 Pipeline DevStage retry Dev）
        if (MockClaudeCodeService.FailScenario == "framework_pipeline_dev_blocker_appeal")
        {
            logger.LogInformation("[MockMode/Stage53B] Petra Blocker 評估 continue（dev_blocker_appeal scenario）");
            return new BlockerDecision("continue", "[MOCK-53B] Petra 判定可重試 Dev");
        }

        var prompt = BuildBlockerAssessPrompt(blockerJson, taskTitle);
        return await RunBlockerLlmAsync(prompt, ct);
    }

    private async Task<BlockerDecision> RunBlockerLlmAsync(string prompt, CancellationToken ct)
    {
        var provider     = providerFactory.Create(AgentName);
        var systemPrompt = BuildBlockerSystemPrompt();

        for (var attempt = 1; attempt <= 2; attempt++)
        {
            try
            {
                var response = await provider.CompleteAsync(systemPrompt, prompt, ct);
                var decision = TryParseBlockerDecision(response.Content);
                if (decision is not null)
                {
                    logger.LogInformation("Petra Blocker 評估完成（第 {Attempt} 次）：{Routing}", attempt, decision.Routing);
                    return decision;
                }
                logger.LogWarning("Petra Blocker 回應解析失敗（第 {Attempt} 次）：{Content}", attempt, response.Content);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Petra Blocker LLM 呼叫失敗（第 {Attempt} 次）", attempt);
            }
        }

        logger.LogError("Petra Blocker 評估所有路徑均失敗，fallback escalate_boss");
        return new BlockerDecision("escalate_boss", "Blocker 評估失敗，升級給老闆");
    }

    private static string BuildBlockerAssessPrompt(string blockerJson, string taskTitle)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## 任務");
        sb.AppendLine(taskTitle);
        sb.AppendLine();
        sb.AppendLine("## Cody 回報的阻礙");
        sb.AppendLine(blockerJson);
        sb.AppendLine();
        sb.AppendLine("## 你的任務");
        sb.AppendLine("評估此阻礙是否可解決：");
        sb.AppendLine("- `continue`：阻礙描述不明確或可以讓 Cody 重試");
        sb.AppendLine("- `escalate_victoria`：需要 CEO Victoria 提供決策或資訊");
        sb.AppendLine("- `escalate_boss`：需要老闆介入（外部系統、API Key、根本性問題）");
        sb.AppendLine();
        sb.AppendLine("只輸出 JSON：{\"routing\": \"continue|escalate_victoria|escalate_boss\", \"instructions\": \"具體說明\"}");
        return sb.ToString();
    }

    private static string BuildBlockerSystemPrompt() => """
        你是 Petra，專案經理。負責評估 Cody 回報的開發阻礙。
        只輸出 JSON，不加任何說明文字、不加 markdown code block。
        使用繁體中文。
        """;

    private static BlockerDecision? TryParseBlockerDecision(string content)
    {
        try
        {
            var start = content.IndexOf('{');
            var end   = content.LastIndexOf('}');
            if (start < 0 || end < 0) return null;
            var json  = content[start..(end + 1)];
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var routing      = root.TryGetProperty("routing",      out var r) ? r.GetString() : null;
            var instructions = root.TryGetProperty("instructions", out var i) ? i.GetString() : null;
            if (string.IsNullOrWhiteSpace(routing)) return null;
            return new BlockerDecision(routing, instructions ?? "");
        }
        catch { return null; }
    }

    // ────────────── 24-1：QA 失敗評估 ──────────────

    /// <summary>
    /// 評估 QA 測試失敗的原因，決定後續路由。
    /// </summary>
    public async Task<QaFailureDecision> AssessQaFailureAsync(
        TaskGroup group,
        string testReportJson,
        CancellationToken ct = default)
    {
        // Stage 43-E：qa_fix_loop_fail 場景持續回 code_bug 路由，讓 QaFixRound 累計觸發 escalate
        // 不切換 FailScenario（持續到 QaCoordinationService 的 QaFixRound >= max 觸發 needs_intervention）
        if (MockClaudeCodeService.FailScenario == "qa_fix_loop_fail")
        {
            logger.LogInformation("[MockMode/QaFixLoop] Petra 模擬路由 code_bug，QaFixRound 累計觸發 escalate");
            return new QaFailureDecision("code_bug", "[MOCK] qa_fix_loop_fail：持續 code_bug 累計觸發 escalate");
        }

        var prompt       = BuildQaFailureAssessPrompt(group, testReportJson);
        var provider     = providerFactory.Create(AgentName);
        var systemPrompt = BuildQaAssessSystemPrompt();

        for (var attempt = 1; attempt <= 2; attempt++)
        {
            try
            {
                var response = await provider.CompleteAsync(systemPrompt, prompt, ct);
                var decision = TryParseQaFailureDecision(response.Content);
                if (decision is not null)
                {
                    logger.LogInformation("Petra QA 失敗評估完成（第 {Attempt} 次）：{Routing}", attempt, decision.Routing);
                    return decision;
                }
                logger.LogWarning("Petra QA 失敗評估解析失敗（第 {Attempt} 次）：{Content}", attempt, response.Content);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Petra QA 失敗評估 LLM 呼叫失敗（第 {Attempt} 次）", attempt);
            }
        }

        logger.LogError("Petra QA 失敗評估所有路徑均失敗，fallback env_or_test_issue（不卡流程）");
        return new QaFailureDecision("env_or_test_issue", "QA 評估失敗，略過以免卡住流程");
    }

    /// <summary>
    /// 評估 QA 無適合測試點的理由是否合理，決定是否放行。
    /// </summary>
    public async Task<QaNoTestDecision> AssessNoApplicableTestsAsync(
        TaskGroup group,
        string? noTestReason,
        CancellationToken ct = default)
    {
        // Stage 53B：qa_no_tests_dynamic 場景 — Petra approve 放行（讓 Pipeline QaStage 推進 Doc）
        if (MockClaudeCodeService.FailScenario == "framework_pipeline_qa_no_tests_dynamic")
        {
            logger.LogInformation("[MockMode/Stage53B] Petra 無測試評估 approve（qa_no_tests_dynamic scenario）");
            return new QaNoTestDecision("approve", "[MOCK-53B] Petra 同意放行純文件變更");
        }

        var prompt       = BuildNoTestAssessPrompt(group, noTestReason);
        var provider     = providerFactory.Create(AgentName);
        var systemPrompt = BuildQaAssessSystemPrompt();

        for (var attempt = 1; attempt <= 2; attempt++)
        {
            try
            {
                var response = await provider.CompleteAsync(systemPrompt, prompt, ct);
                var decision = TryParseQaNoTestDecision(response.Content);
                if (decision is not null)
                {
                    logger.LogInformation("Petra QA 無測試評估完成（第 {Attempt} 次）：{Routing}", attempt, decision.Routing);
                    return decision;
                }
                logger.LogWarning("Petra QA 無測試評估解析失敗（第 {Attempt} 次）：{Content}", attempt, response.Content);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Petra QA 無測試評估 LLM 呼叫失敗（第 {Attempt} 次）", attempt);
            }
        }

        logger.LogError("Petra QA 無測試評估所有路徑均失敗，fallback approve（不卡流程）");
        return new QaNoTestDecision("approve", "QA 無測試評估失敗，自動放行");
    }

    private static string BuildQaAssessSystemPrompt() => """
        你是 Petra，專案經理。負責評估 QA 測試結果並決定後續路由。
        偏好放行（approve / env_or_test_issue），只在確認是程式碼 bug 時才觸發修復。
        只輸出 JSON，不加任何說明文字、不加 markdown code block。
        使用繁體中文。
        """;

    private static string BuildQaFailureAssessPrompt(TaskGroup group, string testReportJson)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## 任務背景");
        sb.AppendLine($"Task Group：{group.Title}");
        if (!string.IsNullOrWhiteSpace(group.ImplementationNote))
        {
            sb.AppendLine();
            sb.AppendLine("## Cody 實作說明");
            sb.AppendLine(group.ImplementationNote.Length > 1500
                ? group.ImplementationNote[..1500] + "\n...（截斷）"
                : group.ImplementationNote);
        }
        sb.AppendLine();
        sb.AppendLine("## Quinn 的測試報告（JSON）");
        sb.AppendLine(testReportJson);
        sb.AppendLine();
        sb.AppendLine("## 你的任務");
        sb.AppendLine("評估測試失敗的根本原因，選擇路由：");
        sb.AppendLine("- `code_bug`：確認是程式碼邏輯 bug，**小修正**即可，Dev_fix 後直接重測（跳過 Vera）");
        sb.AppendLine("- `back_to_reviewer`：確認是程式碼 bug，但**需要大幅改動**，Dev_fix 後需回 Vera 正常審查");
        sb.AppendLine("- `env_or_test_issue`：測試本身的問題（錯誤的期望值、環境差異、測試設計不當），視同通過");
        sb.AppendLine("- `escalate_boss`：反覆失敗無法判斷原因，需要老闆介入");
        sb.AppendLine();
        sb.AppendLine("只輸出 JSON：{\"routing\": \"code_bug|back_to_reviewer|env_or_test_issue|escalate_boss\", \"instructions\": \"具體說明\"}");
        return sb.ToString();
    }

    private static string BuildNoTestAssessPrompt(TaskGroup group, string? noTestReason)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## 任務背景");
        sb.AppendLine($"Task Group：{group.Title}");
        sb.AppendLine();
        sb.AppendLine("## Quinn 無法測試的理由");
        sb.AppendLine(noTestReason ?? "（未提供理由）");
        sb.AppendLine();
        sb.AppendLine("## 你的任務");
        sb.AppendLine("評估 Quinn 無法測試的理由是否合理：");
        sb.AppendLine("- `approve`：理由合理（如純設定檔/migration 變更），直接放行");
        sb.AppendLine("- `escalate_boss`：理由不充分或有疑慮，需要老闆決策");
        sb.AppendLine();
        sb.AppendLine("只輸出 JSON：{\"routing\": \"approve|escalate_boss\", \"instructions\": \"具體說明\"}");
        return sb.ToString();
    }

    private static QaFailureDecision? TryParseQaFailureDecision(string content)
    {
        try
        {
            var start = content.IndexOf('{');
            var end   = content.LastIndexOf('}');
            if (start < 0 || end < 0) return null;
            var json  = content[start..(end + 1)];
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var routing      = root.TryGetProperty("routing",      out var r) ? r.GetString() : null;
            var instructions = root.TryGetProperty("instructions", out var i) ? i.GetString() : null;
            if (string.IsNullOrWhiteSpace(routing)) return null;
            return new QaFailureDecision(routing, instructions ?? "");
        }
        catch { return null; }
    }

    private static QaNoTestDecision? TryParseQaNoTestDecision(string content)
    {
        try
        {
            var start = content.IndexOf('{');
            var end   = content.LastIndexOf('}');
            if (start < 0 || end < 0) return null;
            var json  = content[start..(end + 1)];
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var routing      = root.TryGetProperty("routing",      out var r) ? r.GetString() : null;
            var instructions = root.TryGetProperty("instructions", out var i) ? i.GetString() : null;
            if (string.IsNullOrWhiteSpace(routing)) return null;
            return new QaNoTestDecision(routing, instructions ?? "");
        }
        catch { return null; }
    }
}
