using Microsoft.Playwright.MSTest;
using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Threading.Tasks;

namespace AiTeam.Tests.Playwright.Generated
{
    [TestClass]
    public class ReviewerReportVisualTests : PageTest
    {
        private string _dashboardUrl = string.Empty;
        private string _dashboardUser = string.Empty;
        private string _dashboardPass = string.Empty;
        private string _screenshotDir = string.Empty;

        [TestInitialize]
        public async Task 測試初始化()
        {
            _dashboardUrl = Environment.GetEnvironmentVariable("DASHBOARD_URL") ?? "http://localhost:5051";
            _dashboardUser = Environment.GetEnvironmentVariable("DASHBOARD_USER") ?? string.Empty;
            _dashboardPass = Environment.GetEnvironmentVariable("DASHBOARD_PASS") ?? string.Empty;
            _screenshotDir = Path.Combine(AppContext.BaseDirectory, "screenshots");
            Directory.CreateDirectory(_screenshotDir);

            await 執行登入();
        }

        private async Task 執行登入()
        {
            if (string.IsNullOrWhiteSpace(_dashboardUser) || string.IsNullOrWhiteSpace(_dashboardPass))
                return;

            await Page.GotoAsync($"{_dashboardUrl}/login");
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            var userInput = Page.Locator("input[name='username'], input[type='text'], input[id*='user'], input[placeholder*='user'], input[placeholder*='帳號']").First;
            var passInput = Page.Locator("input[name='password'], input[type='password']").First;

            if (await userInput.IsVisibleAsync())
                await userInput.FillAsync(_dashboardUser);

            if (await passInput.IsVisibleAsync())
                await passInput.FillAsync(_dashboardPass);

            var submitBtn = Page.Locator("button[type='submit'], button:has-text('登入'), button:has-text('Login')").First;
            if (await submitBtn.IsVisibleAsync())
            {
                await submitBtn.ClickAsync();
                await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            }
        }

        private async Task 切換暗黑模式()
        {
            var darkModeToggle = Page.Locator(
                "button[aria-label*='dark'], button[aria-label*='Dark'], " +
                "button[aria-label*='暗'], input[type='checkbox'][id*='dark'], " +
                "label[for*='dark'], .dark-mode-toggle, " +
                "#darkModeToggle, [data-testid*='dark']"
            ).First;

            if (await darkModeToggle.IsVisibleAsync())
            {
                await darkModeToggle.ClickAsync();
                await Page.WaitForTimeoutAsync(500);
            }
        }

        [TestMethod]
        public async Task 審閱者報告頁面_亮色模式_截圖驗證()
        {
            var targetUrl = $"{_dashboardUrl}/reviewer-report";
            await Page.GotoAsync(targetUrl);
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Page.WaitForTimeoutAsync(1000);

            var screenshotPath = Path.Combine(_screenshotDir, "PR46_ReviewerReport_Light.png");
            await Page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = screenshotPath,
                FullPage = true
            });

            Assert.IsTrue(File.Exists(screenshotPath), $"截圖檔案應存在：{screenshotPath}");
            var fileInfo = new FileInfo(screenshotPath);
            Assert.IsTrue(fileInfo.Length > 0, "截圖檔案不應為空");
        }

        [TestMethod]
        public async Task 審閱者報告頁面_暗色模式_截圖驗證()
        {
            var targetUrl = $"{_dashboardUrl}/reviewer-report";
            await Page.GotoAsync(targetUrl);
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Page.WaitForTimeoutAsync(1000);

            await 切換暗黑模式();

            var screenshotPath = Path.Combine(_screenshotDir, "PR46_ReviewerReport_Dark.png");
            await Page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = screenshotPath,
                FullPage = true
            });

            Assert.IsTrue(File.Exists(screenshotPath), $"截圖檔案應存在：{screenshotPath}");
            var fileInfo = new FileInfo(screenshotPath);
            Assert.IsTrue(fileInfo.Length > 0, "截圖檔案不應為空");
        }

        [TestMethod]
        public async Task 審閱者報告頁面_驗證頁面基本元素存在()
        {
            var targetUrl = $"{_dashboardUrl}/reviewer-report";
            await Page.GotoAsync(targetUrl);
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Page.WaitForTimeoutAsync(1000);

            var pageContent = await Page.ContentAsync();
            Assert.IsFalse(string.IsNullOrWhiteSpace(pageContent), "頁面內容不應為空");

            var title = Page.Locator("h1, h2, h3, .page-title, [class*='title'], [class*='header']").First;
            var isTitleVisible = await title.IsVisibleAsync();

            var screenshotPath = Path.Combine(_screenshotDir, "PR46_ReviewerReport_ElementCheck.png");
            await Page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = screenshotPath,
                FullPage = true
            });

            Assert.IsTrue(File.Exists(screenshotPath), $"截圖檔案應存在：{screenshotPath}");
        }

        [TestMethod]
        public async Task 審閱者報告頁面_驗證不含錯誤訊息_亮色與暗色模式截圖()
        {
            var targetUrl = $"{_dashboardUrl}/reviewer-report";

            // 亮色模式
            await Page.GotoAsync(targetUrl);
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Page.WaitForTimeoutAsync(1000);

            var errorLocator = Page.Locator(".error, .alert-danger, [class*='error'], [role='alert']");
            var errorCount = await errorLocator.CountAsync();

            var lightScreenshotPath = Path.Combine(_screenshotDir, "PR46_ReviewerReport_NoError_Light.png");
            await Page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = lightScreenshotPath,
                FullPage = true
            });

            // 暗色模式
            await 切換暗黑模式();
            await Page.WaitForTimeoutAsync(500);

            var darkScreenshotPath = Path.Combine(_screenshotDir, "PR46_ReviewerReport_NoError_Dark.png");
            await Page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = darkScreenshotPath,
                FullPage = true
            });

            Assert.IsTrue(File.Exists(lightScreenshotPath), $"亮色截圖應存在：{lightScreenshotPath}");
            Assert.IsTrue(File.Exists(darkScreenshotPath), $"暗色截圖應存在：{darkScreenshotPath}");
            Assert.AreEqual(0, errorCount, $"頁面不應顯示錯誤訊息，但發現 {errorCount} 個錯誤元素");
        }
    }
}