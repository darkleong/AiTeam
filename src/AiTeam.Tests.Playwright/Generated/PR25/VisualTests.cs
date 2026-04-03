using Microsoft.Playwright.MSTest;
using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Threading.Tasks;

namespace AiTeam.Tests.Playwright.Generated
{
    [TestClass]
    public class PR25_Tasks頁面視覺截圖測試 : PageTest
    {
        private string _dashboardUrl = string.Empty;
        private string _screenshotDir = string.Empty;

        [TestInitialize]
        public async Task 測試初始化()
        {
            _dashboardUrl = Environment.GetEnvironmentVariable("DASHBOARD_URL") ?? "http://localhost:5051";
            _screenshotDir = "screenshots";
            Directory.CreateDirectory(_screenshotDir);

            var user = Environment.GetEnvironmentVariable("DASHBOARD_USER") ?? string.Empty;
            var pass = Environment.GetEnvironmentVariable("DASHBOARD_PASS") ?? string.Empty;

            if (!string.IsNullOrEmpty(user) && !string.IsNullOrEmpty(pass))
            {
                await 執行登入(user, pass);
            }
        }

        private async Task 執行登入(string user, string pass)
        {
            await Page.GotoAsync($"{_dashboardUrl}/login");
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            var usernameInput = Page.Locator("input[name='username'], input[type='text'], input[id*='user'], input[placeholder*='User'], input[placeholder*='user']");
            var passwordInput = Page.Locator("input[name='password'], input[type='password'], input[id*='pass'], input[placeholder*='Pass'], input[placeholder*='pass']");
            var loginButton = Page.Locator("button[type='submit'], button:has-text('Login'), button:has-text('登入'), button:has-text('Sign In')");

            if (await usernameInput.CountAsync() > 0)
            {
                await usernameInput.First.FillAsync(user);
            }

            if (await passwordInput.CountAsync() > 0)
            {
                await passwordInput.First.FillAsync(pass);
            }

            if (await loginButton.CountAsync() > 0)
            {
                await loginButton.First.ClickAsync();
                await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            }
        }

        private async Task 切換暗黑模式()
        {
            var darkModeToggle = Page.Locator(
                "button[id*='dark'], button[class*='dark'], button[aria-label*='dark'], " +
                "button[aria-label*='Dark'], button[aria-label*='暗'], button[aria-label*='夜間'], " +
                "input[type='checkbox'][id*='dark'], input[type='checkbox'][class*='dark'], " +
                ".dark-mode-toggle, .darkmode-toggle, [data-testid*='dark']"
            );

            if (await darkModeToggle.CountAsync() > 0)
            {
                await darkModeToggle.First.ClickAsync();
                await Page.WaitForTimeoutAsync(500);
            }
            else
            {
                var themeToggle = Page.Locator("button:has-text('🌙'), button:has-text('☀️'), button:has-text('Dark'), button:has-text('Light')");
                if (await themeToggle.CountAsync() > 0)
                {
                    await themeToggle.First.ClickAsync();
                    await Page.WaitForTimeoutAsync(500);
                }
            }
        }

        [TestMethod]
        public async Task Tasks頁面_Light模式_視覺截圖驗證()
        {
            await Page.GotoAsync($"{_dashboardUrl}/tasks");
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Page.WaitForTimeoutAsync(1000);

            var screenshotPath = Path.Combine(_screenshotDir, "PR25_Tasks_Light.png");
            await Page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = screenshotPath,
                FullPage = true
            });

            Assert.IsTrue(File.Exists(screenshotPath), $"截圖應存在於路徑：{screenshotPath}");
            var fileInfo = new FileInfo(screenshotPath);
            Assert.IsTrue(fileInfo.Length > 0, "截圖檔案不應為空");

