using System.Text;
using System.Text.Json;
using AiTeam.Bot.Services;

namespace AiTeam.Bot.Orchestration.Meeting;

/// <summary>
/// Stage 52：拆 task 提案評估器（規則層 + Petra 層混合，Stage 46-FF 三十五 戰略級機制）— 從 DesignMeetingService 抽出共用 SoT。
///
/// SoT 對齊：legacy DesignMeetingService.RunDesignMeetingAsync line 320 + Stage 52 FrameworkDesignRouter finalize 段共用此 helper，
/// 避免雙寫漂移（Stage 46-FF 三十五 戰略級機制不能漂移）。
///
/// 設計約束（Forge Plan Mode 拍板 1 + 7）：
///   - scoped lifecycle（對齊 DesignMeetingService 既有 lifecycle，FrameworkDesignRouter 內 CreateAsyncScope 取）
///   - 規則層判「要不要觸發拆 task 提案」（Issue 數 / 預估行數 / Phase 標記任一觸發）
///   - Petra 層判「怎麼拆」（resume PetraSessionId 問細化拆法）
///   - 不觸發 → 回 null；觸發但 Petra 認定不該拆 → 回 ShouldSplit=false 的 SplitProposal
///
/// 純機械化重構：DesignMeetingService 內部 4 個 helper（TryCountIssues / EstimateDesignPlanLines / ContainsPhaseMarkers /
/// BuildSplitTaskPetraPrompt）+ TryParseSplitProposal 全部搬到此檔（行為 0 變動）。
/// </summary>
public class DesignSplitProposalEvaluator(
    MeetingCommons meetingCommons,
    AppSettingsService appSettings,
    Microsoft.Extensions.Configuration.IConfiguration configuration,
    ILogger<DesignSplitProposalEvaluator> logger)
{
    /// <summary>
    /// Stage 46-FF 三十五：規則層 + Petra 層混合（議題 1 C）。
    /// 不觸發 → 回 null；觸發但 Petra 認定不該拆 → 回 ShouldSplit=false 的 SplitProposal。
    /// </summary>
    public async Task<SplitProposal?> EvaluateAndProposeSplitAsync(
        string petraSessionId,
        string designPlan,
        string issuesJson,
        string workingDir,
        string apiKey,
        int round,
        TokenLogService tokenLogService,
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
                "DesignSplitProposalEvaluator：拆 task 規則層未觸發（issueCount={IC}<{Min1}, estLines={EL}<{Min2}, phaseMarkers={PM}）",
                issueCount, minIssues, estimatedLines, minLines, hasPhaseMarkers);
            return null;
        }

        logger.LogInformation(
            "DesignSplitProposalEvaluator：拆 task 規則層觸發（issueCount={IC}, estLines={EL}, phaseMarkers={PM}），呼叫 Petra 細化拆法",
            issueCount, estimatedLines, hasPhaseMarkers);

        return await RunPetraSplitTaskProposalAsync(
            petraSessionId, designPlan, issuesJson, workingDir, apiKey, round, tokenLogService, ct);
    }

    /// <summary>Stage 46-FF 三十五：Petra 細化拆法 — resume Design Petra session 問拆 phases。</summary>
    private async Task<SplitProposal?> RunPetraSplitTaskProposalAsync(
        string petraSessionId,
        string designPlan,
        string issuesJson,
        string workingDir,
        string apiKey,
        int round,
        TokenLogService tokenLogService,
        CancellationToken ct)
    {
        var prompt = BuildSplitTaskPetraPrompt(designPlan, issuesJson);
        var model  = configuration["Agents:PM:Model"] ?? configuration["Anthropic:DefaultModel"] ?? "claude-sonnet-4-6";

        var output = await meetingCommons.RunAgentTurnAsync("Petra", petraSessionId,
            prompt, model, apiKey, isFirstMessage: false, workingDir, MeetingCommons.ReadOnlyTools, ct,
            meetingType: "Design", round: round, tokenLogService: tokenLogService);

        var parsed = TryParseSplitProposal(output);
        if (parsed is null)
        {
            logger.LogWarning("DesignSplitProposalEvaluator：Petra 拆 task 提案 JSON 解析失敗，視為不拆（output 前 200 字={Output})",
                output.Length > 200 ? output[..200] : output);
            return null;
        }
        return parsed;
    }

    private async Task<int> GetSplitTaskAppSettingIntAsync(string key, int defaultValue, CancellationToken ct)
    {
        var raw = await appSettings.GetAsync(key, ct);
        if (string.IsNullOrWhiteSpace(raw)) return defaultValue;
        return int.TryParse(raw, out var value) ? value : defaultValue;
    }

    // ============================================================
    //  Public statics — DesignMeetingService 內聯 + framework path 共用
    // ============================================================

    public static int TryCountIssues(string issuesJson)
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

    public static int EstimateDesignPlanLines(string designPlan)
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

    public static bool ContainsPhaseMarkers(string designPlan)
    {
        if (string.IsNullOrWhiteSpace(designPlan)) return false;
        // Phase 1/2/3 標記（含中英）
        return System.Text.RegularExpressions.Regex.IsMatch(
            designPlan, @"Phase\s*[1-9]", System.Text.RegularExpressions.RegexOptions.IgnoreCase)
            || designPlan.Contains("第一階段") || designPlan.Contains("第二階段");
    }

    public static string BuildSplitTaskPetraPrompt(string designPlan, string issuesJson)
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
}
