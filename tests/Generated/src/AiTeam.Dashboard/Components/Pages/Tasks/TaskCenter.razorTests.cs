using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using AiTeam.Dashboard.Components.Pages.Tasks;
using FluentAssertions;
using MudBlazor;
using Xunit;

namespace AiTeam.Dashboard.Tests.Components.Pages.Tasks;

/// <summary>
/// 針對 TaskCenter 中可測試的靜態與純邏輯方法進行單元測試。
/// SignalR / Blazor 相關的整合行為（OnInitializedAsync、ConnectSignalRAsync 等）
/// 因需要完整 Blazor 執行環境，故以整合測試處理，此處不涵蓋。
/// </summary>
public class TaskCenterTests
{
    // -----------------------------------------------------------------------
    // 取得私有靜態方法的輔助
    // -----------------------------------------------------------------------
    private static string InvokeFormatDuration(TimeSpan? duration)
    {
        var method = typeof(TaskCenter).GetMethod(
            "FormatDuration",
            BindingFlags.NonPublic | BindingFlags.Static)!;

        return (string)method.Invoke(null, new object?[] { duration })!;
    }

    // -----------------------------------------------------------------------
    // FormatDuration — null
    // -----------------------------------------------------------------------

    [Fact]
    public void FormatDuration_傳入Null_應回傳破折號()
    {
        // Arrange
        TimeSpan? duration = null;

        // Act
        var result = InvokeFormatDuration(duration);

        // Assert
        result.Should().Be("—");
    }

    // -----------------------------------------------------------------------
    // FormatDuration — 秒數 < 60
    // -----------------------------------------------------------------------

    [Fact]
    public void FormatDuration_傳入小於60秒的時間_應回傳秒格式()
    {
        // Arrange
        TimeSpan? duration = TimeSpan.FromSeconds(45);

        // Act
        var result = InvokeFormatDuration(duration);

        // Assert
        result.Should().Be("45 秒");
    }

    [Fact]
    public void FormatDuration_傳入零秒_應回傳零秒格式()
    {
        // Arrange
        TimeSpan? duration = TimeSpan.Zero;

        // Act
        var result = InvokeFormatDuration(duration);

        // Assert
        result.Should().Be("0 秒");
    }

    [Fact]
    public void FormatDuration_傳入恰好59秒_應回傳秒格式()
    {
        // Arrange
        TimeSpan? duration = TimeSpan.FromSeconds(59);

        // Act
        var result = InvokeFormatDuration(duration);

        // Assert
        result.Should().Be("59 秒");
    }

    // -----------------------------------------------------------------------
    // FormatDuration — 分鐘 (>= 60 秒，< 60 分)
    // -----------------------------------------------------------------------

    [Fact]
    public void FormatDuration_傳入分鐘含秒數_應回傳分秒格式()
    {
        // Arrange
        TimeSpan? duration = TimeSpan.FromSeconds(3 * 60 + 42); // 3 分 42 秒

        // Act
        var result = InvokeFormatDuration(duration);

        // Assert
        result.Should().Be("3 分 42 秒");
    }

    [Fact]
    public void FormatDuration_傳入整數分鐘無秒數_應回傳純分格式()
    {
        // Arrange
        TimeSpan? duration = TimeSpan.FromMinutes(10); // 10 分 0 秒

        // Act
        var result = InvokeFormatDuration(duration);

        // Assert
        result.Should().Be("10 分");
    }

    [Fact]
    public void FormatDuration_傳入恰好1分鐘_應回傳純分格式()
    {
        // Arrange
        TimeSpan? duration = TimeSpan.FromMinutes(1);

        // Act
        var result = InvokeFormatDuration(duration);

        // Assert
        result.Should().Be("1 分");
    }

    [Fact]
    public void FormatDuration_傳入59分59秒_應回傳分秒格式()
    {
        // Arrange
        TimeSpan? duration = new TimeSpan(0, 59, 59);

        // Act
        var result = InvokeFormatDuration(duration);

        // Assert
        result.Should().Be("59 分 59 秒");
    }

    // -----------------------------------------------------------------------
    // FormatDuration — 小時 (>= 60 分)
    // -----------------------------------------------------------------------

    [Fact]
    public void FormatDuration_傳入小時含分鐘_應回傳時分格式()
    {
        // Arrange
        TimeSpan? duration = new TimeSpan(1, 5, 0); // 1 時 5 分

        // Act
        var result = InvokeFormatDuration(duration);

        // Assert
        result.Should().Be("1 時 5 分");
    }

