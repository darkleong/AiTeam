using System.Reflection;
using AiTeam.Bot.Agents;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AiTeam.Bot.Tests.Agents;

/// <summary>
/// Stage 82 子項 1：ParseJsonOutput 升級 stream-json NDJSON parsing — accumulate type=assistant.message.content[].text，
/// 在 type=result row 的 result 欄位空時 fallback accumulated text（修 Quinn outputLen=0 根因）。
///
/// 紀律對齊：
/// - T1 normal text-only turn → result 非空 → 對齊既有行為 0 regression
/// - T2 tool-heavy 場景 result 空 → fallback accumulated（修根因）
/// - T6 對齊 Aria gate1 補強 — TryParseUsage 對 stream-json result.usage nested schema 對齊 verify
///   （input_tokens / output_tokens / cache_creation_input_tokens / cache_read_input_tokens + top-level total_cost_usd）
/// </summary>
public class ClaudeCodeServiceParseJsonOutputTests
{
    private static (bool Success, string Output, object? Usage) InvokeParseJsonOutput(string rawOutput, int exitCode)
    {
        var service = new ClaudeCodeService(NullLogger<ClaudeCodeService>.Instance);
        var method = typeof(ClaudeCodeService).GetMethod(
            "ParseJsonOutput",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);
        var result = method!.Invoke(service, new object[] { rawOutput, exitCode })!;
        var type = result.GetType();
        var success = (bool)type.GetField("Item1")!.GetValue(result)!;
        var output  = (string)type.GetField("Item2")!.GetValue(result)!;
        var usage   = type.GetField("Item3")!.GetValue(result);
        return (success, output, usage);
    }

    [Fact]
    public void T1_NormalTextOnlyTurn_UsesResultField()
    {
        // NDJSON 含 1 assistant text + 1 result(result="hello") → output="hello"（既有行為 0 regression）
        var ndjson = string.Join('\n', new[]
        {
            """{"type":"system","subtype":"init","session_id":"abc"}""",
            """{"type":"assistant","message":{"role":"assistant","content":[{"type":"text","text":"hello"}]}}""",
            """{"type":"result","subtype":"success","is_error":false,"result":"hello","total_cost_usd":0.001,"usage":{"input_tokens":1,"output_tokens":1,"cache_creation_input_tokens":0,"cache_read_input_tokens":0}}""",
        });

        var (success, output, usage) = InvokeParseJsonOutput(ndjson, 0);

        Assert.True(success);
        Assert.Equal("hello", output);
        Assert.NotNull(usage);
    }

    [Fact]
    public void T2_ToolHeavyEmptyResult_FallbackAccumulated()
    {
        // Quinn tool-heavy 場景：多 assistant text content blocks（Read tests + Bash dotnet test 描述）+
        // final result row 的 result 欄位空（CLI 最後 turn 是 tool_use 不是 text）→ fallback accumulated text（修根因）
        var ndjson = string.Join('\n', new[]
        {
            """{"type":"system","subtype":"init","session_id":"abc"}""",
            """{"type":"assistant","message":{"role":"assistant","content":[{"type":"text","text":"我先 Read 既有 test 檔。"}]}}""",
            """{"type":"assistant","message":{"role":"assistant","content":[{"type":"text","text":"接下來 dotnet test 跑一次。"}]}}""",
            """{"type":"result","subtype":"success","is_error":false,"result":"","total_cost_usd":1.04,"usage":{"input_tokens":22,"output_tokens":27016,"cache_creation_input_tokens":0,"cache_read_input_tokens":0}}""",
        });

        var (success, output, usage) = InvokeParseJsonOutput(ndjson, 0);

        Assert.True(success);
        Assert.Contains("Read 既有 test 檔", output);
        Assert.Contains("dotnet test", output);
        Assert.NotNull(usage);
    }

    [Fact]
    public void T3_ResultIsError_ReturnsFalseSuccess()
    {
        var ndjson = string.Join('\n', new[]
        {
            """{"type":"assistant","message":{"role":"assistant","content":[{"type":"text","text":"partial work"}]}}""",
            """{"type":"result","subtype":"error_during_execution","is_error":true,"result":"failure summary","total_cost_usd":0}""",
        });

        var (success, output, _) = InvokeParseJsonOutput(ndjson, 0);

        Assert.False(success);
        Assert.Equal("failure summary", output);
    }

    [Fact]
    public void T4_EmptyOutput_ReturnsNoOutput()
    {
        var (success, output, usage) = InvokeParseJsonOutput("", 0);

        Assert.True(success);
        Assert.Equal("（無輸出）", output);
        Assert.Null(usage);
    }

    [Fact]
    public void T5_MultilineAssistantTextBlocks_AccumulatedJoined()
    {
        // 多 assistant rows 多 text blocks → 全部 \n 串接
        var ndjson = string.Join('\n', new[]
        {
            """{"type":"assistant","message":{"role":"assistant","content":[{"type":"text","text":"第一段"},{"type":"text","text":"第二段"}]}}""",
            """{"type":"assistant","message":{"role":"assistant","content":[{"type":"text","text":"第三段"}]}}""",
            """{"type":"result","subtype":"success","is_error":false,"result":"","total_cost_usd":0}""",
        });

        var (success, output, _) = InvokeParseJsonOutput(ndjson, 0);

        Assert.True(success);
        Assert.Equal("第一段\n第二段\n第三段", output);
    }

    [Fact]
    public void T6_StreamJsonResultUsageNested_ParsedCorrectly()
    {
        // Aria gate1 補強 — TryParseUsage 對 stream-json result row usage nested + top-level total_cost_usd schema 對齊 verify
        // schema 對齊真實 spike output（usage{input_tokens,output_tokens,cache_creation_input_tokens,cache_read_input_tokens} + top-level total_cost_usd）
        var ndjson = string.Join('\n', new[]
        {
            """{"type":"assistant","message":{"role":"assistant","content":[{"type":"text","text":"x"}]}}""",
            """{"type":"result","subtype":"success","is_error":false,"result":"final","total_cost_usd":0.128589,"usage":{"input_tokens":3,"output_tokens":8,"cache_creation_input_tokens":34256,"cache_read_input_tokens":0}}""",
        });

        var (success, output, usage) = InvokeParseJsonOutput(ndjson, 0);

        Assert.True(success);
        Assert.Equal("final", output);
        Assert.NotNull(usage);

        // usage 是 TokenUsage record（internal）— 透過 reflection 取欄位 verify
        var usageType = usage!.GetType();
        var input  = (int)usageType.GetProperty("InputTokens")!.GetValue(usage)!;
        var output_ = (int)usageType.GetProperty("OutputTokens")!.GetValue(usage)!;
        var cache  = (int)usageType.GetProperty("CacheCreationTokens")!.GetValue(usage)!;
        var read   = (int)usageType.GetProperty("CacheReadTokens")!.GetValue(usage)!;
        var cost   = (decimal?)usageType.GetProperty("TotalCostUsd")!.GetValue(usage);

        Assert.Equal(3, input);
        Assert.Equal(8, output_);
        Assert.Equal(34256, cache);
        Assert.Equal(0, read);
        Assert.Equal(0.128589m, cost);
    }
}
