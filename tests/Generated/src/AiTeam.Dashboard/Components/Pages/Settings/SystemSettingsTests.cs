// 測試標的：AiTeam.Dashboard.Components.Pages.Settings.SystemSettings
// 驗證：grep 'class SystemSettings' src/AiTeam.Dashboard/Components/Pages/Settings/SystemSettings.razor.cs → 命中第 3 行

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
        return (bool)method.Invoke(null, new object[] { value })!;
    }

    private static bool InvokeIsMockDelayValid(int delayMin, int delayMax)
    {
        var instance = Activator.CreateInstance<SystemSettings>();
        typeof(SystemSettings)
            .GetField("_mockDelayMin", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(instance, delayMin);
        typeof(SystemSettings)
            .GetField("_mockDelayMax", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(instance, delayMax);
        var method = typeof(SystemSettings).GetMethod(
            "IsMockDelayValid",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        return (bool)method.Invoke(instance, null)!;
    }

    // ── IsValidSnowflakeId ───────────────────────────────────────────────────

    [Fact]
    public void IsValidSnowflakeId_空字串_回傳True代表清除操作有效()
    {
        var result = InvokeIsValidSnowflakeId("");

        result.Should().BeTrue();
    }

    [Fact]
    public void IsValidSnowflakeId_17位數字_回傳True()
    {
        var result = InvokeIsValidSnowflakeId("12345678901234567");

        result.Should().BeTrue();
    }

    [Fact]
    public void IsValidSnowflakeId_20位數字_回傳True()
    {
        var result = InvokeIsValidSnowflakeId("12345678901234567890");

        result.Should().BeTrue();
    }

    [Fact]
    public void IsValidSnowflakeId_16位數字_回傳False過短()
    {
        var result = InvokeIsValidSnowflakeId("1234567890123456");

        result.Should().BeFalse();
    }

    [Fact]
    public void IsValidSnowflakeId_21位數字_回傳False過長()
    {
        var result = InvokeIsValidSnowflakeId("123456789012345678901");

        result.Should().BeFalse();
    }

    [Fact]
    public void IsValidSnowflakeId_含非數字字元_回傳False格式無效()
    {
        var result = InvokeIsValidSnowflakeId("1234567890abc456789");

        result.Should().BeFalse();
    }

    // ── IsMockDelayValid ─────────────────────────────────────────────────────

    [Fact]
    public void IsMockDelayValid_預設值30000至60000_回傳True()
    {
        var result = InvokeIsMockDelayValid(30000, 60000);

        result.Should().BeTrue();
    }

    [Fact]
    public void IsMockDelayValid_最小值為負數_回傳False()
    {
        var result = InvokeIsMockDelayValid(-1, 60000);

        result.Should().BeFalse();
    }

    [Fact]
    public void IsMockDelayValid_最大值等於最小值_回傳False()
    {
        var result = InvokeIsMockDelayValid(30000, 30000);

        result.Should().BeFalse();
    }

    [Fact]
    public void IsMockDelayValid_最大值超過600000_回傳False()
    {
        var result = InvokeIsMockDelayValid(0, 600001);

        result.Should().BeFalse();
    }

    [Fact]
    public void IsMockDelayValid_最小值0最大值1_回傳True邊界有效()
    {
        var result = InvokeIsMockDelayValid(0, 1);

        result.Should().BeTrue();
    }

    [Fact]
    public void IsMockDelayValid_最小值0最大值600000_回傳True邊界有效()
    {
        var result = InvokeIsMockDelayValid(0, 600000);

        result.Should().BeTrue();
    }
}
