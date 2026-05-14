using AiTeam.Dashboard.Components.Pages.Agents.Dialogs;
using MudBlazor;

namespace AiTeam.Dashboard.Components.Pages.Agents;

/// <summary>
/// Stage 38：Provider / Model 下拉清單資料源為 <see cref="LlmModels"/> 常數檔。
/// Edge case 提醒（未來維護者）：若 LlmModels 清單刪除某 model（例如 Gemini 2.5 於 2026-06-17 deprecated 後移除），
/// 但 DB 仍有舊值 → MudSelect 會顯示空白；目前 UI 已加 MudAlert warning 提示，但本 Stage 不做自動遷移。
/// 發生時再補資料遷移腳本（或在 LlmModels 加「legacy 相容清單」）。
/// </summary>
public partial class AgentSettings
{
    #region Dependencies

    [Inject]
    private DashboardAgentService AgentService { get; set; } = null!;

    [Inject]
    private DashboardBotService BotService { get; set; } = null!;

    [Inject]
    private IDialogService DialogService { get; set; } = null!;

    [Inject]
    private ISnackbar Snackbar { get; set; } = null!;

    #endregion

    #region Private Variables

    private List<AgentConfigDto>  _agents         = [];
    private Dictionary<Guid, int> _trustLevels    = [];
    private AgentConfigDto?       _selectedAgent;
    private bool                  _isSaving;
    private bool                  _isTogglingActive;
    private bool                  _isSavingLlm;
    private string?               _saveMessage;
    private string?               _errorMessage;
    private string?               _loadError;

    // 重啟 Bot
    private bool  _showRestartConfirm;
    private bool  _isRestarting;

    #endregion

