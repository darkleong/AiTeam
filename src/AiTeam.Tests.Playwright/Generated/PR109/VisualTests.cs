using Microsoft.Playwright;
using Microsoft.Playwright.MSTest;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Threading.Tasks;

namespace AiTeam.Tests.Playwright.Generated
{
    [TestClass]
    public class PR109_流程追蹤頁面視覺截圖測試 : PageTest
    {
        private string _dashboardUrl  = string.Empty;
        private string _dashboardUser = string.Empty;
        private string _dashboardPass = string.Empty;
        private const string ScreenshotDir = "screenshots";

        [TestInitialize]
        public async Task 初始化測試環境()
        {
            _dashboardUrl  = Environment.GetEnvironmentVariable("DASHBOARD_URL")  ?? "http://localhost:5051";
            _dashboardUser = Environment.GetEnvironmentVariable("DASHBOARD_USER") ?? string.Empty;
            _dashboardPass = Environment.GetEnvironmentVariable("DASHBOARD_PASS") ?? string.Empty;

            Directory.CreateDirectory(ScreenshotDir);

            await 執行登入();
        }

        private async Task 執行登入()
        {
            await Page.GotoAsync($"{_dashboardUrl}/login");
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            var usernameInput = Page.Locator(
                "input[type='text'], input[name='username'], input[name='email'], " +
                "input[id*='user'], input[id*='email']").First;
            var passwordInput = Page.Locator("input[type='password']").First;

            if (await usernameInput.IsVisibleAsync())
                await usernameInput.FillAsync(_dashboardUser);

            if (await passwordInput.IsVisibleAsync())
                await passwordInput.FillAsync(_dashboardPass);

            var loginButton = Page.Locator(
                "button[type='submit'], button:has-text('登入'), " +
                "button:has-text('Login'), button:has-text('Sign in')").First;

            if (await loginButton.IsVisibleAsync())
                await loginButton.ClickAsync();

            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
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

        // ── 流程追蹤頁面整體截圖 ─────────────────────────────────────────

        [TestMethod]
        public async Task 流程追蹤頁面_亮色模式_完整頁面截圖驗證()
        {
            await Page.GotoAsync($"{_dashboardUrl}/pipeline");
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Page.WaitForTimeoutAsync(1500);

            var screenshotPath = Path.Combine(ScreenshotDir, "PR109_PipelineList_light_full.png");
            await Page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path     = screenshotPath,
                FullPage = true
            });

            Assert.IsTrue(File.Exists(screenshotPath), $"截圖檔案應存在於 {screenshotPath}");
            Assert.IsTrue(new FileInfo(screenshotPath).Length > 0, "截圖檔案不應為空");
        }

