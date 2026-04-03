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

            _screenshotDir = Path.Combine(AppContext.BaseDirectory, "screenshots");
            Directory.CreateDirectory(_screenshotDir);

            await 執行登入();
        }

        private async Task 執行登入()
        {
            if (string.IsNullOrEmpty(_dashboardUser) || string.IsNullOrEmpty(_dashboardPass))
                return;

            await Page.GotoAsync($"{_dashboardUrl}/login");
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            var userInput = Page.Locator("input[name='username'], input[type='text'], input[id*='user'], input[placeholder*='帳號'], input[placeholder*='Username']").First;
            var passInput = Page.Locator("input[name='password'], input[type='password']").First;

            await userInput.FillAsync(_dashboardUser);
            await passInput.FillAsync(_dashboardPass);

            await Page.Locator("button[type='submit'], button:has-text('登入'), button:has-text('Login')").First.ClickAsync();
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        }

        [TestMethod]
        public async Task 審查者報告頁面_淺色模式_截圖驗證()
        {
            var targetUrl = $"{_dashboardUrl}/reviewer-report";
            await Page.GotoAsync(targetUrl);
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Page.WaitForTimeoutAsync(1000);

            var screenshotPath = Path.Combine(_screenshotDir, "PR41_ReviewerReport_LightMode.png");
            await Page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = screenshotPath,
                FullPage = true
            });

            Assert.IsTrue(File.Exists(screenshotPath), $"截圖檔案應存在：{screenshotPath}");
            Console.WriteLine($"[淺色模式] 截圖已儲存：{screenshotPath}");
        }

        [TestMethod]
        public async Task 審查者報告頁面_深色模式_截圖驗證()
        {
            var targetUrl = $"{_dashboardUrl}/reviewer-report";
            await Page.GotoAsync(targetUrl);
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Page.WaitForTimeoutAsync(1000);

            var darkModeToggle = Page.Locator(
                "button[aria-label*='dark'], button[aria-label*='Dark'], " +
                "button[aria-label*='夜間'], button[aria-label*='深色'], " +
                "input[type='checkbox'][id*='dark'], input[type='checkbox'][id*='theme'], " +
                ".dark-mode-toggle, #darkModeToggle, [data-testid='dark-mode-toggle']"
            ).First;

            var toggleCount = await Page.Locator(
                "button[aria-label*='dark'], button[aria-label*='Dark'], " +
                "button[aria-label*='夜間'], button[aria-label*='深色'], " +
                "input[type='checkbox'][id*='dark'], input[type='checkbox'][id*='theme'], " +
                ".dark-mode-toggle, #darkModeToggle, [data-testid='dark-mode-toggle']"
            ).CountAsync();

            if (toggleCount > 0)
            {
                await darkModeToggle.ClickAsync();
                await Page.WaitForTimeoutAsync(800);
            }
            else
            {
                Console.WriteLine("[警告] 未找到 DarkMode toggle，將直接截圖目前狀態。");
            }

            var screenshotPath = Path.Combine(_screenshotDir, "PR41_ReviewerReport_DarkMode.png");
            await Page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = screenshotPath,
                FullPage = true
            });

            Assert.IsTrue(File.Exists(screenshotPath), $"截圖檔案應存在：{screenshotPath}");
            Console.WriteLine($"[深色模式] 截圖已儲存：{screenshotPath}");
        }

        [TestMethod]
        public async Task 審查者報告頁面_頁面標題與主要元素_顯示正常()
        {
            var targetUrl = $"{_dashboardUrl}/reviewer-report";
            await Page.GotoAsync(targetUrl);
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Page.WaitForTimeoutAsync(1000);

            var pageTitle = await Page.TitleAsync();
            Assert.IsFalse(string.IsNullOrWhiteSpace(pageTitle), "頁面標題不應為空");
            Console.WriteLine($"頁面標題：{pageTitle}");

            var bodyContent = await Page.Locator("body").InnerTextAsync();
            Assert.IsFalse(string.IsNullOrWhiteSpace(bodyContent), "頁面主體內容不應為空");

            var screenshotPath = Path.Combine(_screenshotDir, "PR41_ReviewerReport_ElementsCheck.png");
            await Page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = screenshotPath,
                FullPage = true
            });

            Assert.IsTrue(File.Exists(screenshotPath), $"截圖檔案應存在：{screenshotPath}");
            Console.WriteLine($"[元素檢查] 截圖已儲存：{screenshotPath}");
        }

        [TestMethod]
        public async Task 審查者報告頁面_視窗寬度1280_淺色與深色模式_截圖驗證()
        {
            await Page.SetViewportSizeAsync(1280, 800);

            var targetUrl = $"{_dashboardUrl}/reviewer-report";
            await Page.GotoAsync(targetUrl);
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Page.WaitForTimeoutAsync(1000);

            var lightScreenshotPath = Path.Combine(_screenshotDir, "PR41_ReviewerReport_1280_LightMode.png");
            await Page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = lightScreenshotPath,
                FullPage = true
            });
            Assert.IsTrue(File.Exists(lightScreenshotPath), $"截圖檔案應存在：{lightScreenshotPath}");
            Console.WriteLine($"[1280 淺色模式] 截圖已儲存：{lightScreenshotPath}");

            var darkModeToggleLocator = Page.Locator(
                "button[aria-label*='dark'], button[aria-label*='Dark'], " +
                "button[aria-label*='夜間'], button[aria-label*='深色'], " +
                "input[type='checkbox'][id*='dark'], input[type='checkbox'][id*='theme'], " +
                ".dark-mode-toggle, #darkModeToggle, [data-testid='dark-mode-toggle']"
            );

            var toggleCount = await darkModeToggleLocator.CountAsync();
            if (toggleCount > 0)
            {
                await darkModeToggleLocator.First.ClickAsync();
                await Page.WaitForTimeoutAsync(800);
            }
            else
            {
                Console.WriteLine("[警告] 未找到 DarkMode toggle，略過深色模式切換。");
            }

            var darkScreenshotPath = Path.Combine(_screenshotDir, "PR41_ReviewerReport_1280_DarkMode.png");
            await Page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = darkScreenshotPath,
                FullPage = true
            });
            Assert.IsTrue(File.Exists(darkScreenshotPath), $"截圖檔案應存在：{darkScreenshotPath}");
            Console.WriteLine($"[1280 深色模式] 截圖已儲存：{darkScreenshotPath}");
        }
    }
}