    #region Override Methods

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _agents = await AgentService.GetAgentConfigsAsync();
            foreach (var agent in _agents)
                _trustLevels[agent.Id] = agent.TrustLevel;
        }
        catch (Exception ex)
        {
            _loadError = ex.Message;
        }
    }

    #endregion

    #region Private Methods

    private async Task ToggleIsActiveAsync(AgentConfigDto agent, bool newValue)
    {
        if (_isTogglingActive) return;
        _isTogglingActive = true;
        try
        {
            agent.IsActive = await AgentService.UpdateIsActiveAsync(agent.Id, newValue);
            _saveMessage = $"{agent.Name} 已{(agent.IsActive ? "啟用" : "停用")}";
            Snackbar.Add(_saveMessage, Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"{agent.Name} 狀態切換失敗：{ex.Message}", Severity.Error);
        }
        finally
        {
            _isTogglingActive = false;
        }
    }

    private async Task OpenCreateAgentDialogAsync()
    {
        var dialog = await DialogService.ShowAsync<AgentCreateDialog>("新增 Agent");
        var result = await dialog.Result;
        if (result is { Canceled: false } && result.Data is AgentConfigDto created)
        {
            _agents.Add(created);
            _trustLevels[created.Id] = created.TrustLevel;
            _saveMessage = $"Agent「{created.Name}」已新增，重啟 Bot 後生效。";
            Snackbar.Add(_saveMessage, Severity.Success);
        }
    }

    private async Task SaveTrustLevelAsync(AgentConfigDto agent)
    {
        _isSaving    = true;
        _saveMessage = null;
        try
        {
            await AgentService.UpdateTrustLevelAsync(agent.Id, _trustLevels[agent.Id]);
            agent.TrustLevel = _trustLevels[agent.Id];
            _saveMessage = $"{agent.Name} 信任等級已儲存為 Lv{_trustLevels[agent.Id]}";
            Snackbar.Add(_saveMessage, Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"{agent.Name} 信任等級儲存失敗：{ex.Message}", Severity.Error);
        }
        finally
        {
            _isSaving = false;
        }
    }

    private async Task RestartBotAsync()
    {
        _isRestarting = true;
        var success = await BotService.RestartBotAsync();
        _showRestartConfirm = false;
        if (success)
        {
            _errorMessage = null;
            _saveMessage  = "Bot 重啟指令已送出，請稍候約 30 秒後確認上線狀態";
            Snackbar.Add(_saveMessage, Severity.Success);
        }
        else
        {
            _saveMessage  = null;
            _errorMessage = "重啟失敗，請確認 Bot 服務設定";
            Snackbar.Add(_errorMessage, Severity.Error);
        }
        _isRestarting = false;
    }

    /// <summary>
    /// Stage 38：Provider 變更時：若目前 Model 不在新 Provider 清單 → 清空 Model + 提示重選；
    /// 若還在清單內 → 直接連同 Provider 存檔（雙欄位同寫）。
    /// </summary>
    private async Task OnProviderChangedAsync(AgentConfigDto agent, string newProvider)
    {
        agent.Provider = newProvider;
        var validModels = LlmModels.GetModelsForProvider(newProvider);
        if (string.IsNullOrEmpty(agent.Model) || !validModels.Contains(agent.Model))
        {
            agent.Model = null;
            Snackbar.Add($"Provider 已改為 {newProvider}，請選擇對應的 Model。", Severity.Warning);
            return;
        }
        await SaveProviderModelAsync(agent);
    }

    /// <summary>Stage 38：Model 變更時連同現 Provider 存檔 + 觸發 Bot 端 cache invalidate。</summary>
    private async Task OnModelChangedAsync(AgentConfigDto agent, string newModel)
    {
        agent.Model = newModel;
        if (string.IsNullOrEmpty(agent.Provider) || string.IsNullOrEmpty(agent.Model)) return;
        await SaveProviderModelAsync(agent);
    }

    /// <summary>Stage 47：儲存 per-agent Token Limit，成功後立即刷新 Bot AgentConfigCache。</summary>
    private async Task SaveTokenLimitsAsync(AgentConfigDto agent)
    {
        if (_isSavingLlm) return;
        _isSavingLlm = true;
        try
        {
            var ok = await AgentService.UpdateTokenLimitsAsync(
                agent.Id,
                agent.DailyTokenLimitK,
                agent.MonthlyTokenLimitK);
            if (!ok)
            {
                Snackbar.Add($"{agent.Name} Token Limit 儲存失敗：查無 Agent", Severity.Error);
                return;
            }
            await BotService.ReloadCacheAsync("agent-config");
            var dailyStr   = agent.DailyTokenLimitK > 0
                ? $"{agent.DailyTokenLimitK}K"
                : "未設定（fallback appsettings）";
            var monthlyStr = agent.MonthlyTokenLimitK > 0
                ? $"{agent.MonthlyTokenLimitK}K"
                : "未設定（fallback appsettings）";
            Snackbar.Add($"{agent.Name}：日限={dailyStr} / 月限={monthlyStr} 已更新，Bot Cache 已刷新。", Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"儲存失敗：{ex.Message}", Severity.Error);
        }
        finally
        {
            _isSavingLlm = false;
        }
    }

    private async Task SaveProviderModelAsync(AgentConfigDto agent)
    {
        if (_isSavingLlm) return;
        if (string.IsNullOrEmpty(agent.Provider) || string.IsNullOrEmpty(agent.Model)) return;

        _isSavingLlm = true;
        try
        {
            var ok = await AgentService.UpdateProviderModelAsync(agent.Id, agent.Provider, agent.Model);
            if (!ok)
            {
                Snackbar.Add($"{agent.Name} 儲存失敗：查無 Agent", Severity.Error);
                return;
            }
            // Stage 38：通知 Bot 端快取失效，下次任務立即生效（無需重啟）
            await BotService.ReloadCacheAsync("agent-config");
            Snackbar.Add($"{agent.Name}：Provider={agent.Provider}、Model={agent.Model} 已更新，Bot Cache 已刷新。", Severity.Success);
        }
        catch (ArgumentException ex)
        {
            Snackbar.Add($"儲存失敗：{ex.Message}", Severity.Error);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"儲存失敗：{ex.Message}", Severity.Error);
        }
        finally
        {
            _isSavingLlm = false;
        }
    }

    #endregion
}
