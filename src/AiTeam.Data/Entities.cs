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
    /// <summary>Stage 47：Agent 日 Token 上限（千 token）。null = DB 未設定，runtime fallback appsettings。</summary>
    public int? DailyTokenLimitK { get; set; }
    /// <summary>Stage 47：Agent 月 Token 上限（千 token）。null = DB 未設定，runtime fallback appsettings。</summary>
    public int? MonthlyTokenLimitK { get; set; }
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
    /// 原為 ActiveMeetingType（Stage 31，僅涵蓋 Meeting），Stage 37 升級涵蓋所有編排流程。
    /// Stage 49：framework path 啟動時寫 "FrameworkAppeal"，搭配 FrameworkAppealStateJson 雙 marker
    /// 區隔 legacy / framework 雙系統 Crash Recovery（避免 collision）。</summary>
    public string? ActiveOrchestration { get; set; }
    /// <summary>Stage 49：MS Agent Framework Appeal loop Checkpointing 序列化 state（JSON，CheckpointManager.CreateJson 寫入）。
    /// null = 走 legacy AppealOrchestrationService path（feature flag false）；
    /// 有值 = framework Workflow 進行中或已完成，superstep 結束時自動同步寫 DB。
    /// 走 framework path 時搭配 ActiveOrchestration = "FrameworkAppeal"。
    /// legacy RecoverStuckOrchestrationsAsync 必須加排除條件 g.FrameworkAppealStateJson == null
    /// 避免 legacy/framework 雙系統 collision（Stage 49 風險點 R2 緩解）。</summary>
    public string? FrameworkAppealStateJson { get; set; }
    /// <summary>Stage 50：MS Agent Framework Kickoff Meeting Group Chat orchestration Checkpointing 序列化 state（JSON）。
    /// null = 走 legacy KickoffMeetingService path（feature flag false）；
    /// 有值 = framework Workflow 進行中或已完成，superstep 結束時自動同步寫 DB。
    /// 走 framework path 時搭配 ActiveOrchestration = "FrameworkKickoff"。
    /// 與 Stage 49 FrameworkAppealStateJson 完全獨立（不同 framework Workflow / 不同 sessionId 概念）。
    /// legacy RecoverStuckOrchestrationsAsync 必須加排除條件 g.KickoffFrameworkStateJson == null
    /// 避免 legacy/framework 雙系統 collision（Stage 50 風險點 R2 緩解擴充）。</summary>
    public string? KickoffFrameworkStateJson { get; set; }
    /// <summary>Stage 52：MS Agent Framework Design Meeting Workflow Checkpointing 序列化 state（JSON）。
    /// null = 走 legacy DesignMeetingService path（feature flag false）；
    /// 有值 = framework Workflow 進行中或已完成，superstep 結束時自動同步寫 DB。
    /// 走 framework path 時搭配 ActiveOrchestration = "FrameworkDesign"。
    /// 與 Stage 49 / 50 兩個 framework state JSON 完全獨立（pipeline 上 Design 跟 Kickoff 是兩個獨立節點）。
    /// legacy RecoverStuckOrchestrationsAsync 必須加排除條件 g.DesignFrameworkStateJson == null
    /// 避免 legacy/framework 雙系統 collision（Stage 52 風險點 R5 緩解）。</summary>
    public string? DesignFrameworkStateJson { get; set; }
    /// <summary>Stage 53A：MS Agent Framework Pipeline Workflow Checkpointing 序列化 state（JSON，macro-orchestration）。
    /// null = 走 legacy WorkflowEngine.GetDecision + TaskGroupService.HandleAgentCompletedAsync path（feature flag false）；
    /// 有值 = framework Pipeline Workflow 進行中或已完成，superstep 結束時自動同步寫 DB。
    /// 走 framework path 時搭配 ActiveOrchestration = "FrameworkPipeline"。
    /// 與 Stage 49 / 50 / 52 三個 framework state JSON 完全獨立（macro 外層 vs micro 內層）。
    /// framework-in-framework：當 PipelineFrameworkStateJson != null 時，Stage Executor 同步 await inner FrameworkKickoffRouter / FrameworkDesignRouter，
    /// 內層 KickoffFrameworkStateJson / DesignFrameworkStateJson 期間並存，inner 完成後清回 null（外層保留）。
    /// F-α 排除條件（避免 4 marker 共存的 Recovery 篩選優先級 collision）：4 個既有 Recovery method 全加 g.PipelineFrameworkStateJson == null。
    /// I2 反向設計（5 fallback 點主動 call legacy method）：Stage 53B 還沒做必須留 fallback to legacy 路徑（Reviewer 🔴 / Dev_plan 失敗 / Dev 阻礙 / 仲裁後 Dev_fix / QA 修復），Stage 55 收尾統一移除。</summary>
    public string? PipelineFrameworkStateJson { get; set; }
    /// <summary>Stage 54（B2 拍板）：framework Design Workflow 內 GitHub Issue idempotency marker（Crash Recovery 防重複創 Issue）。
    /// null = 從未創過；0 = RosaPreWork 已創；N (≥1) = Round N Adjustment 已創。
    /// DesignRosaPreWorkExecutor (Round 0) check `!= 0` 才創；DesignAdjustmentExecutor (Round N) check `!= state.Round` 才創。
    /// 設計理由：B1（用 state.IssueUrls）會破壞 Adjustment 多輪業務（每輪都會踩 IssueUrls 已 set 而 skip）。
    /// B2 round-aware marker 100% 防重複 + 多輪業務正常覆蓋（marker N != N+1）。</summary>
    public int? LastIssueCreatedRound { get; set; }
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

    /// <summary>Stage 56：TotalCostUsd 是否為 fallback 估算值（true = 由 TokenCostEstimator 推算；false = 由 LLM provider 直接回傳）。</summary>
    public bool IsEstimated { get; set; }

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

