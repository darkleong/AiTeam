using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;

namespace AiTeam.Dashboard.Components.Pages.Home;

public partial class QuickCommandCard
{
    #region Dependencies

    [Inject]
    private DashboardCeoCommandService CeoCommandService { get; set; } = null!;

    [Inject]
    private NavigationManager Navigation { get; set; } = null!;

    [Inject]
    private IJSRuntime Js { get; set; } = null!;

    #endregion

    #region Private State

    private string _text         = "";
    private bool   _isSubmitting;
    private bool   _submitted;
    private string? _error;
    private List<IBrowserFile> _selectedFiles = [];

    private const int  MaxFiles    = 5;
    private const long MaxFileSize = 5 * 1024 * 1024; // 5MB

    #endregion

    #region Event Handlers

    private async Task OnFilesChangedAsync(InputFileChangeEventArgs e)
    {
        _error = null;
        foreach (var file in e.GetMultipleFiles(MaxFiles))
        {
            if (_selectedFiles.Count >= MaxFiles)
            {
                _error = $"最多只能附加 {MaxFiles} 張圖片";
                break;
            }
            if (file.Size > MaxFileSize)
            {
                _error = $"「{file.Name}」超過 5MB 限制，已略過";
                continue;
            }
            if (!file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                _error = $"「{file.Name}」不是有效的圖片格式，已略過";
                continue;
            }
            _selectedFiles.Add(file);
        }
    }

    private void RemoveFile(int index)
    {
        if (index >= 0 && index < _selectedFiles.Count)
            _selectedFiles.RemoveAt(index);
        _error = null;
    }

    private async Task TriggerFilePicker()
        => await Js.InvokeVoidAsync("document.getElementById('quickcmd-files').click");

    private async Task SubmitAsync()
    {
        if (string.IsNullOrWhiteSpace(_text)) return;
        _isSubmitting = true;
        _error        = null;

        // 讀取圖片 bytes → base64
        var images = new List<ImageUploadDto>();
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
        // 3 秒後導向操作中心
        _ = Task.Delay(3000).ContinueWith(_ =>
            InvokeAsync(() => Navigation.NavigateTo("/interactions")));
    }

    private void Reset()
    {
        _text          = "";
        _selectedFiles = [];
        _error         = null;
        _submitted     = false;
    }

    #endregion
}
