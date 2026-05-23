namespace AiTeam.Bot.Orchestration.Petra;

/// <summary>
/// Stage 84：FindTalentForSkill 從 PetraOrchestratorService 抽出 static helper —
/// 解 PetraTalentDispatchService ↔ PetraDynamicReplanService ctor 循環依賴根因（FindTalentForSkill 引用 lookup）。
///
/// counter 作 <c>ref int</c> 傳入：caller（TalentDispatchService / DynamicReplanService）各持自己 <c>_roundRobinCounter</c> instance field（Scoped lifecycle / per-session）。
/// 0 IServiceProvider 走後門 / 0 process-wide static state（FindTalentForSkill 本身 static 但 0 state）。
///
/// 行為等價驗：Test15-16 reflection target 換到此 helper / counter local 變數 / assertion 不變。
/// </summary>
internal static class PetraTalentLookupHelper
{
    /// <summary>
    /// 看 Skill 找 Talent pool（IsPrimary desc + Priority asc 排序）+ round-robin pick。
    /// baseline 簡單實作：pool[counter++ % pool.Count]。pool 空 → return null。
    /// </summary>
    internal static ITalent? FindTalentForSkill(
        string skill,
        IReadOnlyList<ITalent> talents,
        ref int roundRobinCounter)
    {
        var pool = talents
            .Where(t => t.Skills.Any(s => string.Equals(s, skill, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        if (pool.Count == 0) return null;
        if (pool.Count == 1) return pool[0];

        // multi-instance — round-robin baseline（future 加 IsPrimary / Priority 排序由 TalentSkill 對齊複雜化留 Phase 2/3）
        var index = roundRobinCounter % pool.Count;
        roundRobinCounter++;
        return pool[index];
    }
}
