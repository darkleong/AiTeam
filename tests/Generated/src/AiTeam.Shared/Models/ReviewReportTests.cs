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
        public void ReviewReport_建立新實例_應有預設值()
        {
            // Arrange & Act
            var report = new ReviewReport();

            // Assert
            report.Id.Should().NotBeNullOrEmpty();
            report.Title.Should().BeEmpty();
            report.ReviewedBy.Should().BeEmpty();
            report.TargetFile.Should().BeEmpty();
            report.Content.Should().BeEmpty();
            report.Status.Should().Be("Pending");
            report.ProjectId.Should().BeEmpty();
            report.PullRequestId.Should().BeEmpty();
            report.Issues.Should().NotBeNull().And.BeEmpty();
            report.Summary.Should().NotBeNull();
            report.Metadata.Should().NotBeNull().And.BeEmpty();
            report.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        }

        [Fact]
        public void ReviewReport_建立兩個實例_Id應不相同()
        {
            // Arrange & Act
            var report1 = new ReviewReport();
            var report2 = new ReviewReport();

            // Assert
            report1.Id.Should().NotBe(report2.Id);
        }

        [Fact]
        public void ReviewReport_設定屬性值_應正確儲存()
        {
            // Arrange
            var report = new ReviewReport();
            var expectedTitle = "Code Review Report";
            var expectedStatus = "Completed";
            var expectedProjectId = "proj-001";

            // Act
            report.Title = expectedTitle;
            report.Status = expectedStatus;
            report.ProjectId = expectedProjectId;

            // Assert
            report.Title.Should().Be(expectedTitle);
            report.Status.Should().Be(expectedStatus);
            report.ProjectId.Should().Be(expectedProjectId);
        }

        [Fact]
        public void ReviewReport_加入Issues_應正確存放()
        {
            // Arrange
            var report = new ReviewReport();
            var issue = new ReviewIssue
            {
                Message = "Test Issue",
                Severity = IssueSeverity.Error
            };

            // Act
            report.Issues.Add(issue);

            // Assert
            report.Issues.Should().HaveCount(1);
            report.Issues[0].Message.Should().Be("Test Issue");
        }

        [Fact]
        public void ReviewReport_加入Metadata_應正確存放()
        {
            // Arrange
            var report = new ReviewReport();

            // Act
            report.Metadata["key1"] = "value1";
            report.Metadata["key2"] = 42;

            // Assert
            report.Metadata.Should().HaveCount(2);
            report.Metadata["key1"].Should().Be("value1");
            report.Metadata["key2"].Should().Be(42);
        }
    }

    public class ReviewIssueTests
    {
        [Fact]
        public void ReviewIssue_建立新實例_應有預設值()
        {
            // Arrange & Act
            var issue = new ReviewIssue();

            // Assert
            issue.Id.Should().NotBeNullOrEmpty();
            issue.Severity.Should().Be(IssueSeverity.Info);
            issue.Message.Should().BeEmpty();
            issue.FilePath.Should().BeEmpty();
            issue.LineNumber.Should().BeNull();
            issue.ColumnNumber.Should().BeNull();
            issue.Category.Should().BeEmpty();
            issue.Suggestion.Should().BeEmpty();
            issue.RuleId.Should().BeEmpty();
            issue.CodeSnippet.Should().BeEmpty();
        }

        [Fact]
        public void ReviewIssue_建立兩個實例_Id應不相同()
        {
            // Arrange & Act
            var issue1 = new ReviewIssue();
            var issue2 = new ReviewIssue();

            // Assert
            issue1.Id.Should().NotBe(issue2.Id);
        }

        [Fact]
        public void ReviewIssue_設定所有屬性_應正確儲存()
        {
            // Arrange
            var issue = new ReviewIssue();

            // Act
            issue.Severity = IssueSeverity.Error;
            issue.Message = "Null reference exception";
            issue.FilePath = "src/Main.cs";
            issue.LineNumber = 42;
            issue.ColumnNumber = 10;
            issue.Category = "NullCheck";
            issue.Suggestion = "Add null check";
            issue.RuleId = "CS0001";
            issue.CodeSnippet = "var x = obj.Value;";

            // Assert
            issue.Severity.Should().Be(IssueSeverity.Error);
            issue.Message.Should().Be("Null reference exception");
            issue.FilePath.Should().Be("src/Main.cs");
            issue.LineNumber.Should().Be(42);
            issue.ColumnNumber.Should().Be(10);
            issue.Category.Should().Be("NullCheck");
            issue.Suggestion.Should().Be("Add null check");
            issue.RuleId.Should().Be("CS0001");
            issue.CodeSnippet.Should().Be("var x = obj.Value;");
        }

        [Fact]
        public void ReviewIssue_LineNumber與ColumnNumber為Nullable_可設為null()
        {
            // Arrange
            var issue = new ReviewIssue
            {
                LineNumber = 10,
                ColumnNumber = 5
            };

            // Act
            issue.LineNumber = null;
            issue.ColumnNumber = null;

            // Assert
            issue.LineNumber.Should().BeNull();
            issue.ColumnNumber.Should().BeNull();
        }
    }

    public class ReviewSummaryTests
    {
        [Fact]
        public void ReviewSummary_建立新實例_應有預設值()
        {
            // Arrange & Act
            var summary = new ReviewSummary();

            // Assert
            summary.TotalIssues.Should().Be(0);
            summary.ErrorCount.Should().Be(0);
            summary.WarningCount.Should().Be(0);
            summary.InfoCount.Should().Be(0);
            summary.IssuesByCategory.Should().NotBeNull().And.BeEmpty();
            summary.IssuesByFile.Should().NotBeNull().And.BeEmpty();
        }

        [Fact]
        public void SummaryLine_無任何問題時_應回傳正確格式()
        {
            // Arrange
            var summary = new ReviewSummary();

            // Act
            var result = summary.SummaryLine;

            // Assert
            result.Should().Be("統計總結：共 0 個問題 | Error: 0 | Warning: 0 | Info: 0");
        }

        [Fact]
        public void SummaryLine_有各種問題時_應回傳正確格式()
        {
            // Arrange
            var summary = new ReviewSummary
            {
                TotalIssues = 6,
                ErrorCount = 2,
                WarningCount = 3,
                InfoCount = 1
            };

            // Act
            var result = summary.SummaryLine;

            // Assert
            result.Should().Be("統計總結：共 6 個問題 | Error: 2 | Warning: 3 | Info: 1");
        }

        [Fact]
        public void HasCriticalIssues_ErrorCount為零時_應回傳false()
        {
            // Arrange
            var summary = new ReviewSummary { ErrorCount = 0 };

            // Act & Assert
            summary.HasCriticalIssues.Should().BeFalse();
        }

        [Fact]
        public void HasCriticalIssues_ErrorCount大於零時_應回傳true()
        {
            // Arrange
            var summary = new ReviewSummary { ErrorCount = 1 };

            // Act & Assert
            summary.HasCriticalIssues.Should().BeTrue();
        }

        [Fact]
        public void OverallStatus_無任何問題時_應回傳Passed()
        {
            // Arrange
            var summary = new ReviewSummary();

            // Act & Assert
            summary.OverallStatus.Should().Be("Passed");
        }

        [Fact]
        public void OverallStatus_只有Info問題時_應回傳PassedWithInfo()
        {
            // Arrange
            var summary = new ReviewSummary { InfoCount = 1 };

            // Act & Assert
            summary.OverallStatus.Should().Be("Passed with Info");
        }

        [Fact]
        public void OverallStatus_有Warning問題時_應回傳Warning()
        {
            // Arrange
            var summary = new ReviewSummary { WarningCount = 2 };

            // Act & Assert
            summary.OverallStatus.Should().Be("Warning");
        }

        [Fact]
        public void OverallStatus_有Error問題時_應回傳Failed()
        {
            // Arrange
            var summary = new ReviewSummary { ErrorCount = 1, WarningCount = 2, InfoCount = 3 };

            // Act & Assert
            summary.OverallStatus.Should().Be("Failed");
        }

        [Fact]
        public void RecalculateFromIssues_傳入null_應重置所有計數為零()
        {
            // Arrange
            var summary = new ReviewSummary
            {
                TotalIssues = 10,
                ErrorCount = 3,
                WarningCount = 4,
                InfoCount = 3
            };

            // Act
            summary.RecalculateFromIssues(null);

            // Assert
            summary.TotalIssues.Should().Be(0);
            summary.ErrorCount.Should().Be(0);
            summary.WarningCount.Should().Be(0);
            summary.InfoCount.Should().Be(0);
            summary.IssuesByCategory.Should().BeEmpty();
            summary.IssuesByFile.Should().BeEmpty();
        }

        [Fact]
        public void RecalculateFromIssues_傳入空列表_應重置所有計數為零()
        {
            // Arrange
            var summary = new ReviewSummary
            {
                TotalIssues = 5,
                ErrorCount = 2
            };

            // Act
            summary.RecalculateFromIssues(new List<ReviewIssue>());

            // Assert
            summary.TotalIssues.Should().Be(0);
            summary.ErrorCount.Should().Be(0);
            summary.WarningCount.Should().Be(0);
            summary.InfoCount.Should().Be(0);
        }

        [Fact]
        public void RecalculateFromIssues_傳入多個不同嚴重性問題_應正確計算各計數()
        {
            // Arrange
            var summary = new ReviewSummary();
            var issues = new List<ReviewIssue>
            {
                new ReviewIssue { Severity = IssueSeverity.Error },
                new ReviewIssue { Severity = IssueSeverity.Error },
                new ReviewIssue { Severity = IssueSeverity.Warning },
                new ReviewIssue { Severity = IssueSeverity.Info },
                new ReviewIssue { Severity = IssueSeverity.Info },
                new ReviewIssue { Severity = IssueSeverity.Info }
            };

            // Act
            summary.RecalculateFromIssues(issues);

            // Assert
            summary.TotalIssues.Should().Be(6);
            summary.ErrorCount.Should().Be(2);
            summary.WarningCount.Should().Be(1);
            summary.InfoCount.Should().Be(3);
        }

        [Fact]
        public void RecalculateFromIssues_傳入有Category的問題_應正確統計IssuesByCategory()
        {
            // Arrange
            var summary = new ReviewSummary();
            var issues = new List<ReviewIssue>
            {
                new ReviewIssue { Category = "NullCheck", Severity = IssueSeverity.Error },
                new ReviewIssue { Category = "NullCheck", Severity = IssueSeverity.Warning },
                new ReviewIssue { Category = "Performance", Severity = IssueSeverity.Info }
            };

            // Act
            summary.RecalculateFromIssues(issues);

            // Assert
            summary.IssuesByCategory.Should().HaveCount(2);
            summary.IssuesByCategory["NullCheck"].Should().Be(2);
            summary.IssuesByCategory["Performance"].Should().Be(1);
        }

        [Fact]
        public void RecalculateFromIssues_傳入有FilePath的問題_應正確統計IssuesByFile()
        {
            // Arrange
            var summary = new ReviewSummary();
            var issues = new List<ReviewIssue>
            {
                new ReviewIssue { FilePath = "src/A.cs", Severity = IssueSeverity.Error },
                new ReviewIssue { FilePath = "src/A.cs", Severity = IssueSeverity.Warning },
                new ReviewIssue { FilePath = "src/B.cs", Severity = IssueSeverity.Info }
            };

            // Act
            summary.RecalculateFromIssues(issues);

            // Assert
            summary.IssuesByFile.Should().HaveCount(2);
            summary.IssuesByFile["src/A.cs"].Should().Be(2);
            summary.IssuesByFile["src/B.cs"].Should().Be(1);
        }

        [Fact]
        public void RecalculateFromIssues_問題無Category與FilePath_不應加入統計字典()
        {
            // Arrange
            var summary = new ReviewSummary();
            var issues = new List<ReviewIssue>
            {
                new ReviewIssue { Category = string.Empty, FilePath = string.Empty, Severity = IssueSeverity.Error },
                new ReviewIssue { Category = null, FilePath = null, Severity = IssueSeverity.Warning }
            };

            // Act
            summary.RecalculateFromIssues(issues);

            // Assert
            summary.TotalIssues.Should().Be(2);
            summary.IssuesByCategory.Should().BeEmpty();
            summary.IssuesByFile.Should().BeEmpty();
        }

        [Fact]
        public void RecalculateFromIssues_多次呼叫_應重置並重新計算()
        {
            // Arrange
            var summary = new ReviewSummary();
            var firstBatch = new List<ReviewIssue>
            {
                new ReviewIssue { Severity = IssueSeverity.Error, Category = "OldCategory", FilePath = "old.cs" }
            };
            var secondBatch = new List<ReviewIssue>
            {
                new ReviewIssue { Severity = IssueSeverity.Info, Category = "NewCategory", FilePath = "new.cs" },
                new ReviewIssue { Severity = IssueSeverity.Info, Category = "NewCategory", FilePath = "new.cs" }
            };

            // Act
            summary.RecalculateFromIssues(firstBatch);
            summary.RecalculateFromIssues(secondBatch);

            // Assert
            summary.TotalIssues.Should().Be(2);
            summary.ErrorCount.Should().Be(0);
            summary.InfoCount.Should().Be(2);
            