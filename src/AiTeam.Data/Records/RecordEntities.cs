namespace AiTeam.Data.Records;

/// <summary>
/// Stage 91：v4-rewrite MCP record system 5 個新 entity（執行端搬到 Claude Code Agent Team 後，AiTeam 收這些資料當「純記錄」）。
///
/// 命名紀律：用 Agent* prefix 區分既有 Team entity（人員團隊）和新的 Claude Code AgentTeam（execution session）。
/// Table 命名用 mcp_* prefix（強調 MCP write 來源）。
///
/// 5 個 entity：
///   - AgentTeam      — 一個 Claude Code Agent Team execution session（lead 命名）
///   - AgentTeammate  — Team 內的 individual teammate（lead 或 member）
///   - AgentTask      — Task lifecycle current state（create / claim / complete / fail 透過 update）
///   - AgentMessage   — Teammate 對話 log（user / assistant / tool message）
///   - AgentTokenUsage — Token 消耗記錄（每次 LLM call 寫一筆）
/// </summary>

public class AgentTeam
{
    public Guid Id { get; set; }
    /// <summary>Claude Code team name（lead session 命名 / e.g., "feature-x-team"）</summary>
    public string Name { get; set; } = "";
    /// <summary>老闆給 team 的 high-level intent（nullable）</summary>
    public string? Description { get; set; }
    /// <summary>active / closed</summary>
    public string Status { get; set; } = "active";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ClosedAt { get; set; }
}

public class AgentTeammate
{
    public Guid Id { get; set; }
    public Guid TeamId { get; set; }
    /// <summary>Claude Code teammate name（lead 通常 "petra-pm"、member spawn 時帶 name）</summary>
    public string Name { get; set; } = "";
    /// <summary>sonnet / opus / haiku / 完整 model id（nullable / spawn 時不一定知）</summary>
    public string? Model { get; set; }
    /// <summary>lead / member</summary>
    public string Role { get; set; } = "member";
    public DateTime SpawnedAt { get; set; } = DateTime.UtcNow;
    public DateTime? FinishedAt { get; set; }
}

public class AgentTask
{
    public Guid Id { get; set; }
    public Guid TeamId { get; set; }
    /// <summary>哪個 teammate 認領 / null = 未認領（pending）</summary>
    public Guid? TeammateId { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    /// <summary>pending / in_progress / completed / failed</summary>
    public string Status { get; set; } = "pending";
    /// <summary>JSON array of task Guid string — 此 task 依賴哪些其他 task（nullable）</summary>
    public string? DependenciesJson { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ClaimedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public class AgentMessage
{
    public Guid Id { get; set; }
    public Guid TeammateId { get; set; }
    /// <summary>關聯的 task / nullable（lead-level 對話、非 task 相關訊息）</summary>
    public Guid? TaskId { get; set; }
    /// <summary>user / assistant / tool</summary>
    public string Role { get; set; } = "";
    public string Content { get; set; } = "";
    /// <summary>jsonb / nullable / tool call 完整 payload（only for role=tool）</summary>
    public string? ToolCallJson { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class AgentTokenUsage
{
    public Guid Id { get; set; }
    public Guid TeammateId { get; set; }
    public Guid? TaskId { get; set; }
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public string? Model { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
