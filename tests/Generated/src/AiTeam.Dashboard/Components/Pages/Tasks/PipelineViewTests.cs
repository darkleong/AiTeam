using System;
using System.Reflection;
using AiTeam.Dashboard.Components.Pages.Tasks;
using AiTeam.Dashboard.Helpers;
using AiTeam.Dashboard.Services;
using AiTeam.Shared.Dtos;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
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
    // 輔助：建立可觸發 ok=false 路徑的服務實例
    // httpClientFactory=null 時，服務方法內部 try-catch 捕獲 NullReferenceException 並回傳 false
    // -----------------------------------------------------------------------

    private static DashboardBotService CreateFaultyBotService()
    {
        var mockConfig = Substitute.For<IConfiguration>();
        return new DashboardBotService(null!, mockConfig, Substitute.For<ILogger<DashboardBotService>>());
    }

    private static DashboardCeoCommandService CreateFaultyCeoService()
    {
        var mockConfig = Substitute.For<IConfiguration>();
        return new DashboardCeoCommandService(null!, mockConfig, Substitute.For<ILogger<DashboardCeoCommandService>>());
    }

    private static PipelineView CreateViewWithBotService(TaskGroupDto? group, DashboardBotService botService)
    {
        var instance = new PipelineView();
        instance.Group = group;
        typeof(PipelineView)
            .GetProperty("Snackbar", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(instance, Substitute.For<ISnackbar>());
        typeof(PipelineView)
            .GetProperty("BotService", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(instance, botService);
        return instance;
    }

    private static PipelineView CreateViewWithCeoService(TaskGroupDto? group, DashboardCeoCommandService ceoService)
    {
        var instance = new PipelineView();
        instance.Group = group;
        typeof(PipelineView)
            .GetProperty("Snackbar", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(instance, Substitute.For<ISnackbar>());
        typeof(PipelineView)
            .GetProperty("CeoCommandService", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(instance, ceoService);
        return instance;
    }

    private static string? GetActionError(PipelineView instance)
        => (string?)typeof(PipelineView)
            .GetField("_actionError", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(instance);

    private static bool GetPauseBusy(PipelineView instance)
        => (bool)typeof(PipelineView)
            .GetField("_pauseBusy", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(instance)!;

    private static bool GetMidInterruptBusy(PipelineView instance)
        => (bool)typeof(PipelineView)
            .GetField("_midInterruptBusy", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(instance)!;

    private static async Task InvokePrivateAsync(PipelineView instance, string methodName)
    {
        var method = typeof(PipelineView).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance)!;
        await (Task)method.Invoke(instance, null)!;
    }

    // -----------------------------------------------------------------------
    // HandlePauseClickAsync — 暫停 TaskGroup（Stage 45 雙路錯誤通知新增路徑）
    // 測試標的：AiTeam.Dashboard.Components.Pages.Tasks.PipelineView
    // 驗證：grep -rn 'HandlePauseClickAsync' src/AiTeam.Dashboard/ → 命中 PipelineView.razor.cs:219
    // -----------------------------------------------------------------------

    [Fact]
    public async Task HandlePauseClickAsync_Group為Null_應立即返回不改變狀態()
    {
        var instance = new PipelineView();
        // Group 預設為 null，早期返回守衛生效

        await InvokePrivateAsync(instance, "HandlePauseClickAsync");

        GetPauseBusy(instance).Should().BeFalse("Guard: Group==null 時不應進入 busy 狀態");
        GetActionError(instance).Should().BeNull("Guard: Group==null 時不應設定錯誤訊息");
    }

    [Fact]
    public async Task HandlePauseClickAsync_PauseBusy已為True_應立即返回不再次觸發服務()
    {
        var instance = CreateViewWithBotService(new TaskGroupDto { Id = Guid.NewGuid() }, CreateFaultyBotService());
        typeof(PipelineView)
            .GetField("_pauseBusy", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(instance, true);

        await InvokePrivateAsync(instance, "HandlePauseClickAsync");

        GetPauseBusy(instance).Should().BeTrue("Guard: 已 busy 時不應再次修改狀態");
    }

    [Fact]
    public async Task HandlePauseClickAsync_BotService返回False_actionError應被設定且pauseBusy應重設為False()
    {
        // 測試標的：HandlePauseClickAsync else 分支（_actionError + Snackbar 雙路通知，PR 新增）
        var instance = CreateViewWithBotService(
            new TaskGroupDto { Id = Guid.NewGuid() },
            CreateFaultyBotService());

        await InvokePrivateAsync(instance, "HandlePauseClickAsync");

        GetActionError(instance).Should().Be("暫停指令送出失敗", "BotService 返回 false 時應設定 _actionError");
        GetPauseBusy(instance).Should().BeFalse("finally 區塊應重設 _pauseBusy 為 false");
    }

    // -----------------------------------------------------------------------
    // HandleResumeClickAsync — 恢復 TaskGroup（Stage 45 雙路錯誤通知新增路徑）
    // -----------------------------------------------------------------------

    [Fact]
    public async Task HandleResumeClickAsync_Group為Null_應立即返回不改變狀態()
    {
        var instance = new PipelineView();

        await InvokePrivateAsync(instance, "HandleResumeClickAsync");

        GetPauseBusy(instance).Should().BeFalse();
        GetActionError(instance).Should().BeNull();
    }

    [Fact]
    public async Task HandleResumeClickAsync_BotService返回False_actionError應被設定且pauseBusy應重設為False()
    {
        var instance = CreateViewWithBotService(
            new TaskGroupDto { Id = Guid.NewGuid() },
            CreateFaultyBotService());

        await InvokePrivateAsync(instance, "HandleResumeClickAsync");

        GetActionError(instance).Should().Be("恢復指令送出失敗", "BotService 返回 false 時應設定 _actionError");
        GetPauseBusy(instance).Should().BeFalse("finally 區塊應重設 _pauseBusy 為 false");
    }

    // -----------------------------------------------------------------------
    // HandlePauseEpicClickAsync — 暫停 Epic（Stage 61-FF 四十 雙路錯誤通知新增路徑）
    // -----------------------------------------------------------------------

    [Fact]
    public async Task HandlePauseEpicClickAsync_Group為Null_應立即返回不改變狀態()
    {
        var instance = new PipelineView();

        await InvokePrivateAsync(instance, "HandlePauseEpicClickAsync");

        GetPauseBusy(instance).Should().BeFalse();
        GetActionError(instance).Should().BeNull();
    }

    [Fact]
    public async Task HandlePauseEpicClickAsync_BotService返回False_actionError應被設定且pauseBusy應重設為False()
    {
        var instance = CreateViewWithBotService(
            new TaskGroupDto { Id = Guid.NewGuid() },
            CreateFaultyBotService());

        await InvokePrivateAsync(instance, "HandlePauseEpicClickAsync");

        GetActionError(instance).Should().Be("暫停 Epic 失敗", "BotService 返回 false 時應設定 _actionError");
        GetPauseBusy(instance).Should().BeFalse("finally 區塊應重設 _pauseBusy 為 false");
    }

    // -----------------------------------------------------------------------
    // HandleResumeEpicClickAsync — 恢復 Epic（Stage 61-FF 四十 雙路錯誤通知新增路徑）
    // -----------------------------------------------------------------------

    [Fact]
    public async Task HandleResumeEpicClickAsync_Group為Null_應立即返回不改變狀態()
    {
        var instance = new PipelineView();

        await InvokePrivateAsync(instance, "HandleResumeEpicClickAsync");

        GetPauseBusy(instance).Should().BeFalse();
        GetActionError(instance).Should().BeNull();
    }

    [Fact]
    public async Task HandleResumeEpicClickAsync_BotService返回False_actionError應被設定且pauseBusy應重設為False()
    {
        var instance = CreateViewWithBotService(
            new TaskGroupDto { Id = Guid.NewGuid() },
            CreateFaultyBotService());

        await InvokePrivateAsync(instance, "HandleResumeEpicClickAsync");

        GetActionError(instance).Should().Be("恢復 Epic 失敗", "BotService 返回 false 時應設定 _actionError");
        GetPauseBusy(instance).Should().BeFalse("finally 區塊應重設 _pauseBusy 為 false");
    }

    // -----------------------------------------------------------------------
    // HandleMidInterruptClickAsync — 中途介入（Stage 51 雙路錯誤通知新增路徑）
    // -----------------------------------------------------------------------

    [Fact]
    public async Task HandleMidInterruptClickAsync_Group為Null_應立即返回不改變狀態()
    {
        var instance = new PipelineView();

        await InvokePrivateAsync(instance, "HandleMidInterruptClickAsync");

        GetMidInterruptBusy(instance).Should().BeFalse();
        GetActionError(instance).Should().BeNull();
    }

    [Fact]
    public async Task HandleMidInterruptClickAsync_CeoService返回Failure_actionError應包含錯誤訊息且midInterruptBusy應重設為False()
    {
        // CeoCommandService 有自己的 try-catch，null httpClientFactory 捕獲後回傳 (false, "連線失敗...")
        var instance = CreateViewWithCeoService(
            new TaskGroupDto { Id = Guid.NewGuid() },
            CreateFaultyCeoService());

        await InvokePrivateAsync(instance, "HandleMidInterruptClickAsync");

        GetActionError(instance).Should().StartWith("中途介入觸發失敗：",
            "CeoService 回傳 failure 時應設定包含錯誤描述的 _actionError");
        GetMidInterruptBusy(instance).Should().BeFalse("finally 區塊應重設 _midInterruptBusy 為 false");
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
