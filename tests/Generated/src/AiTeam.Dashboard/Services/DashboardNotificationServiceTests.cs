// 測試標的：AiTeam.Dashboard.Services.DashboardNotificationService
// 驗證：grep 'class DashboardNotificationService' src/AiTeam.Dashboard/Services/DashboardNotificationService.cs → 命中第 5 行

using AiTeam.Dashboard.Services;
using FluentAssertions;
using MudBlazor;
using NSubstitute;
using Xunit;

namespace AiTeam.Dashboard.Services.Tests;

public class DashboardNotificationServiceTests
{
    private readonly ISnackbar _snackbar = Substitute.For<ISnackbar>();
    private readonly DashboardNotificationService _svc;

    public DashboardNotificationServiceTests()
    {
        _svc = new DashboardNotificationService(_snackbar);
    }

    [Fact]
    public async Task ShowSuccessAsync_正常訊息_以Success嚴重性呼叫Snackbar()
    {
        await _svc.ShowSuccessAsync("已新增專案");

        _snackbar.Received(1).Add(
            "已新增專案",
            Severity.Success,
            Arg.Any<Action<SnackbarOptions>?>());
    }

    [Fact]
    public async Task ShowSuccessAsync_超過80字元訊息_截斷至83字元後傳入Snackbar()
    {
        var longMsg = new string('A', 100);

        await _svc.ShowSuccessAsync(longMsg);

        _snackbar.Received(1).Add(
            Arg.Is<string>(s => s.Length == 83 && s.EndsWith("...")),
            Severity.Success,
            Arg.Any<Action<SnackbarOptions>?>());
    }

    [Fact]
    public async Task ShowErrorAsync_正常訊息_以Error嚴重性呼叫Snackbar()
    {
        await _svc.ShowErrorAsync("切換狀態失敗，請稍後重試");

        _snackbar.Received(1).Add(
            "切換狀態失敗，請稍後重試",
            Severity.Error,
            Arg.Any<Action<SnackbarOptions>?>());
    }

    [Fact]
    public async Task ShowErrorAsync_超過80字元訊息_截斷至83字元後傳入Snackbar()
    {
        var longMsg = new string('E', 90);

        await _svc.ShowErrorAsync(longMsg);

        _snackbar.Received(1).Add(
            Arg.Is<string>(s => s.Length == 83 && s.EndsWith("...")),
            Severity.Error,
            Arg.Any<Action<SnackbarOptions>?>());
    }

    [Fact]
    public async Task ShowWarningAsync_正常訊息_以Warning嚴重性呼叫Snackbar()
    {
        await _svc.ShowWarningAsync("注意事項");

        _snackbar.Received(1).Add(
            "注意事項",
            Severity.Warning,
            Arg.Any<Action<SnackbarOptions>?>());
    }

    [Fact]
    public async Task ShowWarningAsync_恰好80字元訊息_不截斷直接傳入Snackbar()
    {
        var exactMsg = new string('W', 80);

        await _svc.ShowWarningAsync(exactMsg);

        _snackbar.Received(1).Add(
            Arg.Is<string>(s => s.Length == 80 && !s.EndsWith("...")),
            Severity.Warning,
            Arg.Any<Action<SnackbarOptions>?>());
    }

    [Fact]
    public void ClearAllToasts_呼叫後_清除所有Snackbar()
    {
        _svc.ClearAllToasts();

        _snackbar.Received(1).Clear();
    }
}
