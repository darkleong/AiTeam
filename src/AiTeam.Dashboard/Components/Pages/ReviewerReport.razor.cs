using Microsoft.AspNetCore.Components;

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

    private async Task LoadReportDataAsync()
    {
        // Simulate loading data
        await Task.Delay(0);

        reportRows = new List<ReviewerReportRow>
        {
            new ReviewerReportRow
            {
                ReviewerName = "Alice",
                TotalReviews = 15,
                ApprovedCount = 10,
                RejectedCount = 3,
                PendingCount = 2,
                AverageReviewTimeHours = 4.5
            },
            new ReviewerReportRow
            {
                ReviewerName = "Bob",
                TotalReviews = 20,
                ApprovedCount = 14,
                RejectedCount = 4,
                PendingCount = 2,
                AverageReviewTimeHours = 6.2
            },
            new ReviewerReportRow
            {
                ReviewerName = "Carol",
                TotalReviews = 10,
                ApprovedCount = 7,
                RejectedCount = 2,
                PendingCount = 1,
                AverageReviewTimeHours = 3.8
            }
        };
    }

    private void CalculateSummary()
    {
        if (reportRows == null || !reportRows.Any())
        {
            summary = new ReviewerReportSummary();
            return;
        }

        int totalReviews = reportRows.Sum(r => r.TotalReviews);
        int totalApproved = reportRows.Sum(r => r.ApprovedCount);
        int totalRejected = reportRows.Sum(r => r.RejectedCount);
        int totalPending = reportRows.Sum(r => r.PendingCount);
        double averageReviewTime = reportRows.Average(r => r.AverageReviewTimeHours);
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

public class ReviewerReportRow
{
    public string ReviewerName { get; set; } = string.Empty;
    public int TotalReviews { get; set; }
    public int ApprovedCount { get; set; }
    public int RejectedCount { get; set; }
    public int PendingCount { get; set; }
    public double AverageReviewTimeHours { get; set; }

    public double ApprovalRate =>
        TotalReviews > 0 ? Math.Round((double)ApprovedCount / TotalReviews * 100.0, 2) : 0.0;
}

public class ReviewerReportSummary
{
    public int TotalReviews { get; set; }
    public int TotalApproved { get; set; }
    public int TotalRejected { get; set; }
    public int TotalPending { get; set; }
    public double AverageReviewTimeHours { get; set; }
    public double ApprovalRate { get; set; }
}