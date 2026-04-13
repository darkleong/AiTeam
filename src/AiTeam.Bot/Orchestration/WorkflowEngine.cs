namespace AiTeam.Bot.Orchestration;

/// <summary>
/// 任務流程類型。
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

/// <summary>
/// 任務完成後 CEO 要採取的下一步動作。
/// </summary>
public enum NextAction
{
    /// <summary>觸發下一批 Agent。</summary>
    FireAgents,
    /// <summary>通知老闆可以 merge PR。</summary>
    NotifyBossMerge,
    /// <summary>修復次數超過上限，升級給老闆介入。</summary>
    NotifyBossIntervention,
    /// <summary>本 Agent 不負責觸發後繼（例如 QA / Doc 完成後不需要做任何事）。</summary>
    Nothing
}

/// <summary>
/// WorkflowEngine 回傳給 Orchestrator 的決策結果。
/// </summary>
public record WorkflowDecision(
    NextAction Action,
    IReadOnlyList<WorkflowStep> NextSteps);

/// <summary>
/// Stage 10：開發流程自動閉環的流程表引擎。
/// 純邏輯，不走 LLM，不存 DB，毫秒級決策。
///
/// Stage 13 修正後流程（保留）：
/// Stage 16 新流程（Dev_plan 計畫書 + Petra 審核閘門）：
///
/// 新功能流程：
///   proposal_approved → Dev_plan（計畫書，Petra 審核後才 coding）
///   Dev_plan          → Dev（Petra 在 TaskGroupService 攔截審核）
///   Dev               → Reviewer
///   Reviewer ✅       → Petra 審核 → QA（TaskGroupService 攔截）
///   QA                → Doc（串行）
///   Doc               → 通知老闆 merge
///   Reviewer 🔴       → Petra 審核 → Dev(fix)（TaskGroupService 攔截）
///   Dev(fix)          → Reviewer（重審，最多 3 輪）
///
/// Bug 修復流程（不走 Dev_plan，規模小直接 coding）：
///   Dev               → Reviewer
///   Reviewer ✅       → Petra 審核 → QA（TaskGroupService 攔截）
///   QA                → 通知老闆 merge
///   Reviewer 🔴       → Petra 審核 → Dev(fix)
///   Dev(fix)          → Reviewer（重審，最多 3 輪）
///
/// 技術改善流程：
///   Dev_plan          → Dev（Petra 在 TaskGroupService 攔截審核）
///   Dev               → Reviewer
///   Reviewer ✅       → Petra 審核 → QA
///   QA                → 通知老闆 merge
///   Reviewer 🔴       → Petra 審核 → Dev(fix)
/// </summary>
public class WorkflowEngine
{
    private const int MaxFixIterations = 3;

    // ---- 新功能流程表（Stage 16：加入 Dev_plan 步驟；Stage 25a：加入 Kickoff 步驟）----
    private static readonly Dictionary<string, WorkflowStep[]> NewFeatureTable = new(StringComparer.OrdinalIgnoreCase)
    {
        // Stage 25a：proposal_approved 後先進行 Kick-off 會議（NewFeature 必要，由 MeetingService 協調）
        ["proposal_approved"] = [new WorkflowStep(AiTeam.Shared.Constants.AgentNames.Kickoff)],
        // Kickoff 完成後（Christ 確認後）才進入 Dev_plan（由 TaskGroupService 處理 Christ 確認邏輯）
        [AiTeam.Shared.Constants.AgentNames.Kickoff] = [new WorkflowStep("Dev_plan")],
        ["Dev_plan"]          = [new WorkflowStep("Dev")],     // Petra 審核在 TaskGroupService 攔截（HandleAgentCompletedAsync）
        // Stage 13：Dev 後只觸發 Reviewer（Vera ✅ 後 Petra 攔截再觸發 QA）
        ["Dev"]               = [new WorkflowStep("Reviewer")],
        ["QA"]                = [new WorkflowStep("Doc")],     // QA → Doc（串行）
        ["Doc"]               = [],                             // Doc → NotifyBossMerge（由 GetDecision 動態決定）
        // fix loop：Dev 修完後重派 Reviewer
        ["Dev_fix"]           = [new WorkflowStep("Reviewer", IsFixLoop: true)],
    };

