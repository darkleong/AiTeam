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
    private int     _mockDelayMin  = 30000;
    private int     _mockDelayMax  = 60000;
    private int     _reviewAppealMaxRounds  = 3;
    private int     _qaFixMaxRounds         = 3;
    private int     _devPlanAppealMaxRounds = 3;
    private int     _kickoffMaxRounds       = 3;
    private int     _designMeetingMaxRounds = 3;
    private int     _globalMonthlyLimitK = 0;   // 0 = DB 無設定，fallback appsettings
    private int     _singleRequestLimitK = 0;   // 0 = DB 無設定，fallback appsettings
    private bool    _useFrameworkAppealLoop;        // Stage 49：v4 漸進遷移 feature flag
    private bool    _useFrameworkKickoff;           // Stage 50：v4 漸進遷移第二步 feature flag
    private bool    _useFrameworkKickoffMidInterrupt; // Stage 51：v4 漸進遷移第三步 HITL 試點 feature flag
    private bool    _useFrameworkDesign;             // Stage 52：v4 漸進遷移第四步 Design Meeting feature flag
    private bool    _useFrameworkPipeline;           // Stage 53A：v4 漸進遷移第五步 macro Pipeline feature flag（三 flag 連動：UseFrameworkKickoff + UseFrameworkDesign 都 true 才有意義）
    private bool    _isSavingTokenLimits;
    private bool    _isReloading;
    private bool    _isSavingChannel;
    private bool    _isSavingUserId;
    private bool    _isSavingMockDelay;
    private bool    _isSavingWorkflow;
    private string? _saveMessage;
    private string? _errorMessage;

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

        var delayMinSetting = await AppSettingsService.GetAsync("Mock:DelayMinMs");
        if (int.TryParse(delayMinSetting?.Value, out var delayMin) && delayMin >= 0)
            _mockDelayMin = delayMin;

        var delayMaxSetting = await AppSettingsService.GetAsync("Mock:DelayMaxMs");
        if (int.TryParse(delayMaxSetting?.Value, out var delayMax) && delayMax > 0)
            _mockDelayMax = delayMax;

        _reviewAppealMaxRounds  = await LoadWorkflowRoundsAsync("Workflow:ReviewAppealMaxRounds", 3);
        _qaFixMaxRounds         = await LoadWorkflowRoundsAsync("Workflow:QaFixMaxRounds", 3);
        _devPlanAppealMaxRounds = await LoadWorkflowRoundsAsync("Workflow:DevPlanAppealMaxRounds", 3);
        _kickoffMaxRounds       = await LoadWorkflowRoundsAsync("Workflow:KickoffMaxRounds", 3);
        _designMeetingMaxRounds = await LoadWorkflowRoundsAsync("Workflow:DesignMeetingMaxRounds", 3);

        _globalMonthlyLimitK = await LoadTokenLimitAsync("Token:GlobalMonthlyLimitK");
        _singleRequestLimitK = await LoadTokenLimitAsync("Token:SingleRequestLimitK");

        // Stage 49：v4 漸進遷移 feature flag
        var frameworkAppealSetting = await AppSettingsService.GetAsync("Workflow:UseFrameworkAppealLoop");
        _useFrameworkAppealLoop = bool.TryParse(frameworkAppealSetting?.Value, out var fav) && fav;

        // Stage 50：v4 漸進遷移第二步 feature flag
        var frameworkKickoffSetting = await AppSettingsService.GetAsync("Workflow:UseFrameworkKickoff");
        _useFrameworkKickoff = bool.TryParse(frameworkKickoffSetting?.Value, out var fkv) && fkv;

        // Stage 51：v4 漸進遷移第三步 HITL 試點 feature flag
        var frameworkKickoffMidInterruptSetting = await AppSettingsService.GetAsync("Workflow:UseFrameworkKickoffMidInterrupt");
        _useFrameworkKickoffMidInterrupt = bool.TryParse(frameworkKickoffMidInterruptSetting?.Value, out var fkmi) && fkmi;

        // Stage 52：v4 漸進遷移第四步 Design Meeting feature flag
        var frameworkDesignSetting = await AppSettingsService.GetAsync("Workflow:UseFrameworkDesign");
        _useFrameworkDesign = bool.TryParse(frameworkDesignSetting?.Value, out var fdv) && fdv;

        var frameworkPipelineSetting = await AppSettingsService.GetAsync("Workflow:UseFrameworkPipeline");
        _useFrameworkPipeline = bool.TryParse(frameworkPipelineSetting?.Value, out var fpv) && fpv;
    }

    private async Task<int> LoadWorkflowRoundsAsync(string key, int fallback)
    {
        var setting = await AppSettingsService.GetAsync(key);
        return int.TryParse(setting?.Value, out var v) && v > 0 ? v : fallback;
    }

    private async Task<int> LoadTokenLimitAsync(string key)
    {
        var setting = await AppSettingsService.GetAsync(key);
        return int.TryParse(setting?.Value, out var v) && v > 0 ? v : 0;
    }

    #endregion

    #region Private Methods

    private async Task OnSkipCeoConfirmChanged(bool newValue)
    {
        _skipCeoConfirm = newValue;
        try
        {
            await AppSettingsService.UpsertAsync("SkipCeoConfirm", _skipCeoConfirm.ToString().ToLower());
            _saveMessage = $"「跳過 CEO 派工確認」已{(_skipCeoConfirm ? "啟用" : "停用")}，5 分鐘內自動生效";
        }
        catch (Exception ex)
        {
            _skipCeoConfirm = !newValue;
            Snackbar.Add($"設定儲存失敗：{ex.Message}", Severity.Error);
        }
    }

    private async Task OnMockModeChanged(bool newValue)
    {
        _mockMode = newValue;
        try
        {
            await AppSettingsService.UpsertAsync("MockMode", _mockMode.ToString().ToLower());
            _saveMessage = $"「Mock Mode」已{(_mockMode ? "啟用" : "停用")}，5 分鐘內自動生效";
        }
        catch (Exception ex)
        {
            _mockMode = !newValue;
            Snackbar.Add($"設定儲存失敗：{ex.Message}", Severity.Error);
        }
    }

    private async Task OnUseFrameworkAppealLoopChanged(bool newValue)
    {
        _useFrameworkAppealLoop = newValue;
        try
        {
            await AppSettingsService.UpsertAsync("Workflow:UseFrameworkAppealLoop", _useFrameworkAppealLoop.ToString().ToLower());
            _saveMessage = $"「MS Agent Framework Appeal Loop」已{(_useFrameworkAppealLoop ? "啟用" : "停用")}（v4 漸進遷移 Stage 49 首發），5 分鐘內自動生效";
        }
        catch (Exception ex)
        {
            _useFrameworkAppealLoop = !newValue;
            Snackbar.Add($"設定儲存失敗：{ex.Message}", Severity.Error);
        }
    }

    private async Task OnUseFrameworkKickoffChanged(bool newValue)
    {
        _useFrameworkKickoff = newValue;
        try
        {
            await AppSettingsService.UpsertAsync("Workflow:UseFrameworkKickoff", _useFrameworkKickoff.ToString().ToLower());
            _saveMessage = $"「MS Agent Framework Kickoff Meeting」已{(_useFrameworkKickoff ? "啟用" : "停用")}（v4 漸進遷移 Stage 50 第二步），5 分鐘內自動生效";
        }
        catch (Exception ex)
        {
            _useFrameworkKickoff = !newValue;
            Snackbar.Add($"設定儲存失敗：{ex.Message}", Severity.Error);
        }
    }

    private async Task OnUseFrameworkKickoffMidInterruptChanged(bool newValue)
    {
        _useFrameworkKickoffMidInterrupt = newValue;
        try
        {
            await AppSettingsService.UpsertAsync("Workflow:UseFrameworkKickoffMidInterrupt", _useFrameworkKickoffMidInterrupt.ToString().ToLower());
            _saveMessage = $"「MS Agent Framework HITL（Kickoff 中途介入試點）」已{(_useFrameworkKickoffMidInterrupt ? "啟用" : "停用")}（v4 漸進遷移 Stage 51 第三步試點），5 分鐘內自動生效";
        }
        catch (Exception ex)
        {
            _useFrameworkKickoffMidInterrupt = !newValue;
            Snackbar.Add($"設定儲存失敗：{ex.Message}", Severity.Error);
        }
    }

    private async Task OnUseFrameworkDesignChanged(bool newValue)
    {
        _useFrameworkDesign = newValue;
        try
        {
            await AppSettingsService.UpsertAsync("Workflow:UseFrameworkDesign", _useFrameworkDesign.ToString().ToLower());
            _saveMessage = $"「MS Agent Framework Design Meeting」已{(_useFrameworkDesign ? "啟用" : "停用")}（v4 漸進遷移 Stage 52 第四步），5 分鐘內自動生效";
        }
        catch (Exception ex)
        {
            _useFrameworkDesign = !newValue;
            Snackbar.Add($"設定儲存失敗：{ex.Message}", Severity.Error);
        }
    }

    private async Task OnUseFrameworkPipelineChanged(bool newValue)
    {
        _useFrameworkPipeline = newValue;
        try
        {
            await AppSettingsService.UpsertAsync("Workflow:UseFrameworkPipeline", _useFrameworkPipeline.ToString().ToLower());
            _saveMessage = $"「MS Agent Framework Pipeline」已{(_useFrameworkPipeline ? "啟用" : "停用")}（v4 漸進遷移 Stage 53A 第五步 macro-orchestration），5 分鐘內自動生效";
        }
        catch (Exception ex)
        {
            _useFrameworkPipeline = !newValue;
            Snackbar.Add($"設定儲存失敗：{ex.Message}", Severity.Error);
        }
    }

    private async Task SaveCeoChannelIdAsync()
    {
        var trimmed = _ceoChannelId.Trim();
        if (!IsValidSnowflakeId(trimmed))
        {
            _errorMessage = "格式錯誤：Discord 頻道 ID 應為 17-20 位純數字";
            _saveMessage  = null;
            Snackbar.Add(_errorMessage, Severity.Error);
            return;
        }

        _isSavingChannel = true;
        try
        {
            await AppSettingsService.UpsertAsync("CeoDefaultChannelId", trimmed);
            _errorMessage = null;
            _saveMessage  = $"CEO 指令預設頻道已更新{(string.IsNullOrWhiteSpace(trimmed) ? "（已清除）" : $"：{trimmed}")}";
        }
        catch (Exception ex)
        {
            _errorMessage = $"儲存失敗：{ex.Message}";
            _saveMessage  = null;
            Snackbar.Add(_errorMessage, Severity.Error);
        }
        finally
        {
            _isSavingChannel = false;
        }
    }

    private async Task SaveChristUserIdAsync()
    {
        var trimmed = _christUserId.Trim();
        if (!IsValidSnowflakeId(trimmed))
        {
            _errorMessage = "格式錯誤：Discord User ID 應為 17-20 位純數字";
            _saveMessage  = null;
            Snackbar.Add(_errorMessage, Severity.Error);
            return;
        }

        _isSavingUserId = true;
        try
        {
            await AppSettingsService.UpsertAsync("ChristDiscordUserId", trimmed);
            _errorMessage = null;
            _saveMessage  = $"Christ Discord User ID 已更新{(string.IsNullOrWhiteSpace(trimmed) ? "（已清除）" : $"：{trimmed}")}";
        }
        catch (Exception ex)
        {
            _errorMessage = $"儲存失敗：{ex.Message}";
            _saveMessage  = null;
            Snackbar.Add(_errorMessage, Severity.Error);
        }
        finally
        {
            _isSavingUserId = false;
        }
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
            _errorMessage = "格式錯誤：需滿足 0 ≤ 最小 < 最大 ≤ 600000";
            _saveMessage  = null;
            Snackbar.Add(_errorMessage, Severity.Error);
            return;
        }

        _isSavingMockDelay = true;
        try
        {
            await AppSettingsService.UpsertAsync("Mock:DelayMinMs", _mockDelayMin.ToString());
            await AppSettingsService.UpsertAsync("Mock:DelayMaxMs", _mockDelayMax.ToString());
            _errorMessage = null;
            _saveMessage  = $"Mock Mode 延遲範圍已更新：{_mockDelayMin}–{_mockDelayMax} ms（5 分鐘內自動生效）";
        }
        catch (Exception ex)
        {
            _errorMessage = $"儲存失敗：{ex.Message}";
            _saveMessage  = null;
            Snackbar.Add(_errorMessage, Severity.Error);
        }
        finally
        {
            _isSavingMockDelay = false;
        }
    }

    private async Task SaveWorkflowRoundsAsync()
    {
        _isSavingWorkflow = true;
        try
        {
            await AppSettingsService.UpsertAsync("Workflow:ReviewAppealMaxRounds",  _reviewAppealMaxRounds.ToString());
            await AppSettingsService.UpsertAsync("Workflow:QaFixMaxRounds",         _qaFixMaxRounds.ToString());
            await AppSettingsService.UpsertAsync("Workflow:DevPlanAppealMaxRounds", _devPlanAppealMaxRounds.ToString());
            await AppSettingsService.UpsertAsync("Workflow:KickoffMaxRounds",       _kickoffMaxRounds.ToString());
            await AppSettingsService.UpsertAsync("Workflow:DesignMeetingMaxRounds", _designMeetingMaxRounds.ToString());
            _saveMessage = $"流程輪次上限已更新（Review={_reviewAppealMaxRounds} / QA={_qaFixMaxRounds} / DevPlan={_devPlanAppealMaxRounds} / Kickoff={_kickoffMaxRounds} / Design={_designMeetingMaxRounds}），5 分鐘內自動生效";
        }
        catch (Exception ex)
        {
            Snackbar.Add($"儲存失敗：{ex.Message}", Severity.Error);
        }
        finally
        {
            _isSavingWorkflow = false;
        }
    }

    private async Task SaveTokenLimitsAsync()
    {
        if (_globalMonthlyLimitK <= 0 || _singleRequestLimitK <= 0)
        {
            _errorMessage = "格式錯誤：Token 上限必須大於 0";
            _saveMessage  = null;
            Snackbar.Add(_errorMessage, Severity.Error);
            return;
        }
        _isSavingTokenLimits = true;
        try
        {
            await AppSettingsService.UpsertAsync("Token:GlobalMonthlyLimitK", _globalMonthlyLimitK.ToString());
            await AppSettingsService.UpsertAsync("Token:SingleRequestLimitK", _singleRequestLimitK.ToString());
            // 立即刷新 Bot Cache，不需等 5 分鐘 TTL
            await BotService.ReloadCacheAsync("all");
            _errorMessage = null;
            _saveMessage  = $"Token 守門設定已儲存（全域月限={_globalMonthlyLimitK}K / 單次上限={_singleRequestLimitK}K），Bot Cache 已立即刷新。";
        }
        catch (Exception ex)
        {
            _errorMessage = $"儲存失敗：{ex.Message}";
            _saveMessage  = null;
            Snackbar.Add(_errorMessage, Severity.Error);
        }
        finally
        {
            _isSavingTokenLimits = false;
        }
    }

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
