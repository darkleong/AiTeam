using Microsoft.AspNetCore.Components.Forms;

namespace AiTeam.Dashboard.Components.Pages.Home;

public partial class QuickCommandCard
{
    #region Dependencies

    [Inject]
    private DashboardCeoCommandService CeoCommandService { get; set; } = null!;

    [Inject]
    private NavigationManager Navigation { get; set; } = null!;

    #endregion

    #region Private State

    private string _text         = "";
    private bool   _isSubmitting;
    private bool   _submitted;
    private bool   _dragActive;
    private string? _error;
    private IReadOnlyList<IBrowserFile>? _selectedFiles;

    private const int  MaxFiles    = 5;
    private const long MaxFileSize = 5 * 1024 * 1024; // 5MB

    #endregion

    #region Event Handlers

    /// <summary>
    /// MudFileUpload 的 @bind-Files:after 回呼：在 _selectedFiles 更新後檢查上限與大小，
    /// 違規時剔除不合規的檔並顯示錯誤訊息。
    /// </summary>
    private void OnFilesValidated()
    {
        _error = null;
        if (_selectedFiles is null) return;

        // 超量（MaximumFileCount 理論上擋住，保險起見再驗）
        if (_selectedFiles.Count > MaxFiles)
        {
            _error = $"最多只能附加 {MaxFiles} 張圖片，已保留前 {MaxFiles} 張";
            _selectedFiles = _selectedFiles.Take(MaxFiles).ToList();
            return;
        }

        // 過大
        var tooBig = _selectedFiles.FirstOrDefault(f => f.Size > MaxFileSize);
        if (tooBig is not null)
        {
            _error = $"「{tooBig.Name}」超過 5MB 限制，已略過";
            _selectedFiles = _selectedFiles.Where(f => f.Size <= MaxFileSize).ToList();
        }
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
                    using var stream = file.OpenReadStream(MaxFileSize);
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
