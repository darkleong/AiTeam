namespace AiTeam.Bot.Configuration;

/// <summary>Stage 23/24：工作流程設定，含 Review Appeal 輪次上限、QA 修復上限與版本號要求。</summary>
public class WorkflowSettings
{
    /// <summary>Cody-Vera Review Appeal 最大輪次（超過後由 Petra 仲裁）。</summary>
    public int ReviewAppealMaxRounds { get; set; } = 3;

    /// <summary>Stage 24：QA 修復迴圈最大輪次（Petra 判斷 code_bug 後，最多觸發幾輪 Dev_fix + QA）。</summary>
    public int QaFixMaxRounds { get; set; } = 3;

    /// <summary>Stage 24：Dev_plan Appeal 最大輪次（Cody-Petra 純 LLM 迴圈，超過後升級給老闆）。</summary>
    public int DevPlanAppealMaxRounds { get; set; } = 3;

    /// <summary>Stage 25a：Kick-off 會議最大輪次（超過後直接請 Petra 產出計劃書）。</summary>
    public int KickoffMaxRounds { get; set; } = 3;

    /// <summary>Stage 25b：設計會議最大輪次（含調整重開的次數，超過後 escalate 給 Christ）。</summary>
    public int DesignMeetingMaxRounds { get; set; } = 3;

    /// <summary>期望的版本號（Vera 版本檢查用）。空白時略過版本檢查。</summary>
    public string TargetVersion { get; set; } = "";

    /// <summary>Stage 49：v4 漸進遷移首發 — 是否啟用 MS Agent Framework Appeal loop（Cody-Vera-Petra + Cody-Petra）。
    /// 預設 false（保留 legacy AppealOrchestrationService 路徑），Dashboard SystemSettings → v4 漸進遷移控制 切換為 true
    /// 後 framework path 接管 Cody-Vera-Petra Critical Issue 申訴 + Cody-Petra Dev_plan 申訴 loop。
    /// AppSettings 表 key = "Workflow:UseFrameworkAppealLoop"，DB 優先，appsettings.json fallback。</summary>
    public bool UseFrameworkAppealLoop { get; set; } = false;

    /// <summary>Stage 50：v4 漸進遷移第二步 — 是否啟用 MS Agent Framework Kickoff Meeting Group Chat orchestration。
    /// 預設 false（保留 legacy KickoffMeetingService 路徑），Dashboard SystemSettings → v4 漸進遷移控制 切換為 true 後
    /// framework path 接管 Kickoff Meeting 5 Agent fan-out/fan-in 會議流程。
    /// AppSettings 表 key = "Workflow:UseFrameworkKickoff"，DB 優先，appsettings.json fallback。
    /// 與 Stage 49 Workflow:UseFrameworkAppealLoop 完全獨立。</summary>
    public bool UseFrameworkKickoff { get; set; } = false;

    /// <summary>Stage 51：v4 漸進遷移第三步 — 是否啟用 MS Agent Framework HITL pattern 試點
    /// （Kickoff Workflow 中途介入：Christ 在 Petra Round 邊界輸入修改指引，workflow 從 checkpoint resume）。
    /// 預設 false（不影響 Stage 50 framework Kickoff 既有行為），Dashboard SystemSettings → v4 漸進遷移控制 切換為 true 後啟用試點。
    /// AppSettings 表 key = "Workflow:UseFrameworkKickoffMidInterrupt"，DB 優先，appsettings.json fallback。
    /// 雙 flag 連動：本 flag 只在 UseFrameworkKickoff = true 時有意義（試點是 framework Kickoff path 的擴充，legacy 不適用）。</summary>
    public bool UseFrameworkKickoffMidInterrupt { get; set; } = false;

