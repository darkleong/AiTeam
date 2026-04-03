```csharp
using System.Text;
using AiTeam.Shared.Services;
using FluentAssertions;
using Xunit;

namespace AiTeam.Shared.Tests.Services;

public class CsvExportServiceTests
{
    private readonly CsvExportService _sut;
    private static readonly byte[] Utf8Bom = { 0xEF, 0xBB, 0xBF };

    public CsvExportServiceTests()
    {
        _sut = new CsvExportService();
    }

    #region ExportToCsv<T> 測試

    [Fact]
    public void ExportToCsv_正常資料與欄位定義_應回傳含BOM的UTF8位元組陣列()
    {
        // Arrange
        var data = new List<string> { "測試資料" };
        var columns = new Dictionary<string, Func<string, string>>
        {
            ["欄位一"] = s => s
        };

        // Act
        var result = _sut.ExportToCsv(data, columns);

        // Assert
        result.Should().NotBeNull();
        result.Length.Should().BeGreaterThan(Utf8Bom.Length);
        result[0].Should().Be(0xEF);
        result[1].Should().Be(0xBB);
        result[2].Should().Be(0xBF);
    }

    [Fact]
    public void ExportToCsv_正常資料與欄位定義_應包含正確的標頭列()
    {
        // Arrange
        var data = new List<string> { "值一" };
        var columns = new Dictionary<string, Func<string, string>>
        {
            ["標頭A"] = s => s,
            ["標頭B"] = s => s.ToUpper()
        };

        // Act
        var result = _sut.ExportToCsv(data, columns);
        var content = GetContentWithoutBom(result);
        var lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

        // Assert
        lines[0].Should().Be("標頭A,標頭B");
    }

    [Fact]
    public void ExportToCsv_正常資料_應包含正確的資料列()
    {
        // Arrange
        var data = new List<string> { "Hello" };
        var columns = new Dictionary<string, Func<string, string>>
        {
            ["原始值"] = s => s,
            ["大寫值"] = s => s.ToUpper()
        };

        // Act
        var result = _sut.ExportToCsv(data, columns);
        var content = GetContentWithoutBom(result);
        var lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

        // Assert
        lines.Should().HaveCount(2);
        lines[1].Should().Be("Hello,HELLO");
    }

    [Fact]
    public void ExportToCsv_空資料集合_應只包含標頭列()
    {
        // Arrange
        var data = new List<string>();
        var columns = new Dictionary<string, Func<string, string>>
        {
            ["欄位一"] = s => s
        };

        // Act
        var result = _sut.ExportToCsv(data, columns);
        var content = GetContentWithoutBom(result);
        var lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

        // Assert
        lines.Should().HaveCount(1);
        lines[0].Should().Be("欄位一");
    }

    [Fact]
    public void ExportToCsv_多筆資料_應包含多個資料列()
    {
        // Arrange
        var data = new List<string> { "資料一", "資料二", "資料三" };
        var columns = new Dictionary<string, Func<string, string>>
        {
            ["值"] = s => s
        };

        // Act
        var result = _sut.ExportToCsv(data, columns);
        var content = GetContentWithoutBom(result);
        var lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

        // Assert
        lines.Should().HaveCount(4);
        lines[1].Should().Be("資料一");
        lines[2].Should().Be("資料二");
        lines[3].Should().Be("資料三");
    }

    [Fact]
    public void ExportToCsv_欄位包含逗號_應自動以雙引號包裹()
    {
        // Arrange
        var data = new List<string> { "值,含逗號" };
        var columns = new Dictionary<string, Func<string, string>>
        {
            ["欄位"] = s => s
        };

        // Act
        var result = _sut.ExportToCsv(data, columns);
        var content = GetContentWithoutBom(result);
        var lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

        // Assert
        lines[1].Should().Be("\"值,含逗號\"");
    }

    [Fact]
    public void ExportToCsv_欄位包含雙引號_應轉義雙引號()
    {
        // Arrange
        var data = new List<string> { "值\"含引號\"" };
        var columns = new Dictionary<string, Func<string, string>>
        {
            ["欄位"] = s => s
        };

        // Act
        var result = _sut.ExportToCsv(data, columns);
        var content = GetContentWithoutBom(result);
        var lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

        // Assert
        lines[1].Should().Be("\"值\"\"含引號\"\"\"");
    }

    [Fact]
    public void ExportToCsv_欄位包含換行符號_應自動以雙引號包裹()
    {
        // Arrange
        var data = new List<string> { "值\n含換行" };
        var columns = new Dictionary<string, Func<string, string>>
        {
            ["欄位"] = s => s
        };

        // Act
        var result = _sut.ExportToCsv(data, columns);
        var content = GetContentWithoutBom(result);

        // Assert
        content.Should().Contain("\"值\n含換行\"");
    }

    [Fact]
    public void ExportToCsv_欄位為空字串_應回傳空欄位()
    {
        // Arrange
        var data = new List<string> { "" };
        var columns = new Dictionary<string, Func<string, string>>
        {
            ["欄位"] = s => s
        };

        // Act
        var result = _sut.ExportToCsv(data, columns);
        var content = GetContentWithoutBom(result);
        var lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

        // Assert
        lines[1].Should().Be(string.Empty);
    }

    [Fact]
    public void ExportToCsv_標頭含逗號_應自動以雙引號包裹標頭()
    {
        // Arrange
        var data = new List<string> { "資料" };
        var columns = new Dictionary<string, Func<string, string>>
        {
            ["標頭,含逗號"] = s => s
        };

        // Act
        var result = _sut.ExportToCsv(data, columns);
        var content = GetContentWithoutBom(result);
        var lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

        // Assert
        lines[0].Should().Be("\"標頭,含逗號\"");
    }

    [Fact]
    public void ExportToCsv_回傳位元組前三碼_應為UTF8_BOM()
    {
        // Arrange
        var data = new List<string> { "資料" };
        var columns = new Dictionary<string, Func<string, string>>
        {
            ["欄位"] = s => s
        };

        // Act
        var result = _sut.ExportToCsv(data, columns);

        // Assert
        result[0].Should().Be(0xEF);
        result[1].Should().Be(0xBB);
        result[2].Should().Be(0xBF);
    }

    #endregion

    #region ExportTasksToCsv 測試

    [Fact]
    public void ExportTasksToCsv_正常任務清單_應回傳含BOM的位元組陣列()
    {
        // Arrange
        var tasks = new List<TaskCsvRow>
        {
            new("1", "任務一", "進行中", "高", "張三", "李四", "2024-01-01", "2024-01-02", "2024-01-31", "標籤A", "描述一")
        };

        // Act
        var result = _sut.ExportTasksToCsv(tasks);

        // Assert
        result.Should().NotBeNull();
        result.Length.Should().BeGreaterThan(Utf8Bom.Length);
        result[0].Should().Be(0xEF);
        result[1].Should().Be(0xBB);
        result[2].Should().Be(0xBF);
    }

    [Fact]
    public void ExportTasksToCsv_正常任務清單_應包含所有預期欄位標頭()
    {
        // Arrange
        var tasks = new List<TaskCsvRow>
        {
            new("1", "任務一", "進行中", "高", "張三", "李四", "2024-01-01", "2024-01-02", "2024-01-31", "標籤A", "描述一")
        };

        // Act
        var result = _sut.ExportTasksToCsv(tasks);
        var content = GetContentWithoutBom(result);
        var lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

        // Assert
        lines[0].Should().Be("編號,標題,狀態,優先級,負責人,建立者,建立時間,更新時間,截止日期,標籤,描述");
    }

    [Fact]
    public void ExportTasksToCsv_正常任務清單_應包含正確的資料列內容()
    {
        // Arrange
        var tasks = new List<TaskCsvRow>
        {
            new("T001", "修復Bug", "完成", "緊急", "王五", "陳六", "2024-01-10", "2024-01-15", "2024-01-20", "bug,fix", "詳細描述")
        };

        // Act
        var result = _sut.ExportTasksToCsv(tasks);
        var content = GetContentWithoutBom(result);
        var lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

        // Assert
        lines.Should().HaveCount(2);
        lines[1].Should().Contain("T001");
        lines[1].Should().Contain("修復Bug");
        lines[1].Should().Contain("完成");
        lines[1].Should().Contain("緊急");
        lines[1].Should().Contain("王五");
        lines[1].Should().Contain("陳六");
    }

    [Fact]
    public void ExportTasksToCsv_空任務清單_應只包含標頭列()
    {
        // Arrange
        var tasks = new List<TaskCsvRow>();

        // Act
        var result = _sut.ExportTasksToCsv(tasks);
        var content = GetContentWithoutBom(result);
        var lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

        // Assert
        lines.Should().HaveCount(1);
        lines[0].Should().Contain("編號");
    }

    [Fact]
    public void ExportTasksToCsv_任務標題含逗號_應正確轉義()
    {
        // Arrange
        var tasks = new List<TaskCsvRow>
        {
            new("1", "任務,含逗號", "進行中", "高", "張三", "李四", "2024-01-01", "2024-01-02", "2024-01-31", "", "")
        };

        // Act
        var result = _sut.ExportTasksToCsv(tasks);
        var content = GetContentWithoutBom(result);
        var lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

        // Assert
        lines[1].Should().Contain("\"任務,含逗號\"");
    }

    [Fact]
    public void ExportTasksToCsv_多筆任務_應包含正確數量的資料列()
    {
        // Arrange
        var tasks = new List<TaskCsvRow>
        {
            new("1", "任務一", "待處理", "高", "張三", "李四", "2024-01-01", "2024-01-02", "2024-01-31", "", ""),
            new("2", "任務二", "進行中", "中", "王五", "陳六", "2024-01-03", "2024-01-04", "2024-02-28", "", ""),
            new("3", "任務三", "完成", "低", "趙七", "錢八", "2024-01-05", "2024-01-06", "2024-03-31", "", "")
        };

        // Act
        var result = _sut.ExportTasksToCsv(tasks);
        var content = GetContentWithoutBom(result);
        var lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

        // Assert
        lines.Should().HaveCount(4);
    }

    [Fact]
    public void ExportTasksToCsv_任務欄位為空字串_應正確輸出空欄位()
    {
        // Arrange
        var tasks = new List<TaskCsvRow>
        {
            new("2", "任務二", "待處理", "低", "", "", "", "", "", "", "")
        };

        // Act
        var result = _sut.ExportTasksToCsv(tasks);
        var content = GetContentWithoutBom(result);
        var lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

        // Assert
        lines[1].Should().StartWith("2,任務二,待處理,低,,,,,,");
    }

    [Fact]
    public void ExportTasksToCsv_任務描述含雙引號_應正確轉義雙引號()
    {
        // Arrange
        