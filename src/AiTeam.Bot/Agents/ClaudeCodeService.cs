using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace AiTeam.Bot.Agents;

/// <summary>
/// 封裝 Claude Code CLI subprocess 呼叫，供 Dev Agent 使用。
/// 透過 `claude -p` 非互動模式在指定 repo 目錄內自主探索、寫碼、build 驗證。
/// </summary>
public class ClaudeCodeService(ILogger<ClaudeCodeService> logger)
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(30);

    /// <summary>
    /// 在指定的 repo 工作目錄內執行 Claude Code 完成開發任務。
    /// Claude Code 負責：探索 codebase → 實作變更 → dotnet restore → dotnet build → 修錯直到通過。
    /// 不會 commit 或 push（由呼叫端的 GitHubService 負責）。
    /// </summary>
    /// <param name="workingDir">repo 本地路徑（已 clone 並 checkout 到正確 branch）</param>
    /// <param name="prompt">任務描述 prompt</param>
    /// <param name="model">Claude 模型 ID（來自 appsettings，與其他 Agent 一致）</param>
    /// <param name="anthropicApiKey">Anthropic API Key（注入至子進程環境變數）</param>
    /// <param name="ct">CancellationToken</param>
    /// <returns>執行結果</returns>
    public async Task<ClaudeCodeResult> RunAsync(
        string workingDir,
        string prompt,
        string model,
        string anthropicApiKey,
        CancellationToken ct = default)
    {
        // 1. 確保 git config 已設定（容器內可能缺少 user.name/email）
        await ConfigureGitAsync(workingDir, ct);

        // 2. 組建 claude CLI 參數
        var args = BuildArgs(prompt, model);

        logger.LogInformation(
            "ClaudeCodeService 啟動 Claude Code subprocess（dir={Dir}，model={Model}）",
            workingDir, model);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(DefaultTimeout);

        var psi = new ProcessStartInfo
        {
            FileName               = "claude",
            Arguments              = args,
            WorkingDirectory       = workingDir,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
            CreateNoWindow         = true,
        };

        // 注入 API Key（不寫死到 docker-compose，避免暴露在 log）
        psi.Environment["ANTHROPIC_API_KEY"] = anthropicApiKey;

        using var process = new Process { StartInfo = psi };

        var stdoutBuilder = new StringBuilder();
        var stderrBuilder  = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null) stdoutBuilder.AppendLine(e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null) stderrBuilder.AppendLine(e.Data);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // 逾時（而非外部 cancel）
            try { process.Kill(entireProcessTree: true); } catch { /* ignore */ }
            throw new TimeoutException(
                $"Claude Code subprocess 超過 {DefaultTimeout.TotalMinutes} 分鐘逾時");
        }

        var stdout = stdoutBuilder.ToString();
        var stderr  = stderrBuilder.ToString();

        if (!string.IsNullOrWhiteSpace(stderr))
            logger.LogWarning("Claude Code stderr：{Stderr}", stderr);

        var exitCode = process.ExitCode;
        var (success, output) = ParseJsonOutput(stdout, exitCode);

        logger.LogInformation(
            "Claude Code subprocess 結束（exitCode={Code}，success={Success}）",
            exitCode, success);

        return new ClaudeCodeResult(success, output, exitCode, stdout);
    }

    // ────────────── Private ──────────────

    private static string BuildArgs(string prompt, string model)
    {
        // prompt 中可能含有引號，使用 stdin 更安全；但 -p 模式也接受 argument
        // 使用 --input-format text 讓 prompt 透過 argument 傳遞
        // prompt 不含換行，以雙引號包覆（subprocess 中 argument 已由 OS 負責 escaping）
        var escapedPrompt = prompt.Replace("\"", "\\\"");

        return $"-p \"{escapedPrompt}\" " +
               $"--dangerously-skip-permissions " +
               $"--output-format json " +
               $"--max-turns 20 " +
               $"--no-session-persistence " +
               $"--model {model}";
    }

    /// <summary>
    /// 解析 Claude Code JSON 輸出，提取執行結果與摘要。
    /// --output-format json 的最終結果為最後一行 JSON，type="result"。
    /// </summary>
    private (bool Success, string Output) ParseJsonOutput(string rawOutput, int exitCode)
    {
        if (string.IsNullOrWhiteSpace(rawOutput))
            return (exitCode == 0, "（無輸出）");

        // JSON 輸出為逐行 JSON，最後一行是 type="result" 的結果物件
        var lines = rawOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        for (var i = lines.Length - 1; i >= 0; i--)
        {
            var line = lines[i].Trim();
            if (!line.StartsWith('{')) continue;

            try
            {
                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;

                if (!root.TryGetProperty("type", out var typeProp)) continue;
                if (typeProp.GetString() != "result") continue;

                var isError = root.TryGetProperty("is_error", out var errProp) && errProp.GetBoolean();
                var result  = root.TryGetProperty("result", out var resProp)
                    ? resProp.GetString() ?? ""
                    : "";

                return (!isError && exitCode == 0, result);
            }
            catch (JsonException)
            {
                // 非 JSON 行，繼續往上找
            }
        }

        // 找不到 result 物件：fallback 到 exit code 判斷
        return (exitCode == 0, lines.LastOrDefault(l => l.Trim().Length > 0) ?? "（無摘要）");
    }

    /// <summary>
    /// 確保 repo 目錄的 git config user.name / user.email 已設定（容器內可能為空）。
    /// </summary>
    private async Task ConfigureGitAsync(string workingDir, CancellationToken ct)
    {
        await RunGitConfigAsync(workingDir, "user.name", "Cody Dev Agent", ct);
        await RunGitConfigAsync(workingDir, "user.email", "cody@aiteam.local", ct);
    }

    private async Task RunGitConfigAsync(
        string workingDir, string key, string value, CancellationToken ct)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName         = "git",
                    Arguments        = $"config {key} \"{value}\"",
                    WorkingDirectory = workingDir,
                    UseShellExecute  = false,
                    CreateNoWindow   = true,
                }
            };
            process.Start();
            await process.WaitForExitAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "git config {Key} 設定失敗（不影響主流程）", key);
        }
    }
}

/// <summary>
/// Claude Code CLI 執行結果。
/// </summary>
/// <param name="Success">是否成功（exit code 0 且 is_error=false）。</param>
/// <param name="Output">Claude Code 回報的執行摘要（從 JSON result 欄位解析）。</param>
/// <param name="ExitCode">subprocess exit code。</param>
/// <param name="RawJson">完整 stdout（含所有 JSON 行，供 debug 用）。</param>
public record ClaudeCodeResult(
    bool   Success,
    string Output,
    int    ExitCode,
    string RawJson);
