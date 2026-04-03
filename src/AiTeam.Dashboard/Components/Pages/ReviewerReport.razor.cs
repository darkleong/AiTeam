using Microsoft.AspNetCore.Components;
using AiTeam.Dashboard.Models.ReviewerReport;

namespace AiTeam.Dashboard.Components.Pages;

public partial class ReviewerReport : ComponentBase
{
    private List<ReviewerReportRow> reportRows = new();
    private ReviewerReportSummary summary = new();

    protected override async Task OnInitializedAsync()
    {
        await LoadReportDataAsync();
        CalculateSummary();
    }

    #region STUB
    // TODO: 合併至主線分支前，必須將以下假資料替換為實際的資料來源（Service 注入或 API 呼叫）。
    // 此方法目前僅作為開發階段的暫時 Stub，禁止以此狀態合併至 main/production 分支。
    private Task LoadReportDataAsync()
    {
        reportRows = new List<ReviewerReportRow>
        {
            new() {
                ReviewerName = "Alice",
                TotalReviews = 15,
                ApprovedCount = 10,
                RejectedCount = 3,
                PendingCount = 2,
                AverageReviewTimeHours = 4.5
            },
            new() {
                ReviewerName = "Bob",
                TotalReviews = 20,
                ApprovedCount = 14,
                RejectedCount = 4,
                PendingCount = 2,
                AverageReviewTimeHours = 6.2
            },
            new() {
                ReviewerName = "Carol",
                TotalReviews = 10,
                ApprovedCount = 7,
                RejectedCount = 2,
                PendingCount = 1,
                AverageReviewTimeHours = 3.8
            }
        };

        return Task.CompletedTask;
    }
    #endregion

    private void CalculateSummary()
    {
        if (!reportRows.Any())
        {
            summary = new ReviewerReportSummary();
            return;
        }

        int totalReviews = reportRows.Sum(r => r.TotalReviews);
        int totalApproved = reportRows.Sum(r => r.ApprovedCount);
        int totalRejected = reportRows.Sum(r => r.RejectedCount);
        int totalPending = reportRows.Sum(r => r.PendingCount);
        double averageReviewTime = totalReviews > 0
            ? reportRows.Sum(r => r.AverageReviewTimeHours * r.TotalReviews) / totalReviews
            : 0.0;
        double approvalRate = totalReviews > 0 ? (double)totalApproved / totalReviews * 100.0 : 0.0;

        summary = new ReviewerReportSummary
        {
            TotalReviews = totalReviews,
            TotalApproved = totalApproved,
            TotalRejected = totalRejected,
            TotalPending = totalPending,
            AverageReviewTimeHours = Math.Round(averageReviewTime, 2),
            ApprovalRate = Math.Round(approvalRate, 2)
        };
    }
}