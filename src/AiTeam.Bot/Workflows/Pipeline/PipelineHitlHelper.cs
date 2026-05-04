using Microsoft.Agents.AI.Workflows;

namespace AiTeam.Bot.Workflows.Pipeline;

/// <summary>
/// Stage 55B：Pipeline HITL 共用 helper（議題 2 = 2C 拍板：Pattern A 主 + Stage 51 試點獨立保留）。
///
/// 設計目的：
///   Stage 55A 已驗證 RequestPort + ResumeWithResponseAsync pattern（kickoff / design 兩 type，×0.88 校準錨）
///   Stage 55B 推廣到 8 個新 type — 每個 Pipeline Stage Executor 內 yield 邏輯本質相同：
///     ① BossInteraction 已開（fire-and-forget by 既有 NotifyBossXxxAsync method）
///     ② SendMessageAsync(XxxCompletionRequest(groupId)) 觸發 RequestPort emit RequestInfoEvent
///     ③ Workflow yield 等 Christ 回應 — 由 router watch 後 ResumeWithResponseAsync 餵 XxxCompletionResponse
///
/// 本 helper 只負責 step ② + 統一 log（abstraction value 故意保持最小，避免過度設計）。
/// 真正共用的「open BossInteraction」邏輯仍在 TaskGroupService / AppealOrchestrationService 既有 NotifyBossXxxAsync method。
///
/// 對齊紀律：
///   - 不抽 base class（Stage 51 試點 353 行單 type 邏輯不抽，55B 多 type 沿用 Pattern A 自然不需共用 base）
///   - Stage 51 試點 framework_kickoff_mid_interrupt 獨立保留（本質不同 — Petra meeting 中途介入 trigger）
/// </summary>
internal static class PipelineHitlHelper
{
    /// <summary>
    /// Stage 55B：Pipeline HITL yield 統一 helper — 把 CompletionRequest record 送進 RequestPort，
    /// log 一致並 yield 等 Christ 回應（由 router ResumeWithResponseAsync 觸發 SendResponseAsync 喚醒）。
    /// </summary>
    public static async ValueTask YieldForChristResponseAsync<TRequest>(
        IWorkflowContext context,
        TRequest completionRequest,
        ILogger logger,
        string interactionType,
        Guid groupId)
        where TRequest : notnull
    {
        logger.LogInformation(
            "[Stage55B] {Type} HITL：BossInteraction 已開，SendMessage CompletionRequest yield 等 Christ 回應（Group={Id}）",
            interactionType, groupId);

        await context.SendMessageAsync(completionRequest);
    }
}
