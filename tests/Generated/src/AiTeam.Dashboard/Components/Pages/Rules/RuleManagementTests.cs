// 測試標的：AiTeam.Dashboard.Components.Pages.Rules.RuleManagement
// 驗證：grep -r 'class RuleManagement' src/AiTeam.Dashboard/ → 命中 RuleManagement.razor.cs:5

using System.Collections.Generic;
using System.Reflection;
using AiTeam.Data;
using AiTeam.Dashboard.Components.Pages.Rules;
using AiTeam.Dashboard.Services;
using FluentAssertions;
using MudBlazor;
using NSubstitute;
using Xunit;

namespace AiTeam.Dashboard.Components.Pages.Rules.Tests;

public class RuleManagementTests
{
    // ── 輔助：依賴注入 & 私有存取 ───────────────────────────────────────────

    private static void SetProperty(RuleManagement instance, string propName, object? value)
        => typeof(RuleManagement)
            .GetProperty(propName, BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(instance, value);

    private static T? GetField<T>(RuleManagement instance, string fieldName)
        => (T?)typeof(RuleManagement)
            .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(instance);

    private static Task InvokeToggleActiveAsync(RuleManagement instance, Rule rule, bool isActive)
    {
        var method = typeof(RuleManagement).GetMethod(
            "ToggleActiveAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        return (Task)method.Invoke(instance, new object[] { rule, isActive })!;
    }

    private static Task InvokeDeleteRuleAsync(RuleManagement instance, Guid id)
    {
        var method = typeof(RuleManagement).GetMethod(
            "DeleteRuleAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        return (Task)method.Invoke(instance, new object[] { id })!;
    }

    private static RuleManagement CreateFailingInstance(ISnackbar snackbar)
    {
        var instance = new RuleManagement();
        SetProperty(instance, "Snackbar", snackbar);
        SetProperty(instance, "RuleService", new DashboardRuleService(null!));
        return instance;
    }

    // ── GetAgentChipColor ─────────────────────────────────────────────────

    private static Color InvokeGetAgentChipColor(string? agentName)
    {
        var method = typeof(RuleManagement).GetMethod(
            "GetAgentChipColor",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        return (Color)method.Invoke(null, new object?[] { agentName })!;
    }

    // ── GetAgentChipColor ─────────────────────────────────────────────────

    [Fact]
    public void GetAgentChipColor_CEO_應回傳Primary色()
    {
        InvokeGetAgentChipColor("CEO").Should().Be(Color.Primary);
    }

    [Fact]
    public void GetAgentChipColor_Dev_應回傳Info色()
    {
        InvokeGetAgentChipColor("Dev").Should().Be(Color.Info);
    }

    [Fact]
    public void GetAgentChipColor_Reviewer_應回傳Error色()
    {
        InvokeGetAgentChipColor("Reviewer").Should().Be(Color.Error);
    }

    [Fact]
    public void GetAgentChipColor_Release_應回傳Success色()
    {
        InvokeGetAgentChipColor("Release").Should().Be(Color.Success);
    }

    [Fact]
    public void GetAgentChipColor_Null全域_應回傳Default色()
    {
        InvokeGetAgentChipColor(null).Should().Be(Color.Default);
    }

    [Fact]
    public void GetAgentChipColor_未知AgentName_應回傳Default色()
    {
        InvokeGetAgentChipColor("UnknownAgent").Should().Be(Color.Default);
    }

    // ── ToggleActiveAsync：例外路徑 ───────────────────────────────────────

    [Fact]
    public async Task ToggleActiveAsync_服務拋出例外_應設置formError欄位()
    {
        var snackbar = Substitute.For<ISnackbar>();
        var instance = CreateFailingInstance(snackbar);
        var rule = new Rule { Id = Guid.NewGuid(), Content = "Test rule", IsActive = false };

        await InvokeToggleActiveAsync(instance, rule, true);

        GetField<string?>(instance, "_formError").Should().StartWith("狀態切換失敗：");
    }

    [Fact]
    public async Task ToggleActiveAsync_服務拋出例外_IsActive不應被更新()
    {
        var snackbar = Substitute.For<ISnackbar>();
        var instance = CreateFailingInstance(snackbar);
        var rule = new Rule { Id = Guid.NewGuid(), Content = "Test rule", IsActive = false };

        await InvokeToggleActiveAsync(instance, rule, true);

        rule.IsActive.Should().BeFalse("服務失敗時不應更新 IsActive");
    }

    [Fact]
    public async Task ToggleActiveAsync_服務拋出例外_應呼叫ErrorSnackbar()
    {
        var snackbar = Substitute.For<ISnackbar>();
        var instance = CreateFailingInstance(snackbar);
        var rule = new Rule { Id = Guid.NewGuid(), Content = "Test rule", IsActive = false };

        await InvokeToggleActiveAsync(instance, rule, true);

        snackbar.Received(1).Add(Arg.Any<string>(), Severity.Error, Arg.Any<Action<SnackbarOptions>?>());
    }

    // ── DeleteRuleAsync：例外路徑 ─────────────────────────────────────────

    [Fact]
    public async Task DeleteRuleAsync_服務拋出例外_應設置formError欄位()
    {
        var snackbar = Substitute.For<ISnackbar>();
        var instance = CreateFailingInstance(snackbar);

        await InvokeDeleteRuleAsync(instance, Guid.NewGuid());

        GetField<string?>(instance, "_formError").Should().StartWith("刪除失敗：");
    }

    [Fact]
    public async Task DeleteRuleAsync_服務拋出例外_不應移除清單中的規則()
    {
        var snackbar = Substitute.For<ISnackbar>();
        var instance = CreateFailingInstance(snackbar);
        var ruleId = Guid.NewGuid();
        var rule = new Rule { Id = ruleId, Content = "Test rule" };
        GetField<List<Rule>>(instance, "_rules")!.Add(rule);

        await InvokeDeleteRuleAsync(instance, ruleId);

        GetField<List<Rule>>(instance, "_rules")!.Should().Contain(rule, "服務失敗時不應移除規則");
    }

    [Fact]
    public async Task DeleteRuleAsync_服務拋出例外_應呼叫ErrorSnackbar()
    {
        var snackbar = Substitute.For<ISnackbar>();
        var instance = CreateFailingInstance(snackbar);

        await InvokeDeleteRuleAsync(instance, Guid.NewGuid());

        snackbar.Received(1).Add(Arg.Any<string>(), Severity.Error, Arg.Any<Action<SnackbarOptions>?>());
    }
}
