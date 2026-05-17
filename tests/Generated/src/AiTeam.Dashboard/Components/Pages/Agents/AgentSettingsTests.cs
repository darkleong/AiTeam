// 測試標的：AiTeam.Dashboard.Components.Pages.Agents.AgentSettings
// 驗證：grep -r 'class AgentSettings' src/AiTeam.Dashboard/ → 命中 AgentSettings.razor.cs:12

using System.Reflection;
using AiTeam.Dashboard.Components.Pages.Agents;
using AiTeam.Shared.Constants;
using AiTeam.Shared.Dtos;
using FluentAssertions;
using MudBlazor;
using NSubstitute;
using Xunit;

namespace AiTeam.Dashboard.Components.Pages.Agents.Tests;

public class AgentSettingsTests
{
    private static AgentSettings CreateInstanceWithSnackbar(ISnackbar snackbar)
    {
        var instance = new AgentSettings();
        typeof(AgentSettings)
            .GetProperty("Snackbar", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(instance, snackbar);
        return instance;
    }

    private static AgentSettings CreateBareInstance()
        => new AgentSettings();

    private static void SetField(AgentSettings instance, string fieldName, object value)
        => typeof(AgentSettings)
            .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(instance, value);

    private static T? GetField<T>(AgentSettings instance, string fieldName)
        => (T?)typeof(AgentSettings)
            .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(instance);

    private static async Task InvokeAsync(AgentSettings instance, string methodName, params object?[] args)
    {
        var method = typeof(AgentSettings)
            .GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance)!;
        var result = method.Invoke(instance, args);
        if (result is Task task) await task;
    }

    // ── ToggleIsActiveAsync：guard clause ────────────────────────────────

    [Fact]
    public async Task ToggleIsActiveAsync_已在切換中_應直接返回不拋出例外()
    {
        var instance = CreateBareInstance();
        SetField(instance, "_isTogglingActive", true);
        var agent = new AgentConfigDto { Id = Guid.NewGuid(), Name = "TestAgent" };

        // AgentService 為 null，若未提前返回會拋 NRE
        var act = async () => await InvokeAsync(instance, "ToggleIsActiveAsync", agent, true);

        await act.Should().NotThrowAsync("guard clause 應在存取服務前返回");
    }

    [Fact]
    public async Task ToggleIsActiveAsync_已在切換中_切換旗標應維持True()
    {
        var instance = CreateBareInstance();
        SetField(instance, "_isTogglingActive", true);
        var agent = new AgentConfigDto { Id = Guid.NewGuid(), Name = "TestAgent" };

        await InvokeAsync(instance, "ToggleIsActiveAsync", agent, true);

        GetField<bool>(instance, "_isTogglingActive").Should().BeTrue("guard clause 提早返回後旗標應未被修改");
    }

    // ── SaveTokenLimitsAsync：guard clause ───────────────────────────────

    [Fact]
    public async Task SaveTokenLimitsAsync_已在儲存LLM設定中_應直接返回不拋出例外()
    {
        var instance = CreateBareInstance();
        SetField(instance, "_isSavingLlm", true);
        var agent = new AgentConfigDto { Id = Guid.NewGuid(), Name = "TestAgent" };

        var act = async () => await InvokeAsync(instance, "SaveTokenLimitsAsync", agent);

        await act.Should().NotThrowAsync("guard clause 應在存取服務前返回");
    }

    [Fact]
    public async Task SaveTokenLimitsAsync_已在儲存LLM設定中_旗標應維持True()
    {
        var instance = CreateBareInstance();
        SetField(instance, "_isSavingLlm", true);
        var agent = new AgentConfigDto { Id = Guid.NewGuid(), Name = "TestAgent" };

        await InvokeAsync(instance, "SaveTokenLimitsAsync", agent);

        GetField<bool>(instance, "_isSavingLlm").Should().BeTrue("guard clause 提早返回後旗標應未被 finally 重置");
    }

    // ── SaveTrustLevelAsync：_formError 管理 ─────────────────────────────

