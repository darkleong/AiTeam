using AiTeam.Dashboard.Services;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace AiTeam.Dashboard.Components.Pages.Home;

/// <summary>
/// Stage 33：全域佇列控制（stop-all / resume-all），對齊 Discord 指令。
/// </summary>
public partial class GlobalQueueControlCard
{
    [Inject] private DashboardBotService BotService    { get; set; } = null!;
    [Inject] private IDialogService      DialogService { get; set; } = null!;
    [Inject] private ISnackbar            Snackbar      { get; set; } = null!;

    private bool _loading;

    private async Task StopAllAsync()
    {
        var confirmed = await DialogService.ShowMessageBox(
            "確認緊急停止",
            "確定要暫停所有 Agent 佇列消費？\n\n" +
            "• 正在執行的任務會跑完當輪，完成後自動進入 stopped 狀態\n" +
            "• 新任務會留在佇列，直到按下「全部恢復」才會繼續消費",
            yesText: "確認停止",
            cancelText: "取消");

        if (confirmed != true) return;

        _loading = true;
        var ok = await BotService.StopAllAsync();
        _loading = false;

        Snackbar.Add(
            ok ? "🛑 已送出緊急停止指令，所有 Agent 轉為 stopping"
               : "送出緊急停止指令失敗，請確認 Bot 服務正常",
            ok ? Severity.Warning : Severity.Error);
    }

    private async Task ResumeAllAsync()
    {
        _loading = true;
        var ok = await BotService.ResumeAllAsync();
        _loading = false;

        Snackbar.Add(
            ok ? "▶️ 已送出恢復指令，所有 Agent 回到 active"
               : "送出恢復指令失敗，請確認 Bot 服務正常",
            ok ? Severity.Success : Severity.Error);
    }
}
