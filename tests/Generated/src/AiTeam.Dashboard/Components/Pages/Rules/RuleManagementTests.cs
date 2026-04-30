// 測試標的：AiTeam.Dashboard.Components.Pages.Rules.RuleManagement
// 驗證：grep 'class RuleManagement' src/AiTeam.Dashboard/Components/Pages/Rules/RuleManagement.razor.cs → 命中第 5 行

using System.Reflection;
using AiTeam.Dashboard.Components.Pages.Rules;
using AiTeam.Shared.Constants;
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

    [Fact]
    public void GetAgentChipColor_CEO代理人_回傳Primary顏色()
    {
        var result = InvokeGetAgentChipColor(AgentNames.Ceo);

        result.Should().Be(Color.Primary);
    }

    [Fact]
    public void GetAgentChipColor_Dev代理人_回傳Info顏色()
    {
        var result = InvokeGetAgentChipColor(AgentNames.Dev);

        result.Should().Be(Color.Info);
    }

    [Fact]
    public void GetAgentChipColor_QA代理人_回傳Secondary顏色()
    {
        var result = InvokeGetAgentChipColor(AgentNames.Qa);

        result.Should().Be(Color.Secondary);
    }

    [Fact]
    public void GetAgentChipColor_Reviewer代理人_回傳Error顏色()
    {
        var result = InvokeGetAgentChipColor(AgentNames.Reviewer);

        result.Should().Be(Color.Error);
    }

    [Fact]
    public void GetAgentChipColor_Release代理人_回傳Success顏色()
    {
        var result = InvokeGetAgentChipColor(AgentNames.Release);

        result.Should().Be(Color.Success);
    }

    [Fact]
    public void GetAgentChipColor_Null代理人名稱_回傳Default顏色()
    {
        var result = InvokeGetAgentChipColor(null);

        result.Should().Be(Color.Default);
    }

    [Fact]
    public void GetAgentChipColor_未知代理人名稱_回傳Default顏色()
    {
        var result = InvokeGetAgentChipColor("UnknownAgent");

        result.Should().Be(Color.Default);
    }
}
