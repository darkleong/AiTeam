
```csharp
using AiTeam.Dashboard.Models;
using AiTeam.Dashboard.Services;
using Microsoft.AspNetCore.Components;

namespace AiTeam.Dashboard.Pages;

public partial class ReviewerReport : ComponentBase
{
    [Inject]
    private IReviewerReportService ReviewerReportService { get; set; } = default!;

    private List<ReviewerReportItem> ReportItems { get; set; } = new();
    private ReviewerReportSummary Summary { get; set; } = new();
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
            ErrorMessage = $"載入報告資料時發生錯誤：{ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private static ReviewerReportSummary CalculateSummary(List<ReviewerReportItem> items)
    {
        var summary = new ReviewerReportSummary
        {
            TotalCount = items.Count,
            ErrorCount = items.Count(i => i.IssueType == IssueType.Error),
            WarningCount = items.Count(i => i.IssueType == IssueType.Warning),
            SuggestionCount = items.Count(i => i.IssueType == IssueType.Suggestion),
            InfoCount = items.Count(i => i.IssueType == IssueType.Info)
        };

        return summary;
    }

    private async Task RefreshAsync()
    {
        await LoadReportDataAsync();
    }
}
```