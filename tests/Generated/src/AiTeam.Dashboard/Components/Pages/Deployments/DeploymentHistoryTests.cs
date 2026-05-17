// 測試標的：AiTeam.Dashboard.Components.Pages.Deployments.DeploymentHistory
// 驗證：grep -r 'partial class DeploymentHistory' src/AiTeam.Dashboard/ → 命中 DeploymentHistory.razor.cs:5

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using AiTeam.Dashboard.Components.Pages.Deployments;
using AiTeam.Dashboard.Services;
using AiTeam.Shared.Dtos;
using FluentAssertions;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;
using NSubstitute;
using Xunit;

namespace AiTeam.Dashboard.Components.Pages.Deployments.Tests;

public class DeploymentHistoryTests
{
    // ── 輔助 ──────────────────────────────────────────────────────────────────

    private static void SetProperty(DeploymentHistory instance, string propertyName, object? value)
        => typeof(DeploymentHistory)
            .GetProperty(propertyName, BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(instance, value);

    private static T? GetField<T>(DeploymentHistory instance, string fieldName)
        => (T?)typeof(DeploymentHistory)
            .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(instance);

    // ── OnInitializedAsync：服務失敗（catch 路徑）────────────────────────────

    [Fact]
    public async Task OnInitializedAsync_服務拋出例外_不應拋出未處理例外()
    {
        var instance = new DeploymentHistory();
        SetProperty(instance, "Snackbar", Substitute.For<ISnackbar>());
        SetProperty(instance, "TaskService", new DashboardTaskService(null!));

        var method = typeof(DeploymentHistory)
            .GetMethod("OnInitializedAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        Func<Task> act = async () => await (Task)method.Invoke(instance, null)!;

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task OnInitializedAsync_服務拋出例外_deployments清單應為空()
    {
        var instance = new DeploymentHistory();
        SetProperty(instance, "Snackbar", Substitute.For<ISnackbar>());
        SetProperty(instance, "TaskService", new DashboardTaskService(null!));

        var method = typeof(DeploymentHistory)
            .GetMethod("OnInitializedAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        await (Task)method.Invoke(instance, null)!;

        GetField<List<TaskItemDto>>(instance, "_deployments").Should().BeEmpty();
    }

    // ── OnRowClickAsync：null 防護 ────────────────────────────────────────────

    [Fact]
    public async Task OnRowClickAsync_Item為null_不應拋出例外且Drawer不應開啟()
    {
        var instance = new DeploymentHistory();
        SetProperty(instance, "Snackbar", Substitute.For<ISnackbar>());
        SetProperty(instance, "TaskService", new DashboardTaskService(null!));

        var method = typeof(DeploymentHistory)
            .GetMethod("OnRowClickAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var args = new TableRowClickEventArgs<TaskItemDto>();
        Func<Task> act = async () => await (Task)method.Invoke(instance, new object[] { args })!;

        await act.Should().NotThrowAsync();
        GetField<bool>(instance, "_isDrawerOpen").Should().BeFalse("Item 為 null 時不應開啟 Drawer");
    }

    // ── OnRowClickAsync：服務失敗（catch 路徑）───────────────────────────────

    [Fact]
    public async Task OnRowClickAsync_服務拋出例外_Drawer不應開啟()
    {
        var instance = new DeploymentHistory();
        SetProperty(instance, "Snackbar", Substitute.For<ISnackbar>());
        SetProperty(instance, "TaskService", new DashboardTaskService(null!));

        var method = typeof(DeploymentHistory)
            .GetMethod("OnRowClickAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var args = new TableRowClickEventArgs<TaskItemDto>
        {
            Item = new TaskItemDto { Id = Guid.NewGuid(), Title = "Test Deploy" }
        };
        await (Task)method.Invoke(instance, new object[] { args })!;

        GetField<bool>(instance, "_isDrawerOpen").Should().BeFalse("GetTaskLogsAsync 失敗時 Drawer 不應帶空資料開啟");
    }

    [Fact]
    public async Task OnRowClickAsync_服務拋出例外_不應拋出未處理例外()
    {
        var instance = new DeploymentHistory();
        SetProperty(instance, "Snackbar", Substitute.For<ISnackbar>());
        SetProperty(instance, "TaskService", new DashboardTaskService(null!));

        var method = typeof(DeploymentHistory)
            .GetMethod("OnRowClickAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var args = new TableRowClickEventArgs<TaskItemDto>
        {
            Item = new TaskItemDto { Id = Guid.NewGuid(), Title = "Test Deploy" }
        };
        Func<Task> act = async () => await (Task)method.Invoke(instance, new object[] { args })!;

        await act.Should().NotThrowAsync();
    }
}
