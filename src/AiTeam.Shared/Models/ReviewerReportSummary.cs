
```csharp
namespace AiTeam.Shared.Models;

public class ReviewerReportSummary
{
    public int ErrorCount { get; set; }
    public int WarningCount { get; set; }
    public int InfoCount { get; set; }
    public int Total => ErrorCount + WarningCount + InfoCount;
}
```