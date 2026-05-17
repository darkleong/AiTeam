namespace AiTeam.Bot.Orchestration.Petra.Skills;

/// <summary>
/// Stage 67：v5.5 Phase 1 Step 2 — Skill descriptor（code-defined / 不開放動態加 Skill）。
/// Talent-Skill separation 概念：Skill = code 寫死 capability + 對應 IClaudeCodeService method dispatch；
/// Talent = DB-driven WebUI 動態加 instance（Phase 3 才開放 CRUD）。
///
/// Stage 74：v5.5 Phase 3 Step 8 — 加 RecommendedModelTier + ReturnTypeDescription metadata
///                                 對齊業界 2026 Agent Skills open standard format 第一步（簡化版避過早 over-engineer JSON Schema parameters 全套）。
///
/// RecommendedModelTier 三 tier 字串描述（描述用 / 不強制 — 真實 Model 由 TalentSkillModelResolver 三層 fallback chain resolve）：
/// - "cost-efficient" — 短文件 / 機械化任務（適合 Haiku tier）
/// - "standard"       — 一般主流任務（適合 Sonnet tier）
/// - "strategic"      — 複雜判斷 / 跨領域評估（適合 Opus tier）
///
/// ReturnTypeDescription：一句話描述 skill 輸出格式 — Petra orchestrator 動態決策 + WebUI 顯示參考用。
/// </summary>
public sealed record SkillDescriptor(
    string Name,
    string DisplayName,
    string Description,
    string RecommendedModelTier,
    string ReturnTypeDescription);