    [Fact]
    public void FormatDuration_傳入整數小時無分鐘_應回傳純時格式()
    {
        // Arrange
        TimeSpan? duration = TimeSpan.FromHours(2); // 2 時 0 分

        // Act
        var result = InvokeFormatDuration(duration);

        // Assert
        result.Should().Be("2 時");
    }

    [Fact]
    public void FormatDuration_傳入超過24小時_應回傳時格式()
    {
        // Arrange
        TimeSpan? duration = new TimeSpan(25, 30, 0); // 25 時 30 分

        // Act
        var result = InvokeFormatDuration(duration);

        // Assert
        result.Should().Be("25 時 30 分");
    }

    [Fact]
    public void FormatDuration_傳入恰好1小時_應回傳純時格式()
    {
        // Arrange
        TimeSpan? duration = TimeSpan.FromHours(1);

        // Act
        var result = InvokeFormatDuration(duration);

        // Assert
        result.Should().Be("1 時");
    }

    [Fact]
    public void FormatDuration_傳入小時含秒數但無分鐘_分鐘為零應回傳純時格式()
    {
        // Arrange
        // 1 小時 0 分 30 秒 → TotalMinutes >= 60，Minutes == 0 → 應顯示「1 時」
        TimeSpan? duration = new TimeSpan(1, 0, 30);

        // Act
        var result = InvokeFormatDuration(duration);

        // Assert
        // ts.Minutes == 0，故走純時分支
        result.Should().Be("1 時");
    }

    // -----------------------------------------------------------------------
    // FormatDuration — 邊界值：恰好 60 秒（= 1 分）
    // -----------------------------------------------------------------------

    [Fact]
    public void FormatDuration_傳入恰好60秒_應回傳純分格式而非秒格式()
    {
        // Arrange
        TimeSpan? duration = TimeSpan.FromSeconds(60);

        // Act
        var result = InvokeFormatDuration(duration);

        // Assert
        result.Should().Be("1 分");
    }

    // -----------------------------------------------------------------------
    // FormatDuration — 邊界值：恰好 60 分（= 1 時）
    // -----------------------------------------------------------------------

    [Fact]
    public void FormatDuration_傳入恰好60分鐘_應回傳純時格式而非分格式()
    {
        // Arrange
        TimeSpan? duration = TimeSpan.FromMinutes(60);

        // Act
        var result = InvokeFormatDuration(duration);

        // Assert
        result.Should().Be("1 時");
    }

    // -----------------------------------------------------------------------
    // FormatDuration — 回傳值型別驗證
    // -----------------------------------------------------------------------

    [Fact]
    public void FormatDuration_任意輸入_回傳值不應為Null()
    {
        // Arrange
        var durations = new TimeSpan?[]
        {
            null,
            TimeSpan.Zero,
            TimeSpan.FromSeconds(30),
            TimeSpan.FromMinutes(5),
            TimeSpan.FromHours(3),
        };

        foreach (var duration in durations)
        {
            // Act
            var result = InvokeFormatDuration(duration);

            // Assert
            result.Should().NotBeNull(because: $"duration={duration} 不應回傳 null");
        }
    }

    [Fact]
    public void FormatDuration_任意輸入_回傳值不應為空字串()
    {
        // Arrange
        var durations = new TimeSpan?[]
        {
            null,
            TimeSpan.Zero,
            TimeSpan.FromSeconds(1),
            TimeSpan.FromMinutes(1),
            TimeSpan.FromHours(1),
        };

        foreach (var duration in durations)
        {
            // Act
            var result = InvokeFormatDuration(duration);

            // Assert
            result.Should().NotBeEmpty(because: $"duration={duration} 不應回傳空字串");
        }
    }

    // -----------------------------------------------------------------------
    // TriggeredByColor
    // -----------------------------------------------------------------------

    private static Color InvokeTriggeredByColor(string? triggeredBy)
    {
        var method = typeof(TaskCenter).GetMethod(
            "TriggeredByColor",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        return (Color)method.Invoke(null, new object?[] { triggeredBy })!;
    }

    [Fact]
    public void TriggeredByColor_Discord_應回傳Secondary色()
    {
        InvokeTriggeredByColor("Discord").Should().Be(Color.Secondary);
    }

    [Fact]
    public void TriggeredByColor_Null_應回傳Default色()
    {
        InvokeTriggeredByColor(null).Should().Be(Color.Default);
    }

    [Fact]
    public void TriggeredByColor_未知字串_應回傳Default色()
    {
        InvokeTriggeredByColor("UnknownSource").Should().Be(Color.Default);
    }
}