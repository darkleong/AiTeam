using AiTeam.Shared.Dtos;
using Xunit;

namespace AiTeam.Tests.Unit.Dtos;

public class AgentConfigDtoTests
{
    [Fact]
    public void 預設值_Id應為空Guid()
    {
        var dto = new AgentConfigDto();
        Assert.Equal(Guid.Empty, dto.Id);
    }

    [Fact]
    public void 預設值_Name應為空字串()
    {
        var dto = new AgentConfigDto();
        Assert.Equal("", dto.Name);
    }

    [Fact]
    public void 預設值_Description應為空字串()
    {
        var dto = new AgentConfigDto();
        Assert.Equal("", dto.Description);
    }

    [Fact]
    public void 預設值_TrustLevel應為0()
    {
        var dto = new AgentConfigDto();
        Assert.Equal(0, dto.TrustLevel);
    }

    [Fact]
    public void 預設值_IsActive應為false()
    {
        var dto = new AgentConfigDto();
        Assert.False(dto.IsActive);
    }

    [Fact]
    public void 預設值_TeamName應為null()
    {
        var dto = new AgentConfigDto();
        Assert.Null(dto.TeamName);
    }

    [Fact]
    public void 預設值_Provider應為空字串()
    {
        var dto = new AgentConfigDto();
        Assert.Equal("", dto.Provider);
    }

    [Fact]
    public void 預設值_Model應為空字串()
    {
        var dto = new AgentConfigDto();
        Assert.Equal("", dto.Model);
    }

    [Fact]
    public void 所有屬性_可正確設定與讀取()
    {
        var id = Guid.NewGuid();
        var dto = new AgentConfigDto
        {
            Id          = id,
            Name        = "CEO",
            Description = "Victoria CEO",
            TrustLevel  = 3,
            IsActive    = true,
            TeamName    = "AiTeam",
            Provider    = "Anthropic",
            Model       = "claude-sonnet-4-6"
        };

        Assert.Equal(id, dto.Id);
        Assert.Equal("CEO", dto.Name);
        Assert.Equal("Victoria CEO", dto.Description);
        Assert.Equal(3, dto.TrustLevel);
        Assert.True(dto.IsActive);
        Assert.Equal("AiTeam", dto.TeamName);
        Assert.Equal("Anthropic", dto.Provider);
        Assert.Equal("claude-sonnet-4-6", dto.Model);
    }

    [Fact]
    public void TeamName_允許設為null()
    {
        var dto = new AgentConfigDto { TeamName = "AiTeam" };
        dto.TeamName = null;
        Assert.Null(dto.TeamName);
    }

    [Fact]
    public void IsActive_可切換為true與false()
    {
        var dto = new AgentConfigDto { IsActive = true };
        Assert.True(dto.IsActive);

        dto.IsActive = false;
        Assert.False(dto.IsActive);
    }

    [Fact]
    public void Provider和Model_可設定Gemini模型()
    {
        var dto = new AgentConfigDto
        {
            Provider = "Gemini",
            Model    = "gemini-2.5-flash"
        };

        Assert.Equal("Gemini", dto.Provider);
        Assert.Equal("gemini-2.5-flash", dto.Model);
    }
}
