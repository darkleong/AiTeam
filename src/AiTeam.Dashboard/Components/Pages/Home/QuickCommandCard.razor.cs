using AiTeam.Dashboard.Services;
using Microsoft.AspNetCore.Components.Forms;

namespace AiTeam.Dashboard.Components.Pages.Home;

public partial class QuickCommandCard
{
    #region Dependencies

    [Inject]
    private DashboardCeoCommandService CeoCommandService { get; set; } = null!;

    [Inject]
    private NavigationManager Navigation { get; set; } = null!;

    /// <summary>Stage 79：v5.5 image flow 補完 — Dashboard hardcoded const 改 AppSetting 動態讀（OnInitializedAsync 載入時更新 _maxFiles / _maxFileSize）。</summary>
    [Inject]
    private DashboardAppSettingsService AppSettings { get; set; } = null!;

    #endregion

    #region Private State

    private string _text         = "";
    private bool   _isSubmitting;
    private bool   _submitted;
    private bool   _dragActive;
    private string? _error;
    private IReadOnlyList<IBrowserFile>? _selectedFiles;

    // Stage 79：default fallback 對齊 Workflow:MaxAttachmentsPerTask + Workflow:MaxAttachmentSizeMB AppSetting / OnInitializedAsync 載入後動態 update
    private int  _maxFiles    = 5;
    private long _maxFileSize = 5L * 1024 * 1024; // 5MB

    #endregion

    #region Override Methods

    /// <summary>Stage 79：v5.5 image flow 補完 — 從 AppSettings 載 Workflow:MaxAttachmentsPerTask + Workflow:MaxAttachmentSizeMB（範圍守 [1, 20]）。</summary>
    protected override async Task OnInitializedAsync()
    {
        var maxCountSetting = await AppSettings.GetAsync("Workflow:MaxAttachmentsPerTask");
        var maxSizeMBSetting = await AppSettings.GetAsync("Workflow:MaxAttachmentSizeMB");

        if (int.TryParse(maxCountSetting?.Value, out var maxCount))
            _maxFiles = Math.Max(1, Math.Min(20, maxCount));

        if (int.TryParse(maxSizeMBSetting?.Value, out var maxSizeMB))
            _maxFileSize = (long)Math.Max(1, Math.Min(20, maxSizeMB)) * 1024 * 1024;
    }

    #endregion

    #region Event Handlers

    /// <summary>
    /// MudFileUpload 的 @bind-Files:after 回呼：在 _selectedFiles 更新後檢查上限與大小，
    /// 違規時剔除不合規的檔並顯示錯誤訊息。
    /// 刻意不設 MudFileUpload.MaximumFileCount — 該屬性超量時會拋例外，改由此處自行過濾。
    /// </summary>
    private void OnFilesValidated()
    {
        _error = null;
        if (_selectedFiles is null) return;

        var messages = new List<string>();
        var valid    = _selectedFiles.ToList();

        // 非圖片格式（accept 屬性只是 picker hint，拖移時 browser 不看）
        var nonImage = valid.Where(f => !f.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)).ToList();
        if (nonImage.Count > 0)
        {
            var names = string.Join("、", nonImage.Select(f => $"「{f.Name}」"));
            messages.Add($"{names} 不是有效的圖片格式，已略過");
            valid = valid.Except(nonImage).ToList();
        }

        // 過大
        var oversized = valid.Where(f => f.Size > _maxFileSize).ToList();
        if (oversized.Count > 0)
        {
            var names = string.Join("、", oversized.Select(f => $"「{f.Name}」"));
            messages.Add($"{names} 超過 {_maxFileSize / 1024 / 1024}MB 限制，已略過");
            valid = valid.Except(oversized).ToList();
        }

        // 超量
        if (valid.Count > _maxFiles)
        {
            messages.Add($"圖片超過 {_maxFiles} 張上限（共 {valid.Count} 張），已保留前 {_maxFiles} 張");
            valid = valid.Take(_maxFiles).ToList();
        }

        if (messages.Count > 0)
            _error = string.Join("；", messages);

        if (valid.Count != _selectedFiles.Count)
            _selectedFiles = valid.Count == 0 ? null : valid;
    }

    private void RemoveFile(int index)
    {
        if (_selectedFiles is null) return;
        var list = _selectedFiles.ToList();
        if (index >= 0 && index < list.Count)
        {
            list.RemoveAt(index);
            _selectedFiles = list.Count == 0 ? null : list;
        }
        _error = null;
    }

    private async Task SubmitAsync()
    {
        if (string.IsNullOrWhiteSpace(_text)) return;
        _isSubmitting = true;
        _error        = null;

        var images = new List<ImageUploadDto>();
        if (_selectedFiles is { Count: > 0 })
        {
            foreach (var file in _selectedFiles)
            {
                try
                {
                    using var stream = file.OpenReadStream(_maxFileSize);
                    var bytes = new byte[file.Size];
                    await stream.ReadExactlyAsync(bytes);
                    images.Add(new ImageUploadDto(Convert.ToBase64String(bytes), file.ContentType));
                }
                catch
                {
                    _error = $"讀取圖片「{file.Name}」失敗，請重試。";
                    _isSubmitting = false;
                    return;
                }
            }
        }

        var result = await CeoCommandService.SendCommandAsync(
            _text,
            images.Count > 0 ? images : null);

        _isSubmitting = false;

        if (!result.Success)
        {
            _error = result.ErrorMessage;
            return;
        }

        _submitted = true;
        _ = Task.Delay(3000).ContinueWith(_ =>
            InvokeAsync(() => Navigation.NavigateTo("/interactions")));
    }

    private void Reset()
    {
        _text          = "";
        _selectedFiles = null;
        _error         = null;
        _submitted     = false;
    }

    #endregion
}
