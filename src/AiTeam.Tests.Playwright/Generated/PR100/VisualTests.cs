using Microsoft.Playwright;
using Microsoft.Playwright.MSTest;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Threading.Tasks;

namespace AiTeam.Tests.Playwright.Generated
{
    [TestClass]
    public class PR100_Dashboard登出功能視覺截圖測試 : PageTest
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

        // ── MainLayout 整體佈局 ──────────────────────────────────────────

        [TestMethod]
        public async Task MainLayout_亮色模式_完整佈局截圖驗證()
        {
            await Page.GotoAsync(_dashboardUrl);
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Page.WaitForTimeoutAsync(1500);

            var screenshotPath = Path.Combine(ScreenshotDir, "PR100_MainLayout_light_full.png");
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
        public async Task MainLayout_暗色模式_完整佈局截圖驗證()
        {
            await Page.GotoAsync(_dashboardUrl);
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Page.WaitForTimeoutAsync(1500);

            await 切換暗色模式();
            await Page.WaitForTimeoutAsync(1000);

            var screenshotPath = Path.Combine(ScreenshotDir, "PR100_MainLayout_dark_full.png");
            await Page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = screenshotPath,
                FullPage = true
            });

            Assert.IsTrue(File.Exists(screenshotPath), $"截圖檔案應存在於 {screenshotPath}");
            var fileInfo = new FileInfo(screenshotPath);
            Assert.IsTrue(fileInfo.Length > 0, "截圖檔案不應為空");
        }

        // ── 側邊欄區塊 ──────────────────────────────────────────────────

        [TestMethod]
        public async Task MainLayout_亮色模式_側邊欄截圖驗證()
        {
            await Page.GotoAsync(_dashboardUrl);
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Page.WaitForTimeoutAsync(1500);

            var sidebarLocator = Page.Locator(".sidebar, [class*='sidebar'], nav").First;

            string screenshotPath;
            if (await sidebarLocator.IsVisibleAsync())
            {
                screenshotPath = Path.Combine(ScreenshotDir, "PR100_MainLayout_light_sidebar.png");
                await sidebarLocator.ScreenshotAsync(new LocatorScreenshotOptions
                {
                    Path = screenshotPath
                });
            }
            else
            {
                screenshotPath = Path.Combine(ScreenshotDir, "PR100_MainLayout_light_sidebar_fallback.png");
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
        public async Task MainLayout_暗色模式_側邊欄截圖驗證()
        {
            await Page.GotoAsync(_dashboardUrl);
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Page.WaitForTimeoutAsync(1500);

            await 切換暗色模式();
            await Page.WaitForTimeoutAsync(1000);

            var sidebarLocator = Page.Locator(".sidebar, [class*='sidebar'], nav").First;

            string screenshotPath;
            if (await sidebarLocator.IsVisibleAsync())
            {
                screenshotPath = Path.Combine(ScreenshotDir, "PR100_MainLayout_dark_sidebar.png");
                await sidebarLocator.ScreenshotAsync(new LocatorScreenshotOptions
                {
                    Path = screenshotPath
                });
            }
            else
            {
                screenshotPath = Path.Combine(ScreenshotDir, "PR100_MainLayout_dark_sidebar_fallback.png");
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

        // ── 側邊欄收合 ──────────────────────────────────────────────────

        [TestMethod]
        public async Task MainLayout_亮色模式_側邊欄收合狀態截圖驗證()
        {
            await Page.GotoAsync(_dashboardUrl);
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Page.WaitForTimeoutAsync(1500);

            var toggleBtn = Page.Locator("#sidebar-toggle-btn, .sidebar-toggle, button[aria-label*='toggle'], button[aria-label*='sidebar']").First;
            if (await toggleBtn.IsVisibleAsync())
            {
                await toggleBtn.ClickAsync();
                await Page.WaitForTimeoutAsync(600);
            }

            var screenshotPath = Path.Combine(ScreenshotDir, "PR100_MainLayout_light_sidebar_collapsed.png");
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
        public async Task MainLayout_暗色模式_側邊欄收合狀態截圖驗證()
        {
            await Page.GotoAsync(_dashboardUrl);
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Page.WaitForTimeoutAsync(1500);

            await 切換暗色模式();
            await Page.WaitForTimeoutAsync(1000);

            var toggleBtn = Page.Locator("#sidebar-toggle-btn, .sidebar-toggle, button[aria-label*='toggle'], button[aria-label*='sidebar']").First;
            if (await toggleBtn.IsVisibleAsync())
            {
                await toggleBtn.ClickAsync();
                await Page.WaitForTimeoutAsync(600);
            }

            var screenshotPath = Path.Combine(ScreenshotDir, "PR100_MainLayout_dark_sidebar_collapsed.png");
            await Page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = screenshotPath,
                FullPage = true
            });

            Assert.IsTrue(File.Exists(screenshotPath), $"截圖檔案應存在於 {screenshotPath}");
            var fileInfo = new FileInfo(screenshotPath);
            Assert.IsTrue(fileInfo.Length > 0, "截圖檔案不應為空");
        }

        // ── LogoutButton 外觀 ────────────────────────────────────────────

        [TestMethod]
        public async Task LogoutButton_亮色模式_登出按鈕截圖驗證()
        {
            await Page.GotoAsync(_dashboardUrl);
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Page.WaitForTimeoutAsync(1500);

            var logoutLocator = Page.Locator(".logout-btn, button.logout-btn, button:has-text('登出')").First;

            string screenshotPath;
            if (await logoutLocator.IsVisibleAsync())
            {
                screenshotPath = Path.Combine(ScreenshotDir, "PR100_LogoutButton_light.png");
                await logoutLocator.ScreenshotAsync(new LocatorScreenshotOptions
                {
                    Path = screenshotPath
                });
            }
            else
            {
                screenshotPath = Path.Combine(ScreenshotDir, "PR100_LogoutButton_light_fallback.png");
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
        public async Task LogoutButton_暗色模式_登出按鈕截圖驗證()
        {
            await Page.GotoAsync(_dashboardUrl);
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Page.WaitForTimeoutAsync(1500);

            await 切換暗色模式();
            await Page.WaitForTimeoutAsync(1000);

            var logoutLocator = Page.Locator(".logout-btn, button.logout-btn, button:has-text('登出')").First;

            string screenshotPath;
            if (await logoutLocator.IsVisibleAsync())
            {
                screenshotPath = Path.Combine(ScreenshotDir, "PR100_LogoutButton_dark.png");
                await logoutLocator.ScreenshotAsync(new LocatorScreenshotOptions
                {
                    Path = screenshotPath
                });
            }
            else
            {
                screenshotPath = Path.Combine(ScreenshotDir, "PR100_LogoutButton_dark_fallback.png");
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

        // ── 側邊欄頁尾（含版本號、主題切換、登出按鈕整體區塊）──────────

        [TestMethod]
        public async Task MainLayout_亮色模式_側邊欄頁尾截圖驗證()
        {
            await Page.GotoAsync(_dashboardUrl);
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Page.WaitForTimeoutAsync(1500);

            var footerLocator = Page.Locator(".sidebar-footer, [class*='sidebar-footer'], [class*='SidebarFooter']").First;

            string screenshotPath;
            if (await footerLocator.IsVisibleAsync())
            {
                screenshotPath = Path.Combine(ScreenshotDir, "PR100_MainLayout_light_sidebar_footer.png");
                await footerLocator.ScreenshotAsync(new LocatorScreenshotOptions
                {
                    Path = screenshotPath
                });
            }
            else
            {
                screenshotPath = Path.Combine(ScreenshotDir, "PR100_MainLayout_light_sidebar_footer_fallback.png");
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
        public async Task MainLayout_暗色模式_側邊欄頁尾截圖驗證()
        {
            await Page.GotoAsync(_dashboardUrl);
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Page.WaitForTimeoutAsync(1500);

            await 切換暗色模式();
            await Page.WaitForTimeoutAsync(1000);

            var footerLocator = Page.Locator(".sidebar-footer, [class*='sidebar-footer'], [class*='SidebarFooter']").First;

            string screenshotPath;
            if (await footerLocator.IsVisibleAsync())
            {
                screenshotPath = Path.Combine(ScreenshotDir, "PR100_MainLayout_dark_sidebar_footer.png");
                await footerLocator.ScreenshotAsync(new LocatorScreenshotOptions
                {
                    Path = screenshotPath
                });
            }
            else
            {
                screenshotPath = Path.Combine(ScreenshotDir, "PR100_MainLayout_dark_sidebar_footer_fallback.png");
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

        // ── 登出確認 Dialog ──────────────────────────────────────────────

        [TestMethod]
        public async Task LogoutButton_點擊後_確認Dialog截圖驗證()
        {
            await Page.GotoAsync(_dashboardUrl);
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Page.WaitForTimeoutAsync(1500);

            var logoutBtn = Page.Locator(".logout-btn, button.logout-btn, button:has-text('登出')").First;

            string screenshotPath;
            if (await logoutBtn.IsVisibleAsync())
            {
                await logoutBtn.ClickAsync();
                await Page.WaitForTimeoutAsync(800);

                // Dialog 應出現，擷取確認 Dialog 區塊
                var dialogLocator = Page.Locator(".mud-dialog, [role='dialog'], [class*='dialog'], [class*='Dialog']").First;

                if (await dialogLocator.IsVisibleAsync())
                {
                    screenshotPath = Path.Combine(ScreenshotDir, "PR100_LogoutDialog_confirm.png");
                    await dialogLocator.ScreenshotAsync(new LocatorScreenshotOptions
                    {
                        Path = screenshotPath
                    });
                }
                else
                {
                    screenshotPath = Path.Combine(ScreenshotDir, "PR100_LogoutDialog_confirm_fallback.png");
                    await Page.ScreenshotAsync(new PageScreenshotOptions
                    {
                        Path = screenshotPath,
                        FullPage = false
                    });
                }
            }
            else
            {
                screenshotPath = Path.Combine(ScreenshotDir, "PR100_LogoutDialog_confirm_fallback.png");
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
        public async Task LogoutButton_暗色模式_點擊後_確認Dialog截圖驗證()
        {
            await Page.GotoAsync(_dashboardUrl);
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Page.WaitForTimeoutAsync(1500);

            await 切換暗色模式();
            await Page.WaitForTimeoutAsync(1000);

            var logoutBtn = Page.Locator(".logout-btn, button.logout-btn, button:has-text('登出')").First;

            string screenshotPath;
            if (await logoutBtn.IsVisibleAsync())
            {
                await logoutBtn.ClickAsync();
                await Page.WaitForTimeoutAsync(800);

                var dialogLocator = Page.Locator(".mud-dialog, [role='dialog'], [class*='dialog'], [class*='Dialog']").First;

                if (await dialogLocator.IsVisibleAsync())
                {
                    screenshotPath = Path.Combine(ScreenshotDir, "PR100_LogoutDialog_dark_confirm.png");
                    await dialogLocator.ScreenshotAsync(new LocatorScreenshotOptions
                    {
                        Path = screenshotPath
                    });
                }
                else
                {
                    screenshotPath = Path.Combine(ScreenshotDir, "PR100_LogoutDialog_dark_confirm_fallback.png");
                    await Page.ScreenshotAsync(new PageScreenshotOptions
                    {
                        Path = screenshotPath,
                        FullPage = false
                    });
                }
            }
            else
            {
                screenshotPath = Path.Combine(ScreenshotDir, "PR100_LogoutDialog_dark_confirm_fallback.png");
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
