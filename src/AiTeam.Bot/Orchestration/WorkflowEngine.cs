namespace AiTeam.Bot.Orchestration;

/// <summary>
/// 任務流程類型。
/// </summary>
public enum WorkflowType
{
    NewFeature,
    BugFix
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
/// 新功能流程：
///   proposal_approved  → Dev
///   Dev                → QA（並行）+ Doc（並行）+ Reviewer（並行）
///   Reviewer ✅（無 🔴）→ 通知老闆 merge
///   Reviewer 🔴（有問題）→ Dev(fix)
///   Dev(fix)           → Reviewer（重審）
///
/// Bug 修復流程：
///   Dev                → Reviewer
///   Reviewer ✅        → 通知老闆 merge
///   Reviewer 🔴        → Dev(fix)
///   Dev(fix)           → Reviewer（重審）
/// </summary>
public class WorkflowEngine
{
    private const int MaxFixIterations = 3;

    // ---- 新功能流程表 ----
    private static readonly Dictionary<string, WorkflowStep[]> NewFeatureTable = new(StringComparer.OrdinalIgnoreCase)
    {
        ["proposal_approved"] = [new WorkflowStep("Dev")],
        ["Dev"]               = [new WorkflowStep("QA",       RunInParallel: true),
                                  new WorkflowStep("Doc",      RunInParallel: true),
                                  new WorkflowStep("Reviewer", RunInParallel: true)],
        // Reviewer 節點由 GetDecision 方法依 CriticalReviewCount 動態決定
        ["QA"]                = [],
        ["Doc"]               = [],
        // fix loop：Dev 修完後重派 Reviewer
        ["Dev_fix"]           = [new WorkflowStep("Reviewer", IsFixLoop: true)],
    };

    // ---- Bug 修復流程表 ----
    private static readonly Dictionary<string, WorkflowStep[]> BugFixTable = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Dev"]     = [new WorkflowStep("Reviewer")],
        ["Dev_fix"] = [new WorkflowStep("Reviewer", IsFixLoop: true)],
        // Reviewer 節點由 GetDecision 方法動態決定
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
        var table = workflowType == WorkflowType.NewFeature ? NewFeatureTable : BugFixTable;

        // ---- Reviewer 節點：依 CriticalReviewCount 動態決定 ----
        if (completedAgent.Equals("Reviewer", StringComparison.OrdinalIgnoreCase))
        {
            if (result.CriticalReviewCount == 0)
                return new WorkflowDecision(NextAction.NotifyBossMerge, []);

            // 有 🔴 問題
            if (fixIteration >= MaxFixIterations)
                return new WorkflowDecision(NextAction.NotifyBossIntervention, []);

            return new WorkflowDecision(NextAction.FireAgents,
                [new WorkflowStep("Dev", IsFixLoop: true)]);
        }

        if (!table.TryGetValue(completedAgent, out var steps) || steps.Length == 0)
            return new WorkflowDecision(NextAction.Nothing, []);

        return new WorkflowDecision(NextAction.FireAgents, steps);
    }
}
