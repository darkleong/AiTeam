namespace AiTeam.Data;

public class Team
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Project> Projects { get; set; } = [];
    public ICollection<AgentConfig> Agents { get; set; } = [];
    public ICollection<TaskItem> Tasks { get; set; } = [];
}

public class Project
{
    public Guid Id { get; set; }
    public Guid TeamId { get; set; }
    public string Name { get; set; } = "";
    public string? RepoUrl { get; set; }
    public string? TechStack { get; set; } // JSONB
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Team Team { get; set; } = null!;
    public ICollection<TaskItem> Tasks { get; set; } = [];
}

public class AgentConfig
{
    public Guid Id { get; set; }
    public Guid TeamId { get; set; }
    public string Name { get; set; } = ""; // CEO / Dev / Ops / QA / Doc / Requirements
    public string Description { get; set; } = ""; // CEO 系統提示用描述
    public int TrustLevel { get; set; } = 0;
    public bool IsActive { get; set; } = true;
    public string? DiscordChannelId { get; set; } // Discord 頻道 ID（ulong 存為字串）
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Team Team { get; set; } = null!;
}

public class TaskGroup
{
    public Guid Id { get; set; }
    public string Title { get; set; } = "";
    public string Project { get; set; } = "";
    public string Status { get; set; } = "pending"; // pending / running / done / failed
    public string WorkflowType { get; set; } = "new_feature"; // new_feature / bug_fix
    public string? IssueUrls { get; set; }   // JSONB string[]
    public string? UiSpecPath { get; set; }  // docs/ui-specs/xxx.md
    public string? DevPrUrl { get; set; }
    public int FixIteration { get; set; } = 0; // 防止無限 Review loop，超過 3 次升級給老闆
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<TaskItem> Tasks { get; set; } = [];
}

public class TaskItem
{
    public Guid Id { get; set; }
    public Guid? TeamId { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? GroupId { get; set; } // Stage 10：任務群組（Orchestrator 用）
    public string Title { get; set; } = "";
    public string? Description { get; set; } // CEO 任務描述（供 Agent 使用）
    public string TriggeredBy { get; set; } = ""; // Discord / GitHub / Schedule
    public string AssignedAgent { get; set; } = "";
    public string Status { get; set; } = "pending"; // pending / running / waiting_input / done / failed
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    public Team? Team { get; set; }
    public Project? Project { get; set; }
    public TaskGroup? Group { get; set; }
    public ICollection<TaskLog> Logs { get; set; } = [];
}

public class TaskLog
{
    public Guid Id { get; set; }
    public Guid TaskId { get; set; }
    public string Agent { get; set; } = "";
    public string Step { get; set; } = "";
    public string Status { get; set; } = "pending"; // pending / running / done / failed
    public string? Payload { get; set; } // JSONB
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public TaskItem Task { get; set; } = null!;
}

/// <summary>每次 LLM 呼叫的 Token 用量記錄，供 Dashboard 費用監控使用。</summary>
public class TokenLog
{
    public Guid Id { get; set; }
    public string AgentName { get; set; } = ""; // e.g. "CEO", "Dev", "QA"
    public string Model { get; set; } = "";      // e.g. "claude-sonnet-4-6"
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public Guid? TaskId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public TaskItem? Task { get; set; }
}

/// <summary>動態系統設定（key/value），可從 Dashboard 即時修改，免重啟 Bot。</summary>
public class AppSetting
{
    public string Key { get; set; } = "";
    public string Value { get; set; } = "";
    public string? Description { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class Rule
{
    public Guid Id { get; set; }
    public Guid? TeamId { get; set; }
    public string Content { get; set; } = "";
    /// <summary>null = 全域規則；有值 = 僅套用到指定 Agent</summary>
    public string? AgentName { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; } = 0;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Team? Team { get; set; }
}
