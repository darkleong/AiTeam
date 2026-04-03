using System.Text.Json;
using System.Text.RegularExpressions;
using AiTeam.Data;
using AiTeam.Data.Repositories;
using AiTeam.Bot.GitHub;
using AiTeam.Bot.Services;
using AiTeam.Shared.ViewModels;

namespace AiTeam.Bot.Agents;

/// <summary>
/// Dev Agent：接收 CEO 分派的任務，分析後操作 GitHub repo（修改程式碼、開 PR）。
/// </summary>
public class DevAgentService(
    LlmProviderFactory providerFactory,
    GitHubService gitHubService,
    TaskRepository taskRepository,
    DashboardPushService dashboardPush,
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
            out var issueUrls, out var uiSpecPath, out var isFixLoop);

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

        var userMessage = BuildPlanUserMessage(task, owner, repo, repoTree, issueUrls, uiSpecPath, isFixLoop, fixBranch);

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
    /// 執行任務：依計畫 Clone repo、修改檔案、Commit、Push、開 PR。
    /// 回傳 PR URL。
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

            // 讓 LLM 產出修改後的程式碼並寫入檔案
            await ApplyCodeChangesAsync(task, plan, localPath, owner, repo, cancellationToken);
            AddLog(task, "程式碼修改完成", "done");

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

                ## 修改檔案
                {string.Join("\n", plan.FilesToModify.Select(f => $"- {f}"))}

                ---
                🤖 由 AiTeam Dev Agent 自動產出
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

    private async Task ApplyCodeChangesAsync(
        TaskItem task, DevPlan plan,
        string localPath, string owner, string repo,
        CancellationToken cancellationToken)
    {
        var provider = providerFactory.Create("Dev");

        foreach (var filePath in plan.FilesToModify)
        {
            var fullPath = Path.Combine(localPath, filePath.Replace('/', Path.DirectorySeparatorChar));

            // 讀取現有內容（若存在）
            var existingContent = File.Exists(fullPath) ? await File.ReadAllTextAsync(fullPath, cancellationToken) : "";

            var prompt = $"""
                任務：{task.Title}
                描述：{plan.Summary}
                檔案：{filePath}

                ## 現有程式碼
                ```
                {existingContent}
                ```

                請直接回傳修改後的完整程式碼，不要加任何說明文字或 markdown 格式。
                """;

            var response = await provider.CompleteAsync(
                "你是資深 C# 工程師，直接回傳修改後的完整程式碼，不加任何說明。",
                prompt, cancellationToken);

            // 去除 LLM 可能加上的 markdown 程式碼區塊標記（```csharp / ```）
            var content = StripCodeBlockMarkers(response.Content);

            // 防護：若 LLM 回傳的是對話文字而非程式碼，拋出例外避免污染檔案
            if (IsConversationalResponse(content))
                throw new InvalidOperationException(
                    $"LLM 回傳了說明文字而非程式碼（{filePath}）：{content[..Math.Min(120, content.Length)]}");

            // 確保目錄存在
            var dir = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            await File.WriteAllTextAsync(fullPath, content, cancellationToken);
            logger.LogInformation("已寫入 {File}", filePath);
        }
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
        string? fixBranch = null)
    {
        var issueSection  = string.IsNullOrWhiteSpace(issueUrls)  ? "（無）" : issueUrls;
        var uiSpecSection = string.IsNullOrWhiteSpace(uiSpecPath) ? "（無）" : uiSpecPath;
        var fixLoopHint   = isFixLoop
            ? $"\n⚠️ 這是 Vera 審查後的修復迭代，不要建立新 PR。\n" +
              $"fix_branch（branch_name 必須完全照用此值）：`{fixBranch ?? "（查詢失敗，請沿用原 PR 的 branch）"}`"
            : "";

        return $"""
            ## 任務
            標題：{task.Title}
            Repo：{owner}/{repo}
            觸發來源：{task.TriggeredBy}{fixLoopHint}

            ## 任務描述
            {task.Description ?? task.Title}

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
        out bool isFixLoop)
    {
        issueUrls  = null;
        uiSpecPath = null;
        isFixLoop  = false;

        if (string.IsNullOrWhiteSpace(description)) return;

        var lines = description.Split('\n');
        var inMeta = false;
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed == "---") { inMeta = !inMeta; continue; }
            if (!inMeta) continue;

            if (trimmed.StartsWith("issue_urls:"))
                issueUrls = trimmed["issue_urls:".Length..].Trim();
            else if (trimmed.StartsWith("ui_spec_path:"))
                uiSpecPath = trimmed["ui_spec_path:".Length..].Trim();
            else if (trimmed.StartsWith("fix_loop:"))
                isFixLoop = trimmed.Contains("true", StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// 去除 LLM 回傳內容中的 markdown 程式碼區塊標記（```csharp 或 ```）。
    /// </summary>
    private static string StripCodeBlockMarkers(string content)
    {
        content = content.Trim();
        if (!content.StartsWith("```")) return content;

        // 移除第一行（```csharp 或 ```）
        var firstNewline = content.IndexOf('\n');
        if (firstNewline >= 0)
            content = content[(firstNewline + 1)..].TrimStart('\r', '\n');

        // 移除結尾的 ```
        var trimmed = content.TrimEnd();
        if (trimmed.EndsWith("```"))
            content = trimmed[..trimmed.LastIndexOf("```")].TrimEnd();

        return content;
    }

    /// <summary>
    /// 偵測 LLM 是否回傳了對話文字而非程式碼。
    /// 若是，應拒絕寫入檔案。
    /// </summary>
    private static bool IsConversationalResponse(string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return true;

        var lower = content.TrimStart().ToLowerInvariant();

        // 常見的 LLM 詢問句開頭
        var conversationalPrefixes = new[]
        {
            "i need", "i would", "could you", "please provide", "based on",
            "to implement", "you need", "i don't have", "i cannot", "i can't",
            "unfortunately", "it seems", "the provided", "before i can",
            "in order to"
        };
        if (conversationalPrefixes.Any(p => lower.StartsWith(p))) return true;

        // 若完全沒有任何 C# 程式碼跡象
        var codeIndicators = new[]
        {
            "namespace ", "using ", "class ", "public ", "private ", "protected ",
            "internal ", "void ", "async ", "return ", "//", "/*", "@page", "@using",
            "<", "{", "}"
        };
        return !codeIndicators.Any(indicator => content.Contains(indicator));
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
