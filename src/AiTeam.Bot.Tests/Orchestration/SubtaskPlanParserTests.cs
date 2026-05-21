using AiTeam.Bot.Orchestration.Petra;
using Xunit;

namespace AiTeam.Bot.Tests.Orchestration;

/// <summary>
/// Stage 82 子項 3：SubtaskPlanParser 對 LLM 健談行為防呆（先 strip code fence → strip preamble/postamble → Deserialize）。
/// 對齊「修根因 > 補丁」紀律 — Petra system prompt 純 JSON 紀律維持 + 底層 parser robust 防呆雙保險。
/// </summary>
public class SubtaskPlanParserTests
{
    [Fact]
    public void T1_PureJsonObject_ParsesOk()
    {
        var raw = """{"subtasks":[{"id":1,"skill":"code_implementation","description":"do X"}],"dependencies":[]}""";

        var ok = SubtaskPlanParser.TryParse(raw, out var plan, out var error);

        Assert.True(ok, $"expect success, got error={error}");
        Assert.Null(error);
        Assert.Single(plan.Subtasks);
        Assert.Equal("code_implementation", plan.Subtasks[0].SkillName);
        Assert.Empty(plan.Dependencies);
    }

    [Fact]
    public void T2_MarkdownCodeFenceJson_ParsesOk()
    {
        var raw = """
            ```json
            {"subtasks":[{"id":1,"skill":"qa_testing","description":"test Y"}]}
            ```
            """;

        var ok = SubtaskPlanParser.TryParse(raw, out var plan, out var error);

        Assert.True(ok, $"expect success, got error={error}");
        Assert.Single(plan.Subtasks);
        Assert.Equal("qa_testing", plan.Subtasks[0].SkillName);
    }

    [Fact]
    public void T3_ConversationalPreamble_StripsAndParses()
    {
        // Stage 82 子項 3 新加：對 Anthropic Haiku / 未來 Provider 健談行為防呆
        var raw = """好的，這是我的計劃：{"subtasks":[{"id":1,"skill":"code_review","description":"review Z"}]}""";

        var ok = SubtaskPlanParser.TryParse(raw, out var plan, out var error);

        Assert.True(ok, $"expect success, got error={error}");
        Assert.Single(plan.Subtasks);
        Assert.Equal("code_review", plan.Subtasks[0].SkillName);
    }

    [Fact]
    public void T4_PreambleAndCodeFence_DoubleStripsAndParses()
    {
        // Stage 82 子項 3 新加：對話前綴 + markdown fence 雙包裹 — 兩層 strip 紀律
        var raw = """
            好的，這是計劃：
            ```json
            {"subtasks":[{"id":1,"skill":"documentation","description":"doc W"}]}
            ```
            希望有幫助！
            """;

        var ok = SubtaskPlanParser.TryParse(raw, out var plan, out var error);

        Assert.True(ok, $"expect success, got error={error}");
        Assert.Single(plan.Subtasks);
        Assert.Equal("documentation", plan.Subtasks[0].SkillName);
    }

    [Fact]
    public void T5_NoJsonObject_ReturnsExplicitError()
    {
        var raw = "這個任務有點難講，我覺得需要再想想。";

        var ok = SubtaskPlanParser.TryParse(raw, out var plan, out var error);

        Assert.False(ok);
        Assert.NotNull(error);
        Assert.Equal(SubtaskPlan.Empty, plan);
    }
}
