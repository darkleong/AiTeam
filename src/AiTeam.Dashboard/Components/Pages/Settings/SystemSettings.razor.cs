using MudBlazor;

namespace AiTeam.Dashboard.Components.Pages.Settings;

public partial class SystemSettings
{
    #region Dependencies

    [Inject]
    private DashboardAppSettingsService AppSettingsService { get; set; } = null!;

    [Inject]
    private DashboardBotService BotService { get; set; } = null!;

    [Inject]
    private ISnackbar Snackbar { get; set; } = null!;

    #endregion

    #region Private Variables

    private bool    _skipCeoConfirm;
    private bool    _mockMode;
    private string  _ceoChannelId  = "";
    private string  _christUserId  = "";
    private bool    _isReloading;
    private bool    _isSavingChannel;
    private bool    _isSavingUserId;
    private string? _saveMessage;

    #endregion

    #region Lifecycle

    protected override async Task OnInitializedAsync()
    {
        var skipSetting = await AppSettingsService.GetAsync("SkipCeoConfirm");
        _skipCeoConfirm = bool.TryParse(skipSetting?.Value, out var v) && v;

        var mockSetting = await AppSettingsService.GetAsync("MockMode");
        _mockMode = bool.TryParse(mockSetting?.Value, out var mv) && mv;

        var channelSetting = await AppSettingsService.GetAsync("CeoDefaultChannelId");
        _ceoChannelId = channelSetting?.Value ?? "";

        var userIdSetting = await AppSettingsService.GetAsync("ChristDiscordUserId");
        _christUserId = userIdSetting?.Value ?? "";
    }

    #endregion

    #region Private Methods

    private async Task OnSkipCeoConfirmChanged(bool newValue)
    {
        _skipCeoConfirm = newValue;
        await AppSettingsService.UpsertAsync("SkipCeoConfirm", _skipCeoConfirm.ToString().ToLower());
        _saveMessage = $"「跳過 CEO 派工確認」已{(_skipCeoConfirm ? "啟用" : "停用")}，5 分鐘內自動生效";
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

    private async Task ReloadCacheAsync()
    {
        _isReloading = true;
        var ok = await BotService.ReloadCacheAsync("all");
        _isReloading = false;
        Snackbar.Add(ok ? "已套用變更（規則與系統設定快取已更新）" : "套用失敗，請確認 Bot 服務正常",
            ok ? Severity.Success : Severity.Error);
    }

    #endregion
}