    [Fact]
    public async Task SaveTrustLevelAsync_例外時_formError應包含錯誤說明()
    {
        var snackbar = Substitute.For<ISnackbar>();
        var instance = CreateInstanceWithSnackbar(snackbar);
        var agent = new AgentConfigDto { Id = Guid.NewGuid(), Name = "TestAgent" };
        // _trustLevels 為空 dict → KeyNotFoundException 在 try 中被 catch 捕捉

        await InvokeAsync(instance, "SaveTrustLevelAsync", agent);

        var formError = GetField<string?>(instance, "_formError");
        formError.Should().NotBeNull("例外應觸發 _formError 設定");
        formError.Should().StartWith("信任等級儲存失敗：", "訊息應帶有識別前綴");
    }

    [Fact]
    public async Task SaveTrustLevelAsync_例外時_Snackbar應被呼叫()
    {
        var snackbar = Substitute.For<ISnackbar>();
        var instance = CreateInstanceWithSnackbar(snackbar);
        var agent = new AgentConfigDto { Id = Guid.NewGuid(), Name = "TestAgent" };

        await InvokeAsync(instance, "SaveTrustLevelAsync", agent);

        snackbar.Received(1).Add(
            Arg.Is<string>(s => s.StartsWith("信任等級儲存失敗：")),
            Severity.Error,
            Arg.Any<Action<SnackbarOptions>?>());
    }

    [Fact]
    public async Task SaveTrustLevelAsync_例外時_isSaving應重置為False()
    {
        var snackbar = Substitute.For<ISnackbar>();
        var instance = CreateInstanceWithSnackbar(snackbar);
        var agent = new AgentConfigDto { Id = Guid.NewGuid(), Name = "TestAgent" };

        await InvokeAsync(instance, "SaveTrustLevelAsync", agent);

        GetField<bool>(instance, "_isSaving").Should().BeFalse("finally 區塊應重置 _isSaving");
    }

    [Fact]
    public async Task SaveTrustLevelAsync_入口應清除舊formError()
    {
        var snackbar = Substitute.For<ISnackbar>();
        var instance = CreateInstanceWithSnackbar(snackbar);
        SetField(instance, "_formError", "舊錯誤訊息");
        var agent = new AgentConfigDto { Id = Guid.NewGuid(), Name = "TestAgent" };

        await InvokeAsync(instance, "SaveTrustLevelAsync", agent);

        // 入口 _formError = null 後被 catch 設定新值，舊訊息應消失
        GetField<string?>(instance, "_formError").Should().NotBe("舊錯誤訊息", "入口 _formError = null 應清除舊訊息");
    }

    // ── SaveTokenLimitsAsync：_formError 管理（exception path）────────────

    [Fact]
    public async Task SaveTokenLimitsAsync_例外時_formError應被設定()
    {
        var snackbar = Substitute.For<ISnackbar>();
        var instance = CreateInstanceWithSnackbar(snackbar);
        var agent = new AgentConfigDto { Id = Guid.NewGuid(), Name = "TestAgent" };
        // AgentService 為 null → NullReferenceException 在 try 中被 catch 捕捉

        await InvokeAsync(instance, "SaveTokenLimitsAsync", agent);

        GetField<string?>(instance, "_formError").Should().NotBeNull("例外應觸發 _formError 設定");
    }

    [Fact]
    public async Task SaveTokenLimitsAsync_例外時_Snackbar應被呼叫()
    {
        var snackbar = Substitute.For<ISnackbar>();
        var instance = CreateInstanceWithSnackbar(snackbar);
        var agent = new AgentConfigDto { Id = Guid.NewGuid(), Name = "TestAgent" };

        await InvokeAsync(instance, "SaveTokenLimitsAsync", agent);

        snackbar.Received(1).Add(
            Arg.Any<string>(),
            Severity.Error,
            Arg.Any<Action<SnackbarOptions>?>());
    }

