using System.Text.Json.Serialization;

namespace AiTeam.Bot.Agents;

/// <summary>
/// Stage 24：Quinn 的結構化測試報告（CLAUDE_Quinn.md 輸出格式）。
/// Stage 78a：搬出 QaAgentService.cs（v4 method 砍後留純 v5.5 IAgentTool / 但 QaReport 仍被 QaCoordinationService 用作 Quinn output deserialize target）。
/// </summary>
public class QaReport
{
    [JsonPropertyName("status")]           public string Status        { get; set; } = "passed";
    [JsonPropertyName("passed_tests")]     public List<string> PassedTests { get; set; } = [];
    [JsonPropertyName("failed_tests")]     public List<string> FailedTests { get; set; } = [];
    [JsonPropertyName("no_test_reason")]   public string? NoTestReason { get; set; }
    [JsonPropertyName("summary")]          public string Summary       { get; set; } = "";
}
