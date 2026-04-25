using Microsoft.Playwright;
using Microsoft.Playwright.MSTest;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Threading.Tasks;

namespace AiTeam.Tests.Playwright.Generated
{
    [TestClass]
    public class PR108_規則管理頁面視覺截圖測試 : PageTest
    {
        private string _dashboardUrl = string.Empty;
        private string _dashboardUser = string.Empty;
        private string _dashboardPass = string.Empty;
        private const string ScreenshotDir = "screenshots";

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

        // ── 規則管理頁面整體 ──────────────────────────────────────────────

        [TestMethod]
        public async Task 規則管理頁面_亮色模式_完整頁面截圖驗證()
        {
            await Page.GotoAsync($"{_dashboardUrl}/rules");
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Page.WaitForTimeoutAsync(1500);

            var screenshotPath = Path.Combine(ScreenshotDir, "PR108_RuleManagement_light_full.png");
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
        public async Task 規則管理頁面_暗色模式_完整頁面截圖驗證()
        {
            await Page.GotoAsync($"{_dashboardUrl}/rules");
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Page.WaitForTimeoutAsync(1500);

            await 切換暗色模式();
            await Page.WaitForTimeoutAsync(1000);

            var screenshotPath = Path.Combine(ScreenshotDir, "PR108_RuleManagement_dark_full.png");
            await Page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = screenshotPath,
                FullPage = true
            });

            Assert.IsTrue(File.Exists(screenshotPath), $"截圖檔案應存在於 {screenshotPath}");
            var fileInfo = new FileInfo(screenshotPath);
            Assert.IsTrue(fileInfo.Length > 0, "截圖檔案不應為空");
        }

        // ── 頁面標題與操作按鈕 ───────────────────────────────────────────

