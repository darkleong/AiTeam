
```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace AiTeam.Bot.Agents
{
    public class ReviewIssue
    {
        public string Type { get; set; } = string.Empty; // Bug, Warning, Info, etc.
        public string Severity { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public int? LineNumber { get; set; }
        public string Suggestion { get; set; } = string.Empty;
    }

    public class ReviewIssueSummary
    {
        public int BugCount { get; set; }
        public int WarningCount { get; set; }
        public int InfoCount { get; set; }
        public int TotalCount { get; set; }
        public Dictionary<string, int> IssueCountByType { get; set; } = new Dictionary<string, int>();

        public string RenderStatisticsRow()
        {
            var sb = new StringBuilder();
            sb.AppendLine("## 問題總結統計");
            sb.AppendLine();
            sb.AppendLine("| 類型 | 數量 |");
            sb.AppendLine("|------|------|");
            
            foreach (var kvp in IssueCountByType.OrderByDescending(x => x.Value))
            {
                var emoji = kvp.Key.ToLower() switch
                {
                    "bug" => "🐛",
                    "warning" => "⚠️",
                    "info" => "ℹ️",
                    "error" => "❌",
                    "suggestion" => "💡",
                    _ => "📌"
                };
                sb.AppendLine($"| {emoji} {kvp.Key} | {kvp.Value} |");
            }
            
            sb.AppendLine($"| **總計** | **{TotalCount}** |");
            
            return sb.ToString();
        }
    }

    public class ReviewReport
    {
        public string Title { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public List<ReviewIssue> Issues { get; set; } = new List<ReviewIssue>();
        public ReviewIssueSummary IssueSummary { get; set; } = new ReviewIssueSummary();
        public string OverallAssessment { get; set; } = string.Empty;
        public string Recommendations { get; set; } = string.Empty;
        public DateTime ReviewedAt { get; set; } = DateTime.UtcNow;

        public void CalculateStatistics()
        {
            IssueSummary.IssueCountByType.Clear();
            
            foreach (var issue in Issues)
            {
                var type = string.IsNullOrWhiteSpace(issue.Type) ? "Other" : issue.Type;
                
                if (!IssueSummary.IssueCountByType.ContainsKey(type))
                {
                    IssueSummary.IssueCountByType[type] = 0;
                }
                IssueSummary.IssueCountByType[type]++;
            }
            
            IssueSummary.BugCount = IssueSummary.IssueCountByType.GetValueOrDefault("Bug", 0);
            IssueSummary.WarningCount = IssueSummary.IssueCountByType.GetValueOrDefault("Warning", 0);
            IssueSummary.InfoCount = IssueSummary.IssueCountByType.GetValueOrDefault("Info", 0);
            IssueSummary.TotalCount = Issues.Count;
        }

        public string RenderReport()
        {
            var sb = new StringBuilder();
            
            sb.AppendLine($"# {Title}");
            sb.AppendLine();
            sb.AppendLine($"**審查時間：** {ReviewedAt:yyyy-MM-dd HH:mm:ss} UTC");
            sb.AppendLine();
            
            if (!string.IsNullOrWhiteSpace(Summary))
            {
                sb.AppendLine("## 摘要");
                sb.AppendLine(Summary);
                sb.AppendLine();
            }
            
            if (Issues.Any())
            {
                sb.AppendLine("## 發現的問題");
                sb.AppendLine();
                
                var issuesByType = Issues.GroupBy(i => i.Type ?? "Other")
                                         .OrderBy(g => g.Key);
                
                foreach (var group in issuesByType)
                {
                    sb.AppendLine($"### {group.Key}");
                    sb.AppendLine();
                    
                    foreach (var issue in group)
                    {
                        sb.AppendLine($"- **描述：** {issue.Description}");
                        
                        if (!string.IsNullOrWhiteSpace(issue.FilePath))
                        {
                            var location = issue.LineNumber.HasValue 
                                ? $"{issue.FilePath}:{issue.LineNumber}" 
                                : issue.FilePath;
                            sb.AppendLine($"  - **位置：** {location}");
                        }
                        
                        if (!string.IsNullOrWhiteSpace(issue.Suggestion))
                        {
                            sb.AppendLine($"  - **建議：** {issue.Suggestion}");
                        }
                        
                        sb.AppendLine();
                    }
                }
            }
            
            if (!string.IsNullOrWhiteSpace(OverallAssessment))
            {
                sb.AppendLine("## 整體評估");
                sb.AppendLine(OverallAssessment);
                sb.AppendLine();
            }
            
            if (!string.IsNullOrWhiteSpace(Recommendations))
            {
                sb.AppendLine("## 建議事項");
                sb.AppendLine(Recommendations);
                sb.AppendLine();
            }
            
            // 在報告底部新增問題總結統計行
            if (IssueSummary.TotalCount > 0)
            {
                sb.AppendLine("---");
                sb.AppendLine();
                sb.Append(IssueSummary.RenderStatisticsRow());
            }
            
            return sb.ToString();
        }
    }

    public class ReviewerAgent
    {
        private readonly Kernel _kernel;
        private readonly IChatCompletionService _chatCompletionService;
        private const string SystemPrompt = @"你是一位專業的程式碼審查專家（Code Reviewer）。
你的任務是對提交的程式碼進行詳細審查，並產生結構化的審查報告。

在審查時，請特別注意：
1. **Bug（錯誤）**：可能導致程式崩潰或功能異常的問題
2. **Warning（警告）**：潛在的問題或不良實踐，雖然不會立即導致錯誤
3. **Info（資訊）**：改進建議、程式碼風格問題或最佳實踐提示
4. **Security（安全性）**：安全漏洞或潛在的安全風險
5. **Performance（效能）**：效能問題或可以優化的部分

請以 JSON 格式回傳審查結果，格式如下：
{
  ""title"": ""程式碼審查報告"",
  ""summary"": ""整體摘要"",
  ""issues"": [
    {
      ""type"": ""Bug/Warning/Info/Security/Performance"",
      ""severity"": ""High/Medium/Low"",
      ""description"": ""問題描述"",
      ""filePath"": ""檔案路徑（若有）"",
      ""lineNumber"": null,
      ""suggestion"": ""改善建議""
    }
  ],
  ""overallAssessment"": ""整體評估"",
  ""recommendations"": ""主要建議事項""
}";

        public ReviewerAgent(Kernel kernel)
        {
            _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
            _chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();
        }

        public async Task<ReviewReport> ReviewCodeAsync(string code, string? context = null)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                throw new ArgumentException("程式碼不能為空", nameof(code));
            }

            var chatHistory = new ChatHistory();
            chatHistory.AddSystemMessage(SystemPrompt);

            var userMessage = BuildUserMessage(code, context);
            chatHistory.AddUserMessage(userMessage);

            var executionSettings = new OpenAIPromptExecutionSettings
            {
                MaxTokens = 4096,
                Temperature = 0.1,
                ResponseFormat = "json_object"
            };

            try
            {
                var response = await _chatCompletionService.GetChatMessageContentAsync(
                    chatHistory,
                    executionSettings,
                    _kernel);

                var reportJson = response.Content ?? string.Empty;
                var report = ParseReviewReport(reportJson);
                
                // 計算統計數據
                report.CalculateStatistics();
                
                return report;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"審查過程中發生錯誤：{ex.Message}", ex);
            }
        }

        public async Task<ReviewReport> ReviewCodeChangesAsync(
            string originalCode, 
            string modifiedCode, 
            string? context = null)
        {
            if (string.IsNullOrWhiteSpace(originalCode))
            {
                throw new ArgumentException("原始程式碼不能為空", nameof(originalCode));
            }

            if (string.IsNullOrWhiteSpace(modifiedCode))
            {
                throw new ArgumentException("修改後的程式碼不能為空", nameof(modifiedCode));
            }

            var chatHistory = new ChatHistory();
            chatHistory.AddSystemMessage(SystemPrompt);

            var userMessage = BuildCodeChangesMessage(originalCode, modifiedCode, context);
            chatHistory.AddUserMessage(userMessage);

            var executionSettings = new OpenAIPromptExecutionSettings
            {
                MaxTokens = 4096,
                Temperature = 0.1,
                ResponseFormat = "json_object"
            };

            try
            {
                var response = await _chatCompletionService.GetChatMessageContentAsync(
                    chatHistory,
                    executionSettings,
                    _kernel);

                var reportJson = response.Content ?? string.Empty;
                var report = ParseReviewReport(reportJson);
                
                // 計算統計數據
                report.CalculateStatistics();
                
                return report;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"審查程式碼變更時發生錯誤：{ex.Message}", ex);
            }
        }

        public async IAsyncEnumerable<string> StreamReviewAsync(string code, string? context = null)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                throw new ArgumentException("程式碼不能為空", nameof(code));
            }

            var chatHistory = new ChatHistory();
            chatHistory.AddSystemMessage(@"你是一位專業的程式碼審查專家。請對以下程式碼進行詳細審查，
以清晰的繁體中文說明發現的問題，包括 Bug、Warning、Info 等各類問題，
並在最後提供問題統計摘要。");

            var userMessage = BuildUserMessage(code, context);
            chatHistory.AddUserMessage(userMessage);

            var executionSettings = new OpenAIPromptExecutionSettings
            {
                MaxTokens = 4096,
                Temperature = 0.1
            };

            await foreach (var content in _chatCompletionService.GetStreamingChatMessageContentsAsync(
                chatHistory,
                executionSettings,
                _kernel))
            {
                if (content.Content != null)
                {
                    yield return content.Content;
                }
            }
        }

        private static string BuildUserMessage(string code, string? context)
        {
            var sb = new StringBuilder();
            
            if (!string.IsNullOrWhiteSpace(context))
            {
                sb.AppendLine("## 審查背景");
                sb.AppendLine(context);
                sb.AppendLine();
            }
            
            sb.AppendLine("## 待審查的程式碼");
            sb.AppendLine("```");
            sb.AppendLine(code);
            sb.AppendLine("```");
            sb.AppendLine();
            sb.AppendLine("請對以上程式碼進行詳細審查，找出所有潛在問題並提供改善建議。");
            
            return sb.ToString();
        }

        private static string BuildCodeChangesMessage(
            string originalCode, 
            string modifiedCode, 
            string? context)
        {
            var sb = new StringBuilder();
            
            if (!string.IsNullOrWhiteSpace(context))
            {
                sb.AppendLine("## 審查背景");
                sb.AppendLine(context);
                sb.AppendLine();
            }
            
            sb.AppendLine("## 原始程式碼");
            sb.AppendLine("```");
            sb.AppendLine(originalCode);
            sb.AppendLine("```");
            sb.AppendLine();
            sb.AppendLine("## 修改後的程式碼");
            sb.AppendLine("```");
            sb.AppendLine(modifiedCode);
            sb.AppendLine("```");
            sb.AppendLine();
            sb.AppendLine("請審查程式碼的變更，評估修改是否適當，找出任何新引入的問題或改善的機會。");
            
            return sb.ToString();
        }

        private static ReviewReport ParseReviewReport(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return CreateDefaultReport("無法解析審查結果：回應為空");
            }

            try
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var jsonDoc = JsonDocument.Parse(json);
                var root = jsonDoc.RootElement;

                var report = new ReviewReport
                {
                    Title = GetStringProperty(root, "title") ?? "程式碼審查報告",
                    Summary = GetStringProperty(root, "summary") ?? string.Empty,
                    OverallAssessment = GetStringProperty(root, "overallAssessment") ?? string.Empty,
                    Recommendations = GetStringProperty(root, "recommendations") ?? string.Empty,
                    ReviewedAt = DateTime.UtcNow
                };

                if (root.TryGetProperty("issues", out var issuesElement) && 
                    issuesElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var issueElement in issuesElement.EnumerateArray())
                    {
                        var issue = new ReviewIssue
                        {
                            Type = GetStringProperty(issueElement, "type") ?? "Info",
                            Severity = GetStringProperty(issueElement, "severity") ?? "Low",
                