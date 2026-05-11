using AiTeam.Bot.Agents;
using AiTeam.Bot.Orchestration.Petra.Spike;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace AiTeam.Bot.Tests.Spike;

// Stage 63A 動態決策 API spike — 3 場景驗證 Petra LLM 動態決策真實 fire 三 trigger。
// 預設「無 AITEAM_GEMINI_KEY env 即 silently pass」— 避免污染 131 baseline 也避免 CI 跑 Gemini 真 cost。
// 手動執行：set AITEAM_GEMINI_KEY=... && dotnet test --filter "FullyQualifiedName~PetraSpikePrototypeTests"
public class PetraSpikePrototypeTests(ITestOutputHelper output)
{
    private static string? ApiKey => Environment.GetEnvironmentVariable("AITEAM_GEMINI_KEY");

    [Fact]
    [Trait("Category", "Spike")]
    public Task Scenario1_TinyTypoFix_TriggersOneOnOne() =>
        RunScenarioAsync("修 README typo 1 行", "1-on-1");

    [Fact]
    [Trait("Category", "Spike")]
    public Task Scenario2_CrossComponentFix_TriggersDesign() =>
        RunScenarioAsync("Dashboard 錯誤處理打磨跨 5 元件含 MudBlazor ISnackbar + Error toast", "Design");

    [Fact]
    [Trait("Category", "Spike")]
    public Task Scenario3_ArchitectureRefactor_TriggersKickoff() =>
        RunScenarioAsync("Token 守門架構級重構 — Provider/Model SoT 切 DB + 跨 3 layer + 整批 Migration", "Kickoff");

    private async Task RunScenarioAsync(string scenarioInput, string expectedTrigger)
    {
        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            output.WriteLine($"[Spike {expectedTrigger}] Skipped — 無 AITEAM_GEMINI_KEY env");
            return;
        }

        using var http = new HttpClient { BaseAddress = new Uri("https://generativelanguage.googleapis.com/v1beta/") };
        var gemini = new GeminiProvider(http, ApiKey, model: "gemini-2.5-flash", logger: NullLogger<GeminiProvider>.Instance);

        var log = await PetraSpikePrototype.RunScenarioAsync(scenarioInput, gemini);

        Assert.NotEmpty(log.PetraDecisions);
        Assert.NotEmpty(log.WorkerCalls);

        output.WriteLine($"[Spike {expectedTrigger}] Scenario={log.Scenario}");
        output.WriteLine($"  Petra decisions: {string.Join(" → ", log.PetraDecisions)}");
        output.WriteLine($"  Worker calls: {log.WorkerCalls.Count}");
        foreach (var call in log.WorkerCalls) output.WriteLine($"    - {call}");
    }
}
