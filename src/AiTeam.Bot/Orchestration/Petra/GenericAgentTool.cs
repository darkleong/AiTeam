using AiTeam.Bot.Agents;
using AiTeam.Bot.Services;
using AiTeam.Data;
using Microsoft.Agents.AI;

namespace AiTeam.Bot.Orchestration.Petra;

/// <summary>
/// Stage 67：v5.5 Phase 1 Step 2 — DB-driven Talent 通用實作（取代 7 個獨立 worker class 寫法）。
///
/// 演進設計（Aria 議題 3 路線 B endorse）：
/// - ctor 注入 `Talent talent` entity（從 DB load 含 Skills nav）+ DI 服務（IClaudeCodeService / TokenLogService / ILoggerFactory）
/// - Name / Skills 從 Talent entity 取（vs 既有 worker class 用 [AgentCapability] reflection）
/// - CreateAgent(ctx, skill) 動態建 ClaudeCodeChatClientAdapter — capability=skill 解 Talent 兼多 Skill 問題
///
/// DI 註冊：Program.cs 啟動時 from DB load all active Talent + skill.Count &gt; 0 → AddScoped&lt;ITalent&gt;(sp =&gt; new GenericAgentTool(talent, ...))
/// 一個 active Talent 對應一個 ITalent DI instance / Petra 透過 IEnumerable&lt;ITalent&gt; DI scan 取得所有 Talent。
///
/// Phase 3 動態 Talent CRUD 開放後限制：runtime 新 Talent 不會 hot pickup（DI container 啟動後不能 register）— Phase 3 才解。
/// </summary>
public sealed class GenericAgentTool : ITalent
{
    private readonly IClaudeCodeService _claudeCode;
    private readonly TokenLogService _tokenLogService;
    private readonly ILoggerFactory _loggerFactory;

    public string Name { get; }
    public IReadOnlyList<string> Skills { get; }

    public GenericAgentTool(
        Talent talent,
        IClaudeCodeService claudeCode,
        TokenLogService tokenLogService,
        ILoggerFactory loggerFactory)
    {
        Name = talent.Name;
        Skills = talent.Skills.Select(s => s.SkillName).ToList();
        _claudeCode = claudeCode;
        _tokenLogService = tokenLogService;
        _loggerFactory = loggerFactory;
    }

    /// <summary>
    /// Stage 67：動態建 AIAgent — 對齊既有 PetraWorkerHelper.BuildAgent 7 參數 pattern（v5 既有 worker class CreateAgent 走同 helper）。
    /// capability=skill 動態傳（解 Talent 兼多 Skill 問題）。
    /// instructions=null 對齊 v5 既有 path — ClaudeCodeChatClientAdapter dispatch 內 systemPrompt 由 capability → CLAUDE_*.md 動態載入。
    /// </summary>
    public AIAgent CreateAgent(PetraSessionContext ctx, string skill)
    {
        return PetraWorkerHelper.BuildAgent(
            claudeCode: _claudeCode,
            capability: skill,
            workerName: Name,
            instructions: $"Talent={Name} Skill={skill}（Stage 67 v5.5 dispatch）",
            ctx: ctx,
            tokenLogService: _tokenLogService,
            loggerFactory: _loggerFactory);
    }
}
