
```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AiTeam.Shared.Models
{
    public class ReviewReport
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        
        public string Title { get; set; } = string.Empty;
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public string ReviewedBy { get; set; } = string.Empty;
        
        public string TargetFile { get; set; } = string.Empty;
        
        public string Content { get; set; } = string.Empty;
        
        public List<ReviewIssue> Issues { get; set; } = new List<ReviewIssue>();
        
        public ReviewSummary Summary { get; set; } = new ReviewSummary();
        
        public string Status { get; set; } = "Pending";
        
        public string ProjectId { get; set; } = string.Empty;
        
        public string PullRequestId { get; set; } = string.Empty;
        
        public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();

        public void RecalculateSummary()
        {
            Summary.RecalculateFromIssues(Issues);
        }

        public string GetReportWithSummaryLine()
        {
            var sb = new StringBuilder();

            if (!string.IsNullOrEmpty(Content))
            {
                sb.AppendLine(Content);
            }

            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine(Summary.SummaryLine);

            return sb.ToString();
        }
    }
    
    public class ReviewIssue
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        
        public IssueSeverity Severity { get; set; } = IssueSeverity.Info;
        
        public string Message { get; set; } = string.Empty;
        
        public string FilePath { get; set; } = string.Empty;
        
        public int? LineNumber { get; set; }
        
        public int? ColumnNumber { get; set; }
        
        public string Category { get; set; } = string.Empty;
        
        public string Suggestion { get; set; } = string.Empty;
        
        public string RuleId { get; set; } = string.Empty;
        
        public string CodeSnippet { get; set; } = string.Empty;
    }
    
    public class ReviewSummary
    {
        public int TotalIssues { get; set; } = 0;
        
        public int ErrorCount { get; set; } = 0;
        
        public int WarningCount { get; set; } = 0;
        
        public int InfoCount { get; set; } = 0;

        public int SuggestionCount { get; set; } = 0;

        public string SummaryLine => GenerateSummaryLine();
        
        public bool HasCriticalIssues => ErrorCount > 0;
        
        public string OverallStatus => DetermineOverallStatus();
        
        public Dictionary<string, int> IssuesByCategory { get; set; } = new Dictionary<string, int>();
        
        public Dictionary<string, int> IssuesByFile { get; set; } = new Dictionary<string, int>();
        
        private string GenerateSummaryLine()
        {
            var parts = new List<string>
            {
                $"共 {TotalIssues} 個問題"
            };

            if (ErrorCount > 0)
                parts.Add($"錯誤: {ErrorCount}");

            if (WarningCount > 0)
                parts.Add($"警告: {WarningCount}");

            if (SuggestionCount > 0)
                parts.Add($"建議: {SuggestionCount}");

            if (InfoCount > 0)
                parts.Add($"資訊: {InfoCount}");

            return "統計總結：" + string.Join(" | ", parts);
        }
        
        private string DetermineOverallStatus()
        {
            if (ErrorCount > 0) return "Failed";
            if (WarningCount > 0) return "Warning";
            if (SuggestionCount > 0) return "Passed with Suggestions";
            if (InfoCount > 0) return "Passed with Info";
            return "Passed";
        }
        
        public void RecalculateFromIssues(List<ReviewIssue> issues)
        {
            if (issues == null)
            {
                TotalIssues = 0;
                ErrorCount = 0;
                WarningCount = 0;
                InfoCount = 0;
                SuggestionCount = 0;
                IssuesByCategory = new Dictionary<string, int>();
                IssuesByFile = new Dictionary<string, int>();
                return;
            }
            
            TotalIssues = issues.Count;
            ErrorCount = 0;
            WarningCount = 0;
            InfoCount = 0;
            SuggestionCount = 0;
            IssuesByCategory = new Dictionary<string, int>();
            IssuesByFile = new Dictionary<string, int>();
            
            foreach (var issue in issues)
            {
                switch (issue.Severity)
                {
                    case IssueSeverity.Error:
                        ErrorCount++;
                        break;
                    case IssueSeverity.Warning:
                        WarningCount++;
                        break;
                    case IssueSeverity.Suggestion:
                        SuggestionCount++;
                        break;
                    case IssueSeverity.Info:
                        InfoCount++;
                        break;
                }
                
                if (!string.IsNullOrEmpty(issue.Category))
                {
                    if (IssuesByCategory.ContainsKey(issue.Category))
                        IssuesByCategory[issue.Category]++;
                    else
                        IssuesByCategory[issue.Category] = 1;
                }
                
                if (!string.IsNullOrEmpty(issue.FilePath))
                {
                    if (IssuesByFile.ContainsKey(issue.FilePath))
                        IssuesByFile[issue.FilePath]++;
                    else
                        IssuesByFile[issue.FilePath] = 1;
                }
            }
        }

        public string ToDetailedSummaryBlock()
        {
            var sb = new StringBuilder();
            sb.AppendLine("---");
            sb.AppendLine(SummaryLine);

            if (IssuesByFile.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("依檔案統計：");
                foreach (var kvp in IssuesByFile.OrderByDescending(x => x.Value))
                {
                    sb.AppendLine($"  {kvp.Key}: {kvp.Value} 個問題");
                }
            }

            if (IssuesByCategory.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("依類別統計：");
                foreach (var kvp in IssuesByCategory.OrderByDescending(x => x.Value))
                {
                    sb.AppendLine($"  {kvp.Key}: {kvp.Value} 個問題");
                }
            }

            sb.AppendLine($"整體狀態：{OverallStatus}");
            sb.Append("---");

            return sb.ToString();
        }
    }
    
    public enum IssueSeverity
    {
        Info = 0,
        Suggestion = 1,
        Warning = 2,
        Error = 3
    }
}
```