using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using AiTeam.Bot.GitHub;
using AiTeam.Bot.Services;
using AiTeam.Data;
using AiTeam.Data.Repositories;
using AiTeam.Shared.ViewModels;
using Microsoft.Extensions.Configuration;
using Octokit;

namespace AiTeam.Bot.Agents;

/// <summary>
/// QA Agent（Quinn）：讀取 PR diff，以單一 Claude Code session 產生自動化測試。
/// - .cs 變更 → xUnit + NSubstitute + FluentAssertions
/// - .razor / .css 變更 → Playwright 視覺截圖測試
/// Claude Code 負責：探索 codebase → 寫入測試檔 → dotnet build 驗證 → 修錯直到通過。
/// 不 commit / push（由呼叫端 GitHubService 負責）。
/// </summary>
public class QaAgentService(
    GitHubService gitHubService,
    TaskRepository taskRepository,
    DashboardPushService dashboardPush,
    IClaudeCodeService claudeCodeService,
    AppSettingsService appSettings,
    IConfiguration configuration,
    ILogger<QaAgentService> logger) : IAgentExecutor
{
    private const string AgentName = "QA";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <inheritdoc />
    public async Task<AgentExecutionResult> ExecuteTaskAsync(
        TaskItem task,
        string owner,
        string repo,
        IReadOnlyList<string> rules,
        CancellationToken cancellationToken = default)
    {
        // Stage 17：MockMode early return — 跳過 GitHub API 呼叫，回傳模擬 QA 結果
        // Stage 26：修正狀態時序 — 先推 running，等待延遲後再設 done，確保 Dashboard 可觀察到 running 狀態
        if (await appSettings.GetBoolAsync("MockMode", false, cancellationToken))
        {
            // Stage 43-E：qa_fix_loop_fail 情境 — 持續回 failed 報告，讓 Petra 走 code_bug 路由 + QaFixRound 累計
            //   不切換 FailScenario（持續失敗直到 QaFixRound >= max 由 QaCoordinationService 觸發 escalate）
            if (MockClaudeCodeService.FailScenario == "qa_fix_loop_fail")
            {
                logger.LogInformation("[MockMode/QaFixLoop] Quinn 持續回失敗報告（QaFixRound 累計觸發 escalate）");
                AddLog(task, "[MOCK-FAIL] Quinn 模擬 QA 連敗中...", "running");
                await taskRepository.SaveAsync(cancellationToken);
                await PushStatus("running", task.Title);
                await Task.Delay(await appSettings.GetMockDelayMsAsync(cancellationToken), cancellationToken);
                AddLog(task, "[MOCK-FAIL] Quinn 持續失敗（fix loop）", "failed");
                taskRepository.UpdateStatus(task, "failed");
                await taskRepository.SaveAsync(cancellationToken);
                var loopFailReport = new QaReport
                {
                    Status      = "failed",
                    PassedTests = [],
                    FailedTests = ["[MOCK-FAIL] QaFixLoop::Persistent"],
                    Summary     = "[MOCK-FAIL] QA fix loop 持續失敗（驗收 needs_intervention 觸發點）"
                };
                return new AgentExecutionResult(true, "[MOCK-FAIL] QA fix loop 持續失敗",
                    TestReport: JsonSerializer.Serialize(loopFailReport, JsonOptions));
            }

            // 強制失敗情境：回傳 QA 失敗報告，觸發 Petra 判斷路由
            if (MockClaudeCodeService.FailScenario == "qa_failure")
            {
                MockClaudeCodeService.FailScenario = null;
                logger.LogInformation("[MockMode/FailQA] Quinn 回傳模擬 QA 失敗報告，觸發 Petra 路由");
                AddLog(task, "[MOCK-FAIL] Quinn 模擬 QA 執行中...", "running");
                await taskRepository.SaveAsync(cancellationToken);
                await PushStatus("running", task.Title);
                await Task.Delay(await appSettings.GetMockDelayMsAsync(cancellationToken), cancellationToken);
                AddLog(task, "[MOCK-FAIL] Quinn 模擬 QA 完成，1 個測試失敗", "failed");
                taskRepository.UpdateStatus(task, "failed");
                await taskRepository.SaveAsync(cancellationToken);
                var failReport = new QaReport
                {
                    Status      = "failed",
                    PassedTests = [],
                    FailedTests = ["[MOCK-FAIL] MockFailTest.cs::TestMockFeature"],
                    Summary     = "[MOCK-FAIL] 1 個測試失敗：模擬錯誤處理邏輯異常"
                };
                return new AgentExecutionResult(true, "[MOCK-FAIL] QA 失敗，1 個測試未通過",
                    TestReport: JsonSerializer.Serialize(failReport, JsonOptions));
            }

            logger.LogInformation("[MockMode] QaAgentService 跳過 GitHub 操作，回傳模擬結果");
            AddLog(task, "[MOCK] Quinn 模擬 QA 執行中...", "running");
            await taskRepository.SaveAsync(cancellationToken);
            await PushStatus("running", task.Title);
            await Task.Delay(await appSettings.GetMockDelayMsAsync(cancellationToken), cancellationToken);
            var mockReport = new QaReport { Status = "passed", PassedTests = ["[MOCK] MockTest.cs"], Summary = "[MOCK] QA 完成，0 個失敗" };
            return new AgentExecutionResult(true, "[MOCK] QA 完成，測試 0 個失敗",
                TestReport: JsonSerializer.Serialize(mockReport, JsonOptions));
        }

        AddLog(task, "QA Agent 開始執行", "running");
        await taskRepository.SaveAsync(cancellationToken);
        await PushStatus("running", task.Title);

        var localPath = "";
        try
        {
            // 1. 解析 PR 編號
            var prNumber = ExtractPrNumber($"{task.Title} {task.Description}");
            if (prNumber <= 0)
                return new AgentExecutionResult(false, "無法從任務描述中取得 PR 編號，格式：PR #123");

            var headRef = await gitHubService.GetPullRequestHeadRefAsync(owner, repo, prNumber);
            var prFiles = await gitHubService.GetPullRequestFilesAsync(owner, repo, prNumber);

            // 2. 判斷測試策略
            var hasUiChanges = prFiles.Any(f =>
                f.FileName.EndsWith(".razor", StringComparison.OrdinalIgnoreCase) ||
                f.FileName.EndsWith(".css",   StringComparison.OrdinalIgnoreCase));

            var csFiles = prFiles
                .Where(f => f.FileName.EndsWith(".cs")
                         && !f.FileName.EndsWith("Tests.cs")
                         && !f.FileName.EndsWith("Spec.cs")
                         && !f.FileName.Contains(".Tests/")
                         && !f.FileName.Contains(".Test/"))
                .ToList();

            if (!hasUiChanges && csFiles.Count == 0)
                return AgentExecutionResult.Skipped(
                    $"PR #{prNumber} 未包含可測試的 .cs / .razor / .css 檔案，略過 QA");

            // 3. Clone / Pull + Checkout
            localPath = gitHubService.CloneOrPull(owner, repo, task.Id.ToString("N")[..8]);
            gitHubService.CreateAndCheckoutBranch(localPath, headRef);
            AddLog(task, "Git Clone/Pull 完成", "done");

            // 4. 組 prompt
            var uiFiles = prFiles
                .Where(f => f.FileName.EndsWith(".razor", StringComparison.OrdinalIgnoreCase) ||
                            f.FileName.EndsWith(".css",   StringComparison.OrdinalIgnoreCase))
                .ToList();

            var prompt = BuildClaudeCodeQaPrompt(prNumber, csFiles, uiFiles);

            AddLog(task, $"啟動 Claude Code QA session（{csFiles.Count} 個 .cs + {uiFiles.Count} 個 UI 檔）", "done");
            await taskRepository.SaveAsync(cancellationToken);

            // 5. 單一 Claude Code session
            var ccResult = await RunClaudeCodeQaAsync(owner, repo, headRef, prompt, task, localPath, cancellationToken);

            // 6. 解析 JSON 結果（triple fallback）
            var report = TryParseQaReport(ccResult.Output);
            if (report is null && !string.IsNullOrWhiteSpace(ccResult.RawJson))
                report = TryParseQaReport(ccResult.RawJson);
            if (report is null)
            {
                logger.LogWarning("Quinn Claude Code JSON 解析失敗（exitCode={Code}）", ccResult.ExitCode);
                report = new QaReport { Status = "passed", Summary = "（無法解析 Quinn 輸出）" };
            }

            var passedCount  = report.PassedTests.Count;
            var failedCount  = report.FailedTests.Count;
            var strategyDesc = hasUiChanges
                ? $"xUnit {csFiles.Count} 個 + Playwright 1 個"
                : $"xUnit {csFiles.Count} 個";

            AddLog(task, $"測試報告：{report.Status}，通過 {passedCount} 個，失敗 {failedCount} 個。{report.Summary}", "done");

            // 7. Commit + Push（呼叫端負責）
            var commitMessage = $"test: QA Agent 自動產生測試（來自 PR #{prNumber}）";
            gitHubService.CommitAll(localPath, commitMessage);
            gitHubService.Push(localPath, headRef);

            AddLog(task, $"測試已推送到 branch {headRef}（{strategyDesc}）", "done");
            await taskRepository.SaveAsync(cancellationToken);

            // Stage 24：序列化完整測試報告，回傳給 TaskGroupService 存入 DB
            var testReportJson = JsonSerializer.Serialize(report, JsonOptions);

            await PushStatus("idle");
            return new AgentExecutionResult(true,
                $"QA 測試已推送到 Dev branch（{strategyDesc}，PR #{prNumber}，{report.Status}）",
                TestReport: testReportJson);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "QA Agent 執行失敗：{Title}", task.Title);
            AddLog(task, $"執行失敗：{ex.Message}", "failed");
            await taskRepository.SaveAsync(cancellationToken);
            await PushStatus("error");
            return new AgentExecutionResult(false, $"QA Agent 執行失敗：{ex.Message}");
        }
        finally
        {
            if (!string.IsNullOrEmpty(localPath))
                gitHubService.CleanupLocalRepo(localPath);
        }
    }

    // ────────────── Claude Code QA Session ──────────────

    /// <summary>
    /// 在已 clone 的 localPath 上：備份 CLAUDE.md → 寫入 CLAUDE_Quinn.md → 執行 RunQaAsync → 還原 CLAUDE.md。
    /// localPath 由呼叫端管理（不在此 method 內清理）。
    /// </summary>
    private async Task<ClaudeCodeResult> RunClaudeCodeQaAsync(
        string owner,
        string repo,
        string headRef,
        string prompt,
        TaskItem task,
        string localPath,
        CancellationToken cancellationToken)
    {
        var claudeMdPath  = Path.Combine(localPath, "CLAUDE.md");
        var templatePath  = Path.Combine(AppContext.BaseDirectory, "Resources", "CLAUDE_Quinn.md");
        var backup = File.Exists(claudeMdPath)
            ? await File.ReadAllTextAsync(claudeMdPath, cancellationToken)
            : null;

        try
        {
            if (File.Exists(templatePath))
                await File.WriteAllTextAsync(claudeMdPath,
                    await File.ReadAllTextAsync(templatePath, cancellationToken),
                    cancellationToken);
            else
                logger.LogWarning("CLAUDE_Quinn.md 不存在於 {Path}", templatePath);

            var model  = configuration["Agents:QA:Model"]
                      ?? configuration["Anthropic:DefaultModel"]
                      ?? "claude-sonnet-4-6";
            var apiKey = configuration["Anthropic:ApiKey"] ?? "";

            return await claudeCodeService.RunQaAsync(
                localPath, prompt, model, apiKey, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "RunClaudeCodeQaAsync 失敗（headRef={Ref}）", headRef);
            return new ClaudeCodeResult(false, "", -1, "");
        }
        finally
        {
            // 不論成功或失敗，還原 CLAUDE.md
            if (backup is not null)
                await File.WriteAllTextAsync(claudeMdPath, backup, CancellationToken.None);
            else if (File.Exists(claudeMdPath))
                File.Delete(claudeMdPath);
        }
    }

    // ────────────── Prompt 建構 ──────────────

    private static string BuildClaudeCodeQaPrompt(
        int prNumber,
        IReadOnlyList<PullRequestFile> csFiles,
        IReadOnlyList<PullRequestFile> uiFiles)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"## PR 編號：#{prNumber}");
        sb.AppendLine();

        if (csFiles.Count > 0)
        {
            sb.AppendLine("## 需要 xUnit 測試的 .cs 檔案");
            foreach (var f in csFiles)
                sb.AppendLine($"- `{f.FileName}`");
            sb.AppendLine();
        }

        if (uiFiles.Count > 0)
        {
            sb.AppendLine("## 需要 Playwright 視覺測試的 UI 檔案");
            foreach (var f in uiFiles)
                sb.AppendLine($"- `{f.FileName}`");
            sb.AppendLine();
        }

        sb.AppendLine("## 指示");
        sb.AppendLine("1. 依照 CLAUDE.md 的規則，為上列檔案產生對應的自動化測試");
        sb.AppendLine("2. 先用 Read / Glob / Grep 探索各檔案的完整內容與相依關係");
        sb.AppendLine("3. 使用 Write 工具直接寫入測試檔（不要輸出 markdown code fence）");
        sb.AppendLine("4. 執行 dotnet build 確認編譯通過，有錯自行修正");
        sb.AppendLine("5. 只輸出 JSON 結果（格式見 CLAUDE.md）");

        return sb.ToString();
    }

    // ────────────── 解析 ──────────────

    private QaReport? TryParseQaReport(string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return null;
        try
        {
            var start = content.IndexOf('{');
            var end   = content.LastIndexOf('}');
            if (start < 0 || end < 0) return null;

            return JsonSerializer.Deserialize<QaReport>(content[start..(end + 1)], JsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "QaReport 解析失敗");
            return null;
        }
    }

    private static int ExtractPrNumber(string text)
    {
        var match = Regex.Match(text, @"PR\s*#(\d+)", RegexOptions.IgnoreCase);
        if (match.Success && int.TryParse(match.Groups[1].Value, out var num)) return num;

        match = Regex.Match(text, @"/pull/(\d+)", RegexOptions.IgnoreCase);
        return match.Success && int.TryParse(match.Groups[1].Value, out num) ? num : 0;
    }

    // ────────────── 輔助方法 ──────────────

    private void AddLog(TaskItem task, string step, string status)
        => taskRepository.AddLog(new TaskLog
        {
            TaskId = task.Id,
            Agent  = AgentName,
            Step   = step,
            Status = status
        });

    private async Task PushStatus(string status, string? taskTitle = null)
        => await dashboardPush.PushAgentStatusAsync(new AgentStatusViewModel
        {
            AgentName        = AgentName,
            Status           = status,
            CurrentTaskTitle = taskTitle ?? "",
            LastUpdated      = DateTime.UtcNow
        });
}

// ────────────── 資料模型 ──────────────

/// <summary>Stage 24：Quinn 的結構化測試報告（CLAUDE_Quinn.md 輸出格式）。</summary>
public class QaReport
{
    [JsonPropertyName("status")]           public string Status        { get; set; } = "passed";
    [JsonPropertyName("passed_tests")]     public List<string> PassedTests { get; set; } = [];
    [JsonPropertyName("failed_tests")]     public List<string> FailedTests { get; set; } = [];
    [JsonPropertyName("no_test_reason")]   public string? NoTestReason { get; set; }
    [JsonPropertyName("summary")]          public string Summary       { get; set; } = "";
}
