
```csharp
namespace AiTeam.Shared.Models;

public class ReviewerReportSummary
{
    public int ErrorCount { get; set; }
    public int WarningCount { get; set; }
    public int SuggestionCount { get; set; }
    public int TotalCount => ErrorCount + WarningCount + SuggestionCount;
}
```