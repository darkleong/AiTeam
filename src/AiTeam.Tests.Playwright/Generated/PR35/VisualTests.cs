using Microsoft.Playwright.MSTest;
using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Threading.Tasks;

namespace AiTeam.Tests.Playwright.Generated
{
    [TestClass]
    public class PR35_ReviewerAgent頁面視覺截圖測試 : PageTest
    {
        private string _dashboardUrl = string.Empty;
        private string _screenshotDir = string.Empty;

        [TestInitialize]
        public async Task 測試初始化()
        {
            _dashboardUrl = Environment.GetEnvironmentVariable("DASHBOARD_URL") ?? "http://localhost:5051";
            _screenshotDir = Path.Combine("screenshots", "PR35_ReviewerAgent");
            Directory.CreateDirectory(_screenshotDir);

            var user = Environment.GetEnvironmentVariable("DASHBOARD_USER") ?? string.Empty;
            var pass = Environment.GetEnvironmentVariable("DASHBOARD_PASS") ?? string.Empty;

            if (!string.IsNullOrEmpty(user) && !string.IsNullOrEmpty(pass))
            {
                await Page.GotoAsync($"{_dashboardUrl}/login");
                await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

                var userInput = Page.Locator("input[type='text'], input[name='username'], input[id='username'], input[placeholder*='user' i], input[placeholder*='帳號']").First;
                var passInput = Page.Locator("input[type='password']").First;

                await userInput.FillAsync(user);
                await passInput.FillAsync(pass);

                await Page.Locator("button[type='submit'], button:has-text('登入'), button:has-text('Login')").First.ClickAsync();
                await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            }
        }

        [TestMethod]
        public async Task ReviewerAgent頁面_Light模式_截圖驗證()
        {
            var targetUrl = $"{_dashboardUrl}/reviewer-agent";
            await Page.GotoAsync(targetUrl);
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Page.WaitForTimeoutAsync(1500);

            var screenshotPath = Path.Combine(_screenshotDir, "ReviewerAgent_light.png");
            await Page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = screenshotPath,
                FullPage = true
            });

