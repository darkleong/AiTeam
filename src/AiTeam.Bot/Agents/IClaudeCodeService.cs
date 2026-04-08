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
}