    // ---- Bug 修復流程表（不走 Dev_plan）----
    private static readonly Dictionary<string, WorkflowStep[]> BugFixTable = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Dev"]     = [new WorkflowStep("Reviewer")],
        // Stage 13：QA 完成後 → NotifyBossMerge（由 GetDecision 動態決定）
        ["QA"]      = [],
        ["Dev_fix"] = [new WorkflowStep("Reviewer", IsFixLoop: true)],
        // Reviewer 節點由 GetDecision 方法動態決定
    };

    // ---- 技術改善流程表（Stage 14 + Stage 16：加入 Dev_plan）----
    private static readonly Dictionary<string, WorkflowStep[]> TechImprovementTable = new(StringComparer.OrdinalIgnoreCase)
    {
        // Stage 16：TechImprovement 也先產計畫書
        ["Dev_plan"] = [new WorkflowStep("Dev")],
        ["Dev"]      = [new WorkflowStep("Reviewer")],
        ["QA"]       = [],
        ["Dev_fix"]  = [new WorkflowStep("Reviewer", IsFixLoop: true)],
    };

    /// <summary>
    /// 根據完成的 Agent 與其結果，決定 Orchestrator 下一步要做什麼。
    /// </summary>
    /// <param name="workflowType">本次任務群組的流程類型。</param>
    /// <param name="completedAgent">剛完成的 Agent 名稱（若為修復迭代則為 "Dev_fix"）。</param>
    /// <param name="result">Agent 執行結果。</param>
    /// <param name="fixIteration">目前已累計的修復次數（存在 TaskGroup.FixIteration）。</param>
    public WorkflowDecision GetDecision(
        WorkflowType workflowType,
        string completedAgent,
        Agents.AgentExecutionResult result,
        int fixIteration = 0)
    {
        var table = workflowType switch
        {
            WorkflowType.NewFeature      => NewFeatureTable,
            WorkflowType.TechImprovement => TechImprovementTable,
            _                            => BugFixTable
        };

        // ---- Reviewer 節點：依 CriticalReviewCount 動態決定 ----
        if (completedAgent.Equals("Reviewer", StringComparison.OrdinalIgnoreCase))
        {
            if (result.CriticalReviewCount == 0)
            {
                // Stage 13：Reviewer ✅ 後觸發 QA（新功能和 Bug 修復都需要 QA 驗證）
                return new WorkflowDecision(NextAction.FireAgents,
                    [new WorkflowStep("QA")]);
            }

            // 有 🔴 問題
            if (fixIteration >= MaxFixIterations)
                return new WorkflowDecision(NextAction.NotifyBossIntervention, []);

            return new WorkflowDecision(NextAction.FireAgents,
                [new WorkflowStep("Dev", IsFixLoop: true)]);
        }

        // ---- Stage 13：Doc 完成（新功能最後一步）→ 通知老闆 merge ----
        if (completedAgent.Equals("Doc", StringComparison.OrdinalIgnoreCase))
            return new WorkflowDecision(NextAction.NotifyBossMerge, []);

        // ---- Stage 13/14：QA 完成 + Bug 修復 / 技術改善 → 通知老闆 merge（不需要 Doc）----
        if (completedAgent.Equals("QA", StringComparison.OrdinalIgnoreCase)
            && workflowType is WorkflowType.BugFix or WorkflowType.TechImprovement)
            return new WorkflowDecision(NextAction.NotifyBossMerge, []);

        if (!table.TryGetValue(completedAgent, out var steps) || steps.Length == 0)
            return new WorkflowDecision(NextAction.Nothing, []);

        return new WorkflowDecision(NextAction.FireAgents, steps);
    }
}
