using System;
using System.Reflection;
using AiTeam.Dashboard.Components.Pages.Tasks;
using AiTeam.Dashboard.Helpers;
using AiTeam.Dashboard.Services;
using AiTeam.Shared.Dtos;
using FluentAssertions;
using MudBlazor;
using NSubstitute;
using Xunit;

namespace AiTeam.Dashboard.Components.Pages.Tasks.Tests;

public class PipelineViewTests
{
    // -----------------------------------------------------------------------
    // 輔助：呼叫私有靜態方法
    // -----------------------------------------------------------------------

    private static string InvokeExtractPrNumber(string? url)
        => PrNumberHelper.ExtractPrNumber(url);

    private static string InvokeFormatDuration(TaskItemDto task)
    {
        var method = typeof(PipelineView).GetMethod(
            "FormatDuration",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        return (string)method.Invoke(null, new object?[] { task })!;
    }

    private static bool InvokeIsCompleted(string status)
    {
        var method = typeof(PipelineView).GetMethod(
            "IsCompleted",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        return (bool)method.Invoke(null, new object?[] { status })!;
    }

    private static bool InvokeIsFailed(string status)
    {
        var method = typeof(PipelineView).GetMethod(
            "IsFailed",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        return (bool)method.Invoke(null, new object?[] { status })!;
    }

    private static bool InvokeIsRevision(string status)
    {
        var method = typeof(PipelineView).GetMethod(
            "IsRevision",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        return (bool)method.Invoke(null, new object?[] { status })!;
    }

    private static Color InvokeGetLogColor(string status)
    {
        var method = typeof(PipelineView).GetMethod(
            "GetLogColor",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        return (Color)method.Invoke(null, new object?[] { status })!;
    }

    // -----------------------------------------------------------------------
    // ExtractPrNumber — 正常 URL
    // -----------------------------------------------------------------------

    [Fact]
    public void ExtractPrNumber_標準GitHub_PR_URL_應回傳井號加數字()
    {
        var result = InvokeExtractPrNumber("https://github.com/org/repo/pull/109");

        result.Should().Be("#109");
    }

    [Fact]
    public void ExtractPrNumber_URL結尾含斜線_應正確解析PR編號()
    {
        var result = InvokeExtractPrNumber("https://github.com/org/repo/pull/55/");

        result.Should().Be("#55");
    }

    // -----------------------------------------------------------------------
    // ExtractPrNumber — 邊界
    // -----------------------------------------------------------------------

    [Fact]
    public void ExtractPrNumber_Null_應回傳PR文字()
    {
        var result = InvokeExtractPrNumber(null);

        result.Should().Be("PR");
    }

    [Fact]
    public void ExtractPrNumber_空字串_應回傳PR文字()
    {
        var result = InvokeExtractPrNumber("");

        result.Should().Be("PR");
    }

    [Fact]
    public void ExtractPrNumber_URL結尾為非數字_應回傳PR文字()
    {
        var result = InvokeExtractPrNumber("https://github.com/org/repo/compare/main...feature");

        result.Should().Be("PR");
    }

    // -----------------------------------------------------------------------
    // FormatDuration — running 狀態
    // -----------------------------------------------------------------------

    [Fact]
    public void FormatDuration_狀態為running_應回傳進行中格式()
    {
        var task = new TaskItemDto
        {
            Status    = "running",
            CreatedAt = DateTime.UtcNow.AddMinutes(-5)
        };

        var result = InvokeFormatDuration(task);

        result.Should().StartWith("進行中（");
        result.Should().EndWith(" 分）");
    }

    [Fact]
    public void FormatDuration_狀態為running且CreatedAt為現在_應回傳零分()
    {
        var task = new TaskItemDto
        {
            Status    = "running",
            CreatedAt = DateTime.UtcNow
        };

        var result = InvokeFormatDuration(task);

        result.Should().StartWith("進行中（");
    }

    // -----------------------------------------------------------------------
    // FormatDuration — Duration 為 null
    // -----------------------------------------------------------------------

    [Fact]
    public void FormatDuration_Duration為Null_應回傳空字串()
    {
        var task = new TaskItemDto
        {
            Status      = "pending",
            CompletedAt = null
        };

        var result = InvokeFormatDuration(task);

        result.Should().Be("");
    }

    // -----------------------------------------------------------------------
    // FormatDuration — 各時間範圍
    // -----------------------------------------------------------------------

    [Fact]
    public void FormatDuration_Duration小於60秒_應回傳秒格式()
    {
        var now  = DateTime.UtcNow;
        var task = new TaskItemDto
        {
            Status      = "done",
            CreatedAt   = now.AddSeconds(-45),
            CompletedAt = now
        };

        var result = InvokeFormatDuration(task);

        result.Should().Be("45 秒");
    }

    [Fact]
    public void FormatDuration_Duration為分鐘含秒_應回傳分秒格式()
    {
        var now  = DateTime.UtcNow;
        var task = new TaskItemDto
        {
            Status      = "done",
            CreatedAt   = now.AddSeconds(-(3 * 60 + 30)),
            CompletedAt = now
        };

        var result = InvokeFormatDuration(task);

        result.Should().Be("3 分 30 秒");
    }

    [Fact]
    public void FormatDuration_Duration為整數分鐘_應回傳純分格式()
    {
        var now  = DateTime.UtcNow;
        var task = new TaskItemDto
        {
            Status      = "done",
            CreatedAt   = now.AddMinutes(-10),
            CompletedAt = now
        };

        var result = InvokeFormatDuration(task);

        result.Should().Be("10 分");
    }

    [Fact]
    public void FormatDuration_Duration超過1小時含分鐘_應回傳時分格式()
    {
        var now  = DateTime.UtcNow;
        var task = new TaskItemDto
        {
            Status      = "done",
            CreatedAt   = now.AddHours(-1).AddMinutes(-15),
            CompletedAt = now
        };

        var result = InvokeFormatDuration(task);

        result.Should().Be("1 時 15 分");
    }

    [Fact]
    public void FormatDuration_Duration為整數小時_應回傳純時格式()
    {
        var now  = DateTime.UtcNow;
        var task = new TaskItemDto
        {
            Status      = "done",
            CreatedAt   = now.AddHours(-2),
            CompletedAt = now
        };

        var result = InvokeFormatDuration(task);

        result.Should().Be("2 時");
    }

    // -----------------------------------------------------------------------
    // IsCompleted
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("done")]
    [InlineData("skipped")]
    public void IsCompleted_完成類狀態_應回傳True(string status)
    {
        var result = InvokeIsCompleted(status);

        result.Should().BeTrue(because: $"狀態 '{status}' 視為已完成");
    }

    [Theory]
    [InlineData("running")]
    [InlineData("failed")]
    [InlineData("pending")]
    [InlineData("revision")]
    public void IsCompleted_非完成類狀態_應回傳False(string status)
    {
        var result = InvokeIsCompleted(status);

        result.Should().BeFalse(because: $"狀態 '{status}' 不視為已完成");
    }

    // -----------------------------------------------------------------------
    // IsFailed
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("failed")]
    [InlineData("error")]
    public void IsFailed_失敗類狀態_應回傳True(string status)
    {
        var result = InvokeIsFailed(status);

        result.Should().BeTrue(because: $"狀態 '{status}' 視為失敗");
    }

    [Theory]
    [InlineData("done")]
    [InlineData("running")]
    [InlineData("cancelled")]
    public void IsFailed_非失敗類狀態_應回傳False(string status)
    {
        var result = InvokeIsFailed(status);

        result.Should().BeFalse(because: $"狀態 '{status}' 不視為失敗");
    }

    // -----------------------------------------------------------------------
    // IsRevision
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("revision")]
    [InlineData("reviewing")]
    public void IsRevision_修正類狀態_應回傳True(string status)
    {
        var result = InvokeIsRevision(status);

        result.Should().BeTrue(because: $"狀態 '{status}' 視為修正中");
    }

    [Theory]
    [InlineData("done")]
    [InlineData("running")]
    [InlineData("failed")]
    public void IsRevision_非修正類狀態_應回傳False(string status)
    {
        var result = InvokeIsRevision(status);

        result.Should().BeFalse(because: $"狀態 '{status}' 不視為修正中");
    }

    // -----------------------------------------------------------------------
    // GetLogColor
    // -----------------------------------------------------------------------

    [Fact]
    public void GetLogColor_done狀態_應回傳Success顏色()
    {
        InvokeGetLogColor("done").Should().Be(Color.Success);
    }

    [Theory]
    [InlineData("failed")]
    [InlineData("error")]
    public void GetLogColor_失敗類狀態_應回傳Error顏色(string status)
    {
        InvokeGetLogColor(status).Should().Be(Color.Error);
    }

    [Fact]
    public void GetLogColor_running狀態_應回傳Info顏色()
    {
        InvokeGetLogColor("running").Should().Be(Color.Info);
    }

    [Theory]
    [InlineData("revision")]
    [InlineData("reviewing")]
    public void GetLogColor_修正類狀態_應回傳Warning顏色(string status)
    {
        InvokeGetLogColor(status).Should().Be(Color.Warning);
    }

    [Fact]
    public void GetLogColor_skipped狀態_應回傳Tertiary顏色()
    {
        InvokeGetLogColor("skipped").Should().Be(Color.Tertiary);
    }

    [Fact]
    public void GetLogColor_未知狀態_應回傳Default顏色()
    {
        InvokeGetLogColor("unknown").Should().Be(Color.Default);
    }

    // -----------------------------------------------------------------------
    // IsRevision — needs_intervention（Stage 43 補充）
    // -----------------------------------------------------------------------

    [Fact]
    public void IsRevision_needs_intervention狀態_應回傳True()
    {
        InvokeIsRevision("needs_intervention").Should().BeTrue(
            because: "needs_intervention 屬於需要介入的修正類狀態");
    }

    // -----------------------------------------------------------------------
    // GetLogColor — needs_intervention（Stage 43 補充）
    // -----------------------------------------------------------------------

    [Fact]
    public void GetLogColor_needs_intervention狀態_應回傳Warning顏色()
    {
        InvokeGetLogColor("needs_intervention").Should().Be(Color.Warning);
    }

    // -----------------------------------------------------------------------
    // PipelineStepViewModel — 預設值
    // -----------------------------------------------------------------------

    [Fact]
    public void PipelineStepViewModel_預設值_LogsLoaded應為False()
    {
        var vm = new PipelineStepViewModel();

        vm.LogsLoaded.Should().BeFalse();
    }

    [Fact]
    public void PipelineStepViewModel_預設值_Logs應為空列表且不為Null()
    {
        var vm = new PipelineStepViewModel();

        vm.Logs.Should().NotBeNull();
        vm.Logs.Should().BeEmpty();
    }

    // -----------------------------------------------------------------------
    // LoadStepsAsync — 服務失敗（catch 路徑）
    // -----------------------------------------------------------------------

    [Fact]
    public async Task LoadStepsAsync_服務拋出例外_loading應重設為False()
    {
        var instance = new PipelineView();
        instance.Group = new TaskGroupDto { Id = Guid.NewGuid() };
        typeof(PipelineView)
            .GetProperty("Snackbar", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(instance, Substitute.For<ISnackbar>());
        typeof(PipelineView)
            .GetProperty("TaskService", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(instance, new DashboardTaskService(null!));

        var method = typeof(PipelineView).GetMethod(
            "LoadStepsAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        await (Task)method.Invoke(instance, null)!;

        var loading = (bool)typeof(PipelineView)
            .GetField("_loading", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(instance)!;
        loading.Should().BeFalse("finally 區塊應確保 _loading 重設為 false");
    }

    [Fact]
    public async Task LoadStepsAsync_服務拋出例外_不應拋出未處理例外()
    {
        var instance = new PipelineView();
        instance.Group = new TaskGroupDto { Id = Guid.NewGuid() };
        typeof(PipelineView)
            .GetProperty("Snackbar", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(instance, Substitute.For<ISnackbar>());
        typeof(PipelineView)
            .GetProperty("TaskService", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(instance, new DashboardTaskService(null!));

        var method = typeof(PipelineView).GetMethod(
            "LoadStepsAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        Func<Task> act = async () => await (Task)method.Invoke(instance, null)!;

        await act.Should().NotThrowAsync();
    }
}
