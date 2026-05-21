// 測試標的：AiTeam.Dashboard.Components.Pages.Home.QuickCommandCard
// 驗證：grep -r 'class QuickCommandCard' src/AiTeam.Dashboard/ → 命中 QuickCommandCard.razor.cs:6

using System.Collections.Generic;
using System.Reflection;
using AiTeam.Dashboard.Components.Pages.Home;
using FluentAssertions;
using Microsoft.AspNetCore.Components.Forms;
using MudBlazor;
using NSubstitute;
using Xunit;

namespace AiTeam.Dashboard.Components.Pages.Home.Tests;

public class QuickCommandCardTests
{
    private static QuickCommandCard CreateInstanceWithSnackbar()
    {
        var instance = new QuickCommandCard();
        var snackbar = Substitute.For<ISnackbar>();
        typeof(QuickCommandCard)
            .GetProperty("Snackbar", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(instance, snackbar);
        return instance;
    }

    private static void SetSelectedFiles(QuickCommandCard instance, IReadOnlyList<IBrowserFile>? files)
    {
        typeof(QuickCommandCard)
            .GetField("_selectedFiles", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(instance, files);
    }

    private static void InvokeOnFilesValidated(QuickCommandCard instance)
    {
        typeof(QuickCommandCard)
            .GetMethod("OnFilesValidated", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(instance, null);
    }

    private static string? GetError(QuickCommandCard instance)
        => typeof(QuickCommandCard)
            .GetField("_error", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(instance) as string;

    private static IReadOnlyList<IBrowserFile>? GetSelectedFiles(QuickCommandCard instance)
        => typeof(QuickCommandCard)
            .GetField("_selectedFiles", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(instance) as IReadOnlyList<IBrowserFile>;

    // ── OnFilesValidated ──────────────────────────────────────────────────

    [Fact]
    public void OnFilesValidated_包含非圖片檔案_應過濾非圖片並設定錯誤訊息()
    {
        var instance = CreateInstanceWithSnackbar();

        var nonImageFile = Substitute.For<IBrowserFile>();
        nonImageFile.ContentType.Returns("application/pdf");
        nonImageFile.Name.Returns("document.pdf");
        nonImageFile.Size.Returns(1024L);

        SetSelectedFiles(instance, new List<IBrowserFile> { nonImageFile });
        InvokeOnFilesValidated(instance);

        GetError(instance).Should().NotBeNull();
        GetError(instance).Should().Contain("不是有效的圖片格式");
        GetSelectedFiles(instance).Should().BeNull("非圖片應被過濾後剩0張，應設為null");
    }

    [Fact]
    public void OnFilesValidated_有效圖片檔案_應不設定錯誤訊息()
    {
        var instance = CreateInstanceWithSnackbar();

        var imageFile = Substitute.For<IBrowserFile>();
        imageFile.ContentType.Returns("image/jpeg");
        imageFile.Name.Returns("photo.jpg");
        imageFile.Size.Returns(1024L);

        SetSelectedFiles(instance, new List<IBrowserFile> { imageFile });
        InvokeOnFilesValidated(instance);

        GetError(instance).Should().BeNull("有效圖片應無錯誤訊息");
    }

    // ── OnFilesValidated：Snackbar 雙通知驗證 ────────────────────────────────

    [Fact]
    public void OnFilesValidated_非圖片檔案_應同步呼叫Snackbar警告通知()
    {
        var snackbar = Substitute.For<ISnackbar>();
        var instance = new QuickCommandCard();
        typeof(QuickCommandCard)
            .GetProperty("Snackbar", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(instance, snackbar);

        var nonImageFile = Substitute.For<IBrowserFile>();
        nonImageFile.ContentType.Returns("application/pdf");
        nonImageFile.Name.Returns("document.pdf");
        nonImageFile.Size.Returns(1024L);

        SetSelectedFiles(instance, new List<IBrowserFile> { nonImageFile });
        InvokeOnFilesValidated(instance);

        snackbar.Received(1).Add(
            Arg.Is<string>(s => s.Contains("不是有效的圖片格式")),
            Severity.Warning,
            Arg.Any<Action<SnackbarOptions>?>());
    }

    [Fact]
    public void OnFilesValidated_有效圖片檔案_應不觸發Snackbar通知()
    {
        var snackbar = Substitute.For<ISnackbar>();
        var instance = new QuickCommandCard();
        typeof(QuickCommandCard)
            .GetProperty("Snackbar", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(instance, snackbar);

        var imageFile = Substitute.For<IBrowserFile>();
        imageFile.ContentType.Returns("image/png");
        imageFile.Name.Returns("image.png");
        imageFile.Size.Returns(512L);

        SetSelectedFiles(instance, new List<IBrowserFile> { imageFile });
        InvokeOnFilesValidated(instance);

        snackbar.DidNotReceive().Add(Arg.Any<string>(), Arg.Any<Severity>(), Arg.Any<Action<SnackbarOptions>?>());
    }
}
