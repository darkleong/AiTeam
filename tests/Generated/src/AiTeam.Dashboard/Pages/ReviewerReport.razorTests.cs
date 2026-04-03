```csharp
using AiTeam.Dashboard.Models;
using AiTeam.Dashboard.Pages;
using AiTeam.Dashboard.Services;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using System.Reflection;
using Xunit;

namespace AiTeam.Dashboard.Tests.Pages;

public class ReviewerReportTests
{
    private readonly IReviewerReportService _reviewerReportService;
    private readonly ReviewerReport _sut;

    public ReviewerReportTests()
    {
        _reviewerReportService = Substitute.For<IReviewerReportService>();
        _sut = new ReviewerReport();

        // 注入 mock service
        var serviceProperty = typeof(ReviewerReport)
            .GetProperty("ReviewerReportService", BindingFlags.NonPublic | BindingFlags.Instance);
        serviceProperty!.SetValue(_sut, _reviewerReportService);
    }

    #region Helper Methods

    private List<ReviewerReportItem> GetReportItems() =>
        GetPrivateProperty<List<ReviewerReportItem>>("ReportItems")!;

    private ReviewerReportSummary GetSummary() =>
        GetPrivateProperty<ReviewerReportSummary>("Summary")!;

    private bool GetIsLoading() =>
        GetPrivateProperty<bool>("IsLoading");

    private string? GetErrorMessage() =>
        GetPrivateProperty<string?>("ErrorMessage");

    private T? GetPrivateProperty<T>(string propertyName)
    {
        var property = typeof(ReviewerReport)
            .GetProperty(propertyName, BindingFlags.NonPublic | BindingFlags.Instance);
        return (T?)property!.GetValue(_sut);
    }

    private async Task InvokeLoadReportDataAsync()
    {
        var method = typeof(ReviewerReport)
            .GetMethod("LoadReportDataAsync", BindingFlags.NonPublic | BindingFlags.Instance);
        await (Task)method!.Invoke(_sut, null)!;
    }

    private async Task InvokeOnInitializedAsync()
    {
        var method = typeof(ReviewerReport)
            .GetMethod("OnInitializedAsync", BindingFlags.NonPublic | BindingFlags.Instance);
        await (Task)method!.Invoke(_sut, null)!;
    }

    private async Task InvokeRefreshAsync()
    {
        var method = typeof(ReviewerReport)
            .GetMethod("RefreshAsync", BindingFlags.NonPublic | BindingFlags.Instance);
        await (Task)method!.Invoke(_sut, null)!;
    }

    private static ReviewerReportSummary InvokeCalculateSummary(List<ReviewerReportItem> items)
    {
        var method = typeof(ReviewerReport)
            .GetMethod("CalculateSummary", BindingFlags.NonPublic | BindingFlags.Static);
        return (ReviewerReportSummary)method!.Invoke(null, new object[] { items })!;
    }

    #endregion

    #region OnInitializedAsync Tests

    [Fact]
    public async Task 初始化_服務回傳報告資料_應正確載入報告項目()
    {
        // Arrange
        var expectedItems = new List<ReviewerReportItem>
        {
            new() { IssueType = IssueType.Error },
            new() { IssueType = IssueType.Warning },
            new() { IssueType = IssueType.Suggestion }
        };
        _reviewerReportService.GetReportItemsAsync().Returns(expectedItems);

        // Act
        await InvokeOnInitializedAsync();

        // Assert
        var reportItems = GetReportItems();
        reportItems.Should().BeEquivalentTo(expectedItems);
        GetIsLoading().Should().BeFalse();
        GetErrorMessage().Should().BeNull();
    }

    [Fact]
    public async Task 初始化_服務拋出例外_應設定錯誤訊息()
    {
        // Arrange
        var exceptionMessage = "連線逾時";
        _reviewerReportService.GetReportItemsAsync()
            .ThrowsAsync(new Exception(exceptionMessage));

        // Act
        await InvokeOnInitializedAsync();

        // Assert
        GetErrorMessage().Should().Contain(exceptionMessage);
        GetErrorMessage().Should().Contain("載入報告資料時發生錯誤");
        GetIsLoading().Should().BeFalse();
    }

    #endregion

    #region LoadReportDataAsync Tests

    [Fact]
    public async Task 載入報告資料_成功取得資料_IsLoading應為False且ErrorMessage應為Null()
    {
        // Arrange
        var items = new List<ReviewerReportItem>
        {
            new() { IssueType = IssueType.Error },
            new() { IssueType = IssueType.Info }
        };
        _reviewerReportService.GetReportItemsAsync().Returns(items);

        // Act
        await InvokeLoadReportDataAsync();

        // Assert
        GetIsLoading().Should().BeFalse();
        GetErrorMessage().Should().BeNull();
        GetReportItems().Should().HaveCount(2);
    }

    [Fact]
    public async Task 載入報告資料_服務拋出例外_IsLoading應為False且應有錯誤訊息()
    {
        // Arrange
        _reviewerReportService.GetReportItemsAsync()
            .ThrowsAsync(new InvalidOperationException("無效操作"));

        // Act
        await InvokeLoadReportDataAsync();

        // Assert
        GetIsLoading().Should().BeFalse();
        GetErrorMessage().Should().NotBeNullOrEmpty();
        GetErrorMessage().Should().Contain("無效操作");
    }

    [Fact]
    public async Task 載入報告資料_成功取得資料_應計算正確摘要()
    {
        // Arrange
        var items = new List<ReviewerReportItem>
        {
            new() { IssueType = IssueType.Error },
            new() { IssueType = IssueType.Error },
            new() { IssueType = IssueType.Warning },
            new() { IssueType = IssueType.Suggestion },
            new() { IssueType = IssueType.Info }
        };
        _reviewerReportService.GetReportItemsAsync().Returns(items);

        // Act
        await InvokeLoadReportDataAsync();

        // Assert
        var summary = GetSummary();
        summary.TotalCount.Should().Be(5);
        summary.ErrorCount.Should().Be(2);
        summary.WarningCount.Should().Be(1);
        summary.SuggestionCount.Should().Be(1);
        summary.InfoCount.Should().Be(1);
    }

    [Fact]
    public async Task 載入報告資料_服務回傳空清單_摘要應全為零()
    {
        // Arrange
        _reviewerReportService.GetReportItemsAsync().Returns(new List<ReviewerReportItem>());

        // Act
        await InvokeLoadReportDataAsync();

        // Assert
        var summary = GetSummary();
        summary.TotalCount.Should().Be(0);
        summary.ErrorCount.Should().Be(0);
        summary.WarningCount.Should().Be(0);
        summary.SuggestionCount.Should().Be(0);
        summary.InfoCount.Should().Be(0);
    }

    #endregion

    #region CalculateSummary Tests

    [Fact]
    public void 計算摘要_包含各類型問題_應正確統計各類型數量()
    {
        // Arrange
        var items = new List<ReviewerReportItem>
        {
            new() { IssueType = IssueType.Error },
            new() { IssueType = IssueType.Error },
            new() { IssueType = IssueType.Error },
            new() { IssueType = IssueType.Warning },
            new() { IssueType = IssueType.Warning },
            new() { IssueType = IssueType.Suggestion },
            new() { IssueType = IssueType.Info }
        };

        // Act
        var summary = InvokeCalculateSummary(items);

        // Assert
        summary.TotalCount.Should().Be(7);
        summary.ErrorCount.Should().Be(3);
        summary.WarningCount.Should().Be(2);
        summary.SuggestionCount.Should().Be(1);
        summary.InfoCount.Should().Be(1);
    }

    [Fact]
    public void 計算摘要_空清單_應回傳全零摘要()
    {
        // Arrange
        var items = new List<ReviewerReportItem>();

        // Act
        var summary = InvokeCalculateSummary(items);

        // Assert
        summary.TotalCount.Should().Be(0);
        summary.ErrorCount.Should().Be(0);
        summary.WarningCount.Should().Be(0);
        summary.SuggestionCount.Should().Be(0);
        summary.InfoCount.Should().Be(0);
    }

    [Fact]
    public void 計算摘要_只有錯誤類型_其他類型數量應為零()
    {
        // Arrange
        var items = new List<ReviewerReportItem>
        {
            new() { IssueType = IssueType.Error },
            new() { IssueType = IssueType.Error }
        };

        // Act
        var summary = InvokeCalculateSummary(items);

        // Assert
        summary.TotalCount.Should().Be(2);
        summary.ErrorCount.Should().Be(2);
        summary.WarningCount.Should().Be(0);
        summary.SuggestionCount.Should().Be(0);
        summary.InfoCount.Should().Be(0);
    }

    [Fact]
    public void 計算摘要_只有提示類型_TotalCount應等於InfoCount()
    {
        // Arrange
        var items = new List<ReviewerReportItem>
        {
            new() { IssueType = IssueType.Info },
            new() { IssueType = IssueType.Info },
            new() { IssueType = IssueType.Info }
        };

        // Act
        var summary = InvokeCalculateSummary(items);

        // Assert
        summary.TotalCount.Should().Be(3);
        summary.InfoCount.Should().Be(3);
        summary.ErrorCount.Should().Be(0);
        summary.WarningCount.Should().Be(0);
        summary.SuggestionCount.Should().Be(0);
    }

    #endregion

    #region RefreshAsync Tests

    [Fact]
    public async Task 重新整理_呼叫成功_應重新載入報告資料()
    {
        // Arrange
        var initialItems = new List<ReviewerReportItem>
        {
            new() { IssueType = IssueType.Error }
        };
        var refreshedItems = new List<ReviewerReportItem>
        {
            new() { IssueType = IssueType.Warning },
            new() { IssueType = IssueType.Info }
        };

        _reviewerReportService.GetReportItemsAsync()
            .Returns(initialItems, refreshedItems);

        await InvokeLoadReportDataAsync();

        // Act
        await InvokeRefreshAsync();

        // Assert
        GetReportItems().Should().HaveCount(2);
        GetReportItems().Should().Contain(i => i.IssueType == IssueType.Warning);
        GetReportItems().Should().Contain(i => i.IssueType == IssueType.Info);
        GetIsLoading().Should().BeFalse();
        GetErrorMessage().Should().BeNull();
    }

    [Fact]
    public async Task 重新整理_服務拋出例外_應設定錯誤訊息且IsLoading為False()
    {
        // Arrange
        _reviewerReportService.GetReportItemsAsync()
            .ThrowsAsync(new HttpRequestException("網路連線失敗"));

        // Act
        await InvokeRefreshAsync();

        // Assert
        GetIsLoading().Should().BeFalse();
        GetErrorMessage().Should().NotBeNullOrEmpty();
        GetErrorMessage().Should().Contain("網路連線失敗");
    }

    [Fact]
    public async Task 重新整理_呼叫多次_應多次呼叫服務()
    {
        // Arrange
        _reviewerReportService.GetReportItemsAsync()
            .Returns(new List<ReviewerReportItem>());

        // Act
        await InvokeRefreshAsync();
        await InvokeRefreshAsync();
        await InvokeRefreshAsync();

        // Assert
        await _reviewerReportService.Received(3).GetReportItemsAsync();
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task 載入報告資料_發生例外後再次載入成功_ErrorMessage應被清除()
    {
        // Arrange
        _reviewerReportService.GetReportItemsAsync()
            .ThrowsAsync(new Exception("第一次失敗"));

        await InvokeLoadReportDataAsync();
        GetErrorMessage().Should().NotBeNull();

        // 第二次成功
        _reviewerReportService.GetReportItemsAsync()
            .Returns(new List<ReviewerReportItem> { new() { IssueType = IssueType.Info } });

        // Act
        await InvokeLoadReportDataAsync();

        // Assert
        GetErrorMessage().Should().BeNull();
        GetIsLoading().Should().BeFalse();
        GetReportItems().Should().HaveCount(1);
    }

    [Fact]
    public async Task 載入報告資料_例外訊息包含特殊字元_應完整保留錯誤訊息()
    {
        // Arrange
        var specialMessage = "Error: <特殊字元> & \"引號\" 'apostrophe'";
        _reviewerReportService.GetReportItemsAsync()
            .ThrowsAsync(new Exception(specialMessage));

        // Act
        await InvokeLoadReportDataAsync();

        // Assert
        GetErrorMessage().Should().Contain(specialMessage);
    }

    #endregion
}
```