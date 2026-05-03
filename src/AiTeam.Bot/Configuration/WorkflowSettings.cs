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
}
