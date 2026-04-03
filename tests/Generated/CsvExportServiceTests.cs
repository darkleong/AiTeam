
```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using AiTeam.Shared.Models;
using AiTeam.Shared.Services;
using Xunit;

namespace AiTeam.Tests.Generated
{
    public class CsvExportServiceTests
    {
        private readonly ICsvExportService _csvExportService;

        public CsvExportServiceTests()
        {
            _csvExportService = new CsvExportService();
        }

        [Fact]
        public void ExportToCsv_WithEmptyList_ReturnsOnlyHeader()
        {
            // Arrange
            var tasks = new List<TaskItem>();

            // Act
            var result = _csvExportService.ExportToCsv(tasks);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Length > 0);
            
            // Check BOM
            Assert.Equal(0xEF, result[0]);
            Assert.Equal(0xBB, result[1]);
            Assert.Equal(0xBF, result[2]);
            
            // Check header content
            var content = Encoding.UTF8.GetString(result, 3, result.Length - 3);
            Assert.Contains("任務ID", content);
            Assert.Contains("任務名稱", content);
            Assert.Contains("狀態", content);
            Assert.Contains("優先級", content);
            Assert.Contains("負責人", content);
            Assert.Contains("建立日期", content);
            Assert.Contains("截止日期", content);
            Assert.Contains("描述", content);
        }

        [Fact]
        public void ExportToCsv_WithSingleTask_ReturnsHeaderAndOneRow()
        {
            // Arrange
            var tasks = new List<TaskItem>
            {
                new TaskItem
                {
                    Id = "TASK-001",
                    Title = "測試任務",
                    Status = "進行中",
                    Priority = "高",
                    Assignee = "張三",
                    CreatedDate = new DateTime(2024, 1, 15),
                    DueDate = new DateTime(2024, 2, 15),
                    Description = "這是一個測試任務"
                }
            };

            // Act
            var result = _csvExportService.ExportToCsv(tasks);

            // Assert
            Assert.NotNull(result);
            var content = Encoding.UTF8.GetString(result, 3, result.Length - 3);
            var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            
            Assert.Equal(2, lines.Length); // Header + 1 data row
            Assert.Contains("TASK-001", lines[1]);
            Assert.Contains("測試任務", lines[1]);
            Assert.Contains("進行中", lines[1]);
            Assert.Contains("高", lines[1]);
            Assert.Contains("張三", lines[1]);
        }

        [Fact]
        public void ExportToCsv_WithMultipleTasks_ReturnsCorrectRowCount()
        {
            // Arrange
            var tasks = new List<TaskItem>
            {
                new TaskItem
                {
                    Id = "TASK-001",
                    Title = "任務一",
                    Status = "待處理",
                    Priority = "中",
                    Assignee = "李四",
                    CreatedDate = new DateTime(2024, 1, 1),
                    DueDate = new DateTime(2024, 1, 31)
                },
                new TaskItem
                {
                    Id = "TASK-002",
                    Title = "任務二",
                    Status = "已完成",
                    Priority = "低",
                    Assignee = "王五",
                    CreatedDate = new DateTime(2024, 1, 5),
                    DueDate = new DateTime(2024, 2, 5)
                },
                new TaskItem
                {
                    Id = "TASK-003",
                    Title = "任務三",
                    Status = "進行中",
                    Priority = "高",
                    Assignee = "趙六",
                    CreatedDate = new DateTime(2024, 1, 10),
                    DueDate = new DateTime(2024, 2, 10)
                }
            };

            // Act
            var result = _csvExportService.ExportToCsv(tasks);

            // Assert
            var content = Encoding.UTF8.GetString(result, 3, result.Length - 3);
            var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            
            Assert.Equal(4, lines.Length); // Header + 3 data rows
        }

        [Fact]
        public void ExportToCsv_WithSpecialCharacters_EscapesCorrectly()
        {
            // Arrange
            var tasks = new List<TaskItem>
            {
                new TaskItem
                {
                    Id = "TASK-001",
                    Title = "任務含,逗號",
                    Status = "待處理",
                    Priority = "中",
                    Assignee = "測試者",
                    CreatedDate = new DateTime(2024, 1, 1),
                    DueDate = new DateTime(2024, 1, 31),
                    Description = "描述含\"引號"
                }
            };

            // Act
            var result = _csvExportService.ExportToCsv(tasks);

            // Assert
            var content = Encoding.UTF8.GetString(result, 3, result.Length - 3);
            
            // Fields with commas should be quoted
            Assert.Contains("\"任務含,逗號\"", content);
            // Fields with quotes should have escaped quotes
            Assert.Contains("\"描述含\"\"引號\"", content);
        }

        [Fact]
        public void ExportToCsv_WithNewlineInDescription_EscapesCorrectly()
        {
            // Arrange
            var tasks = new List<TaskItem>
            {
                new TaskItem
                {
                    Id = "TASK-001",
                    Title = "測試任務",
                    Status = "待處理",
                    Priority = "中",
                    Assignee = "測試者",
                    CreatedDate = new DateTime(2024, 1, 1),
                    DueDate = new DateTime(2024, 1, 31),
                    Description = "第一行\n第二行"
                }
            };

            // Act
            var result = _csvExportService.ExportToCsv(tasks);

            // Assert
            var content = Encoding.UTF8.GetString(result, 3, result.Length - 3);
            
            // Fields with newlines should be quoted
            Assert.Contains("\"第一行\n第二行\"", content);
        }

        [Fact]
        public void ExportToCsv_HasBomPrefix()
        {
            // Arrange
            var tasks = new List<TaskItem>();

            // Act
            var result = _csvExportService.ExportToCsv(tasks);

            // Assert - UTF-8 BOM: EF BB BF
            Assert.True(result.Length >= 3);
            Assert.Equal(0xEF, result[0]);
            Assert.Equal(0xBB, result[1]);
            Assert.Equal(0xBF, result[2]);
        }

        [Fact]
        public void ExportToCsv_WithNullDueDate_HandlesGracefully()
        {
            // Arrange
            var tasks = new List<TaskItem>
            {
                new TaskItem
                {
                    Id = "TASK-001",
                    Title = "無截止日期任務",
                    Status = "待處理",
                    Priority = "中",
                    Assignee = "測試者",
                    CreatedDate = new DateTime(2024, 1, 1),
                    DueDate = null,
                    Description = "此任務沒有截止日期"
                }
            };

            // Act
            var result = _csvExportService.ExportToCsv(tasks);

            // Assert
            Assert.NotNull(result);
            var content = Encoding.UTF8.GetString(result, 3, result.Length - 3);
            Assert.Contains("TASK-001", content);
            Assert.Contains("無截止日期任務", content);
        }

        [Fact]
        public void ExportToCsv_WithNullDescription_HandlesGracefully()
        {
            // Arrange
            var tasks = new List<TaskItem>
            {
                new TaskItem
                {
                    Id = "TASK-001",
                    Title = "無描述任務",
                    Status = "待處理",
                    Priority = "中",
                    Assignee = "測試者",
                    CreatedDate = new DateTime(2024, 1, 1),
                    DueDate = new DateTime(2024, 1, 31),
                    Description = null
                }
            };

            // Act
            var result = _csvExportService.ExportToCsv(tasks);

            // Assert
            Assert.NotNull(result);
            var content = Encoding.UTF8.GetString(result, 3, result.Length - 3);
            Assert.Contains("TASK-001", content);
        }

        [Fact]
        public void ExportToCsv_DateFormat_IsCorrect()
        {
            // Arrange
            var tasks = new List<TaskItem>
            {
                new TaskItem
                {
                    Id = "TASK-001",
                    Title = "日期格式測試",
                    Status = "待處理",
                    Priority = "中",
                    Assignee = "測試者",
                    CreatedDate = new DateTime(2024, 3, 15),
                    DueDate = new DateTime(2024, 4, 20)
                }
            };

            // Act
            var result = _csvExportService.ExportToCsv(tasks);

            // Assert
            var content = Encoding.UTF8.GetString(result, 3, result.Length - 3);
            Assert.Contains("2024-03-15", content);
            Assert.Contains("2024-04-20", content);
        }

        [Fact]
        public void ExportToCsv_NullInput_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => _csvExportService.ExportToCsv(null));
        }

        [Fact]
        public void GetFileName_ReturnsValidFileName()
        {
            // Act
            var fileName = _csvExportService.GetFileName();

            // Assert
            Assert.NotNull(fileName);
            Assert.NotEmpty(fileName);
            Assert.EndsWith(".csv", fileName);
            Assert.Contains("tasks", fileName.ToLower());
        }

        [Fact]
        public void GetFileName_ContainsCurrentDate()
        {
            // Act
            var fileName = _csvExportService.GetFileName();
            var today = DateTime.Now.ToString("yyyyMMdd");

            // Assert
            Assert.Contains(today, fileName);
        }
    }
}
```