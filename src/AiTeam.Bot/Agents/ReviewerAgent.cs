using System.Text;
using System.Text.RegularExpressions;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace AiTeam.Bot.Agents
{
    public class ReviewerAgent
    {
        private const int MaxContextLength = 2000;

        private readonly Kernel _kernel;
        private readonly IChatCompletionService _chatCompletion;

        public ReviewerAgent(Kernel kernel)
        {
            _kernel = kernel;
            _chatCompletion = kernel.GetRequiredService<IChatCompletionService>();
        }

        public async Task<string> ReviewCodeAsync(string code, string context = "")
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(code);

            var sanitizedContext = string.IsNullOrEmpty(context)
                ? string.Empty
                : context.Length > MaxContextLength
                    ? context.Substring(0, MaxContextLength)
                    : context;

            var systemPrompt = @"你是一位資深的程式碼審查專家。請仔細審查提供的程式碼，並給出詳細的審查報告。

審查報告應包含以下幾個面向：
1. 程式碼品質
2. 安全性問題
3. 效能問題
4. 可維護性
5. 最佳實踐

對於每個問題，請標示嚴重程度：
- [CRITICAL] 或 [ERROR]：嚴重問題，必須修正
- [WARNING]：警告，建議修正
- [SUGGESTION]：建議改善項目

請用繁體中文回覆。";

            var userPrompt = string.IsNullOrEmpty(sanitizedContext)
                ? $"請審查以下程式碼：\n\n<code>\n{code}\n</code>"
                : $"背景資訊：\n<context>\n{sanitizedContext}\n</context>\n\n請審查以下程式碼：\n\n<code>\n{code}\n</code>";

            var chatHistory = new ChatHistory();
            chatHistory.AddSystemMessage(systemPrompt);
            chatHistory.AddUserMessage(userPrompt);

            var result = await _chatCompletion.GetChatMessageContentAsync(chatHistory);
            var reviewContent = result.Content ?? string.Empty;

            var fullReport = AssembleReport(reviewContent);
            return fullReport;
        }

        private string AssembleReport(string reviewContent)
        {
            var sb = new StringBuilder();
            sb.AppendLine(reviewContent);
            sb.AppendLine();

            var summary = GenerateStatisticsSummary(reviewContent);
            sb.Append(summary);

            return sb.ToString();
        }

        private string GenerateStatisticsSummary(string reviewContent)
        {
            int criticalCount = CountMatches(reviewContent, new[] { @"\[CRITICAL\]", @"\[ERROR\]" });
            int suggestionCount = CountMatches(reviewContent, new[] { @"\[WARNING\]", @"\[SUGGESTION\]" });

            var sb = new StringBuilder();
            sb.AppendLine("---");
            sb.AppendLine("## 📊 審查統計總結");
            sb.AppendLine();
            sb.AppendLine($"- 🔴 嚴重問題（Critical / Error）：**{criticalCount}** 項");
            sb.AppendLine($"- 🟡 建議改善（Warning / Suggestion）：**{suggestionCount}** 項");
            sb.AppendLine();

            if (criticalCount == 0 && suggestionCount == 0)
            {
                sb.AppendLine("> ✅ 本次審查未發現明顯問題，程式碼品質良好。");
            }
            else if (criticalCount > 0)
            {
                sb.AppendLine("> ⚠️ 本次審查發現嚴重問題，請優先處理標示為 CRITICAL / ERROR 的項目。");
            }
            else
            {
                sb.AppendLine("> 💡 本次審查無嚴重問題，建議參考 WARNING / SUGGESTION 項目進行改善。");
            }

            sb.AppendLine("---");

            return sb.ToString();
        }

        private int CountMatches(string content, string[] patterns)
        {
            int count = 0;
            foreach (var pattern in patterns)
            {
                var matches = Regex.Matches(content, pattern, RegexOptions.IgnoreCase);
                count += matches.Count;
            }
            return count;
        }
    }
}