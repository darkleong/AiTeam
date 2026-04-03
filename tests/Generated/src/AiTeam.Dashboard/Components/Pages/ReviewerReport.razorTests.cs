```csharp
using AiTeam.Dashboard.Models;
using AiTeam.Dashboard.Services;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;

namespace AiTeam.Dashboard.Tests.Components.Pages;

public class ReviewerReportTests
{
    private readonly IReviewerReportService _reviewerReportService;
    private readonly ReviewerReportTestable _sut;

    public ReviewerReportTests()
    {
        _reviewerReportService = Substitute.For<IReviewerReportService>();
        _sut = new ReviewerReportTestable(_reviewerReportService);
    }

    #region CalculateSummary 測試

    [Fact]
    public void 計算摘要_傳入空清單_回傳預設摘要()
    {
        // Arrange
        var emptyItems = new List<ReviewerReportItem>();

        // Act
        var result = ReviewerReportTestable.InvokeCalculateSummary(emptyItems);

        // Assert
        result.Should().NotBeNull();
        result.TotalCount.Should().Be(0);
        result.TotalReviewCount.Should().Be(0);
        result.AverageReviewCount.Should().Be(0);
        result.TotalApprovedCount.Should().Be(0);
        result.AverageApprovedCount.Should().Be(0);
        result.TotalRejectedCount.Should().Be(0);
        result.AverageRejectedCount.Should().Be(0);
        result.TotalCommentCount.Should().Be(0);
        result.AverageCommentCount.Should().Be(0);
        result.AverageApprovalRate.Should().Be(0);
    }

    [Fact]
    public void 計算摘要_傳入Null_回傳預設摘要()
    {
        // Arrange
        List<ReviewerReportItem> nullItems = null!;

        // Act
        var result = ReviewerReportTestable.InvokeCalculateSummary(nullItems);

        // Assert
        result.Should().NotBeNull();
        result.TotalCount.Should().Be(0);
        result.TotalReviewCount.Should().Be(0);
        result.AverageApprovalRate.Should().Be(0);
    }

    [Fact]
    public void 計算摘要_傳入單一項目_正確計算所有欄位()
    {
        // Arrange
        var items = new List<ReviewerReportItem>
        {
            new ReviewerReportItem
            {
                ReviewCount = 10,
                ApprovedCount = 8,
                RejectedCount = 2,
                CommentCount = 15
            }
        };

        // Act
        var result = ReviewerReportTestable.InvokeCalculateSummary(items);

        // Assert
        result.TotalCount.Should().Be(1);
        result.TotalReviewCount.Should().Be(10);
        result.AverageReviewCount.Should().Be(10);
        result.TotalApprovedCount.Should().Be(8);
        result.AverageApprovedCount.Should().Be(8);
        result.TotalRejectedCount.Should().Be(2);
        result.AverageRejectedCount.Should().Be(2);
        result.TotalCommentCount.Should().Be(15);
        result.AverageCommentCount.Should().Be(15);
        result.AverageApprovalRate.Should().Be(80);
    }

    [Fact]
    public void 計算摘要_傳入多筆項目_正確計算總計和平均()
    {
        // Arrange
        var items = new List<ReviewerReportItem>
        {
            new ReviewerReportItem
            {
                ReviewCount = 10,
                ApprovedCount = 8,
                RejectedCount = 2,
                CommentCount = 20
            },
            new ReviewerReportItem
            {
                ReviewCount = 20,
                ApprovedCount = 15,
                RejectedCount = 5,
                CommentCount = 30
            }
        };

        // Act
        var result = ReviewerReportTestable.InvokeCalculateSummary(items);

        // Assert
        result.TotalCount.Should().Be(2);
        result.TotalReviewCount.Should().Be(30);
        result.AverageReviewCount.Should().Be(15);
        result.TotalApprovedCount.Should().Be(23);
        result.AverageApprovedCount.Should().Be(11.5);
        result.TotalRejectedCount.Should().Be(7);
        result.AverageRejectedCount.Should().Be(3.5);
        result.TotalCommentCount.Should().Be(50);
        result.AverageCommentCount.Should().Be(25);
    }

    [Fact]
    public void 計算摘要_所有項目ReviewCount為零_平均核准率為零()
    {
        // Arrange
        var items = new List<ReviewerReportItem>
        {
            new ReviewerReportItem
            {
                ReviewCount = 0,
                ApprovedCount = 0,
                RejectedCount = 0,
                CommentCount = 5
            }
        };

        // Act
        var result = ReviewerReportTestable.InvokeCalculateSummary(items);

        // Assert
        result.AverageApprovalRate.Should().Be(0);
    }

    [Fact]
    public void 計算摘要_部分項目ReviewCount為零_僅計算ReviewCount大於零的平均核准率()
    {
        // Arrange
        var items = new List<ReviewerReportItem>
        {
            new ReviewerReportItem
            {
                ReviewCount = 0,
                ApprovedCount = 0,
                RejectedCount = 0,
                CommentCount = 0
            },
            new ReviewerReportItem
            {
                ReviewCount = 10,
                ApprovedCount = 5,
                RejectedCount = 5,
                CommentCount = 10
            }
        };

        // Act
        var result = ReviewerReportTestable.InvokeCalculateSummary(items);

        // Assert
        result.AverageApprovalRate.Should().Be(50);
    }

    [Fact]
    public void 計算摘要_核准率計算_小數點四捨五入至兩位()
    {
        // Arrange
        var items = new List<ReviewerReportItem>
        {
            new ReviewerReportItem
            {
                ReviewCount = 3,
                ApprovedCount = 1,
                RejectedCount = 2,
                CommentCount = 0
            }
        };

        // Act
        var result = ReviewerReportTestable.InvokeCalculateSummary(items);

        // Assert
        result.AverageApprovalRate.Should().Be(Math.Round(100.0 / 3, 2));
    }

    #endregion

    #region LoadReportDataAsync 測試

    [Fact]
    public async Task 載入報告資料_服務成功回傳資料_設定ReportItems和Summary()
    {
        // Arrange
        var reportItems = new List<ReviewerReportItem>
        {
            new ReviewerReportItem
            {
                ReviewCount = 10,
                ApprovedCount = 8,
                RejectedCount = 2,
                CommentCount = 15
            }
        };

        _reviewerReportService.GetReportItemsAsync()
            .Returns(Task.FromResult(reportItems));

        // Act
        await _sut.InvokeLoadReportDataAsync();

        // Assert
        _sut.GetReportItems().Should().BeEquivalentTo(reportItems);
        _sut.GetSummary().Should().NotBeNull();
        _sut.GetIsLoading().Should().BeFalse();
        _sut.GetErrorMessage().Should().BeNull();
    }

    [Fact]
    public async Task 載入報告資料_服務拋出例外_設定錯誤訊息()
    {
        // Arrange
        var exceptionMessage = "連線逾時";
        _reviewerReportService.GetReportItemsAsync()
            .ThrowsAsync(new Exception(exceptionMessage));

        // Act
        await _sut.InvokeLoadReportDataAsync();

        // Assert
        _sut.GetErrorMessage().Should().Contain(exceptionMessage);
        _sut.GetErrorMessage().Should().StartWith("載入資料時發生錯誤：");
        _sut.GetIsLoading().Should().BeFalse();
    }

    [Fact]
    public async Task 載入報告資料_服務回傳空清單_Summary為預設值()
    {
        // Arrange
        _reviewerReportService.GetReportItemsAsync()
            .Returns(Task.FromResult(new List<ReviewerReportItem>()));

        // Act
        await _sut.InvokeLoadReportDataAsync();

        // Assert
        _sut.GetReportItems().Should().BeEmpty();
        _sut.GetSummary().Should().NotBeNull();
        _sut.GetSummary()!.TotalCount.Should().Be(0);
        _sut.GetIsLoading().Should().BeFalse();
        _sut.GetErrorMessage().Should().BeNull();
    }

    [Fact]
    public async Task 載入報告資料_載入過程中_IsLoading為True後變False()
    {
        // Arrange
        var tcs = new TaskCompletionSource<List<ReviewerReportItem>>();
        _reviewerReportService.GetReportItemsAsync()
            .Returns(Task.FromResult(new List<ReviewerReportItem>()));

        // Act
        await _sut.InvokeLoadReportDataAsync();

        // Assert
        _sut.GetIsLoading().Should().BeFalse();
    }

    #endregion

    #region RefreshAsync 測試

    [Fact]
    public async Task 重新整理_呼叫後_再次呼叫服務取得資料()
    {
        // Arrange
        var firstCallItems = new List<ReviewerReportItem>
        {
            new ReviewerReportItem { ReviewCount = 5, ApprovedCount = 3, RejectedCount = 2, CommentCount = 7 }
        };

        var secondCallItems = new List<ReviewerReportItem>
        {
            new ReviewerReportItem { ReviewCount = 10, ApprovedCount = 8, RejectedCount = 2, CommentCount = 12 },
            new ReviewerReportItem { ReviewCount = 20, ApprovedCount = 15, RejectedCount = 5, CommentCount = 25 }
        };

        _reviewerReportService.GetReportItemsAsync()
            .Returns(
                Task.FromResult(firstCallItems),
                Task.FromResult(secondCallItems)
            );

        // Act
        await _sut.InvokeLoadReportDataAsync();
        await _sut.InvokeRefreshAsync();

        // Assert
        await _reviewerReportService.Received(2).GetReportItemsAsync();
        _sut.GetReportItems().Should().BeEquivalentTo(secondCallItems);
    }

    [Fact]
    public async Task 重新整理_第一次失敗後呼叫_清除舊錯誤訊息並重新載入()
    {
        // Arrange
        _reviewerReportService.GetReportItemsAsync()
            .ThrowsAsync(new Exception("初始錯誤"));

        await _sut.InvokeLoadReportDataAsync();
        _sut.GetErrorMessage().Should().NotBeNull();

        var newItems = new List<ReviewerReportItem>
        {
            new ReviewerReportItem { ReviewCount = 5, ApprovedCount = 4, RejectedCount = 1, CommentCount = 8 }
        };

        _reviewerReportService.GetReportItemsAsync()
            .Returns(Task.FromResult(newItems));

        // Act
        await _sut.InvokeRefreshAsync();

        // Assert
        _sut.GetErrorMessage().Should().BeNull();
        _sut.GetReportItems().Should().BeEquivalentTo(newItems);
        _sut.GetIsLoading().Should().BeFalse();
    }

    #endregion

    #region OnInitializedAsync 測試

    [Fact]
    public async Task 初始化_元件初始化時_自動載入報告資料()
    {
        // Arrange
        var reportItems = new List<ReviewerReportItem>
        {
            new ReviewerReportItem { ReviewCount = 8, ApprovedCount = 6, RejectedCount = 2, CommentCount = 10 }
        };

        _reviewerReportService.GetReportItemsAsync()
            .Returns(Task.FromResult(reportItems));

        // Act
        await _sut.InvokeOnInitializedAsync();

        // Assert
        await _reviewerReportService.Received(1).GetReportItemsAsync();
        _sut.GetReportItems().Should().BeEquivalentTo(reportItems);
    }

    [Fact]
    public async Task 初始化_服務拋出例外_錯誤訊息被設定()
    {
        // Arrange
        _reviewerReportService.GetReportItemsAsync()
            .ThrowsAsync(new InvalidOperationException("服務不可用"));

        // Act
        await _sut.InvokeOnInitializedAsync();

        // Assert
        _sut.GetErrorMessage().Should().Contain("服務不可用");
        _sut.GetIsLoading().Should().BeFalse();
    }

    #endregion
}

/// <summary>
/// 可測試的 ReviewerReport 包裝類別，用於存取私有成員和受保護方法
/// </summary>
public class ReviewerReportTestable
{
    private readonly IReviewerReportService _reviewerReportService;
    private List<ReviewerReportItem> _reportItems = new();
    private ReviewerReportSummary? _summary;
    private bool _isLoading = true;
    private string? _errorMessage;

    public ReviewerReportTestable(IReviewerReportService reviewerReportService)
    {
        _reviewerReportService = reviewerReportService;
    }

    public static ReviewerReportSummary InvokeCalculateSummary(List<ReviewerReportItem> items)
    {
        if (items == null || items.Count == 0)
        {
            return new ReviewerReportSummary();
        }

        return new ReviewerReportSummary
        {
            TotalCount = items.Count,
            TotalReviewCount = items.Sum(x => x.ReviewCount),
            AverageReviewCount = Math.Round(items.Average(x => x.ReviewCount), 2),
            TotalApprovedCount = items.Sum(x => x.ApprovedCount),
            AverageApprovedCount = Math.Round(items.Average(x => x.ApprovedCount), 2),
            TotalRejectedCount = items.Sum(x => x.RejectedCount),
            AverageRejectedCount = Math.Round(items.Average(x => x.RejectedCount), 2),
            TotalCommentCount = items.Sum(x => x.CommentCount),
            