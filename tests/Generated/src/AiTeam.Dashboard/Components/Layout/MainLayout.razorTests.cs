using System.Reflection;
using FluentAssertions;
using Xunit;

namespace AiTeam.Dashboard.Components.Layout.Tests;

public class MainLayoutTests
{
    private class TestableMainLayout : MainLayout
    {
        public string GetAppVersion() => AppVersion;
    }

    [Fact]
    public void AppVersion_當組件版本資訊存在時_應回傳帶有v前綴的版本字串()
    {
        // Arrange
        var layout = new TestableMainLayout();

        // Act
        var result = layout.GetAppVersion();

        // Assert
        // 由於執行中的組件可能有或沒有版本資訊，我們驗證格式正確
        if (!string.IsNullOrEmpty(result))
        {
            result.Should().StartWith("v");
        }
        else
        {
            result.Should().BeEmpty();
        }
    }

    [Fact]
    public void AppVersion_當多次呼叫時_應回傳相同結果()
    {
        // Arrange
        var layout = new TestableMainLayout();

        // Act
        var firstCall = layout.GetAppVersion();
        var secondCall = layout.GetAppVersion();

        // Assert
        firstCall.Should().Be(secondCall);
    }

    [Fact]
    public void AppVersion_回傳值不應為Null()
    {
        // Arrange
        var layout = new TestableMainLayout();

        // Act
        var result = layout.GetAppVersion();

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public void AppVersion_當版本資訊格式正確時_應只有一個v前綴()
    {
        // Arrange
        var layout = new TestableMainLayout();

        // Act
        var result = layout.GetAppVersion();

        // Assert
        if (!string.IsNullOrEmpty(result))
        {
            result.Should().StartWith("v");
            result.Should().NotStartWith("vv");
        }
    }

    [Fact]
    public void AppVersion_版本字串格式_應符合預期模式()
    {
        // Arrange
        var layout = new TestableMainLayout();

        // Act
        var result = layout.GetAppVersion();

        // Assert
        result.Should().NotBeNull();
        if (!string.IsNullOrEmpty(result))
        {
            result.Length.Should().BeGreaterThan(1,
                because: "版本字串若存在，應至少包含 'v' 前綴及版本號");
        }
    }

    // Stage 41 移除：以下兩個測試由 Quinn 生成時假設錯誤
    //   ① 用 Assembly.GetExecutingAssembly() 取得測試 assembly 版本
    //      （= AiTeam.Tests.Generated 的 1.0.0+commitHash），與 MainLayout.AppVersion
    //      實際查詢的 Dashboard assembly 版本不同 assembly。
    //   ② 忽略 AppVersion getter 內 `+commitHash` suffix 剝離邏輯（MainLayout.razor.cs:14-19）。
    // 兩條斷言邏輯本身錯誤，刪除而非寫假斷言遷就（FF 三十一 嚴格版）。
}