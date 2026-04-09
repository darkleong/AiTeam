using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace AiTeam.Dashboard.Components.Layout;

public partial class MainLayout
{
    [Inject] private IJSRuntime JS { get; set; } = null!;

    private bool _isDarkMode = false;
    private bool _sidebarOpen = true;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _isDarkMode = await JS.InvokeAsync<bool>("appTheme.init");
            StateHasChanged();
        }
    }

    private async Task OnDarkModeChangedAsync(bool isDark)
    {
        _isDarkMode = isDark;
        await JS.InvokeVoidAsync("appTheme.setDark", isDark);
    }

    private void ToggleSidebar() => _sidebarOpen = !_sidebarOpen;

    protected string AppVersion
    {
        get
        {
            var version = Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion;
            return string.IsNullOrWhiteSpace(version) ? string.Empty : $"v{version}";
        }
    }
}
