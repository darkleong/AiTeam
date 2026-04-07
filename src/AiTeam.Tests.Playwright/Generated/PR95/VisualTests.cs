```csharp
using Microsoft.Playwright;
using Microsoft.Playwright.MSTest;
using System.IO;
using System.Threading.Tasks;

namespace AiTeam.Tests.Playwright.Generated
{
    [TestClass]
    public class PR95_MainLayout_AppCss_視覺截圖測試 : PageTest
    {
        private string _dashboardUrl = string.Empty;
        private string _dashboardUser = string.Empty;
        private string _dashboardPass = string.Empty;
        private const string ScreenshotDir = "screenshots";

        [TestInitialize]
        public async Task 初始化測試環境()
        {
            _dashboardUrl = System.Environment.GetEnvironmentVariable("DASHBOARD_URL") ?? "http://localhost:5051";
            _dashboardUser = System.Environment.GetEnvironmentVariable("DASHBOARD_USER") ?? string.Empty;
            _dashboardPass = System.Environment.GetEnvironmentVariable("DASHBOARD_PASS") ?? string.Empty;

            Directory.CreateDirectory(ScreenshotDir);

            await 執行登入();
        }

        private async Task 執行登入()
        {
            await Page.GotoAsync($"{_dashboardUrl}/login");
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            var 使用者輸入框 = Page.Locator("input[type='text'], input[name='username'], input[id='username'], input[placeholder*='user' i], input[placeholder*='帳號']");
            var 密碼輸入框 = Page.Locator("input[type='password']");

            if (await 使用者輸入框.CountAsync() > 0 && !string.IsNullOrEmpty(_dashboardUser))
            {
                await 使用者輸入框.First.FillAsync(_dashboardUser);
                await 密碼輸入框.First.FillAsync(_dashboardPass);
                await Page.Keyboard.PressAsync("Enter");
                await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            }
        }

        private async Task 切換至深色模式()
        {
            var darkModeToggle = Page.Locator(
                "button[class*='dark' i], " +
                "button[aria-label*='dark' i], " +
                "button[aria-label*='深色' i], " +
                "button[title*='dark' i], " +
                "button[title*='Dark' i], " +
                "input[type='checkbox'][class*='dark' i], " +
                ".dark-mode-toggle, " +
                "#darkModeToggle, " +
                "[data-testid='dark-mode-toggle']"
            );

            if (await darkModeToggle.CountAsync() > 0)
            {
                await darkModeToggle.First.ClickAsync();
                await Page.WaitForTimeoutAsync(600);
            }
            else
            {
                await Page.EvaluateAsync("document.documentElement.classList.add('dark')");
                await Page.WaitForTimeoutAsync(400);
            }
        }

        private async Task 恢復至淺色模式()
        {
            var darkModeToggle = Page.Locator(
                "button[class*='dark' i], " +
                "button[aria-label*='dark' i], " +
                "button[aria-label*='深色' i], " +
                "button[title*='dark' i], " +
                "button[title*='Dark' i], " +
                "input[type='checkbox'][class*='dark' i], " +
                ".dark-mode-toggle, " +
                "#darkModeToggle, " +
                "[data-testid='dark-mode-toggle']"
            );

            if (await darkModeToggle.CountAsync() > 0)
            {
                await darkModeToggle.First.ClickAsync();
                await Page.WaitForTimeoutAsync(600);
            }
            else
            {
                await Page.EvaluateAsync("document.documentElement.classList.remove('dark')");
                await Page.WaitForTimeoutAsync(400);
            }
        }

        // ──────────────────────────────────────────────
        // 首頁 / Dashboard 主版面佈局截圖
        // ──────────────────────────────────────────────

        [TestMethod]
        public async Task 首頁主版面佈局_淺色模式截圖()
        {
            await Page.GotoAsync($"{_dashboardUrl}/");
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Page.WaitForTimeoutAsync(500);

            await Page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = Path.Combine(ScreenshotDir, "PR95_首頁主版面佈局_淺色模式.png"),
                FullPage = true
            });
        }

        [TestMethod]
        public async Task 首頁主版面佈局_深色模式截圖()
        {
            await Page.GotoAsync($"{_dashboardUrl}/");
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Page.WaitForTimeoutAsync(500);

            await 切換至深色模式();

            await Page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = Path.Combine(ScreenshotDir, "PR95_首頁主版面佈局_深色模式.png"),
                FullPage = true
            });

            await 恢復至淺色模式();
        }

        // ──────────────────────────────────────────────
        // 導覽列（NavBar / Sidebar）與全域版面
        // ──────────────────────────────────────────────

        [TestMethod]
        public async Task 導覽列全域版面_淺色模式截圖()
        {
            await Page.GotoAsync($"{_dashboardUrl}/");
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Page.WaitForTimeoutAsync(500);

            var navBar = Page.Locator("nav, .navbar, .sidebar, [class*='nav' i], [class*='layout' i]").First;
            if (await navBar.CountAsync() > 0)
            {
                await navBar.ScreenshotAsync(new LocatorScreenshotOptions
                {
                    Path = Path.Combine(ScreenshotDir, "PR95_導覽列全域版面_淺色模式.png")
                });
            }
            else
            {
                await Page.ScreenshotAsync(new PageScreenshotOptions
                {
                    Path = Path.Combine(ScreenshotDir, "PR95_導覽列全域版面_淺色模式.png"),
                    FullPage = true
                });
            }
        }

        [TestMethod]
        public async Task 導覽列全域版面_深色模式截圖()
        {
            await Page.GotoAsync($"{_dashboardUrl}/");
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Page.WaitForTimeoutAsync(500);

            await 切換至深色模式();

            var navBar = Page.Locator("nav, .navbar, .sidebar, [class*='nav' i], [class*='layout' i]").First;
            if (await navBar.CountAsync() > 0)
            {
                await navBar.ScreenshotAsync(new LocatorScreenshotOptions
                {
                    Path = Path.Combine(ScreenshotDir, "PR95_導覽列全域版面_深色模式.png")
                });
            }
            else
            {
                await Page.ScreenshotAsync(new PageScreenshotOptions
                {
                    Path = Path.Combine(ScreenshotDir, "PR95_導覽列全域版面_深色模式.png"),
                    FullPage = true
                });
            }

            await 恢復至淺色模式();
        }

        // ──────────────────────────────────────────────
        // 頁首（Header）區塊
        // ──────────────────────────────────────────────

        [TestMethod]
        public async Task 頁首區塊_淺色模式截圖()
        {
            await Page.GotoAsync($"{_dashboardUrl}/");
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Page.WaitForTimeoutAsync(500);

            var header = Page.Locator("header, .header, [class*='header' i], [class*='topbar' i]").First;
            if (await header.CountAsync() > 0)
            {
                await header.ScreenshotAsync(new LocatorScreenshotOptions
                {
                    Path = Path.Combine(ScreenshotDir, "PR95_頁首區塊_淺色模式.png")
                });
            }
            else
            {
                await Page.ScreenshotAsync(new PageScreenshotOptions
                {
                    Path = Path.Combine(ScreenshotDir, "PR95_頁首區塊_淺色模式.png"),
                    Clip = new Clip { X = 0, Y = 0, Width = 1280, Height = 80 }
                });
            }
        }

        [TestMethod]
        public async Task 頁首區塊_深色模式截圖()
        {
            await Page.GotoAsync($"{_dashboardUrl}/");
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Page.WaitForTimeoutAsync(500);

            await 切換至深色模式();

            var header = Page.Locator("header, .header, [class*='header' i], [class*='topbar' i]").First;
            if (await header.CountAsync() > 0)
            {
                await header.ScreenshotAsync(new LocatorScreenshotOptions
                {
                    Path = Path.Combine(ScreenshotDir, "PR95_頁首區塊_深色模式.png")
                });
            }
            else
            {
                await Page.ScreenshotAsync(new PageScreenshotOptions
                {
                    Path = Path.Combine(ScreenshotDir, "PR95_頁首區塊_深色模式.png"),
                    Clip = new Clip { X = 0, Y = 0, Width = 1280, Height = 80 }
                });
            }

            await 恢復至淺色模式();
        }

        // ──────────────────────────────────────────────
        // 主內容區塊
        // ──────────────────────────────────────────────

        [TestMethod]
        public async Task 主內容區塊_淺色模式截圖()
        {
            await Page.GotoAsync($"{_dashboardUrl}/");
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Page.WaitForTimeoutAsync(500);

            var main = Page.Locator("main, .main, [class*='main-content' i], [class*='page-content' i], article").First;
            if (await main.CountAsync() > 0)
            {
                await main.ScreenshotAsync(new LocatorScreenshotOptions
                {
                    Path = Path.Combine(ScreenshotDir, "PR95_主內容區塊_淺色模式.png")
                });
            }
            else
            {
                await Page.ScreenshotAsync(new PageScreenshotOptions
                {
                    Path = Path.Combine(ScreenshotDir, "PR95_主內容區塊_淺色模式.png"),
                    FullPage = true
                });
            }
        }

        [TestMethod]
        public async Task 主內容區塊_深色模式截圖()
        {
            await Page.GotoAsync($"{_dashboardUrl}/");
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Page.WaitForTimeoutAsync(500);

            await 切換至深色模式();

            var main = Page.Locator("main, .main, [class*='main-content' i], [class*='page-content' i], article").First;
            if (await main.CountAsync() > 0)
            {
                await main.ScreenshotAsync(new LocatorScreenshotOptions
                {
                    Path = Path.Combine(ScreenshotDir, "PR95_主內容區塊_深色模式.png")
                });
            }
            else
            {
                await Page.ScreenshotAsync(new PageScreenshotOptions
                {
                    Path = Path.Combine(ScreenshotDir, "PR95_主內容區塊_深色模式.png"),
                    FullPage = true
                });
            }

            await 恢復至淺色模式();
        }

        // ──────────────────────────────────────────────
        // 全域 CSS 樣式：響應式視窗截圖（手機尺寸）
        // ──────────────────────────────────────────────

        [TestMethod]
        public async Task 行動裝置響應式版面_淺色模式截圖()
        {
            await Page.SetViewportSizeAsync(375, 812);
            await Page.GotoAsync($"{_dashboardUrl}/");
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Page.WaitForTimeoutAsync(500);

            await Page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = Path.Combine(ScreenshotDir, "PR95_行動裝置響應式版面_淺色模式.png"),
                FullPage = true
            });

            await Page.SetViewportSizeAsync(1280, 720);
        }

        [TestMethod]
        public async Task 行動裝置響應式版面_深色模式截圖()
        {
            await Page.SetViewportSizeAsync(375, 812);
            await Page.GotoAsync($"{_dashboardUrl}/");
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Page.WaitForTimeoutAsync(500);

            await 切換至深色模式();

            await Page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = Path.Combine(ScreenshotDir, "PR95_行動裝置響應式版面_深色模式.png"),
                FullPage = true
            });

            await 恢復至淺色模式();
            await Page.SetViewportSizeAsync(1280, 720);
        }

        // ──────────────────────────────────────────────
        // 全域 CSS 樣式：平板尺寸截圖
        // ──────────────────────────────────────────────

        [TestMethod]
        public async Task 平板響應式版面_淺色模式截圖()
        {
            await Page.SetViewportSizeAsync(768, 1024);
            await Page.GotoAsync($"{_dashboardUrl}/");
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await Page.WaitForTimeoutAsync(500);

            await Page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = Path.Combine(ScreenshotDir, "PR95_平板響應式版面_淺色模式.png"),
                FullPage = true
            });

            await Page.SetViewportSizeAsync(1280, 720);
        }

        [TestMethod]
        public async Task 平板響應式版面_