// 測試標的：AiTeam.Dashboard.Components.Pages.Home.Home
// 驗證：grep -r 'partial class Home' src/AiTeam.Dashboard/ → 命中 Home.razor.cs:10

using System.Collections.Generic;
using System.Reflection;
using AiTeam.Dashboard.Components.Pages.Home;
using AiTeam.Shared.ViewModels;
using FluentAssertions;
using MudBlazor;
using Xunit;

namespace AiTeam.Dashboard.Components.Pages.Home.Tests;

// 注意：ConnectSignalRAsync / OnInitializedAsync 含完整 SignalR 環境依賴，
// 與 TaskCenter 同類型元件相同，整合測試不在此涵蓋。
public class HomeTests
{
    // ── 輔助：呼叫私有靜態方法 ──────────────────────────────────────────────

    private static string InvokeWorkflowTypeLabel(string? workflowType)
    {
        var method = typeof(Home).GetMethod(
            "WorkflowTypeLabel",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        return (string)method.Invoke(null, new object?[] { workflowType })!;
    }

    private static Color InvokeWorkflowTypeColor(string? workflowType)
    {
        var method = typeof(Home).GetMethod(
            "WorkflowTypeColor",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        return (Color)method.Invoke(null, new object?[] { workflowType })!;
    }

    private static void InvokeUpdateAgentStatus(Home instance, AgentStatusViewModel updated)
    {
        var method = typeof(Home).GetMethod(
            "UpdateAgentStatus",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        method.Invoke(instance, new object[] { updated });
    }

    private static List<AgentStatusViewModel> GetAgentStatuses(Home instance)
        => (List<AgentStatusViewModel>)typeof(Home)
            .GetField("_agentStatuses", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(instance)!;

    private static void SetAgentStatuses(Home instance, List<AgentStatusViewModel> statuses)
        => typeof(Home)
            .GetField("_agentStatuses", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(instance, statuses);

    // ── WorkflowTypeLabel ─────────────────────────────────────────────────────

    [Fact]
    public void WorkflowTypeLabel_新功能類型_應回傳中文新功能標籤()
    {
        InvokeWorkflowTypeLabel("new_feature").Should().Be("新功能");
    }

    [Fact]
    public void WorkflowTypeLabel_BugFix類型_應回傳BugFix標籤()
    {
        InvokeWorkflowTypeLabel("bug_fix").Should().Be("Bug Fix");
    }

    [Fact]
    public void WorkflowTypeLabel_技術改善類型_應回傳技術改善標籤()
    {
        InvokeWorkflowTypeLabel("tech_improvement").Should().Be("技術改善");
    }

    [Fact]
    public void WorkflowTypeLabel_未知類型_應回傳原始字串值()
    {
        InvokeWorkflowTypeLabel("unknown_type").Should().Be("unknown_type");
    }

    [Fact]
    public void WorkflowTypeLabel_Null_應回傳空字串()
    {
        InvokeWorkflowTypeLabel(null).Should().Be("");
    }

    // ── WorkflowTypeColor ─────────────────────────────────────────────────────

    [Fact]
    public void WorkflowTypeColor_新功能類型_應回傳Primary顏色()
    {
        InvokeWorkflowTypeColor("new_feature").Should().Be(Color.Primary);
    }

    [Fact]
    public void WorkflowTypeColor_BugFix類型_應回傳Warning顏色()
    {
        InvokeWorkflowTypeColor("bug_fix").Should().Be(Color.Warning);
    }

    [Fact]
    public void WorkflowTypeColor_技術改善類型_應回傳Secondary顏色()
    {
        InvokeWorkflowTypeColor("tech_improvement").Should().Be(Color.Secondary);
    }

    [Fact]
    public void WorkflowTypeColor_未知類型_應回傳Default顏色()
    {
        InvokeWorkflowTypeColor("unknown_type").Should().Be(Color.Default);
    }

    [Fact]
    public void WorkflowTypeColor_Null_應回傳Default顏色()
    {
        InvokeWorkflowTypeColor(null).Should().Be(Color.Default);
    }

    // ── UpdateAgentStatus ─────────────────────────────────────────────────────

    [Fact]
    public void UpdateAgentStatus_Agent存在於清單中_應更新對應狀態()
    {
        var instance = new Home();
        SetAgentStatuses(instance, new List<AgentStatusViewModel>
        {
            new() { AgentName = "Cody", Status = "idle" }
        });

        InvokeUpdateAgentStatus(instance, new AgentStatusViewModel { AgentName = "Cody", Status = "running" });

        GetAgentStatuses(instance)[0].Status.Should().Be("running");
    }

    [Fact]
    public void UpdateAgentStatus_Agent不在白名單清單中_清單長度不應改變且現有Agent不被取代()
    {
        var instance = new Home();
        SetAgentStatuses(instance, new List<AgentStatusViewModel>
        {
            new() { AgentName = "Cody", Status = "idle" }
        });

        // "Dev_plan" 是 workflow-only 名稱，不在初始清單中，應被忽略（白名單過濾）
        InvokeUpdateAgentStatus(instance, new AgentStatusViewModel { AgentName = "Dev_plan", Status = "running" });

        var statuses = GetAgentStatuses(instance);
        statuses.Should().HaveCount(1);
        statuses[0].AgentName.Should().Be("Cody");
        statuses[0].Status.Should().Be("idle");
    }
}
