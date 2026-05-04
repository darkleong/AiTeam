namespace AiTeam.Bot.Orchestration;

/// <summary>
/// 任務流程類型（跨 service fundamental enum）。
///
/// Stage 55A：原 WorkflowEngine class + GetDecision method + NextAction enum + WorkflowDecision record
/// 已刪除（v4 漸進遷移第八步 — Pipeline framework 接管全 routing）。本檔保留 WorkflowType + WorkflowStep
/// 作為跨 service fundamental type（被 TaskGroupService.FireStepsAsync / ProposalConfirmationService /
/// ButtonCallbackRouter / MockScenarioService 等廣泛使用）。
/// </summary>
public enum WorkflowType
{
    NewFeature,
    BugFix,
    /// <summary>重構、效能優化、技術債清理，流程同 BugFix（Dev→Reviewer→QA），不需要 Rosa/Demi。</summary>
    TechImprovement
}

/// <summary>
/// 流程表中的一個步驟：要觸發哪個 Agent、是否可與其他步驟並行、額外 metadata。
/// </summary>
/// <param name="AgentName">目標 Agent 名稱（與 DI key 一致）。</param>
/// <param name="RunInParallel">是否可與同層其他步驟並行觸發。</param>
/// <param name="IsFixLoop">是否為 Review 閉環的修復迭代（Dev 推修正到同一 branch）。</param>
public record WorkflowStep(
    string AgentName,
    bool RunInParallel = false,
    bool IsFixLoop = false);
