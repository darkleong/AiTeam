namespace AiTeam.Bot.Agents;

/// <summary>
/// Claude Code subprocess 執行介面。
/// Stage 17：提取此介面以支援 MockMode 的 proxy pattern runtime 切換。
/// </summary>
public interface IClaudeCodeService
{
    /// <summary>以完整開發模式執行 Claude Code（寫碼 + build + 修錯）。</summary>
    Task<ClaudeCodeResult> RunAsync(
        string workingDir,
        string prompt,
        string model,
        string anthropicApiKey,
        CancellationToken ct = default);

    /// <summary>以唯讀模式執行 Claude Code（僅 Glob / Grep / Read）。</summary>
    Task<ClaudeCodeResult> RunReadOnlyAsync(
        string workingDir,
        string prompt,
        string model,
        string anthropicApiKey,
        CancellationToken ct = default);

    /// <summary>以 Victoria CEO 模式執行 Claude Code（讀 repo、寫 docs/、git commit）。</summary>
    Task<ClaudeCodeResult> RunVictoriaAsync(
        string workingDir,
        string prompt,
        string model,
        string anthropicApiKey,
        CancellationToken ct = default);

    /// <summary>以 QA 模式執行 Claude Code（開放所有工具，供 Quinn 產生測試）。</summary>
    Task<ClaudeCodeResult> RunQaAsync(
        string workingDir,
        string prompt,
        string model,
        string anthropicApiKey,
        CancellationToken ct = default);

    /// <summary>以 Review 模式執行 Claude Code（Glob / Grep / Read / Bash，供 Vera 程式碼審查）。</summary>
    Task<ClaudeCodeResult> RunReviewAsync(
        string workingDir,
        string prompt,
        string model,
        string anthropicApiKey,
        CancellationToken ct = default);

    /// <summary>
    /// Stage 25a：以持續對話 session 模式執行 Claude Code，支援跨輪次 context 累積（Kick-off 會議使用）。
    /// 第一輪使用 --session-id 建立 session；後續輪使用 --resume {sessionId} 繼續對話。
    /// 不帶 --no-session-persistence，session 資料保留於本機供後續輪使用。
    /// </summary>
    /// <param name="workingDir">repo 本地路徑。</param>
    /// <param name="sessionId">UUID 格式的 session ID（由呼叫端生成並管理）。</param>
    /// <param name="prompt">本輪要傳給 Agent 的訊息。</param>
    /// <param name="model">Claude 模型 ID。</param>
    /// <param name="anthropicApiKey">Anthropic API Key。</param>
    /// <param name="isFirstMessage">true 時建立新 session；false 時 resume 既有 session。</param>
    /// <param name="maxTurns">本輪最大 turn 數（建議 10~15）。</param>
    /// <param name="allowedTools">允許的工具集；null 表示開放全部工具（Cody 使用）。</param>
    /// <param name="ct">CancellationToken。</param>
    Task<ClaudeCodeResult> RunMeetingSessionAsync(
        string workingDir,
        string sessionId,
        string prompt,
        string model,
        string anthropicApiKey,
        bool isFirstMessage,
        int maxTurns,
        string[]? allowedTools = null,
        CancellationToken ct = default);
}