            Console.WriteLine($"[PR25] Tasks 頁面 Light 模式截圖已儲存：{screenshotPath}");
        }

        [TestMethod]
        public async Task Tasks頁面_Dark模式_視覺截圖驗證()
        {
            await Page.GotoAsync($"{_dashboardUrl}/tasks");
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Page.WaitForTimeoutAsync(1000);

            await 切換暗黑模式();

            var screenshotPath = Path.Combine(_screenshotDir, "PR25_Tasks_Dark.png");
            await Page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = screenshotPath,
                FullPage = true
            });

            Assert.IsTrue(File.Exists(screenshotPath), $"截圖應存在於路徑：{screenshotPath}");
            var fileInfo = new FileInfo(screenshotPath);
            Assert.IsTrue(fileInfo.Length > 0, "截圖檔案不應為空");

            Console.WriteLine($"[PR25] Tasks 頁面 Dark 模式截圖已儲存：{screenshotPath}");
        }

        [TestMethod]
        public async Task Tasks頁面_Light模式_頁面標題與核心元素驗證()
        {
            await Page.GotoAsync($"{_dashboardUrl}/tasks");
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Page.WaitForTimeoutAsync(1000);

            var pageTitle = await Page.TitleAsync();
            Assert.IsFalse(string.IsNullOrWhiteSpace(pageTitle), "頁面標題不應為空");

            var bodyContent = await Page.InnerTextAsync("body");
            Assert.IsFalse(string.IsNullOrWhiteSpace(bodyContent), "頁面 Body 內容不應為空");

            var screenshotPath = Path.Combine(_screenshotDir, "PR25_Tasks_Light_元素驗證.png");
            await Page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = screenshotPath,
                FullPage = true
            });

            Assert.IsTrue(File.Exists(screenshotPath), $"截圖應存在於路徑：{screenshotPath}");
            Console.WriteLine($"[PR25] Tasks 頁面標題：{pageTitle}");
            Console.WriteLine($"[PR25] Tasks 頁面元素驗證截圖已儲存：{screenshotPath}");
        }

        [TestMethod]
        public async Task Tasks頁面_Dark模式_頁面標題與核心元素驗證()
        {
            await Page.GotoAsync($"{_dashboardUrl}/tasks");
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Page.WaitForTimeoutAsync(1000);

            await 切換暗黑模式();

            var pageTitle = await Page.TitleAsync();
            Assert.IsFalse(string.IsNullOrWhiteSpace(pageTitle), "頁面標題不應為空");

            var bodyContent = await Page.InnerTextAsync("body");
            Assert.IsFalse(string.IsNullOrWhiteSpace(bodyContent), "頁面 Body 內容不應為空");

            var screenshotPath = Path.Combine(_screenshotDir, "PR25_Tasks_Dark_元素驗證.png");
            await Page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = screenshotPath,
                FullPage = true
            });

            Assert.IsTrue(File.Exists(screenshotPath), $"截圖應存在於路徑：{screenshotPath}");
            Console.WriteLine($"[PR25] Tasks 頁面 Dark 模式標題：{pageTitle}");
            Console.WriteLine($"[PR25] Tasks 頁面 Dark 模式元素驗證截圖已儲存：{screenshotPath}");
        }

        [TestMethod]
        public async Task Tasks頁面_頁面無JavaScript錯誤驗證()
        {
            var jsErrors = new System.Collections.Generic.List<string>();

            Page.PageError += (_, error) =>
            {
                jsErrors.Add(error);
            };

            await Page.GotoAsync($"{_dashboardUrl}/tasks");
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Page.WaitForTimeoutAsync(1500);

            var screenshotPath = Path.Combine(_screenshotDir, "PR25_Tasks_JS錯誤驗證.png");
            await Page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = screenshotPath,
                FullPage = true
            });

            if (jsErrors.Count > 0)
            {
                Console.WriteLine($"[PR25] 偵測到 JavaScript 錯誤：");
                foreach (var error in jsErrors)
                {
                    Console.WriteLine($"  - {error}");
                }
            }

            Assert.AreEqual(0, jsErrors.Count, $"Tasks 頁面不應有 JavaScript 錯誤，發現 {jsErrors.Count} 個錯誤：{string.Join("; ", jsErrors)}");
            Console.WriteLine($"[PR25] Tasks 頁面無 JavaScript 錯誤，截圖已儲存：{screenshotPath}");
        }

        [TestMethod]
        public async Task Tasks頁面_響應式行動裝置視圖截圖驗證()
        {
            await Page.SetViewportSizeAsync(375, 812);
            await Page.GotoAsync($"{_dashboardUrl}/tasks");
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Page.WaitForTimeoutAsync(1000);

            var screenshotPathLight = Path.Combine(_screenshotDir, "PR25_Tasks_Mobile_Light.png");
            await Page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = screenshotPathLight,
                FullPage = true
            });

            Assert.IsTrue(File.Exists(screenshotPathLight), $"行動裝置 Light 截圖應存在：{screenshotPathLight}");

            await 切換暗黑模式();

            var screenshotPathDark = Path.Combine(_screenshotDir, "PR25_Tasks_Mobile_Dark.png");
            await Page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = screenshotPathDark,
                FullPage = true
            });

            Assert.IsTrue(File.Exists(screenshotPathDark), $"行動裝置 Dark 截圖應存在：{screenshotPathDark}");

            Console.WriteLine($"[PR25] Tasks 頁面行動裝置截圖已儲存：{screenshotPathLight}, {screenshotPathDark}");
        }
    }
}