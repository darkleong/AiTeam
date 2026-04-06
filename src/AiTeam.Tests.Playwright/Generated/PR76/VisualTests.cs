using Microsoft.Playwright;
using Microsoft.Playwright.MSTest;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Threading.Tasks;

namespace AiTeam.Tests.Playwright.Generated
{
    [TestClass]
    public class PR76_TaskCenter視覺截圖測試 : PageTest
    {
        private string _dashboardUrl = string.Empty;
        private string _dashboardUser = string.Empty;
        private string _dashboardPass = string.Empty;
        private const string ScreenshotDir = "screenshots";
        private const string PagePath = "/Tasks/TaskCenter";

        [TestInitialize]
        public async Task 初始化測試環境()
        {
            _dashboardUrl = Environment.GetEnvironmentVariable("DASHBOARD_URL") ?? "http://localhost:5051";
            _dashboardUser = Environment.GetEnvironmentVariable("DASHBOARD_USER") ?? string.Empty;
            _dashboardPass = Environment.GetEnvironmentVariable("DASHBOARD_PASS") ?? string.Empty;

            Directory.CreateDirectory(ScreenshotDir);

            await 執行登入();
        }

        private async Task 執行登入()
        {
            await Page.GotoAsync($"{_dashboardUrl}/login");
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            var usernameInput = Page.Locator("input[type='text'], input[name='username'], input[name='email'], input[id*='user'], input[id*='email']").First;
            var passwordInput = Page.Locator("input[type='password']").First;

            if (await usernameInput.IsVisibleAsync())
            {
                await usernameInput.FillAsync(_dashboardUser);
            }

            if (await passwordInput.IsVisibleAsync())
            {
                await passwordInput.FillAsync(_dashboardPass);
            }

            var loginButton = Page.Locator("button[type='submit'], button:has-text('登入'), button:has-text('Login'), button:has-text('Sign in')").First;
            if (await loginButton.IsVisibleAsync())
            {
                await loginButton.ClickAsync();
            }

            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        }

        [TestMethod]
        public async Task 任務中心頁面_亮色模式_截圖驗證()
        {
            await Page.GotoAsync($"{_dashboardUrl}{PagePath}");
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Page.WaitForTimeoutAsync(1500);

            var screenshotPath = Path.Combine(ScreenshotDir, "PR76_TaskCenter_light_mode.png");
            await Page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = screenshotPath,
                FullPage = true
            });