/// <summary>
/// Stage 67：v5.5 Phase 1 Step 2 — Talent registry（DB-driven baseline 6 instance）。
/// Talent-Skill separation 概念：Talent = portable agent identity（角色 + provider + model + skill assignment）；
/// Skill = code-defined capability（ISkillRegistry hardcode）。
/// ProjectId nullable（Christ 決議 2 per-Project 隔離 / null = 全域共用 / 對齊未來客戶專案場景）。
/// Phase 3 才開放 WebUI Talent CRUD — Phase 1 baseline 6 instance 由 DbSeeder seed。
/// </summary>
public class Talent
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";                  // 對齊既有 worker name："Cody" / "Vera" / "Quinn" / "Sage" / "Petra" / "Victoria"
    public string DisplayName { get; set; } = "";
    public string Description { get; set; } = "";
    /// <summary>Stage 67：per-Project Talent 隔離 — null = 全域共用 / Guid = 該 Project 專屬 Talent。</summary>
    public Guid? ProjectId { get; set; }
    /// <summary>Stage 67：Talent 預設 LLM Provider（null = runtime fallback Agents:Dev:Model / 對齊既有 AgentConfig pattern）。</summary>
    public string? Provider { get; set; }
    /// <summary>Stage 67：Talent 預設 LLM Model（null = runtime fallback Agents:Dev:Model）。</summary>
    public string? Model { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Project? ProjectRef { get; set; }
    public ICollection<TalentSkill> Skills { get; set; } = [];
}

