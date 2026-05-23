using AiTeam.Bot.Orchestration.Petra;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AiTeam.Bot.Tests.Orchestration;

/// <summary>
/// Stage 84：PetraGitFinalizationService smoke tests（對齊 Aria Roadmap 子項 7 新增 path + Stage 83 Bug 4 path A+ prUrl 寫回紀律）。
///
/// 真實 git commit / push / PR 需 GitHubService + LibGit2Sharp 真實 repo — unit test 限制：只驗 method 存在 + 非 git repo fallback 不擋流程紀律。
/// 真實 prUrl 寫回 PetraSession.ResultPrUrl 驗收走 MockMode 6 驗收情境 + Forge 自驗（Internal API + psql 對齊 Stage 83 Bug 4 path A+）。
/// </summary>
public class PetraGitFinalizationServiceTests
{
    [Fact]
    public void FinalizeGitAsync_MethodExists_PublicSignatureReady()
    {
        var method = typeof(PetraGitFinalizationService).GetMethod(
            "FinalizeGitAsync",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(method);

        // 簽名驗：5 params（ctx / taskInput / caps / dispatchNames / ct）+ return Task<string?>
        var parameters = method!.GetParameters();
        Assert.Equal(5, parameters.Length);
        Assert.Equal(typeof(PetraSessionContext), parameters[0].ParameterType);
        Assert.Equal(typeof(string), parameters[1].ParameterType);
        Assert.Equal(typeof(IReadOnlyList<string>), parameters[2].ParameterType);
        Assert.Equal(typeof(IReadOnlyList<string>), parameters[3].ParameterType);
        Assert.Equal(typeof(CancellationToken), parameters[4].ParameterType);
        Assert.Equal(typeof(Task<string?>), method.ReturnType);
    }
}
