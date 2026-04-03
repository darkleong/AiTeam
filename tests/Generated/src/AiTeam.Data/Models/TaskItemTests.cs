```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AiTeam.Data.Models;
using AiTeam.Shared.Services;
using FluentAssertions;
using NSubstitute;
using Xunit;
using TaskStatus = AiTeam.Data.Models.TaskStatus;

namespace AiTeam.Tests
{
    public class TaskItemTests
    {
        [Fact]
        public void TaskItem_建立新實例_預設值應正確()
        {
            // Arrange & Act
            var task = new TaskItem();

            // Assert
            task.Title.Should().Be(string.Empty);
            task.Status.Should().Be(TaskStatus.Todo);
            task.Priority.Should().Be(TaskPriority.Medium);
            task.Description.Should().BeNull();
            task.AssigneeId.Should().BeNull();
            task.ProjectId.Should().BeNull();
            task.DueDate.Should().BeNull();
            task.Tags.Should().BeNull();
        }

        [Fact]
        public void TaskItem_設定所有屬性_應正確儲存()
        {
            // Arrange
            var now = DateTime.UtcNow;
            var task = new TaskItem
            {
                Id = 1,
                Title = "測試任務",
                Description = "描述",
                Status = TaskStatus.InProgress,
                Priority = TaskPriority.High,
                AssigneeId = 10,
                ProjectId = 20,
                DueDate = now.AddDays(7),
                CreatedAt = now,
                UpdatedAt = now,
                Tags = "tag1,tag2"
            };

            // Assert
            task.Id.Should().Be(1);
            task.Title.Should().Be("測試任務");
            task.Description.Should().Be("描述");
            task.Status.Should().Be(TaskStatus.InProgress);
            task.Priority.Should().Be(TaskPriority.High);
            task.AssigneeId.Should().Be(10);
            task.ProjectId.Should().Be(20);
            task.DueDate.Should().BeCloseTo(now.AddDays(7), TimeSpan.FromSeconds(1));
            task.Tags.Should().Be("tag1,tag2");
        }
    }

    public class CsvExportServiceTests
    {
        private readonly CsvExportService _sut;

        public CsvExportServiceTests()
        {
            _sut = new CsvExportService();
        }

        #region ExportTasksToCsv

        [Fact]
        public void ExportTasksToCsv_空集合_應回傳只含標頭的CSV()
        {
            // Arrange
            var tasks = Enumerable.Empty<TaskItem>();

            // Act
            var result = _sut.ExportTasksToCsv(tasks);

            // Assert
            result.Should().NotBeNull();
            result.Length.Should().BeGreaterThan(0);

            var content = Encoding.UTF8.GetString(result);
            content.Should().Contain("編號");
            content.Should().Contain("標題");
            content.Should().Contain("狀態");
            content.Should().Contain("優先級");
        }

        [Fact]
        public void ExportTasksToCsv_單一任務_應包含正確的資料列()
        {
            // Arrange
            var task = new TaskItem
            {
                Id = 1,
                Title = "測試任務",
                Description = "描述內容",
                Status = TaskStatus.Todo,
                Priority = TaskPriority.Medium,
                CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc)
            };

            // Act
            var result = _sut.ExportTasksToCsv(new[] { task });

            // Assert
            var content = Encoding.UTF8.GetString(result);
            content.Should().Contain("1");
            content.Should().Contain("測試任務");
            content.Should().Contain("描述內容");
            content.Should().Contain("待處理");
            content.Should().Contain("中");
        }

        [Fact]
        public void ExportTasksToCsv_任務含有Assignee和Project_應包含對應名稱()
        {
            // Arrange
            var assignee = new AppUser { UserName = "john.doe" };
            var project = new Project { Name = "測試專案" };
            var task = new TaskItem
            {
                Id = 2,
                Title = "有負責人的任務",
                Status = TaskStatus.InProgress,
                Priority = TaskPriority.High,
                Assignee = assignee,
                Project = project,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            // Act
            var result = _sut.ExportTasksToCsv(new[] { task });

            // Assert
            var content = Encoding.UTF8.GetString(result);
            content.Should().Contain("john.doe");
            content.Should().Contain("測試專案");
            content.Should().Contain("進行中");
            content.Should().Contain("高");
        }

        [Fact]
        public void ExportTasksToCsv_任務含有截止日期_應格式化為正確日期格式()
        {
            // Arrange
            var dueDate = new DateTime(2024, 12, 31, 0, 0, 0, DateTimeKind.Utc);
            var task = new TaskItem
            {
                Id = 3,
                Title = "有截止日期的任務",
                Status = TaskStatus.Todo,
                Priority = TaskPriority.Low,
                DueDate = dueDate,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            // Act
            var result = _sut.ExportTasksToCsv(new[] { task });

            // Assert
            var content = Encoding.UTF8.GetString(result);
            content.Should().Contain("2024/12/31");
        }

        [Fact]
        public void ExportTasksToCsv_任務沒有截止日期_截止日期欄位應為空()
        {
            // Arrange
            var task = new TaskItem
            {
                Id = 4,
                Title = "無截止日期",
                Status = TaskStatus.Todo,
                Priority = TaskPriority.Low,
                DueDate = null,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            // Act
            var result = _sut.ExportTasksToCsv(new[] { task });

            // Assert
            var content = Encoding.UTF8.GetString(result);
            // 截止日期欄位應為空字串（連續逗號）
            content.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public void ExportTasksToCsv_標題含有逗號_應正確跳脫CSV欄位()
        {
            // Arrange
            var task = new TaskItem
            {
                Id = 5,
                Title = "任務,含有逗號",
                Status = TaskStatus.Todo,
                Priority = TaskPriority.Low,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            // Act
            var result = _sut.ExportTasksToCsv(new[] { task });

            // Assert
            var content = Encoding.UTF8.GetString(result);
            content.Should().Contain("\"任務,含有逗號\"");
        }

        [Fact]
        public void ExportTasksToCsv_標題含有雙引號_應正確跳脫CSV欄位()
        {
            // Arrange
            var task = new TaskItem
            {
                Id = 6,
                Title = "任務\"含有引號",
                Status = TaskStatus.Todo,
                Priority = TaskPriority.Low,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            // Act
            var result = _sut.ExportTasksToCsv(new[] { task });

            // Assert
            var content = Encoding.UTF8.GetString(result);
            content.Should().Contain("\"任務\"\"含有引號\"");
        }

        [Fact]
        public void ExportTasksToCsv_多筆任務_應包含所有任務資料()
        {
            // Arrange
            var tasks = new List<TaskItem>
            {
                new TaskItem { Id = 1, Title = "任務一", Status = TaskStatus.Todo, Priority = TaskPriority.Low, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                new TaskItem { Id = 2, Title = "任務二", Status = TaskStatus.Done, Priority = TaskPriority.High, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                new TaskItem { Id = 3, Title = "任務三", Status = TaskStatus.Cancelled, Priority = TaskPriority.Critical, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
            };

            // Act
            var result = _sut.ExportTasksToCsv(tasks);

            // Assert
            var content = Encoding.UTF8.GetString(result);
            content.Should().Contain("任務一");
            content.Should().Contain("任務二");
            content.Should().Contain("任務三");
            content.Should().Contain("已完成");
            content.Should().Contain("已取消");
            content.Should().Contain("緊急");
        }

        [Fact]
        public void ExportTasksToCsv_回傳結果_應包含UTF8BOM()
        {
            // Arrange
            var tasks = Enumerable.Empty<TaskItem>();

            // Act
            var result = _sut.ExportTasksToCsv(tasks);

            // Assert
            // UTF-8 BOM 為 EF BB BF
            result.Should().StartWith(new byte[] { 0xEF, 0xBB, 0xBF });
        }

        [Fact]
        public void ExportTasksToCsv_所有狀態_應正確翻譯為中文()
        {
            // Arrange
            var tasks = new List<TaskItem>
            {
                new TaskItem { Id = 1, Title = "T1", Status = TaskStatus.Todo, Priority = TaskPriority.Medium, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                new TaskItem { Id = 2, Title = "T2", Status = TaskStatus.InProgress, Priority = TaskPriority.Medium, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                new TaskItem { Id = 3, Title = "T3", Status = TaskStatus.Review, Priority = TaskPriority.Medium, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                new TaskItem { Id = 4, Title = "T4", Status = TaskStatus.Done, Priority = TaskPriority.Medium, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                new TaskItem { Id = 5, Title = "T5", Status = TaskStatus.Cancelled, Priority = TaskPriority.Medium, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
            };

            // Act
            var result = _sut.ExportTasksToCsv(tasks);

            // Assert
            var content = Encoding.UTF8.GetString(result);
            content.Should().Contain("待處理");
            content.Should().Contain("進行中");
            content.Should().Contain("審核中");
            content.Should().Contain("已完成");
            content.Should().Contain("已取消");
        }

        [Fact]
        public void ExportTasksToCsv_所有優先級_應正確翻譯為中文()
        {
            // Arrange
            var tasks = new List<TaskItem>
            {
                new TaskItem { Id = 1, Title = "T1", Status = TaskStatus.Todo, Priority = TaskPriority.Low, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                new TaskItem { Id = 2, Title = "T2", Status = TaskStatus.Todo, Priority = TaskPriority.Medium, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                new TaskItem { Id = 3, Title = "T3", Status = TaskStatus.Todo, Priority = TaskPriority.High, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                new TaskItem { Id = 4, Title = "T4", Status = TaskStatus.Todo, Priority = TaskPriority.Critical, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
            };

            // Act
            var result = _sut.ExportTasksToCsv(tasks);

            // Assert
            var content = Encoding.UTF8.GetString(result);
            content.Should().Contain("低");
            content.Should().Contain("中");
            content.Should().Contain("高");
            content.Should().Contain("緊急");
        }

        [Fact]
        public void ExportTasksToCsv_描述含有換行符號_應正確跳脫CSV欄位()
        {
            // Arrange
            var task = new TaskItem
            {
                Id = 7,
                Title = "換行測試",
                Description = "第一行\n第二行",
                Status = TaskStatus.Todo,
                Priority = TaskPriority.Low,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            // Act
            var result = _sut.ExportTasksToCsv(new[] { task });

            // Assert
            var content = Encoding.UTF8.GetString(result);
            content.Should().Contain("\"第一行\n第二行\"");
        }

        #endregion

        #region GetTasksCsvFileName

        [Fact]
        public void GetTasksCsvFileName_呼叫方法_應回傳含tasks前綴的檔名()
        {
            // Act
            var result = _sut.GetTasksCsvFileName();

            // Assert
            result.Should().StartWith("tasks_");
            result.Should().EndWith(".csv");
        }

        [Fact]
        public void GetTasksCsvFileName_呼叫方法_檔名應包含日期時間格式()
        {
            // Act
            var result = _sut.GetTasksCsvFileName();

            // Assert
            // 格式為 tasks_yyyyMMdd_HHmmss.csv，總長度 = 6 + 8 + 1 + 6 + 4 = 25
            result.Length.Should().Be(25);
            result.Should().MatchRegex(@"^tasks_\d{8}_\d{6}\.csv$");
        }

        [Fact]
        public void GetTasksCsvFileName_連續呼叫兩次_檔名應不同或相同但格式正確()
        {