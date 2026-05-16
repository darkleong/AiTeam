// 測試標的：AiTeam.Dashboard.Components.Pages.Settings.SystemSettings
// 驗證：grep -r 'class SystemSettings' src/AiTeam.Dashboard/ → 命中 SystemSettings.razor.cs:5

using System.Reflection;
using AiTeam.Dashboard.Components.Pages.Settings;
using FluentAssertions;
using Xunit;

namespace AiTeam.Dashboard.Components.Pages.Settings.Tests;

public class SystemSettingsTests
{
    private static bool InvokeIsValidSnowflakeId(string value)
    {
        var method = typeof(SystemSettings).GetMethod(
            "IsValidSnowflakeId",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        return (bool)method.Invoke(null, new object?[] { value })!;
    }

    private static SystemSettings CreateInstanceWithDelays(int min, int max)
    {
        var instance = new SystemSettings();
        typeof(SystemSettings)
            .GetField("_mockDelayMin", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(instance, min);
        typeof(SystemSettings)
            .GetField("_mockDelayMax", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(instance, max);
        return instance;
    }

    private static bool InvokeIsMockDelayValid(SystemSettings instance)
    {
        var method = typeof(SystemSettings).GetMethod(
            "IsMockDelayValid",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        return (bool)method.Invoke(instance, null)!;
    }

    // ── IsValidSnowflakeId ───────────────────────────────────────────────

    [Fact]
    public void IsValidSnowflakeId_有效的17位純數字_應回傳True()
    {
        InvokeIsValidSnowflakeId("12345678901234567").Should().BeTrue();
    }

    [Fact]
    public void IsValidSnowflakeId_有效的20位純數字_應回傳True()
    {
        InvokeIsValidSnowflakeId("12345678901234567890").Should().BeTrue();
    }

    [Fact]
    public void IsValidSnowflakeId_空字串代表清除_應回傳True()
    {
        InvokeIsValidSnowflakeId(string.Empty).Should().BeTrue();
    }

    [Fact]
    public void IsValidSnowflakeId_16位數字太短_應回傳False()
    {
        InvokeIsValidSnowflakeId("1234567890123456").Should().BeFalse();
    }

    [Fact]
    public void IsValidSnowflakeId_21位數字太長_應回傳False()
    {
        InvokeIsValidSnowflakeId("123456789012345678901").Should().BeFalse();
    }

    [Fact]
    public void IsValidSnowflakeId_含有字母_應回傳False()
    {
        InvokeIsValidSnowflakeId("1234567890abcdefg").Should().BeFalse();
    }

    // ── IsMockDelayValid ─────────────────────────────────────────────────

    [Fact]
    public void IsMockDelayValid_最小0最大1000_應回傳True()
    {
        var instance = CreateInstanceWithDelays(0, 1000);
        InvokeIsMockDelayValid(instance).Should().BeTrue();
    }

    [Fact]
    public void IsMockDelayValid_最大等於最小_應回傳False()
    {
        var instance = CreateInstanceWithDelays(1000, 1000);
        InvokeIsMockDelayValid(instance).Should().BeFalse();
    }

    [Fact]
    public void IsMockDelayValid_最小為負數_應回傳False()
    {
        var instance = CreateInstanceWithDelays(-1, 1000);
        InvokeIsMockDelayValid(instance).Should().BeFalse();
    }

    [Fact]
    public void IsMockDelayValid_最大超過600000_應回傳False()
    {
        var instance = CreateInstanceWithDelays(0, 600001);
        InvokeIsMockDelayValid(instance).Should().BeFalse();
    }
}
