namespace AiTeam.Data;

/// <summary>
/// Stage 63B：Petra Orchestrator session message（v5 動態架構 PoC）。
/// 記錄 Petra 動態決策過程的 user / assistant / tool message 軌跡。
/// </summary>
public class PetraSessionMessage
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }

    /// <summary>角色：system / user / tool / assistant。</summary>
    public string Role { get; set; } = "";

    public string Content { get; set; } = "";

    /// <summary>tool 呼叫 id（assistant 帶 tool_calls 或 tool result 回填時填）。</summary>
    public string? ToolCallId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public PetraSession? Session { get; set; }
}
