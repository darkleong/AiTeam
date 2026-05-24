namespace AiTeam.Bot.Configuration;

/// <summary>v5.5 production active 工作流程設定（v4 dead flag 11 個 Stage 85 砍光）。</summary>
public class WorkflowSettings
{
    /// <summary>期望的版本號（Vera 版本檢查用）。空白時略過版本檢查。</summary>
    public string TargetVersion { get; set; } = "";

    /// <summary>Stage 63B：v5 動態架構 PoC — 是否啟用 PetraOrchestratorService 接管 CeoAgentService 入口。
    /// 預設 false（保留既有 v4 path），切 true 後 CeoAgentService.ProcessWithClaudeCodeAsync 開頭直接 forward
    /// 到 PetraOrchestratorService.StartAsync（v5 動態決策 + BuildSequential + 7 Worker IAgentTool dispatch）。
    /// AppSettings 表 key = "Workflow:UsePetraOrchestratorV5"，DB 優先，appsettings.json fallback。
    /// 與 Stage 49-53A 五 framework flag 完全獨立 — v4 / v5 兩條路線並行（feature/v5-poc branch 開發，Stage 64+ 全量遷移）。</summary>
    public bool UsePetraOrchestratorV5 { get; set; } = false;

    /// <summary>Stage 69：v5.5 Phase 2 Step 3 — 是否啟用跨 session 長期持久記憶（TaskMemory + TalentMemory 注入 + 寫回）。
    /// 預設 false（守 v5.5 既有 dispatch path 0 regression — Trial_v15 驗 + Christ 拍板才切 default true）。
    /// 必須 UsePetraOrchestratorV5=true + UseTalentSkillSeparation=true 才有意義（Phase 2 是 Phase 1 之上演進）。
    /// AppSettings 表 key = "Workflow:UseV5Memory"，DB 優先，appsettings.json fallback。</summary>
    public bool UseV5Memory { get; set; } = false;

    /// <summary>Stage 69：v5.5 Phase 2 Step 3 — Token budget compact 觸發閾值百分比（buffer-above-keep 模型）。
    /// 語意：trigger 條件 = count &gt;= KeepCount * (100 + ThresholdPercent) / 100。
    /// default 60：50 keep + 60% buffer → trigger 在 80 entries → 削回 50。
    /// AppSettings 表 key = "Workflow:V5MemoryCompactThresholdPercent"，DB 優先 / fallback 60。</summary>
    public int V5MemoryCompactThresholdPercent { get; set; } = 60;

    /// <summary>Stage 69：v5.5 Phase 2 Step 3 — compact 後保留 newest N 條（per-TaskGroup / per-Talent 各自獨立計算）。
    /// AppSettings 表 key = "Workflow:V5MemoryCompactKeepCount"，DB 優先 / fallback 50。</summary>
    public int V5MemoryCompactKeepCount { get; set; } = 50;

    /// <summary>Stage 70：v5.5 Phase 2 Step 4 — Petra hierarchical decomposition + dependency graph 拆解 feature flag。
    /// 預設 false（守 v5.5 既有「Skill 序列線性 chain」path 0 regression — Trial_v16 驗 + Christ 拍板才切 default true）。
    /// 必須 UsePetraOrchestratorV5=true + UseTalentSkillSeparation=true 才有意義（Phase 2 第二步 = Phase 2 第一步之上演進）。
    /// AppSettings 表 key = "Workflow:UseV5SubtaskPlanning"，DB 優先，appsettings.json fallback。</summary>
    public bool UseV5SubtaskPlanning { get; set; } = false;

    /// <summary>Stage 72：v5.5 Phase 2 Step 5 — Prompt DB 化 feature flag。
    /// 預設 false（守 v5.5 既有 hardcoded path fallback — Trial_v18 驗 + Christ 拍板才切 default true）。
    /// flag=true 時 BuildPetraSystemPrompt 從 DB load `petra_orchestration` SkillPrompt base template +
    /// ClaudeCodeChatClientAdapter Worker dispatch 從 DB load `{capability}` SkillPrompt（含 TalentPrompt persona nullable overlay）。
    /// 必須 UsePetraOrchestratorV5=true + UseTalentSkillSeparation=true 才有意義（Phase 2 第三步 = Phase 2 第二步之上演進）。
    /// AppSettings 表 key = "Workflow:UseV5PromptDb"，DB 優先，appsettings.json fallback。</summary>
    public bool UseV5PromptDb { get; set; } = false;

