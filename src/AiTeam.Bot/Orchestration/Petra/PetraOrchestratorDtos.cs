using System.Text.Json.Serialization;

namespace AiTeam.Bot.Orchestration.Petra;

// Stage 84：拆出 PetraOrchestratorService nested record 集中此檔（對齊 refactor-sop.md SOP 1 / Internal DTO 集中）
// 全 internal sealed — 不對外部 namespace 暴露 / 跨 Petra 子 service 透過同 namespace 直接引用

/// <summary>Stage 80：plan_confirm BossInteraction.ContextJson 結構 — resume edit/respond path 重 decide 用。</summary>
internal sealed record PlanConfirmContext(
    Guid SessionId,
    string TaskInput,
    List<PlanConfirmSubtask> Subtasks,
    List<PlanConfirmDependency> Dependencies,
    List<string> TalentNames);

internal sealed record PlanConfirmSubtask(int Id, string Skill, string Description, string TalentName, bool NeedsImageContext);

internal sealed record PlanConfirmDependency(int From, int To, string Type);

/// <summary>Stage 81：DispatchTalentsAsync 回傳結構 — Summaries（已完成 subtask 摘要）+ Replan（觸發信號 / null = 正常完成）。</summary>
internal sealed record DispatchOutcome(
    List<WorkerDispatchSummary> Summaries,
    ReplanSignal? Replan);

/// <summary>Stage 81：replan / cap-reached 觸發信號 — caller 路由分支 HandleReplanSignalAsync 用。
/// Kind="replan_confirm" → 開卡 + Replanning result / Kind="cap_reached_iter|cost" → intervention card + Cancelled result。</summary>
internal sealed record ReplanSignal(
    string Kind,
    int CurrentSubtaskId,
    string TriggerReason,
    string RetryInstruction,
    string LastOutputPreview,
    int CompletedCount,
    int ReplanIteration,
    int? CapValueInt,
    decimal? CapValueDecimal);

/// <summary>Stage 81：Petra LLM JSON 回應結構 — LangGraph cycles 紀律：不回 plan 結構 / 只回 retry instruction（W8 對齊）。</summary>
internal sealed record ReplanDecisionJson(
    [property: JsonPropertyName("shouldReplan")]     bool   ShouldReplan,
    [property: JsonPropertyName("reason")]           string Reason,
    [property: JsonPropertyName("retryInstruction")] string RetryInstruction,
    [property: JsonPropertyName("targetSubtaskId")]  int    TargetSubtaskId);

/// <summary>Stage 81：replan_confirm BossInteraction.ContextJson 結構 — render UI + Resume 用。</summary>
internal sealed record ReplanConfirmContext(
    Guid   SessionId,
    string TaskInput,
    int    CurrentSubtaskId,
    string CurrentSkillName,
    string CurrentTalentName,
    string TriggerReason,
    string RetryInstruction,
    string LastOutputPreview,
    int    CompletedCount,
    int    ReplanIteration);
