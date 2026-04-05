using System.Text.Json;
using System.Text.RegularExpressions;
using AiTeam.Data;
using AiTeam.Data.Repositories;
using AiTeam.Bot.GitHub;
using AiTeam.Bot.Services;
using AiTeam.Shared.ViewModels;

namespace AiTeam.Bot.Agents;

/// <summary>
/// Dev Agent：接收 CEO 分派的任務，驅動 Claude Code CLI 自主探索 repo、實作變更、
/// 確認 build 通過後，由 GitHubService 負責 commit / push / 開 PR。
/// Stage 11：核心執行層從「Claude API 一次性產出」升級為「Claude Code CLI 自主開發」。
/// </summary>
public class DevAgentService(
    LlmProviderFactory providerFactory,
    ClaudeCodeService claudeCodeService,
    GitHubService gitHubService,
    TaskRepository taskRepository,
    DashboardPushService dashboardPush,
    IConfiguration configuration,
    ILogger<DevAgentService> logger) : IAgentExecutor
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    /// <summary>
    /// 處理 CEO 分派的任務，回傳執行計畫（給老闆第二層確認用）。
    /// Stage 10：制定計畫前先取得 repo 結構（2 層），並解析 task.Description 中的 metadata block。
    /// </summary>
    public async Task<DevPlan> BuildPlanAsync(
        TaskItem task,
        string owner,
        string repo,
        IReadOnlyList<string> rules,
        CancellationToken cancellationToken = default)
    {
        var provider = providerFactory.Create("Dev");
        var systemPrompt = BuildSystemPrompt(rules);

        // 取得 repo 目錄結構（最多 200 筆，兩層）
        var repoTree = await gitHubService.GetRepoTreeSummaryAsync(owner, repo);

        // 解析 task.Description 中的 Orchestrator metadata block
        ParseDescriptionMeta(task.Description,
            out var issueUrls, out var uiSpecPath, out var isFixLoop, out var veraReview);

        // fix loop：取得既有 PR 的 branch name，確保 LLM 不自創新 branch
        string? fixBranch = null;
        if (isFixLoop)
        {
            var prNum = ExtractPrNumberFromText(task.Description ?? "");
            if (prNum > 0)
            {
                try
                {
                    fixBranch = await gitHubService.GetPullRequestHeadRefAsync(owner, repo, prNum);
                    logger.LogInformation("Fix loop 使用既有 branch：{Branch}", fixBranch);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "無法取得 PR #{Num} 的 branch name", prNum);
                }
            }
        }

        var userMessage = BuildPlanUserMessage(task, owner, repo, repoTree, issueUrls, uiSpecPath, isFixLoop, fixBranch, veraReview);

        for (var attempt = 1; attempt <= 2; attempt++)
        {
            var response = await provider.CompleteAsync(systemPrompt, userMessage, cancellationToken);
            var plan = TryParsePlan(response.Content);
            if (plan is not null)
            {
                logger.LogInformation("Dev Agent 計畫解析成功（第 {Attempt} 次）", attempt);
                return plan;
            }
            logger.LogWarning("Dev Agent 計畫格式錯誤（第 {Attempt} 次）：{Content}", attempt, response.Content);
        }

        return new DevPlan
        {
            Summary = "Dev Agent 無法解析任務，請查看 log。",
            TaskType = "unknown",
            BranchName = "",
            FilesToModify = [],
            CommitMessage = ""
        };
    }

    /// <summary>
    /// 執行任務：Clone repo、建立 branch、驅動 Claude Code 自主實作並確認 build 通過、
    /// 然後 Commit、Push、開 PR。
    /// Stage 11：核心程式碼產出改由 Claude Code CLI 負責，取代原本的 LLM 一次性輸出。
    /// </summary>
    public async Task<string> ExecuteAsync(
        TaskItem task,
        DevPlan plan,
        string owner,
        string repo,
        CancellationToken cancellationToken = default)
    {
        var logEntry = new TaskLog
        {
            TaskId = task.Id,
            Agent = "Dev",
            Step = "開始執行",
            Status = "running"
        };
        taskRepository.AddLog(logEntry);
        await taskRepository.SaveAsync(cancellationToken);

        await dashboardPush.PushAgentStatusAsync(new AgentStatusViewModel
        {
            AgentName        = "Dev",
            Status           = "running",
            CurrentTaskTitle = task.Title,
            LastUpdated      = DateTime.UtcNow
        });

        var localPath = "";

        try
        {
            if (plan.TaskType == "code_review")
                return await ExecuteCodeReviewAsync(task, plan, owner, repo, cancellationToken);

            // Clone / Pull
            localPath = gitHubService.CloneOrPull(owner, repo, task.Id.ToString("N")[..8]);
            AddLog(task, "Git Clone/Pull 完成", "done");

            // 建立 feature branch
            gitHubService.CreateAndCheckoutBranch(localPath, plan.BranchName);
            AddLog(task, $"Branch {plan.BranchName} 已建立", "done");

            // 驅動 Claude Code 自主實作（探索 repo → 寫碼 → dotnet restore → dotnet build → 修錯）
            await RunClaudeCodeAsync(task, plan, localPath, cancellationToken);
            AddLog(task, "Claude Code 程式碼實作完成（build 通過）", "done");

            // Commit + Push
            gitHubService.CommitAll(localPath, plan.CommitMessage);
            gitHubService.Push(localPath, plan.BranchName);
            AddLog(task, "Commit & Push 完成", "done");

            // 開 PR
            var prBody = $"""
                ## 任務說明
                {task.Title}

                ## 變更摘要
                {plan.Summary}

                ---
                🤖 由 AiTeam Dev Agent（Claude Code）自動產出
                """;

            var prUrl = await gitHubService.OpenPullRequestAsync(
                owner, repo, task.Title, prBody, plan.BranchName);

            AddLog(task, $"PR 已開啟：{prUrl}", "done");
            taskRepository.UpdateStatus(task, "done");
            await taskRepository.SaveAsync(cancellationToken);

            await dashboardPush.PushAgentStatusAsync(new AgentStatusViewModel
            {
                AgentName   = "Dev",
                Status      = "idle",
                LastUpdated = DateTime.UtcNow
            });

            await dashboardPush.PushTaskUpdateAsync(new TaskUpdateViewModel
            {
                TaskId    = task.Id,
                Title     = task.Title,
                Status    = "done",
                AgentName = "Dev"
            });

            return prUrl;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Dev Agent 執行任務失敗：{Title}", task.Title);
            AddLog(task, $"執行失敗：{ex.Message}", "failed");
            taskRepository.UpdateStatus(task, "failed");
            await taskRepository.SaveAsync(cancellationToken);

            await dashboardPush.PushAgentStatusAsync(new AgentStatusViewModel
            {
                AgentName   = "Dev",
                Status      = "error",
                LastUpdated = DateTime.UtcNow
            });

            await dashboardPush.PushTaskUpdateAsync(new TaskUpdateViewModel
            {
                TaskId    = task.Id,
                Title     = task.Title,
                Status    = "failed",
                AgentName = "Dev"
            });

            throw;
        }
        finally
        {
            if (!string.IsNullOrEmpty(localPath))
                gitHubService.CleanupLocalRepo(localPath);
        }
    }

    // ────────────── Private ──────────────

    /// <summary>
    /// 驅動 Claude Code CLI 自主完成程式碼實作，並確認 dotnet build 通過。
    /// Stage 11 核心：取代原本的 ApplyCodeChangesAsync（LLM 一次性輸出）。
    /// </summary>
    private async Task RunClaudeCodeAsync(
        TaskItem task,
        DevPlan plan,
        string localPath,
        CancellationToken cancellationToken)
    {
        // 將 Cody 專用 CLAUDE.md 模板寫入 repo 根目錄，執行完後還原
        // （避免 CLAUDE.md 的替換被 GitHubService 一起 commit 進 PR）
        var claudeMdPath  = Path.Combine(localPath, "CLAUDE.md");
        var templatePath  = Path.Combine(AppContext.BaseDirectory, "Resources", "CLAUDE_CODY.md");
        var originalClaudeMd = File.Exists(claudeMdPath)
            ? await File.ReadAllTextAsync(claudeMdPath, cancellationToken)
            : null;

        if (File.Exists(templatePath))
        {
            var claudeMdContent = await File.ReadAllTextAsync(templatePath, cancellationToken);
            await File.WriteAllTextAsync(claudeMdPath, claudeMdContent, cancellationToken);
            logger.LogInformation("CLAUDE.md 已寫入 repo 根目錄");
        }
        else
        {
            logger.LogWarning("CLAUDE_CODY.md 模板不存在於 {Path}，略過寫入", templatePath);
        }

        // 解析 Vera 報告（fix loop 時提供）
        ParseDescriptionMeta(task.Description,
            out _, out _, out var isFixLoop, out var veraReview);

        // 組建給 Claude Code 的任務 prompt
        var prompt = BuildClaudeCodePrompt(task, plan, isFixLoop, veraReview);

        // 從設定讀取模型名稱（與其他 Agent 一致，不寫死）
        var model = configuration["Agents:Dev:Model"]
                 ?? configuration["Anthropic:DefaultModel"]
                 ?? "claude-opus-4-6";
        var apiKey = configuration["Anthropic:ApiKey"] ?? "";

        AddLog(task, $"啟動 Claude Code（model={model}）", "running");

        ClaudeCodeResult result;
        try
        {
            result = await claudeCodeService.RunAsync(localPath, prompt, model, apiKey, cancellationToken);
        }
        finally
        {
            // 不論成功或失敗，還原 CLAUDE.md 至原始內容（或刪除若原本不存在）
            if (originalClaudeMd is not null)
                await File.WriteAllTextAsync(claudeMdPath, originalClaudeMd, CancellationToken.None);
            else if (File.Exists(claudeMdPath))
                File.Delete(claudeMdPath);
        }

        if (!result.Success)
        {
            var msg = string.IsNullOrWhiteSpace(result.Output)
                ? $"Claude Code 執行失敗（exit code={result.ExitCode}）"
                : $"Claude Code 執行失敗：{result.Output}";
            throw new InvalidOperationException(msg);
        }

        logger.LogInformation("Claude Code 執行完成：{Summary}", result.Output);
        AddLog(task, $"Claude Code 完成：{result.Output[..Math.Min(200, result.Output.Length)]}", "done");
    }

    private static string BuildClaudeCodePrompt(
        TaskItem task,
        DevPlan plan,
        bool isFixLoop,
        string? veraReview)
    {
        var veraSection = isFixLoop && !string.IsNullOrWhiteSpace(veraReview)
            ? $"""

              ## ⚠️ Vera 審查報告（你必須修改以下 🔴 必須修改的項目）
              {veraReview}

              **重點：只修改報告中 🔴 必須修改的項目，不要重寫整個檔案，不要修改其他檔案。**
              """
            : "";

        var fixLoopHint = isFixLoop
            ? "\n⚠️ 這是 Vera 審查後的修復迭代，不要修改已通過的部分，只修正 Vera 指出的問題。\n"
            : "";

        return $"""
            ## 任務
            標題：{task.Title}
            計畫摘要：{plan.Summary}
            {fixLoopHint}
            ## 任務描述
            {task.Description ?? task.Title}
            {veraSection}
            ## 執行要求
            1. 先探索 repo 結構，理解相關程式碼
            2. 實作上述任務所需的程式碼變更
            3. 執行 `dotnet restore`（必須先 restore 再 build，避免找不到套件的假錯誤）
            4. 執行 `dotnet build` 確認編譯通過
            5. 若有編譯錯誤，修復後再次執行 build
            6. 確認 build 通過後結束任務
            7. **不要 commit 或 push**（外部流程會處理）
            """;
    }

    private async Task<string> ExecuteCodeReviewAsync(
        TaskItem task, DevPlan plan,
        string owner, string repo,
        CancellationToken cancellationToken)
    {
        var provider = providerFactory.Create("Dev");
        var files = await gitHubService.ListFilesAsync(owner, repo);
        var fileList = string.Join("\n", files.Select(f => $"- {f.Path}"));

        var reviewPrompt = $"""
            請對以下 repo 進行 Code Review：

            Repo：{owner}/{repo}
            任務：{task.Title}
            描述：{plan.Summary}

            ## 檔案清單
            {fileList}

            請提供：
            1. 整體架構評估
            2. 潛在問題與風險
            3. 改善建議（具體到檔案與行號）
            """;

        var response = await provider.CompleteAsync(
            "你是資深軟體工程師，專精 C# / .NET / Blazor，負責 Code Review。",
            reviewPrompt, cancellationToken);

        AddLog(task, "Code Review 完成", "done");
        taskRepository.UpdateStatus(task, "done");
        await taskRepository.SaveAsync(cancellationToken);

        return response.Content;
    }

    private void AddLog(TaskItem task, string step, string status)
    {
        taskRepository.AddLog(new TaskLog
        {
            TaskId = task.Id,
            Agent = "Dev",
            Step = step,
            Status = status
        });
    }

    private static string BuildSystemPrompt(IReadOnlyList<string> rules)
    {
        var ruleList = rules.Count > 0
            ? string.Join("\n", rules.Select(r => $"- {r}"))
            : "（尚無規則）";

        return $$"""
            你是 AI 軟體團隊的 Dev Agent，專精 C# / .NET / Blazor / EF Core。
            你的任務是分析 CEO 分派的任務，產出執行計畫 JSON。

            ## 規則清單
            {{ruleList}}

            ## 重要規則
            - 若任務訊息中提供了 `fix_branch`，`branch_name` 欄位必須**完全照用**該值，一字不差，不得自行修改或重新命名。
            - `files_to_modify` 中**絕對不能**包含 `tests/` 或 `test/` 目錄下的任何檔案。
            - `files_to_modify` 中的路徑必須是 Repo 結構中**確實存在**的檔案，不要憑空新增全新的 Razor 頁面或服務類別。
            - 只修改生產程式碼（Production code），不要新增或修改測試檔案。

            ## 回應格式（只回傳 JSON，不要加任何說明）
            {
              "task_type": "bug_fix | feature | refactor | code_review",
              "branch_name": "feature/xxx 或 fix/xxx",
              "files_to_modify": ["路徑/檔案.cs"],
              "commit_message": "fix: 修復 xxx 問題",
              "summary": "簡要說明將做什麼"
            }
            """;
    }

    private static string BuildPlanUserMessage(
        TaskItem task,
        string owner,
        string repo,
        string repoTree,
        string? issueUrls,
        string? uiSpecPath,
        bool isFixLoop,
        string? fixBranch = null,
        string? veraReview = null)
    {
        var issueSection  = string.IsNullOrWhiteSpace(issueUrls)  ? "（無）" : issueUrls;
        var uiSpecSection = string.IsNullOrWhiteSpace(uiSpecPath) ? "（無）" : uiSpecPath;
        var fixLoopHint   = isFixLoop
            ? $"\n⚠️ 這是 Vera 審查後的修復迭代，不要建立新 PR。\n" +
              $"fix_branch（branch_name 必須完全照用此值）：`{fixBranch ?? "（查詢失敗，請沿用原 PR 的 branch）"}`"
            : "";

        var veraSection = string.IsNullOrWhiteSpace(veraReview)
            ? ""
            : $"""

            ## ⚠️ Vera 審查報告（你必須根據以下問題修改，只修改被指出的檔案和問題）
            {veraReview}

            **重點：只修改上述報告中 🔴 必須修改的項目，不要重寫整個檔案，不要修改其他檔案。**
            """;

        return $"""
            ## 任務
            標題：{task.Title}
            Repo：{owner}/{repo}
            觸發來源：{task.TriggeredBy}{fixLoopHint}

            ## 任務描述
            {task.Description ?? task.Title}
            {veraSection}
            ## Repo 結構（2 層，供選擇修改檔案參考）
            {repoTree}

            ## 相關 GitHub Issues
            {issueSection}

            ## UI 規格文件路徑
            {uiSpecSection}

            請根據以上資訊產出執行計畫 JSON。
            files_to_modify 中的路徑必須是上方 Repo 結構中**確實存在**的路徑。
            """;
    }

    /// <summary>
    /// 解析 task.Description 中由 Orchestrator 附加的 metadata block。
    /// 格式：
    /// ---
    /// issue_urls: ...
    /// ui_spec_path: ...
    /// fix_loop: true
    /// ---
    /// </summary>
    private static void ParseDescriptionMeta(
        string? description,
        out string? issueUrls,
        out string? uiSpecPath,
        out bool isFixLoop,
        out string? veraReview)
    {
        issueUrls  = null;
        uiSpecPath = null;
        isFixLoop  = false;
        veraReview = null;

        if (string.IsNullOrWhiteSpace(description)) return;

        var lines = description.Split('\n');
        var inMeta = false;
        var inVeraReview = false;
        var reviewLines = new List<string>();

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed == "---")
            {
                if (inVeraReview) inVeraReview = false; // 結束 vera_review 區段
                inMeta = !inMeta;
                continue;
            }
            if (!inMeta) continue;

            // vera_review 是多行區段，從 "vera_review:" 開始到下一個 key 或 ---
            if (inVeraReview)
            {
                // 遇到其他 key 時結束 vera_review 收集
                if (trimmed.StartsWith("issue_urls:") || trimmed.StartsWith("ui_spec_path:")
                    || trimmed.StartsWith("fix_loop:"))
                {
                    inVeraReview = false;
                    // 繼續往下判斷這行本身是哪個 key
                }
                else
                {
                    reviewLines.Add(line);
                    continue;
                }
            }

            if (trimmed.StartsWith("vera_review:"))
            {
                inVeraReview = true;
                var remainder = trimmed["vera_review:".Length..].Trim();
                if (!string.IsNullOrEmpty(remainder))
                    reviewLines.Add(remainder);
            }
            else if (trimmed.StartsWith("issue_urls:"))
                issueUrls = trimmed["issue_urls:".Length..].Trim();
            else if (trimmed.StartsWith("ui_spec_path:"))
                uiSpecPath = trimmed["ui_spec_path:".Length..].Trim();
            else if (trimmed.StartsWith("fix_loop:"))
                isFixLoop = trimmed.Contains("true", StringComparison.OrdinalIgnoreCase);
        }

        if (reviewLines.Count > 0)
            veraReview = string.Join("\n", reviewLines).Trim();
    }

    /// <summary>從描述文字中提取 PR 編號（支援 /pull/123 與 PR #123 兩種格式）。</summary>
    private static int ExtractPrNumberFromText(string text)
    {
        var match = Regex.Match(text, @"/pull/(\d+)", RegexOptions.IgnoreCase);
        if (match.Success && int.TryParse(match.Groups[1].Value, out var n1)) return n1;
        match = Regex.Match(text, @"PR\s*#(\d+)", RegexOptions.IgnoreCase);
        return match.Success && int.TryParse(match.Groups[1].Value, out var n2) ? n2 : 0;
    }

    /// <inheritdoc />
    public async Task<AgentExecutionResult> ExecuteTaskAsync(
        TaskItem task,
        string owner,
        string repo,
        IReadOnlyList<string> rules,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var plan = await BuildPlanAsync(task, owner, repo, rules, cancellationToken);
            var prUrl = await ExecuteAsync(task, plan, owner, repo, cancellationToken);
            return new AgentExecutionResult(true, $"PR 已開啟：{prUrl}", prUrl);
        }
        catch (Exception ex)
        {
            return new AgentExecutionResult(false, $"Dev Agent 執行失敗：{ex.Message}");
        }
    }

    private static DevPlan? TryParsePlan(string content)
    {
        try
        {
            var start = content.IndexOf('{');
            var end = content.LastIndexOf('}');
            if (start < 0 || end < 0) return null;
            var json = content[start..(end + 1)];
            return JsonSerializer.Deserialize<DevPlan>(json, JsonOptions);
        }
        catch { return null; }
    }
}

/// <summary>
/// Dev Agent 產出的執行計畫。
/// </summary>
public class DevPlan
{
    [System.Text.Json.Serialization.JsonPropertyName("task_type")]
    public string TaskType { get; set; } = "";

    [System.Text.Json.Serialization.JsonPropertyName("branch_name")]
    public string BranchName { get; set; } = "";

    [System.Text.Json.Serialization.JsonPropertyName("files_to_modify")]
    public List<string> FilesToModify { get; set; } = [];

    [System.Text.Json.Serialization.JsonPropertyName("commit_message")]
    public string CommitMessage { get; set; } = "";

    [System.Text.Json.Serialization.JsonPropertyName("summary")]
    public string Summary { get; set; } = "";
}
