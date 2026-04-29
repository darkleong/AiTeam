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
    /// <summary>Stage 38：LLM Provider（"Anthropic" / "Gemini"）。null = 啟動時從 appsettings.json 補 seed，Dashboard 改過後由此欄位為準。</summary>
    public string? Provider { get; set; }
    /// <summary>Stage 38：Model 名稱（如 "claude-sonnet-4-6" / "gemini-2.5-flash"）。null = 啟動時從 appsettings.json 補 seed。</summary>
    public string? Model { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Team Team { get; set; } = null!;
}

public class TaskGroup
{
    public Guid Id { get; set; }
    public string Title { get; set; } = "";
    public string Project { get; set; } = "";
    /// <summary>Stage 13：專案 FK（對應 projects.id），舊資料為 null。</summary>
    public Guid? ProjectId { get; set; }
    public Project? ProjectRef { get; set; }
    public string Status { get; set; } = "pending"; // pending / running / done / failed / needs_intervention
    // Stage 43：failed 與 needs_intervention 語意分離。
    //   failed              = 明確失敗已不可挽救（如 Petra 終評不通過、Vera Review Appeal 無共識、Dev Blocker）
    //   needs_intervention  = Christ 介入後可恢復（DevPlan 重產上限 / Dev fix failed / QA fix loop 上限 / Sage escalate）
    /// <summary>Stage 43：需介入時的原因摘要（如「Dev fix failed: Token 守門擋下」/「QA 修復連 3 輪失敗」）。null = 無介入。</summary>
    public string? InterventionReason { get; set; }

    // ── Stage 45：TaskGroup 流程暫停（FF 三十四） ──
    /// <summary>Stage 45：是否暫停下階段啟動（true = 當前階段跑完不轉下階段）。Default false。</summary>
    public bool IsPaused { get; set; } = false;
    /// <summary>Stage 45：暫停時間（UTC）。null = 無暫停紀錄。</summary>
    public DateTime? PausedAt { get; set; }
    /// <summary>Stage 45：暫停發起者識別（"Dashboard" / "MockAutoPause" / 未來 "Discord"）。null = 無暫停紀錄。</summary>
    public string? PausedBy { get; set; }
    /// <summary>Stage 45：暫停時被攔下的 next steps（JSON 序列化的 WorkflowStep[]）。Resume 時讀回再 FireStepsAsync，避免重做 routing。</summary>
    public string? PendingStepsJson { get; set; }

    // ── Stage 46：FF 三十五 自動拆任務（Petra Design 階段 propose 拆 sub-task） ──
    /// <summary>Stage 46：epic 關係 — sub-task 指向 parent TaskGroup。null = 普通 TaskGroup or epic 主 group。</summary>
    public Guid? ParentGroupId { get; set; }
    /// <summary>Stage 46：epic 級暫停（議題 5：只 epic 級，sub-task 級暫停留 Phase 2）。
    /// true = epic paused（後續 sub-task Sequential 鏈不啟動）；null = 不是 epic 主 group。</summary>
    public bool? EpicPaused { get; set; }
    /// <summary>Stage 46：sub-task 在 epic 內的 Phase 編號（1, 2, 3...）。null = 不是 sub-task。</summary>
    public int? PhaseNumber { get; set; }
    /// <summary>Stage 46：Petra 提供的 Phase 描述（如 "基礎結構" / "遷移" / "收尾"）。null = 不是 sub-task。</summary>
    public string? PhaseDescription { get; set; }

