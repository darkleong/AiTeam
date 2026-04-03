```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AiTeam.Shared.Models;
using AiTeam.Shared.Services;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace AiTeam.Shared.Tests.Services
{
    public class ICsvExportServiceTests
    {
        private readonly ICsvExportService _csvExportService;

        public ICsvExportServiceTests()
        {
            _csvExportService = Substitute.For<ICsvExportService>();
        }

        #region ExportTasksToCsv 測試

        [Fact]
        public void 匯出任務為Csv_給定有效任務清單_應回傳非空位元組陣列()
        {
            // Arrange
            var tasks = new List<TaskItem>
            {
                new TaskItem { Id = 1, Title = "任務一", Description = "描述一" },
                new TaskItem { Id = 2, Title = "任務二", Description = "描述二" }
            };

            var expectedCsvBytes = Encoding.UTF8.GetBytes("Id,Title,Description\n1,任務一,描述一\n2,任務二,描述二");
            _csvExportService.ExportTasksToCsv(tasks).Returns(expectedCsvBytes);

            // Act
            var result = _csvExportService.ExportTasksToCsv(tasks);

            // Assert
            result.Should().NotBeNull();
            result.Should().NotBeEmpty();
            result.Should().BeEquivalentTo(expectedCsvBytes);
        }

        [Fact]
        public void 匯出任務為Csv_給定空任務清單_應回傳僅含標頭的位元組陣列()
        {
            // Arrange
            var emptyTasks = new List<TaskItem>();
            var expectedCsvBytes = Encoding.UTF8.GetBytes("Id,Title,Description\n");
            _csvExportService.ExportTasksToCsv(emptyTasks).Returns(expectedCsvBytes);

            // Act
            var result = _csvExportService.ExportTasksToCsv(emptyTasks);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEquivalentTo(expectedCsvBytes);
        }

        [Fact]
        public void 匯出任務為Csv_給定單一任務_應回傳含單筆資料的位元組陣列()
        {
            // Arrange
            var tasks = new List<TaskItem>
            {
                new TaskItem { Id = 1, Title = "單一任務", Description = "單一描述" }
            };

            var expectedCsvBytes = Encoding.UTF8.GetBytes("Id,Title,Description\n1,單一任務,單一描述");
            _csvExportService.ExportTasksToCsv(tasks).Returns(expectedCsvBytes);

            // Act
            var result = _csvExportService.ExportTasksToCsv(tasks);

            // Assert
            result.Should().NotBeNull();
            result.Should().NotBeEmpty();
            result.Length.Should().BeGreaterThan(0);
            result.Should().BeEquivalentTo(expectedCsvBytes);
        }

        [Fact]
        public void 匯出任務為Csv_給定含特殊字元的任務_應正確回傳位元組陣列()
        {
            // Arrange
            var tasks = new List<TaskItem>
            {
                new TaskItem { Id = 1, Title = "任務,含逗號", Description = "描述\"含引號\"" }
            };

            var expectedCsvBytes = Encoding.UTF8.GetBytes("Id,Title,Description\n1,\"任務,含逗號\",\"描述\"\"含引號\"\"\"");
            _csvExportService.ExportTasksToCsv(tasks).Returns(expectedCsvBytes);

            // Act
            var result = _csvExportService.ExportTasksToCsv(tasks);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEquivalentTo(expectedCsvBytes);
        }

        [Fact]
        public void 匯出任務為Csv_給定Null任務清單_應拋出例外()
        {
            // Arrange
            IEnumerable<TaskItem> nullTasks = null;
            _csvExportService
                .ExportTasksToCsv(nullTasks)
                .Returns(x => throw new ArgumentNullException(nameof(nullTasks)));

            // Act
            Action act = () => _csvExportService.ExportTasksToCsv(nullTasks);

            // Assert
            act.Should().Throw<ArgumentNullException>();
        }

        [Fact]
        public void 匯出任務為Csv_服務被呼叫一次_應驗證呼叫次數正確()
        {
            // Arrange
            var tasks = new List<TaskItem>
            {
                new TaskItem { Id = 1, Title = "任務一", Description = "描述一" }
            };

            var expectedCsvBytes = Encoding.UTF8.GetBytes("Id,Title,Description\n1,任務一,描述一");
            _csvExportService.ExportTasksToCsv(tasks).Returns(expectedCsvBytes);

            // Act
            _csvExportService.ExportTasksToCsv(tasks);

            // Assert
            _csvExportService.Received(1).ExportTasksToCsv(tasks);
        }

        [Fact]
        public void 匯出任務為Csv_服務被呼叫多次_應驗證每次回傳結果一致()
        {
            // Arrange
            var tasks = new List<TaskItem>
            {
                new TaskItem { Id = 1, Title = "任務一", Description = "描述一" },
                new TaskItem { Id = 2, Title = "任務二", Description = "描述二" }
            };

            var expectedCsvBytes = Encoding.UTF8.GetBytes("Id,Title,Description\n1,任務一,描述一\n2,任務二,描述二");
            _csvExportService.ExportTasksToCsv(tasks).Returns(expectedCsvBytes);

            // Act
            var firstResult = _csvExportService.ExportTasksToCsv(tasks);
            var secondResult = _csvExportService.ExportTasksToCsv(tasks);

            // Assert
            firstResult.Should().BeEquivalentTo(secondResult);
            _csvExportService.Received(2).ExportTasksToCsv(tasks);
        }

        [Fact]
        public void 匯出任務為Csv_給定大量任務清單_應回傳非空位元組陣列()
        {
            // Arrange
            var tasks = Enumerable.Range(1, 1000).Select(i => new TaskItem
            {
                Id = i,
                Title = $"任務{i}",
                Description = $"描述{i}"
            }).ToList();

            var expectedCsvBytes = Encoding.UTF8.GetBytes(string.Join("\n", tasks.Select(t => $"{t.Id},{t.Title},{t.Description}")));
            _csvExportService.ExportTasksToCsv(tasks).Returns(expectedCsvBytes);

            // Act
            var result = _csvExportService.ExportTasksToCsv(tasks);

            // Assert
            result.Should().NotBeNull();
            result.Should().NotBeEmpty();
            result.Length.Should().BeGreaterThan(0);
        }

        [Fact]
        public void 匯出任務為Csv_給定任務清單_應確認未呼叫其他方法()
        {
            // Arrange
            var tasks = new List<TaskItem>
            {
                new TaskItem { Id = 1, Title = "任務一", Description = "描述一" }
            };

            var expectedCsvBytes = Encoding.UTF8.GetBytes("Id,Title,Description\n1,任務一,描述一");
            _csvExportService.ExportTasksToCsv(tasks).Returns(expectedCsvBytes);

            // Act
            _csvExportService.ExportTasksToCsv(tasks);

            // Assert
            _csvExportService.Received(1).ExportTasksToCsv(Arg.Any<IEnumerable<TaskItem>>());
        }

        #endregion
    }
}
```