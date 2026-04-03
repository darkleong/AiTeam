
```csharp
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using System.Text.Json;
using System.Text.Json.Serialization;
using AiTeam.Dashboard.Services;

namespace AiTeam.Dashboard.Components.Pages;

public partial class ReviewerAgent : ComponentBase
{
    [Inject]
    private IReviewerAgentService ReviewerAgentService { get; set; } = default!;

    private string _codeInput = string.Empty;
    private bool _isLoading = false;
    private ReviewReport? _reviewReport = null;
    private string? _errorMessage = null;

    private async Task SubmitReview()
    {
        if (string.IsNullOrWhiteSpace(_codeInput))
        {
            _errorMessage = "請輸入要審查的程式碼";
            return;
        }

        _isLoading = true;
        _errorMessage = null;
        _reviewReport = null;

        try
        {
            _reviewReport = await ReviewerAgentService.ReviewCodeAsync(_codeInput);
        }
        catch (Exception ex)
        {
            _errorMessage = $"審查過程發生錯誤：{ex.Message}";
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void ClearInput()
    {
        _codeInput = string.Empty;
        _reviewReport = null;
        _errorMessage = null;
    }
}

public class ReviewReport
{
    [JsonPropertyName("issues")]
    public List<ReviewIssue> Issues { get; set; } = new();

    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;

    [JsonPropertyName("overallScore")]
    public int OverallScore { get; set; }

    public IssueSummaryStatistics Statistics => CalculateStatistics();

    private IssueSummaryStatistics CalculateStatistics()
    {
        var stats = new IssueSummaryStatistics();

        foreach (var issue in Issues)
        {
            switch (issue.Severity?.ToLower())
            {
                case "bug":
                case "error":
                    stats.BugCount++;
                    break;
                case "warning":
                    stats.WarningCount++;
                    break;
                case "info":
                case "information":
                    stats.InfoCount++;
                    break;
                case "suggestion":
                    stats.SuggestionCount++;
                    break;
                default:
                    stats.OtherCount++;
                    break;
            }
        }

        stats.TotalCount = Issues.Count;
        return stats;
    }
}

public class ReviewIssue
{
    [JsonPropertyName("severity")]
    public string Severity { get; set; } = string.Empty;

    [JsonPropertyName("line")]
    public int? Line { get; set; }

    [JsonPropertyName("column")]
    public int? Column { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("suggestion")]
    public string? Suggestion { get; set; }

    [JsonPropertyName("code")]
    public string? Code { get; set; }
}

public class IssueSummaryStatistics
{
    public int TotalCount { get; set; }
    public int BugCount { get; set; }
    public int WarningCount { get; set; }
    public int InfoCount { get; set; }
    public int SuggestionCount { get; set; }
    public int OtherCount { get; set; }

    public bool HasIssues => TotalCount > 0;

    public string GetSeverityCssClass(string severity)
    {
        return severity?.ToLower() switch
        {
            "bug" or "error" => "badge-danger",
            "warning" => "badge-warning",
            "info" or "information" => "badge-info",
            "suggestion" => "badge-success",
            _ => "badge-secondary"
        };
    }
}
```