/// <summary>
/// Stage 67：v5.5 Phase 1 Step 2 — Talent ↔ Skill 多對多 assignment（一 Talent 可擔任 N Skill / 一 Skill 可由 N Talent 擔任 — horizontal scaling 預備）。
/// SkillName FK by name 對 ISkillRegistry hardcode 列表（不建 skills 表 — Skill 是 code-defined / 不開放動態加）。
/// IsPrimary = true 表主任職（dispatch 排序優先）/ false = 兼任。
/// Priority 同 Talent 多 Skill 排序（同 IsPrimary 群內 ASC）。
/// </summary>
public class TalentSkill
{
    public Guid Id { get; set; }
    public Guid TalentId { get; set; }
    /// <summary>Stage 67：Skill 名稱 — FK by name 對齊 ISkillRegistry 6 Skill。</summary>
    public string SkillName { get; set; } = "";
    /// <summary>Stage 67：true = Talent 主任職（dispatch 排序優先）/ false = 兼任。</summary>
    public bool IsPrimary { get; set; }
    /// <summary>Stage 67：同 Talent 多 Skill dispatch 排序（同 IsPrimary 群內 ASC）。</summary>
    public int Priority { get; set; }
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
    /// <summary>Stage 74：v5.5 Phase 3 Step 8 — per-Skill Provider override（null = fallback per-Talent Talent.Provider / 最終 fallback runtime Agents:Dev:Provider）。</summary>
    public string? Provider { get; set; }
    /// <summary>Stage 74：v5.5 Phase 3 Step 8 — per-Skill Model override（null = fallback per-Talent Talent.Model / 最終 fallback runtime Agents:Dev:Model）。</summary>
    public string? Model { get; set; }

    public Talent? Talent { get; set; }
}

