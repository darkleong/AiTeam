using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace AiTeam.Bot.Agents;

/// <summary>
/// 封裝 Claude Code CLI subprocess 呼叫，供 Dev Agent 與唯讀探索使用。
/// 透過 `claude -p` 非互動模式在指定 repo 目錄內執行任務。
/// </summary>
public class ClaudeCodeService(ILogger<ClaudeCodeService> logger) : IClaudeCodeService
{
    private static readonly TimeSpan DefaultTimeout     = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan ReadOnlyTimeout    = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan VictoriaTimeout    = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan MeetingTimeout     = TimeSpan.FromMinutes(20);

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
        CancellationToken ct = default,
        string? systemPrompt = null)
    {
        // 確保 git config 已設定（容器內可能缺少 user.name/email）
        await ConfigureGitAsync(workingDir, ct);
        return await RunCoreAsync(workingDir, prompt, model, anthropicApiKey,
            DefaultTimeout, allowedTools: null, maxTurns: 40, systemPrompt, ct);
    }

    /// <summary>
    /// 以唯讀模式執行 Claude Code，僅開放 Glob / Grep / Read 工具。
    /// 供 Rosa / Demi / Vera / Sage 等 Agent 探索 codebase 使用，不寫入任何檔案。
    /// </summary>
    /// <param name="workingDir">repo 本地路徑</param>
    /// <param name="prompt">探索任務描述（含輸出格式要求）</param>
    /// <param name="model">Claude 模型 ID</param>
    /// <param name="anthropicApiKey">Anthropic API Key</param>
    /// <param name="ct">CancellationToken</param>
    /// <returns>執行結果（Output 即為 Agent 的結構化輸出）</returns>
    /// <summary>
    /// Victoria CEO 模式：可讀取整個 repo（src/ docs/ 等）、寫入 docs/、執行 git commit docs 變更。
    /// 不可 push、不可修改 src/ 程式碼（靠 CLAUDE_Victoria.md 提示詞約束）。
    /// 使用實際 repo 路徑（非 clone），呼叫端負責在進入前設定 CLAUDE.md。
    /// 有圖片時改用 stdin stream-json 模式（--input-format/--output-format stream-json）。
    /// </summary>
    public async Task<ClaudeCodeResult> RunVictoriaAsync(
        string workingDir,
        string prompt,
        string model,
        string anthropicApiKey,
        IReadOnlyList<ImageAttachment>? images = null,
        CancellationToken ct = default,
        string? systemPrompt = null)
    {
        await ConfigureGitVictoriaAsync(workingDir, ct);
        if (images is { Count: > 0 })
            return await RunCoreWithImagesAsync(workingDir, prompt, model, anthropicApiKey, images, VictoriaTimeout, maxTurns: 15, systemPrompt, ct);
        return await RunCoreAsync(workingDir, prompt, model, anthropicApiKey,
            VictoriaTimeout, allowedTools: null, maxTurns: 15, systemPrompt, ct);
    }

    public Task<ClaudeCodeResult> RunReadOnlyAsync(
        string workingDir,
        string prompt,
        string model,
        string anthropicApiKey,
        int? maxTurns = null,
        CancellationToken ct = default,
        string? systemPrompt = null)
        => RunCoreAsync(workingDir, prompt, model, anthropicApiKey,
            ReadOnlyTimeout, allowedTools: ["Glob", "Grep", "Read"], maxTurns: maxTurns ?? 10, systemPrompt, ct);

    /// <summary>
    /// QA 模式：開放所有工具（含 Write / Edit / Bash），供 Quinn 產生測試並以 dotnet build 驗證。
    /// 不呼叫 ConfigureGitAsync（QA 不 commit，由呼叫端 GitHubService 負責）。
    /// 靠 CLAUDE_Quinn.md 約束只寫入 tests/ 與 Playwright Generated/ 目錄。
    /// </summary>
    public Task<ClaudeCodeResult> RunQaAsync(
        string workingDir,
        string prompt,
        string model,
        string anthropicApiKey,
        CancellationToken ct = default,
        string? systemPrompt = null)
        => RunCoreAsync(workingDir, prompt, model, anthropicApiKey,
            DefaultTimeout, allowedTools: null, maxTurns: 20, systemPrompt, ct);

    /// <summary>
    /// Review 模式：開放 Glob / Grep / Read / Bash，不開放 Write / Edit。
    /// 供 Vera 做 code review + 影響範圍分析合一的 session。
    /// Bash 僅供唯讀診斷（git log、dotnet build 等），靠 CLAUDE_Vera.md 約束使用範圍。
    /// </summary>
    public Task<ClaudeCodeResult> RunReviewAsync(
        string workingDir,
        string prompt,
        string model,
        string anthropicApiKey,
        CancellationToken ct = default,
        string? systemPrompt = null)
        => RunCoreAsync(workingDir, prompt, model, anthropicApiKey,
            ReadOnlyTimeout, allowedTools: ["Glob", "Grep", "Read", "Bash"], maxTurns: 15, systemPrompt, ct);

    /// <summary>
    /// Stage 25a：以持續對話 session 模式執行 Claude Code，供 Kick-off 會議的多輪討論使用。
    /// 第一輪：--session-id {uuid}；後續輪：--resume {uuid}。
    /// 不帶 --no-session-persistence，session 資料保留於本機。
    /// </summary>
    public Task<ClaudeCodeResult> RunMeetingSessionAsync(
        string workingDir,
        string sessionId,
        string prompt,
        string model,
        string anthropicApiKey,
        bool isFirstMessage,
        int maxTurns,
        string[]? allowedTools = null,
        CancellationToken ct = default,
        string? systemPrompt = null)
        => RunMeetingCoreAsync(workingDir, sessionId, prompt, model, anthropicApiKey,
            isFirstMessage, maxTurns, allowedTools, systemPrompt, ct);

    // ────────────── Private ──────────────

    /// <summary>
    /// 有圖片時改用 stdin stream-json 格式，解決 Claude Code CLI 不支援 -p 帶圖片的限制。
    /// Discord Embed 的圖片縮圖展示已在 Bot controller 端以 SendMessageAsync 附件方式簡化處理，
    /// 因此此處只需把圖片 base64 組進 stream-json content array，讓 Victoria 能看到圖片內容。
    /// </summary>
    private async Task<ClaudeCodeResult> RunCoreWithImagesAsync(
        string workingDir,
        string prompt,
        string model,
        string anthropicApiKey,
        IReadOnlyList<ImageAttachment> images,
        TimeSpan timeout,
        int maxTurns,
        string? systemPrompt,
        CancellationToken ct)
    {
        // 組 stream-json 輸入：text + N 張圖片
        var contentItems = new List<object>
        {
            new { type = "text", text = prompt }
        };
        foreach (var img in images)
            contentItems.Add(new { type = "image", source = new { type = "base64", media_type = img.MediaType, data = img.Base64Data } });

        var inputJson = JsonSerializer.Serialize(new
        {
            type    = "user",
            message = new { role = "user", content = contentItems }
        });

        // Stage 65 子項 1：可選 --append-system-prompt（trust source 自家 CLAUDE_<X>.md，escape 對齊既有 prompt pattern）
        var sysPromptArg = string.IsNullOrEmpty(systemPrompt)
            ? ""
            : $"--append-system-prompt \"{systemPrompt.Replace("\"", "\\\"")}\" ";
        var args = $"--input-format stream-json --output-format stream-json --verbose " +
                   $"--dangerously-skip-permissions " +
                   $"{sysPromptArg}" +
                   $"--max-turns {maxTurns} " +
                   $"--no-session-persistence " +
                   $"--model {model}";

        logger.LogInformation(
            "ClaudeCodeService 啟動 subprocess（含圖片，dir={Dir}，model={Model}，images={Count}）",
            workingDir, model, images.Count);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout);

        var psi = new ProcessStartInfo
        {
            FileName               = "claude",
            Arguments              = args,
            WorkingDirectory       = workingDir,
            RedirectStandardInput  = true,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
            CreateNoWindow         = true,
        };
        psi.Environment["ANTHROPIC_API_KEY"] = anthropicApiKey;

        using var process = new Process { StartInfo = psi };
        var stdoutBuilder = new StringBuilder();
        var stderrBuilder = new StringBuilder();

        process.OutputDataReceived += (_, e) => { if (e.Data is not null) stdoutBuilder.AppendLine(e.Data); };
        process.ErrorDataReceived  += (_, e) => { if (e.Data is not null) stderrBuilder.AppendLine(e.Data); };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        // 寫入 stream-json 後關閉 stdin，CLI 讀到 EOF 才開始處理
        await process.StandardInput.WriteLineAsync(inputJson);
        process.StandardInput.Close();

        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            throw new TimeoutException($"Claude Code subprocess（含圖片）超過 {timeout.TotalMinutes} 分鐘逾時");
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            throw;
        }

        var stdout   = stdoutBuilder.ToString();
        var stderr   = stderrBuilder.ToString();
        var exitCode = process.ExitCode;
        var (success, output, usage) = ParseJsonOutput(stdout, exitCode);

        if (!string.IsNullOrWhiteSpace(stderr))
        {
            if (!success || exitCode != 0)
                logger.LogError("Claude Code stderr（含圖片，exitCode={Code}）：{Stderr}", exitCode, stderr);
            else
                logger.LogDebug("Claude Code stderr（含圖片）：{Stderr}", stderr);
        }

        if (!success || exitCode != 0)
        {
            var rawTail = stdout.Length > 3000 ? "…" + stdout[^3000..] : stdout;
            logger.LogError("Claude Code 失敗完整輸出（含圖片，exitCode={Code}）：\n{Raw}", exitCode, rawTail);
        }

        // Stage 58：CLI path API 失敗 signal 偵測（detect 失敗 fallback 既有 result.Success=false path 不破壞既有失敗路徑）
        if (!success && DetectApiFailureSignal(output, stderr) is { } apiErrorSnippet)
        {
            logger.LogWarning("[Stage58] Claude Code 偵測到 API 失敗 signal（含圖片）：{Snippet}", apiErrorSnippet);
            throw new LlmApiFailureException(LlmProviderType.Anthropic, apiErrorSnippet);
        }

        logger.LogInformation(
            "Claude Code subprocess 結束（含圖片，exitCode={Code}，success={Success}）",
            exitCode, success);

        return new ClaudeCodeResult(success, output, exitCode, stdout, usage);
    }

    private async Task<ClaudeCodeResult> RunCoreAsync(
        string workingDir,
        string prompt,
        string model,
        string anthropicApiKey,
        TimeSpan timeout,
        string[]? allowedTools,
        int maxTurns,
        string? systemPrompt,
        CancellationToken ct)
    {
        var args = BuildArgs(prompt, model, allowedTools, maxTurns, systemPrompt);

        logger.LogInformation(
            "ClaudeCodeService 啟動 subprocess（dir={Dir}，model={Model}，readOnly={ReadOnly}）",
            workingDir, model, allowedTools is not null);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout);

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
        var stderrBuilder = new StringBuilder();

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
                $"Claude Code subprocess 超過 {timeout.TotalMinutes} 分鐘逾時");
        }
        catch (OperationCanceledException)
        {
            // Stage 14：外部取消（CancelAsync 呼叫），確保 subprocess 被 kill
            try { process.Kill(entireProcessTree: true); } catch { /* ignore */ }
            throw;
        }

        var stdout  = stdoutBuilder.ToString();
        var stderr  = stderrBuilder.ToString();

        var exitCode = process.ExitCode;
        var (success, output, usage) = ParseJsonOutput(stdout, exitCode);

        if (!string.IsNullOrWhiteSpace(stderr))
        {
            // 失敗時升級為 Error 方便 Docker log 過濾；成功時降為 Debug（通常是 progress info）
            if (!success || exitCode != 0)
                logger.LogError("Claude Code stderr（exitCode={Code}）：{Stderr}", exitCode, stderr);
            else
                logger.LogDebug("Claude Code stderr：{Stderr}", stderr);
        }

        if (!success || exitCode != 0)
        {
            // 失敗時記錄 stdout 尾段，方便 Docker log 追查根因
            var rawTail = stdout.Length > 3000 ? "…" + stdout[^3000..] : stdout;
            logger.LogError(
                "Claude Code 失敗完整輸出（exitCode={Code}）：\n{Raw}",
                exitCode, rawTail);
        }

        // Stage 58：CLI path API 失敗 signal 偵測（detect 失敗 fallback 既有 result.Success=false path 不破壞既有失敗路徑）
        if (!success && DetectApiFailureSignal(output, stderr) is { } apiErrorSnippet)
        {
            logger.LogWarning("[Stage58] Claude Code 偵測到 API 失敗 signal：{Snippet}", apiErrorSnippet);
            throw new LlmApiFailureException(LlmProviderType.Anthropic, apiErrorSnippet);
        }

        logger.LogInformation(
            "Claude Code subprocess 結束（exitCode={Code}，success={Success}）",
            exitCode, success);

        return new ClaudeCodeResult(success, output, exitCode, stdout, usage);
    }

    private async Task<ClaudeCodeResult> RunMeetingCoreAsync(
        string workingDir,
        string sessionId,
        string prompt,
        string model,
        string anthropicApiKey,
        bool isFirstMessage,
        int maxTurns,
        string[]? allowedTools,
        string? systemPrompt,
        CancellationToken ct)
    {
        var args = BuildMeetingArgs(prompt, model, sessionId, isFirstMessage, maxTurns, allowedTools, systemPrompt);

        logger.LogInformation(
            "ClaudeCodeService 啟動會議 session subprocess（dir={Dir}，sessionId={SessionId}，isFirst={IsFirst}）",
            workingDir, sessionId, isFirstMessage);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(MeetingTimeout);

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
        psi.Environment["ANTHROPIC_API_KEY"] = anthropicApiKey;

        using var process = new Process { StartInfo = psi };
        var stdoutBuilder = new StringBuilder();
        var stderrBuilder = new StringBuilder();

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
            try { process.Kill(entireProcessTree: true); } catch { /* ignore */ }
            throw new TimeoutException(
                $"Claude Code 會議 session subprocess 超過 {MeetingTimeout.TotalMinutes} 分鐘逾時");
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { /* ignore */ }
            throw;
        }

        var stdout  = stdoutBuilder.ToString();
        var stderr  = stderrBuilder.ToString();
        var exitCode = process.ExitCode;
        var (success, output, usage) = ParseJsonOutput(stdout, exitCode);

        if (!string.IsNullOrWhiteSpace(stderr))
        {
            if (!success || exitCode != 0)
                logger.LogError("Claude Code 會議 session stderr（sessionId={Id}，exitCode={Code}）：{Stderr}",
                    sessionId, exitCode, stderr);
            else
                logger.LogDebug("Claude Code 會議 session stderr：{Stderr}", stderr);
        }

        if (!success || exitCode != 0)
        {
            var rawTail = stdout.Length > 3000 ? "…" + stdout[^3000..] : stdout;
            logger.LogError(
                "Claude Code 會議 session 失敗（sessionId={Id}，exitCode={Code}）：\n{Raw}",
                sessionId, exitCode, rawTail);
        }

        // Stage 58：CLI path API 失敗 signal 偵測（detect 失敗 fallback 既有 result.Success=false path 不破壞既有失敗路徑）
        if (!success && DetectApiFailureSignal(output, stderr) is { } apiErrorSnippet)
        {
            logger.LogWarning("[Stage58] Claude Code 會議 session 偵測到 API 失敗 signal（sessionId={Id}）：{Snippet}",
                sessionId, apiErrorSnippet);
            throw new LlmApiFailureException(LlmProviderType.Anthropic, apiErrorSnippet);
        }

        logger.LogInformation(
            "Claude Code 會議 session 結束（sessionId={Id}，exitCode={Code}，success={Success}）",
            sessionId, exitCode, success);

        return new ClaudeCodeResult(success, output, exitCode, stdout, usage);
    }

    /// <summary>
    /// Stage 25a：組合會議 session 的 CLI 參數。
    /// 第一輪：--session-id {uuid}；後續輪：--resume {uuid}。
    /// 不帶 --no-session-persistence。
    /// </summary>
    private static string BuildMeetingArgs(
        string prompt, string model, string sessionId, bool isFirstMessage, int maxTurns, string[]? allowedTools, string? systemPrompt = null)
    {
        var escapedPrompt = prompt.Replace("\"", "\\\"");
        // 第一輪建立 session；後續輪 resume（UUID 直接傳給 --resume）
        var sessionArg = isFirstMessage
            ? $"--session-id {sessionId} "
            : $"--resume {sessionId} ";
        var toolsArg = allowedTools?.Length > 0
            ? $"--allowedTools \"{string.Join(",", allowedTools)}\" "
            : "";
        // Stage 65 子項 1：可選 --append-system-prompt（trust source 自家 CLAUDE_<X>.md，escape 對齊既有 prompt pattern）
        var sysPromptArg = string.IsNullOrEmpty(systemPrompt)
            ? ""
            : $"--append-system-prompt \"{systemPrompt.Replace("\"", "\\\"")}\" ";

        return $"-p \"{escapedPrompt}\" " +
               $"--dangerously-skip-permissions " +
               $"{toolsArg}" +
               $"{sysPromptArg}" +
               $"--output-format json " +
               $"--max-turns {maxTurns} " +
               $"{sessionArg}" +
               $"--model {model}";
        // 注意：不帶 --no-session-persistence，session 資料保留於本機
    }

    private static string BuildArgs(string prompt, string model, string[]? allowedTools, int maxTurns, string? systemPrompt = null)
    {
        var escapedPrompt = prompt.Replace("\"", "\\\"");
        var toolsArg = allowedTools?.Length > 0
            ? $"--allowedTools \"{string.Join(",", allowedTools)}\" "
            : "";
        // Stage 65 子項 1：可選 --append-system-prompt（trust source 自家 CLAUDE_<X>.md，escape 對齊既有 prompt pattern）
        var sysPromptArg = string.IsNullOrEmpty(systemPrompt)
            ? ""
            : $"--append-system-prompt \"{systemPrompt.Replace("\"", "\\\"")}\" ";

        return $"-p \"{escapedPrompt}\" " +
               $"--dangerously-skip-permissions " +
               $"{toolsArg}" +
               $"{sysPromptArg}" +
               $"--output-format json " +
               $"--max-turns {maxTurns} " +
               $"--no-session-persistence " +
               $"--model {model}";
    }

    /// <summary>
    /// 解析 Claude Code JSON 輸出，提取執行結果與摘要。
    /// --output-format json 的最終結果為最後一行 JSON，type="result"。
    /// Stage 44：額外解析 usage 子物件 + 頂層 total_cost_usd 供 token_logs 寫入。
    /// </summary>
    private (bool Success, string Output, TokenUsage? Usage) ParseJsonOutput(string rawOutput, int exitCode)
    {
        if (string.IsNullOrWhiteSpace(rawOutput))
            return (exitCode == 0, "（無輸出）", null);

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
                var usage = TryParseUsage(root);

                return (!isError && exitCode == 0, result, usage);
            }
            catch (JsonException)
            {
                // 非 JSON 行，繼續往上找
            }
        }

        // 找不到 result 物件：fallback 到 exit code 判斷
        return (exitCode == 0, lines.LastOrDefault(l => l.Trim().Length > 0) ?? "（無摘要）", null);
    }

    /// <summary>
    /// Stage 44：從 type="result" 物件解析 usage + total_cost_usd。
    /// schema 不符或欄位缺失 → 回傳 null（呼叫端 LogCliUsageAsync 會 early return，不阻塞主流程）。
    /// Stage 56：cost 欄位擴容多名兼容（total_cost_usd / cost_usd / usage.cost_usd）+ 找不到時 LogDebug dump
    /// result keys（FF 四十三 spike H1/H2 觀察用）。Cost null 時呼叫端會走 TokenCostEstimator fallback。
    /// </summary>
    private TokenUsage? TryParseUsage(JsonElement root)
    {
        try
        {
            if (!root.TryGetProperty("usage", out var u)) return null;
            var input  = u.TryGetProperty("input_tokens",                out var v1) && v1.ValueKind == JsonValueKind.Number ? v1.GetInt32() : 0;
            var output = u.TryGetProperty("output_tokens",               out var v2) && v2.ValueKind == JsonValueKind.Number ? v2.GetInt32() : 0;
            var cc     = u.TryGetProperty("cache_creation_input_tokens", out var v3) && v3.ValueKind == JsonValueKind.Number ? v3.GetInt32() : 0;
            var cr     = u.TryGetProperty("cache_read_input_tokens",     out var v4) && v4.ValueKind == JsonValueKind.Number ? v4.GetInt32() : 0;

            // Stage 56：多欄位名兼容（按優先序試 total_cost_usd → cost_usd → usage.cost_usd）
            decimal? cost = null;
            if (root.TryGetProperty("total_cost_usd", out var c1) && c1.ValueKind == JsonValueKind.Number)
                cost = c1.GetDecimal();
            else if (root.TryGetProperty("cost_usd", out var c2) && c2.ValueKind == JsonValueKind.Number)
                cost = c2.GetDecimal();
            else if (u.TryGetProperty("cost_usd", out var c3) && c3.ValueKind == JsonValueKind.Number)
                cost = c3.GetDecimal();

            // Stage 56：cost 找不到時 dump result keys 供未來 Docker log 觀察真實 schema（FF 四十三 spike H1/H2）
            if (cost is null && logger.IsEnabled(LogLevel.Debug))
            {
                var keys = new List<string>();
                foreach (var p in root.EnumerateObject()) keys.Add(p.Name);
                logger.LogDebug("[Stage56-FF43] CLI result.total_cost_usd 找不到（兼容三欄位皆無），result keys={Keys}", string.Join(",", keys));
            }

            return new TokenUsage(input, output, cc, cr, cost);
        }
        catch (Exception)
        {
            return null;   // 任何例外 → null，硬規則「不阻塞」
        }
    }

    /// <summary>
    /// Stage 58 (FF 五十三)：CLI path API 失敗 signal 偵測（CLI subprocess stdout / stderr 含 Anthropic API 餘額不足 / 401 等錯誤格式）。
    ///
    /// 配對 case-insensitive substring：
    ///   - "Credit balance is too low"（餘額不足，最常見）
    ///   - "insufficient_balance"（API error code）
    ///   - "401"（HTTP 401 Unauthorized — 可能是 API key 失效或 over-limit）
    ///   - "authentication_error"（API error type）
    ///
    /// 保守原則：detect 失敗時回 null，呼叫端 fallback 既有 result.Success=false path（不破壞既有失敗路徑），
    /// 任何漏接最壞情況退化為 silent fail（修前 baseline 行為，不會比現況差）。
    ///
    /// 回傳：偵測到 signal 時回 capped 500 chars 的錯誤摘要供 LlmApiFailureException.RawError 用；無 signal 回 null。
    /// </summary>
    private static string? DetectApiFailureSignal(string output, string stderr)
    {
        if (string.IsNullOrWhiteSpace(output) && string.IsNullOrWhiteSpace(stderr))
            return null;

        var combined = $"{output}\n{stderr}";
        var lower    = combined.ToLowerInvariant();

        if (lower.Contains("credit balance is too low")
            || lower.Contains("insufficient_balance")
            || lower.Contains("authentication_error")
            || lower.Contains("401"))
        {
            // 取 output 優先（API error 通常在 result 文字內）；若 output 空 fallback stderr
            var source = !string.IsNullOrWhiteSpace(output) ? output : stderr;
            return source.Length > 500 ? source[..500] : source;
        }

        return null;
    }

    /// <summary>
    /// 確保 repo 目錄的 git config user.name / user.email 已設定（容器內可能為空）。
    /// </summary>
    private async Task ConfigureGitAsync(string workingDir, CancellationToken ct)
    {
        await RunGitConfigAsync(workingDir, "user.name", "Cody Dev Agent", ct);
        await RunGitConfigAsync(workingDir, "user.email", "cody@aiteam.local", ct);
    }

    /// <summary>設定 Victoria CEO 的 git user identity。</summary>
    private async Task ConfigureGitVictoriaAsync(string workingDir, CancellationToken ct)
    {
        await RunGitConfigAsync(workingDir, "user.name", "Victoria CEO", ct);
        await RunGitConfigAsync(workingDir, "user.email", "victoria@aiteam.local", ct);
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
/// <param name="Usage">Stage 44：token usage（input / output / cache / cost）。CLI 解析失敗時為 null。</param>
public record ClaudeCodeResult(
    bool        Success,
    string      Output,
    int         ExitCode,
    string      RawJson,
    TokenUsage? Usage = null);
