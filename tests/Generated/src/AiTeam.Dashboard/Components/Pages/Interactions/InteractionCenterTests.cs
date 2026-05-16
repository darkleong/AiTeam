// 測試標的：AiTeam.Dashboard.Components.Pages.Interactions.InteractionCenter
// 驗證：grep -r 'class InteractionCenter' src/AiTeam.Dashboard/ → 命中 InteractionCenter.razor.cs:8

using System.Reflection;
using AiTeam.Dashboard.Components.Pages.Interactions;
using FluentAssertions;
using MudBlazor;
using Xunit;

namespace AiTeam.Dashboard.Components.Pages.Interactions.Tests;

public class InteractionCenterTests
{
    private static string InvokeGetInteractionIcon(string type)
    {
        var method = typeof(InteractionCenter).GetMethod(
            "GetInteractionIcon",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        return (string)method.Invoke(null, new object?[] { type })!;
    }

    private static Color InvokeGetInteractionColor(string type)
    {
        var method = typeof(InteractionCenter).GetMethod(
            "GetInteractionColor",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        return (Color)method.Invoke(null, new object?[] { type })!;
    }

    private static string InvokeGetInteractionLabel(string type)
    {
        var method = typeof(InteractionCenter).GetMethod(
            "GetInteractionLabel",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        return (string)method.Invoke(null, new object?[] { type })!;
    }

    private static Color InvokeGetActionColor(string? action)
    {
        var method = typeof(InteractionCenter).GetMethod(
            "GetActionColor",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        return (Color)method.Invoke(null, new object?[] { action })!;
    }

    // ── GetInteractionIcon ────────────────────────────────────────────────

    [Fact]
    public void GetInteractionIcon_ceo_confirm_應回傳Assignment圖示()
    {
        InvokeGetInteractionIcon("ceo_confirm").Should().Be(Icons.Material.Filled.Assignment);
    }

    [Fact]
    public void GetInteractionIcon_未知類型_應回傳Notifications預設圖示()
    {
        InvokeGetInteractionIcon("unknown_type").Should().Be(Icons.Material.Filled.Notifications);
    }

    // ── GetInteractionColor ───────────────────────────────────────────────

    [Fact]
    public void GetInteractionColor_merge_notify_應回傳Success色()
    {
        InvokeGetInteractionColor("merge_notify").Should().Be(Color.Success);
    }

    [Fact]
    public void GetInteractionColor_intervention_應回傳Error色()
    {
        InvokeGetInteractionColor("intervention").Should().Be(Color.Error);
    }

    [Fact]
    public void GetInteractionColor_devplan_escalate_應回傳Warning色()
    {
        InvokeGetInteractionColor("devplan_escalate").Should().Be(Color.Warning);
    }

    [Fact]
    public void GetInteractionColor_未知類型_應回傳Default色()
    {
        InvokeGetInteractionColor("unknown_type").Should().Be(Color.Default);
    }

    // ── GetInteractionLabel ───────────────────────────────────────────────

    [Fact]
    public void GetInteractionLabel_ceo_confirm_應回傳CEO決策確認()
    {
        InvokeGetInteractionLabel("ceo_confirm").Should().Be("CEO 決策確認");
    }

    [Fact]
    public void GetInteractionLabel_merge_notify_應回傳全流程完成()
    {
        InvokeGetInteractionLabel("merge_notify").Should().Be("全流程完成");
    }

    [Fact]
    public void GetInteractionLabel_未知類型_應直接回傳類型字串本身()
    {
        const string unknownType = "custom_unknown_type";
        InvokeGetInteractionLabel(unknownType).Should().Be(unknownType);
    }

    // ── GetActionColor ────────────────────────────────────────────────────

    [Fact]
    public void GetActionColor_confirm_yes_應回傳Success色()
    {
        InvokeGetActionColor("confirm_yes").Should().Be(Color.Success);
    }

    [Fact]
    public void GetActionColor_confirm_no_應回傳Error色()
    {
        InvokeGetActionColor("confirm_no").Should().Be(Color.Error);
    }

    [Fact]
    public void GetActionColor_devplan_skip_應回傳Warning色()
    {
        InvokeGetActionColor("devplan_skip").Should().Be(Color.Warning);
    }

    [Fact]
    public void GetActionColor_Null_應回傳Default色()
    {
        InvokeGetActionColor(null).Should().Be(Color.Default);
    }
}
