using Microsoft.JSInterop;
using MudBlazor;

namespace AiTeam.Dashboard.Components.Layout;

public partial class MainLayout
{
    private bool _isDarkMode = false;
    private bool _initialized = false;

    private readonly MudTheme _customTheme = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary         = "#6366f1",
            PrimaryDarken   = "#4f46e5",
            PrimaryLighten  = "#818cf8",
            AppbarBackground = "#6366f1",
            Background      = "#f8f9fa",
            Surface         = "#ffffff",
            DrawerBackground = "#ffffff",
            DrawerText      = "rgba(0,0,0,0.7)",
            TextPrimary     = "#212529",
            TextSecondary   = "#6c757d",
            ActionDefault   = "#6c757d",
            Divider         = "#dee2e6",
        },
        PaletteDark = new PaletteDark
        {
            Primary         = "#818cf8",
            PrimaryDarken   = "#6366f1",
            PrimaryLighten  = "#a5b4fc",
            AppbarBackground = "#16213e",
            Background      = "#1a1a2e",
            Surface         = "#0f3460",
            DrawerBackground = "#16213e",
            DrawerText      = "rgba(233,236,239,0.7)",
            TextPrimary     = "#e9ecef",
            TextSecondary   = "#adb5bd",
            ActionDefault   = "#adb5bd",
            Divider         = "#495057",
            TableLines      = "#495057",
            OverlayLight    = "rgba(15,52,96,0.5)",
        }
    };

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || _initialized) return;
        _initialized = true;

        // 從 localStorage 讀取使用者上次的主題設定
        var saved = await JS.InvokeAsync<string?>("localStorage.getItem", "theme");
        _isDarkMode = saved == "dark";

        // 同步 data-theme 給自訂 CSS 變數（sidebar / nav 使用）
        await JS.InvokeVoidAsync("eval",
            $"document.documentElement.dataset.theme = '{(saved ?? "light")}'");

        StateHasChanged();
    }

    private async Task ToggleThemeAsync()
    {
        _isDarkMode = !_isDarkMode;
        var theme = _isDarkMode ? "dark" : "light";

        // 同步 localStorage 與 data-theme（給自訂 CSS 變數使用）
        await JS.InvokeVoidAsync("eval",
            $"localStorage.setItem('theme','{theme}');document.documentElement.dataset.theme='{theme}'");
    }
}
