using Microsoft.Playwright.MSTest;
using Microsoft.Playwright;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Threading.Tasks;

namespace AiTeam.Tests.Playwright.Generated
{
    [TestClass]
    public class ReviewerReport_PR57_視覺截圖測試 : PageTest
    {
        private string _dashboardUrl = null!;
        private string _dashboardUser = null!;
        private string _dashboardPass = null!;
        private string _screenshotDir = null!;

        [TestInitialize]
        public async Task 測試初始化()
        {
            _dashboardUrl = Environment.GetEnvironmentVariable("DASHBOARD_URL") ?? "http://localhost:5051";
            _dashboardUser = Environment.GetEnvironmentVariable("DASHBOARD_USER") ?? "admin";
            _dashboardPass = Environment.GetEnvironmentVariable("DASHBOARD_PASS") ?? "admin";

            _screenshotDir = Path.Combine("screenshots", "PR57", "ReviewerReport");
            Directory.CreateDirectory(_screenshotDir);

            await 執行登入();
        }

        private async Task 執行登入()
        {
            await Page.GotoAsync($"{_dashboardUrl}/login");
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            var 使用者名稱欄位 = Page.Locator("input[name='username'], input[type='text'], input[id*='user'], input[placeholder*='user'], input[placeholder*='帳號']");
            var 密碼欄位 = Page.Locator("input[name='password'], input[type='password']");

            if (await 使用者名稱欄位.CountAsync() > 0)
            {
                await 使用者名稱欄位.First.FillAsync(_dashboardUser);
            }

            if (await 密碼欄位.CountAsync() > 0)
            {
                await 密碼欄位.First.FillAsync(_dashboardPass);
            }

            var 登入按鈕 = Page.Locator("button[type='submit'], button:has-text('登入'), button:has-text('Login'), button:has-text('Sign in')");
            if (await 登入按鈕.CountAsync() > 0)
            {
                await 登入按鈕.First.ClickAsync();
                await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            }
        }

        private async Task 切換至深色模式()
        {
            var 深色模式切換 = Page.Locator(
                "input[type='checkbox'][id*='dark'], " +
                "input[type='checkbox'][id*='Dark'], " +
                "button[id*='dark'], " +
                "button[id*='Dark'], " +
                "button[aria-label*='dark'], " +
                "button[aria-label*='Dark'], " +
                "button[aria-label*='暗色'], " +
                "button[aria-label*='深色'], " +
                ".dark-mode-toggle, " +
                ".darkmode-toggle, " +
                "[data-testid*='dark']"
            );

            if (await 深色模式切換.CountAsync() > 0)
            {
                await 深色模式切換.First.ClickAsync();
                await Page.WaitForTimeoutAsync(800);
            }
            else
            {
                await Page.EvaluateAsync("document.documentElement.setAttribute('data-theme', 'dark')");
                await Page.EvaluateAsync("document.documentElement.classList.add('dark')");
                await Page.WaitForTimeoutAsync(800);
            }
        }

        private async Task 切換回淺色模式()
        {
            var 深色模式切換 = Page.Locator(
                "input[type='checkbox'][id*='dark'], " +
                "input[type='checkbox'][id*='Dark'], " +
                "button[id*='dark'], " +
                "button[id*='Dark'], " +
                "button[aria-label*='dark'], " +
                "button[aria-label*='Dark'], " +
                "button[aria-label*='暗色'], " +
                "button[aria-label*='深色'], " +
                ".dark-mode-toggle, " +
                ".darkmode-toggle, " +
                "[data-testid*='dark']"
            );

            if (await 深色模式切換.CountAsync() > 0)
            {
                await 深色模式切換.First.ClickAsync();
                await Page.WaitForTimeoutAsync(800);
            }
            else
            {
                await Page.EvaluateAsync("document.documentElement.removeAttribute('data-theme')");
                await Page.EvaluateAsync("document.documentElement.classList.remove('dark')");
                await Page.WaitForTimeoutAsync(800);
            }
        }

        [TestMethod]
        public async Task 審查者報告頁面_淺色模式_截圖驗證()
        {
            await Page.GotoAsync($"{_dashboardUrl}/reviewer-report");
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Page.WaitForTimeoutAsync(1500);

            var 截圖路徑 = Path.Combine(_screenshotDir, "reviewer-report_light.png");
            await Page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = 截圖路徑,
                FullPage = true
            });

