```csharp
using AiTeam.Shared.Dtos;
using FluentAssertions;

namespace AiTeam.Shared.Tests.Dtos;

public class TaskItemDtoTests
{
    // ────────────────────────────────────────────────
    // 預設值測試
    // ────────────────────────────────────────────────

    [Fact]
    public void 建立TaskItemDto_未指定任何屬性_字串屬性應為空字串()
    {
        // Arrange & Act
        var dto = new TaskItemDto();

        // Assert
        dto.Title.Should().BeEmpty();
        dto.TriggeredBy.Should().BeEmpty();
        dto.AssignedAgent.Should().BeEmpty();
        dto.Status.Should().BeEmpty();
    }

    [Fact]
    public void 建立TaskItemDto_未指定任何屬性_可為Null屬性應為Null()
    {
        // Arrange & Act
        var dto = new TaskItemDto();

        // Assert
        dto.CompletedAt.Should().BeNull();
        dto.ProjectName.Should().BeNull();
        dto.TeamName.Should().BeNull();
        dto.Duration.Should().BeNull();
    }

    [Fact]
    public void 建立TaskItemDto_未指定任何屬性_Id應為空Guid()
    {
        // Arrange & Act
        var dto = new TaskItemDto();

        // Assert
        dto.Id.Should().Be(Guid.Empty);
    }

    // ────────────────────────────────────────────────
    // Id 屬性測試
    // ────────────────────────────────────────────────

    [Fact]
    public void 設定Id_給定有效Guid_應正確儲存並回傳相同值()
    {
        // Arrange
        var expectedId = Guid.NewGuid();
        var dto = new TaskItemDto();

        // Act
        dto.Id = expectedId;

        // Assert
        dto.Id.Should().Be(expectedId);
    }

    [Fact]
    public void 設定Id_給定空Guid_應正確儲存空Guid()
    {
        // Arrange
        var dto = new TaskItemDto();

        // Act
        dto.Id = Guid.Empty;

        // Assert
        dto.Id.Should().Be(Guid.Empty);
    }

    // ────────────────────────────────────────────────
    // Title 屬性測試
    // ────────────────────────────────────────────────

    [Fact]
    public void 設定Title_給定一般字串_應正確儲存並回傳相同值()
    {
        // Arrange
        var dto = new TaskItemDto();
        const string expectedTitle = "執行資料分析任務";

        // Act
        dto.Title = expectedTitle;

        // Assert
        dto.Title.Should().Be(expectedTitle);
    }

    [Fact]
    public void 設定Title_給定空字串_應正確儲存空字串()
    {
        // Arrange
        var dto = new TaskItemDto { Title = "初始標題" };

        // Act
        dto.Title = string.Empty;

        // Assert
        dto.Title.Should().BeEmpty();
    }

    // ────────────────────────────────────────────────
    // TriggeredBy 屬性測試
    // ────────────────────────────────────────────────

    [Fact]
    public void 設定TriggeredBy_給定使用者名稱_應正確儲存並回傳相同值()
    {
        // Arrange
        var dto = new TaskItemDto();
        const string expectedUser = "user@example.com";

        // Act
        dto.TriggeredBy = expectedUser;

        // Assert
        dto.TriggeredBy.Should().Be(expectedUser);
    }

    [Fact]
    public void 設定TriggeredBy_給定空字串_應正確儲存空字串()
    {
        // Arrange
        var dto = new TaskItemDto { TriggeredBy = "admin" };

        // Act
        dto.TriggeredBy = string.Empty;

        // Assert
        dto.TriggeredBy.Should().BeEmpty();
    }

    // ────────────────────────────────────────────────
    // AssignedAgent 屬性測試
    // ────────────────────────────────────────────────

    [Fact]
    public void 設定AssignedAgent_給定代理名稱_應正確儲存並回傳相同值()
    {
        // Arrange
        var dto = new TaskItemDto();
        const string expectedAgent = "DataAnalysisAgent";

        // Act
        dto.AssignedAgent = expectedAgent;

        // Assert
        dto.AssignedAgent.Should().Be(expectedAgent);
    }

    [Fact]
    public void 設定AssignedAgent_給定空字串_應正確儲存空字串()
    {
        // Arrange
        var dto = new TaskItemDto { AssignedAgent = "SomeAgent" };

        // Act
        dto.AssignedAgent = string.Empty;

        // Assert
        dto.AssignedAgent.Should().BeEmpty();
    }

    // ────────────────────────────────────────────────
    // Status 屬性測試
    // ────────────────────────────────────────────────

    [Fact]
    public void 設定Status_給定有效狀態字串_應正確儲存並回傳相同值()
    {
        // Arrange
        var dto = new TaskItemDto();
        const string expectedStatus = "Running";

        // Act
        dto.Status = expectedStatus;

        // Assert
        dto.Status.Should().Be(expectedStatus);
    }

    [Fact]
    public void 設定Status_給定空字串_應正確儲存空字串()
    {
        // Arrange
        var dto = new TaskItemDto { Status = "Completed" };

        // Act
        dto.Status = string.Empty;

        // Assert
        dto.Status.Should().BeEmpty();
    }

    // ────────────────────────────────────────────────
    // CreatedAt 屬性測試
    // ────────────────────────────────────────────────

    [Fact]
    public void 設定CreatedAt_給定特定日期時間_應正確儲存並回傳相同值()
    {
        // Arrange
        var dto = new TaskItemDto();
        var expectedCreatedAt = new DateTime(2024, 6, 15, 10, 30, 0, DateTimeKind.Utc);

        // Act
        dto.CreatedAt = expectedCreatedAt;

        // Assert
        dto.CreatedAt.Should().Be(expectedCreatedAt);
    }

    [Fact]
    public void 設定CreatedAt_給定MinValue_應正確儲存MinValue()
    {
        // Arrange
        var dto = new TaskItemDto();

        // Act
        dto.CreatedAt = DateTime.MinValue;

        // Assert
        dto.CreatedAt.Should().Be(DateTime.MinValue);
    }

    // ────────────────────────────────────────────────
    // CompletedAt 屬性測試
    // ────────────────────────────────────────────────

    [Fact]
    public void 設定CompletedAt_給定有效日期時間_應正確儲存並回傳相同值()
    {
        // Arrange
        var dto = new TaskItemDto();
        var expectedCompletedAt = new DateTime(2024, 6, 15, 12, 0, 0, DateTimeKind.Utc);

        // Act
        dto.CompletedAt = expectedCompletedAt;

        // Assert
        dto.CompletedAt.Should().Be(expectedCompletedAt);
        dto.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public void 設定CompletedAt_給定Null_應正確儲存Null()
    {
        // Arrange
        var dto = new TaskItemDto
        {
            CompletedAt = new DateTime(2024, 6, 15, 12, 0, 0, DateTimeKind.Utc)
        };

        // Act
        dto.CompletedAt = null;

        // Assert
        dto.CompletedAt.Should().BeNull();
    }

    // ────────────────────────────────────────────────
    // ProjectName 屬性測試
    // ────────────────────────────────────────────────

    [Fact]
    public void 設定ProjectName_給定有效專案名稱_應正確儲存並回傳相同值()
    {
        // Arrange
        var dto = new TaskItemDto();
        const string expectedProjectName = "AI 分析專案";

        // Act
        dto.ProjectName = expectedProjectName;

        // Assert
        dto.ProjectName.Should().Be(expectedProjectName);
    }

    [Fact]
    public void 設定ProjectName_給定Null_應正確儲存Null()
    {
        // Arrange
        var dto = new TaskItemDto { ProjectName = "舊專案名稱" };

        // Act
        dto.ProjectName = null;

        // Assert
        dto.ProjectName.Should().BeNull();
    }

    // ────────────────────────────────────────────────
    // TeamName 屬性測試
    // ────────────────────────────────────────────────

    [Fact]
    public void 設定TeamName_給定有效團隊名稱_應正確儲存並回傳相同值()
    {
        // Arrange
        var dto = new TaskItemDto();
        const string expectedTeamName = "資料科學團隊";

        // Act
        dto.TeamName = expectedTeamName;

        // Assert
        dto.TeamName.Should().Be(expectedTeamName);
    }

    [Fact]
    public void 設定TeamName_給定Null_應正確儲存Null()
    {
        // Arrange
        var dto = new TaskItemDto { TeamName = "舊團隊名稱" };

        // Act
        dto.TeamName = null;

        // Assert
        dto.TeamName.Should().BeNull();
    }

    // ────────────────────────────────────────────────
    // Duration 屬性測試
    // ────────────────────────────────────────────────

    [Fact]
    public void 設定Duration_給定有效執行時間_應正確儲存並回傳相同值()
    {
        // Arrange
        var dto = new TaskItemDto();
        var expectedDuration = TimeSpan.FromMinutes(90);

        // Act
        dto.Duration = expectedDuration;

        // Assert
        dto.Duration.Should().Be(expectedDuration);
        dto.Duration.Should().NotBeNull();
    }

    [Fact]
    public void 設定Duration_給定Null_應正確儲存Null表示任務尚未完成()
    {
        // Arrange
        var dto = new TaskItemDto { Duration = TimeSpan.FromHours(1) };

        // Act
        dto.Duration = null;

        // Assert
        dto.Duration.Should().BeNull();
    }

    // ────────────────────────────────────────────────
    // 整合情境測試
    // ────────────────────────────────────────────────

    [Fact]
    public void 建立TaskItemDto_設定所有屬性_所有屬性應正確儲存()
    {
        // Arrange
        var id = Guid.NewGuid();
        var createdAt = new DateTime(2024, 1, 1, 8, 0, 0, DateTimeKind.Utc);
        var completedAt = new DateTime(2024, 1, 1, 10, 30, 0, DateTimeKind.Utc);
        var duration = completedAt - createdAt;

        // Act
        var dto = new TaskItemDto
        {
            Id = id,
            Title = "完整任務標題",
            TriggeredBy = "operator@example.com",
            AssignedAgent = "ReportAgent",
            Status = "Completed",
            CreatedAt = createdAt,
            CompletedAt = completedAt,
            ProjectName = "季度報告專案",
            TeamName = "報告團隊",
            Duration = duration
        };

        // Assert
        dto.Id.Should().Be(id);
        dto.Title.Should().Be("完整任務標題");
        dto.TriggeredBy.Should().Be("operator@example.com");
        dto.AssignedAgent.Should().Be("ReportAgent");
        dto.Status.Should().Be("Completed");
        dto.CreatedAt.Should().Be(createdAt);
        dto.CompletedAt.Should().Be(completedAt);
        dto.ProjectName.Should().Be("季度報告專案");
        dto.TeamName.Should().Be("報告團隊");
        dto.Duration.Should().Be(duration);
    }

    [Fact]
    public void 建立TaskItemDto_任務尚未完成_CompletedAt與Duration應皆為Null()
    {
        // Arrange
        var createdAt = DateTime.UtcNow;

        // Act
        var dto = new TaskItemDto
        {
            Id = Guid.NewGuid(),
            Title = "進行中任務",
            TriggeredBy = "scheduler",
            AssignedAgent = "ProcessingAgent",
            Status = "Running",
            CreatedAt = createdAt,
            CompletedAt = null,
            Duration = null
        };

        // Assert
        dto.CompletedAt.Should().BeNull();
        dto.Duration.Should().BeNull();
        dto.Status.Should().Be("Running");
    }

    [Fact]
    public void Duration計算_CompletedAt減去CreatedAt_應等於設定的Duration值()
    {
        // Arrange
        var createdAt = new DateTime(2024, 3, 10, 9, 0, 0, DateTimeKind.Utc);
        var completedAt = new DateTime(2024, 3, 10, 11, 45, 0, DateTimeKind.Utc);
        var expectedDuration = completedAt - createdAt;

        // Act
        var dto = new TaskItemDto
        {
            CreatedAt = createdAt,
            CompletedAt = completedAt,
            Duration = expectedDuration
        };

        // Assert
        dto.Duration.Should().Be(TimeSpan.FromHours(2).Add(TimeSpan.FromMinutes(45)));
        dto.Duration!.Value.TotalMinutes.Should().Be(165);
    }
}
```