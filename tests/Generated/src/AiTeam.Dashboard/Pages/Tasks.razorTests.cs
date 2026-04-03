```csharp
using System.Text;
using AiTeam.Shared.Models;
using AiTeam.Shared.Services;
using FluentAssertions;
using Xunit;

namespace AiTeam.Tests.Services;

public class CsvExportServiceTests
{
    private readonly CsvExportService _sut;

    public CsvExportServiceTests()
    {
        _sut = new CsvExportService();
    }

    #region ExportTasksToCsv - Happy Path

    [Fact]
    public void ExportTasksToCsv_給定正常任務清單_應回傳包含BOM的UTF8位元組陣列()
    {
        // Arrange
        var tasks = new List<TaskItem>
        {
            new TaskItem
            {
                Id = 1,
                Title = "測試任務",
                Status = "進行中",
                Priority = "高",
                Assignee = "張三",
                Project = "專案A",
                CreatedAt = new DateTime(2024, 1, 15),
                DueDate = new DateTime(2024, 2, 15),
                CompletedAt = null,
                Description = "這是測試描述"
            }
        };

        // Act
        var result = _sut.ExportTasksToCsv(tasks);

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBeEmpty();

        var bom = Encoding.UTF8.GetPreamble();
        result.Take(bom.Length).Should().BeEquivalentTo(bom);
    }

    [Fact]
    public void ExportTasksToCsv_給定正常任務清單_應包含正確的標頭列()
    {
        // Arrange
        var tasks = new List<TaskItem>
        {
            new TaskItem
            {
                Id = 1,
                Title = "任務一",
                Status = "待處理",
                Priority = "中",
                Assignee = "李四",
                Project = "專案B",
                CreatedAt = new DateTime(2024, 3, 1),
                DueDate = null,
                CompletedAt = null,
                Description = "描述"
            }
        };

        // Act
        var result = _sut.ExportTasksToCsv(tasks);
        var content = Encoding.UTF8.GetString(result).TrimStart('\uFEFF');
        var lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

        // Assert
        lines.Should().NotBeEmpty();
        var headerLine = lines[0];
        headerLine.Should().Contain("任務編號");
        headerLine.Should().Contain("任務名稱");
        headerLine.Should().Contain("狀態");
        headerLine.Should().Contain("優先級");
        headerLine.Should().Contain("負責人");
        headerLine.Should().Contain("專案");
        headerLine.Should().Contain("建立日期");
        headerLine.Should().Contain("截止日期");
        headerLine.Should().Contain("完成日期");
        headerLine.Should().Contain("描述");
    }

    [Fact]
    public void ExportTasksToCsv_給定正常任務清單_應包含正確的資料列內容()
    {
        // Arrange
        var tasks = new List<TaskItem>
        {
            new TaskItem
            {
                Id = 42,
                Title = "重要任務",
                Status = "進行中",
                Priority = "高",
                Assignee = "王五",
                Project = "專案C",
                CreatedAt = new DateTime(2024, 5, 10),
                DueDate = new DateTime(2024, 6, 30),
                CompletedAt = new DateTime(2024, 6, 25),
                Description = "重要任務描述"
            }
        };

        // Act
        var result = _sut.ExportTasksToCsv(tasks);
        var content = Encoding.UTF8.GetString(result).TrimStart('\uFEFF');
        var lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

        // Assert
        lines.Should().HaveCountGreaterThanOrEqualTo(2);
        var dataLine = lines[1];
        dataLine.Should().Contain("42");
        dataLine.Should().Contain("重要任務");
        dataLine.Should().Contain("進行中");
        dataLine.Should().Contain("高");
        dataLine.Should().Contain("王五");
        dataLine.Should().Contain("專案C");
        dataLine.Should().Contain("2024-05-10");
        dataLine.Should().Contain("2024-06-30");
        dataLine.Should().Contain("2024-06-25");
        dataLine.Should().Contain("重要任務描述");
    }

    [Fact]
    public void ExportTasksToCsv_給定多筆任務_應包含所有資料列()
    {
        // Arrange
        var tasks = new List<TaskItem>
        {
            new TaskItem { Id = 1, Title = "任務一", CreatedAt = new DateTime(2024, 1, 1) },
            new TaskItem { Id = 2, Title = "任務二", CreatedAt = new DateTime(2024, 1, 2) },
            new TaskItem { Id = 3, Title = "任務三", CreatedAt = new DateTime(2024, 1, 3) }
        };

        // Act
        var result = _sut.ExportTasksToCsv(tasks);
        var content = Encoding.UTF8.GetString(result).TrimStart('\uFEFF');
        var lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

        // Assert
        // 1 header + 3 data rows
        lines.Should().HaveCount(4);
    }

    [Fact]
    public void ExportTasksToCsv_截止日期為null_應輸出空字串()
    {
        // Arrange
        var tasks = new List<TaskItem>
        {
            new TaskItem
            {
                Id = 1,
                Title = "無截止日期任務",
                CreatedAt = new DateTime(2024, 1, 1),
                DueDate = null,
                CompletedAt = null
            }
        };

        // Act
        var result = _sut.ExportTasksToCsv(tasks);
        var content = Encoding.UTF8.GetString(result).TrimStart('\uFEFF');
        var lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

        // Assert
        lines.Should().HaveCountGreaterThanOrEqualTo(2);
        var dataLine = lines[1];
        // DueDate and CompletedAt should be empty (consecutive commas)
        dataLine.Should().Contain(",,");
    }

    [Fact]
    public void ExportTasksToCsv_完成日期有值_應格式化為yyyy_MM_dd()
    {
        // Arrange
        var tasks = new List<TaskItem>
        {
            new TaskItem
            {
                Id = 1,
                Title = "已完成任務",
                CreatedAt = new DateTime(2024, 1, 1),
                CompletedAt = new DateTime(2024, 12, 31)
            }
        };

        // Act
        var result = _sut.ExportTasksToCsv(tasks);
        var content = Encoding.UTF8.GetString(result).TrimStart('\uFEFF');

        // Assert
        content.Should().Contain("2024-12-31");
    }

    #endregion

    #region ExportTasksToCsv - Edge Cases

    [Fact]
    public void ExportTasksToCsv_給定空集合_應只有標頭列()
    {
        // Arrange
        var tasks = new List<TaskItem>();

        // Act
        var result = _sut.ExportTasksToCsv(tasks);
        var content = Encoding.UTF8.GetString(result).TrimStart('\uFEFF');
        var lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

        // Assert
        result.Should().NotBeNull();
        lines.Should().HaveCount(1);
        lines[0].Should().Contain("任務編號");
    }

    [Fact]
    public void ExportTasksToCsv_欄位包含逗號_應以雙引號包覆()
    {
        // Arrange
        var tasks = new List<TaskItem>
        {
            new TaskItem
            {
                Id = 1,
                Title = "任務,含逗號",
                CreatedAt = new DateTime(2024, 1, 1)
            }
        };

        // Act
        var result = _sut.ExportTasksToCsv(tasks);
        var content = Encoding.UTF8.GetString(result).TrimStart('\uFEFF');

        // Assert
        content.Should().Contain("\"任務,含逗號\"");
    }

    [Fact]
    public void ExportTasksToCsv_欄位包含雙引號_應以雙引號跳脫()
    {
        // Arrange
        var tasks = new List<TaskItem>
        {
            new TaskItem
            {
                Id = 1,
                Title = "任務\"含引號\"",
                CreatedAt = new DateTime(2024, 1, 1)
            }
        };

        // Act
        var result = _sut.ExportTasksToCsv(tasks);
        var content = Encoding.UTF8.GetString(result).TrimStart('\uFEFF');

        // Assert
        content.Should().Contain("\"任務\"\"含引號\"\"\"");
    }

    [Fact]
    public void ExportTasksToCsv_欄位包含換行符號_應以雙引號包覆()
    {
        // Arrange
        var tasks = new List<TaskItem>
        {
            new TaskItem
            {
                Id = 1,
                Title = "任務\n換行",
                CreatedAt = new DateTime(2024, 1, 1)
            }
        };

        // Act
        var result = _sut.ExportTasksToCsv(tasks);
        var content = Encoding.UTF8.GetString(result).TrimStart('\uFEFF');

        // Assert
        content.Should().Contain("\"任務\n換行\"");
    }

    [Fact]
    public void ExportTasksToCsv_欄位包含回車符號_應以雙引號包覆()
    {
        // Arrange
        var tasks = new List<TaskItem>
        {
            new TaskItem
            {
                Id = 1,
                Title = "任務\r回車",
                CreatedAt = new DateTime(2024, 1, 1)
            }
        };

        // Act
        var result = _sut.ExportTasksToCsv(tasks);
        var content = Encoding.UTF8.GetString(result).TrimStart('\uFEFF');

        // Assert
        content.Should().Contain("\"任務\r回車\"");
    }

    [Fact]
    public void ExportTasksToCsv_所有可空字串欄位為null_應輸出空字串而非null文字()
    {
        // Arrange
        var tasks = new List<TaskItem>
        {
            new TaskItem
            {
                Id = 1,
                Title = null,
                Status = null,
                Priority = null,
                Assignee = null,
                Project = null,
                Description = null,
                CreatedAt = new DateTime(2024, 1, 1),
                DueDate = null,
                CompletedAt = null
            }
        };

        // Act
        var result = _sut.ExportTasksToCsv(tasks);
        var content = Encoding.UTF8.GetString(result).TrimStart('\uFEFF');

        // Assert
        content.Should().NotContain("null");
        content.Should().NotContain("NULL");
    }

    [Fact]
    public void ExportTasksToCsv_回傳位元組陣列_應以UTF8_BOM開頭()
    {
        // Arrange
        var tasks = new List<TaskItem>
        {
            new TaskItem { Id = 1, CreatedAt = DateTime.Now }
        };
        var expectedBom = new byte[] { 0xEF, 0xBB, 0xBF };

        // Act
        var result = _sut.ExportTasksToCsv(tasks);

        // Assert
        result.Should().StartWith(expectedBom);
    }

    [Fact]
    public void ExportTasksToCsv_欄位不包含特殊字元_不應加雙引號()
    {
        // Arrange
        var tasks = new List<TaskItem>
        {
            new TaskItem
            {
                Id = 1,
                Title = "普通任務",
                Status = "進行中",
                CreatedAt = new DateTime(2024, 1, 1)
            }
        };

        // Act
        var result = _sut.ExportTasksToCsv(tasks);
        var content = Encoding.UTF8.GetString(result).TrimStart('\uFEFF');
        var lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

        // Assert
        lines.Should().HaveCountGreaterThanOrEqualTo(2);
        var dataLine = lines[1];
        dataLine.Should().Contain("普通任務");
        dataLine.Should().NotContain("\"普通任務\"");
    }

    [Fact]
    public void ExportTasksToCsv_標頭列有十個欄位_應正確對應所有定義欄位()
    {
        // Arrange
        var tasks = new List<TaskItem>();

        // Act
        var result = _sut.ExportTasksToCsv(tasks);
        var content = Encoding.UTF8.GetString(result).TrimStart('\uFEFF');
        var lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

        // Assert
        lines.Should().HaveCount(1);
        var headerFields = lines[0].Split(',');
        headerFields.Should().HaveCount(10);
    }

    [Fact]
    public void ExportTasksToCsv_建立日期_應格式化為yyyy_MM_dd()
    {
        // Arrange
        var tasks = new List<TaskItem>
        {
            new TaskItem
            {
                Id = 1,
                CreatedAt = new DateTime(2024, 7, 4)
            }
        };

        // Act
        var result = _sut.ExportTasksToCsv(tasks);
        var content = Encoding.UTF8.GetString(result).TrimStart('\uFEFF');

        // Assert
        content.Should().Contain("2024-07-04");
    }

    [Fact]
    public void ExportTasksToCsv_同時包含逗號與雙引號_應正確跳脫()
    {
        // Arrange
        var tasks = new List<TaskItem>
        {
            new TaskItem
            {
                Id = 1,
                Title = "任務,\"特殊\"",
                CreatedAt = new DateTime(2024, 