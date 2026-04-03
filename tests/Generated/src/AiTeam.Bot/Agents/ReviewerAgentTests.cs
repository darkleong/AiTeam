```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using AiTeam.Bot.Agents;
using FluentAssertions;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using NSubstitute;
using Xunit;

namespace AiTeam.Bot.Tests.Agents
{
    public class ReviewerAgentTests
    {
        private readonly IChatCompletionService _chatCompletionService;
        private readonly Kernel _kernel;
        private readonly ReviewerAgent _sut;

        public ReviewerAgentTests()
        {
            _chatCompletionService = Substitute.For<IChatCompletionService>();

            var kernelBuilder = Kernel.CreateBuilder();
            kernelBuilder.Services.AddSingleton(_chatCompletionService);
            _kernel = kernelBuilder.Build();

            _sut = new ReviewerAgent(_kernel);
        }

        #region ReviewCodeAsync 測試

        [Fact]
        public async Task 審查程式碼_無背景資訊_回傳包含審查內容的報告()
        {
            // Arrange
            var code = "public void Foo() { var x = 1; }";
            var fakeReviewContent = "程式碼品質良好，沒有發現問題。";

            var chatMessageContent = new ChatMessageContent(AuthorRole.Assistant, fakeReviewContent);

            _chatCompletionService
                .GetChatMessageContentAsync(
                    Arg.Any<ChatHistory>(),
                    Arg.Any<PromptExecutionSettings>(),
                    Arg.Any<Kernel>(),
                    Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(chatMessageContent));

            // Act
            var result = await _sut.ReviewCodeAsync(code);

            // Assert
            result.Should().NotBeNullOrEmpty();
            result.Should().Contain(fakeReviewContent);
        }

        [Fact]
        public async Task 審查程式碼_提供背景資訊_回傳包含審查統計的報告()
        {
            // Arrange
            var code = "public void Bar() { }";
            var context = "這是一個工具方法";
            var fakeReviewContent = "程式碼結構清楚。";

            var chatMessageContent = new ChatMessageContent(AuthorRole.Assistant, fakeReviewContent);

            _chatCompletionService
                .GetChatMessageContentAsync(
                    Arg.Any<ChatHistory>(),
                    Arg.Any<PromptExecutionSettings>(),
                    Arg.Any<Kernel>(),
                    Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(chatMessageContent));

            // Act
            var result = await _sut.ReviewCodeAsync(code, context);

            // Assert
            result.Should().NotBeNullOrEmpty();
            result.Should().Contain("審查統計總結");
        }

        [Fact]
        public async Task 審查程式碼_回覆包含CRITICAL標記_報告顯示嚴重問題計數大於零()
        {
            // Arrange
            var code = "var password = \"123456\";";
            var fakeReviewContent = "[CRITICAL] 密碼不應硬編碼。\n[ERROR] 缺少輸入驗證。";

            var chatMessageContent = new ChatMessageContent(AuthorRole.Assistant, fakeReviewContent);

            _chatCompletionService
                .GetChatMessageContentAsync(
                    Arg.Any<ChatHistory>(),
                    Arg.Any<PromptExecutionSettings>(),
                    Arg.Any<Kernel>(),
                    Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(chatMessageContent));

            // Act
            var result = await _sut.ReviewCodeAsync(code);

            // Assert
            result.Should().Contain("嚴重問題");
            result.Should().Contain("**2**");
            result.Should().Contain("請優先處理標示為 CRITICAL / ERROR 的項目");
        }

        [Fact]
        public async Task 審查程式碼_回覆包含WARNING標記_報告顯示建議改善計數大於零()
        {
            // Arrange
            var code = "public int Add(int a, int b) { return a + b; }";
            var fakeReviewContent = "[WARNING] 缺少 XML 文件註解。\n[SUGGESTION] 可考慮加入單元測試。";

            var chatMessageContent = new ChatMessageContent(AuthorRole.Assistant, fakeReviewContent);

            _chatCompletionService
                .GetChatMessageContentAsync(
                    Arg.Any<ChatHistory>(),
                    Arg.Any<PromptExecutionSettings>(),
                    Arg.Any<Kernel>(),
                    Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(chatMessageContent));

            // Act
            var result = await _sut.ReviewCodeAsync(code);

            // Assert
            result.Should().Contain("建議改善");
            result.Should().Contain("**2**");
            result.Should().Contain("建議參考 WARNING / SUGGESTION 項目進行改善");
        }

        [Fact]
        public async Task 審查程式碼_回覆無任何問題標記_報告顯示程式碼品質良好()
        {
            // Arrange
            var code = "public string GetName() => \"Alice\";";
            var fakeReviewContent = "程式碼簡潔易讀，無明顯問題。";

            var chatMessageContent = new ChatMessageContent(AuthorRole.Assistant, fakeReviewContent);

            _chatCompletionService
                .GetChatMessageContentAsync(
                    Arg.Any<ChatHistory>(),
                    Arg.Any<PromptExecutionSettings>(),
                    Arg.Any<Kernel>(),
                    Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(chatMessageContent));

            // Act
            var result = await _sut.ReviewCodeAsync(code);

            // Assert
            result.Should().Contain("未發現明顯問題，程式碼品質良好");
            result.Should().Contain("**0**");
        }

        [Fact]
        public async Task 審查程式碼_ChatCompletion回傳空內容_報告仍包含統計區塊()
        {
            // Arrange
            var code = "// empty";
            var chatMessageContent = new ChatMessageContent(AuthorRole.Assistant, null);

            _chatCompletionService
                .GetChatMessageContentAsync(
                    Arg.Any<ChatHistory>(),
                    Arg.Any<PromptExecutionSettings>(),
                    Arg.Any<Kernel>(),
                    Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(chatMessageContent));

            // Act
            var result = await _sut.ReviewCodeAsync(code);

            // Assert
            result.Should().NotBeNull();
            result.Should().Contain("審查統計總結");
            result.Should().Contain("未發現明顯問題，程式碼品質良好");
        }

        [Fact]
        public async Task 審查程式碼_背景資訊為空字串_與無背景資訊行為一致不拋出例外()
        {
            // Arrange
            var code = "public void Test() { }";
            var fakeReviewContent = "看起來沒問題。";
            var chatMessageContent = new ChatMessageContent(AuthorRole.Assistant, fakeReviewContent);

            _chatCompletionService
                .GetChatMessageContentAsync(
                    Arg.Any<ChatHistory>(),
                    Arg.Any<PromptExecutionSettings>(),
                    Arg.Any<Kernel>(),
                    Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(chatMessageContent));

            // Act
            Func<Task> act = async () => await _sut.ReviewCodeAsync(code, string.Empty);

            // Assert
            await act.Should().NotThrowAsync();
        }

        [Fact]
        public async Task 審查程式碼_回覆包含大小寫混合的CRITICAL標記_仍能正確計數()
        {
            // Arrange
            var code = "var x = eval(input);";
            var fakeReviewContent = "[critical] 使用 eval 非常危險。\n[Critical] 另一個嚴重問題。";

            var chatMessageContent = new ChatMessageContent(AuthorRole.Assistant, fakeReviewContent);

            _chatCompletionService
                .GetChatMessageContentAsync(
                    Arg.Any<ChatHistory>(),
                    Arg.Any<PromptExecutionSettings>(),
                    Arg.Any<Kernel>(),
                    Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(chatMessageContent));

            // Act
            var result = await _sut.ReviewCodeAsync(code);

            // Assert
            result.Should().Contain("**2**");
            result.Should().Contain("請優先處理標示為 CRITICAL / ERROR 的項目");
        }

        [Fact]
        public async Task 審查程式碼_回覆同時包含嚴重與建議問題_報告包含正確統計數字()
        {
            // Arrange
            var code = "var pass = \"abc\"; // TODO: fix this";
            var fakeReviewContent = "[CRITICAL] SQL Injection 漏洞。\n[WARNING] 命名不符規範。\n[SUGGESTION] 可加入日誌記錄。";

            var chatMessageContent = new ChatMessageContent(AuthorRole.Assistant, fakeReviewContent);

            _chatCompletionService
                .GetChatMessageContentAsync(
                    Arg.Any<ChatHistory>(),
                    Arg.Any<PromptExecutionSettings>(),
                    Arg.Any<Kernel>(),
                    Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(chatMessageContent));

            // Act
            var result = await _sut.ReviewCodeAsync(code);

            // Assert
            result.Should().Contain("嚴重問題（Critical / Error）：**1**");
            result.Should().Contain("建議改善（Warning / Suggestion）：**2**");
            result.Should().Contain("請優先處理標示為 CRITICAL / ERROR 的項目");
        }

        [Fact]
        public async Task 審查程式碼_報告包含分隔線與統計區塊結構()
        {
            // Arrange
            var code = "int[] arr = new int[5]; arr[10] = 1;";
            var fakeReviewContent = "[ERROR] 陣列越界存取。";

            var chatMessageContent = new ChatMessageContent(AuthorRole.Assistant, fakeReviewContent);

            _chatCompletionService
                .GetChatMessageContentAsync(
                    Arg.Any<ChatHistory>(),
                    Arg.Any<PromptExecutionSettings>(),
                    Arg.Any<Kernel>(),
                    Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(chatMessageContent));

            // Act
            var result = await _sut.ReviewCodeAsync(code);

            // Assert
            result.Should().Contain("---");
            result.Should().Contain("## 📊 審查統計總結");
            result.Should().Contain("🔴 嚴重問題");
            result.Should().Contain("🟡 建議改善");
        }

        [Fact]
        public async Task 審查程式碼_使用正確的ChatHistory呼叫ChatCompletion服務()
        {
            // Arrange
            var code = "Console.WriteLine(\"Hello\");";
            var fakeReviewContent = "OK";
            var chatMessageContent = new ChatMessageContent(AuthorRole.Assistant, fakeReviewContent);

            _chatCompletionService
                .GetChatMessageContentAsync(
                    Arg.Any<ChatHistory>(),
                    Arg.Any<PromptExecutionSettings>(),
                    Arg.Any<Kernel>(),
                    Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(chatMessageContent));

            // Act
            await _sut.ReviewCodeAsync(code);

            // Assert
            await _chatCompletionService
                .Received(1)
                .GetChatMessageContentAsync(
                    Arg.Any<ChatHistory>(),
                    Arg.Any<PromptExecutionSettings>(),
                    Arg.Any<Kernel>(),
                    Arg.Any<CancellationToken>());
        }

        #endregion
    }
}
```