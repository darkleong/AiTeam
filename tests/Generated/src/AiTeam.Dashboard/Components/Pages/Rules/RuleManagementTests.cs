// 測試標的：AiTeam.Dashboard.Components.Pages.Rules.RuleManagement
// 驗證：grep -r 'class RuleManagement' src/AiTeam.Dashboard/ → 命中 RuleManagement.razor.cs:5

using System.Reflection;
using AiTeam.Dashboard.Components.Pages.Rules;
using FluentAssertions;
using MudBlazor;
using Xunit;

namespace AiTeam.Dashboard.Components.Pages.Rules.Tests;

public class RuleManagementTests
{
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
}
