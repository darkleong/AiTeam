// 測試標的：AiTeam.Dashboard.Components.Pages.Home.GlobalQueueControlCard
// 驗證：grep -r 'class GlobalQueueControlCard' src/AiTeam.Dashboard/ → 命中 GlobalQueueControlCard.razor.cs:11

using System.Reflection;
using System.Threading.Tasks;
using AiTeam.Dashboard.Components.Pages.Home;
using FluentAssertions;
using MudBlazor;
using NSubstitute;
using Xunit;

namespace AiTeam.Dashboard.Components.Pages.Home.Tests;

public class GlobalQueueControlCardTests
{
    private static void SetProperty(GlobalQueueControlCard instance, string name, object? value)
        => typeof(GlobalQueueControlCard)
            .GetProperty(name, BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(instance, value);

    private static T? GetField<T>(GlobalQueueControlCard instance, string name)
        => (T?)typeof(GlobalQueueControlCard)
            .GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(instance);

    private static async Task InvokeAsync(GlobalQueueControlCard instance, string methodName)
    {
        var method = typeof(GlobalQueueControlCard)
            .GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance)!;
        var result = method.Invoke(instance, null);
        if (result is Task task) await task;
    }

    private static GlobalQueueControlCard CreateWithDialogAndSnackbar()
    {
        var instance = new GlobalQueueControlCard();
        // NSubstitute default for Task<bool?> returns null, which triggers confirmed != true guard
        SetProperty(instance, "DialogService", Substitute.For<IDialogService>());
        SetProperty(instance, "Snackbar", Substitute.For<ISnackbar>());
        return instance;
    }

    // ── StopAllAsync：dialog 預設返回 null → guard clause 提早返回 ───────────────

    [Fact]
    public async Task StopAllAsync_DialogService預設返回null_應直接返回不拋出例外()
    {
        // NSubstitute 對 Task<bool?> 方法預設返回 Task.FromResult<bool?>(null)
        // confirmed != true → 提早返回，BotService 為 null 也不拋例外
        var instance = CreateWithDialogAndSnackbar();

        var act = async () => await InvokeAsync(instance, "StopAllAsync");

        await act.Should().NotThrowAsync("dialog 返回 null 後 guard clause 應直接返回，BotService 為 null 不拋例外");
    }

    [Fact]
    public async Task StopAllAsync_DialogService預設返回null_loading應維持False()
    {
        var instance = CreateWithDialogAndSnackbar();

        await InvokeAsync(instance, "StopAllAsync");

        GetField<bool>(instance, "_loading").Should().BeFalse("guard clause 提早返回前 _loading 從未被設定為 true");
    }

    [Fact]
    public async Task StopAllAsync_DialogService預設返回null_error應維持null()
    {
        var instance = CreateWithDialogAndSnackbar();

        await InvokeAsync(instance, "StopAllAsync");

        GetField<string?>(instance, "_error").Should().BeNull("dialog 取消後 _error 應維持 null");
    }
}
