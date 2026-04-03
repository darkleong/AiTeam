我需要查看規格文件和相關檔案才能正確實作。由於沒有現有程式碼和規格文件內容，我將根據任務描述建立一個合理的實作：

```csharp
using AiTeam.Dashboard.Components.Pages.Base;
using AiTeam.Dashboard.Models;
using AiTeam.Dashboard.Services;
using Microsoft.AspNetCore.Components;

namespace AiTeam.Dashboard.Components.Pages;

public partial class ReviewerReport : BasePage
{
    [Inject]
    private IReviewerReportService ReviewerReportService { get; set; } = default!;

    private List<ReviewerReportItem> ReportItems { get; set; } = new();
    private ReviewerReportSummary? Summary { get; set; }
    private bool IsLoading { get; set; } = true;
    private string? ErrorMessage { get; set; }

    protected override async Task OnInitializedAsync()
    {
        await LoadReportDataAsync();
    }

    private async Task LoadReportDataAsync()
    {
        try
        {
            IsLoading = true;
            ErrorMessage = null;

            ReportItems = await ReviewerReportService.GetReportItemsAsync();
            Summary = CalculateSummary(ReportItems);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"載入資料時發生錯誤：{ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private static ReviewerReportSummary CalculateSummary(List<ReviewerReportItem> items)
    {
        if (items == null || items.Count == 0)
        {
            return new ReviewerReportSummary();
        }

        return new ReviewerReportSummary
        {
            TotalCount = items.Count,
            TotalReviewCount = items.Sum(x => x.ReviewCount),
            AverageReviewCount = Math.Round(items.Average(x => x.ReviewCount), 2),
            TotalApprovedCount = items.Sum(x => x.ApprovedCount),
            AverageApprovedCount = Math.Round(items.Average(x => x.ApprovedCount), 2),
            TotalRejectedCount = items.Sum(x => x.RejectedCount),
            AverageRejectedCount = Math.Round(items.Average(x => x.RejectedCount), 2),
            TotalCommentCount = items.Sum(x => x.CommentCount),
            AverageCommentCount = Math.Round(items.Average(x => x.CommentCount), 2),
            AverageApprovalRate = items.Any(x => x.ReviewCount > 0)
                ? Math.Round(items.Where(x => x.ReviewCount > 0)
                    .Average(x => (double)x.ApprovedCount / x.ReviewCount * 100), 2)
                : 0
        };
    }

    private async Task RefreshAsync()
    {
        await LoadReportDataAsync();
    }
}
```