// 測試標的：AiTeam.Dashboard.Components.Pages.Projects.ProjectManagement
// 驗證：grep 'class ProjectManagement' src/AiTeam.Dashboard/Components/Pages/Projects/ProjectManagement.razor.cs → 命中第 5 行

using System.Reflection;
using System.Runtime.CompilerServices;
using AiTeam.Dashboard.Components.Pages.Projects;
using AiTeam.Shared.Dtos;
using FluentAssertions;
using MudBlazor;
using Xunit;

namespace AiTeam.Dashboard.Components.Pages.Projects.Tests;

public class ProjectManagementTests
{
    private static ProjectManagement CreateInstance()
        => Activator.CreateInstance<ProjectManagement>();

    private static bool GetIsDrawerOpen(ProjectManagement instance)
        => (bool)typeof(ProjectManagement)
            .GetField("_isDrawerOpen", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(instance)!;

    private static void SetIsDrawerOpen(ProjectManagement instance, bool value)
        => typeof(ProjectManagement)
            .GetField("_isDrawerOpen", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(instance, value);

    private static ProjectDto? GetSelectedProject(ProjectManagement instance)
        => (ProjectDto?)typeof(ProjectManagement)
            .GetField("_selectedProject", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(instance);

    private static void InvokeCloseDrawer(ProjectManagement instance)
        => typeof(ProjectManagement)
            .GetMethod("CloseDrawer", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(instance, null);

    private static async Task InvokeOnRowClickAsync(ProjectManagement instance, TableRowClickEventArgs<ProjectDto> args)
    {
        var method = typeof(ProjectManagement).GetMethod(
            "OnRowClickAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        await (Task)method.Invoke(instance, new object[] { args })!;
    }

    // 繞過 TableRowClickEventArgs<T> 需要 MudTr 的建構子限制，使用 GetUninitializedObject 建立空白實例後設定 Item
    private static TableRowClickEventArgs<ProjectDto> CreateArgs(ProjectDto? item)
    {
        var args = (TableRowClickEventArgs<ProjectDto>)
            RuntimeHelpers.GetUninitializedObject(typeof(TableRowClickEventArgs<ProjectDto>));

        var backingField =
            typeof(TableRowClickEventArgs<ProjectDto>)
                .GetField("<Item>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? typeof(TableRowClickEventArgs<ProjectDto>)
                .GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
                .FirstOrDefault(f => f.Name.Contains("Item", StringComparison.OrdinalIgnoreCase));

        backingField?.SetValue(args, item);
        return args;
    }

    // ── CloseDrawer ──────────────────────────────────────────────────────────

    [Fact]
    public void CloseDrawer_抽屜開啟時_設定為關閉狀態()
    {
        var instance = CreateInstance();
        SetIsDrawerOpen(instance, true);

        InvokeCloseDrawer(instance);

        GetIsDrawerOpen(instance).Should().BeFalse();
    }

    [Fact]
    public void CloseDrawer_抽屜已關閉時_狀態保持關閉()
    {
        var instance = CreateInstance();
        SetIsDrawerOpen(instance, false);

        InvokeCloseDrawer(instance);

        GetIsDrawerOpen(instance).Should().BeFalse();
    }

    // ── OnRowClickAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task OnRowClickAsync_傳入非空專案項目_開啟抽屜並設定選中專案()
    {
        var instance = CreateInstance();
        var project  = new ProjectDto { Id = Guid.NewGuid(), Name = "測試專案" };
        var args     = CreateArgs(project);

        await InvokeOnRowClickAsync(instance, args);

        GetIsDrawerOpen(instance).Should().BeTrue();
        GetSelectedProject(instance).Should().Be(project);
    }

    [Fact]
    public async Task OnRowClickAsync_傳入空項目_不開啟抽屜並清除選中專案()
    {
        var instance = CreateInstance();
        SetIsDrawerOpen(instance, true);
        var args = CreateArgs(null);

        await InvokeOnRowClickAsync(instance, args);

        GetIsDrawerOpen(instance).Should().BeFalse();
        GetSelectedProject(instance).Should().BeNull();
    }
}