    /// <summary>Stage 52：v4 漸進遷移第四步 — 是否啟用 MS Agent Framework Design Meeting Workflow
    /// （fan-out/fan-in + 條件式 Demi + needs_adjustment 子流程 + 拆 task 提案後置）。
    /// 預設 false（保留 legacy DesignMeetingService 路徑），Dashboard SystemSettings → v4 漸進遷移控制 切換為 true 後
    /// framework path 接管 Design Meeting 完整流程（前置作業 + 主迴圈 round loop + B2 needs_adjustment Executor）。
    /// AppSettings 表 key = "Workflow:UseFrameworkDesign"，DB 優先，appsettings.json fallback。
    /// 與 Stage 49 / 50 / 51 三 flag 完全獨立（pipeline 上 Design 跟 Kickoff 是兩個獨立節點）。</summary>
    public bool UseFrameworkDesign { get; set; } = false;

    /// <summary>Stage 53A：v4 漸進遷移第五步 — 是否啟用 MS Agent Framework Pipeline Workflow
    /// （macro-orchestration framework-in-framework，NewFeature 主路徑 happy path 限定）。
    /// 預設 false（保留 legacy WorkflowEngine.GetDecision + TaskGroupService.HandleAgentCompletedAsync 路徑），
    /// Dashboard SystemSettings → v4 漸進遷移控制 切換為 true 後 framework path 接管整個任務 pipeline
    /// （proposal_approved → Kickoff → Design → Dev_plan → Dev → Reviewer → QA → Doc → NotifyBossMerge）。
    /// AppSettings 表 key = "Workflow:UseFrameworkPipeline"，DB 優先，appsettings.json fallback。
    /// 三 flag 連動規則：本 flag 只在 UseFrameworkKickoff = true AND UseFrameworkDesign = true 時有意義
    /// （pipeline 主 Workflow 內 KickoffStage / DesignStage Executor 同步 await 既有 framework router，
    /// 路徑必須 framework 才通）。Dashboard UI 上顯示 disabled 狀態當任一前置 flag = false。
    /// fix loop / appeal / QA fix loop / intervention 子流程留 Stage 53B 範圍 — 53A 5 個 fallback 點主動 call legacy method
    /// 接手（I2 反向設計，Stage 55 收尾統一移除）。</summary>
    public bool UseFrameworkPipeline { get; set; } = false;

    /// <summary>Stage 63B：v5 動態架構 PoC — 是否啟用 PetraOrchestratorService 接管 CeoAgentService 入口。
    /// 預設 false（保留既有 v4 path），切 true 後 CeoAgentService.ProcessWithClaudeCodeAsync 開頭直接 forward
    /// 到 PetraOrchestratorService.StartAsync（v5 動態決策 + BuildSequential + 7 Worker IAgentTool dispatch）。
    /// AppSettings 表 key = "Workflow:UsePetraOrchestratorV5"，DB 優先，appsettings.json fallback。
    /// 與 Stage 49-53A 五 framework flag 完全獨立 — v4 / v5 兩條路線並行（feature/v5-poc branch 開發，Stage 64+ 全量遷移）。</summary>
    public bool UsePetraOrchestratorV5 { get; set; } = false;

    /// <summary>Stage 67：v5.5 Phase 1 Step 2 — 是否啟用 Talent-Skill separation 重構基底（DB-driven Talent pool + Skill registry + GenericAgentTool）。
    /// 預設 false（保留 v5 既有 path — IAgentTool + 7 worker class fallback），切 true 後 PetraOrchestratorService.StartAsync dispatch
    /// 走 ITalent + GenericAgentTool path（看 Skill 找 Talent pool / round-robin / Talent 兼多 Skill）。
    /// AppSettings 表 key = "Workflow:UseTalentSkillSeparation"，DB 優先，appsettings.json fallback。
    /// 與 Stage 49-63B 六 framework / v5 flag 並存 — 必須 UsePetraOrchestratorV5=true 才有意義（v5.5 是 v5 path 上面的演進）。
    /// Trial_v13 驗 + Christ 拍板才切 default true。</summary>
    public bool UseTalentSkillSeparation { get; set; } = false;

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
}