    public string WorkflowType { get; set; } = "new_feature"; // new_feature / bug_fix
    public string? IssueUrls { get; set; }   // JSONB string[]
    public string? UiSpecPath { get; set; }  // docs/ui-specs/xxx.md（舊欄位，保留相容，新資料不再使用）
    public string? UiSpecContent { get; set; }  // UI 規格全文（Stage 12 起改存 DB）
    public string? DevPrUrl { get; set; }
    public string? LastReviewBody { get; set; } // Vera 最新一次的完整審查報告（fix loop 傳給 Dev 用）
    public int FixIteration { get; set; } = 0; // 防止無限 Review loop，超過 3 次升級給老闆
    /// <summary>Stage 16：Cody 產出的實作計畫書全文（Petra 審核通過後帶給 Dev coding 用）。</summary>
    public string? DevPlan { get; set; }
    /// <summary>Stage 16：Dev_plan 修正次數（獨立於 FixIteration，避免與 Vera fix loop 互相干擾）。
    /// Stage 43：用於 DevPlan 重產次數計數（accept 後 plan 仍失敗 → 重新呼叫 Cody Dev_plan agent），上限 2 次。
    /// 與 DevPlanAppealRoundA（Cody-Petra 對話迴圈計數）獨立不互相干擾。</summary>
    public int DevPlanRevision { get; set; } = 0;
    /// <summary>Stage 23：Cody 產出的結構化實作說明（Vera 審查與 QA 測試參考）。</summary>
    public string? ImplementationNote { get; set; }
    /// <summary>Stage 23：Cody-Vera Review Appeal 討論輪次計數（迴圈 A）。</summary>
    public int ReviewAppealRoundA { get; set; } = 0;
    /// <summary>Stage 23：Review Appeal 逐輪完整紀錄（Markdown，含 Cody/Vera 完整回應 JSON）。</summary>
    public string? ReviewAppealLog { get; set; }
    /// <summary>Stage 23：仲裁後 Cody 修正應跳過 Vera，直接交 Petra 審核。</summary>
    public bool SkipReviewerAfterArbitration { get; set; } = false;
    /// <summary>Stage 24：Quinn 的測試報告 JSON（Petra QA 判斷 + Sage 歸檔用）。</summary>
    public string? TestReport { get; set; }
    /// <summary>Stage 24：Dev_plan Appeal 輪次計數（Cody-Petra 純 LLM 對話迴圈）。</summary>
    public int DevPlanAppealRoundA { get; set; } = 0;
    /// <summary>Stage 24：Dev_plan Appeal 完整對話紀錄（Markdown，含各輪完整 JSON）。</summary>
    public string? DevPlanAppealLog { get; set; }
    /// <summary>Stage 24：QA 修復迴圈輪次計數（Petra 判斷 code_bug 後，Dev_fix 跳過 Vera 直接重測的次數）。</summary>
    public int QaFixRound { get; set; } = 0;
    /// <summary>Stage 25a：Kick-off 會議完整紀錄（Markdown，含各 Agent 完整回應 + Christ 修改歷史）。</summary>
    public string? KickoffMeetingLog { get; set; }
    /// <summary>Stage 25a：Petra 在 Kick-off 會議結束後產出的任務計劃書。</summary>
    public string? TaskPlan { get; set; }
    /// <summary>Stage 25a：Kick-off 會議輪次計數。</summary>
    public int KickoffRound { get; set; } = 0;
    /// <summary>Stage 25b：設計會議完整紀錄（Markdown，含前置作業、各輪討論、調整紀錄）。</summary>
    public string? DesignMeetingLog { get; set; }
    /// <summary>Stage 25b：Petra 在設計會議結束後產出的設計規劃書。</summary>
    public string? DesignPlan { get; set; }
    /// <summary>Stage 25b：設計會議輪次計數（含調整重開的次數）。</summary>
    public int DesignRound { get; set; } = 0;
    /// <summary>Stage 29-1：Sage 歸檔報告全文（docs/archive/pr{N}-archive.md）。</summary>
    public string? ArchiveContent { get; set; }
    /// <summary>Stage 37：Crash Recovery 標記，紀錄目前進行中的非佇列化編排流程
    /// （Kickoff / Design / ReviewAppeal / DevPlanAppeal / QaRouting / null）。
    /// Bot 重啟後 MeetingOrchestrationService.RecoverStuckOrchestrationsAsync 掃描此欄位自動重跑。
    /// 原為 ActiveMeetingType（Stage 31，僅涵蓋 Meeting），Stage 37 升級涵蓋所有編排流程。</summary>
    public string? ActiveOrchestration { get; set; }
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
    public string Status { get; set; } = "pending"; // pending / queued / running / waiting_input / reviewing / revision / done / failed / cancelled
    public DateTime? QueuedAt { get; set; }          // Stage 27a：進入佇列的時間（排序用）
    public string? QueueStatus { get; set; }         // Stage 27a："queued" / "processing" / null（不在佇列中）
    public string? WorkflowAgentKey { get; set; }    // Stage 27a：HandleAgentCompletedAsync 的 agentKey（預計算，含 IsFixLoop 邏輯）
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
    public string AgentName { get; set; } = ""; // e.g. "CEO", "Dev", "QA", "Meeting-Kickoff"
    public string Model { get; set; } = "";      // e.g. "claude-sonnet-4-6"
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public Guid? TaskId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Stage 44：工作階段（Kickoff / Design / Dev_plan / Dev / Reviewer / QA / Doc / 申訴各分支）。null = 既有資料或無階段語意。</summary>
    public string? Stage { get; set; }
    /// <summary>Stage 44：fix iteration / 會議輪次（如 Vera fix loop round 1, Kickoff round 2）。null = 無輪次語意。</summary>
    public int? Round { get; set; }
    /// <summary>Stage 44：Prompt cache 寫入 token 數（Anthropic 計 1.25× cost）。null = 既有資料或無 cache。</summary>
    public int? CacheCreationTokens { get; set; }
    /// <summary>Stage 44：Prompt cache 讀取 token 數（Anthropic 計 0.1× cost）。null = 既有資料或無 cache。</summary>
    public int? CacheReadTokens { get; set; }
    /// <summary>Stage 44：本次呼叫總成本（USD），由 Claude Code 直接提供。null = 既有資料。</summary>
    public decimal? TotalCostUsd { get; set; }

    public TaskItem? Task { get; set; }

    /// <summary>
    /// Stage 44：計算 Anthropic 等效 token（守門用）。
    /// 公式：input + output + cache_creation × 1.25 + cache_read × 0.1
    /// 用整數運算避免 LINQ → SQL translate 浮點問題：
    ///   cache_creation × 1.25 ≈ cache_creation × 5 / 4
    ///   cache_read × 0.1      ≈ cache_read / 10
    /// 舊資料 cache 欄位 null 視為 0（與舊行為相容）。
    /// 注意：TokenRepository 的 SumAsync 仍需 inline 此公式（LINQ→SQL translate 不能跨 method invocation），
    /// 此 helper 主要供文件 / 單元測試 / client-side 計算引用。
    /// </summary>
    public static long ComputeEffectiveTokens(TokenLog log)
        => (long)log.InputTokens
         + log.OutputTokens
         + ((long)(log.CacheCreationTokens ?? 0) * 5L) / 4L
         + (long)(log.CacheReadTokens ?? 0) / 10L;
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

/// <summary>CEO 多輪對話的單一訊息記錄（DB 持久化，支援 Session 語境）。</summary>
public class CeoConversation
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }          // 同一段對話的 GUID
    public string UserId { get; set; } = "";     // Discord ulong.ToString()
    public string Role { get; set; } = "";       // "user" | "assistant"
    public string Content { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>CEO 跨 Session 長期記憶。Victoria 回應中的 memories_to_save 持久化至此表。</summary>
public class CeoMemory
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = "";
    public string Content { get; set; } = "";
    public string Category { get; set; } = "";  // "preference" | "decision" | "context"
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
