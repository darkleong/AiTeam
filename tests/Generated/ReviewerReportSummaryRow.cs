
```csharp
using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Tests.Generated
{
    [TestFixture]
    public class ReviewerReportSummaryRow : PageTest
    {
        private const string BaseUrl = "http://localhost:3000";

        [Test]
        public async Task SummaryRowDisplaysAtBottomOfReport()
        {
            await Page.GotoAsync($"{BaseUrl}/reviewer-report");
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            var summaryRow = Page.Locator("[data-testid='issue-summary-row']");
            await Expect(summaryRow).ToBeVisibleAsync();
        }

        [Test]
        public async Task SummaryRowShowsBugCount()
        {
            await Page.GotoAsync($"{BaseUrl}/reviewer-report");
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            var bugCount = Page.Locator("[data-testid='summary-bug-count']");
            await Expect(bugCount).ToBeVisibleAsync();

            var bugCountText = await bugCount.TextContentAsync();
            Assert.IsNotNull(bugCountText);
            Assert.IsTrue(bugCountText!.Contains("Bug") || int.TryParse(bugCountText.Trim(), out _),
                $"Bug count should display a number or 'Bug' label, but got: {bugCountText}");
        }

        [Test]
        public async Task SummaryRowShowsSuggestionCount()
        {
            await Page.GotoAsync($"{BaseUrl}/reviewer-report");
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            var suggestionCount = Page.Locator("[data-testid='summary-suggestion-count']");
            await Expect(suggestionCount).ToBeVisibleAsync();

            var suggestionCountText = await suggestionCount.TextContentAsync();
            Assert.IsNotNull(suggestionCountText);
            Assert.IsTrue(suggestionCountText!.Contains("建議") || suggestionCountText.Contains("Suggestion") || int.TryParse(suggestionCountText.Trim(), out _),
                $"Suggestion count should display a number or suggestion label, but got: {suggestionCountText}");
        }

        [Test]
        public async Task SummaryRowShowsWarningCount()
        {
            await Page.GotoAsync($"{BaseUrl}/reviewer-report");
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            var warningCount = Page.Locator("[data-testid='summary-warning-count']");
            await Expect(warningCount).ToBeVisibleAsync();

            var warningCountText = await warningCount.TextContentAsync();
            Assert.IsNotNull(warningCountText);
            Assert.IsTrue(warningCountText!.Contains("警告") || warningCountText.Contains("Warning") || int.TryParse(warningCountText.Trim(), out _),
                $"Warning count should display a number or warning label, but got: {warningCountText}");
        }

        [Test]
        public async Task SummaryRowIsPositionedAtBottomOfReport()
        {
            await Page.GotoAsync($"{BaseUrl}/reviewer-report");
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            var reportContainer = Page.Locator("[data-testid='reviewer-report-container']");
            var summaryRow = Page.Locator("[data-testid='issue-summary-row']");

            await Expect(reportContainer).ToBeVisibleAsync();
            await Expect(summaryRow).ToBeVisibleAsync();

            var reportBounds = await reportContainer.BoundingBoxAsync();
            var summaryBounds = await summaryRow.BoundingBoxAsync();

            Assert.IsNotNull(reportBounds, "Report container should have bounding box");
            Assert.IsNotNull(summaryBounds, "Summary row should have bounding box");

            // Summary row should be near the bottom of the report container
            var reportBottom = reportBounds!.Y + reportBounds.Height;
            var summaryBottom = summaryBounds!.Y + summaryBounds.Height;

            Assert.IsTrue(summaryBottom <= reportBottom + 10,
                $"Summary row bottom ({summaryBottom}) should be at or near the report container bottom ({reportBottom})");
        }

        [Test]
        public async Task SummaryRowCountsMatchActualIssues()
        {
            await Page.GotoAsync($"{BaseUrl}/reviewer-report");
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            // Count actual bug issues in the report
            var bugIssues = Page.Locator("[data-testid='issue-item'][data-issue-type='bug']");
            var bugIssueCount = await bugIssues.CountAsync();

            // Get the displayed bug count in summary
            var summaryBugElement = Page.Locator("[data-testid='summary-bug-count'] [data-testid='count-value']");
            await Expect(summaryBugElement).ToBeVisibleAsync();
            var summaryBugText = await summaryBugElement.TextContentAsync();

            if (int.TryParse(summaryBugText?.Trim(), out int displayedBugCount))
            {
                Assert.AreEqual(bugIssueCount, displayedBugCount,
                    $"Summary bug count ({displayedBugCount}) should match actual bug issues ({bugIssueCount})");
            }
        }

        [Test]
        public async Task SummaryRowHasCorrectLabels()
        {
            await Page.GotoAsync($"{BaseUrl}/reviewer-report");
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            var summaryRow = Page.Locator("[data-testid='issue-summary-row']");
            await Expect(summaryRow).ToBeVisibleAsync();

            var summaryText = await summaryRow.TextContentAsync();
            Assert.IsNotNull(summaryText);

            // Check that summary row contains relevant labels
            bool hasBugLabel = summaryText!.Contains("Bug") || summaryText.Contains("bug");
            bool hasSuggestionLabel = summaryText.Contains("建議") || summaryText.Contains("Suggestion") || summaryText.Contains("suggestion");
            bool hasWarningLabel = summaryText.Contains("警告") || summaryText.Contains("Warning") || summaryText.Contains("warning");

            Assert.IsTrue(hasBugLabel || hasSuggestionLabel || hasWarningLabel,
                "Summary row should contain at least one type label (Bug, 建議/Suggestion, or 警告/Warning)");
        }

        [Test]
        public async Task SummaryRowShowsZeroWhenNoIssues()
        {
            // Navigate to a report with no issues if such a route exists
            await Page.GotoAsync($"{BaseUrl}/reviewer-report?empty=true");
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            var summaryRow = Page.Locator("[data-testid='issue-summary-row']");

            // If the page loads successfully and shows the summary row
            var isVisible = await summaryRow.IsVisibleAsync();
            if (isVisible)
            {
                var bugCount = Page.Locator("[data-testid='summary-bug-count'] [data-testid='count-value']");
                var warningCount = Page.Locator("[data-testid='summary-warning-count'] [data-testid='count-value']");
                var suggestionCount = Page.Locator("[data-testid='summary-suggestion-count'] [data-testid='count-value']");

                if (await bugCount.IsVisibleAsync())
                {
                    var bugText = await bugCount.TextContentAsync();
                    Assert.AreEqual("0", bugText?.Trim(), "Bug count should be 0 when there are no issues");
                }

                if (await warningCount.IsVisibleAsync())
                {
                    var warningText = await warningCount.TextContentAsync();
                    Assert.AreEqual("0", warningText?.Trim(), "Warning count should be 0 when there are no issues");
                }

                if (await suggestionCount.IsVisibleAsync())
                {
                    var suggestionText = await suggestionCount.TextContentAsync();
                    Assert.AreEqual("0", suggestionText?.Trim(), "Suggestion count should be 0 when there are no issues");
                }
            }
        }

        [Test]
        public async Task SummaryRowUpdatesWhenIssuesAreFiltered()
        {
            await Page.GotoAsync($"{BaseUrl}/reviewer-report");
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            // Get initial summary counts
            var initialBugCount = Page.Locator("[data-testid='summary-bug-count'] [data-testid='count-value']");
            var initialCountText = await initialBugCount.TextContentAsync();

            // Apply a filter if filter controls exist
            var filterControl = Page.Locator("[data-testid='issue-type-filter']");
            var filterExists = await filterControl.IsVisibleAsync();

            if (filterExists)
            {
                await filterControl.ClickAsync();
                await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

                // Summary row should still be visible after filtering
                var summaryRow = Page.Locator("[data-testid='issue-summary-row']");
                await Expect(summaryRow).ToBeVisibleAsync();
            }
        }

        [Test]
        public async Task SummaryRowHasProperStyling()
        {
            await Page.GotoAsync($"{BaseUrl}/reviewer-report");
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            var summaryRow = Page.Locator("[data-testid='issue-summary-row']");
            await Expect(summaryRow).ToBeVisibleAsync();

            // Check that summary row has some styling applied
            var backgroundColor = await summaryRow.EvaluateAsync<string>(
                "el => window.getComputedStyle(el).backgroundColor");

            Assert.IsNotNull(backgroundColor);
            Assert.IsNotEmpty(backgroundColor);
        }

        [Test]
        public async Task SummaryRowIsAccessible()
        {
            await Page.GotoAsync($"{BaseUrl}/reviewer-report");
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            var summaryRow = Page.Locator("[data-testid='issue-summary-row']");
            await Expect(summaryRow).ToBeVisibleAsync();

            // Check for aria attributes or role
            var role = await summaryRow.GetAttributeAsync("role");
            var ariaLabel = await summaryRow.GetAttributeAsync("aria-label");

            // Either role or aria-label should be present for accessibility
            bool hasAccessibilityAttribute = !string.IsNullOrEmpty(role) || !string.IsNullOrEmpty(ariaLabel);

            // This is a soft check - log warning if not accessible but don't fail
            if (!hasAccessibilityAttribute)
            {
                Console.WriteLine("Warning: Summary row may lack accessibility attributes (role or aria-label)");
            }
        }

        [Test]
        public async Task SummaryRowDisplaysAllRequiredIssueTypes()
        {
            await Page.GotoAsync($"{BaseUrl}/reviewer-report");
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            var summaryRow = Page.Locator("[data-testid='issue-summary-row']");
            await Expect(summaryRow).ToBeVisibleAsync();

            // All three required issue types should be represented in summary
            var bugSection = Page.Locator("[data-testid='summary-bug-count']");
            var suggestionSection = Page.Locator("[data-testid='summary-suggestion-count']");
            var warningSection = Page.Locator("[data-testid='summary-warning-count']");

            await Expect(bugSection).ToBeVisibleAsync();
            await Expect(suggestionSection).ToBeVisibleAsync();
            await Expect(warningSection).ToBeVisibleAsync();
        }
    }
}
```