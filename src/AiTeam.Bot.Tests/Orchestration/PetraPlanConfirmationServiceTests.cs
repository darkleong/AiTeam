using AiTeam.Bot.Orchestration.Petra;
using Xunit;

namespace AiTeam.Bot.Tests.Orchestration;

/// <summary>
/// Stage 84：PetraPlanConfirmationService smoke tests（對齊 Aria Roadmap 子項 7 新增 path）。
///
/// 真實 4-way decision pattern resume flow 驗收走 MockMode 6 驗收情境（plan_confirm 卡 approve / edit / respond / reject）+ Forge 自驗（Internal API + psql）。
/// unit test 限制：只驗 4 public method 存在 + 簽名對齊（防回歸刪除）/ 行為驗證 cover by MockMode。
/// </summary>
public class PetraPlanConfirmationServiceTests
{
    [Fact]
    public void ResumeFromPlanConfirmationAsync_MethodExists_PublicSignatureReady()
    {
        var method = typeof(PetraPlanConfirmationService).GetMethod(
            "ResumeFromPlanConfirmationAsync",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(method);

        // 簽名驗：4 params（sessionId / decision / contextOverride / ct）+ return Task<PetraOrchestratorResult>
        var parameters = method!.GetParameters();
        Assert.Equal(4, parameters.Length);
        Assert.Equal(typeof(Guid), parameters[0].ParameterType);
        Assert.Equal(typeof(string), parameters[1].ParameterType);
        Assert.Equal(typeof(string), parameters[2].ParameterType);
        Assert.Equal(typeof(CancellationToken), parameters[3].ParameterType);
        Assert.Equal(typeof(Task<PetraOrchestratorResult>), method.ReturnType);
    }
}
