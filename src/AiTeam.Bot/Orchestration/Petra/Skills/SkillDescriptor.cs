namespace AiTeam.Bot.Orchestration.Petra.Skills;

/// <summary>
/// Stage 67：v5.5 Phase 1 Step 2 — Skill descriptor（code-defined / 不開放動態加 Skill）。
/// Talent-Skill separation 概念：Skill = code 寫死 capability + 對應 IClaudeCodeService method dispatch；
/// Talent = DB-driven WebUI 動態加 instance（Phase 3 才開放 CRUD）。
/// </summary>
public sealed record SkillDescriptor(string Name, string DisplayName, string Description);
