namespace AiTeam.Bot.Orchestration.Petra.Skills;

/// <summary>
/// Stage 67：v5.5 Phase 1 Step 2 — Skill registry（code-defined / DI Singleton 安全）。
/// 6 Final Skill baseline（從 7 砍 1 → 6 / Christ 2026-05-15 拍板對齊業界 finding sweet spot 5-7）：
/// - 必備 4：code_implementation / code_review / qa_testing / documentation
/// - 進階 2：ui_design / release_publishing
/// - ❌ 砍 requirements_extraction（合進 Petra orchestrator system prompt — Petra 自然拆需求紀律）
/// </summary>
public interface ISkillRegistry
{
    /// <summary>所有 code-defined Skill。</summary>
    IReadOnlyList<SkillDescriptor> All { get; }

    /// <summary>以 name 查詢 Skill（case-insensitive）。找不到回 null。</summary>
    SkillDescriptor? GetByName(string name);
}

/// <summary>
/// Stage 67：預設 Skill registry 實作 — 6 Skill hardcode。
/// 對齊 v5 既有 ClaudeCodeChatClientAdapter capability dispatch（保留同 name 兼容 v5 fallback path）。
/// </summary>
internal sealed class DefaultSkillRegistry : ISkillRegistry
{
    private static readonly IReadOnlyList<SkillDescriptor> _skills = new[]
    {
        new SkillDescriptor("code_implementation", "Code Implementation", "寫 code — 主開發任務（dispatch IClaudeCodeService.RunAsync）"),
        new SkillDescriptor("code_review",         "Code Review",          "review code — 找 bug / coding style / production safety check（dispatch RunReviewAsync）"),
        new SkillDescriptor("qa_testing",          "QA Testing",           "測試 + 寫 test case + Playwright 真實點擊（dispatch RunQaAsync）"),
        new SkillDescriptor("documentation",       "Documentation",        "寫文件 + README + commit message + PR body（dispatch RunReadOnlyAsync）"),
        new SkillDescriptor("ui_design",           "UI Design",            "UI/UX 設計（特定 UI 任務 Petra dispatch — dispatch RunReadOnlyAsync）"),
        new SkillDescriptor("release_publishing",  "Release Publishing",   "Release notes + version bump + 部署協調（dispatch RunAsync）"),
    };

    public IReadOnlyList<SkillDescriptor> All => _skills;

    public SkillDescriptor? GetByName(string name)
        => _skills.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));
}
