// 測試標的：AiTeam.Dashboard.Components.Pages.Home.MockScenarioCard
// 驗證：grep -r 'class MockScenarioCard' src/AiTeam.Dashboard/ → 命中 MockScenarioCard.razor.cs:12

using System.Reflection;
using System.Threading.Tasks;
using AiTeam.Dashboard.Components.Pages.Home;
using FluentAssertions;
using MudBlazor;
using NSubstitute;
using Xunit;

namespace AiTeam.Dashboard.Components.Pages.Home.Tests;

public class MockScenarioCardTests
{
    private static void SetField(MockScenarioCard instance, string name, object? value)
        => typeof(MockScenarioCard)
            .GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(instance, value);

    private static T? GetField<T>(MockScenarioCard instance, string name)
        => (T?)typeof(MockScenarioCard)
            .GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(instance);

    private static async Task InvokeAsync(MockScenarioCard instance, string methodName)
    {
        var method = typeof(MockScenarioCard)
            .GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance)!;
        var result = method.Invoke(instance, null);
        if (result is Task task) await task;
    }

    private static MockScenarioCard CreateBareInstance()
    {
        var instance = new MockScenarioCard();
        typeof(MockScenarioCard)
            .GetProperty("Snackbar", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(instance, Substitute.For<ISnackbar>());
        return instance;
    }

    // ── TriggerAsync：guard clause — MockMode 未啟用 ───────────────────────────

    [Fact]
    public async Task TriggerAsync_MockMode未啟用_應提早返回不拋出例外()
    {
        var instance = CreateBareInstance();
        SetField(instance, "_mockModeEnabled", false);
        // BotService 為 null；若未提早返回會拋 NRE
        var act = async () => await InvokeAsync(instance, "TriggerAsync");

        await act.Should().NotThrowAsync("_mockModeEnabled=false 應觸發 guard clause，BotService 為 null 不拋例外");
    }

    [Fact]
    public async Task TriggerAsync_MockMode未啟用_isSubmitting應維持False()
    {
        var instance = CreateBareInstance();
        SetField(instance, "_mockModeEnabled", false);

        await InvokeAsync(instance, "TriggerAsync");

        GetField<bool>(instance, "_isSubmitting").Should().BeFalse("guard clause 提早返回，_isSubmitting 從未被設為 true");
    }

    // ── TriggerAsync：guard clause — scenario 為空 ────────────────────────────

    [Fact]
    public async Task TriggerAsync_Scenario為空白_應提早返回不拋出例外()
    {
        var instance = CreateBareInstance();
        SetField(instance, "_mockModeEnabled", true);
        SetField(instance, "_scenario", "");
        // BotService 為 null；若未提早返回會拋 NRE
        var act = async () => await InvokeAsync(instance, "TriggerAsync");

        await act.Should().NotThrowAsync("_scenario 為空應觸發 guard clause，BotService 為 null 不拋例外");
    }

    [Fact]
    public async Task TriggerAsync_Scenario為空白_isSubmitting應維持False()
    {
        var instance = CreateBareInstance();
        SetField(instance, "_mockModeEnabled", true);
        SetField(instance, "_scenario", "   ");

        await InvokeAsync(instance, "TriggerAsync");

        GetField<bool>(instance, "_isSubmitting").Should().BeFalse("guard clause 提早返回，_isSubmitting 從未被設為 true");
    }

    // ── TriggerAsync：error 初始狀態 ──────────────────────────────────────────

    [Fact]
    public async Task TriggerAsync_MockMode未啟用_error應維持null()
    {
        var instance = CreateBareInstance();
        SetField(instance, "_mockModeEnabled", false);

        await InvokeAsync(instance, "TriggerAsync");

        GetField<string?>(instance, "_error").Should().BeNull("guard clause 提早返回，_error 從未被設定");
    }
}
