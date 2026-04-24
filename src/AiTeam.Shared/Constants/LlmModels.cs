namespace AiTeam.Shared.Constants;

/// <summary>
/// Stage 38：LLM Provider / Model 的白名單常數（Dashboard 下拉清單 + server-side 驗證來源）。
/// Dashboard 依 Provider 動態切換 Model 清單，避免 Provider/Model 混搭組合。
/// 維護慣例：新 model 上線時由 Aria WebFetch 官方文件驗證 → 加字串 → commit + push → 5-10 分鐘內部署生效。
/// 清單按新到舊排列（預設顯示第一個作為 UX hint）。
/// 未來若需更頻繁動態更新 → 升級到 DB 化（FF 二十六）。
/// </summary>
public static class LlmModels
{
    public const string ProviderAnthropic = "Anthropic";
    public const string ProviderGemini    = "Gemini";

    /// <summary>可用 Provider 清單（Dashboard 下拉 + server-side 白名單驗證用）。</summary>
    public static readonly IReadOnlyList<string> AvailableProviders =
        [ProviderAnthropic, ProviderGemini];

    /// <summary>
    /// 依 Provider 返回對應的 Model 清單。Dashboard Model 下拉依 Provider 動態切換（avoid Anthropic+gemini-xxx 混搭）。
    /// 未知 Provider 回空 list（防空指標 + 讓 UI 顯示空下拉提示）。
    /// </summary>
    public static IReadOnlyList<string> GetModelsForProvider(string provider) =>
        provider switch
        {
            ProviderAnthropic => AnthropicModels,
            ProviderGemini    => GeminiModels,
            _                 => []
        };

    public static readonly IReadOnlyList<string> AnthropicModels =
    [
        "claude-opus-4-7",
        "claude-sonnet-4-6",
        "claude-haiku-4-5",
    ];

    // ⚠️ 時效（2026-04-25 Aria WebFetch 官方文件確認）：
    // Gemini 2.5 系列（pro / flash）Google 官方將於 2026-06-17 deprecated，
    // 屆時需評估遷移到 Gemini 3 stable GA 版本（當前 Gemini 3 仍為 -preview，不建議 production）。
    // 這個事件是 FF 二十六 的第一個實際升級 trigger。
    public static readonly IReadOnlyList<string> GeminiModels =
    [
        "gemini-2.5-pro",
        "gemini-2.5-flash",
    ];
}
