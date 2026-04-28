using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AiTeam.Bot.Configuration;
using AiTeam.Bot.GitHub;
using AiTeam.Data;
using Microsoft.Extensions.Options;

namespace AiTeam.Bot.Agents.Pm;

/// <summary>
/// Stage 35：PmAgentService 拆解後的共用工具。
/// 職責：
/// - 申訴環節共用的環境準備（PrepareClaudeCodeEnv / BuildAppealContextSectionAsync / CleanupAppealRepo）
/// - Petra 標準審核 system prompt + JSON 解析（Review + Dev_plan 重評共用）
/// </summary>
public class PmAgentCommons(
    GitHubService gitHubService,
    IOptions<GitHubSettings> gitHubSettings,
    IConfiguration configuration,
    ILogger<PmAgentCommons> logger)
{
    private readonly GitHubSettings _gitHub = gitHubSettings.Value;

    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    // ────────────── Claude Code 環境準備（5 申訴共用）──────────────

    /// <summary>
    /// Clone/Pull repo 並讀取 model + API key，供 5 個申訴環節共用。
    /// CloneOrPull 失敗時 workingDir 回傳空字串（由呼叫方的 finally 透過 CleanupAppealRepo 安全處理）。
    /// </summary>
    public (string workingDir, string model, string apiKey) PrepareClaudeCodeEnv(
        TaskGroup group, string agentConfigKey)
    {
        var model  = configuration[$"Agents:{agentConfigKey}:Model"]
                  ?? configuration["Anthropic:DefaultModel"]
                  ?? "claude-haiku-4-5";
        var apiKey = configuration["Anthropic:ApiKey"] ?? "";

        var owner = _gitHub.Owner;
        var repo  = string.IsNullOrWhiteSpace(group.Project) ? _gitHub.DefaultRepo : group.Project;
        var workingDir = "";
        try
        {
            workingDir = gitHubService.CloneOrPull(owner, repo, $"appeal-{group.Id:N}"[..12]);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "PrepareClaudeCodeEnv CloneOrPull 失敗，workingDir 為空（group={Id}）", group.Id);
        }
        return (workingDir, model, apiKey);
    }

    /// <summary>
    /// 申訴環節結束時的 workspace 清理 wrapper。
    /// 空路徑直接跳過，失敗只 log warning 不拋例外（清理是 best-effort）。
    /// </summary>
    public void CleanupAppealRepo(string workingDir, string operationName)
    {
        if (string.IsNullOrEmpty(workingDir)) return;
        try { gitHubService.CleanupLocalRepo(workingDir); }
        catch (Exception ex) { logger.LogWarning(ex, "{Op} cleanup 失敗", operationName); }
    }

    /// <summary>
    /// 組建「任務背景脈絡」區塊，供 5 個申訴 prompt 共用。
    /// 含 TaskPlan / DesignPlan / DevPlan / ImplementationNote / PR diff（best-effort）。
    /// Dev_plan Appeal 場景通常尚未建 PR，TryParsePrNumber 會自動返回 false 跳過。
    /// </summary>
    public async Task<string> BuildAppealContextSectionAsync(TaskGroup group)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## 任務背景脈絡（供閱讀 codebase 時參考）");
        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(group.TaskPlan))
        {
            sb.AppendLine("### TaskPlan（Kickoff 會議產出）");
            sb.AppendLine(group.TaskPlan.Length > 2000 ? group.TaskPlan[..2000] + "\n...（截斷）" : group.TaskPlan);
            sb.AppendLine();
        }
        if (!string.IsNullOrWhiteSpace(group.DesignPlan))
        {
            sb.AppendLine("### DesignPlan（設計規劃書）");
            sb.AppendLine(group.DesignPlan.Length > 2000 ? group.DesignPlan[..2000] + "\n...（截斷）" : group.DesignPlan);
            sb.AppendLine();
        }
        if (!string.IsNullOrWhiteSpace(group.DevPlan))
        {
            sb.AppendLine("### DevPlan（實作計畫書）");
            sb.AppendLine(group.DevPlan.Length > 2000 ? group.DevPlan[..2000] + "\n...（截斷）" : group.DevPlan);
            sb.AppendLine();
        }
        if (!string.IsNullOrWhiteSpace(group.ImplementationNote))
        {
            sb.AppendLine("### ImplementationNote（Cody 實作自述）");
            sb.AppendLine(group.ImplementationNote.Length > 2000 ? group.ImplementationNote[..2000] + "\n...（截斷）" : group.ImplementationNote);
            sb.AppendLine();
        }

        if (TryParsePrNumber(group.DevPrUrl, out var prNumber))
        {
            try
            {
                var owner = _gitHub.Owner;
                var repo  = string.IsNullOrWhiteSpace(group.Project) ? _gitHub.DefaultRepo : group.Project;
                var files = await gitHubService.GetPullRequestFilesAsync(owner, repo, prNumber);
                if (files.Count > 0)
                {
                    sb.AppendLine($"### PR #{prNumber} 變更摘要（{files.Count} 個檔案）");
                    foreach (var f in files.Take(15))
                    {
                        sb.AppendLine($"**{f.FileName}** (+{f.Additions}/-{f.Deletions})");
                        if (!string.IsNullOrWhiteSpace(f.Patch))
                            sb.AppendLine($"```diff\n{(f.Patch.Length > 400 ? f.Patch[..400] + "\n..." : f.Patch)}\n```");
                    }
                    sb.AppendLine();
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "BuildAppealContextSectionAsync 取得 PR diff 失敗（PR#{N}），略過", prNumber);
            }
        }

        return sb.ToString();
    }

    private static bool TryParsePrNumber(string? prUrl, out int prNumber)
    {
        prNumber = 0;
        if (string.IsNullOrWhiteSpace(prUrl)) return false;
        var parts = prUrl.TrimEnd('/').Split('/');
        return parts.Length > 0 && int.TryParse(parts[^1], out prNumber);
    }

    // ────────────── 標準 Petra system prompt + JSON 解析 ──────────────
    // （PmReviewService / DevPlanAppealService 共用）

    public static string BuildPetraSystemPrompt() => """
        你是 Petra，專案經理。負責審核 AI 團隊的產出品質。

        重要原則：偏好 approve。你的職責是擋住「會出事」的問題，不是追求完美。如果產出能用，就放行。

        只輸出 JSON，不加任何說明文字、不加 markdown code block。

        格式：
        {
          "decision": "approve" | "revise" | "escalate",
          "summary": "一句話說明審核結論",
          "issues": [
            {
              "severity": "blocking" | "minor",
              "description": "具體問題描述"
            }
          ],
          "revision_instructions": "打回修正時給 Agent 的具體修改指示（approve 時為 null）"
        }

        decision 說明：
        - approve：無 blocking 問題，放行
        - revise：有 blocking 問題，打回修正（提供 revision_instructions）
        - escalate：情況複雜或 2 次修正後仍不通過，需老闆決定

        使用繁體中文，程式碼與專有名詞保留英文。
        """;

    public static PetraReview? TryParseReview(string content)
    {
        try
        {
            var start = content.IndexOf('{');
            var end   = content.LastIndexOf('}');
            if (start < 0 || end < 0) return null;
            var json  = content[start..(end + 1)];
            var dto   = JsonSerializer.Deserialize<PetraReviewDto>(json, JsonOptions);
            if (dto is null || string.IsNullOrWhiteSpace(dto.Decision)) return null;

            var decision = dto.Decision.ToLowerInvariant() switch
            {
                "approve"  => "approve",
                "revise"   => "revise",
                "escalate" => "escalate",
                _          => "approve" // 未知值 fallback 放行
            };

            var issues = dto.Issues?.Select(i => new PetraIssue(
                i.Severity ?? "minor",
                i.Description ?? "")).ToList() ?? [];

            return new PetraReview(decision, dto.Summary ?? "", issues, dto.RevisionInstructions);
        }
        catch { return null; }
    }

    // ────────────── Stage 43-A：DevPlan 失敗判定（多重 OR） ──────────────

    /// <summary>
    /// 判定 DevPlan 是否為「失敗訊息」（用於 Cody Dev_plan agent 產出後檢查是否值得進 Dev 階段）。
    /// 多重 OR 條件，依序檢查並回報命中項：
    ///   ① null 或字數 &lt; 100 → "DevPlan 為空或字數 &lt; 100"
    ///   ② 含「產出失敗」/「請查看 log」/「無法產出」任一 → "DevPlan 含失敗關鍵字"
    ///   ③ 缺結構 marker（"## 實作說明" 或常見實作章節）→ "DevPlan 結構不完整"
    /// </summary>
    public static (bool Failed, string? Reason) IsDevPlanFailed(string? devPlan)
    {
        if (string.IsNullOrWhiteSpace(devPlan) || devPlan.Length < 100)
            return (true, "DevPlan 為空或字數 < 100");

        if (devPlan.Contains("產出失敗") || devPlan.Contains("請查看 log") || devPlan.Contains("無法產出"))
            return (true, "DevPlan 含失敗關鍵字");

        // Cody Dev_plan 期望結構：「## 實作說明」 or「## 實作步驟」or「## 變更檔案」其中之一
        if (!devPlan.Contains("## 實作說明") &&
            !devPlan.Contains("## 實作步驟") &&
            !devPlan.Contains("## 變更檔案") &&
            !devPlan.Contains("## 實作項目"))
            return (true, "DevPlan 結構不完整（缺實作說明/步驟/檔案章節）");

        return (false, null);
    }

    // ────────────── 內部 DTO（JSON parse 用）──────────────

    private sealed class PetraReviewDto
    {
        [JsonPropertyName("decision")]
        public string? Decision { get; set; }

        [JsonPropertyName("summary")]
        public string? Summary { get; set; }

        [JsonPropertyName("issues")]
        public List<PetraIssueDto>? Issues { get; set; }

        [JsonPropertyName("revision_instructions")]
        public string? RevisionInstructions { get; set; }
    }

    private sealed class PetraIssueDto
    {
        [JsonPropertyName("severity")]
        public string? Severity { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }
    }
}
