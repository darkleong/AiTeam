
```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Playwright;
using Xunit;

namespace Tests.Generated
{
    public class ReviewerReportSummaryRowTests : IAsyncLifetime
    {
        private IPlaywright _playwright;
        private IBrowser _browser;
        private IBrowserContext _context;
        private IPage _page;
        private HttpClient _httpClient;
        private const string BaseUrl = "http://localhost:3000";
        private const string ApiBaseUrl = "http://localhost:3001";

        public async Task InitializeAsync()
        {
            _playwright = await Playwright.CreateAsync();
            _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true
            });
            _context = await _browser.NewContextAsync();
            _page = await _context.NewPageAsync();
            _httpClient = new HttpClient();
        }

        public async Task DisposeAsync()
        {
            _httpClient?.Dispose();
            await _context?.DisposeAsync();
            await _browser?.DisposeAsync();
            _playwright?.Dispose();
        }

        private async Task<List<ReviewerReportItem>> GetTestData()
        {
            return new List<ReviewerReportItem>
            {
                new ReviewerReportItem
                {
                    ReviewerId = "R001",
                    ReviewerName = "Alice Chen",
                    TotalReviews = 45,
                    ApprovedCount = 38,
                    ReturnedCount = 7,
                    PendingCount = 0,
                    AverageReviewDays = 2.3
                },
                new ReviewerReportItem
                {
                    ReviewerId = "R002",
                    ReviewerName = "Bob Wang",
                    TotalReviews = 32,
                    ApprovedCount = 25,
                    ReturnedCount = 5,
                    PendingCount = 2,
                    AverageReviewDays = 3.1
                },
                new ReviewerReportItem
                {
                    ReviewerId = "R003",
                    ReviewerName = "Carol Liu",
                    TotalReviews = 28,
                    ApprovedCount = 20,
                    ReturnedCount = 6,
                    PendingCount = 2,
                    AverageReviewDays = 2.8
                }
            };
        }

        [Fact]
        public async Task SummaryRow_ShouldExistAtBottomOfTable()
        {
            await _page.GotoAsync($"{BaseUrl}/reviewer-report");
            await _page.WaitForSelectorAsync("[data-testid='reviewer-report-table']");

            var summaryRow = await _page.QuerySelectorAsync("[data-testid='summary-row']");
            Assert.NotNull(summaryRow);
        }

        [Fact]
        public async Task SummaryRow_ShouldDisplayCorrectTotalReviews()
        {
            var testData = await GetTestData();
            var expectedTotal = testData.Sum(x => x.TotalReviews);

            await _page.GotoAsync($"{BaseUrl}/reviewer-report");
            await _page.WaitForSelectorAsync("[data-testid='summary-row']");

            var totalReviewsCell = await _page.QuerySelectorAsync("[data-testid='summary-total-reviews']");
            Assert.NotNull(totalReviewsCell);

            var cellText = await totalReviewsCell.InnerTextAsync();
            Assert.Equal(expectedTotal.ToString(), cellText.Trim());
        }

        [Fact]
        public async Task SummaryRow_ShouldDisplayCorrectApprovedCount()
        {
            var testData = await GetTestData();
            var expectedApproved = testData.Sum(x => x.ApprovedCount);

            await _page.GotoAsync($"{BaseUrl}/reviewer-report");
            await _page.WaitForSelectorAsync("[data-testid='summary-row']");

            var approvedCell = await _page.QuerySelectorAsync("[data-testid='summary-approved-count']");
            Assert.NotNull(approvedCell);

            var cellText = await approvedCell.InnerTextAsync();
            Assert.Equal(expectedApproved.ToString(), cellText.Trim());
        }

        [Fact]
        public async Task SummaryRow_ShouldDisplayCorrectReturnedCount()
        {
            var testData = await GetTestData();
            var expectedReturned = testData.Sum(x => x.ReturnedCount);

            await _page.GotoAsync($"{BaseUrl}/reviewer-report");
            await _page.WaitForSelectorAsync("[data-testid='summary-row']");

            var returnedCell = await _page.QuerySelectorAsync("[data-testid='summary-returned-count']");
            Assert.NotNull(returnedCell);

            var cellText = await returnedCell.InnerTextAsync();
            Assert.Equal(expectedReturned.ToString(), cellText.Trim());
        }

        [Fact]
        public async Task SummaryRow_ShouldDisplayCorrectPendingCount()
        {
            var testData = await GetTestData();
            var expectedPending = testData.Sum(x => x.PendingCount);

            await _page.GotoAsync($"{BaseUrl}/reviewer-report");
            await _page.WaitForSelectorAsync("[data-testid='summary-row']");

            var pendingCell = await _page.QuerySelectorAsync("[data-testid='summary-pending-count']");
            Assert.NotNull(pendingCell);

            var cellText = await pendingCell.InnerTextAsync();
            Assert.Equal(expectedPending.ToString(), cellText.Trim());
        }

        [Fact]
        public async Task SummaryRow_ShouldDisplayAverageReviewDays()
        {
            var testData = await GetTestData();
            var expectedAverage = Math.Round(testData.Average(x => x.AverageReviewDays), 1);

            await _page.GotoAsync($"{BaseUrl}/reviewer-report");
            await _page.WaitForSelectorAsync("[data-testid='summary-row']");

            var avgDaysCell = await _page.QuerySelectorAsync("[data-testid='summary-average-review-days']");
            Assert.NotNull(avgDaysCell);

            var cellText = await avgDaysCell.InnerTextAsync();
            Assert.Equal(expectedAverage.ToString("F1"), cellText.Trim());
        }

        [Fact]
        public async Task SummaryRow_ShouldHaveSummaryLabel()
        {
            await _page.GotoAsync($"{BaseUrl}/reviewer-report");
            await _page.WaitForSelectorAsync("[data-testid='summary-row']");

            var summaryLabel = await _page.QuerySelectorAsync("[data-testid='summary-label']");
            Assert.NotNull(summaryLabel);

            var labelText = await summaryLabel.InnerTextAsync();
            Assert.Contains("總計", labelText);
        }

        [Fact]
        public async Task SummaryRow_ShouldBeLastRowInTable()
        {
            await _page.GotoAsync($"{BaseUrl}/reviewer-report");
            await _page.WaitForSelectorAsync("[data-testid='reviewer-report-table']");

            var allRows = await _page.QuerySelectorAllAsync("[data-testid='reviewer-report-table'] tbody tr");
            Assert.True(allRows.Count > 0);

            var lastRow = allRows[allRows.Count - 1];
            var lastRowTestId = await lastRow.GetAttributeAsync("data-testid");
            Assert.Equal("summary-row", lastRowTestId);
        }

        [Fact]
        public async Task SummaryRow_ShouldHaveDistinctStyling()
        {
            await _page.GotoAsync($"{BaseUrl}/reviewer-report");
            await _page.WaitForSelectorAsync("[data-testid='summary-row']");

            var summaryRow = await _page.QuerySelectorAsync("[data-testid='summary-row']");
            var className = await summaryRow.GetAttributeAsync("class");

            Assert.NotNull(className);
            Assert.Contains("summary", className.ToLower());
        }

        [Fact]
        public async Task SummaryRow_ShouldUpdateWhenFilterApplied()
        {
            await _page.GotoAsync($"{BaseUrl}/reviewer-report");
            await _page.WaitForSelectorAsync("[data-testid='reviewer-report-table']");

            var initialTotalCell = await _page.QuerySelectorAsync("[data-testid='summary-total-reviews']");
            var initialTotal = await initialTotalCell.InnerTextAsync();

            var filterInput = await _page.QuerySelectorAsync("[data-testid='reviewer-filter']");
            if (filterInput != null)
            {
                await filterInput.FillAsync("Alice");
                await _page.WaitForTimeoutAsync(500);

                var filteredTotalCell = await _page.QuerySelectorAsync("[data-testid='summary-total-reviews']");
                var filteredTotal = await filteredTotalCell.InnerTextAsync();

                Assert.NotEqual(initialTotal, filteredTotal);
            }
        }

        [Fact]
        public async Task SummaryRow_ShouldShowZeroWhenNoData()
        {
            await _page.GotoAsync($"{BaseUrl}/reviewer-report?empty=true");

            var noDataMessage = await _page.QuerySelectorAsync("[data-testid='no-data-message']");
            if (noDataMessage != null)
            {
                var summaryRow = await _page.QuerySelectorAsync("[data-testid='summary-row']");
                if (summaryRow != null)
                {
                    var totalCell = await _page.QuerySelectorAsync("[data-testid='summary-total-reviews']");
                    var cellText = await totalCell.InnerTextAsync();
                    Assert.Equal("0", cellText.Trim());
                }
            }
        }

        [Fact]
        public async Task SummaryRow_ApiReturnsCorrectSummaryData()
        {
            var response = await _httpClient.GetAsync($"{ApiBaseUrl}/api/reviewer-report/summary");
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var summary = JsonSerializer.Deserialize<ReviewerReportSummary>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            Assert.NotNull(summary);
            Assert.True(summary.TotalReviews >= 0);
            Assert.True(summary.ApprovedCount >= 0);
            Assert.True(summary.ReturnedCount >= 0);
            Assert.True(summary.PendingCount >= 0);
            Assert.True(summary.AverageReviewDays >= 0);
        }

        [Fact]
        public async Task SummaryRow_TotalShouldEqualSumOfApprovedReturnedPending()
        {
            await _page.GotoAsync($"{BaseUrl}/reviewer-report");
            await _page.WaitForSelectorAsync("[data-testid='summary-row']");

            var totalCell = await _page.QuerySelectorAsync("[data-testid='summary-total-reviews']");
            var approvedCell = await _page.QuerySelectorAsync("[data-testid='summary-approved-count']");
            var returnedCell = await _page.QuerySelectorAsync("[data-testid='summary-returned-count']");
            var pendingCell = await _page.QuerySelectorAsync("[data-testid='summary-pending-count']");

            if (totalCell != null && approvedCell != null && returnedCell != null && pendingCell != null)
            {
                var total = int.Parse((await totalCell.InnerTextAsync()).Trim());
                var approved = int.Parse((await approvedCell.InnerTextAsync()).Trim());
                var returned = int.Parse((await returnedCell.InnerTextAsync()).Trim());
                var pending = int.Parse((await pendingCell.InnerTextAsync()).Trim());

                Assert.Equal(total, approved + returned + pending);
            }
        }

        [Fact]
        public async Task SummaryRow_ShouldBeVisibleWithoutScrolling()
        {
            await _page.GotoAsync($"{BaseUrl}/reviewer-report");
            await _page.WaitForSelectorAsync("[data-testid='summary-row']");

            var summaryRow = await _page.QuerySelectorAsync("[data-testid='summary-row']");
            Assert.NotNull(summaryRow);

            var isVisible = await summaryRow.IsVisibleAsync();
            Assert.True(isVisible);
        }

        [Fact]
        public async Task SummaryRow_ShouldHaveCorrectColumnCount()
        {
            await _page.GotoAsync($"{BaseUrl}/reviewer-report");
            await _page.WaitForSelectorAsync("[data-testid='summary-row']");

            var headerRow = await _page.QuerySelectorAsync("[data-testid='reviewer-report-table'] thead tr");
            var summaryRow = await _page.QuerySelectorAsync("[data-testid='summary-row']");

            if (headerRow != null && summaryRow != null)
            {
                var headerCells = await headerRow.QuerySelectorAllAsync("th");
                var summaryCells = await summaryRow.QuerySelectorAllAsync("td");

                Assert.Equal(headerCells.Count, summaryCells.Count);
            }
        }
    }

    public class ReviewerReportItem
    {
        public string ReviewerId { get; set; }
        public string ReviewerName { get; set; }
        public int TotalReviews { get; set; }
        public int ApprovedCount { get; set; }
        public int ReturnedCount { get; set; }
        public int PendingCount { get; set; }
        public double AverageReviewDays { get; set; }
    }

    public class ReviewerReportSummary
    {
        public int TotalReviews { get; set; }
        public int ApprovedCount { get; set; }
        public int ReturnedCount { get; set; }
        public int PendingCount { get; set; }
        public double AverageReviewDays { get; set; }
    }
}
```