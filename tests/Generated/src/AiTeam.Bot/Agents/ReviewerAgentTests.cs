```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using AiTeam.Bot.Agents;

namespace AiTeam.Bot.Tests.Agents
{
    // ===== ReviewIssue Tests =====
    public class ReviewIssueTests
    {
        [Fact]
        public void ReviewIssue_預設建立_所有字串屬性應為空字串()
        {
            // Arrange & Act
            var issue = new ReviewIssue();

            // Assert
            issue.Type.Should().BeEmpty();
            issue.Severity.Should().BeEmpty();
            issue.Description.Should().BeEmpty();
            issue.FilePath.Should().BeEmpty();
            issue.LineNumber.Should().BeNull();
            issue.Suggestion.Should().BeEmpty();
        }

        [Fact]
        public void ReviewIssue_設定所有屬性_應正確儲存()
        {
            // Arrange & Act
            var issue = new ReviewIssue
            {
                Type = "Bug",
                Severity = "High",
                Description = "空指標參考",
                FilePath = "src/Program.cs",
                LineNumber = 42,
                Suggestion = "請加入 null 檢查"
            };

            // Assert
            issue.Type.Should().Be("Bug");
            issue.Severity.Should().Be("High");
            issue.Description.Should().Be("空指標參考");
            issue.FilePath.Should().Be("src/Program.cs");
            issue.LineNumber.Should().Be(42);
            issue.Suggestion.Should().Be("請加入 null 檢查");
        }
    }

    // ===== ReviewIssueSummary Tests =====
    public class ReviewIssueSummaryTests
    {
        [Fact]
        public void RenderStatisticsRow_有各類型問題_應渲染包含所有類型的表格()
        {
            // Arrange
            var summary = new ReviewIssueSummary
            {
                BugCount = 2,
                WarningCount = 3,
                InfoCount = 1,
                TotalCount = 6,
                IssueCountByType = new Dictionary<string, int>
                {
                    { "Bug", 2 },
                    { "Warning", 3 },
                    { "Info", 1 }
                }
            };

            // Act
            var result = summary.RenderStatisticsRow();

            // Assert
            result.Should().Contain("## 問題總結統計");
            result.Should().Contain("🐛 Bug");
            result.Should().Contain("⚠️ Warning");
            result.Should().Contain("ℹ️ Info");
            result.Should().Contain("**總計**");
            result.Should().Contain("**6**");
        }

        [Fact]
        public void RenderStatisticsRow_包含未知類型_應使用預設emoji()
        {
            // Arrange
            var summary = new ReviewIssueSummary
            {
                TotalCount = 1,
                IssueCountByType = new Dictionary<string, int>
                {
                    { "CustomType", 1 }
                }
            };

            // Act
            var result = summary.RenderStatisticsRow();

            // Assert
            result.Should().Contain("📌 CustomType");
        }

        [Fact]
        public void RenderStatisticsRow_包含Error類型_應使用錯誤emoji()
        {
            // Arrange
            var summary = new ReviewIssueSummary
            {
                TotalCount = 1,
                IssueCountByType = new Dictionary<string, int>
                {
                    { "error", 1 }
                }
            };

            // Act
            var result = summary.RenderStatisticsRow();

            // Assert
            result.Should().Contain("❌ error");
        }

        [Fact]
        public void RenderStatisticsRow_包含Suggestion類型_應使用燈泡emoji()
        {
            // Arrange
            var summary = new ReviewIssueSummary
            {
                TotalCount = 1,
                IssueCountByType = new Dictionary<string, int>
                {
                    { "suggestion", 1 }
                }
            };

            // Act
            var result = summary.RenderStatisticsRow();

            // Assert
            result.Should().Contain("💡 suggestion");
        }

        [Fact]
        public void RenderStatisticsRow_問題依數量降序排列_數量多的類型應排前面()
        {
            // Arrange
            var summary = new ReviewIssueSummary
            {
                TotalCount = 6,
                IssueCountByType = new Dictionary<string, int>
                {
                    { "Info", 1 },
                    { "Bug", 5 }
                }
            };

            // Act
            var result = summary.RenderStatisticsRow();

            // Assert
            var bugIndex = result.IndexOf("Bug", StringComparison.Ordinal);
            var infoIndex = result.IndexOf("Info", StringComparison.Ordinal);
            bugIndex.Should().BeLessThan(infoIndex);
        }

        [Fact]
        public void RenderStatisticsRow_空IssueCountByType_應僅渲染總計行()
        {
            // Arrange
            var summary = new ReviewIssueSummary
            {
                TotalCount = 0,
                IssueCountByType = new Dictionary<string, int>()
            };

            // Act
            var result = summary.RenderStatisticsRow();

            // Assert
            result.Should().Contain("**總計**");
            result.Should().Contain("**0**");
        }
    }

    // ===== ReviewReport Tests =====
    public class ReviewReportTests
    {
        private ReviewReport CreateReportWithIssues(List<ReviewIssue>? issues = null)
        {
            var report = new ReviewReport
            {
                Title = "測試報告",
                Summary = "這是測試摘要",
                OverallAssessment = "整體良好",
                Recommendations = "建議加強測試",
                Issues = issues ?? new List<ReviewIssue>()
            };
            return report;
        }

        [Fact]
        public void CalculateStatistics_有多種類型問題_應正確計算各類型數量()
        {
            // Arrange
            var report = CreateReportWithIssues(new List<ReviewIssue>
            {
                new ReviewIssue { Type = "Bug" },
                new ReviewIssue { Type = "Bug" },
                new ReviewIssue { Type = "Warning" },
                new ReviewIssue { Type = "Info" }
            });

            // Act
            report.CalculateStatistics();

            // Assert
            report.IssueSummary.BugCount.Should().Be(2);
            report.IssueSummary.WarningCount.Should().Be(1);
            report.IssueSummary.InfoCount.Should().Be(1);
            report.IssueSummary.TotalCount.Should().Be(4);
            report.IssueSummary.IssueCountByType["Bug"].Should().Be(2);
            report.IssueSummary.IssueCountByType["Warning"].Should().Be(1);
            report.IssueSummary.IssueCountByType["Info"].Should().Be(1);
        }

        [Fact]
        public void CalculateStatistics_問題清單為空_應所有計數為零()
        {
            // Arrange
            var report = CreateReportWithIssues(new List<ReviewIssue>());

            // Act
            report.CalculateStatistics();

            // Assert
            report.IssueSummary.BugCount.Should().Be(0);
            report.IssueSummary.WarningCount.Should().Be(0);
            report.IssueSummary.InfoCount.Should().Be(0);
            report.IssueSummary.TotalCount.Should().Be(0);
            report.IssueSummary.IssueCountByType.Should().BeEmpty();
        }

        [Fact]
        public void CalculateStatistics_問題Type為空白字串_應歸類為Other()
        {
            // Arrange
            var report = CreateReportWithIssues(new List<ReviewIssue>
            {
                new ReviewIssue { Type = "" },
                new ReviewIssue { Type = "   " }
            });

            // Act
            report.CalculateStatistics();

            // Assert
            report.IssueSummary.IssueCountByType.Should().ContainKey("Other");
            report.IssueSummary.IssueCountByType["Other"].Should().Be(2);
        }

        [Fact]
        public void CalculateStatistics_重複計算_應重置並重新計算()
        {
            // Arrange
            var report = CreateReportWithIssues(new List<ReviewIssue>
            {
                new ReviewIssue { Type = "Bug" }
            });

            // Act - 計算兩次
            report.CalculateStatistics();
            report.CalculateStatistics();

            // Assert - 計數不應翻倍
            report.IssueSummary.BugCount.Should().Be(1);
            report.IssueSummary.TotalCount.Should().Be(1);
        }

        [Fact]
        public void RenderReport_有完整資料_應包含所有區段()
        {
            // Arrange
            var report = CreateReportWithIssues(new List<ReviewIssue>
            {
                new ReviewIssue
                {
                    Type = "Bug",
                    Description = "空指標",
                    FilePath = "src/Program.cs",
                    LineNumber = 10,
                    Suggestion = "加入 null 檢查"
                }
            });
            report.CalculateStatistics();

            // Act
            var result = report.RenderReport();

            // Assert
            result.Should().Contain("# 測試報告");
            result.Should().Contain("## 摘要");
            result.Should().Contain("這是測試摘要");
            result.Should().Contain("## 發現的問題");
            result.Should().Contain("空指標");
            result.Should().Contain("src/Program.cs:10");
            result.Should().Contain("加入 null 檢查");
            result.Should().Contain("## 整體評估");
            result.Should().Contain("整體良好");
            result.Should().Contain("## 建議事項");
            result.Should().Contain("建議加強測試");
        }

        [Fact]
        public void RenderReport_無問題清單_應不包含發現的問題區段()
        {
            // Arrange
            var report = CreateReportWithIssues(new List<ReviewIssue>());
            report.CalculateStatistics();

            // Act
            var result = report.RenderReport();

            // Assert
            result.Should().NotContain("## 發現的問題");
            result.Should().NotContain("---");
        }

        [Fact]
        public void RenderReport_有問題且統計不為零_應渲染統計區段()
        {
            // Arrange
            var report = CreateReportWithIssues(new List<ReviewIssue>
            {
                new ReviewIssue { Type = "Bug", Description = "測試 Bug" }
            });
            report.CalculateStatistics();

            // Act
            var result = report.RenderReport();

            // Assert
            result.Should().Contain("---");
            result.Should().Contain("## 問題總結統計");
        }

        [Fact]
        public void RenderReport_問題有FilePath但無LineNumber_應只顯示路徑不含行號()
        {
            // Arrange
            var report = CreateReportWithIssues(new List<ReviewIssue>
            {
                new ReviewIssue
                {
                    Type = "Warning",
                    Description = "測試問題",
                    FilePath = "src/Helper.cs",
                    LineNumber = null
                }
            });
            report.CalculateStatistics();

            // Act
            var result = report.RenderReport();

            // Assert
            result.Should().Contain("src/Helper.cs");
            result.Should().NotContain("src/Helper.cs:");
        }

        [Fact]
        public void RenderReport_問題無FilePath_應不顯示位置資訊()
        {
            // Arrange
            var report = CreateReportWithIssues(new List<ReviewIssue>
            {
                new ReviewIssue
                {
                    Type = "Info",
                    Description = "只有描述沒有路徑",
                    FilePath = ""
                }
            });
            report.CalculateStatistics();

            // Act
            var result = report.RenderReport();

            // Assert
            result.Should().Contain("只有描述沒有路徑");
            result.Should().NotContain("**位置：**");
        }

        [Fact]
        public void RenderReport_Summary為空_應不包含摘要區段()
        {
            // Arrange
            var report = new ReviewReport
            {
                Title = "測試",
                Summary = "",
                Issues = new List<ReviewIssue>()
            };
            report.CalculateStatistics();

            // Act
            var result = report.RenderReport();

            // Assert
            result.Should().NotContain("## 摘要");
        }

        [Fact]
        public void RenderReport_OverallAssessment為空_應不包含整體評估區段()
        {
            // Arrange
            var report = new ReviewReport
            {
                Title = "測試",
                OverallAssessment = "",
                Issues = new List<ReviewIssue>()
            };
            report.CalculateStatistics();

            // Act
            var result = report.RenderReport();

            // Assert
            result.Should().NotContain("## 整體評估");
        }

        [Fact]
        public void RenderReport_Recommendations為空_應不包含建議事項區段()
        {
            // Arrange
            var report = new ReviewReport
            {
                Title = "測試",
                Recommendations = "",
                Issues = new List<ReviewIssue>()
            };
            report.CalculateStatistics();

            // Act
            var result = report.RenderReport();

            // Assert
            result.Should().NotContain("## 建議事項");
        }

        [Fact]
        public void RenderReport_多種類型問題_應依類型分組顯示()
        {
            // Arrange
            var report = CreateReportWithIssues(new List<ReviewIssue>
            {
                new ReviewIssue { Type = "Bug", Description = "Bug 問題" },
                new ReviewIssue { Type = "Warning", Description = "Warning 問題" },
                new ReviewIssue { Type = "Bug", Description = "另一個 Bug" }
            });
            report.CalculateStatistics();

            // Act
            var result = report.RenderReport();

            // Assert
            result.Should().Contain("### Bug");
            result.Should().Contain("### Warning");
            result.Should().Contain("Bug 問題");
            result.Should().Contain("另一個 Bug");
            result.Should().Contain("Warning 問題");
        }

        [Fact]
        public void RenderReport_應包含審查時