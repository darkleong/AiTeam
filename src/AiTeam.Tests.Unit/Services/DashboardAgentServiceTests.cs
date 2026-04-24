using AiTeam.Dashboard.Configuration;
using AiTeam.Dashboard.Services;
using AiTeam.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace AiTeam.Tests.Unit.Services;

public class DashboardAgentServiceTests : IDisposable
{
    private readonly AppDbContext _db;

    public DashboardAgentServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);
    }

    public void Dispose() => _db.Dispose();

    private DashboardAgentService CreateService(AgentTokenLimits? limits = null) =>
        new(_db, Options.Create(limits ?? new AgentTokenLimits()));

    private async Task<Guid> SeedTeamAsync(string name = "TestTeam")
    {
        var teamId = Guid.NewGuid();
        _db.Teams.Add(new Team { Id = teamId, Name = name });
        await _db.SaveChangesAsync();
        return teamId;
    }

    private async Task<AgentConfig> SeedAgentAsync(Guid teamId, string name = "CEO", int trustLevel = 1, bool isActive = true)
    {
        var agent = new AgentConfig
        {
            Id         = Guid.NewGuid(),
            TeamId     = teamId,
            Name       = name,
            TrustLevel = trustLevel,
            IsActive   = isActive
        };
        _db.AgentConfigs.Add(agent);
        await _db.SaveChangesAsync();
        return agent;
    }

    // ── GetAgentConfigsAsync ─────────────────────────────────────────────

    [Fact]
    public async Task GetAgentConfigsAsync_無Agent時_回傳空清單()
    {
        var service = CreateService();
        var result = await service.GetAgentConfigsAsync();
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetAgentConfigsAsync_Agent名稱不在TokenLimits_回傳預設ProviderAndModel()
    {
        var teamId = await SeedTeamAsync();
        await SeedAgentAsync(teamId, "CEO");

        var service = CreateService(new AgentTokenLimits());
        var result = await service.GetAgentConfigsAsync();

        Assert.Single(result);
        Assert.Equal("Anthropic", result[0].Provider);
        Assert.Equal("claude-sonnet-4-6", result[0].Model);
    }

    [Fact]
    public async Task GetAgentConfigsAsync_Agent名稱在TokenLimits_回傳設定的ProviderAndModel()
    {
        var teamId = await SeedTeamAsync();
        await SeedAgentAsync(teamId, "CEO");

        var limits = new AgentTokenLimits();
        limits.Agents["CEO"] = new AgentLimit { Provider = "Gemini", Model = "gemini-2.5-flash" };

        var service = CreateService(limits);
        var result = await service.GetAgentConfigsAsync();

        Assert.Single(result);
        Assert.Equal("Gemini", result[0].Provider);
        Assert.Equal("gemini-2.5-flash", result[0].Model);
    }

    [Fact]
    public async Task GetAgentConfigsAsync_多個Agent混合設定_各自套用正確ProviderAndModel()
    {
        var teamId = await SeedTeamAsync();
        await SeedAgentAsync(teamId, "CEO");
        await SeedAgentAsync(teamId, "Dev");

        var limits = new AgentTokenLimits();
        limits.Agents["CEO"] = new AgentLimit { Provider = "Anthropic", Model = "claude-opus-4-7" };

        var service = CreateService(limits);
        var result = await service.GetAgentConfigsAsync();

        Assert.Equal(2, result.Count);
        var ceo = result.First(r => r.Name == "CEO");
        var dev = result.First(r => r.Name == "Dev");

        Assert.Equal("claude-opus-4-7", ceo.Model);
        Assert.Equal("Anthropic", dev.Provider);
        Assert.Equal("claude-sonnet-4-6", dev.Model);
    }

    [Fact]
    public async Task GetAgentConfigsAsync_回傳DTO包含正確基本欄位()
    {
        var teamId = await SeedTeamAsync("MyTeam");
        await SeedAgentAsync(teamId, "CEO", trustLevel: 2, isActive: true);

        var service = CreateService();
        var result = await service.GetAgentConfigsAsync();

        Assert.Single(result);
        Assert.Equal("CEO", result[0].Name);
        Assert.Equal(2, result[0].TrustLevel);
        Assert.True(result[0].IsActive);
        Assert.Equal("MyTeam", result[0].TeamName);
    }

    // ── UpdateIsActiveAsync ──────────────────────────────────────────────

    [Fact]
    public async Task UpdateIsActiveAsync_AgentId不存在_回傳原始isActive值()
    {
        var service = CreateService();
        var result = await service.UpdateIsActiveAsync(Guid.NewGuid(), true);
        Assert.True(result);
    }

    [Fact]
    public async Task UpdateIsActiveAsync_Agent存在_更新為true並回傳true()
    {
        var teamId = await SeedTeamAsync();
        var agent  = await SeedAgentAsync(teamId, isActive: false);

        var service = CreateService();
        var result  = await service.UpdateIsActiveAsync(agent.Id, true);

        Assert.True(result);

        var updated = await _db.AgentConfigs.FindAsync(agent.Id);
        Assert.True(updated!.IsActive);
    }

    [Fact]
    public async Task UpdateIsActiveAsync_Agent存在_更新為false並回傳false()
    {
        var teamId = await SeedTeamAsync();
        var agent  = await SeedAgentAsync(teamId, isActive: true);

        var service = CreateService();
        var result  = await service.UpdateIsActiveAsync(agent.Id, false);

        Assert.False(result);

        var updated = await _db.AgentConfigs.FindAsync(agent.Id);
        Assert.False(updated!.IsActive);
    }

    // ── UpdateTrustLevelAsync ────────────────────────────────────────────

    [Fact]
    public async Task UpdateTrustLevelAsync_AgentId不存在_不拋例外()
    {
        var service = CreateService();
        var ex = await Record.ExceptionAsync(() => service.UpdateTrustLevelAsync(Guid.NewGuid(), 2));
        Assert.Null(ex);
    }

    [Fact]
    public async Task UpdateTrustLevelAsync_Agent存在_信任等級更新至DB()
    {
        var teamId = await SeedTeamAsync();
        var agent  = await SeedAgentAsync(teamId, trustLevel: 0);

        var service = CreateService();
        await service.UpdateTrustLevelAsync(agent.Id, 3);

        var updated = await _db.AgentConfigs.FindAsync(agent.Id);
        Assert.Equal(3, updated!.TrustLevel);
    }

    // ── CreateAgentAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task CreateAgentAsync_建立成功_回傳正確DTO()
    {
        await SeedTeamAsync("AiTeam");

        var service = CreateService();
        var dto = await service.CreateAgentAsync("Ops", "Ops Agent", 1);

        Assert.Equal("Ops", dto.Name);
        Assert.Equal("Ops Agent", dto.Description);
        Assert.Equal(1, dto.TrustLevel);
        Assert.True(dto.IsActive);
        Assert.NotEqual(Guid.Empty, dto.Id);
        Assert.Equal("AiTeam", dto.TeamName);
    }

    [Fact]
    public async Task CreateAgentAsync_名稱有空白_自動去除首尾空白()
    {
        await SeedTeamAsync();

        var service = CreateService();
        var dto = await service.CreateAgentAsync("  Ops  ", "  Ops desc  ", 0);

        Assert.Equal("Ops", dto.Name);
        Assert.Equal("Ops desc", dto.Description);
    }

    [Fact]
    public async Task CreateAgentAsync_名稱在TokenLimits中_套用設定的ProviderAndModel()
    {
        await SeedTeamAsync();

        var limits = new AgentTokenLimits();
        limits.Agents["Rena"] = new AgentLimit { Provider = "Gemini", Model = "gemini-2.5-flash" };

        var service = CreateService(limits);
        var dto = await service.CreateAgentAsync("Rena", "Rena Agent", 0);

        Assert.Equal("Gemini", dto.Provider);
        Assert.Equal("gemini-2.5-flash", dto.Model);
    }

    [Fact]
    public async Task CreateAgentAsync_名稱不在TokenLimits中_套用預設ProviderAndModel()
    {
        await SeedTeamAsync();

        var service = CreateService(new AgentTokenLimits());
        var dto = await service.CreateAgentAsync("NewBot", "New bot", 0);

        Assert.Equal("Anthropic", dto.Provider);
        Assert.Equal("claude-sonnet-4-6", dto.Model);
    }

    [Fact]
    public async Task CreateAgentAsync_建立後可從DB查到Agent()
    {
        await SeedTeamAsync();

        var service = CreateService();
        var dto = await service.CreateAgentAsync("Tester", "Test Agent", 2);

        var inDb = await _db.AgentConfigs.FirstOrDefaultAsync(a => a.Id == dto.Id);
        Assert.NotNull(inDb);
        Assert.Equal("Tester", inDb.Name);
        Assert.Equal(2, inDb.TrustLevel);
    }

    // ── GetAllAgentStatusesAsync ─────────────────────────────────────────

    [Fact]
    public async Task GetAllAgentStatusesAsync_無Agent時_回傳空清單()
    {
        var service = CreateService();
        var result = await service.GetAllAgentStatusesAsync();
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetAllAgentStatusesAsync_無Running任務_Agent狀態為idle()
    {
        var teamId = await SeedTeamAsync();
        await SeedAgentAsync(teamId, "CEO");

        var service = CreateService();
        var result = await service.GetAllAgentStatusesAsync();

        Assert.Single(result);
        Assert.Equal("CEO", result[0].AgentName);
        Assert.Equal("idle", result[0].Status);
        Assert.Null(result[0].CurrentTaskTitle);
    }

    [Fact]
    public async Task GetAllAgentStatusesAsync_有Running任務_Agent狀態為running且含TaskTitle()
    {
        var teamId = await SeedTeamAsync();
        await SeedAgentAsync(teamId, "Dev");

        _db.Tasks.Add(new TaskItem
        {
            Id            = Guid.NewGuid(),
            AssignedAgent = "Dev",
            Title         = "Implement feature X",
            Status        = "running",
            TriggeredBy   = "Discord"
        });
        await _db.SaveChangesAsync();

        var service = CreateService();
        var result = await service.GetAllAgentStatusesAsync();

        Assert.Single(result);
        Assert.Equal("running", result[0].Status);
        Assert.Equal("Implement feature X", result[0].CurrentTaskTitle);
    }

    [Fact]
    public async Task GetAllAgentStatusesAsync_有Done任務_TodayCompletedCount正確()
    {
        var teamId = await SeedTeamAsync();
        await SeedAgentAsync(teamId, "CEO");

        _db.Tasks.Add(new TaskItem
        {
            Id            = Guid.NewGuid(),
            AssignedAgent = "CEO",
            Title         = "Done task",
            Status        = "done",
            TriggeredBy   = "Discord",
            CreatedAt     = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        var service = CreateService();
        var result = await service.GetAllAgentStatusesAsync();

        Assert.Single(result);
        Assert.Equal(1, result[0].TodayCompletedCount);
    }
}
