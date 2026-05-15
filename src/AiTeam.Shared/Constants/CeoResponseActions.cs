namespace AiTeam.Shared.Constants;

/// <summary>
/// CeoResponse.Action 常數 — CeoAgentService 寫入 / 下游 confirm 接力 service 讀取。
/// 抽常數避免 magic string 跨檔散布（Stage 68 抽出對齊 AgentNames pattern）。
/// </summary>
public static class CeoResponseActions
{
    /// <summary>
    /// Stage 63B：v5/v5.5 path Petra 動態調度完成 marker。
    /// CeoAgentService.ProcessWithClaudeCodeAsync forward 到 PetraOrchestratorService.StartAsync 後寫入；
    /// ProposalConfirmationService.ProcessCeoConfirmAsync / ButtonCallbackRouter.HandleCeoConfirmYesAsync
    /// 讀取後跳過 TaskItem 創建 + exec_confirm fire（Petra 已完成工作 — 收尾乾淨無 UI 雜訊）。
    /// Stage 68 = Trial_v12 揭 stale 卡議題修法 marker。
    /// </summary>
    public const string PetraV5Dispatched = "petra_v5_dispatched";
}
