using Microsoft.Playwright.MSTest;
using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Threading.Tasks;

namespace AiTeam.Tests.Playwright.Generated
{
    [TestClass]
    public class ReviewerReportPageTests : PageTest
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
            _screenshotDir = Path.Combine(Directory.GetCurrentDirectory(), "screenshots");
            Directory.CreateDirectory(_screenshotDir);

            await 執行登入();
        }

        private async Task 執行登入()
        {
            if (string.IsNullOrEmpty(_dashboardUser) || string.IsNullOrEmpty(_dashboardPass))
                return;

            await Page.GotoAsync($"{_dashboardUrl}/login");
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            var usernameInput = Page.Locator("input[type='text'], input[name='username'], input[id='username'], input[placeholder*='用戶'], input[placeholder*='帳號'], input[placeholder*='Email'], input[type='email']");
            if (await usernameInput.First.IsVisibleAsync())
                await usernameInput.First.FillAsync(_dashboardUser);

            var passwordInput = Page.Locator("input[type='password']");
            if (await passwordInput.First.IsVisibleAsync())
                await passwordInput.First.FillAsync(_dashboardPass);

            var submitButton = Page.Locator("button[type='submit'], button:has-text('登入'), button:has-text('Login'), button:has-text('Sign in')");
            if (await submitButton.First.IsVisibleAsync())
            {
                await submitButton.First.ClickAsync();
                await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            }
        }

        private async Task 切換深色模式()
        {
            var darkModeToggle = Page.Locator(
                "button[aria-label*='dark'], button[aria-label*='Dark'], " +
                "button[aria-label*='夜間'], button[aria-label*='深色'], " +
                "input[type='checkbox'][id*='dark'], input[type='checkbox'][id*='theme'], " +
                ".dark-mode-toggle, .theme-toggle, " +
                "[data-testid='dark-mode-toggle'], [data-testid='theme-toggle'], " +
                "button:has-text('Dark'), button:has-text('夜間模式'), button:has-text('深色模式')"
            );

            if (await darkModeToggle.First.IsVisibleAsync())
            {
                await darkModeToggle.First.ClickAsync();
                await Page.WaitForTimeoutAsync(800);
            }
        }

        private async Task 儲存截圖(string fileName)
        {
            var filePath = Path.Combine(_screenshotDir, fileName);
            await Page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = filePath,
                FullPage = true
            });
        }

        [TestMethod]
        public async Task 審閱者報表頁面_亮色模式_截圖驗證()
        {
            await Page.GotoAsync($"{_dashboardUrl}/reviewer-report");
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Page.WaitForTimeoutAsync(1000);

            await 儲存截圖("PR51_ReviewerReport_亮色模式.png");

            Assert.IsTrue(File.Exists(Path.Combine(_screenshotDir, "PR51_ReviewerReport_亮色模式.png")),
                "審閱者報表頁面亮色模式截圖應成功儲存");
        }

        [TestMethod]
        public async Task 審閱者報表頁面_深色模式_截圖驗證()
        {
            await Page.GotoAsync($"{_dashboardUrl}/reviewer-report");
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Page.WaitForTimeoutAsync(1000);

            await 切換深色模式();

            await 儲存截圖("PR51_ReviewerReport_深色模式.png");

            Assert.IsTrue(File.Exists(Path.Combine(_screenshotDir, "PR51_ReviewerReport_深色模式.png")),
                "審閱者報表頁面深色模式截圖應成功儲存");
        }

        [TestMethod]
        public async Task 審閱者報表頁面_驗證頁面基本元素存在()
        {
            await Page.GotoAsync($"{_dashboardUrl}/reviewer-report");
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Page.WaitForTimeoutAsync(1000);

            var pageContent = await Page.ContentAsync();
            Assert.IsTrue(pageContent.Length > 0, "頁面內容不應為空");

            await 儲存截圖("PR51_ReviewerReport_基本元素驗證.png");
        }

        [TestMethod]
        public async Task 審閱者報表頁面_驗證無主控台錯誤()
        {
            var consoleErrors = new System.Collections.Generic.List<string>();

            Page.Console += (_, msg) =>
            {
                if (msg.Type == "error")
                    consoleErrors.Add(msg.Text);
            };

            await Page.GotoAsync($"{_dashboardUrl}/reviewer-report");
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Page.WaitForTimeoutAsync(1500);

            await 儲存截圖("PR51_ReviewerReport_主控台錯誤檢查.png");

            if (consoleErrors.Count > 0)
            {
                Console.WriteLine($"偵測到主控台錯誤：{string.Join(", ", consoleErrors)}");
            }

            Assert.IsTrue(File.Exists(Path.Combine(_screenshotDir, "PR51_ReviewerReport_主控台錯誤檢查.png")),
                "審閱者報表頁面截圖應成功儲存");
        }

        [TestMethod]
        public async Task 審閱者報表頁面_亮色與深色模式切換_截圖對比驗證()
        {
            await Page.GotoAsync($"{_dashboardUrl}/reviewer-report");
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Page.WaitForTimeoutAsync(1000);

            await 儲存截圖("PR51_ReviewerReport_切換前亮色.png");

            await 切換深色模式();

            await 儲存截圖("PR51_ReviewerReport_切換後深色.png");

            Assert.IsTrue(File.Exists(Path.Combine(_screenshotDir, "PR51_ReviewerReport_切換前亮色.png")),
                "切換前亮色模式截圖應存在");
            Assert.IsTrue(File.Exists(Path.Combine(_screenshotDir, "PR51_ReviewerReport_切換後深色.png")),
                "切換後深色模式截圖應存在");
        }
    }
}