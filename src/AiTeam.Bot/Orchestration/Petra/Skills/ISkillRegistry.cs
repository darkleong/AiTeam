namespace AiTeam.Bot.Orchestration.Petra.Skills;

/// <summary>
/// Stage 67：v5.5 Phase 1 Step 2 — Skill registry（code-defined / DI Singleton 安全）。
/// Stage 78a：4 Final Skill baseline（v4 path 砍後縮為 v5.5 6 Talent baseline 對應 capability）：
/// - 必備 4：code_implementation / code_review / qa_testing / documentation
/// - ❌ Stage 78a 砍 ui_design + release_publishing（對應 Rosa/Demi/Release class 整套砍 / ClaudeCodeChatClientAdapter 4 capability baseline / Petra LLM dispatch roster 真實縮限避 throw "未知 capability"）
/// - ❌ Stage 67 砍 requirements_extraction（合進 Petra orchestrator system prompt — Petra 自然拆需求紀律）
/// </summary>
public interface ISkillRegistry
{
    /// <summary>所有 code-defined Skill。</summary>
    IReadOnlyList<SkillDescriptor> All { get; }

    /// <summary>以 name 查詢 Skill（case-insensitive）。找不到回 null。</summary>
    SkillDescriptor? GetByName(string name);
}

/// <summary>
/// Stage 67：預設 Skill registry 實作。
/// Stage 78a：4 Skill hardcode（對齊 ClaudeCodeChatClientAdapter 4 capability baseline / v5.5 6 Talent baseline）。
/// </summary>
internal sealed class DefaultSkillRegistry : ISkillRegistry
{
    // Stage 74：v5.5 Phase 3 Step 8 — SkillDescriptor 全含 RecommendedModelTier + ReturnTypeDescription metadata
    //                                 對齊業界 Agent Skills open standard format 第一步（簡化版避過早 over-engineer JSON Schema parameters 全套）。
    // Stage 78a：4 SkillDescriptor（砍 ui_design + release_publishing — Rosa/Demi/Release class 砍對應）。
    private static readonly IReadOnlyList<SkillDescriptor> _skills = new[]
    {
        new SkillDescriptor(
            "code_implementation", "Code Implementation",
            "寫 code — 主開發任務（dispatch IClaudeCodeService.RunAsync）",
            "standard",
            "code patch + Implementation Note 段（含實作摘要 / 自驗結果 / 已知 follow-up）"),
        new SkillDescriptor(
            "code_review",         "Code Review",
            "review code — 找 bug / coding style / production safety check（dispatch RunReviewAsync）",
            "strategic",
            "JSON review report — critical/warning/info 三層 array + summary + impact 分析"),
        new SkillDescriptor(
            "qa_testing",          "QA Testing",
            "測試 + 寫 test case + Playwright 真實點擊（dispatch RunQaAsync）",
            "standard",
            "test files 直寫 + JSON QA report（status / passed_tests / failed_tests / unverifiable_targets / summary）"),
        new SkillDescriptor(
            "documentation",       "Documentation",
            "寫文件 + README + commit message + PR body（dispatch RunReadOnlyAsync）",
            "cost-efficient",
            "CHANGELOG entry + 歸檔 markdown（含實作摘要 / 審查摘要 / PR 連結 / 版本號）"),
    };

    public IReadOnlyList<SkillDescriptor> All => _skills;

    public SkillDescriptor? GetByName(string name)
        => _skills.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));
}
