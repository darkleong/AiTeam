using System;
using AiTeam.Shared.Dtos;
using FluentAssertions;
using Xunit;

namespace AiTeam.Shared.Tests.Dtos;

public class TaskItemDtoTests
{
    #region 屬性預設值測試

    [Fact]
    public void 建立新實例_無設定任何屬性_字串屬性應為空字串()
    {
        // Arrange & Act
        var dto = new TaskItemDto();

        // Assert
        dto.Title.Should().Be("");
        dto.TriggeredBy.Should().Be("");
        dto.AssignedAgent.Should().Be("");
        dto.Status.Should().Be("");
    }

    [Fact]
    public void 建立新實例_無設定任何屬性_可為Null屬性應為Null()
    {
        // Arrange & Act
        var dto = new TaskItemDto();

        // Assert
        dto.CompletedAt.Should().BeNull();
        dto.ProjectName.Should().BeNull();
        dto.TeamName.Should().BeNull();
    }

    [Fact]
    public void 建立新實例_無設定任何屬性_Id應為空Guid()
    {
        // Arrange & Act
        var dto = new TaskItemDto();

        // Assert
        dto.Id.Should().Be(Guid.Empty);
    }

    #endregion

    #region Duration 屬性測試

    [Fact]
    public void Duration_CompletedAt有值_應回傳CompletedAt與CreatedAt的差值()
    {
        // Arrange
        var createdAt = new DateTime(2024, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var completedAt = new DateTime(2024, 1, 1, 12, 30, 0, DateTimeKind.Utc);
        var expected = TimeSpan.FromHours(2.5);

        var dto = new TaskItemDto
        {
            CreatedAt = createdAt,
            CompletedAt = completedAt
        };

        // Act
        var duration = dto.Duration;

        // Assert
        duration.Should().Be(expected);
    }

    [Fact]
    public void Duration_CompletedAt為Null_應回傳Null()
    {
        // Arrange
        var dto = new TaskItemDto
        {
            CreatedAt = new DateTime(2024, 1, 1, 10, 0, 0, DateTimeKind.Utc),
            CompletedAt = null
        };

        // Act
        var duration = dto.Duration;

        // Assert
        duration.Should().BeNull();
    }

    [Fact]
    public void Duration_CompletedAt與CreatedAt相同_應回傳零TimeSpan()
    {
        // Arrange
        var sameTime = new DateTime(2024, 6, 15, 8, 0, 0, DateTimeKind.Utc);
        var dto = new TaskItemDto
        {
            CreatedAt = sameTime,
            CompletedAt = sameTime
        };

        // Act
        var duration = dto.Duration;

        // Assert
        duration.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void Duration_CompletedAt早於CreatedAt_應回傳負值TimeSpan()
    {
        // Arrange
        var createdAt = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var completedAt = new DateTime(2024, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var expected = TimeSpan.FromHours(-2);

        var dto = new TaskItemDto
        {
            CreatedAt = createdAt,
            CompletedAt = completedAt
        };

        // Act
        var duration = dto.Duration;

        // Assert
        duration.Should().Be(expected);
    }

    [Fact]
    public void Duration_耗時跨越多天_應正確計算差值()
    {
        // Arrange
        var createdAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var completedAt = new DateTime(2024, 1, 4, 6, 0, 0, DateTimeKind.Utc);
        var expected = TimeSpan.FromHours(78); // 3天 + 6小時

        var dto = new TaskItemDto
        {
            CreatedAt = createdAt,
            CompletedAt = completedAt
        };

        // Act
        var duration = dto.Duration;

        // Assert
        duration.Should().Be(expected);
    }

    #endregion

    #region 屬性設定與讀取測試

    [Fact]
    public void 設定所有屬性_讀取時_應回傳相同值()
    {
        // Arrange
        var id = Guid.NewGuid();
        var createdAt = new DateTime(2024, 3, 15, 9, 0, 0, DateTimeKind.Utc);
        var completedAt = new DateTime(2024, 3, 15, 11, 0, 0, DateTimeKind.Utc);

        var dto = new TaskItemDto
        {
            Id = id,
            Title = "測試任務",
            TriggeredBy = "user@example.com",
            AssignedAgent = "AgentA",
            Status = "Completed",
            CreatedAt = createdAt,
            CompletedAt = completedAt,
            ProjectName = "專案Alpha",
            TeamName = "開發團隊"
        };

        // Assert
        dto.Id.Should().Be(id);
        dto.Title.Should().Be("測試任務");
        dto.TriggeredBy.Should().Be("user@example.com");
        dto.AssignedAgent.Should().Be("AgentA");
        dto.Status.Should().Be("Completed");
        dto.CreatedAt.Should().Be(createdAt);
        dto.CompletedAt.Should().Be(completedAt);
        dto.ProjectName.Should().Be("專案Alpha");
        dto.TeamName.Should().Be("開發團隊");
    }

    [Fact]
    public void 設定ProjectName與TeamName為Null_讀取時_應回傳Null()
    {
        // Arrange
        var dto = new TaskItemDto
        {
            ProjectName = null,
            TeamName = null
        };

        // Assert
        dto.ProjectName.Should().BeNull();
        dto.TeamName.Should().BeNull();
    }

    [Fact]
    public void 設定Status為特定字串_讀取時_應回傳相同字串()
    {
        // Arrange
        var dto = new TaskItemDto
        {
            Status = "Running"
        };

        // Act & Assert
        dto.Status.Should().Be("Running");
    }

    [Fact]
    public void 設定Id為新Guid_讀取時_應回傳相同Guid()
    {
        // Arrange
        var expectedId = Guid.NewGuid();
        var dto = new TaskItemDto
        {
            Id = expectedId
        };

        // Act & Assert
        dto.Id.Should().Be(expectedId);
    }

    #endregion

    #region Duration 精確度測試

    [Fact]
    public void Duration_耗時包含毫秒_應精確計算差值()
    {
        // Arrange
        var createdAt = new DateTime(2024, 5, 10, 10, 0, 0, 0, DateTimeKind.Utc);
        var completedAt = new DateTime(2024, 5, 10, 10, 0, 1, 500, DateTimeKind.Utc);
        var expected = TimeSpan.FromMilliseconds(1500);

        var dto = new TaskItemDto
        {
            CreatedAt = createdAt,
            CompletedAt = completedAt
        };

        // Act
        var duration = dto.Duration;

        // Assert
        duration.Should().Be(expected);
    }

    [Fact]
    public void Duration_多次讀取_應回傳相同值()
    {
        // Arrange
        var createdAt = new DateTime(2024, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var completedAt = new DateTime(2024, 1, 1, 11, 0, 0, DateTimeKind.Utc);

        var dto = new TaskItemDto
        {
            CreatedAt = createdAt,
            CompletedAt = completedAt
        };

        // Act
        var firstRead = dto.Duration;
        var secondRead = dto.Duration;

        // Assert
        firstRead.Should().Be(secondRead);
    }

    #endregion
}