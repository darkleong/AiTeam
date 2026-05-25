using AiTeam.Dashboard.Services;
using MudBlazor;

namespace AiTeam.Dashboard.Components.Pages.Settings;

/// <summary>
/// Stage 87 follow-up #9：系統設定 sub-page（從既有 SystemSettings reuse component 拆出 Mock + CEO 通道段）。
/// 對應 NavMenu「設定中心 → 系統設定」/ 取代之前 reuse SystemSettings.razor 整檔。
/// 無 SignalR 訂閱（純 DB 操作 + Bot Cache reload 走「套用變更」button）。
/// </summary>
public partial class SettingsSystem
{
    #region Dependencies

    [Inject] private DashboardAppSettingsService AppSettingsService { get; set; } = null!;
    [Inject] private DashboardBotService         BotService         { get; set; } = null!;
    [Inject] private ISnackbar                   Snackbar           { get; set; } = null!;

    #endregion

    #region Private State

    private bool    _mockMode;
    private string  _ceoChannelId      = "";
    private string  _christUserId      = "";
    private int     _mockDelayMin      = 30000;
    private int     _mockDelayMax      = 60000;
    private bool    _isReloading;
    private bool    _isSavingChannel;
    private bool    _isSavingUserId;
    private bool    _isSavingMockDelay;
    private string? _saveMessage;

    #endregion

    protected override async Task OnInitializedAsync()
    {
        var mockSetting = await AppSettingsService.GetAsync("MockMode");
        _mockMode = bool.TryParse(mockSetting?.Value, out var mv) && mv;

        var channelSetting = await AppSettingsService.GetAsync("CeoDefaultChannelId");
        _ceoChannelId = channelSetting?.Value ?? "";

        var userIdSetting = await AppSettingsService.GetAsync("ChristDiscordUserId");
        _christUserId = userIdSetting?.Value ?? "";

        var delayMinSetting = await AppSettingsService.GetAsync("Mock:DelayMinMs");
        if (int.TryParse(delayMinSetting?.Value, out var delayMin) && delayMin >= 0)
            _mockDelayMin = delayMin;

        var delayMaxSetting = await AppSettingsService.GetAsync("Mock:DelayMaxMs");
        if (int.TryParse(delayMaxSetting?.Value, out var delayMax) && delayMax > 0)
            _mockDelayMax = delayMax;
    }

    private async Task OnMockModeChanged(bool newValue)
    {
        _mockMode = newValue;
        await AppSettingsService.UpsertAsync("MockMode", _mockMode.ToString().ToLower());
        _saveMessage = $"「Mock Mode」已{(_mockMode ? "啟用" : "停用")}，5 分鐘內自動生效";
    }

    private async Task SaveCeoChannelIdAsync()
    {
        var trimmed = _ceoChannelId.Trim();
        if (!IsValidSnowflakeId(trimmed))
        {
            _saveMessage = "格式錯誤：Discord 頻道 ID 應為 17-20 位純數字";
            return;
        }

        _isSavingChannel = true;
        await AppSettingsService.UpsertAsync("CeoDefaultChannelId", trimmed);
        _isSavingChannel = false;
        _saveMessage = $"CEO 指令預設頻道已更新{(string.IsNullOrWhiteSpace(trimmed) ? "（已清除）" : $"：{trimmed}")}";
    }

    private async Task SaveChristUserIdAsync()
    {
        var trimmed = _christUserId.Trim();
        if (!IsValidSnowflakeId(trimmed))
        {
            _saveMessage = "格式錯誤：Discord User ID 應為 17-20 位純數字";
            return;
        }

        _isSavingUserId = true;
        await AppSettingsService.UpsertAsync("ChristDiscordUserId", trimmed);
        _isSavingUserId = false;
        _saveMessage = $"Christ Discord User ID 已更新{(string.IsNullOrWhiteSpace(trimmed) ? "（已清除）" : $"：{trimmed}")}";
    }

    /// <summary>Discord Snowflake ID 格式驗證：空字串（代表清除）或 17-20 位純數字。</summary>
    private static bool IsValidSnowflakeId(string value)
        => string.IsNullOrEmpty(value) || System.Text.RegularExpressions.Regex.IsMatch(value, @"^\d{17,20}$");

    private bool IsMockDelayValid()
        => _mockDelayMin >= 0 && _mockDelayMax > _mockDelayMin && _mockDelayMax <= 600000;

    private async Task SaveMockDelayAsync()
    {
        if (!IsMockDelayValid())
        {
            _saveMessage = "格式錯誤：需滿足 0 ≤ 最小 < 最大 ≤ 600000";
            return;
        }

        _isSavingMockDelay = true;
        await AppSettingsService.UpsertAsync("Mock:DelayMinMs", _mockDelayMin.ToString());
        await AppSettingsService.UpsertAsync("Mock:DelayMaxMs", _mockDelayMax.ToString());
        _isSavingMockDelay = false;
        _saveMessage = $"Mock Mode 延遲範圍已更新：{_mockDelayMin}–{_mockDelayMax} ms（5 分鐘內自動生效）";
    }

    private async Task ReloadCacheAsync()
    {
        _isReloading = true;
        var ok = await BotService.ReloadCacheAsync("all");
        _isReloading = false;
        Snackbar.Add(ok ? "已套用變更（系統設定快取已更新）" : "套用失敗，請確認 Bot 服務正常",
            ok ? Severity.Success : Severity.Error);
    }
}