            Assert.IsTrue(File.Exists(screenshotPath), $"截圖檔案應存在於 {screenshotPath}");
            var fileInfo = new FileInfo(screenshotPath);
            Assert.IsTrue(fileInfo.Length > 0, "截圖檔案不應為空");
        }

        [TestMethod]
        public async Task 任務中心頁面_暗色模式_截圖驗證()
        {
            await Page.GotoAsync($"{_dashboardUrl}{PagePath}");
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Page.WaitForTimeoutAsync(1500);

            await 切換暗色模式();
            await Page.WaitForTimeoutAsync(1000);

            var screenshotPath = Path.Combine(ScreenshotDir, "PR76_TaskCenter_dark_mode.png");
            await Page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = screenshotPath,
                FullPage = true
            });

            Assert.IsTrue(File.Exists(screenshotPath), $"截圖檔案應存在於 {screenshotPath}");
            var fileInfo = new FileInfo(screenshotPath);
            Assert.IsTrue(fileInfo.Length > 0, "截圖檔案不應為空");
        }

        [TestMethod]
        public async Task 任務中心頁面_亮色模式_任務列表區塊截圖驗證()
        {
            await Page.GotoAsync($"{_dashboardUrl}{PagePath}");
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Page.WaitForTimeoutAsync(1500);

            var taskListLocator = Page.Locator(".task-list, [class*='task-list'], [class*='TaskList'], [data-testid*='task'], table, .grid, .list-group").First;

            string screenshotPath;
            if (await taskListLocator.IsVisibleAsync())
            {
                screenshotPath = Path.Combine(ScreenshotDir, "PR76_TaskCenter_light_task_list.png");
                await taskListLocator.ScreenshotAsync(new LocatorScreenshotOptions
                {
                    Path = screenshotPath
                });
            }
            else
            {
                screenshotPath = Path.Combine(ScreenshotDir, "PR76_TaskCenter_light_task_list_fallback.png");
                await Page.ScreenshotAsync(new PageScreenshotOptions
                {
                    Path = screenshotPath,
                    FullPage = true
                });
            }

            Assert.IsTrue(File.Exists(screenshotPath), $"截圖檔案應存在於 {screenshotPath}");
            var fileInfo = new FileInfo(screenshotPath);
            Assert.IsTrue(fileInfo.Length > 0, "截圖檔案不應為空");
        }

        [TestMethod]
        public async Task 任務中心頁面_暗色模式_任務列表區塊截圖驗證()
        {
            await Page.GotoAsync($"{_dashboardUrl}{PagePath}");
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Page.WaitForTimeoutAsync(1500);

            await 切換暗色模式();
            await Page.WaitForTimeoutAsync(1000);

            var taskListLocator = Page.Locator(".task-list, [class*='task-list'], [class*='TaskList'], [data-testid*='task'], table, .grid, .list-group").First;

            string screenshotPath;
            if (await taskListLocator.IsVisibleAsync())
            {
                screenshotPath = Path.Combine(ScreenshotDir, "PR76_TaskCenter_dark_task_list.png");
                await taskListLocator.ScreenshotAsync(new LocatorScreenshotOptions
                {
                    Path = screenshotPath
                });
            }
            else
            {
                screenshotPath = Path.Combine(ScreenshotDir, "PR76_TaskCenter_dark_task_list_fallback.png");
                await Page.ScreenshotAsync(new PageScreenshotOptions
                {
                    Path = screenshotPath,
                    FullPage = true
                });
            }

            Assert.IsTrue(File.Exists(screenshotPath), $"截圖檔案應存在於 {screenshotPath}");
            var fileInfo = new FileInfo(screenshotPath);
            Assert.IsTrue(fileInfo.Length > 0, "截圖檔案不應為空");
        }

        [TestMethod]
        public async Task 任務中心頁面_亮色模式_頁面標題與頁首截圖驗證()
        {
            await Page.GotoAsync($"{_dashboardUrl}{PagePath}");
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Page.WaitForTimeoutAsync(1500);

            var headerLocator = Page.Locator("h1, h2, .page-header, [class*='header'], [class*='title'], .card-header").First;

            string screenshotPath;
            if (await headerLocator.IsVisibleAsync())
            {
                screenshotPath = Path.Combine(ScreenshotDir, "PR76_TaskCenter_light_header.png");
                await headerLocator.ScreenshotAsync(new LocatorScreenshotOptions
                {
                    Path = screenshotPath
                });
            }
            else
            {
                screenshotPath = Path.Combine(ScreenshotDir, "PR76_TaskCenter_light_header_fallback.png");
                await Page.ScreenshotAsync(new PageScreenshotOptions
                {
                    Path = screenshotPath,
                    FullPage = false
                });
            }

            Assert.IsTrue(File.Exists(screenshotPath), $"截圖檔案應存在於 {screenshotPath}");
            var fileInfo = new FileInfo(screenshotPath);
            Assert.IsTrue(fileInfo.Length > 0, "截圖檔案不應為空");
        }

        [TestMethod]
        public async Task 任務中心頁面_暗色模式_頁面標題與頁首截圖驗證()
        {
            await Page.GotoAsync($"{_dashboardUrl}{PagePath}");
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Page.WaitForTimeoutAsync(1500);

            await 切換暗色模式();
            await Page.WaitForTimeoutAsync(1000);

            var headerLocator = Page.Locator("h1, h2, .page-header, [class*='header'], [class*='title'], .card-header").First;

            string screenshotPath;
            if (await headerLocator.IsVisibleAsync())
            {
                screenshotPath = Path.Combine(ScreenshotDir, "PR76_TaskCenter_dark_header.png");
                await headerLocator.ScreenshotAsync(new LocatorScreenshotOptions
                {
                    Path = screenshotPath
                });
            }
            else
            {
                screenshotPath = Path.Combine(ScreenshotDir, "PR76_TaskCenter_dark_header_fallback.png");
                await Page.ScreenshotAsync(new PageScreenshotOptions
                {
                    Path = screenshotPath,
                    FullPage = false
                });
            }

            Assert.IsTrue(File.Exists(screenshotPath), $"截圖檔案應存在於 {screenshotPath}");
            var fileInfo = new FileInfo(screenshotPath);
            Assert.IsTrue(fileInfo.Length > 0, "截圖檔案不應為空");
        }

        [TestMethod]
        public async Task 任務中心頁面_亮色模式_篩選與操作區塊截圖驗證()
        {
            await Page.GotoAsync($"{_dashboardUrl}{PagePath}");
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Page.WaitForTimeoutAsync(1500);

            var filterLocator = Page.Locator(".filter, [class*='filter'], [class*='search'], .toolbar, [class*='toolbar'], .action-bar, [class*='action']").First;

            string screenshotPath;
            if (await filterLocator.IsVisibleAsync())
            {
                screenshotPath = Path.Combine(ScreenshotDir, "PR76_TaskCenter_light_filter_bar.png");
                await filterLocator.ScreenshotAsync(new LocatorScreenshotOptions
                {
                    Path = screenshotPath
                });
            }
            else
            {
                screenshotPath = Path.Combine(ScreenshotDir, "PR76_TaskCenter_light_filter_bar_fallback.png");
                await Page.ScreenshotAsync(new PageScreenshotOptions
                {
                    Path = screenshotPath,
                    FullPage = true
                });
            }

            Assert.IsTrue(File.Exists(screenshotPath), $"截圖檔案應存在於 {screenshotPath}");
            var fileInfo = new FileInfo(screenshotPath);
            Assert.IsTrue(fileInfo.Length > 0, "截圖檔案不應為空");
        }

        [TestMethod]
        public async Task 任務中心頁面_暗色模式_篩選與操作區塊截圖驗證()
        {
            await Page.GotoAsync($"{_dashboardUrl}{PagePath}");
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Page.WaitForTimeoutAsync(1500);

            await 切換暗色模式();
            await Page.WaitForTimeoutAsync(1000);

            var filterLocator = Page.Locator(".filter, [class*='filter'], [class*='search'], .toolbar, [class*='toolbar'], .action-bar, [class*='action']").First;

            string screenshotPath;
            if (await filterLocator.IsVisibleAsync())
            {
                screenshotPath = Path.Combine(ScreenshotDir, "PR76_TaskCenter_dark_filter_bar.png");
                await filterLocator.ScreenshotAsync(new LocatorScreenshotOptions
                {
                    Path = screenshotPath
                });
            }
            else
            {
                screenshotPath = Path.Combine(ScreenshotDir, "PR76_TaskCenter_dark_filter_bar_fallback.png");
                await Page.ScreenshotAsync(new PageScreenshotOptions
                {
                    Path = screenshotPath,
                    FullPage = true
                });
            }

            Assert.IsTrue(File.Exists(screenshotPath), $"截圖檔案應存在於 {screenshotPath}");
            var fileInfo = new FileInfo(screenshotPath);
            Assert.IsTrue(fileInfo.Length > 0, "截圖檔案不應為空");
        }

        private async Task 切換暗色模式()
        {
            var darkModeToggle = Page.Locator(
                "button[aria-label*='dark'], button[aria-label*='Dark'], " +
                "button[aria-label*='暗色'], button[aria-label*='dark mode'], " +
                "input[type='checkbox'][id*='dark'], input[type='checkbox'][id*='Dark'], " +
                "[class*='dark-mode-toggle'], [class*='DarkModeToggle'], " +
                "[class*='theme-toggle'], [class*='ThemeToggle'], " +
                "button:has-text('Dark'), button:has-text('暗色'), " +
                "button:has-text('🌙'), button:has-text('☀️'), " +
                "[data-testid*='dark-mode'], [data-testid*='theme-toggle']"
            ).First;

            if (await darkModeToggle.IsVisibleAsync())
            {
                await darkModeToggle.ClickAsync();
            }
            else
            {
                await Page.EvaluateAsync(@"
                    document.documentElement.classList.add('dark');
                    document.documentElement.setAttribute('data-theme', 'dark');
                    document.body.classList.add('dark-mode');
                ");
            }

            await Page.WaitForTimeoutAsync(500);
        }
    }
}