            Assert.IsTrue(File.Exists(截圖路徑), $"淺色模式截圖應成功儲存於：{截圖路徑}");
            var 檔案資訊 = new FileInfo(截圖路徑);
            Assert.IsTrue(檔案資訊.Length > 0, "截圖檔案不應為空");
        }

        [TestMethod]
        public async Task 審查者報告頁面_深色模式_截圖驗證()
        {
            await Page.GotoAsync($"{_dashboardUrl}/reviewer-report");
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Page.WaitForTimeoutAsync(1500);

            await 切換至深色模式();

            var 截圖路徑 = Path.Combine(_screenshotDir, "reviewer-report_dark.png");
            await Page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = 截圖路徑,
                FullPage = true
            });

            Assert.IsTrue(File.Exists(截圖路徑), $"深色模式截圖應成功儲存於：{截圖路徑}");
            var 檔案資訊 = new FileInfo(截圖路徑);
            Assert.IsTrue(檔案資訊.Length > 0, "截圖檔案不應為空");

            await 切換回淺色模式();
        }

        [TestMethod]
        public async Task 審查者報告頁面_頁面標題與主要元件_可見性驗證()
        {
            await Page.GotoAsync($"{_dashboardUrl}/reviewer-report");
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Page.WaitForTimeoutAsync(1500);

            var 頁面內容 = await Page.ContentAsync();
            Assert.IsFalse(string.IsNullOrWhiteSpace(頁面內容), "頁面內容不應為空");

            var 錯誤提示 = Page.Locator("text=404, text=找不到頁面, text=Not Found, text=Error");
            var 錯誤數量 = await 錯誤提示.CountAsync();
            Assert.AreEqual(0, 錯誤數量, "頁面不應顯示 404 或錯誤訊息");

            var 截圖路徑 = Path.Combine(_screenshotDir, "reviewer-report_元件驗證.png");
            await Page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = 截圖路徑,
                FullPage = true
            });

            Assert.IsTrue(File.Exists(截圖路徑), $"元件驗證截圖應成功儲存於：{截圖路徑}");
        }

        [TestMethod]
        public async Task 審查者報告頁面_行動裝置視窗_淺色模式_截圖驗證()
        {
            await Page.SetViewportSizeAsync(375, 812);

            await Page.GotoAsync($"{_dashboardUrl}/reviewer-report");
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Page.WaitForTimeoutAsync(1500);

            var 截圖路徑 = Path.Combine(_screenshotDir, "reviewer-report_mobile_light.png");
            await Page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = 截圖路徑,
                FullPage = true
            });

            Assert.IsTrue(File.Exists(截圖路徑), $"行動裝置淺色模式截圖應成功儲存於：{截圖路徑}");
            var 檔案資訊 = new FileInfo(截圖路徑);
            Assert.IsTrue(檔案資訊.Length > 0, "截圖檔案不應為空");
        }

        [TestMethod]
        public async Task 審查者報告頁面_行動裝置視窗_深色模式_截圖驗證()
        {
            await Page.SetViewportSizeAsync(375, 812);

            await Page.GotoAsync($"{_dashboardUrl}/reviewer-report");
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Page.WaitForTimeoutAsync(1500);

            await 切換至深色模式();

            var 截圖路徑 = Path.Combine(_screenshotDir, "reviewer-report_mobile_dark.png");
            await Page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = 截圖路徑,
                FullPage = true
            });

            Assert.IsTrue(File.Exists(截圖路徑), $"行動裝置深色模式截圖應成功儲存於：{截圖路徑}");
            var 檔案資訊 = new FileInfo(截圖路徑);
            Assert.IsTrue(檔案資訊.Length > 0, "截圖檔案不應為空");

            await 切換回淺色模式();
        }

        [TestMethod]
        public async Task 審查者報告頁面_寬螢幕視窗_淺色模式_截圖驗證()
        {
            await Page.SetViewportSizeAsync(1920, 1080);

            await Page.GotoAsync($"{_dashboardUrl}/reviewer-report");
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Page.WaitForTimeoutAsync(1500);

            var 截圖路徑 = Path.Combine(_screenshotDir, "reviewer-report_widescreen_light.png");
            await Page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = 截圖路徑,
                FullPage = true
            });

            Assert.IsTrue(File.Exists(截圖路徑), $"寬螢幕淺色模式截圖應成功儲存於：{截圖路徑}");
            var 檔案資訊 = new FileInfo(截圖路徑);
            Assert.IsTrue(檔案資訊.Length > 0, "截圖檔案不應為空");
        }

        [TestMethod]
        public async Task 審查者報告頁面_寬螢幕視窗_深色模式_截圖驗證()
        {
            await Page.SetViewportSizeAsync(1920, 1080);

            await Page.GotoAsync($"{_dashboardUrl}/reviewer-report");
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Page.WaitForTimeoutAsync(1500);

            await 切換至深色模式();

            var 截圖路徑 = Path.Combine(_screenshotDir, "reviewer-report_widescreen_dark.png");
            await Page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = 截圖路徑,
                FullPage = true
            });

            Assert.IsTrue(File.Exists(截圖路徑), $"寬螢幕深色模式截圖應成功儲存於：{截圖路徑}");
            var 檔案資訊 = new FileInfo(截圖路徑);
            Assert.IsTrue(檔案資訊.Length > 0, "截圖檔案不應為空");

            await 切換回淺色模式();
        }

        [TestMethod]
        public async Task 審查者報告頁面_替代路徑_淺色模式_截圖驗證()
        {
            var 候選路徑清單 = new[]
            {
                "/reviewer-report",
                "/ReviewerReport",
                "/reviewer_report",
                "/reports/reviewer",
                "/report/reviewer"
            };

            string? 有效路徑 = null;

            foreach (var 路徑 in 候選路徑清單)
            {
                var 回應 = await Page.GotoAsync($"{_dashboardUrl}{路徑}");
                await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

                if (回應 != null && 回應.Status < 400)
                {
                    var 錯誤提示 = Page.Locator("text=404");
                    if (await 錯誤提示.CountAsync() == 0)
                    {
                        有效路徑 = 路徑;
                        break;
                    }
                }
            }

            Assert.IsNotNull(有效路徑, $"未能找到有效的審查者報告頁面路徑，已嘗試：{string.Join(", ", 候選路徑清單)}");

            await Page.WaitForTimeoutAsync(1500);

            var 截圖路徑 = Path.Combine(_screenshotDir, "reviewer-report_有效路徑_light.png");
            await Page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = 截圖路徑,
                FullPage = true
            });

            Assert.IsTrue(File.Exists(截圖路徑), $"有效路徑淺色模式截圖應成功儲存於：{截圖路徑}");
        }

        [TestMethod]
        public async Task 審查者報告頁面_替代路徑_深色模式_截圖驗證()
        {
            var 候選路徑清單 = new[]
            {
                "/reviewer-report",
                "/ReviewerReport",
                "/reviewer_report",
                "/reports/reviewer",
                "/report/reviewer"
            };

            string? 有效路徑 = null;

            foreach (var 路徑 in 候選路徑清單)
            {
                var 回