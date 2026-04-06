```csharp
using FluentAssertions;
using Xunit;

namespace AiTeam.Dashboard.Components.Pages.Tasks.Tests;

/// <summary>
/// TaskCenter 的可測試靜態/純邏輯方法測試
/// 注意：Blazor 元件的 protected/private 方法透過反射或繼承進行測試
/// SignalR、MudTable 等 UI 相依性使用整合測試或 bUnit 處理
/// 此處針對可抽離的純邏輯進行單元測試
/// </summary>
public class TaskCenterTests
{
    #region FormatDuration 測試 (透過 FormatElapsed 公開邏輯驗證)

    // 建立一個可測試的子類別以存取 protected/private static 方法
    private class TestableTaskCenter : TaskCenter
    {
        public string PublicFormatElapsed(TimeSpan ts) => FormatElapsed(ts);

        public static string PublicFormatDuration(TimeSpan ts) => FormatDuration(ts);
    }

    private readonly TestableTaskCenter _sut = new();

    #region FormatDuration - 秒數格式

    [Fact]
    public void FormatDuration_時間少於60秒_應回傳秒數格式()
    {
        // Arrange
        var ts = TimeSpan.FromSeconds(45);

        // Act
        var result = TestableTaskCenter.PublicFormatDuration(ts);

        // Assert
        result.Should().Be("45秒");
    }

    [Fact]
    public void FormatDuration_時間為0秒_應回傳0秒格式()
    {
        // Arrange
        var ts = TimeSpan.Zero;

        // Act
        var result = TestableTaskCenter.PublicFormatDuration(ts);

        // Assert
        result.Should().Be("0秒");
    }

    [Fact]
    public void FormatDuration_時間為59秒_應仍回傳秒數格式()
    {
        // Arrange
        var ts = TimeSpan.FromSeconds(59);

        // Act
        var result = TestableTaskCenter.PublicFormatDuration(ts);

        // Assert
        result.Should().Be("59秒");
    }

    #endregion

    #region FormatDuration - 分鐘格式

    [Fact]
    public void FormatDuration_時間為1分鐘_應回傳分秒格式()
    {
        // Arrange
        var ts = TimeSpan.FromMinutes(1);

        // Act
        var result = TestableTaskCenter.PublicFormatDuration(ts);

        // Assert
        result.Should().Be("1分00秒");
    }

    [Fact]
    public void FormatDuration_時間為5分30秒_應回傳分秒格式()
    {
        // Arrange
        var ts = new TimeSpan(0, 5, 30);

        // Act
        var result = TestableTaskCenter.PublicFormatDuration(ts);

        // Assert
        result.Should().Be("5分30秒");
    }

    [Fact]
    public void FormatDuration_時間為59分59秒_應仍回傳分秒格式()
    {
        // Arrange
        var ts = new TimeSpan(0, 59, 59);

        // Act
        var result = TestableTaskCenter.PublicFormatDuration(ts);

        // Assert
        result.Should().Be("59分59秒");
    }

    [Fact]
    public void FormatDuration_時間為10分05秒_秒數應補零格式()
    {
        // Arrange
        var ts = new TimeSpan(0, 10, 5);

        // Act
        var result = TestableTaskCenter.PublicFormatDuration(ts);

        // Assert
        result.Should().Be("10分05秒");
    }

    #endregion

    #region FormatDuration - 小時格式

    [Fact]
    public void FormatDuration_時間為1小時_應回傳小時分鐘格式()
    {
        // Arrange
        var ts = TimeSpan.FromHours(1);

        // Act
        var result = TestableTaskCenter.PublicFormatDuration(ts);

        // Assert
        result.Should().Be("1小時00分");
    }

    [Fact]
    public void FormatDuration_時間為2小時30分_應回傳小時分鐘格式()
    {
        // Arrange
        var ts = new TimeSpan(2, 30, 0);

        // Act
        var result = TestableTaskCenter.PublicFormatDuration(ts);

        // Assert
        result.Should().Be("2小時30分");
    }

    [Fact]
    public void FormatDuration_時間為25小時05分_應回傳正確小時分鐘格式()
    {
        // Arrange
        var ts = new TimeSpan(25, 5, 0);

        // Act
        var result = TestableTaskCenter.PublicFormatDuration(ts);

        // Assert
        result.Should().Be("25小時05分");
    }

    [Fact]
    public void FormatDuration_時間剛好為60分鐘_應回傳小時格式而非分鐘格式()
    {
        // Arrange
        var ts = TimeSpan.FromMinutes(60);

        // Act
        var result = TestableTaskCenter.PublicFormatDuration(ts);

        // Assert
        result.Should().Be("1小時00分");
    }

    #endregion

    #region FormatElapsed - 邊界值處理

    [Fact]
    public void FormatElapsed_傳入正數時間_應正確格式化()
    {
        // Arrange
        var ts = TimeSpan.FromSeconds(30);

        // Act
        var result = _sut.PublicFormatElapsed(ts);

        // Assert
        result.Should().Be("30秒");
    }

    [Fact]
    public void FormatElapsed_傳入負數時間_應視為零秒()
    {
        // Arrange
        var ts = TimeSpan.FromSeconds(-10);

        // Act
        var result = _sut.PublicFormatElapsed(ts);

        // Assert
        result.Should().Be("0秒");
    }

    [Fact]
    public void FormatElapsed_傳入極小負值_應視為零秒()
    {
        // Arrange
        var ts = TimeSpan.FromMilliseconds(-1);

        // Act
        var result = _sut.PublicFormatElapsed(ts);

        // Assert
        result.Should().Be("0秒");
    }

    [Fact]
    public void FormatElapsed_傳入零時間_應回傳0秒()
    {
        // Arrange
        var ts = TimeSpan.Zero;

        // Act
        var result = _sut.PublicFormatElapsed(ts);

        // Assert
        result.Should().Be("0秒");
    }

    [Fact]
    public void FormatElapsed_傳入大負值時間_應視為零秒而非負秒數()
    {
        // Arrange
        var ts = TimeSpan.FromHours(-5);

        // Act
        var result = _sut.PublicFormatElapsed(ts);

        // Assert
        result.Should().Be("0秒");
        result.Should().NotContain("-");
    }

    [Fact]
    public void FormatElapsed_傳入正數小時時間_應正確格式化為小時格式()
    {
        // Arrange
        var ts = new TimeSpan(3, 45, 0);

        // Act
        var result = _sut.PublicFormatElapsed(ts);

        // Assert
        result.Should().Be("3小時45分");
    }

    #endregion

    #region FormatDuration 格式輸出內容驗證

    [Theory]
    [InlineData(0, "0秒")]
    [InlineData(1, "1秒")]
    [InlineData(59, "59秒")]
    public void FormatDuration_不同秒數輸入_應回傳正確秒數格式(int seconds, string expected)
    {
        // Arrange
        var ts = TimeSpan.FromSeconds(seconds);

        // Act
        var result = TestableTaskCenter.PublicFormatDuration(ts);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(1, 0, "1分00秒")]
    [InlineData(1, 1, "1分01秒")]
    [InlineData(30, 30, "30分30秒")]
    [InlineData(59, 59, "59分59秒")]
    public void FormatDuration_不同分秒組合輸入_應回傳正確分秒格式(int minutes, int seconds, string expected)
    {
        // Arrange
        var ts = new TimeSpan(0, minutes, seconds);

        // Act
        var result = TestableTaskCenter.PublicFormatDuration(ts);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(1, 0, "1小時00分")]
    [InlineData(2, 30, "2小時30分")]
    [InlineData(10, 5, "10小時05分")]
    [InlineData(100, 59, "100小時59分")]
    public void FormatDuration_不同小時分鐘組合輸入_應回傳正確小時格式(int hours, int minutes, string expected)
    {
        // Arrange
        var ts = new TimeSpan(hours, minutes, 0);

        // Act
        var result = TestableTaskCenter.PublicFormatDuration(ts);

        // Assert
        result.Should().Be(expected);
    }

    #endregion

    #region DisposeAsync 測試

    [Fact]
    public async Task DisposeAsync_正常呼叫_應不拋出例外()
    {
        // Arrange
        var sut = new TestableTaskCenter();

        // Act
        Func<Task> act = async () => await sut.DisposeAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DisposeAsync_多次呼叫_應不拋出例外()
    {
        // Arrange
        var sut = new TestableTaskCenter();

        // Act
        Func<Task> act = async () =>
        {
            await sut.DisposeAsync();
            await sut.DisposeAsync();
        };

        // Assert
        await act.Should().NotThrowAsync();
    }

    #endregion

    #region 格式化輸出不含非預期字元

    [Fact]
    public void FormatDuration_秒數格式_不應包含分或小時字元()
    {
        // Arrange
        var ts = TimeSpan.FromSeconds(30);

        // Act
        var result = TestableTaskCenter.PublicFormatDuration(ts);

        // Assert
        result.Should().NotContain("分");
        result.Should().NotContain("小時");
        result.Should().Contain("秒");
    }

    [Fact]
    public void FormatDuration_分鐘格式_不應包含小時字元()
    {
        // Arrange
        var ts = TimeSpan.FromMinutes(30);

        // Act
        var result = TestableTaskCenter.PublicFormatDuration(ts);

        // Assert
        result.Should().NotContain("小時");
        result.Should().Contain("分");
        result.Should().Contain("秒");
    }

    [Fact]
    public void FormatDuration_小時格式_不應包含秒字元()
    {
        // Arrange
        var ts = TimeSpan.FromHours(2);

        // Act
        var result = TestableTaskCenter.PublicFormatDuration(ts);

        // Assert
        result.Should().NotContain("秒");
        result.Should().Contain("小時");
        result.Should().Contain("分");
    }

    #endregion
}
```