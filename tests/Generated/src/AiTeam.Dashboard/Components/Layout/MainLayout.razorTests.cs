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

    [Fact]
    public void AppVersion_當版本資訊不存在時_應回傳空字串()
    {
        // Arrange
        // 透過反射驗證邏輯：若 InformationalVersion 為 null 或空白，應回傳空字串
        var versionAttribute = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>();

        var layout = new TestableMainLayout();

        // Act
        var result = layout.GetAppVersion();

        // Assert
        if (versionAttribute == null || string.IsNullOrWhiteSpace(versionAttribute.InformationalVersion))
        {
            result.Should().BeEmpty();
        }
        else
        {
            result.Should().Be($"v{versionAttribute.InformationalVersion}");
        }
    }

    [Fact]
    public void AppVersion_版本資訊與組件屬性一致_應正確對應()
    {
        // Arrange
        var layout = new TestableMainLayout();
        var expectedVersion = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        var expected = string.IsNullOrWhiteSpace(expectedVersion)
            ? string.Empty
            : $"v{expectedVersion}";

        // Act
        var result = layout.GetAppVersion();

        // Assert
        result.Should().Be(expected);
    }
}