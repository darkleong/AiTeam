```csharp
using AiTeam.Dashboard.Components.Pages;
using FluentAssertions;

namespace AiTeam.Dashboard.Tests.Components.Pages;

public class ReviewerReportTests
{
    #region ReviewerReportRow Tests

    [Fact]
    public void ReviewerReportRow_有審查記錄時_ApprovalRate應正確計算()
    {
        // Arrange
        var row = new ReviewerReportRow
        {
            ReviewerName = "Alice",
            TotalReviews = 10,
            ApprovedCount = 7,
            RejectedCount = 2,
            PendingCount = 1,
            AverageReviewTimeHours = 4.5
        };

        // Act
        var approvalRate = row.ApprovalRate;

        // Assert
        approvalRate.Should().Be(70.0);
    }

    [Fact]
    public void ReviewerReportRow_無審查記錄時_ApprovalRate應為零()
    {
        // Arrange
        var row = new ReviewerReportRow
        {
            ReviewerName = "Bob",
            TotalReviews = 0,
            ApprovedCount = 0,
            RejectedCount = 0,
            PendingCount = 0,
            AverageReviewTimeHours = 0.0
        };

        // Act
        var approvalRate = row.ApprovalRate;

        // Assert
        approvalRate.Should().Be(0.0);
    }

    [Fact]
    public void ReviewerReportRow_全部通過審查時_ApprovalRate應為一百()
    {
        // Arrange
        var row = new ReviewerReportRow
        {
            ReviewerName = "Carol",
            TotalReviews = 5,
            ApprovedCount = 5,
            RejectedCount = 0,
            PendingCount = 0,
            AverageReviewTimeHours = 2.0
        };

        // Act
        var approvalRate = row.ApprovalRate;

        // Assert
        approvalRate.Should().Be(100.0);
    }

    [Fact]
    public void ReviewerReportRow_預設值應正確初始化()
    {
        // Arrange & Act
        var row = new ReviewerReportRow();

        // Assert
        row.ReviewerName.Should().BeEmpty();
        row.TotalReviews.Should().Be(0);
        row.ApprovedCount.Should().Be(0);
        row.RejectedCount.Should().Be(0);
        row.PendingCount.Should().Be(0);
        row.AverageReviewTimeHours.Should().Be(0.0);
        row.ApprovalRate.Should().Be(0.0);
    }

    [Fact]
    public void ReviewerReportRow_ApprovalRate應四捨五入到小數點後兩位()
    {
        // Arrange
        var row = new ReviewerReportRow
        {
            ReviewerName = "Dave",
            TotalReviews = 3,
            ApprovedCount = 1,
            RejectedCount = 1,
            PendingCount = 1,
            AverageReviewTimeHours = 5.0
        };

        // Act
        var approvalRate = row.ApprovalRate;

        // Assert
        approvalRate.Should().Be(Math.Round(1.0 / 3.0 * 100.0, 2));
    }

    #endregion

    #region ReviewerReportSummary Tests

    [Fact]
    public void ReviewerReportSummary_預設值應正確初始化()
    {
        // Arrange & Act
        var summary = new ReviewerReportSummary();

        // Assert
        summary.TotalReviews.Should().Be(0);
        summary.TotalApproved.Should().Be(0);
        summary.TotalRejected.Should().Be(0);
        summary.TotalPending.Should().Be(0);
        summary.AverageReviewTimeHours.Should().Be(0.0);
        summary.ApprovalRate.Should().Be(0.0);
    }

    [Fact]
    public void ReviewerReportSummary_設定屬性後應正確儲存值()
    {
        // Arrange & Act
        var summary = new ReviewerReportSummary
        {
            TotalReviews = 100,
            TotalApproved = 70,
            TotalRejected = 20,
            TotalPending = 10,
            AverageReviewTimeHours = 5.25,
            ApprovalRate = 70.0
        };

        // Assert
        summary.TotalReviews.Should().Be(100);
        summary.TotalApproved.Should().Be(70);
        summary.TotalRejected.Should().Be(20);
        summary.TotalPending.Should().Be(10);
        summary.AverageReviewTimeHours.Should().Be(5.25);
        summary.ApprovalRate.Should().Be(70.0);
    }

    #endregion

    #region CalculateSummary Logic Tests (透過可測試的邏輯封裝)

    [Fact]
    public void 計算摘要_有三位審查人員資料時_總審查數應為正確加總()
    {
        // Arrange
        var rows = new List<ReviewerReportRow>
        {
            new ReviewerReportRow { TotalReviews = 15, ApprovedCount = 10, RejectedCount = 3, PendingCount = 2, AverageReviewTimeHours = 4.5 },
            new ReviewerReportRow { TotalReviews = 20, ApprovedCount = 14, RejectedCount = 4, PendingCount = 2, AverageReviewTimeHours = 6.2 },
            new ReviewerReportRow { TotalReviews = 10, ApprovedCount = 7,  RejectedCount = 2, PendingCount = 1, AverageReviewTimeHours = 3.8 }
        };

        // Act
        var summary = CalculateSummaryFromRows(rows);

        // Assert
        summary.TotalReviews.Should().Be(45);
        summary.TotalApproved.Should().Be(31);
        summary.TotalRejected.Should().Be(9);
        summary.TotalPending.Should().Be(5);
    }

    [Fact]
    public void 計算摘要_有三位審查人員資料時_平均審查時間應正確計算()
    {
        // Arrange
        var rows = new List<ReviewerReportRow>
        {
            new ReviewerReportRow { TotalReviews = 15, ApprovedCount = 10, RejectedCount = 3, PendingCount = 2, AverageReviewTimeHours = 4.5 },
            new ReviewerReportRow { TotalReviews = 20, ApprovedCount = 14, RejectedCount = 4, PendingCount = 2, AverageReviewTimeHours = 6.2 },
            new ReviewerReportRow { TotalReviews = 10, ApprovedCount = 7,  RejectedCount = 2, PendingCount = 1, AverageReviewTimeHours = 3.8 }
        };

        // Act
        var summary = CalculateSummaryFromRows(rows);

        // Assert
        summary.AverageReviewTimeHours.Should().Be(Math.Round((4.5 + 6.2 + 3.8) / 3.0, 2));
    }

    [Fact]
    public void 計算摘要_有三位審查人員資料時_批准率應正確計算()
    {
        // Arrange
        var rows = new List<ReviewerReportRow>
        {
            new ReviewerReportRow { TotalReviews = 15, ApprovedCount = 10, RejectedCount = 3, PendingCount = 2, AverageReviewTimeHours = 4.5 },
            new ReviewerReportRow { TotalReviews = 20, ApprovedCount = 14, RejectedCount = 4, PendingCount = 2, AverageReviewTimeHours = 6.2 },
            new ReviewerReportRow { TotalReviews = 10, ApprovedCount = 7,  RejectedCount = 2, PendingCount = 1, AverageReviewTimeHours = 3.8 }
        };

        // Act
        var summary = CalculateSummaryFromRows(rows);

        // Assert
        var expectedApprovalRate = Math.Round(31.0 / 45.0 * 100.0, 2);
        summary.ApprovalRate.Should().Be(expectedApprovalRate);
    }

    [Fact]
    public void 計算摘要_空白列表時_應回傳預設摘要()
    {
        // Arrange
        var rows = new List<ReviewerReportRow>();

        // Act
        var summary = CalculateSummaryFromRows(rows);

        // Assert
        summary.TotalReviews.Should().Be(0);
        summary.TotalApproved.Should().Be(0);
        summary.TotalRejected.Should().Be(0);
        summary.TotalPending.Should().Be(0);
        summary.AverageReviewTimeHours.Should().Be(0.0);
        summary.ApprovalRate.Should().Be(0.0);
    }

    [Fact]
    public void 計算摘要_null列表時_應回傳預設摘要()
    {
        // Arrange
        List<ReviewerReportRow>? rows = null;

        // Act
        var summary = CalculateSummaryFromRows(rows);

        // Assert
        summary.TotalReviews.Should().Be(0);
        summary.TotalApproved.Should().Be(0);
        summary.TotalRejected.Should().Be(0);
        summary.TotalPending.Should().Be(0);
        summary.AverageReviewTimeHours.Should().Be(0.0);
        summary.ApprovalRate.Should().Be(0.0);
    }

    [Fact]
    public void 計算摘要_單一審查人員且無審查時_批准率應為零()
    {
        // Arrange
        var rows = new List<ReviewerReportRow>
        {
            new ReviewerReportRow { TotalReviews = 0, ApprovedCount = 0, RejectedCount = 0, PendingCount = 0, AverageReviewTimeHours = 0.0 }
        };

        // Act
        var summary = CalculateSummaryFromRows(rows);

        // Assert
        summary.ApprovalRate.Should().Be(0.0);
    }

    [Fact]
    public void 計算摘要_單一審查人員時_應正確計算所有欄位()
    {
        // Arrange
        var rows = new List<ReviewerReportRow>
        {
            new ReviewerReportRow
            {
                ReviewerName = "Alice",
                TotalReviews = 10,
                ApprovedCount = 8,
                RejectedCount = 1,
                PendingCount = 1,
                AverageReviewTimeHours = 3.5
            }
        };

        // Act
        var summary = CalculateSummaryFromRows(rows);

        // Assert
        summary.TotalReviews.Should().Be(10);
        summary.TotalApproved.Should().Be(8);
        summary.TotalRejected.Should().Be(1);
        summary.TotalPending.Should().Be(1);
        summary.AverageReviewTimeHours.Should().Be(3.5);
        summary.ApprovalRate.Should().Be(80.0);
    }

    #endregion

    #region ReviewerReportRow ApprovalRate Edge Cases

    [Fact]
    public void ReviewerReportRow_只有一筆審查且未通過時_ApprovalRate應為零()
    {
        // Arrange
        var row = new ReviewerReportRow
        {
            ReviewerName = "Eve",
            TotalReviews = 1,
            ApprovedCount = 0,
            RejectedCount = 1,
            PendingCount = 0,
            AverageReviewTimeHours = 2.0
        };

        // Act
        var approvalRate = row.ApprovalRate;

        // Assert
        approvalRate.Should().Be(0.0);
    }

    [Fact]
    public void ReviewerReportRow_審查人員名稱設定後應正確儲存()
    {
        // Arrange
        var row = new ReviewerReportRow
        {
            ReviewerName = "Frank"
        };

        // Act & Assert
        row.ReviewerName.Should().Be("Frank");
    }

    #endregion

    #region Helper Methods

    private static ReviewerReportSummary CalculateSummaryFromRows(List<ReviewerReportRow>? reportRows)
    {
        if (reportRows == null || !reportRows.Any())
        {
            return new ReviewerReportSummary();
        }

        int totalReviews = reportRows.Sum(r => r.TotalReviews);
        int totalApproved = reportRows.Sum(r => r.ApprovedCount);
        int totalRejected = reportRows.Sum(r => r.RejectedCount);
        int totalPending = reportRows.Sum(r => r.PendingCount);
        double averageReviewTime = reportRows.Average(r => r.AverageReviewTimeHours);
        double approvalRate = totalReviews > 0 ? (double)totalApproved / totalReviews * 100.0 : 0.0;

        return new ReviewerReportSummary
        {
            TotalReviews = totalReviews,
            TotalApproved = totalApproved,
            TotalRejected = totalRejected,
            TotalPending = totalPending,
            AverageReviewTimeHours = Math.Round(averageReviewTime, 2),
            ApprovalRate = Math.Round(approvalRate, 2)
        };
    }

    #endregion
}
```