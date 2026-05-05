using AiTeam.Bot.Workflows.Design;
using Xunit;

namespace AiTeam.Bot.Tests;

/// <summary>
/// Stage 56：FF 四十二 修法驗證 — TryParseDesignIssues line-iteration + try-deserialize pattern
/// 必須能處理 [MOCK] 前綴 / 純 multi-line array / 含字串 [example] 嵌套 三 case。
/// </summary>
public class DesignPromptsTests
{
    [Fact]
    public void TryParseDesignIssues_T1_MockPrefix_MultiLineArray_ParsesCorrectly()
    {
        // case ①：[MOCK] 前綴 + 後接 multi-line array — 既有 IndexOf 邊界誤判 case
        var input =
            "[MOCK] 開頭文字說明\n" +
            "[\n" +
            "  { \"title\": \"a\", \"body\": \"b\", \"labels\": [\"feature\"] }\n" +
            "]";

        var result = DesignPrompts.TryParseDesignIssues(input);

        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal("a", result[0].Title);
        Assert.Equal("b", result[0].Body);
        Assert.Single(result[0].Labels);
        Assert.Equal("feature", result[0].Labels[0]);
    }

    [Fact]
    public void TryParseDesignIssues_T2_PureMultiLineArray_ParsesCorrectly()
    {
        // case ②：純 multi-line array — regression 確認既有 case 不破壞
        var input =
            "[\n" +
            "  { \"title\": \"x\", \"body\": \"y\", \"labels\": [\"P1\"] },\n" +
            "  { \"title\": \"u\", \"body\": \"v\", \"labels\": [] }\n" +
            "]";

        var result = DesignPrompts.TryParseDesignIssues(input);

        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
        Assert.Equal("x", result[0].Title);
        Assert.Equal("u", result[1].Title);
    }

    [Fact]
    public void TryParseDesignIssues_T3_NestedExampleString_SkipsToRealArray()
    {
        // case ③：含字串 [example] 嵌套（在前綴行內）— 跳過字串邊界，正確 parse 真 array
        var input =
            "前綴段落 [example] text 在這\n" +
            "[\n" +
            "  { \"title\": \"real\", \"body\": \"actual\", \"labels\": [] }\n" +
            "]";

        var result = DesignPrompts.TryParseDesignIssues(input);

        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal("real", result[0].Title);
    }

    [Fact]
    public void TryParseDesignIssues_NoArray_ReturnsNull()
    {
        // 邊界：完全沒有 array → 回 null（保守失敗）
        var input = "純文字回應，沒有任何 JSON";
        var result = DesignPrompts.TryParseDesignIssues(input);
        Assert.Null(result);
    }
}
