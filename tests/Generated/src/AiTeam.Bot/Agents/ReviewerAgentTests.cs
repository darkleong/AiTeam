```csharp
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AiTeam.Bot.Agents;
using AiTeam.Bot.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using NSubstitute;
using Xunit;

namespace AiTeam.Bot.Tests.Agents
{
    public class ReviewerAgentTests
    {
        private readonly Kernel _kernel;
        private readonly ILogger<ReviewerAgent> _logger;
        private readonly ReviewerAgent _sut;

        public ReviewerAgentTests()
        {
            _kernel = Substitute.For<Kernel>();
            _logger = Substitute.For<ILogger<ReviewerAgent>>();
            _sut = new ReviewerAgent(_kernel, _logger);
        }

        #region ReviewAsync 測試

        [Fact]
        public async Task ReviewAsync_正常程式碼含錯誤警告資訊_回傳正確統計報告()
        {
            // Arrange
            var code = "var x = 1;";
            var fakeResponse = "[ERROR] 存在空指標例外風險\n[WARNING] 效能可改善\n[INFO] 建議加入文件";

            var functionResult = Substitute.For<FunctionResult>(
                Substitute.For<KernelFunction>(),
                fakeResponse);

            _kernel.InvokePromptAsync(Arg.Any<string>())
                   .Returns(Task.FromResult(functionResult));

            // Act
            var report = await _sut.ReviewAsync(code);

            // Assert
            report.Should().NotBeNull();
            report.ErrorCount.Should().Be(1);
            report.WarningCount.Should().Be(1);
            report.InfoCount.Should().Be(1);
            report.TotalIssueCount.Should().Be(3);
            report.OriginalCode.Should().Be(code);
        }

        [Fact]
        public async Task ReviewAsync_含有背景資訊_回傳包含背景的報告()
        {
            // Arrange
            var code = "public void Foo() {}";
            var context = "這是一段重要的業務邏輯";
            var fakeResponse = "[INFO] 方法名稱可更具描述性";

            var functionResult = Substitute.For<FunctionResult>(
                Substitute.For<KernelFunction>(),
                fakeResponse);

            _kernel.InvokePromptAsync(Arg.Any<string>())
                   .Returns(Task.FromResult(functionResult));

            // Act
            var report = await _sut.ReviewAsync(code, context);

            // Assert
            report.Should().NotBeNull();
            report.InfoCount.Should().Be(1);
            report.ErrorCount.Should().Be(0);
            report.WarningCount.Should().Be(0);
        }

        [Fact]
        public async Task ReviewAsync_回應無任何問題標記_回傳零計數報告()
        {
            // Arrange
            var code = "// 完美的程式碼";
            var fakeResponse = "程式碼看起來沒有任何問題，非常乾淨。";

            var functionResult = Substitute.For<FunctionResult>(
                Substitute.For<KernelFunction>(),
                fakeResponse);

            _kernel.InvokePromptAsync(Arg.Any<string>())
                   .Returns(Task.FromResult(functionResult));

            // Act
            var report = await _sut.ReviewAsync(code);

            // Assert
            report.Should().NotBeNull();
            report.ErrorCount.Should().Be(0);
            report.WarningCount.Should().Be(0);
            report.InfoCount.Should().Be(0);
            report.TotalIssueCount.Should().Be(0);
        }

        [Fact]
        public async Task ReviewAsync_多個Error_回傳正確Error計數()
        {
            // Arrange
            var code = "unsafe { int* p = null; *p = 1; }";
            var fakeResponse = "[ERROR] 存在記憶體洩漏\n[ERROR] 空指標解參考\n[ERROR] 未處理例外";

            var functionResult = Substitute.For<FunctionResult>(
                Substitute.For<KernelFunction>(),
                fakeResponse);

            _kernel.InvokePromptAsync(Arg.Any<string>())
                   .Returns(Task.FromResult(functionResult));

            // Act
            var report = await _sut.ReviewAsync(code);

            // Assert
            report.ErrorCount.Should().Be(3);
            report.WarningCount.Should().Be(0);
            report.InfoCount.Should().Be(0);
            report.TotalIssueCount.Should().Be(3);
        }

        [Fact]
        public async Task ReviewAsync_報告內容包含摘要行_摘要行格式正確()
        {
            // Arrange
            var code = "var x = 1;";
            var fakeResponse = "[ERROR] 嚴重問題\n[WARNING] 警告問題\n[INFO] 提示問題";

            var functionResult = Substitute.For<FunctionResult>(
                Substitute.For<KernelFunction>(),
                fakeResponse);

            _kernel.InvokePromptAsync(Arg.Any<string>())
                   .Returns(Task.FromResult(functionResult));

            // Act
            var report = await _sut.ReviewAsync(code);

            // Assert
            report.SummaryLine.Should().Contain("Error 1 個");
            report.SummaryLine.Should().Contain("Warning 1 個");
            report.SummaryLine.Should().Contain("Info 1 個");
            report.SummaryLine.Should().Contain("共 3 個問題");
            report.Content.Should().Contain("---");
            report.Content.Should().Contain(report.SummaryLine);
        }

        [Fact]
        public async Task ReviewAsync_ReviewedAt_時間接近現在時刻()
        {
            // Arrange
            var code = "int a = 0;";
            var fakeResponse = "[INFO] 變數名稱可更有意義";
            var beforeCall = DateTime.UtcNow;

            var functionResult = Substitute.For<FunctionResult>(
                Substitute.For<KernelFunction>(),
                fakeResponse);

            _kernel.InvokePromptAsync(Arg.Any<string>())
                   .Returns(Task.FromResult(functionResult));

            // Act
            var report = await _sut.ReviewAsync(code);
            var afterCall = DateTime.UtcNow;

            // Assert
            report.ReviewedAt.Should().BeOnOrAfter(beforeCall);
            report.ReviewedAt.Should().BeOnOrBefore(afterCall);
        }

        [Fact]
        public async Task ReviewAsync_大小寫混合標記_皆能正確解析()
        {
            // Arrange
            var code = "var y = 2;";
            var fakeResponse = "[error] 小寫錯誤標記\n[Warning] 混合警告標記\n[INFO] 大寫資訊標記";

            var functionResult = Substitute.For<FunctionResult>(
                Substitute.For<KernelFunction>(),
                fakeResponse);

            _kernel.InvokePromptAsync(Arg.Any<string>())
                   .Returns(Task.FromResult(functionResult));

            // Act
            var report = await _sut.ReviewAsync(code);

            // Assert
            report.ErrorCount.Should().Be(1);
            report.WarningCount.Should().Be(1);
            report.InfoCount.Should().Be(1);
        }

        [Fact]
        public async Task ReviewAsync_Issues清單_包含正確描述與嚴重等級()
        {
            // Arrange
            var code = "string s = null;";
            var fakeResponse = "[ERROR] 空參考例外風險\n[WARNING] 建議使用String.Empty";

            var functionResult = Substitute.For<FunctionResult>(
                Substitute.For<KernelFunction>(),
                fakeResponse);

            _kernel.InvokePromptAsync(Arg.Any<string>())
                   .Returns(Task.FromResult(functionResult));

            // Act
            var report = await _sut.ReviewAsync(code);

            // Assert
            report.Issues.Should().HaveCount(2);
            report.Issues.Should().Contain(i =>
                i.Severity == IssueSeverity.Error &&
                i.Description == "空參考例外風險");
            report.Issues.Should().Contain(i =>
                i.Severity == IssueSeverity.Warning &&
                i.Description == "建議使用String.Empty");
        }

        [Fact]
        public async Task ReviewAsync_空背景資訊字串_不影響報告產生()
        {
            // Arrange
            var code = "bool flag = false;";
            var fakeResponse = "[INFO] 變數初始化正確";

            var functionResult = Substitute.For<FunctionResult>(
                Substitute.For<KernelFunction>(),
                fakeResponse);

            _kernel.InvokePromptAsync(Arg.Any<string>())
                   .Returns(Task.FromResult(functionResult));

            // Act
            var report = await _sut.ReviewAsync(code, string.Empty);

            // Assert
            report.Should().NotBeNull();
            report.InfoCount.Should().Be(1);
        }

        [Fact]
        public async Task ReviewAsync_呼叫Kernel_應傳入非空的Prompt()
        {
            // Arrange
            var code = "int z = 42;";
            var fakeResponse = "[INFO] 神奇數字建議定義為常數";
            string capturedPrompt = null;

            var functionResult = Substitute.For<FunctionResult>(
                Substitute.For<KernelFunction>(),
                fakeResponse);

            _kernel.InvokePromptAsync(Arg.Do<string>(p => capturedPrompt = p))
                   .Returns(Task.FromResult(functionResult));

            // Act
            await _sut.ReviewAsync(code);

            // Assert
            capturedPrompt.Should().NotBeNullOrWhiteSpace();
            capturedPrompt.Should().Contain(code);
            capturedPrompt.Should().Contain("[ERROR]");
            capturedPrompt.Should().Contain("[WARNING]");
            capturedPrompt.Should().Contain("[INFO]");
        }

        [Fact]
        public async Task ReviewAsync_含背景資訊_Prompt應包含背景資訊內容()
        {
            // Arrange
            var code = "int z = 42;";
            var context = "這段程式碼用於金融計算";
            var fakeResponse = "[WARNING] 浮點數運算可能有精度問題";
            string capturedPrompt = null;

            var functionResult = Substitute.For<FunctionResult>(
                Substitute.For<KernelFunction>(),
                fakeResponse);

            _kernel.InvokePromptAsync(Arg.Do<string>(p => capturedPrompt = p))
                   .Returns(Task.FromResult(functionResult));

            // Act
            await _sut.ReviewAsync(code, context);

            // Assert
            capturedPrompt.Should().Contain(context);
        }

        [Fact]
        public async Task ReviewAsync_多行標記夾雜其他文字_只解析有效標記()
        {
            // Arrange
            var code = "var list = new List<int>();";
            var fakeResponse =
                "以下是審查結果：\n" +
                "[ERROR] 未釋放資源\n" +
                "這行是一般說明，不是標記\n" +
                "[WARNING] 容量未預先設定\n" +
                "另一行說明文字\n" +
                "[INFO] 可考慮使用唯讀集合";

            var functionResult = Substitute.For<FunctionResult>(
                Substitute.For<KernelFunction>(),
                fakeResponse);

            _kernel.InvokePromptAsync(Arg.Any<string>())
                   .Returns(Task.FromResult(functionResult));

            // Act
            var report = await _sut.ReviewAsync(code);

            // Assert
            report.ErrorCount.Should().Be(1);
            report.WarningCount.Should().Be(1);
            report.InfoCount.Should().Be(1);
            report.TotalIssueCount.Should().Be(3);
        }

        #endregion
    }
}
```