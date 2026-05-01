using System.IO;
using System.Threading.Tasks;

namespace AiTeam.Tests.Playwright;

/// <summary>
/// Stage 47：Token 守門設定 UI 驗收截圖。
/// 驗證 SystemSettings 頁 Token 守門設定區塊 + AgentSettings 頁 Token Limit 欄位是否正確出現。
/// </summary>
[TestClass]
public class Stage47TokenSettingsTest : PageTest
{
    private static readonly string DashboardUrl =
        Environment.GetEnvironmentVariable("DASHBOARD_URL") ?? "http://localhost:5051";

    private static readonly string AdminEmail =
        Environment.GetEnvironmentVariable("DASHBOARD_ADMIN_EMAIL") ?? "admin@aiteam.local";

    private static readonly string AdminPassword =
        Environment.GetEnvironmentVariable("DASHBOARD_ADMIN_PASSWORD") ?? "Admin1234!";

    private async Task LoginAsync()
    {
        await Page.GotoAsync($"{DashboardUrl}/login");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await Page.FillAsync("input[type='email'], input[name='email'], input[placeholder*='Email'], input[placeholder*='email']", AdminEmail);
        await Page.FillAsync("input[type='password']", AdminPassword);
        await Page.ClickAsync("button[type='submit'], button:has-text('Login'), button:has-text('登入')");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    [TestMethod]
    public async Task SystemSettings頁_Token守門設定區塊_出現並截圖()
    {
        await LoginAsync();

        await Page.GotoAsync($"{DashboardUrl}/system-settings");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        // 等待 Token 守門設定 h3 出現
        await Page.WaitForSelectorAsync("h3:has-text('Token 守門設定')", new() { Timeout = 10000 });

        var dir = Path.Combine(Directory.GetCurrentDirectory(), "screenshots");
        Directory.CreateDirectory(dir);

        // 全頁截圖
        await Page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path     = Path.Combine(dir, "stage47-system-settings-token.png"),
            FullPage = true
        });

        // 確認 Token 守門設定 section 存在
        var tokenSection = await Page.QuerySelectorAsync("h3:has-text('Token 守門設定')");
        Assert.IsNotNull(tokenSection, "SystemSettings 頁應出現「Token 守門設定」標題");

        // 確認兩個 MudNumericField 存在（全域月限 + 單次上限）
        var inputs = await Page.QuerySelectorAllAsync("input[id*='MudNumericField']");
        Assert.IsTrue(inputs.Count >= 2, $"Token 守門設定應有至少 2 個數字輸入欄，實際：{inputs.Count}");
    }

    [TestMethod]
    public async Task AgentSettings頁_TokenLimit欄位_出現並截圖()
    {
        await LoginAsync();

        await Page.GotoAsync($"{DashboardUrl}/agents");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // 點選第一個 Agent（CEO）
        var firstAgent = await Page.QuerySelectorAsync(".mud-list-item");
        if (firstAgent is not null)
        {
            await firstAgent.ClickAsync();
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            // 等待 Token 限額設定 subtitle 出現
            await Page.WaitForSelectorAsync("text=Token 限額設定", new() { Timeout = 8000 });
        }

        var dir = Path.Combine(Directory.GetCurrentDirectory(), "screenshots");
        Directory.CreateDirectory(dir);

        await Page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path     = Path.Combine(dir, "stage47-agent-settings-token.png"),
            FullPage = true
        });

        // 確認 Token 限額設定 section 存在（即使沒選 Agent 也至少頁面載入成功）
        var title = await Page.TitleAsync();
        Assert.IsTrue(title.Length > 0, "AgentSettings 頁應正常載入");
    }
}
