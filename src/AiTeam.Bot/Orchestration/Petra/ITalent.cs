using Microsoft.Agents.AI;

namespace AiTeam.Bot.Orchestration.Petra;

/// <summary>
/// Stage 67：v5.5 Phase 1 Step 2 — Talent 對 Petra Orchestrator 暴露的介面。
///
/// 演進設計（Aria 議題 3 路線 B endorse）：
/// - `Skills` — Talent 擔任的 N Skill 名稱 list（從 DB TalentSkill 一對多載入）
/// - `CreateAgent(ctx, skill)` 加 `skill` 參數動態傳 — 解 Talent 兼多 Skill 問題（Cody 兼 code_implementation + ui_design + release_publishing 必須 dispatch 時動態傳 skill 決定 ClaudeCodeChatClientAdapter capability）
///
/// Petra dispatch 流程：
/// 1. DecideTalentsAsync 回 `(List&lt;string&gt; skills, List&lt;ITalent&gt; talentPicks)` — skills index 對 talentPicks index
/// 2. DispatchTalentsAsync 對每個 i 呼叫 `talentPicks[i].CreateAgent(ctx, skills[i])` 動態建 ChatClientAgent + adapter capability=skill
/// </summary>
public interface ITalent
{
    /// <summary>Talent 名稱（"Cody" / "Vera" / "Quinn" / "Sage" / 未來客戶專案 "Cody-2" 等）。</summary>
    string Name { get; }

    /// <summary>Talent 擔任的 Skill 名稱 list（從 DB TalentSkill 一對多載入 — 對齊 ISkillRegistry 6 Skill）。</summary>
    IReadOnlyList<string> Skills { get; }

    /// <summary>
    /// Factory：建 AIAgent 給 Petra dispatch 用。
    /// 內部建 ClaudeCodeChatClientAdapter(capability=skill) + 包 ChatClientAgent（對齊既有 PetraWorkerHelper.BuildAgent pattern）。
    /// skill 必須在本 Talent.Skills 內（caller 負責驗 — Petra dispatch 時 lookup 已過濾）。
    /// </summary>
    AIAgent CreateAgent(PetraSessionContext ctx, string skill);
}
