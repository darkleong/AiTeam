using FluentAssertions;
using AiTeam.Shared.Models;

namespace AiTeam.Shared.Tests.Models;

public class ReviewerReportSummaryTests
{
    [Fact]
    public void TotalCount_當三個計數皆為零_應回傳零()
    {
        // Arrange
        var summary = new ReviewerReportSummary
        {
            ErrorCount = 0,
            WarningCount = 0,
            SuggestionCount = 0
        };

        // Act
        var result = summary.TotalCount;

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public void TotalCount_當三個計數皆有正整數_應回傳正確總和()
    {
        // Arrange
        var summary = new ReviewerReportSummary
        {
            ErrorCount = 3,
            WarningCount = 5,
            SuggestionCount = 2
        };

        // Act
        var result = summary.TotalCount;

        // Assert
        result.Should().Be(10);
    }

    [Fact]
    public void TotalCount_當只有ErrorCount有值_應回傳ErrorCount的值()
    {
        // Arrange
        var summary = new ReviewerReportSummary
        {
            ErrorCount = 7,
            WarningCount = 0,
            SuggestionCount = 0
        };

        // Act
        var result = summary.TotalCount;

        // Assert
        result.Should().Be(7);
    }

    [Fact]
    public void TotalCount_當只有WarningCount有值_應回傳WarningCount的值()
    {
        // Arrange
        var summary = new ReviewerReportSummary
        {
            ErrorCount = 0,
            WarningCount = 4,
            SuggestionCount = 0
        };

        // Act
        var result = summary.TotalCount;

        // Assert
        result.Should().Be(4);
    }

    [Fact]
    public void TotalCount_當只有SuggestionCount有值_應回傳SuggestionCount的值()
    {
        // Arrange
        var summary = new ReviewerReportSummary
        {
            ErrorCount = 0,
            WarningCount = 0,
            SuggestionCount = 9
        };

        // Act
        var result = summary.TotalCount;

        // Assert
        result.Should().Be(9);
    }

    [Fact]
    public void TotalCount_當動態變更ErrorCount後_應即時反映最新總和()
    {
        // Arrange
        var summary = new ReviewerReportSummary
        {
            ErrorCount = 1,
            WarningCount = 1,
            SuggestionCount = 1
        };

        // Act
        summary.ErrorCount = 10;
        var result = summary.TotalCount;

        // Assert
        result.Should().Be(12);
    }

    [Fact]
    public void ErrorCount_設定後_應可正確讀取()
    {
        // Arrange
        var summary = new ReviewerReportSummary();

        // Act
        summary.ErrorCount = 5;

        // Assert
        summary.ErrorCount.Should().Be(5);
    }

    [Fact]
    public void WarningCount_設定後_應可正確讀取()
    {
        // Arrange
        var summary = new ReviewerReportSummary();

        // Act
        summary.WarningCount = 8;

        // Assert
        summary.WarningCount.Should().Be(8);
    }

    [Fact]
    public void SuggestionCount_設定後_應可正確讀取()
    {
        // Arrange
        var summary = new ReviewerReportSummary();

        // Act
        summary.SuggestionCount = 3;

        // Assert
        summary.SuggestionCount.Should().Be(3);
    }

    [Fact]
    public void 建構式_預設值_三個計數應皆為零()
    {
        // Arrange & Act
        var summary = new ReviewerReportSummary();

        // Assert
        summary.ErrorCount.Should().Be(0);
        summary.WarningCount.Should().Be(0);
        summary.SuggestionCount.Should().Be(0);
        summary.TotalCount.Should().Be(0);
    }

    [Theory]
    [InlineData(1, 2, 3, 6)]
    [InlineData(0, 0, 1, 1)]
    [InlineData(100, 200, 300, 600)]
    [InlineData(int.MaxValue / 3, int.MaxValue / 3, int.MaxValue / 3, (int.MaxValue / 3) * 3)]
    public void TotalCount_多組資料組合_應回傳正確總和(int errors, int warnings, int suggestions, int expected)
    {
        // Arrange
        var summary = new ReviewerReportSummary
        {
            ErrorCount = errors,
            WarningCount = warnings,
            SuggestionCount = suggestions
        };

        // Act
        var result = summary.TotalCount;

        // Assert
        result.Should().Be(expected);
    }
}