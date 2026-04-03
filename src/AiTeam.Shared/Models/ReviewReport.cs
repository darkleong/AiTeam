
```csharp
using System.Collections.Generic;

namespace AiTeam.Shared.Models
{
    public class ReviewReport
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public System.DateTime CreatedAt { get; set; } = System.DateTime.UtcNow;
        public List<ReviewIssue> Issues { get; set; } = new List<ReviewIssue>();
        public ReviewIssueSummary Summary { get; set; } = new ReviewIssueSummary();
    }

    public class ReviewIssue
    {
        public string Id { get; set; } = string.Empty;
        public ReviewIssueType Type { get; set; } = ReviewIssueType.Info;
        public string Description { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public int? LineNumber { get; set; }
        public string Suggestion { get; set; } = string.Empty;
    }

    public enum ReviewIssueType
    {
        Bug,
        Warning,
        Info,
        Security,
        Performance,
        Style
    }

    public class ReviewIssueSummary
    {
        public int TotalCount { get; set; }
        public int BugCount { get; set; }
        public int WarningCount { get; set; }
        public int InfoCount { get; set; }
        public int SecurityCount { get; set; }
        public int PerformanceCount { get; set; }
        public int StyleCount { get; set; }

        public static ReviewIssueSummary FromIssues(List<ReviewIssue> issues)
        {
            var summary = new ReviewIssueSummary
            {
                TotalCount = issues.Count
            };

            foreach (var issue in issues)
            {
                switch (issue.Type)
                {
                    case ReviewIssueType.Bug:
                        summary.BugCount++;
                        break;
                    case ReviewIssueType.Warning:
                        summary.WarningCount++;
                        break;
                    case ReviewIssueType.Info:
                        summary.InfoCount++;
                        break;
                    case ReviewIssueType.Security:
                        summary.SecurityCount++;
                        break;
                    case ReviewIssueType.Performance:
                        summary.PerformanceCount++;
                        break;
                    case ReviewIssueType.Style:
                        summary.StyleCount++;
                        break;
                }
            }

            return summary;
        }

        public Dictionary<string, int> ToDictionary()
        {
            return new Dictionary<string, int>
            {
                { "Total", TotalCount },
                { "Bug", BugCount },
                { "Warning", WarningCount },
                { "Info", InfoCount },
                { "Security", SecurityCount },
                { "Performance", PerformanceCount },
                { "Style", StyleCount }
            };
        }
    }
}
```