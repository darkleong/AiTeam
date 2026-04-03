
```csharp
using System;
using System.Collections.Generic;

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
        
        public string SummaryLine => GenerateSummaryLine();
        
        public bool HasCriticalIssues => ErrorCount > 0;
        
        public string OverallStatus => DetermineOverallStatus();
        
        public Dictionary<string, int> IssuesByCategory { get; set; } = new Dictionary<string, int>();
        
        public Dictionary<string, int> IssuesByFile { get; set; } = new Dictionary<string, int>();
        
        private string GenerateSummaryLine()
        {
            return $"統計總結：共 {TotalIssues} 個問題 | Error: {ErrorCount} | Warning: {WarningCount} | Info: {InfoCount}";
        }
        
        private string DetermineOverallStatus()
        {
            if (ErrorCount > 0) return "Failed";
            if (WarningCount > 0) return "Warning";
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
                IssuesByCategory = new Dictionary<string, int>();
                IssuesByFile = new Dictionary<string, int>();
                return;
            }
            
            TotalIssues = issues.Count;
            ErrorCount = 0;
            WarningCount = 0;
            InfoCount = 0;
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
    }
    
    public enum IssueSeverity
    {
        Info = 0,
        Warning = 1,
        Error = 2
    }
}
```