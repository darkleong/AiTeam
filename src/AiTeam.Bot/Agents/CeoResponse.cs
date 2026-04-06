using System.Text.Json.Serialization;

namespace AiTeam.Bot.Agents;

/// <summary>
/// CEO Agent 固定回傳的 JSON 結構。
/// Stage 9：新增 propose action，支援提案模式。
/// </summary>
public class CeoResponse
{
    [JsonPropertyName("reply")]
    public string Reply { get; set; } = "";

    /// <summary>reply / delegate / propose / cancel</summary>
    [JsonPropertyName("action")]
    public string Action { get; set; } = "reply";

    /// <summary>
    /// 僅 delegate 時使用：null = 不指定；"bug_fix" | "tech_improvement"。
    /// 決定是否建立 TaskGroup 及使用哪個 WorkflowType。
    /// </summary>
    [JsonPropertyName("workflow_type")]
    public string? WorkflowType { get; set; }

    [JsonPropertyName("target_agent")]
    public string? TargetAgent { get; set; }

    [JsonPropertyName("task")]
    public CeoTaskPayload? Task { get; set; }

    [JsonPropertyName("require_confirmation")]
    public bool RequireConfirmation { get; set; } = true;

    /// <summary>Stage 15：Victoria 在 Claude Code 模式下判斷值得保存的長期記憶清單。</summary>
    [JsonPropertyName("memories_to_save")]
    public List<MemoryToSaveDto>? MemoriesToSave { get; set; }

    /// <summary>Stage 15：本次回應是否有執行 git commit 提交 docs/ 變更。</summary>
    [JsonPropertyName("docs_committed")]
    public bool DocsCommitted { get; set; }
}

/// <summary>Victoria 回應 ACTION JSON 中 memories_to_save 的元素 DTO（Bot 層，勿與 Data 層的 MemoryToSave 混淆）。</summary>
public class MemoryToSaveDto
{
    [JsonPropertyName("content")]
    public string Content { get; set; } = "";

    [JsonPropertyName("category")]
    public string Category { get; set; } = "context";
}

public class CeoTaskPayload
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("project")]
    public string Project { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("priority")]
    public string Priority { get; set; } = "normal"; // low / normal / high / critical
}
