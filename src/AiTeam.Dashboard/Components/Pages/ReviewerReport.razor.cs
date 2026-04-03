using Microsoft.AspNetCore.Components;
using AiTeam.Dashboard.Models.ReviewerReport;
using AiTeam.Dashboard.Services;

namespace AiTeam.Dashboard.Components.Pages;

public partial class ReviewerReport : ComponentBase
{
    [Inject]
    private IReviewerReportService ReviewerReportService { get; set; } = default!;

    private List<ReviewerReportRow> reportRows = new();
    private ReviewerReportSummary summary = new();
    private bool isLoading = false;
    private string? errorMessage;

    protected override async Task OnInitializedAsync()
    {
        await LoadReportDataAsync();
        CalculateSummary();
    }

    private async Task LoadReportDataAsync()
    {
        isLoading = true;
        errorMessage = null;
        try
        {
            reportRows = await ReviewerReportService.GetReviewerReportRowsAsync();
        }
        catch (Exception ex)
        {
            errorMessage = $"載入資料時發生錯誤：{ex.Message}";
            reportRows = new List<ReviewerReportRow>();
        }
        finally
        {
            isLoading = false;
        }
    }

    private void CalculateSummary()
    {
        if (!reportRows.Any())
        {
            summary = new ReviewerReportSummary();
            return;
        }

        int totalReviews = 0;
        int totalApproved = 0;
        int totalRejected = 0;
        int totalPending = 0;
        double weightedReviewTimeSum = 0.0;

        foreach (var r in reportRows)
        {
            totalReviews += r.TotalReviews;
            totalApproved += r.ApprovedCount;
            totalRejected += r.RejectedCount;
            totalPending += r.PendingCount;
            weightedReviewTimeSum += r.AverageReviewTimeHours * r.TotalReviews;
        }

        double averageReviewTime = weightedReviewTimeSum / totalReviews;
        double approvalRate = (double)totalApproved / totalReviews * 100.0;

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