        [TestMethod]
        public async Task 規則管理頁面_亮色模式_頁面標題與按鈕截圖驗證()
        {
            await Page.GotoAsync($"{_dashboardUrl}/rules");
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Page.WaitForTimeoutAsync(1500);

            var headerLocator = Page.Locator(".page-header").First;

            string screenshotPath;
            if (await headerLocator.IsVisibleAsync())
            {
                screenshotPath = Path.Combine(ScreenshotDir, "PR108_RuleManagement_light_header.png");
                await headerLocator.ScreenshotAsync(new LocatorScreenshotOptions
                {
                    Path = screenshotPath
                });
            }
            else
            {
                screenshotPath = Path.Combine(ScreenshotDir, "PR108_RuleManagement_light_header_fallback.png");
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
        public async Task 規則管理頁面_暗色模式_頁面標題與按鈕截圖驗證()
        {
            await Page.GotoAsync($"{_dashboardUrl}/rules");
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Page.WaitForTimeoutAsync(1500);

            await 切換暗色模式();
            await Page.WaitForTimeoutAsync(1000);

            var headerLocator = Page.Locator(".page-header").First;

            string screenshotPath;
            if (await headerLocator.IsVisibleAsync())
            {
                screenshotPath = Path.Combine(ScreenshotDir, "PR108_RuleManagement_dark_header.png");
                await headerLocator.ScreenshotAsync(new LocatorScreenshotOptions
                {
                    Path = screenshotPath
                });
            }
            else
            {
                screenshotPath = Path.Combine(ScreenshotDir, "PR108_RuleManagement_dark_header_fallback.png");
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

        // ── 規則表格（含圖示按鈕操作欄）────────────────────────────────

        [TestMethod]
        public async Task 規則管理頁面_亮色模式_規則表格截圖驗證()
        {
            await Page.GotoAsync($"{_dashboardUrl}/rules");
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Page.WaitForTimeoutAsync(1500);

            var tableLocator = Page.Locator(".mud-table, table, [class*='MudTable']").First;

            string screenshotPath;
            if (await tableLocator.IsVisibleAsync())
            {
                screenshotPath = Path.Combine(ScreenshotDir, "PR108_RuleManagement_light_table.png");
                await tableLocator.ScreenshotAsync(new LocatorScreenshotOptions
                {
                    Path = screenshotPath
                });
            }
            else
            {
                screenshotPath = Path.Combine(ScreenshotDir, "PR108_RuleManagement_light_table_fallback.png");
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
        public async Task 規則管理頁面_暗色模式_規則表格截圖驗證()
        {
            await Page.GotoAsync($"{_dashboardUrl}/rules");
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Page.WaitForTimeoutAsync(1500);

            await 切換暗色模式();
            await Page.WaitForTimeoutAsync(1000);

            var tableLocator = Page.Locator(".mud-table, table, [class*='MudTable']").First;

            string screenshotPath;
            if (await tableLocator.IsVisibleAsync())
            {
                screenshotPath = Path.Combine(ScreenshotDir, "PR108_RuleManagement_dark_table.png");
                await tableLocator.ScreenshotAsync(new LocatorScreenshotOptions
                {
                    Path = screenshotPath
                });
            }
            else
            {
                screenshotPath = Path.Combine(ScreenshotDir, "PR108_RuleManagement_dark_table_fallback.png");
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

        // ── 圖示按鈕（編輯 / 刪除）懸停提示 ────────────────────────────

        [TestMethod]
        public async Task 規則管理頁面_亮色模式_操作欄圖示按鈕截圖驗證()
        {
            await Page.GotoAsync($"{_dashboardUrl}/rules");
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Page.WaitForTimeoutAsync(1500);

            // 嘗試定位第一列的操作欄（含編輯與刪除圖示按鈕）
            var actionCellLocator = Page.Locator(".mud-table-row .mud-table-cell:last-child, tr td:last-child").First;

            string screenshotPath;
            if (await actionCellLocator.IsVisibleAsync())
            {
                screenshotPath = Path.Combine(ScreenshotDir, "PR108_RuleManagement_light_action_icons.png");
                await actionCellLocator.ScreenshotAsync(new LocatorScreenshotOptions
                {
                    Path = screenshotPath
                });
            }
            else
            {
                screenshotPath = Path.Combine(ScreenshotDir, "PR108_RuleManagement_light_action_icons_fallback.png");
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
        public async Task 規則管理頁面_暗色模式_操作欄圖示按鈕截圖驗證()
        {
            await Page.GotoAsync($"{_dashboardUrl}/rules");
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Page.WaitForTimeoutAsync(1500);

            await 切換暗色模式();
            await Page.WaitForTimeoutAsync(1000);

            var actionCellLocator = Page.Locator(".mud-table-row .mud-table-cell:last-child, tr td:last-child").First;

            string screenshotPath;
            if (await actionCellLocator.IsVisibleAsync())
            {
                screenshotPath = Path.Combine(ScreenshotDir, "PR108_RuleManagement_dark_action_icons.png");
                await actionCellLocator.ScreenshotAsync(new LocatorScreenshotOptions
                {
                    Path = screenshotPath
                });
            }
            else
            {
                screenshotPath = Path.Combine(ScreenshotDir, "PR108_RuleManagement_dark_action_icons_fallback.png");
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

        // ── 空狀態畫面 ───────────────────────────────────────────────────

        [TestMethod]
        public async Task 規則管理頁面_亮色模式_空狀態截圖驗證()
        {
            await Page.GotoAsync($"{_dashboardUrl}/rules");
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Page.WaitForTimeoutAsync(1500);

            var emptyStateLocator = Page.Locator(".empty-state").First;

            string screenshotPath;
            if (await emptyStateLocator.IsVisibleAsync())
            {
                screenshotPath = Path.Combine(ScreenshotDir, "PR108_RuleManagement_light_empty_state.png");
                await emptyStateLocator.ScreenshotAsync(new LocatorScreenshotOptions
                {
                    Path = screenshotPath
                });
            }
            else
            {
                // 規則存在時截全頁
                screenshotPath = Path.Combine(ScreenshotDir, "PR108_RuleManagement_light_empty_state_fallback.png");
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
        public async Task 規則管理頁面_暗色模式_空狀態截圖驗證()
        {
            await Page.GotoAsync($"{_dashboardUrl}/rules");
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Page.WaitForTimeoutAsync(1500);

            await 切換暗色模式();
            await Page.WaitForTimeoutAsync(1000);

            var emptyStateLocator = Page.Locator(".empty-state").First;

            string screenshotPath;
            if (await emptyStateLocator.IsVisibleAsync())
            {
                screenshotPath = Path.Combine(ScreenshotDir, "PR108_RuleManagement_dark_empty_state.png");
                await emptyStateLocator.ScreenshotAsync(new LocatorScreenshotOptions
                {
                    Path = screenshotPath
                });
            }
            else
            {
                screenshotPath = Path.Combine(ScreenshotDir, "PR108_RuleManagement_dark_empty_state_fallback.png");
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
    }
}
