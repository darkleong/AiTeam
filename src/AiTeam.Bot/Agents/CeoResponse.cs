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
