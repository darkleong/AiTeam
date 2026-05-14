namespace AiTeam.Bot.Orchestration.Petra;

/// <summary>
/// Stage 63B：Petra Orchestrator session context（v5 動態架構 PoC）。
/// 從 PetraOrchestratorService 傳到 Worker.CreateAgent — Worker 內部建 ClaudeCodeChatClientAdapter 用。
/// Mock 階段 Model / ApiKey / WorkingDir 可為空字串（IClaudeCodeService DI proxy 自動接管 Mock fixture）。
/// </summary>
public sealed record PetraSessionContext(
    Guid SessionId,
    Guid TaskGroupId,
    int Round,
    string Model,
    string ApiKey,
    string WorkingDir);
