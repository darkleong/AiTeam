using AiTeam.Dashboard.Services;
using MudBlazor;

namespace AiTeam.Dashboard.Components.Pages.Settings;

/// <summary>
/// Stage 87 follow-up #9：Token 守門 sub-page（從既有 SystemSettings reuse component 拆出 Token 段）。
/// 對應 NavMenu「設定中心 → Token 守門」/ 取代之前 reuse SystemSettings.razor 整檔。
/// 無 SignalR 訂閱（純 DB 操作 + Bot Cache reload）。
/// </summary>
public partial class SettingsTokens
{
    #region Dependencies

    [Inject] private DashboardAppSettingsService AppSettingsService { get; set; } = null!;
    [Inject] private DashboardBotService         BotService         { get; set; } = null!;

    #endregion

    #region Private State

    private int     _globalMonthlyLimitK;   // 0 = DB 無設定，fallback appsettings
    private int     _singleRequestLimitK;   // 0 = DB 無設定，fallback appsettings
    private bool    _isSavingTokenLimits;
    private string? _saveMessage;

    #endregion

    protected override async Task OnInitializedAsync()
    {
        _globalMonthlyLimitK = await LoadTokenLimitAsync("Token:GlobalMonthlyLimitK");
        _singleRequestLimitK = await LoadTokenLimitAsync("Token:SingleRequestLimitK");
    }

    private async Task<int> LoadTokenLimitAsync(string key)
    {
        var setting = await AppSettingsService.GetAsync(key);
        return int.TryParse(setting?.Value, out var v) && v > 0 ? v : 0;
    }

    private async Task SaveTokenLimitsAsync()
    {
        if (_globalMonthlyLimitK <= 0 || _singleRequestLimitK <= 0)
        {
            _saveMessage = "格式錯誤：Token 上限必須大於 0";
            return;
        }
        _isSavingTokenLimits = true;
        await AppSettingsService.UpsertAsync("Token:GlobalMonthlyLimitK", _globalMonthlyLimitK.ToString());
        await AppSettingsService.UpsertAsync("Token:SingleRequestLimitK", _singleRequestLimitK.ToString());
        // 立即刷新 Bot Cache，不需等 5 分鐘 TTL
        await BotService.ReloadCacheAsync("all");
        _isSavingTokenLimits = false;
        _saveMessage = $"Token 守門設定已儲存（全域月限={_globalMonthlyLimitK}K / 單次上限={_singleRequestLimitK}K），Bot Cache 已立即刷新。";
    }
}
