using MudBlazor;

namespace AiTeam.Dashboard.Services;

/// <summary>集中管理 Dashboard 通知，提供 Snackbar 時間分級策略。</summary>
public class DashboardNotificationService(ISnackbar snackbar)
{
    /// <summary>顯示成功訊息（3 秒自動消失）。</summary>
    public void ShowSuccess(string message)
        => snackbar.Add(message, Severity.Success, configure: options =>
        {
            options.VisibleStateDuration = 3000;
        });

    /// <summary>顯示資訊通知（3 秒自動消失）。</summary>
    public void ShowInfo(string message)
        => snackbar.Add(message, Severity.Info, configure: options =>
        {
            options.VisibleStateDuration = 3000;
        });

    /// <summary>顯示警告（5 秒自動消失）。</summary>
    public void ShowWarning(string message)
        => snackbar.Add(message, Severity.Warning, configure: options =>
        {
            options.VisibleStateDuration = 5000;
        });

    /// <summary>顯示錯誤（8 秒後自動消失，支援非同步 undo 回調）。</summary>
    public void ShowError(string message, Func<Task>? onUndo = null)
        => snackbar.Add(message, Severity.Error, configure: options =>
        {
            options.VisibleStateDuration = 8000;
            if (onUndo is not null)
            {
                options.Action = "撤銷";
                options.ActionColor = Color.Warning;
                options.OnClick = _ => onUndo();
            }
        });
}
