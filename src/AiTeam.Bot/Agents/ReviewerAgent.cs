
```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AiTeam.Bot.Models;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace AiTeam.Bot.Agents
{
    public class ReviewerAgent
    {
        private readonly Kernel _kernel;
        private readonly ILogger<ReviewerAgent> _logger;

        public ReviewerAgent(Kernel kernel, ILogger<ReviewerAgent> logger)
        {
            _kernel = kernel;
            _logger = logger;
        }

        public async Task<ReviewReport> ReviewAsync(string code, string context = "")
        {
            _logger.LogInformation("Starting code review...");

            var prompt = BuildReviewPrompt(code, context);
            var response = await _kernel.InvokePromptAsync(prompt);
            var reportContent = response.ToString();

            var report = ParseReport(reportContent, code);

            _logger.LogInformation("Code review completed. Errors: {Errors}, Warnings: {Warnings}, Infos: {Infos}",
                report.ErrorCount, report.WarningCount, report.InfoCount);

            return report;
        }

        private string BuildReviewPrompt(string code, string context)
        {
            var sb = new StringBuilder();
            sb.AppendLine("你是一位資深程式碼審查員。請對以下程式碼進行詳細審查，並以結構化格式回報問題。");
            sb.AppendLine();

            if (!string.IsNullOrEmpty(context))
            {
                sb.AppendLine($"背景資訊：{context}");
                sb.AppendLine();
            }

            sb.AppendLine("請按照以下格式回報每個問題：");
            sb.AppendLine("[ERROR] <問題描述>");
            sb.AppendLine("[WARNING] <問題描述>");
            sb.AppendLine("[INFO] <問題描述>");
            sb.AppendLine();
            sb.AppendLine("嚴重等級說明：");
            sb.AppendLine("- ERROR: 嚴重問題，必須修正（如安全漏洞、記憶體洩漏、邏輯錯誤）");
            sb.AppendLine("- WARNING: 警告問題，建議修正（如效能問題、不良實踐、潛在錯誤）");
            sb.AppendLine("- INFO: 資訊提示，可選擇性改善（如程式碼風格、文件建議）");
            sb.AppendLine();
            sb.AppendLine("待審查的程式碼：");
            sb.AppendLine("```");
            sb.AppendLine(code);
            sb.AppendLine("```");
            sb.AppendLine();
            sb.AppendLine("請提供詳細的審查報告，包含所有發現的問題。");

            return sb.ToString();
        }

        private ReviewReport ParseReport(string reportContent, string originalCode)
        {
            var issues = new List<ReviewIssue>();
            var lines = reportContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            var errorPattern = new Regex(@"^\[ERROR\]\s*(.+)$", RegexOptions.IgnoreCase);
            var warningPattern = new Regex(@"^\[WARNING\]\s*(.+)$", RegexOptions.IgnoreCase);
            var infoPattern = new Regex(@"^\[INFO\]\s*(.+)$", RegexOptions.IgnoreCase);

            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();

                var errorMatch = errorPattern.Match(trimmedLine);
                if (errorMatch.Success)
                {
                    issues.Add(new ReviewIssue
                    {
                        Severity = IssueSeverity.Error,
                        Description = errorMatch.Groups[1].Value.Trim()
                    });
                    continue;
                }

                var warningMatch = warningPattern.Match(trimmedLine);
                if (warningMatch.Success)
                {
                    issues.Add(new ReviewIssue
                    {
                        Severity = IssueSeverity.Warning,
                        Description = warningMatch.Groups[1].Value.Trim()
                    });
                    continue;
                }

                var infoMatch = infoPattern.Match(trimmedLine);
                if (infoMatch.Success)
                {
                    issues.Add(new ReviewIssue
                    {
                        Severity = IssueSeverity.Info,
                        Description = infoMatch.Groups[1].Value.Trim()
                    });
                }
            }

            var errorCount = issues.Count(i => i.Severity == IssueSeverity.Error);
            var warningCount = issues.Count(i => i.Severity == IssueSeverity.Warning);
            var infoCount = issues.Count(i => i.Severity == IssueSeverity.Info);

            var summaryLine = BuildSummaryLine(errorCount, warningCount, infoCount);
            var fullContent = AppendSummaryToReport(reportContent, summaryLine);

            return new ReviewReport
            {
                Content = fullContent,
                OriginalCode = originalCode,
                Issues = issues,
                ErrorCount = errorCount,
                WarningCount = warningCount,
                InfoCount = infoCount,
                TotalIssueCount = issues.Count,
                SummaryLine = summaryLine,
                ReviewedAt = DateTime.UtcNow
            };
        }

        private string BuildSummaryLine(int errorCount, int warningCount, int infoCount)
        {
            return $"統計總結：Error {errorCount} 個，Warning {warningCount} 個，Info {infoCount} 個，共 {errorCount + warningCount + infoCount} 個問題";
        }

        private string AppendSummaryToReport(string reportContent, string summaryLine)
        {
            var sb = new StringBuilder(reportContent);

            if (!reportContent.EndsWith("\n"))
            {
                sb.AppendLine();
            }

            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine(summaryLine);

            return sb.ToString();
        }
    }
}
```