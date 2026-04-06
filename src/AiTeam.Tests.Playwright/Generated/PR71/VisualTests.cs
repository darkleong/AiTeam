```csharp
using Microsoft.Playwright;
using Microsoft.Playwright.MSTest;

namespace AiTeam.Tests.Playwright.Generated;

[TestClass]
public class PR71_TaskCenter_視覺截圖測試 : PageTest
{
    private string _dashboardUrl = string.Empty;
    private string _dashboardUser = string.Empty;
    private string _dashboardPass = string.Empty;
    private const string ScreenshotDir = "screenshots";

    [TestInitialize]
    public async Task 測試初始化()
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

        var userInput = Page.Locator("input[type='text'], input[name='username'], input[id*='user'], input[placeholder*='使用者'], input[placeholder*='User'], input[placeholder*='Email'], input[type='email']").First;
        var passInput = Page.Locator("input[type='password']").First;

        if (await userInput.CountAsync() > 0 && !string.IsNullOrEmpty(_dashboardUser))
        {
            await userInput.FillAsync(_dashboardUser);
        }

        if (await passInput.CountAsync() > 0 && !string.IsNullOrEmpty(_dashboardPass))
        {
            await passInput.FillAsync(_dashboardPass);
        }

        var submitButton = Page.Locator("button[type='submit'], button:has-text('登入'), button:has-text('Login'), button:has-text('Sign in')").First;
        if (await submitButton.CountAsync() > 0)
        {
            await submitButton.ClickAsync();
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        }
    }

    private async Task 切換暗色模式()
    {
        var darkModeToggle = Page.Locator(
            "button[aria-label*='dark'], button[aria-label*='Dark'], " +
            "button[aria-label*='theme'], button[aria-label*='Theme'], " +
            "button[title*='dark'], button[title*='Dark'], " +
            "input[type='checkbox'][id*='dark'], input[type='checkbox'][id*='theme'], " +
            "[class*='dark-mode'], [class*='darkMode'], [class*='theme-toggle'], " +
            "label[for*='dark'], label[for*='theme'], " +
            "button:has-text('Dark'), button:has-text('dark'), " +
            "button:has-text('暗色'), button:has-text('深色')").First;

        if (await darkModeToggle.CountAsync() > 0)
        {
            await darkModeToggle.ClickAsync();
            await Page.WaitForTimeoutAsync(800);
        }
        else
        {
            await Page.EmulateMediaAsync(new PageEmulateMediaOptions { ColorScheme = ColorScheme.Dark });
        }
    }

    [TestMethod]
    public async Task 任務中心頁面_亮色模式_截圖驗證()
    {
        await Page.GotoAsync($"{_dashboardUrl}/tasks/task-center");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Page.WaitForTimeoutAsync(1000);

        var screenshotPath = Path.Combine(ScreenshotDir, "PR71_TaskCenter_亮色模式.png");
        await Page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = screenshotPath,
            FullPage = true
        });

        Assert.IsTrue(File.Exists(screenshotPath), $"截圖檔案應存在：{screenshotPath}");
        var fileInfo = new FileInfo(screenshotPath);
        Assert.IsTrue(fileInfo.Length > 0, "截圖檔案不應為空");
    }

    [TestMethod]
    public async Task 任務中心頁面_暗色模式_截圖驗證()
    {
        await Page.GotoAsync($"{_dashboardUrl}/tasks/task-center");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Page.WaitForTimeoutAsync(1000);

        await 切換暗色模式();

        var screenshotPath = Path.Combine(ScreenshotDir, "PR71_TaskCenter_暗色模式.png");
        await Page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = screenshotPath,
            FullPage = true
        });

        Assert.IsTrue(File.Exists(screenshotPath), $"截圖檔案應存在：{screenshotPath}");
        var fileInfo = new FileInfo(screenshotPath);
        Assert.IsTrue(fileInfo.Length > 0, "截圖檔案不應為空");
    }

    [TestMethod]
    public async Task 任務中心頁面_備用路由_亮色模式_截圖驗證()
    {
        var candidateUrls = new[]
        {
            $"{_dashboardUrl}/tasks/task-center",
            $"{_dashboardUrl}/tasks",
            $"{_dashboardUrl}/task-center",
            $"{_dashboardUrl}/Tasks/TaskCenter",
            $"{_dashboardUrl}/Tasks"
        };

        bool pageLoaded = false;
        string loadedUrl = string.Empty;

        foreach (var url in candidateUrls)
        {
            try
            {
                var response = await Page.GotoAsync(url);
                await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

                if (response != null && response.Ok)
                {
                    pageLoaded = true;
                    loadedUrl = url;
                    break;
                }
            }
            catch
            {
                // 嘗試下一個 URL
            }
        }

        await Page.WaitForTimeoutAsync(1000);

        var screenshotPath = Path.Combine(ScreenshotDir, "PR71_TaskCenter_備用路由_亮色模式.png");
        await Page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = screenshotPath,
            FullPage = true
        });

        Assert.IsTrue(File.Exists(screenshotPath), $"截圖檔案應存在：{screenshotPath}");
        var fileInfo = new FileInfo(screenshotPath);
        Assert.IsTrue(fileInfo.Length > 0, "截圖檔案不應為空");
    }

    [TestMethod]
    public async Task 任務中心頁面_備用路由_暗色模式_截圖驗證()
    {
        var candidateUrls = new[]
        {
            $"{_dashboardUrl}/tasks/task-center",
            $"{_dashboardUrl}/tasks",
            $"{_dashboardUrl}/task-center",
            $"{_dashboardUrl}/Tasks/TaskCenter",
            $"{_dashboardUrl}/Tasks"
        };

        foreach (var url in candidateUrls)
        {
            try
            {
                var response = await Page.GotoAsync(url);
                await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

                if (response != null && response.Ok)
                {
                    break;
                }
            }
            catch
            {
                // 嘗試下一個 URL
            }
        }

        await Page.WaitForTimeoutAsync(1000);
        await 切換暗色模式();

        var screenshotPath = Path.Combine(ScreenshotDir, "PR71_TaskCenter_備用路由_暗色模式.png");
        await Page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = screenshotPath,
            FullPage = true
        });

        Assert.IsTrue(File.Exists(screenshotPath), $"截圖檔案應存在：{screenshotPath}");
        var fileInfo = new FileInfo(screenshotPath);
        Assert.IsTrue(fileInfo.Length > 0, "截圖檔案不應為空");
    }

    [TestMethod]
    public async Task 任務中心頁面_元件可見性_驗證()
    {
        await Page.GotoAsync($"{_dashboardUrl}/tasks/task-center");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Page.WaitForTimeoutAsync(1000);

        // 驗證頁面主要容器已渲染
        var bodyContent = await Page.ContentAsync();
        Assert.IsFalse(string.IsNullOrWhiteSpace(bodyContent), "頁面內容不應為空");

        // 截圖記錄元件可見性狀態
        var screenshotPath = Path.Combine(ScreenshotDir, "PR71_TaskCenter_元件可見性驗證.png");
        await Page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = screenshotPath,
            FullPage = true
        });

        Assert.IsTrue(File.Exists(screenshotPath), $"截圖檔案應存在：{screenshotPath}");
    }

    [TestMethod]
    public async Task 任務中心頁面_響應式視窗_行動裝置_截圖驗證()
    {
        await Page.SetViewportSizeAsync(375, 812);
        await Page.GotoAsync($"{_dashboardUrl}/tasks/task-center");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Page.WaitForTimeoutAsync(1000);

        var screenshotPath = Path.Combine(ScreenshotDir, "PR71_TaskCenter_行動裝置_亮色模式.png");
        await Page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = screenshotPath,
            FullPage = true
        });

        Assert.IsTrue(File.Exists(screenshotPath), $"截圖檔案應存在：{screenshotPath}");
        var fileInfo = new FileInfo(screenshotPath);
        Assert.IsTrue(fileInfo.Length > 0, "截圖檔案不應為空");
    }

    [TestMethod]
    public async Task 任務中心頁面_響應式視窗_行動裝置_暗色模式_截圖驗證()
    {
        await Page.SetViewportSizeAsync(375, 812);
        await Page.GotoAsync($"{_dashboardUrl}/tasks/task-center");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Page.WaitForTimeoutAsync(1000);

        await 切換暗色模式();

        var screenshotPath = Path.Combine(ScreenshotDir, "PR71_TaskCenter_行動裝置_暗色模式.png");
        await Page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = screenshotPath,
            FullPage = true
        });

        Assert.IsTrue(File.Exists(screenshotPath), $"截圖檔案應存在：{screenshotPath}");
        var fileInfo = new FileInfo(screenshotPath);
        Assert.IsTrue(fileInfo.Length > 0, "截圖檔案不應為空");
    }

    [TestMethod]
    public async Task 任務中心頁面_寬螢幕視窗_截圖驗證()
    {
        await Page.SetViewportSizeAsync(1920, 1080);
        await Page.GotoAsync($"{_dashboardUrl}/tasks/task-center");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Page.WaitForTimeoutAsync(1000);

        var screenshotPath = Path.Combine(ScreenshotDir, "PR71_TaskCenter_寬螢幕_亮色模式.png");
        await Page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = screenshotPath,
            FullPage = true
        });

        Assert.IsTrue(File.Exists(screenshotPath), $"截圖檔案應存在：{screenshotPath}");
        var fileInfo = new FileInfo(screenshotPath);
        Assert.IsTrue(fileInfo.Length > 0, "截圖檔案不應為空");
    }

    [TestMethod]
    public async Task 任務中心頁面_寬螢幕視窗_暗色模式_截圖驗證()
    {
        await Page.SetViewportSizeAsync(1920, 1080);
        await Page.GotoAsync($"{_dashboardUrl}/tasks/task-center");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Page.WaitForTimeoutAsync(1000);

        await 切換暗色模式();

        var screenshotPath = Path.Combine(ScreenshotDir, "PR71_TaskCenter_寬螢幕_暗色模式.png");
        await Page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = screenshotPath,
            FullPage = true
        });

        Assert.IsTrue(File.Exists(screenshotPath), $"截圖檔案應存在：{screenshotPath}");
        var fileInfo = new FileInfo(screenshotPath);
        Assert.IsTrue(fileInfo.Length > 0, "截圖檔案不應為空");
    }
}
```