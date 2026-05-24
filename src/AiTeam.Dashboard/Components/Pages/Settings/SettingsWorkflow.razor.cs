using AiTeam.Dashboard.Services;
using MudBlazor;

namespace AiTeam.Dashboard.Components.Pages.Settings;

/// <summary>
/// Stage 87 B0：Settings 拆 6 sub-page 之 Workflow Flags（取代 SettingsHub.razor Tab 1）。
/// 無 SignalR 訂閱（純 DB 操作）/ 對齊 Stage 86 既有 21 Workflow:* flag 管理 + Restart Bot。
/// </summary>
public partial class SettingsWorkflow
{
    #region Dependencies

    [Inject] private DashboardAppSettingsService SettingsSvc { get; set; } = null!;
    [Inject] private DashboardBotService         BotSvc      { get; set; } = null!;
    [Inject] private ISnackbar                   Snackbar    { get; set; } = null!;
    [Inject] private ILogger<SettingsWorkflow>   Logger      { get; set; } = null!;

    #endregion

    private Dictionary<string, string?> _flagValues = new();
    private bool   _pendingRestart;
    private bool   _isRestarting;
    private string? _saveMessage;

    private static readonly string[] _v5Flags =
    [
        "Workflow:UsePetraOrchestratorV5",
        "Workflow:UseV5Memory",
        "Workflow:UseV5SubtaskPlanning",
        "Workflow:UseV5PromptDb",
        "Workflow:UseHITLPlanConfirmation",
        "Workflow:UseDynamicReplanning",
    ];

    private static readonly (string Key, int Min, int Max)[] _numericFlags =
    [
        ("Workflow:V5MemoryCompactThresholdPercent", 30, 95),
        ("Workflow:V5MemoryCompactKeepCount",       10, 200),
        ("Workflow:MaxConcurrentPetra",             1, 10),
        ("Workflow:MaxAttachmentsPerTask",          1, 20),
        ("Workflow:MaxAttachmentSizeMB",            1, 100),
        ("Workflow:MaxReplanIterations",            1, 10),
    ];

    protected override async Task OnInitializedAsync() => await LoadFlagsAsync();

    private async Task LoadFlagsAsync()
    {
        try
        {
            var allKeys = _v5Flags.Concat(_numericFlags.Select(n => n.Key)).ToArray();
            _flagValues = new Dictionary<string, string?>();
            foreach (var k in allKeys)
            {
                var row = await SettingsSvc.GetAsync(k);
                _flagValues[k] = row?.Value;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "SettingsWorkflow LoadFlagsAsync 失敗");
        }
    }

    private bool GetBoolFlag(string key)
        => bool.TryParse(_flagValues.GetValueOrDefault(key), out var v) && v;

    private int GetIntFlag(string key)
        => int.TryParse(_flagValues.GetValueOrDefault(key), out var v) ? v : 0;

    private async Task SetBoolFlagAsync(string key, bool value)
    {
        try
        {
            await SettingsSvc.UpsertAsync(key, value.ToString().ToLowerInvariant());
            _flagValues[key] = value.ToString().ToLowerInvariant();
            _pendingRestart = true;
            _saveMessage = $"{key} = {value}（需重啟 Bot 才生效）";
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Flag 寫入失敗：{ex.Message}", Severity.Error);
        }
    }

    private async Task SetIntFlagAsync(string key, int value)
    {
        try
        {
            await SettingsSvc.UpsertAsync(key, value.ToString());
            _flagValues[key] = value.ToString();
            _pendingRestart = true;
            _saveMessage = $"{key} = {value}（需重啟 Bot 才生效）";
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Flag 寫入失敗：{ex.Message}", Severity.Error);
        }
    }

    private async Task RestartBotAsync()
    {
        _isRestarting = true;
        try
        {
            await BotSvc.RestartBotAsync();
            Snackbar.Add("已觸發 Bot 重啟，CI/CD self-hosted runner 接手部署中（約 30 秒-2 分鐘）。", Severity.Success);
            _pendingRestart = false;
            _saveMessage = null;
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Restart Bot 失敗：{ex.Message}", Severity.Error);
        }
        finally
        {
            _isRestarting = false;
        }
    }

    private static string GetFlagDescription(string key) => key switch
    {
        "Workflow:UsePetraOrchestratorV5"          => "動態 orchestrator（Petra 拆 SubtaskPlan + 派工）",
        "Workflow:UseV5Memory"                     => "跨 Session 長期記憶",
        "Workflow:UseV5SubtaskPlanning"            => "動態 SubtaskPlan 拆解",
        "Workflow:UseV5PromptDb"                   => "Prompt DB 化（SkillPrompt / TalentPrompt 動態管理）",
        "Workflow:UseHITLPlanConfirmation"         => "plan_confirm HITL gate（老闆審 SubtaskPlan 才執行）",
        "Workflow:UseDynamicReplanning"            => "動態 replan + replan_confirm HITL retry gate",
        "Workflow:V5MemoryCompactThresholdPercent" => "記憶 compact 觸發 % 閾值",
        "Workflow:V5MemoryCompactKeepCount"        => "記憶 compact 保留筆數",
        "Workflow:MaxConcurrentPetra"              => "並行 Petra 消費者數",
        "Workflow:MaxAttachmentsPerTask"           => "per task 附件上限",
        "Workflow:MaxAttachmentSizeMB"             => "per 附件 MB 上限",
        "Workflow:MaxReplanIterations"             => "動態 replan 最大 iteration 輪次",
        _                                          => "（無描述）",
    };
}
