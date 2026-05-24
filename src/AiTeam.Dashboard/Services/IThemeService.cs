namespace AiTeam.Dashboard.Services;

/// <summary>
/// Stage 86 子項 6：Theme 切換 Scoped service（per Blazor circuit）。
///
/// 設計脈絡（修 Stage 83 v4 Bug 9 + Aria Roadmap 子項 6 衝突）：
/// - MainLayout 是 Static SSR（mudblazor.md 第一節紀律 / 加 @rendermode 拋錯）→ 無法持有 C# event
/// - 既有 MudProviders.razor 已 binding MudThemeProvider.IsDarkMode = _isDarkMode / OnAfterRenderAsync 讀 localStorage 設初值（reload 後 sync OK）
/// - Bug：切換 button 走 JS appTheme.setDark → 只改 html[data-theme] 跟 CSS 變數 / MudProviders._isDarkMode 不變 → MudPaper / MudCard 不切色（要 reload）
///
/// 修法：ThemeToggleButton（@rendermode Interactive）+ MudProviders 都注入 IThemeService → ThemeToggleButton click 改 state + raise event → MudProviders subscribe → StateHasChanged → MudBlazor 即時切色。
/// </summary>
public interface IThemeService
{
    bool IsDarkMode { get; }

    /// <summary>state 改變時 fire（subscribers: MudProviders / 其他需要 sync 的 Interactive component）。</summary>
    event Action? OnChanged;

    void SetDarkMode(bool isDark);
}

/// <summary>IThemeService 預設實作 — 對齊 AddScoped&lt;IThemeService, ThemeService&gt; / per Blazor circuit。</summary>
public class ThemeService : IThemeService
{
    public bool IsDarkMode { get; private set; }

    public event Action? OnChanged;

    public void SetDarkMode(bool isDark)
    {
        if (IsDarkMode == isDark) return;
        IsDarkMode = isDark;
        OnChanged?.Invoke();
    }
}
