```csharp
using Microsoft.AspNetCore.Components;

namespace AiTeam.Dashboard.Pages
{
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
            await Task.Delay(100);

            reportRows = new List<ReviewerReportRow>
            {
                new ReviewerReportRow
                {
                    ReviewerName = "Alice",
                    TotalReviews = 25,
                    PassedReviews = 20,
                    ReturnedReviews = 5,
                    AverageReviewTime = 2.5,
                    PendingReviews = 3
                },
                new ReviewerReportRow
                {
                    ReviewerName = "Bob",
                    TotalReviews = 30,
                    PassedReviews = 22,
                    ReturnedReviews = 8,
                    AverageReviewTime = 3.2,
                    PendingReviews = 5
                },
                new ReviewerReportRow
                {
                    ReviewerName = "Charlie",
                    TotalReviews = 18,
                    PassedReviews = 15,
                    ReturnedReviews = 3,
                    AverageReviewTime = 1.8,
                    PendingReviews = 2
                },
                new ReviewerReportRow
                {
                    ReviewerName = "Diana",
                    TotalReviews = 40,
                    PassedReviews = 35,
                    ReturnedReviews = 5,
                    AverageReviewTime = 2.1,
                    PendingReviews = 7
                },
                new ReviewerReportRow
                {
                    ReviewerName = "Eve",
                    TotalReviews = 22,
                    PassedReviews = 18,
                    ReturnedReviews = 4,
                    AverageReviewTime = 2.8,
                    PendingReviews = 1
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
            int totalPassedReviews = reportRows.Sum(r => r.PassedReviews);
            int totalReturnedReviews = reportRows.Sum(r => r.ReturnedReviews);
            int totalPendingReviews = reportRows.Sum(r => r.PendingReviews);

            // 加權平均：各審查者的平均審查時間以其 TotalReviews 加權
            double weightedAverageReviewTime = totalReviews > 0
                ? reportRows.Sum(r => r.AverageReviewTime * r.TotalReviews) / totalReviews
                : 0;

            double overallPassRate = totalReviews > 0
                ? (double)totalPassedReviews / totalReviews * 100
                : 0;

            summary = new ReviewerReportSummary
            {
                TotalReviewers = reportRows.Count,
                TotalReviews = totalReviews,
                TotalPassedReviews = totalPassedReviews,
                TotalReturnedReviews = totalReturnedReviews,
                AverageReviewTime = weightedAverageReviewTime,
                TotalPendingReviews = totalPendingReviews,
                OverallPassRate = overallPassRate
            };
        }
    }

    public class ReviewerReportRow
    {
        public string ReviewerName { get; set; } = string.Empty;
        public int TotalReviews { get; set; }
        public int PassedReviews { get; set; }
        public int ReturnedReviews { get; set; }
        public double AverageReviewTime { get; set; }
        public int PendingReviews { get; set; }

        public double PassRate => TotalReviews > 0 ? (double)PassedReviews / TotalReviews * 100 : 0;
    }

    public class ReviewerReportSummary
    {
        public int TotalReviewers { get; set; }
        public int TotalReviews { get; set; }
        public int TotalPassedReviews { get; set; }
        public int TotalReturnedReviews { get; set; }
        public double AverageReviewTime { get; set; }
        public int TotalPendingReviews { get; set; }
        public double OverallPassRate { get; set; }
    }
}
```