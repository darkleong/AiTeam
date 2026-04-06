```csharp
using AiTeam.Dashboard.Services;
using AiTeam.Data;
using AiTeam.Data.Entities;
using AiTeam.Shared.Dtos;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AiTeam.Dashboard.Tests.Services;

public class DashboardTaskServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly DashboardTaskService _sut;

    public DashboardTaskServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _db = new AppDbContext(options);
        _sut = new DashboardTaskService(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    #region 測試資料建立輔助方法

    private static TaskItem CreateTask(
        string title = "測試任務",
        string status = "Pending",
        Guid? projectId = null,
        Guid? teamId = null,
        DateTime? createdAt = null,
        DateTime? completedAt = null)
    {
        return new TaskItem
        {
            Id            = Guid.NewGuid(),
            Title         = title,
            TriggeredBy   = "user@test.com",
            AssignedAgent = "AgentA",
            Status        = status,
            CreatedAt     = createdAt ?? DateTime.UtcNow,
            CompletedAt   = completedAt,
            ProjectId     = projectId,
            TeamId        = teamId
        };
    }

    private static Project CreateProject(string name = "測試專案")
    {
        return new Project
        {
            Id   = Guid.NewGuid(),
            Name = name
        };
    }

    private static Team CreateTeam(string name = "測試團隊")
    {
        return new Team
        {
            Id   = Guid.NewGuid(),
            Name = name
        };
    }

    private static TaskLog CreateTaskLog(Guid taskId, string step = "Step1", string status = "Success")
    {
        return new TaskLog
        {
            Id        = Guid.NewGuid(),
            TaskId    = taskId,
            Agent     = "AgentA",
            Step      = step,
            Status    = status,
            Payload   = "{}",
            CreatedAt = DateTime.UtcNow
        };
    }

    #endregion

    #region GetTasksPagedAsync 測試

    [Fact]
    public async Task 取得分頁任務_資料庫有資料_應回傳正確分頁結果()
    {
        // Arrange
        var tasks = Enumerable.Range(1, 10)
            .Select(i => CreateTask($"任務 {i}", createdAt: DateTime.UtcNow.AddMinutes(-i)))
            .ToList();

        await _db.Tasks.AddRangeAsync(tasks);
        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.GetTasksPagedAsync(page: 1, pageSize: 5);

        // Assert
        result.Should().NotBeNull();
        result.TotalCount.Should().Be(10);
        result.Items.Should().HaveCount(5);
    }

    [Fact]
    public async Task 取得分頁任務_第二頁_應回傳正確資料()
    {
        // Arrange
        var tasks = Enumerable.Range(1, 10)
            .Select(i => CreateTask($"任務 {i}", createdAt: DateTime.UtcNow.AddMinutes(-i)))
            .ToList();

        await _db.Tasks.AddRangeAsync(tasks);
        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.GetTasksPagedAsync(page: 2, pageSize: 5);

        // Assert
        result.Should().NotBeNull();
        result.TotalCount.Should().Be(10);
        result.Items.Should().HaveCount(5);
    }

    [Fact]
    public async Task 取得分頁任務_套用狀態篩選_應只回傳符合狀態的任務()
    {
        // Arrange
        var pendingTasks = Enumerable.Range(1, 3)
            .Select(i => CreateTask($"Pending 任務 {i}", status: "Pending"))
            .ToList();

        var completedTasks = Enumerable.Range(1, 5)
            .Select(i => CreateTask($"Completed 任務 {i}", status: "Completed"))
            .ToList();

        await _db.Tasks.AddRangeAsync(pendingTasks);
        await _db.Tasks.AddRangeAsync(completedTasks);
        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.GetTasksPagedAsync(statusFilter: "Pending");

        // Assert
        result.Should().NotBeNull();
        result.TotalCount.Should().Be(3);
        result.Items.Should().AllSatisfy(t => t.Status.Should().Be("Pending"));
    }

    [Fact]
    public async Task 取得分頁任務_無符合狀態篩選資料_應回傳空列表()
    {
        // Arrange
        var tasks = Enumerable.Range(1, 3)
            .Select(i => CreateTask($"任務 {i}", status: "Pending"))
            .ToList();

        await _db.Tasks.AddRangeAsync(tasks);
        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.GetTasksPagedAsync(statusFilter: "NotExistStatus");

        // Assert
        result.Should().NotBeNull();
        result.TotalCount.Should().Be(0);
        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task 取得分頁任務_任務有關聯專案與團隊_應正確對應專案與團隊名稱()
    {
        // Arrange
        var project = CreateProject("我的專案");
        var team    = CreateTeam("我的團隊");

        await _db.Projects.AddAsync(project);
        await _db.Teams.AddAsync(team);
        await _db.SaveChangesAsync();

        var task = CreateTask("有關聯的任務", projectId: project.Id, teamId: team.Id);
        await _db.Tasks.AddAsync(task);
        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.GetTasksPagedAsync();

        // Assert
        result.Items.Should().HaveCount(1);
        result.Items[0].ProjectName.Should().Be("我的專案");
        result.Items[0].TeamName.Should().Be("我的團隊");
    }

    [Fact]
    public async Task 取得分頁任務_任務無關聯專案與團隊_ProjectName與TeamName應為null()
    {
        // Arrange
        var task = CreateTask("無關聯任務");
        await _db.Tasks.AddAsync(task);
        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.GetTasksPagedAsync();

        // Assert
        result.Items.Should().HaveCount(1);
        result.Items[0].ProjectName.Should().BeNull();
        result.Items[0].TeamName.Should().BeNull();
    }

    [Fact]
    public async Task 取得分頁任務_任務已完成_Duration應正確計算()
    {
        // Arrange
        var createdAt   = new DateTime(2024, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var completedAt = new DateTime(2024, 1, 1, 11, 30, 0, DateTimeKind.Utc);
        var expected    = completedAt - createdAt;

        var task = CreateTask("已完成任務", status: "Completed", createdAt: createdAt, completedAt: completedAt);
        await _db.Tasks.AddAsync(task);
        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.GetTasksPagedAsync();

        // Assert
        result.Items.Should().HaveCount(1);
        result.Items[0].Duration.Should().Be(expected);
    }

    [Fact]
    public async Task 取得分頁任務_任務未完成_Duration應為null()
    {
        // Arrange
        var task = CreateTask("未完成任務", status: "Pending");
        await _db.Tasks.AddAsync(task);
        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.GetTasksPagedAsync();

        // Assert
        result.Items.Should().HaveCount(1);
        result.Items[0].Duration.Should().BeNull();
    }

    [Fact]
    public async Task 取得分頁任務_資料庫無資料_應回傳空結果且TotalCount為零()
    {
        // Act
        var result = await _sut.GetTasksPagedAsync();

        // Assert
        result.Should().NotBeNull();
        result.TotalCount.Should().Be(0);
        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task 取得分頁任務_結果應依建立時間降冪排序()
    {
        // Arrange
        var base_time = DateTime.UtcNow;
        var oldest  = CreateTask("最舊任務",  createdAt: base_time.AddHours(-3));
        var middle  = CreateTask("中間任務",  createdAt: base_time.AddHours(-2));
        var newest  = CreateTask("最新任務",  createdAt: base_time.AddHours(-1));

        await _db.Tasks.AddRangeAsync(oldest, middle, newest);
        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.GetTasksPagedAsync();

        // Assert
        result.Items.Should().HaveCount(3);
        result.Items[0].Title.Should().Be("最新任務");
        result.Items[1].Title.Should().Be("中間任務");
        result.Items[2].Title.Should().Be("最舊任務");
    }

    #endregion

    #region GetRecentTasksAsync 測試

    [Fact]
    public async Task 取得最近任務_資料庫有資料_應回傳指定數量的任務()
    {
        // Arrange
        var tasks = Enumerable.Range(1, 20)
            .Select(i => CreateTask($"任務 {i}", createdAt: DateTime.UtcNow.AddMinutes(-i)))
            .ToList();

        await _db.Tasks.AddRangeAsync(tasks);
        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.GetRecentTasksAsync(limit: 10);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(10);
    }

    [Fact]
    public async Task 取得最近任務_資料筆數少於Limit_應回傳所有資料()
    {
        // Arrange
        var tasks = Enumerable.Range(1, 3)
            .Select(i => CreateTask($"任務 {i}", createdAt: DateTime.UtcNow.AddMinutes(-i)))
            .ToList();

        await _db.Tasks.AddRangeAsync(tasks);
        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.GetRecentTasksAsync(limit: 10);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task 取得最近任務_資料庫無資料_應回傳空列表()
    {
        // Act
        var result = await _sut.GetRecentTasksAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task 取得最近任務_結果應依建立時間降冪排序()
    {
        // Arrange
        var base_time = DateTime.UtcNow;
        var oldest = CreateTask("最舊任務", createdAt: base_time.AddHours(-3));
        var middle = CreateTask("中間任務", createdAt: base_time.AddHours(-2));
        var newest = CreateTask("最新任務", createdAt: base_time.AddHours(-1));

        await _db.Tasks.AddRangeAsync(oldest, middle, newest);
        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.GetRecentTasksAsync();

        // Assert
        result[0].Title.Should().Be("最新任務");
        result[1].Title.Should().Be("中間任務");
        result[2].Title.Should().Be("最舊任務");
    }

    [Fact]
    public async Task 取得最近任務_任務有關聯專案與團隊_應正確對應名稱()
    {
        // Arrange
        var project = CreateProject("專案A");
        var team    = CreateTeam("團隊B");

        await _db.Projects.AddAsync(project);
        await _db.Teams.AddAsync(team);
        await _db.SaveChangesAsync();

        var task = CreateTask("有關聯任務", projectId: project.Id, teamId: team.Id);
        await _db.Tasks.AddAsync(task);
        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.GetRecentTasksAsync();

        // Assert
        result.Should().HaveCount(1);
        result[0].ProjectName.Should().Be("專案A");
        result[0].TeamName.Should().Be("團隊B");
    }

    [Fact]
    public async Task 取得最近任務_已完成任務_Duration應正確計算()
    {
        // Arrange
        var createdAt   = new DateTime(2024, 6, 1, 9, 0, 0, DateTimeKind.Utc);
        var completedAt = new DateTime(2024, 6, 1, 9, 45, 0, DateTimeKind.Utc);
        var expected    = completedAt - createdAt;

        var task = CreateTask("已完成任務", status: "Completed", createdAt: createdAt, completedAt: completedAt);
        await _db.Tasks.AddAsync(task);
        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.GetRecentTasksAsync();

        // Assert
        result.Should().HaveCount(1);
        result[0].Duration.Should().Be(expected);
    }

    [Fact]
    public async Task 取得最近任務_未完成任務_Duration應為null()
    {
        // Arrange
        var task = CreateTask("進行中任務", status: "Running");
        await _db.Tasks.AddAsync(task);
        await _db.SaveChangesAsync();

        // Act
        var result = await _sut.GetRecentTasksAsync();

        // Assert
        result.Should().HaveCount(1);
        result[0].Duration.Should().BeNull