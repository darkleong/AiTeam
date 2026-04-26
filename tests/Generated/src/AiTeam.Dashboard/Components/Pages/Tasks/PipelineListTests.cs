using System.Reflection;
using AiTeam.Dashboard.Components.Pages.Tasks;
using AiTeam.Dashboard.Helpers;
using FluentAssertions;
using MudBlazor;
using Xunit;

namespace AiTeam.Dashboard.Components.Pages.Tasks.Tests;

public class PipelineListTests
{
    // -----------------------------------------------------------------------
    // 輔助：呼叫私有靜態方法
    // -----------------------------------------------------------------------

    private static string InvokeWorkflowTypeLabel(string? workflowType)
    {
        var method = typeof(PipelineList).GetMethod(
            "WorkflowTypeLabel",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        return (string)method.Invoke(null, new object?[] { workflowType })!;
    }

    private static Color InvokeWorkflowTypeColor(string? workflowType)
    {
        var method = typeof(PipelineList).GetMethod(
            "WorkflowTypeColor",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        return (Color)method.Invoke(null, new object?[] { workflowType })!;
    }

    private static string InvokeExtractPrNumber(string? url)
        => PrNumberHelper.ExtractPrNumber(url);

    // -----------------------------------------------------------------------
    // WorkflowTypeLabel — 已知類型
    // -----------------------------------------------------------------------

    [Fact]
    public void WorkflowTypeLabel_新功能類型_應回傳中文新功能標籤()
    {
        var result = InvokeWorkflowTypeLabel("new_feature");

        result.Should().Be("新功能");
    }

    [Fact]
    public void WorkflowTypeLabel_BugFix類型_應回傳BugFix標籤()
    {
        var result = InvokeWorkflowTypeLabel("bug_fix");

        result.Should().Be("Bug Fix");
    }

    [Fact]
    public void WorkflowTypeLabel_技術改善類型_應回傳技術改善標籤()
    {
        var result = InvokeWorkflowTypeLabel("tech_improvement");

        result.Should().Be("技術改善");
    }

    // -----------------------------------------------------------------------
    // WorkflowTypeLabel — 邊界：未知類型與 null
    // -----------------------------------------------------------------------

    [Fact]
    public void WorkflowTypeLabel_未知類型_應回傳原始字串值()
    {
        var result = InvokeWorkflowTypeLabel("unknown_type");

        result.Should().Be("unknown_type");
    }

    [Fact]
    public void WorkflowTypeLabel_Null_應回傳空字串()
    {
        var result = InvokeWorkflowTypeLabel(null);

        result.Should().Be("");
    }

    [Fact]
    public void WorkflowTypeLabel_空字串_應回傳空字串()
    {
        var result = InvokeWorkflowTypeLabel("");

        result.Should().Be("");
    }

    // -----------------------------------------------------------------------
    // WorkflowTypeColor — 已知類型
    // -----------------------------------------------------------------------

    [Fact]
    public void WorkflowTypeColor_新功能類型_應回傳Primary顏色()
    {
        var result = InvokeWorkflowTypeColor("new_feature");

        result.Should().Be(Color.Primary);
    }

    [Fact]
    public void WorkflowTypeColor_BugFix類型_應回傳Warning顏色()
    {
        var result = InvokeWorkflowTypeColor("bug_fix");

        result.Should().Be(Color.Warning);
    }

    [Fact]
    public void WorkflowTypeColor_技術改善類型_應回傳Secondary顏色()
    {
        var result = InvokeWorkflowTypeColor("tech_improvement");

        result.Should().Be(Color.Secondary);
    }

    // -----------------------------------------------------------------------
    // WorkflowTypeColor — 邊界：未知類型與 null
    // -----------------------------------------------------------------------

    [Fact]
    public void WorkflowTypeColor_未知類型_應回傳Default顏色()
    {
        var result = InvokeWorkflowTypeColor("other");

        result.Should().Be(Color.Default);
    }

    [Fact]
    public void WorkflowTypeColor_Null_應回傳Default顏色()
    {
        var result = InvokeWorkflowTypeColor(null);

        result.Should().Be(Color.Default);
    }

    // -----------------------------------------------------------------------
    // ExtractPrNumber — 正常 URL
    // -----------------------------------------------------------------------

    [Fact]
    public void ExtractPrNumber_標準GitHub_PR_URL_應回傳井號加數字()
    {
        var result = InvokeExtractPrNumber("https://github.com/org/repo/pull/109");

        result.Should().Be("#109");
    }

    [Fact]
    public void ExtractPrNumber_URL結尾含斜線_應正確解析PR編號()
    {
        var result = InvokeExtractPrNumber("https://github.com/org/repo/pull/42/");

        result.Should().Be("#42");
    }

    [Fact]
    public void ExtractPrNumber_單一數字路徑_應回傳井號加數字()
    {
        var result = InvokeExtractPrNumber("https://example.com/pulls/7");

        result.Should().Be("#7");
    }

    // -----------------------------------------------------------------------
    // ExtractPrNumber — 邊界：無效輸入
    // -----------------------------------------------------------------------

    [Fact]
    public void ExtractPrNumber_Null_應回傳PR文字()
    {
        var result = InvokeExtractPrNumber(null);

        result.Should().Be("PR");
    }

    [Fact]
    public void ExtractPrNumber_空字串_應回傳PR文字()
    {
        var result = InvokeExtractPrNumber("");

        result.Should().Be("PR");
    }

    [Fact]
    public void ExtractPrNumber_URL結尾為非數字字串_應回傳PR文字()
    {
        var result = InvokeExtractPrNumber("https://github.com/org/repo/pull/abc");

        result.Should().Be("PR");
    }

    [Fact]
    public void ExtractPrNumber_URL結尾為純斜線_應回傳PR文字()
    {
        // TrimEnd('/') 後最後一段為 "pull"，非數字
        var result = InvokeExtractPrNumber("https://github.com/org/repo/pull/");

        result.Should().Be("PR");
    }
}
