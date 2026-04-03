
```csharp
using AiTeam.Dashboard.Models;
using Microsoft.AspNetCore.Components;

namespace AiTeam.Dashboard.Pages
{
    public partial class ReviewerReport : ComponentBase
    {
        [Parameter]
        public List<ReviewerIssue> Issues { get; set; } = new();

        public ReviewerSummaryStats SummaryStats { get; private set; } = new();

        protected override void OnParametersSet()
        {
            base.OnParametersSet();
            ComputeSummaryStats();
        }

        private void ComputeSummaryStats()
        {
            SummaryStats = new ReviewerSummaryStats
            {
                ErrorCount = Issues.Count(i => i.Severity == IssueSeverity.Error),
                WarningCount = Issues.Count(i => i.Severity == IssueSeverity.Warning),
                InfoCount = Issues.Count(i => i.Severity == IssueSeverity.Info),
                Total = Issues.Count
            };
        }
    }
}
```