// 測試標的：AiTeam.Dashboard.Components.Pages.Projects.ProjectManagement
// 驗證：grep -r 'partial class ProjectManagement' src/AiTeam.Dashboard/ → 命中 ProjectManagement.razor.cs:5

using System.Reflection;
using AiTeam.Dashboard.Components.Pages.Projects;
using AiTeam.Dashboard.Services;
using AiTeam.Shared.Dtos;
using FluentAssertions;
using MudBlazor;
using NSubstitute;
using Xunit;

namespace AiTeam.Dashboard.Components.Pages.Projects.Tests;

public class ProjectManagementTests
{
    private static void SetProperty(ProjectManagement instance, string propertyName, object? value)
        => typeof(ProjectManagement)
            .GetProperty(propertyName, BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(instance, value);

    private static T? GetField<T>(ProjectManagement instance, string fieldName)
        => (T?)typeof(ProjectManagement)
            .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(instance);

    // ── OnInitializedAsync：載入失敗（catch 路徑）────────────────────────────

    [Fact]
    public async Task OnInitializedAsync_服務拋出例外_應設置_loadError欄位()
    {
        var instance = new ProjectManagement();
        SetProperty(instance, "Snackbar", Substitute.For<ISnackbar>());
        SetProperty(instance, "ProjectService", new DashboardProjectService(null!));

        var method = typeof(ProjectManagement)
            .GetMethod("OnInitializedAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        await (Task)method.Invoke(instance, null)!;

        GetField<string?>(instance, "_loadError").Should().StartWith("專案清單載入失敗：");
    }

    [Fact]
    public async Task OnInitializedAsync_服務拋出例外_不應拋出未處理例外()
    {
        var instance = new ProjectManagement();
        SetProperty(instance, "Snackbar", Substitute.For<ISnackbar>());
        SetProperty(instance, "ProjectService", new DashboardProjectService(null!));

        var method = typeof(ProjectManagement)
            .GetMethod("OnInitializedAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        Func<Task> act = async () => await (Task)method.Invoke(instance, null)!;

        await act.Should().NotThrowAsync();
    }

    // ── ToggleIsActiveAsync：切換失敗（catch 路徑）──────────────────────────

    [Fact]
    public async Task ToggleIsActiveAsync_服務拋出例外_IsActive不應被更新()
    {
        var instance = new ProjectManagement();
        SetProperty(instance, "Snackbar", Substitute.For<ISnackbar>());
        SetProperty(instance, "ProjectService", new DashboardProjectService(null!));

        var project = new ProjectDto { Id = Guid.NewGuid(), IsActive = false };

        var method = typeof(ProjectManagement)
            .GetMethod("ToggleIsActiveAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        await (Task)method.Invoke(instance, new object[] { project, true })!;

        project.IsActive.Should().BeFalse("服務失敗時不應更新 IsActive");
    }

    [Fact]
    public async Task ToggleIsActiveAsync_服務拋出例外_不應拋出未處理例外()
    {
        var instance = new ProjectManagement();
        SetProperty(instance, "Snackbar", Substitute.For<ISnackbar>());
        SetProperty(instance, "ProjectService", new DashboardProjectService(null!));

        var project = new ProjectDto { Id = Guid.NewGuid(), IsActive = true };

        var method = typeof(ProjectManagement)
            .GetMethod("ToggleIsActiveAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        Func<Task> act = async () => await (Task)method.Invoke(instance, new object[] { project, false })!;

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ToggleIsActiveAsync_服務拋出例外_應呼叫ErrorSnackbar()
    {
        var snackbar = Substitute.For<ISnackbar>();
        var instance = new ProjectManagement();
        SetProperty(instance, "Snackbar", snackbar);
        SetProperty(instance, "ProjectService", new DashboardProjectService(null!));

        var project = new ProjectDto { Id = Guid.NewGuid(), IsActive = true };

        var method = typeof(ProjectManagement)
            .GetMethod("ToggleIsActiveAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        await (Task)method.Invoke(instance, new object[] { project, false })!;

        snackbar.Received(1).Add(Arg.Any<string>(), Severity.Error, Arg.Any<Action<SnackbarOptions>?>());
    }
}
