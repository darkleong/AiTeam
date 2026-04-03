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

        private List<ReviewerReportItem> GetTestData()
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

        private ReviewerReportSummary CalculateExpectedSummary(List<ReviewerReportItem> data)
        {
            var totalReviews = data.Sum(x => x.TotalReviews);
            var approvedCount = data.Sum(x => x.ApprovedCount);
            var returnedCount = data.Sum(x => x.ReturnedCount);
            var pendingCount = data.Sum(x => x.PendingCount);
            var averageReviewDays = data.Count > 0
                ? Math.Round(data.Sum(x => x.AverageReviewDays * x.TotalReviews) / totalReviews, 1)
                : 0.0;

            return new ReviewerReportSummary
            {
                TotalReviews = totalReviews,
                ApprovedCount = approvedCount,
                ReturnedCount = returnedCount,
                PendingCount = pendingCount,
                AverageReviewDays = averageReviewDays
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
            var testData = GetTestData();
            var expected = CalculateExpectedSummary(testData);

            await _page.GotoAsync($"{BaseUrl}/reviewer-report");
            await _page.WaitForSelectorAsync("[data-testid='summary-row']");

            var totalReviewsCell = await _page.QuerySelectorAsync("[data-testid='summary-total-reviews']");
            Assert.NotNull(totalReviewsCell);

            var cellText = await totalReviewsCell.InnerTextAsync();
            Assert.Equal(expected.TotalReviews.ToString(), cellText.Trim());
        }

        [Fact]
        public async Task SummaryRow_ShouldDisplayCorrectApprovedCount()
        {
            var testData = GetTestData();
            var expected = CalculateExpectedSummary(testData);

            await _page.GotoAsync($"{BaseUrl}/reviewer-report");
            await _page.WaitForSelectorAsync("[data-testid='summary-row']");

            var approvedCell = await _page.QuerySelectorAsync("[data-testid='summary-approved-count']");
            Assert.NotNull(approvedCell);

            var cellText = await approvedCell.InnerTextAsync();
            Assert.Equal(expected.ApprovedCount.ToString(), cellText.Trim());
        }

        [Fact]
        public async Task SummaryRow_ShouldDisplayCorrectReturnedCount()
        {
            var testData = GetTestData();
            var expected = CalculateExpectedSummary(testData);

            await _page.GotoAsync($"{BaseUrl}/reviewer-report");
            await _page.WaitForSelectorAsync("[data-testid='summary-row']");

            var returnedCell = await _page.QuerySelectorAsync("[data-testid='summary-returned-count']");
            Assert.NotNull(returnedCell);

            var cellText = await returnedCell.InnerTextAsync();
            Assert.Equal(expected.ReturnedCount.ToString(), cellText.Trim());
        }

        [Fact]
        public async Task SummaryRow_ShouldDisplayCorrectPendingCount()
        {
            var testData = GetTestData();
            var expected = CalculateExpectedSummary(testData);

            await _page.GotoAsync($"{BaseUrl}/reviewer-report");
            await _page.WaitForSelectorAsync("[data-testid='summary-row']");

            var pendingCell = await _page.QuerySelectorAsync("[data-testid='summary-pending-count']");
            Assert.NotNull(pendingCell);

            var cellText = await pendingCell.InnerTextAsync();
            Assert.Equal(expected.PendingCount.ToString(), cellText.Trim());
        }

        [Fact]
        public async Task SummaryRow_ShouldDisplayWeightedAverageReviewDays()
        {
            var testData = GetTestData();
            var expected = CalculateExpectedSummary(testData);

            await _page.GotoAsync($"{BaseUrl}/reviewer-report");
            await _page.WaitForSelectorAsync("[data-testid='summary-row']");

            var avgDaysCell = await _page.QuerySelectorAsync("[data-testid='summary-average-review-days']");
            Assert.NotNull(avgDaysCell);

            var cellText = await avgDaysCell.InnerTextAsync();
            Assert.Equal(expected.AverageReviewDays.ToString("F1"), cellText.Trim());
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
            Assert.NotNull(summaryRow);

            var className = await summaryRow.GetAttributeAsync("class");
            Assert.NotNull(className);
            Assert.Contains("summary", className.ToLower());
        }

        [Fact]
        public async Task SummaryRow_ShouldHaveBoldFontWeight()
        {
            await _page.GotoAsync($"{BaseUrl}/reviewer-report");
            await _page.WaitForSelectorAsync("[data-testid='summary-row']");

            var summaryRow = await _page.QuerySelectorAsync("[data-testid='summary-row']");
            Assert.NotNull(summaryRow);

            var fontWeight = await summaryRow.EvaluateAsync<string>("el => window.getComputedStyle(el).fontWeight");
            Assert.True(fontWeight == "bold" || fontWeight == "700", $"Expected bold font weight but got: {fontWeight}");
        }

        [Fact]
        public async Task SummaryRow_ShouldHaveDistinctBackgroundColor()
        {
            await _page.GotoAsync($"{BaseUrl}/reviewer-report");
            await _page.WaitForSelectorAsync("[data-testid='summary-row']");

            var summaryRow = await _page.QuerySelectorAsync("[data-testid='summary-row']");
            Assert.NotNull(summaryRow);

            var dataRows = await _page.QuerySelectorAllAsync("[data-testid='reviewer-report-table'] tbody tr:not([data-testid='summary-row'])");
            if (dataRows.Count > 0)
            {
                var summaryBg = await summaryRow.EvaluateAsync<string>("el => window.getComputedStyle(el).backgroundColor");
                var dataRowBg = await dataRows[0].EvaluateAsync<string>("el => window.getComputedStyle(el).backgroundColor");
                Assert.NotEqual(dataRowBg, summaryBg);
            }
        }

        [Fact]
        public async Task SummaryRow_ShouldUpdateWhenFilterApplied()
        {
            await _page.GotoAsync($"{BaseUrl}/reviewer-report");
            await _page.WaitForSelectorAsync("[data-testid='reviewer-report-table']");

            var initialTotalCell = await _page.QuerySelectorAsync("[data-testid='summary-total-reviews']");
            Assert.NotNull(initialTotalCell);
            var initialTotal = await initialTotalCell.InnerTextAsync();

            var filterInput = await _page.QuerySelectorAsync("[data-testid='reviewer-filter']");
            if (filterInput != null)
            {
                await filterInput.FillAsync("Alice");
                await _page.WaitForTimeoutAsync(500);

                var filteredTotalCell = await _page.QuerySelectorAsync("[data-testid='summary-total-reviews']");
                Assert.NotNull(filteredTotalCell);
                var filteredTotal = await filteredTotalCell.InnerTextAsync();

                Assert.NotEqual(initialTotal.Trim(), filteredTotal.Trim());
            }
        }

        [Fact]
        public async Task SummaryRow_ShouldShowZeroWhenNoData()
        {
            await _page.GotoAsync($"{BaseUrl}/reviewer-report?empty=true");
            await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            var noDataMessage = await _page.QuerySelectorAsync("[data-testid='no-data-message']");
            if (noDataMessage != null)
            {
                var summaryRow = await _page.QuerySelectorAsync("[data-testid='summary-row']");
                if (summaryRow != null)
                {
                    var totalCell = await _page.QuerySelectorAsync("[data-testid='summary-total-reviews']");
                    Assert.NotNull(totalCell);
                    var cellText = await totalCell.InnerTextAsync();
                    Assert.Equal("0", cellText.Trim());

                    var approvedCell = await _page.QuerySelectorAsync("[data-testid='summary-approved-count']");
                    Assert.NotNull(approvedCell);
                    Assert.Equal("0", (await approvedCell.InnerTextAsync()).Trim());

                    var returnedCell = await _page.QuerySelectorAsync("[data-testid='summary-returned-count']");
                    Assert.NotNull(returnedCell);
                    Assert.Equal("0", (await returnedCell.InnerTextAsync()).Trim());

                    var pendingCell = await _page.QuerySelectorAsync("[data-testid='summary-pending-count']");
                    Assert.NotNull(pendingCell);
                    Assert.Equal("0", (await pendingCell.InnerTextAsync()).Trim());

                    var avgCell = await _page.QuerySelectorAsync("[data-testid='summary-average-review-days']");
                    Assert.NotNull(avgCell);
                    Assert.Equal("0.0", (await avgCell.InnerTextAsync()).Trim());
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
            Assert.Equal(summary.TotalReviews, summary.ApprovedCount + summary.ReturnedCount + summary.PendingCount);
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

            Assert.NotNull(totalCell);
            Assert.NotNull(approvedCell);
            Assert.NotNull(returnedCell);
            Assert.NotNull(pendingCell);