    /// <summary>Stage 77：v5.5 Phase 3 補強 — PetraDispatchWorker multi-consumer 並行上限。
    /// 預設 3（業界 Anthropic Tier 1-2 個人帳號保守紀律 / 配 Stage 76 retry path 兜底 transient 429/5xx）。
    /// 範圍守 [1, 10]（超出 fallback default / 對齊 token rate limit 真實上限）。
    /// AppSettings 表 key = "Workflow:MaxConcurrentPetra"，DB 優先，appsettings.json fallback。
    /// 啟動時讀一次：後續 SQL UPDATE 需 Bot 重啟生效（動態 reload N consumer 非當前 Stage 範圍 / 對齊「自己用爽 / 不過早 over-engineer」）。</summary>
    public int MaxConcurrentPetra { get; set; } = 3;

    /// <summary>Stage 79：v5.5 image flow 補完 — per task max attachment count（對齊 Claude Code CLI + Claude API 真實上限）。
    /// 範圍守 [1, 20]（超出 fallback default）。
    /// AppSettings 表 key = "Workflow:MaxAttachmentsPerTask"，DB 優先，appsettings.json fallback。</summary>
    public int MaxAttachmentsPerTask { get; set; } = 5;

    /// <summary>Stage 79：v5.5 image flow 補完 — per attachment max size MB（對齊 Claude Code CLI + Claude API 5 MB per image 真實上限）。
    /// 範圍守 [1, 20]（超出 fallback default）。
    /// AppSettings 表 key = "Workflow:MaxAttachmentSizeMB"，DB 優先，appsettings.json fallback。</summary>
    public int MaxAttachmentSizeMB { get; set; } = 5;

    /// <summary>Stage 80：HITL plan confirmation 閘門 — Petra 拆完 plan 開 BossInteraction plan_confirm 卡 + Christ 4 decision pattern 拍板（approve / edit / reject / respond）。
    /// 預設 false（守 v5.5 baseline auto dispatch / 0 行為改變）— Trial_v24 開時切 true → 結案切回 false（對齊 aria-trial-summary skill flag 切回紀律）。
    /// AppSettings 表 key = "Workflow:UseHITLPlanConfirmation"，DB 優先，appsettings.json fallback。</summary>
    public bool UseHITLPlanConfirmation { get; set; } = false;

    /// <summary>Stage 81：動態 re-planning（LangGraph cycles）+ HITL retry gate 配套 feature flag。
    /// 預設 false（守 Stage 80 baseline / Trial_v25 開時切 true → 結案切回 false）。
    /// **真實生效需 UseHITLPlanConfirmation=true 為前置**（補強 #A 紀律 — ContinueChainFromSubtaskAsync 取 plan_confirm ContextJson 是 single source of truth）。
    /// AppSettings 表 key = "Workflow:UseDynamicReplanning"，DB 優先，appsettings.json fallback。</summary>
    public bool UseDynamicReplanning { get; set; } = false;

    /// <summary>Stage 81：max replan iterations hard cap（業界 LangGraph best practice）。
    /// 預設 3（approve / edit / respond 各 +1 / reject 不算）。範圍守 [1, 10]。
    /// AppSettings 表 key = "Workflow:MaxReplanIterations"。</summary>
    public int MaxReplanIterations { get; set; } = 3;

    /// <summary>Stage 81：replan session cost soft cap USD（雙重保險）。
    /// 預設 5 USD（達上限升 intervention 卡讓 Christ 拍板介入）。範圍守 > 0。
    /// AppSettings 表 key = "Workflow:ReplanCostCapUsd"。</summary>
    public decimal ReplanCostCapUsd { get; set; } = 5m;

    /// <summary>Stage 85：paused PetraSession timeout cleanup（小時）。
    /// 超過此值的 paused session 自動 cancel + Discord push 告知。
    /// 預設 24（HITL 等老闆回應比例上限）/ 範圍守 [1, 168]（1h-7d）。
    /// AppSettings 表 key = "Workflow:PausedSessionTimeoutHours"，DB 優先。</summary>
    public int PausedSessionTimeoutHours { get; set; } = 24;

    /// <summary>Stage 85：DiscordAlertService rate-limit window（分鐘）— per event type per window 只發 1 則 + aggregate 描述。
    /// 預設 5（連續失敗洗版防護）/ 範圍守 [1, 60]。
    /// AppSettings 表 key = "Workflow:AlertRateLimitMinutes"，DB 優先。</summary>
    public int AlertRateLimitMinutes { get; set; } = 5;
}