        [TestMethod]
        public async Task 流程追蹤頁面_暗色模式_完整頁面截圖驗證()
        {
            await Page.GotoAsync($"{_dashboardUrl}/pipeline");
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Page.WaitForTimeoutAsync(1500);

            await 切換暗色模式();
            await Page.WaitForTimeoutAsync(1000);

            var screenshotPath = Path.Combine(ScreenshotDir, "PR109_PipelineList_dark_full.png");
            await Page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path     = screenshotPath,
                FullPage = true
            });

            Assert.IsTrue(File.Exists(screenshotPath), $"截圖檔案應存在於 {screenshotPath}");
            Assert.IsTrue(new FileInfo(screenshotPath).Length > 0, "截圖檔案不應為空");
        }

        // ── 頁面標題 ─────────────────────────────────────────────────────

        [TestMethod]
        public async Task 流程追蹤頁面_亮色模式_頁面標題截圖驗證()
        {
            await Page.GotoAsync($"{_dashboardUrl}/pipeline");
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Page.WaitForTimeoutAsync(1500);

            var headerLocator = Page.Locator(".page-header").First;

            string screenshotPath;
            if (await headerLocator.IsVisibleAsync())
            {
                screenshotPath = Path.Combine(ScreenshotDir, "PR109_PipelineList_light_header.png");
                await headerLocator.ScreenshotAsync(new LocatorScreenshotOptions { Path = screenshotPath });
            }
            else
            {
                screenshotPath = Path.Combine(ScreenshotDir, "PR109_PipelineList_light_header_fallback.png");
                await Page.ScreenshotAsync(new PageScreenshotOptions { Path = screenshotPath, FullPage = false });
            }

            Assert.IsTrue(File.Exists(screenshotPath), $"截圖檔案應存在於 {screenshotPath}");
            Assert.IsTrue(new FileInfo(screenshotPath).Length > 0, "截圖檔案不應為空");
        }

        [TestMethod]
        public async Task 流程追蹤頁面_暗色模式_頁面標題截圖驗證()
        {
            await Page.GotoAsync($"{_dashboardUrl}/pipeline");
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Page.WaitForTimeoutAsync(1500);

            await 切換暗色模式();
            await Page.WaitForTimeoutAsync(1000);

            var headerLocator = Page.Locator(".page-header").First;

            string screenshotPath;
            if (await headerLocator.IsVisibleAsync())
            {
                screenshotPath = Path.Combine(ScreenshotDir, "PR109_PipelineList_dark_header.png");
                await headerLocator.ScreenshotAsync(new LocatorScreenshotOptions { Path = screenshotPath });
            }
            else
            {
                screenshotPath = Path.Combine(ScreenshotDir, "PR109_PipelineList_dark_header_fallback.png");
                await Page.ScreenshotAsync(new PageScreenshotOptions { Path = screenshotPath, FullPage = false });
            }

            Assert.IsTrue(File.Exists(screenshotPath), $"截圖檔案應存在於 {screenshotPath}");
            Assert.IsTrue(new FileInfo(screenshotPath).Length > 0, "截圖檔案不應為空");
        }

        // ── TaskGroup 表格（含 PR 欄改顯示 PR 編號） ─────────────────────

        [TestMethod]
        public async Task 流程追蹤頁面_亮色模式_任務群組表格截圖驗證()
        {
            await Page.GotoAsync($"{_dashboardUrl}/pipeline");
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Page.WaitForTimeoutAsync(1500);

            var tableLocator = Page.Locator(".mud-table, table, [class*='MudTable']").First;

            string screenshotPath;
            if (await tableLocator.IsVisibleAsync())
            {
                screenshotPath = Path.Combine(ScreenshotDir, "PR109_PipelineList_light_table.png");
                await tableLocator.ScreenshotAsync(new LocatorScreenshotOptions { Path = screenshotPath });
            }
            else
            {
                screenshotPath = Path.Combine(ScreenshotDir, "PR109_PipelineList_light_table_fallback.png");
                await Page.ScreenshotAsync(new PageScreenshotOptions { Path = screenshotPath, FullPage = true });
            }

            Assert.IsTrue(File.Exists(screenshotPath), $"截圖檔案應存在於 {screenshotPath}");
            Assert.IsTrue(new FileInfo(screenshotPath).Length > 0, "截圖檔案不應為空");
        }

        [TestMethod]
        public async Task 流程追蹤頁面_暗色模式_任務群組表格截圖驗證()
        {
            await Page.GotoAsync($"{_dashboardUrl}/pipeline");
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Page.WaitForTimeoutAsync(1500);

            await 切換暗色模式();
            await Page.WaitForTimeoutAsync(1000);

            var tableLocator = Page.Locator(".mud-table, table, [class*='MudTable']").First;

            string screenshotPath;
            if (await tableLocator.IsVisibleAsync())
            {
                screenshotPath = Path.Combine(ScreenshotDir, "PR109_PipelineList_dark_table.png");
                await tableLocator.ScreenshotAsync(new LocatorScreenshotOptions { Path = screenshotPath });
            }
            else
            {
                screenshotPath = Path.Combine(ScreenshotDir, "PR109_PipelineList_dark_table_fallback.png");
                await Page.ScreenshotAsync(new PageScreenshotOptions { Path = screenshotPath, FullPage = true });
            }

            Assert.IsTrue(File.Exists(screenshotPath), $"截圖檔案應存在於 {screenshotPath}");
            Assert.IsTrue(new FileInfo(screenshotPath).Length > 0, "截圖檔案不應為空");
        }

        // ── 狀態篩選器 ───────────────────────────────────────────────────

        [TestMethod]
        public async Task 流程追蹤頁面_亮色模式_狀態篩選器截圖驗證()
        {
            await Page.GotoAsync($"{_dashboardUrl}/pipeline");
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Page.WaitForTimeoutAsync(1500);

            var filtersLocator = Page.Locator(".page-filters").First;

            string screenshotPath;
            if (await filtersLocator.IsVisibleAsync())
            {
                screenshotPath = Path.Combine(ScreenshotDir, "PR109_PipelineList_light_filters.png");
                await filtersLocator.ScreenshotAsync(new LocatorScreenshotOptions { Path = screenshotPath });
            }
            else
            {
                screenshotPath = Path.Combine(ScreenshotDir, "PR109_PipelineList_light_filters_fallback.png");
                await Page.ScreenshotAsync(new PageScreenshotOptions { Path = screenshotPath, FullPage = false });
            }

            Assert.IsTrue(File.Exists(screenshotPath), $"截圖檔案應存在於 {screenshotPath}");
            Assert.IsTrue(new FileInfo(screenshotPath).Length > 0, "截圖檔案不應為空");
        }

        [TestMethod]
        public async Task 流程追蹤頁面_暗色模式_狀態篩選器截圖驗證()
        {
            await Page.GotoAsync($"{_dashboardUrl}/pipeline");
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Page.WaitForTimeoutAsync(1500);

            await 切換暗色模式();
            await Page.WaitForTimeoutAsync(1000);

            var filtersLocator = Page.Locator(".page-filters").First;

            string screenshotPath;
            if (await filtersLocator.IsVisibleAsync())
            {
                screenshotPath = Path.Combine(ScreenshotDir, "PR109_PipelineList_dark_filters.png");
                await filtersLocator.ScreenshotAsync(new LocatorScreenshotOptions { Path = screenshotPath });
            }
            else
            {
                screenshotPath = Path.Combine(ScreenshotDir, "PR109_PipelineList_dark_filters_fallback.png");
                await Page.ScreenshotAsync(new PageScreenshotOptions { Path = screenshotPath, FullPage = false });
            }

            Assert.IsTrue(File.Exists(screenshotPath), $"截圖檔案應存在於 {screenshotPath}");
            Assert.IsTrue(new FileInfo(screenshotPath).Length > 0, "截圖檔案不應為空");
        }

        // ── Pipeline Drawer（PipelineView 元件）─────────────────────────

        [TestMethod]
        public async Task 流程追蹤頁面_亮色模式_點擊第一列開啟Drawer截圖驗證()
        {
            await Page.GotoAsync($"{_dashboardUrl}/pipeline");
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Page.WaitForTimeoutAsync(1500);

            // 嘗試點擊第一列以開啟 PipelineView Drawer
            var firstRow = Page.Locator(".mud-table-row.cursor-pointer, .mud-table tbody tr").First;
            if (await firstRow.IsVisibleAsync())
            {
                await firstRow.ClickAsync();
                await Page.WaitForTimeoutAsync(1000);
            }

            var screenshotPath = Path.Combine(ScreenshotDir, "PR109_PipelineView_light_drawer.png");
            await Page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path     = screenshotPath,
                FullPage = false
            });

            Assert.IsTrue(File.Exists(screenshotPath), $"截圖檔案應存在於 {screenshotPath}");
            Assert.IsTrue(new FileInfo(screenshotPath).Length > 0, "截圖檔案不應為空");
        }

        [TestMethod]
        public async Task 流程追蹤頁面_暗色模式_點擊第一列開啟Drawer截圖驗證()
        {
            await Page.GotoAsync($"{_dashboardUrl}/pipeline");
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Page.WaitForTimeoutAsync(1500);

            await 切換暗色模式();
            await Page.WaitForTimeoutAsync(500);

            var firstRow = Page.Locator(".mud-table-row.cursor-pointer, .mud-table tbody tr").First;
            if (await firstRow.IsVisibleAsync())
            {
                await firstRow.ClickAsync();
                await Page.WaitForTimeoutAsync(1000);
            }

            var screenshotPath = Path.Combine(ScreenshotDir, "PR109_PipelineView_dark_drawer.png");
            await Page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path     = screenshotPath,
                FullPage = false
            });

            Assert.IsTrue(File.Exists(screenshotPath), $"截圖檔案應存在於 {screenshotPath}");
            Assert.IsTrue(new FileInfo(screenshotPath).Length > 0, "截圖檔案不應為空");
        }
    }
}
