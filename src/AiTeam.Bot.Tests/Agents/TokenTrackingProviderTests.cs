using System.Reflection;
using AiTeam.Bot.Agents;
using Xunit;

namespace AiTeam.Bot.Tests.Agents;

/// <summary>
/// Stage 82 子項 2：TokenTrackingProvider AsyncLocal scope — 驗 PetraSessionAmbient 行為。
///
/// 紀律對齊：
/// - 構造完整 TokenTrackingProvider 依賴繁多（10+ dep）→ 此 test 只 cover AsyncLocal 行為（單一職責）
/// - 真實 TokenLog.PetraSessionId 透傳留 production SQL 驗證（Trial_v26）+ Stage 82 結案 self-verify Layer 2
/// - 巢狀 scope 對應實務 — 多 Petra session 並行（v5 future）/ 確保 Dispose 恢復前值避免洩漏
/// </summary>
public class TokenTrackingProviderTests
{
    private static AsyncLocal<Guid?> Ambient() =>
        (AsyncLocal<Guid?>)typeof(TokenTrackingProvider)
            .GetField("PetraSessionAmbient", BindingFlags.NonPublic | BindingFlags.Static)!
            .GetValue(null)!;

    [Fact]
    public void T1_NoScope_PetraSessionIdNull()
    {
        // 對齊既有 caller 透明 — 不包 scope 時 ambient 應為 null
        Ambient().Value = null;   // reset 確保前 test 不污染
        Assert.Null(Ambient().Value);
    }

    [Fact]
    public void T2_WithScope_PetraSessionIdSet()
    {
        Ambient().Value = null;
        var sid = Guid.NewGuid();

        using (TokenTrackingProvider.BeginPetraSessionScope(sid))
        {
            Assert.Equal(sid, Ambient().Value);
        }

        // Dispose 後 ambient 恢復 null
        Assert.Null(Ambient().Value);
    }

    [Fact]
    public void T3_NestedScope_InnerOverridesOuter()
    {
        Ambient().Value = null;
        var outer = Guid.NewGuid();
        var inner = Guid.NewGuid();

        using (TokenTrackingProvider.BeginPetraSessionScope(outer))
        {
            Assert.Equal(outer, Ambient().Value);

            using (TokenTrackingProvider.BeginPetraSessionScope(inner))
            {
                Assert.Equal(inner, Ambient().Value);
            }

            // inner Dispose 後恢復 outer（不洩漏到 outer scope）
            Assert.Equal(outer, Ambient().Value);
        }

        // outer Dispose 後恢復 null
        Assert.Null(Ambient().Value);
    }
}
