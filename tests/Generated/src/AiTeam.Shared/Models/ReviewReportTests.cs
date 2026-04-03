```csharp
using System;
using System.Collections.Generic;
using AiTeam.Shared.Models;
using FluentAssertions;
using Xunit;

namespace AiTeam.Shared.Tests.Models
{
    public class ReviewReportTests
    {
        [Fact]
        public void ReviewReport_建立預設實例_所有屬性應有預設值()
        {
            // Arrange & Act
            var report = new ReviewReport();

            // Assert
            report.Id.Should().BeEmpty();
            report.Title.Should().BeEmpty();
            report.Content.Should().BeEmpty();
            report.Issues.Should().NotBeNull().And.BeEmpty();
            report.Summary.Should().NotBeNull();
            report.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        }

        [Fact]
        public void ReviewReport_設定所有屬性_應正確儲存值()
        {
            // Arrange
            var expectedId = "report-001";
            var expectedTitle = "Code Review Report";
            var expectedContent = "This is the report content.";
            var expectedCreatedAt = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc);
            var expectedIssues = new List<ReviewIssue>
            {
                new ReviewIssue { Id = "issue-001", Type = ReviewIssueType.Bug }
            };
            var expectedSummary = new ReviewIssueSummary { TotalCount = 1, BugCount = 1 };

            // Act
            var report = new ReviewReport
            {
                Id = expectedId,
                Title = expectedTitle,
                Content = expectedContent,
                CreatedAt = expectedCreatedAt,
                Issues = expectedIssues,
                Summary = expectedSummary
            };

            // Assert
            report.Id.Should().Be(expectedId);
            report.Title.Should().Be(expectedTitle);
            report.Content.Should().Be(expectedContent);
            report.CreatedAt.Should().Be(expectedCreatedAt);
            report.Issues.Should().BeEquivalentTo(expectedIssues);
            report.Summary.Should().BeEquivalentTo(expectedSummary);
        }

        [Fact]
        public void ReviewReport_Issues清單_可以新增問題()
        {
            // Arrange
            var report = new ReviewReport();
            var issue = new ReviewIssue
            {
                Id = "issue-001",
                Type = ReviewIssueType.Warning,
                Description = "Potential null reference"
            };

            // Act
            report.Issues.Add(issue);

            // Assert
            report.Issues.Should().HaveCount(1);
            report.Issues[0].Should().BeEquivalentTo(issue);
        }

        [Fact]
        public void ReviewReport_設定空白Issues清單_應正常運作()
        {
            // Arrange & Act
            var report = new ReviewReport
            {
                Issues = new List<ReviewIssue>()
            };

            // Assert
            report.Issues.Should().NotBeNull().And.BeEmpty();
        }
    }

    public class ReviewIssueTests
    {
        [Fact]
        public void ReviewIssue_建立預設實例_所有屬性應有預設值()
        {
            // Arrange & Act
            var issue = new ReviewIssue();

            // Assert
            issue.Id.Should().BeEmpty();
            issue.Type.Should().Be(ReviewIssueType.Info);
            issue.Description.Should().BeEmpty();
            issue.FilePath.Should().BeEmpty();
            issue.LineNumber.Should().BeNull();
            issue.Suggestion.Should().BeEmpty();
        }

        [Fact]
        public void ReviewIssue_設定所有屬性_應正確儲存值()
        {
            // Arrange
            var expectedId = "issue-001";
            var expectedType = ReviewIssueType.Bug;
            var expectedDescription = "Null pointer dereference";
            var expectedFilePath = "src/Service/MyService.cs";
            var expectedLineNumber = 42;
            var expectedSuggestion = "Add null check before accessing the property";

            // Act
            var issue = new ReviewIssue
            {
                Id = expectedId,
                Type = expectedType,
                Description = expectedDescription,
                FilePath = expectedFilePath,
                LineNumber = expectedLineNumber,
                Suggestion = expectedSuggestion
            };

            // Assert
            issue.Id.Should().Be(expectedId);
            issue.Type.Should().Be(expectedType);
            issue.Description.Should().Be(expectedDescription);
            issue.FilePath.Should().Be(expectedFilePath);
            issue.LineNumber.Should().Be(expectedLineNumber);
            issue.Suggestion.Should().Be(expectedSuggestion);
        }

        [Fact]
        public void ReviewIssue_LineNumber為Null_應可正常設定與讀取()
        {
            // Arrange & Act
            var issue = new ReviewIssue
            {
                LineNumber = null
            };

            // Assert
            issue.LineNumber.Should().BeNull();
        }

        [Fact]
        public void ReviewIssue_LineNumber設定為零_應正確儲存()
        {
            // Arrange & Act
            var issue = new ReviewIssue
            {
                LineNumber = 0
            };

            // Assert
            issue.LineNumber.Should().Be(0);
        }
    }

    public class ReviewIssueSummaryTests
    {
        [Fact]
        public void ReviewIssueSummary_建立預設實例_所有計數應為零()
        {
            // Arrange & Act
            var summary = new ReviewIssueSummary();

            // Assert
            summary.TotalCount.Should().Be(0);
            summary.BugCount.Should().Be(0);
            summary.WarningCount.Should().Be(0);
            summary.InfoCount.Should().Be(0);
            summary.SecurityCount.Should().Be(0);
            summary.PerformanceCount.Should().Be(0);
            summary.StyleCount.Should().Be(0);
        }

        [Fact]
        public void FromIssues_傳入空清單_所有計數應為零()
        {
            // Arrange
            var issues = new List<ReviewIssue>();

            // Act
            var summary = ReviewIssueSummary.FromIssues(issues);

            // Assert
            summary.TotalCount.Should().Be(0);
            summary.BugCount.Should().Be(0);
            summary.WarningCount.Should().Be(0);
            summary.InfoCount.Should().Be(0);
            summary.SecurityCount.Should().Be(0);
            summary.PerformanceCount.Should().Be(0);
            summary.StyleCount.Should().Be(0);
        }

        [Fact]
        public void FromIssues_傳入混合類型問題清單_應正確統計各類型數量()
        {
            // Arrange
            var issues = new List<ReviewIssue>
            {
                new ReviewIssue { Type = ReviewIssueType.Bug },
                new ReviewIssue { Type = ReviewIssueType.Bug },
                new ReviewIssue { Type = ReviewIssueType.Warning },
                new ReviewIssue { Type = ReviewIssueType.Info },
                new ReviewIssue { Type = ReviewIssueType.Security },
                new ReviewIssue { Type = ReviewIssueType.Performance },
                new ReviewIssue { Type = ReviewIssueType.Style },
                new ReviewIssue { Type = ReviewIssueType.Style }
            };

            // Act
            var summary = ReviewIssueSummary.FromIssues(issues);

            // Assert
            summary.TotalCount.Should().Be(8);
            summary.BugCount.Should().Be(2);
            summary.WarningCount.Should().Be(1);
            summary.InfoCount.Should().Be(1);
            summary.SecurityCount.Should().Be(1);
            summary.PerformanceCount.Should().Be(1);
            summary.StyleCount.Should().Be(2);
        }

        [Fact]
        public void FromIssues_傳入單一Bug問題_BugCount應為一且TotalCount為一()
        {
            // Arrange
            var issues = new List<ReviewIssue>
            {
                new ReviewIssue { Type = ReviewIssueType.Bug }
            };

            // Act
            var summary = ReviewIssueSummary.FromIssues(issues);

            // Assert
            summary.TotalCount.Should().Be(1);
            summary.BugCount.Should().Be(1);
            summary.WarningCount.Should().Be(0);
            summary.InfoCount.Should().Be(0);
            summary.SecurityCount.Should().Be(0);
            summary.PerformanceCount.Should().Be(0);
            summary.StyleCount.Should().Be(0);
        }

        [Fact]
        public void FromIssues_傳入所有相同類型問題_應只增加對應計數()
        {
            // Arrange
            var issues = new List<ReviewIssue>
            {
                new ReviewIssue { Type = ReviewIssueType.Security },
                new ReviewIssue { Type = ReviewIssueType.Security },
                new ReviewIssue { Type = ReviewIssueType.Security }
            };

            // Act
            var summary = ReviewIssueSummary.FromIssues(issues);

            // Assert
            summary.TotalCount.Should().Be(3);
            summary.SecurityCount.Should().Be(3);
            summary.BugCount.Should().Be(0);
            summary.WarningCount.Should().Be(0);
            summary.InfoCount.Should().Be(0);
            summary.PerformanceCount.Should().Be(0);
            summary.StyleCount.Should().Be(0);
        }

        [Fact]
        public void ToDictionary_預設實例_應回傳包含七個鍵的字典且值均為零()
        {
            // Arrange
            var summary = new ReviewIssueSummary();

            // Act
            var result = summary.ToDictionary();

            // Assert
            result.Should().HaveCount(7);
            result.Should().ContainKey("Total").WhoseValue.Should().Be(0);
            result.Should().ContainKey("Bug").WhoseValue.Should().Be(0);
            result.Should().ContainKey("Warning").WhoseValue.Should().Be(0);
            result.Should().ContainKey("Info").WhoseValue.Should().Be(0);
            result.Should().ContainKey("Security").WhoseValue.Should().Be(0);
            result.Should().ContainKey("Performance").WhoseValue.Should().Be(0);
            result.Should().ContainKey("Style").WhoseValue.Should().Be(0);
        }

        [Fact]
        public void ToDictionary_設定各計數後_應回傳對應正確值的字典()
        {
            // Arrange
            var summary = new ReviewIssueSummary
            {
                TotalCount = 10,
                BugCount = 3,
                WarningCount = 2,
                InfoCount = 1,
                SecurityCount = 2,
                PerformanceCount = 1,
                StyleCount = 1
            };

            // Act
            var result = summary.ToDictionary();

            // Assert
            result.Should().HaveCount(7);
            result["Total"].Should().Be(10);
            result["Bug"].Should().Be(3);
            result["Warning"].Should().Be(2);
            result["Info"].Should().Be(1);
            result["Security"].Should().Be(2);
            result["Performance"].Should().Be(1);
            result["Style"].Should().Be(1);
        }

        [Fact]
        public void ToDictionary_從FromIssues建立後_字典值應與統計計數一致()
        {
            // Arrange
            var issues = new List<ReviewIssue>
            {
                new ReviewIssue { Type = ReviewIssueType.Bug },
                new ReviewIssue { Type = ReviewIssueType.Warning },
                new ReviewIssue { Type = ReviewIssueType.Info },
                new ReviewIssue { Type = ReviewIssueType.Security },
                new ReviewIssue { Type = ReviewIssueType.Performance },
                new ReviewIssue { Type = ReviewIssueType.Style }
            };

            // Act
            var summary = ReviewIssueSummary.FromIssues(issues);
            var result = summary.ToDictionary();

            // Assert
            result["Total"].Should().Be(summary.TotalCount);
            result["Bug"].Should().Be(summary.BugCount);
            result["Warning"].Should().Be(summary.WarningCount);
            result["Info"].Should().Be(summary.InfoCount);
            result["Security"].Should().Be(summary.SecurityCount);
            result["Performance"].Should().Be(summary.PerformanceCount);
            result["Style"].Should().Be(summary.StyleCount);
        }
    }

    public class ReviewIssueTypeTests
    {
        [Theory]
        [InlineData(ReviewIssueType.Bug, 0)]
        [InlineData(ReviewIssueType.Warning, 1)]
        [InlineData(ReviewIssueType.Info, 2)]
        [InlineData(ReviewIssueType.Security, 3)]
        [InlineData(ReviewIssueType.Performance, 4)]
        [InlineData(ReviewIssueType.Style, 5)]
        public void ReviewIssueType_列舉值_應具備正確的整數對應值(ReviewIssueType type, int expectedValue)
        {
            // Assert
            ((int)type).Should().Be(expectedValue);
        }

        [Fact]
        public void ReviewIssueType_列舉_應包含六個成員()
        {
            // Arrange & Act
            var values = Enum.GetValues(typeof(ReviewIssueType));

            // Assert
            values.Length.Should().Be(6);
        }
    }
}
```