/// <summary>
/// Stage 69：v5.5 Phase 2 Step 3 — per-Task 共用記憶（Petra dispatch 多 Talent 共看）。
/// 跨 session 長期持久層（vs PetraSessionMessage 是 short-term session 對話 history — 本表是「事件累積結論」級記憶）。
/// 對齊業界 2026 Hybrid memory 三層 standard 中段 — searchable recall database / per-task scoping。
///
/// Stage 69 v2.1：scope = **PetraSession**（不是 v4 TaskGroup）。
/// 對齊 v5.5「每次 CEO 觸發 = 一個 PetraSession = 一個 Task event」設計精神 — v5.5 path 刻意不建 v4 workflow 容器（dynamic orchestrator 取代 hierarchical static）。
/// </summary>
public class TaskMemory
{
    public Guid Id { get; set; }
    /// <summary>所屬 PetraSession（required FK — 對齊 petra_sessions / cascade delete）。</summary>
    public Guid PetraSessionId { get; set; }
    /// <summary>所屬 Project（nullable — PetraSession 自身可能無 ProjectId）。</summary>
    public Guid? ProjectId { get; set; }
    /// <summary>記憶 key（KV 風格 — 如 `decision/cody-output-summary` / `context/business-rule`）。</summary>
    public string Key { get; set; } = "";
    /// <summary>記憶 content（任意 text — Talent output 摘要 / Petra 拍板 decision / Christ 中途指引等）。</summary>
    public string Content { get; set; } = "";
    /// <summary>寫入此記憶的 Talent name（如 "Cody" / "Vera" / "Petra"）。</summary>
    public string CreatedByTalent { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Stage 69：v5.5 Phase 2 Step 3 — per-Talent 私有記憶（個人記憶 / 跨 task 累積）。
/// 對齊 Stage 67 Talent schema ProjectId nullable + partial unique index 紀律
/// （docs/conventions/ef-core.md Stage 68 新段 PostgreSQL `NULL ≠ NULL` 雷修法）。
/// </summary>
public class TalentMemory
{
    public Guid Id { get; set; }
    /// <summary>所屬 Talent（required FK — 對齊 talents）。</summary>
    public Guid TalentId { get; set; }
    /// <summary>所屬 Project（nullable — 對齊 Talent.ProjectId 隔離 pattern / null = 全域 Talent 全域記憶）。</summary>
    public Guid? ProjectId { get; set; }
    /// <summary>記憶 key（KV 風格 — 如 `last-task-summary` / `preference/coding-style`）。</summary>
    public string Key { get; set; } = "";
    /// <summary>記憶 content。</summary>
    public string Content { get; set; } = "";
    /// <summary>Stage 69 起步：簡單 keyword tag（Phase 3 可升 vector index）。</summary>
    public List<string> Tags { get; set; } = [];
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Talent? Talent { get; set; }
}

/// <summary>
/// Stage 72：v5.5 Phase 2 Step 5 — Prompt DB 化 / 職位層 SkillPrompt（per-Skill 共享 / 工作範圍）。
/// 對齊業界 2026 prompt orchestration 主流：DB-backed + versioning + rollback 保護 production。
///
/// Versioning method A：單表 + version_number + is_active flag（同 SkillName 多 row 累積，partial unique index 守一條 active）。
/// rollback = SQL UPDATE 切 is_active（不刪舊版本，留 audit trail）。
/// </summary>
public class SkillPrompt
{
    public Guid Id { get; set; }
    /// <summary>Skill 名稱（如 code_implementation / code_review / petra_orchestration）。</summary>
    public string SkillName { get; set; } = "";
    /// <summary>Prompt 本體（含 {{capabilityRoster}}/{{decompositionSection}}/{{outputSection}} placeholder 對齊 BuildPetraSystemPrompt — 其他 skill 為純文字內容）。</summary>
    public string PromptBody { get; set; } = "";
    /// <summary>版本號（baseline = 1，每次 Upsert 累加）。</summary>
    public int VersionNumber { get; set; } = 1;
    /// <summary>是否為當前 active 版本（partial unique index 守同 SkillName 只一條 true）。</summary>
    public bool IsActive { get; set; } = true;
    /// <summary>Stage 72：Phase 3 audit 預留（nullable — baseline seed 為 null）。</summary>
    public string? CreatedByUser { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Stage 72：v5.5 Phase 2 Step 5 — Prompt DB 化 / 個性層 TalentPrompt（per-Talent / 風格差異 / nullable）。
/// 對齊 Christ 議題 2 拍板：「同 Skill 多 Talent 不重複存職責 — SkillPrompt 共享」+「Talent 個性風格分開 — TalentPrompt 各自獨立」。
/// baseline 0 row（Phase 3 WebUI Talent CRUD 才補 persona content / Stage 72 schema 預留 nullable）。
/// </summary>
public class TalentPrompt
{
    public Guid Id { get; set; }
    /// <summary>所屬 Talent（required FK → talents）。</summary>
    public Guid TalentId { get; set; }
    /// <summary>Persona 本體（風格 / 個性 / 語氣等 — Stage 72 baseline 0 row，Phase 3 補）。</summary>
    public string PersonaBody { get; set; } = "";
    /// <summary>版本號（baseline = 1）。</summary>
    public int VersionNumber { get; set; } = 1;
    /// <summary>是否為當前 active 版本（partial unique index 守同 TalentId 只一條 true）。</summary>
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Talent? Talent { get; set; }
}

/// <summary>
/// Stage 75：v5.5 Phase 3 — Petra 接收層 queue（Layer 1）— FIFO DB-as-Queue 對齊 Stage 27 既有 agent_queues 紀律。
/// 設計核心：每次 CeoAgentService.ProcessWithClaudeCodeAsync v5.5 flag forward 一個 task → 寫一筆 PetraInbox row → 立即 ack；
/// PetraInboxProcessor BackgroundService polling pending → 開新 Scoped PetraOrchestratorService 處理。
/// 對齊 Christ「Agent 像人類處理事件」精神延伸（PM 接收並行 / 執行管理）+ 業界 70% production Orchestrator-Worker pattern。
///
/// W2 trade-off 紀律（Aria gate1）：Repository TryMarkRunningAsync 用「先 read 再 UPDATE」非 atomic — 單 Bot instance 場景 OK；
/// 未來多 instance 才踩 race / 對齊 AiTeam 單 Bot 真實架構約束。
/// </summary>
public class PetraInbox
{
    public Guid Id { get; set; }
    /// <summary>Christ 送的 task description（從 CeoAgentService userInput 傳入）。</summary>
    public string UserInput { get; set; } = "";
    /// <summary>來源：「discord」/「dashboard」(對齊既有 BossCommandLog.Source 紀律)。</summary>
    public string Source { get; set; } = "";
    /// <summary>「pending」 / 「running」 / 「completed」 / 「failed」。</summary>
    public string Status { get; set; } = "pending";
    /// <summary>對應 PetraSession（accept 後 Processor 處理時 set）。null = 尚未開 session。</summary>
    public Guid? PetraSessionId { get; set; }
    public DateTime EnqueuedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }
}
