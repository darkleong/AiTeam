```csharp
using AiTeam.Dashboard.Components.Pages;
using AiTeam.Dashboard.Services;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using System.Reflection;
using Xunit;

namespace AiTeam.Dashboard.Tests.Components.Pages;

public class ReviewerAgentTests
{
    private readonly IReviewerAgentService _reviewerAgentService;
    private readonly ReviewerAgent _component;

    public ReviewerAgentTests()
    {
        _reviewerAgentService = Substitute.For<IReviewerAgentService>();
        _component = new ReviewerAgent();

        // 透過反射注入服務
        var serviceProperty = typeof(ReviewerAgent)
            .GetProperty("ReviewerAgentService", BindingFlags.NonPublic | BindingFlags.Instance);
        serviceProperty!.SetValue(_component, _reviewerAgentService);
    }

    private Task InvokeSubmitReview()
    {
        var method = typeof(ReviewerAgent)
            .GetMethod("SubmitReview", BindingFlags.NonPublic | BindingFlags.Instance);
        return (Task)method!.Invoke(_component, null)!;
    }

    private void InvokeClearInput()
    {
        var method = typeof(ReviewerAgent)
            .GetMethod("ClearInput", BindingFlags.NonPublic | BindingFlags.Instance);
        method!.Invoke(_component, null);
    }

    private void SetCodeInput(string value)
    {
        var field = typeof(ReviewerAgent)
            .GetField("_codeInput", BindingFlags.NonPublic | BindingFlags.Instance);
        field!.SetValue(_component, value);
    }

    private string? GetErrorMessage()
    {
        var field = typeof(ReviewerAgent)
            .GetField("_errorMessage", BindingFlags.NonPublic | BindingFlags.Instance);
        return (string?)field!.GetValue(_component);
    }

    private ReviewReport? GetReviewReport()
    {
        var field = typeof(ReviewerAgent)
            .GetField("_reviewReport", BindingFlags.NonPublic | BindingFlags.Instance);
        return (ReviewReport?)field!.GetValue(_component);
    }

    private bool GetIsLoading()
    {
        var field = typeof(ReviewerAgent)
            .GetField("_isLoading", BindingFlags.NonPublic | BindingFlags.Instance);
        return (bool)field!.GetValue(_component)!;
    }

    #region SubmitReview Tests

    [Fact]
    public async Task SubmitReview_輸入有效程式碼_應回傳審查報告()
    {
        // Arrange
        var expectedReport = new ReviewReport
        {
            Summary = "程式碼品質良好",
            OverallScore = 85,
            Issues = new List<ReviewIssue>
            {
                new ReviewIssue { Severity = "warning", Message = "建議命名改善" }
            }
        };

        SetCodeInput("var x = 1;");
        _reviewerAgentService.ReviewCodeAsync(Arg.Any<string>()).Returns(expectedReport);

        // Act
        await InvokeSubmitReview();

        // Assert
        var report = GetReviewReport();
        report.Should().NotBeNull();
        report!.Summary.Should().Be("程式碼品質良好");
        report.OverallScore.Should().Be(85);
        GetIsLoading().Should().BeFalse();
        GetErrorMessage().Should().BeNull();
    }

    [Fact]
    public async Task SubmitReview_輸入空白字串_應設置錯誤訊息且不呼叫服務()
    {
        // Arrange
        SetCodeInput("   ");

        // Act
        await InvokeSubmitReview();

        // Assert
        GetErrorMessage().Should().Be("請輸入要審查的程式碼");
        GetReviewReport().Should().BeNull();
        await _reviewerAgentService.DidNotReceive().ReviewCodeAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task SubmitReview_輸入空字串_應設置錯誤訊息且不呼叫服務()
    {
        // Arrange
        SetCodeInput(string.Empty);

        // Act
        await InvokeSubmitReview();

        // Assert
        GetErrorMessage().Should().Be("請輸入要審查的程式碼");
        GetReviewReport().Should().BeNull();
        await _reviewerAgentService.DidNotReceive().ReviewCodeAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task SubmitReview_服務拋出例外_應設置錯誤訊息()
    {
        // Arrange
        SetCodeInput("var x = 1;");
        _reviewerAgentService.ReviewCodeAsync(Arg.Any<string>())
            .Throws(new Exception("連線逾時"));

        // Act
        await InvokeSubmitReview();

        // Assert
        GetErrorMessage().Should().Be("審查過程發生錯誤：連線逾時");
        GetReviewReport().Should().BeNull();
        GetIsLoading().Should().BeFalse();
    }

    [Fact]
    public async Task SubmitReview_呼叫服務時_應傳入正確的程式碼內容()
    {
        // Arrange
        var codeContent = "public void Test() { }";
        SetCodeInput(codeContent);
        _reviewerAgentService.ReviewCodeAsync(Arg.Any<string>()).Returns(new ReviewReport());

        // Act
        await InvokeSubmitReview();

        // Assert
        await _reviewerAgentService.Received(1).ReviewCodeAsync(codeContent);
    }

    [Fact]
    public async Task SubmitReview_成功執行後_載入狀態應為false()
    {
        // Arrange
        SetCodeInput("int a = 1;");
        _reviewerAgentService.ReviewCodeAsync(Arg.Any<string>()).Returns(new ReviewReport());

        // Act
        await InvokeSubmitReview();

        // Assert
        GetIsLoading().Should().BeFalse();
    }

    #endregion

    #region ClearInput Tests

    [Fact]
    public void ClearInput_清除後_所有欄位應重置為預設值()
    {
        // Arrange
        SetCodeInput("some code");
        var reportField = typeof(ReviewerAgent)
            .GetField("_reviewReport", BindingFlags.NonPublic | BindingFlags.Instance);
        reportField!.SetValue(_component, new ReviewReport());

        var errorField = typeof(ReviewerAgent)
            .GetField("_errorMessage", BindingFlags.NonPublic | BindingFlags.Instance);
        errorField!.SetValue(_component, "some error");

        // Act
        InvokeClearInput();

        // Assert
        var codeField = typeof(ReviewerAgent)
            .GetField("_codeInput", BindingFlags.NonPublic | BindingFlags.Instance);
        var codeValue = (string?)codeField!.GetValue(_component);

        codeValue.Should().BeEmpty();
        GetReviewReport().Should().BeNull();
        GetErrorMessage().Should().BeNull();
    }

    [Fact]
    public void ClearInput_已有審查結果時清除_審查報告應為null()
    {
        // Arrange
        var reportField = typeof(ReviewerAgent)
            .GetField("_reviewReport", BindingFlags.NonPublic | BindingFlags.Instance);
        reportField!.SetValue(_component, new ReviewReport { Summary = "測試摘要", OverallScore = 90 });

        // Act
        InvokeClearInput();

        // Assert
        GetReviewReport().Should().BeNull();
    }

    #endregion
}

public class ReviewReportTests
{
    #region CalculateStatistics Tests

    [Fact]
    public void Statistics_空的問題列表_所有計數應為零()
    {
        // Arrange
        var report = new ReviewReport
        {
            Issues = new List<ReviewIssue>()
        };

        // Act
        var stats = report.Statistics;

        // Assert
        stats.TotalCount.Should().Be(0);
        stats.BugCount.Should().Be(0);
        stats.WarningCount.Should().Be(0);
        stats.InfoCount.Should().Be(0);
        stats.SuggestionCount.Should().Be(0);
        stats.OtherCount.Should().Be(0);
    }

    [Fact]
    public void Statistics_包含多種嚴重程度的問題_計數應正確()
    {
        // Arrange
        var report = new ReviewReport
        {
            Issues = new List<ReviewIssue>
            {
                new ReviewIssue { Severity = "bug" },
                new ReviewIssue { Severity = "error" },
                new ReviewIssue { Severity = "warning" },
                new ReviewIssue { Severity = "info" },
                new ReviewIssue { Severity = "information" },
                new ReviewIssue { Severity = "suggestion" },
                new ReviewIssue { Severity = "unknown" }
            }
        };

        // Act
        var stats = report.Statistics;

        // Assert
        stats.TotalCount.Should().Be(7);
        stats.BugCount.Should().Be(2);
        stats.WarningCount.Should().Be(1);
        stats.InfoCount.Should().Be(2);
        stats.SuggestionCount.Should().Be(1);
        stats.OtherCount.Should().Be(1);
    }

    [Fact]
    public void Statistics_嚴重程度大寫_應正確分類()
    {
        // Arrange
        var report = new ReviewReport
        {
            Issues = new List<ReviewIssue>
            {
                new ReviewIssue { Severity = "BUG" },
                new ReviewIssue { Severity = "WARNING" },
                new ReviewIssue { Severity = "INFO" }
            }
        };

        // Act
        var stats = report.Statistics;

        // Assert
        stats.BugCount.Should().Be(1);
        stats.WarningCount.Should().Be(1);
        stats.InfoCount.Should().Be(1);
    }

    [Fact]
    public void Statistics_嚴重程度為null_應計入OtherCount()
    {
        // Arrange
        var report = new ReviewReport
        {
            Issues = new List<ReviewIssue>
            {
                new ReviewIssue { Severity = null! }
            }
        };

        // Act
        var stats = report.Statistics;

        // Assert
        stats.OtherCount.Should().Be(1);
        stats.TotalCount.Should().Be(1);
    }

    [Fact]
    public void Statistics_多個Bug問題_BugCount應正確累計()
    {
        // Arrange
        var report = new ReviewReport
        {
            Issues = new List<ReviewIssue>
            {
                new ReviewIssue { Severity = "bug" },
                new ReviewIssue { Severity = "bug" },
                new ReviewIssue { Severity = "error" }
            }
        };

        // Act
        var stats = report.Statistics;

        // Assert
        stats.BugCount.Should().Be(3);
    }

    #endregion
}

public class IssueSummaryStatisticsTests
{
    #region HasIssues Tests

    [Fact]
    public void HasIssues_TotalCount為零_應回傳false()
    {
        // Arrange
        var stats = new IssueSummaryStatistics { TotalCount = 0 };

        // Act & Assert
        stats.HasIssues.Should().BeFalse();
    }

    [Fact]
    public void HasIssues_TotalCount大於零_應回傳true()
    {
        // Arrange
        var stats = new IssueSummaryStatistics { TotalCount = 3 };

        // Act & Assert
        stats.HasIssues.Should().BeTrue();
    }

    #endregion

    #region GetSeverityCssClass Tests

    [Theory]
    [InlineData("bug", "badge-danger")]
    [InlineData("error", "badge-danger")]
    [InlineData("BUG", "badge-danger")]
    [InlineData("ERROR", "badge-danger")]
    public void GetSeverityCssClass_Bug或Error嚴重程度_應回傳badge_danger(string severity, string expected)
    {
        // Arrange
        var stats = new IssueSummaryStatistics();

        // Act
        var result = stats.GetSeverityCssClass(severity);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("warning", "badge-warning")]
    [InlineData("WARNING", "badge-warning")]
    public void GetSeverityCssClass_Warning嚴重程度_應回傳badge_warning(string severity, string expected)
    {
        // Arrange
        var stats = new IssueSummaryStatistics();

        // Act
        var result = stats.GetSeverityCssClass(severity);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("info", "badge-info")]
    [InlineData("information", "badge-info")]
    [InlineData("INFO", "badge-info")]
    public void GetSeverityCssClass_Info或Information嚴重程度_應回傳badge_info(string severity, string expected)
    {
        // Arrange
        var stats = new IssueSummaryStatistics();

        // Act
        var result = stats.GetSeverityCssClass(severity);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("suggestion", "badge-success")]
    [InlineData("SUGGESTION", "badge-success")]
    public void GetSeverityCssClass_Suggestion嚴重程度_應回傳badge_success(string severity, string expected)
    {
        // Arrange
        var stats = new IssueSummaryStatistics();

        // Act
        var result = stats.GetSeverityCssClass(severity);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("unknown", "badge-secondary")]
    [InlineData("other", "badge-secondary")]
    [InlineData("", "badge-secondary")]
    public void GetSeverityCssClass_未知嚴重程度_應回傳badge_secondary(string severity, string expected)
    {
        // Arrange
        var stats = new IssueSummaryStatistics();

        // Act
        var result = stats.GetSeverityCssClass(severity);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void GetSeverityCssClass_傳入null_應回傳badge_secondary()
    {
        // Arrange
        var stats = new IssueSummaryStatistics();

        // Act
        var result = stats.GetSeverityCssClass(null!);

        // Assert
        result.Should().Be("badge-secondary");
    }

    #endregion
}

public class ReviewIssueTests
{
    [Fact]
    public void ReviewIssue_建立新實例_預設值應正確()
    {
        // Act
        var issue = new ReviewIssue();

        // Assert
        issue.Severity.Should().BeEmpty();
        issue.Message