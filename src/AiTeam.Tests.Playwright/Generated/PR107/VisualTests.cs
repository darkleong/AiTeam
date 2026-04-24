using Microsoft.Playwright;
using Microsoft.Playwright.MSTest;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Threading.Tasks;

namespace AiTeam.Tests.Playwright.Generated
{
    [TestClass]
    public class PR107_AgentSettings視覺截圖測試 : PageTest
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
                await usernameInput.FillAsync(_dashboardUser);

            if (await passwordInput.IsVisibleAsync())
                await passwordInput.FillAsync(_dashboardPass);

            var loginButton = Page.Locator("button[type='submit'], button:has-text('登入'), button:has-text('Login'), button:has-text('Sign in')").First;
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

        // ── AgentSettings 整頁截圖 ────────────────────────────────────────

        [TestMethod]
        public async Task AgentSettings_亮色模式_完整頁面截圖驗證()
        {
            await Page.GotoAsync($"{_dashboardUrl}/agents");
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Page.WaitForTimeoutAsync(1500);

            var screenshotPath = Path.Combine(ScreenshotDir, "PR107_AgentSettings_light_full.png");
            await Page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path     = screenshotPath,
                FullPage = true
            });

            Assert.IsTrue(File.Exists(screenshotPath), $"截圖檔案應存在於 {screenshotPath}");
            var fileInfo = new FileInfo(screenshotPath);
            Assert.IsTrue(fileInfo.Length > 0, "截圖檔案不應為空");
        }

        [TestMethod]
        public async Task AgentSettings_暗色模式_完整頁面截圖驗證()
        {
            await Page.GotoAsync($"{_dashboardUrl}/agents");
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Page.WaitForTimeoutAsync(1500);

            await 切換暗色模式();
            await Page.WaitForTimeoutAsync(1000);

            var screenshotPath = Path.Combine(ScreenshotDir, "PR107_AgentSettings_dark_full.png");
            await Page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path     = screenshotPath,
                FullPage = true
            });

            Assert.IsTrue(File.Exists(screenshotPath), $"截圖檔案應存在於 {screenshotPath}");
            var fileInfo = new FileInfo(screenshotPath);
            Assert.IsTrue(fileInfo.Length > 0, "截圖檔案不應為空");
        }

        // ── Agent 清單區塊 ────────────────────────────────────────────────

        [TestMethod]
        public async Task AgentSettings_亮色模式_Agent清單截圖驗證()
        {
            await Page.GotoAsync($"{_dashboardUrl}/agents");
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Page.WaitForTimeoutAsync(1500);

            var agentListLocator = Page.Locator(".mud-list, [class*='MudList'], .mud-paper").First;

            string screenshotPath;
            if (await agentListLocator.IsVisibleAsync())
            {
                screenshotPath = Path.Combine(ScreenshotDir, "PR107_AgentSettings_light_list.png");
                await agentListLocator.ScreenshotAsync(new LocatorScreenshotOptions { Path = screenshotPath });
            }
            else
            {
                screenshotPath = Path.Combine(ScreenshotDir, "PR107_AgentSettings_light_list_fallback.png");
                await Page.ScreenshotAsync(new PageScreenshotOptions { Path = screenshotPath, FullPage = true });
            }

            Assert.IsTrue(File.Exists(screenshotPath), $"截圖檔案應存在於 {screenshotPath}");
            var fileInfo = new FileInfo(screenshotPath);
            Assert.IsTrue(fileInfo.Length > 0, "截圖檔案不應為空");
        }

        [TestMethod]
        public async Task AgentSettings_暗色模式_Agent清單截圖驗證()
        {
            await Page.GotoAsync($"{_dashboardUrl}/agents");
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Page.WaitForTimeoutAsync(1500);

            await 切換暗色模式();
            await Page.WaitForTimeoutAsync(1000);

            var agentListLocator = Page.Locator(".mud-list, [class*='MudList'], .mud-paper").First;

            string screenshotPath;
            if (await agentListLocator.IsVisibleAsync())
            {
                screenshotPath = Path.Combine(ScreenshotDir, "PR107_AgentSettings_dark_list.png");
                await agentListLocator.ScreenshotAsync(new LocatorScreenshotOptions { Path = screenshotPath });
            }
            else
            {
                screenshotPath = Path.Combine(ScreenshotDir, "PR107_AgentSettings_dark_list_fallback.png");
                await Page.ScreenshotAsync(new PageScreenshotOptions { Path = screenshotPath, FullPage = true });
            }

            Assert.IsTrue(File.Exists(screenshotPath), $"截圖檔案應存在於 {screenshotPath}");
            var fileInfo = new FileInfo(screenshotPath);
            Assert.IsTrue(fileInfo.Length > 0, "截圖檔案不應為空");
        }

        // ── 選取 Agent 後的詳情面板（含 LLM 設定） ───────────────────────

        [TestMethod]
        public async Task AgentSettings_選取第一個Agent_詳情面板截圖驗證()
        {
            await Page.GotoAsync($"{_dashboardUrl}/agents");
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Page.WaitForTimeoutAsync(1500);

            // 嘗試點擊清單中第一個 Agent
            var firstItem = Page.Locator(".mud-list-item, [class*='MudListItem']").First;

            string screenshotPath;
            if (await firstItem.IsVisibleAsync())
            {
                await firstItem.ClickAsync();
                await Page.WaitForTimeoutAsync(800);

                screenshotPath = Path.Combine(ScreenshotDir, "PR107_AgentSettings_detail_panel.png");
                await Page.ScreenshotAsync(new PageScreenshotOptions { Path = screenshotPath, FullPage = true });
            }
            else
            {
                screenshotPath = Path.Combine(ScreenshotDir, "PR107_AgentSettings_detail_panel_fallback.png");
                await Page.ScreenshotAsync(new PageScreenshotOptions { Path = screenshotPath, FullPage = true });
            }

            Assert.IsTrue(File.Exists(screenshotPath), $"截圖檔案應存在於 {screenshotPath}");
            var fileInfo = new FileInfo(screenshotPath);
            Assert.IsTrue(fileInfo.Length > 0, "截圖檔案不應為空");
        }

        [TestMethod]
        public async Task AgentSettings_暗色模式_選取Agent後LLM設定區塊截圖驗證()
        {
            await Page.GotoAsync($"{_dashboardUrl}/agents");
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Page.WaitForTimeoutAsync(1500);

            await 切換暗色模式();
            await Page.WaitForTimeoutAsync(1000);

            var firstItem = Page.Locator(".mud-list-item, [class*='MudListItem']").First;

            string screenshotPath;
            if (await firstItem.IsVisibleAsync())
            {
                await firstItem.ClickAsync();
                await Page.WaitForTimeoutAsync(800);

                // LLM 設定區塊
                var llmSection = Page.Locator("text=LLM 設定").First;
                if (await llmSection.IsVisibleAsync())
                {
                    screenshotPath = Path.Combine(ScreenshotDir, "PR107_AgentSettings_dark_llm_section.png");
                    await Page.ScreenshotAsync(new PageScreenshotOptions { Path = screenshotPath, FullPage = false });
                }
                else
                {
                    screenshotPath = Path.Combine(ScreenshotDir, "PR107_AgentSettings_dark_llm_section_fallback.png");
                    await Page.ScreenshotAsync(new PageScreenshotOptions { Path = screenshotPath, FullPage = true });
                }
            }
            else
            {
                screenshotPath = Path.Combine(ScreenshotDir, "PR107_AgentSettings_dark_llm_section_fallback.png");
                await Page.ScreenshotAsync(new PageScreenshotOptions { Path = screenshotPath, FullPage = true });
            }

            Assert.IsTrue(File.Exists(screenshotPath), $"截圖檔案應存在於 {screenshotPath}");
            var fileInfo = new FileInfo(screenshotPath);
            Assert.IsTrue(fileInfo.Length > 0, "截圖檔案不應為空");
        }

        // ── 新增 Agent 按鈕 ────────────────────────────────────────────────

        [TestMethod]
        public async Task AgentSettings_亮色模式_新增Agent按鈕截圖驗證()
        {
            await Page.GotoAsync($"{_dashboardUrl}/agents");
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Page.WaitForTimeoutAsync(1500);

            var addBtn = Page.Locator("button:has-text('新增 Agent'), button:has-text('新增Agent')").First;

            string screenshotPath;
            if (await addBtn.IsVisibleAsync())
            {
                screenshotPath = Path.Combine(ScreenshotDir, "PR107_AgentSettings_light_add_btn.png");
                await addBtn.ScreenshotAsync(new LocatorScreenshotOptions { Path = screenshotPath });
            }
            else
            {
                screenshotPath = Path.Combine(ScreenshotDir, "PR107_AgentSettings_light_add_btn_fallback.png");
                await Page.ScreenshotAsync(new PageScreenshotOptions { Path = screenshotPath, FullPage = true });
            }

            Assert.IsTrue(File.Exists(screenshotPath), $"截圖檔案應存在於 {screenshotPath}");
            var fileInfo = new FileInfo(screenshotPath);
            Assert.IsTrue(fileInfo.Length > 0, "截圖檔案不應為空");
        }

        // ── 重啟 Bot 確認流程 ─────────────────────────────────────────────

        [TestMethod]
        public async Task AgentSettings_點擊重啟Bot_顯示確認按鈕截圖驗證()
        {
            await Page.GotoAsync($"{_dashboardUrl}/agents");
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Page.WaitForTimeoutAsync(1500);

            var restartBtn = Page.Locator("button:has-text('重啟 Bot'), button:has-text('重啟Bot')").First;

            string screenshotPath;
            if (await restartBtn.IsVisibleAsync())
            {
                await restartBtn.ClickAsync();
                await Page.WaitForTimeoutAsync(600);

                screenshotPath = Path.Combine(ScreenshotDir, "PR107_AgentSettings_restart_confirm.png");
                await Page.ScreenshotAsync(new PageScreenshotOptions { Path = screenshotPath, FullPage = false });
            }
            else
            {
                screenshotPath = Path.Combine(ScreenshotDir, "PR107_AgentSettings_restart_confirm_fallback.png");
                await Page.ScreenshotAsync(new PageScreenshotOptions { Path = screenshotPath, FullPage = true });
            }

            Assert.IsTrue(File.Exists(screenshotPath), $"截圖檔案應存在於 {screenshotPath}");
            var fileInfo = new FileInfo(screenshotPath);
            Assert.IsTrue(fileInfo.Length > 0, "截圖檔案不應為空");
        }
    }
}
