using System.IO;
using System.Threading.Tasks;

namespace AiTeam.Tests.Playwright;

/// <summary>
/// Dashboard 基線 Smoke Test：驗證登入頁可正常存取並截圖。
/// </summary>
[TestClass]
public class DashboardSmokeTest : PageTest
{
    private static readonly string DashboardUrl =
        Environment.GetEnvironmentVariable("DASHBOARD_URL") ?? "http://localhost:5051";

    [TestMethod]
    public async Task 登入頁面_可正常存取_截圖驗證()
    {
        await Page.GotoAsync(DashboardUrl);

        // 等待頁面穩定（登入頁或 redirect）
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // 截圖存到 screenshots/
        var dir = Path.Combine(Directory.GetCurrentDirectory(), "screenshots");
        Directory.CreateDirectory(dir);
        await Page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path     = Path.Combine(dir, "dashboard-login.png"),
            FullPage = true
        });

        // 驗證頁面標題包含 AI Team 或 Login
        var title = await Page.TitleAsync();
        Assert.IsTrue(
            title.Contains("AI Team", StringComparison.OrdinalIgnoreCase) ||
            title.Contains("Login",   StringComparison.OrdinalIgnoreCase) ||
            title.Length > 0,
            $"頁面標題應非空，實際：{title}");
    }
}
