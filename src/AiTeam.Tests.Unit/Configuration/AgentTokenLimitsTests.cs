using AiTeam.Dashboard.Configuration;
using Xunit;

namespace AiTeam.Tests.Unit.Configuration;

public class AgentTokenLimitsTests
{
    [Fact]
    public void 預設MonthlyTokenLimitK_應為1000()
    {
        var limits = new AgentTokenLimits();
        Assert.Equal(1000, limits.MonthlyTokenLimitK);
    }

    [Fact]
    public void 預設Agents_應為空字典()
    {
        var limits = new AgentTokenLimits();
        Assert.Empty(limits.Agents);
    }

    [Fact]
    public void 設定MonthlyTokenLimitK_可正確讀取()
    {
        var limits = new AgentTokenLimits { MonthlyTokenLimitK = 2000 };
        Assert.Equal(2000, limits.MonthlyTokenLimitK);
    }

    [Fact]
    public void 可新增Agent限額並正確讀取()
    {
        var limits = new AgentTokenLimits();
        limits.Agents["CEO"] = new AgentLimit { DailyTokenLimitK = 50, MonthlyTokenLimitK = 500 };

        Assert.Single(limits.Agents);
        Assert.Equal(50, limits.Agents["CEO"].DailyTokenLimitK);
        Assert.Equal(500, limits.Agents["CEO"].MonthlyTokenLimitK);
    }

    [Fact]
    public void 可新增多個Agent並分別存取()
    {
        var limits = new AgentTokenLimits();
        limits.Agents["CEO"] = new AgentLimit { Provider = "Anthropic" };
        limits.Agents["Dev"] = new AgentLimit { Provider = "Gemini" };

        Assert.Equal(2, limits.Agents.Count);
        Assert.Equal("Anthropic", limits.Agents["CEO"].Provider);
        Assert.Equal("Gemini", limits.Agents["Dev"].Provider);
    }
}

public class AgentLimitTests
{
    [Fact]
    public void 預設DailyTokenLimitK_應為10()
    {
        var limit = new AgentLimit();
        Assert.Equal(10, limit.DailyTokenLimitK);
    }

    [Fact]
    public void 預設MonthlyTokenLimitK_應為200()
    {
        var limit = new AgentLimit();
        Assert.Equal(200, limit.MonthlyTokenLimitK);
    }

    [Fact]
    public void 預設Provider_應為Anthropic()
    {
        var limit = new AgentLimit();
        Assert.Equal("Anthropic", limit.Provider);
    }

    [Fact]
    public void 預設Model_應為claudeSonnet46()
    {
        var limit = new AgentLimit();
        Assert.Equal("claude-sonnet-4-6", limit.Model);
    }

    [Fact]
    public void 設定自訂Provider和Model_可正確讀取()
    {
        var limit = new AgentLimit { Provider = "Gemini", Model = "gemini-2.5-flash" };

        Assert.Equal("Gemini", limit.Provider);
        Assert.Equal("gemini-2.5-flash", limit.Model);
    }

    [Fact]
    public void 設定自訂DailyAndMonthlyLimits_可正確讀取()
    {
        var limit = new AgentLimit { DailyTokenLimitK = 100, MonthlyTokenLimitK = 1000 };

        Assert.Equal(100, limit.DailyTokenLimitK);
        Assert.Equal(1000, limit.MonthlyTokenLimitK);
    }
}
