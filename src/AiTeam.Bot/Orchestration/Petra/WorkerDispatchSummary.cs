namespace AiTeam.Bot.Orchestration.Petra;

/// <summary>
/// Stage 66：PetraOrchestratorService 自管 chain dispatch 過程的 worker 結果紀錄。
/// 用於 chain 中後續 worker input 拼接（BuildNextWorkerInput）+ PetraSessionMessages tool role 寫入對齊。
/// </summary>
internal sealed record WorkerDispatchSummary(
    string WorkerName,
    string Capability,
    string Output,
    string ToolCallId);