    [Fact]
    public async Task SaveTokenLimitsAsync_例外時_isSavingLlm應重置為False()
    {
        var snackbar = Substitute.For<ISnackbar>();
        var instance = CreateInstanceWithSnackbar(snackbar);
        var agent = new AgentConfigDto { Id = Guid.NewGuid(), Name = "TestAgent" };

        await InvokeAsync(instance, "SaveTokenLimitsAsync", agent);

        GetField<bool>(instance, "_isSavingLlm").Should().BeFalse("finally 區塊應重置 _isSavingLlm");
    }

    [Fact]
    public async Task SaveTokenLimitsAsync_入口應清除舊formError()
    {
        var snackbar = Substitute.For<ISnackbar>();
        var instance = CreateInstanceWithSnackbar(snackbar);
        SetField(instance, "_formError", "舊錯誤訊息");
        var agent = new AgentConfigDto { Id = Guid.NewGuid(), Name = "TestAgent" };

        await InvokeAsync(instance, "SaveTokenLimitsAsync", agent);

        GetField<string?>(instance, "_formError").Should().NotBe("舊錯誤訊息", "入口 _formError = null 應清除舊訊息");
    }

    // ── OnProviderChangedAsync：舊 Model 不在新 Provider 清單 ─────────────

    [Fact]
    public async Task OnProviderChangedAsync_切換至Anthropic但舊Model為Gemini_應清空Model()
    {
        var snackbar = Substitute.For<ISnackbar>();
        var instance = CreateInstanceWithSnackbar(snackbar);
        var agent = new AgentConfigDto
        {
            Provider = LlmModels.ProviderGemini,
            Model    = "gemini-2.5-pro"
        };

        await InvokeAsync(instance, "OnProviderChangedAsync", agent, LlmModels.ProviderAnthropic);

        agent.Model.Should().BeNull("gemini-2.5-pro 不在 Anthropic 清單中，應被清空");
    }

    [Fact]
    public async Task OnProviderChangedAsync_切換至未知Provider_應清空Model()
    {
        var snackbar = Substitute.For<ISnackbar>();
        var instance = CreateInstanceWithSnackbar(snackbar);
        var agent = new AgentConfigDto
        {
            Provider = LlmModels.ProviderAnthropic,
            Model    = "claude-sonnet-4-6"
        };

        await InvokeAsync(instance, "OnProviderChangedAsync", agent, "UnknownProvider");

        agent.Model.Should().BeNull("未知 Provider 對應空 Model 清單，任何舊 Model 均應被清空");
    }

    // ── OnModelChangedAsync：空 Provider 提早返回 ────────────────────────

    [Fact]
    public async Task OnModelChangedAsync_Provider為空_應設定Model後提早返回不呼叫服務()
    {
        var instance = CreateBareInstance();
        var agent = new AgentConfigDto { Provider = null, Model = null };

        // AgentService 為 null；若未提早返回會拋 NRE
        var act = async () => await InvokeAsync(instance, "OnModelChangedAsync", agent, "claude-sonnet-4-6");

        await act.Should().NotThrowAsync("Provider 為空應提早返回");
        agent.Model.Should().Be("claude-sonnet-4-6", "Model 賦值在 guard check 之前發生");
    }

    // ── SaveProviderModelAsync：Provider / Model 為空提早返回 ─────────────

    [Fact]
    public async Task SaveProviderModelAsync_Provider為空_應直接返回不呼叫服務()
    {
        var instance = CreateBareInstance();
        var agent = new AgentConfigDto { Provider = "", Model = "claude-sonnet-4-6" };

        var act = async () => await InvokeAsync(instance, "SaveProviderModelAsync", agent);

        await act.Should().NotThrowAsync("Provider 為空應提早返回");
    }

    [Fact]
    public async Task SaveProviderModelAsync_Model為空_應直接返回不呼叫服務()
    {
        var instance = CreateBareInstance();
        var agent = new AgentConfigDto { Provider = LlmModels.ProviderAnthropic, Model = null };

        var act = async () => await InvokeAsync(instance, "SaveProviderModelAsync", agent);

        await act.Should().NotThrowAsync("Model 為空應提早返回");
    }
}