            Assert.IsTrue(File.Exists(screenshotPath), $"Light 模式截圖應存在：{screenshotPath}");
            var fileInfo = new FileInfo(screenshotPath);
            Assert.IsTrue(fileInfo.Length > 0, "Light 模式截圖檔案不應為空");
        }

        [TestMethod]
        public async Task ReviewerAgent頁面_Dark模式_截圖驗證()
        {
            var targetUrl = $"{_dashboardUrl}/reviewer-agent";
            await Page.GotoAsync(targetUrl);
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Page.WaitForTimeoutAsync(1500);

            var darkModeToggle = Page.Locator(
                "button[aria-label*='dark' i], button[aria-label*='Dark' i], " +
                "button[aria-label*='theme' i], button[aria-label*='Theme' i], " +
                "input[type='checkbox'][aria-label*='dark' i], " +
                ".dark-mode-toggle, #darkModeToggle, [data-testid='dark-mode-toggle'], " +
                "button:has-text('Dark'), button:has-text('dark'), " +
                "button:has-text('夜間'), button:has-text('深色')"
            ).First;

            var toggleVisible = await darkModeToggle.IsVisibleAsync();
            if (toggleVisible)
            {
                await darkModeToggle.ClickAsync();
                await Page.WaitForTimeoutAsync(800);
            }
            else
            {
                await Page.EvaluateAsync(@"
                    document.documentElement.classList.add('dark');
                    document.body.classList.add('dark');
                    document.body.setAttribute('data-theme', 'dark');
                ");
                await Page.WaitForTimeoutAsync(800);
            }

            var screenshotPath = Path.Combine(_screenshotDir, "ReviewerAgent_dark.png");
            await Page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = screenshotPath,
                FullPage = true
            });

            Assert.IsTrue(File.Exists(screenshotPath), $"Dark 模式截圖應存在：{screenshotPath}");
            var fileInfo = new FileInfo(screenshotPath);
            Assert.IsTrue(fileInfo.Length > 0, "Dark 模式截圖檔案不應為空");
        }

        [TestMethod]
        public async Task ReviewerAgent頁面_驗證頁面標題與主要內容存在()
        {
            var targetUrl = $"{_dashboardUrl}/reviewer-agent";
            await Page.GotoAsync(targetUrl);
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Page.WaitForTimeoutAsync(1500);

            var pageTitle = await Page.TitleAsync();
            Assert.IsFalse(string.IsNullOrEmpty(pageTitle), "頁面標題不應為空");

            var bodyContent = await Page.Locator("body").InnerTextAsync();
            Assert.IsFalse(string.IsNullOrEmpty(bodyContent), "頁面主體內容不應為空");

            var screenshotPath = Path.Combine(_screenshotDir, "ReviewerAgent_內容驗證.png");
            await Page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = screenshotPath,
                FullPage = true
            });

            Assert.IsTrue(File.Exists(screenshotPath), $"內容驗證截圖應存在：{screenshotPath}");
        }

        [TestMethod]
        public async Task ReviewerAgent頁面_驗證無主控台錯誤_Light模式()
        {
            var consoleErrors = new System.Collections.Generic.List<string>();
            Page.Console += (_, msg) =>
            {
                if (msg.Type == "error")
                {
                    consoleErrors.Add(msg.Text);
                }
            };

            var targetUrl = $"{_dashboardUrl}/reviewer-agent";
            await Page.GotoAsync(targetUrl);
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Page.WaitForTimeoutAsync(2000);

            var screenshotPath = Path.Combine(_screenshotDir, "ReviewerAgent_無錯誤驗證_light.png");
            await Page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = screenshotPath,
                FullPage = true
            });

            Assert.IsTrue(File.Exists(screenshotPath), $"無錯誤驗證截圖應存在：{screenshotPath}");

            if (consoleErrors.Count > 0)
            {
                Console.WriteLine($"偵測到主控台錯誤（共 {consoleErrors.Count} 筆）：");
                foreach (var error in consoleErrors)
                {
                    Console.WriteLine($"  - {error}");
                }
            }

            Assert.AreEqual(0, consoleErrors.Count, $"頁面不應有主控台錯誤，實際錯誤：{string.Join("; ", consoleErrors)}");
        }

        [TestMethod]
        public async Task ReviewerAgent頁面_驗證無主控台錯誤_Dark模式()
        {
            var consoleErrors = new System.Collections.Generic.List<string>();
            Page.Console += (_, msg) =>
            {
                if (msg.Type == "error")
                {
                    consoleErrors.Add(msg.Text);
                }
            };

            var targetUrl = $"{_dashboardUrl}/reviewer-agent";
            await Page.GotoAsync(targetUrl);
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Page.WaitForTimeoutAsync(1500);

            var darkModeToggle = Page.Locator(
                "button[aria-label*='dark' i], button[aria-label*='Dark' i], " +
                "button[aria-label*='theme' i], button[aria-label*='Theme' i], " +
                "input[type='checkbox'][aria-label*='dark' i], " +
                ".dark-mode-toggle, #darkModeToggle, [data-testid='dark-mode-toggle'], " +
                "button:has-text('Dark'), button:has-text('dark'), " +
                "button:has-text('夜間'), button:has-text('深色')"
            ).First;

            var toggleVisible = await darkModeToggle.IsVisibleAsync();
            if (toggleVisible)
            {
                await darkModeToggle.ClickAsync();
                await Page.WaitForTimeoutAsync(800);
            }
            else
            {
                await Page.EvaluateAsync(@"
                    document.documentElement.classList.add('dark');
                    document.body.classList.add('dark');
                    document.body.setAttribute('data-theme', 'dark');
                ");
                await Page.WaitForTimeoutAsync(800);
            }

            var screenshotPath = Path.Combine(_screenshotDir, "ReviewerAgent_無錯誤驗證_dark.png");
            await Page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = screenshotPath,
                FullPage = true
            });

            Assert.IsTrue(File.Exists(screenshotPath), $"Dark 模式無錯誤驗證截圖應存在：{screenshotPath}");

            if (consoleErrors.Count > 0)
            {
                Console.WriteLine($"偵測到主控台錯誤（共 {consoleErrors.Count} 筆）：");
                foreach (var error in consoleErrors)
                {
                    Console.WriteLine($"  - {error}");
                }
            }

            Assert.AreEqual(0, consoleErrors.Count, $"Dark 模式頁面不應有主控台錯誤，實際錯誤：{string.Join("; ", consoleErrors)}");
        }

        [TestMethod]
        public async Task ReviewerAgent頁面_行動裝置版面_截圖驗證()
        {
            await Page.SetViewportSizeAsync(390, 844);

            var targetUrl = $"{_dashboardUrl}/reviewer-agent";
            await Page.GotoAsync(targetUrl);
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Page.WaitForTimeoutAsync(1500);

            var screenshotPath = Path.Combine(_screenshotDir, "ReviewerAgent_mobile_light.png");
            await Page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = screenshotPath,
                FullPage = true
            });

            Assert.IsTrue(File.Exists(screenshotPath), $"行動裝置 Light 模式截圖應存在：{screenshotPath}");
            var fileInfo = new FileInfo(screenshotPath);
            Assert.IsTrue(fileInfo.Length > 0, "行動裝置截圖檔案不應為空");

            var darkModeToggle = Page.Locator(
                "button[aria-label*='dark' i], button[aria-label*='Dark' i], " +
                "button[aria-label*='theme' i], button[aria-label*='Theme' i], " +
                ".dark-mode-toggle, #darkModeToggle, [data-testid='dark-mode-toggle'], " +
                "button:has-text('Dark'), button:has-text('夜間'), button:has-text('深色')"
            ).First;

            var toggleVisible = await darkModeToggle.IsVisibleAsync();
            if (toggleVisible)
            {
                await darkModeToggle.ClickAsync();
                await Page.WaitForTimeoutAsync(800);
            }
            else
            {
                await Page.EvaluateAsync(@"
                    document.documentElement.classList.add('dark');
                    document.body.classList.add('dark');
                    document.body.setAttribute('data-theme', 'dark');
                ");
                await Page.WaitForTimeoutAsync(800);
            }

            var darkScreenshotPath = Path.Combine(_screenshotDir, "ReviewerAgent_mobile_dark.png");
            await Page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = darkScreenshotPath,
                FullPage = true
            });

            Assert.IsTrue(File.Exists(darkScreenshotPath), $"行動裝置 Dark 模式截圖應存在：{darkScreenshotPath}");
            var darkFileInfo = new FileInfo(darkScreenshotPath);
            Assert.IsTrue(darkFileInfo.Length > 0, "行動裝置 Dark 模式截圖檔案不應為空");
        }
    }
}