using System.Text;
using AiTeam.Bot.GitHub;
using AiTeam.Bot.Services;
using AiTeam.Data;
using AiTeam.Data.Repositories;
using AiTeam.Shared.ViewModels;
using Microsoft.Extensions.Configuration;

namespace AiTeam.Bot.Agents;

/// <summary>
/// Designer Agent（Demi）：將功能需求轉換為 MudBlazor UI 規格文件（Markdown）。
/// Stage 12：改用 Claude Code 唯讀模式探索現有頁面結構，規格不再 commit 到 GitHub（改存 DB）。
/// </summary>
public class DesignerAgentService(
    LlmProviderFactory providerFactory,
    GitHubService gitHubService,
    TaskRepository taskRepository,
    DashboardPushService dashboardPush,
    ClaudeCodeService claudeCodeService,
    IConfiguration configuration,
    ILogger<DesignerAgentService> logger) : IAgentExecutor
{
    private const string AgentName = "Designer";

    /// <inheritdoc />
    public async Task<AgentExecutionResult> ExecuteTaskAsync(
        TaskItem task,
        string owner,
        string repo,
        IReadOnlyList<string> rules,
        CancellationToken cancellationToken = default)
    {
        AddLog(task, "Designer Agent 開始執行", "running");
        await taskRepository.SaveAsync(cancellationToken);
        await PushStatus("running", task.Id, task.Title);

        var localPath = "";
        try
        {
            // Clone repo 供 Claude Code 唯讀探索使用
            localPath = gitHubService.CloneOrPull(owner, repo, $"demi-{task.Id:N}"[..8]);
            AddLog(task, "Git Clone/Pull 完成", "running");

            var markdown = await GenerateDraftAsync(
                task.Title,
                task.Description ?? task.Title,
                rules,
                localPath,
                cancellationToken: cancellationToken);

            if (string.IsNullOrWhiteSpace(markdown))
                return Fail(task, "未能產出 UI 規格內容。");

            AddLog(task, "UI 規格文件產出完成", "running");
            await taskRepository.SaveAsync(cancellationToken);

            // 將 Markdown 存入 TaskLog.Payload，供 CommandHandler 傳送至 Discord
            task.Logs.Add(new TaskLog
            {
                TaskId    = task.Id,
                Agent     = AgentName,
                Step      = "ui-spec-output",
                Status    = "done",
                Payload   = System.Text.Json.JsonSerializer.Serialize(new { markdown }),
                CreatedAt = DateTime.UtcNow
            });
            await taskRepository.SaveAsync(cancellationToken);

            // Stage 12：不再 commit UI 規格到 GitHub，改由 TaskGroupService 存入 DB
            // Dev Agent 的 Claude Code 會在 PR 中自行加入 docs/ui-specs/ 規格文件
            var summary = $"UI 規格文件已完成（{task.Title}）";
            AddLog(task, summary, "done");
            await taskRepository.SaveAsync(cancellationToken);
            await PushStatus("done", task.Id, task.Title);

            return new AgentExecutionResult(true, summary, OutputContent: markdown);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Designer Agent 執行失敗（TaskId={Id}）", task.Id);
            AddLog(task, $"執行失敗：{ex.Message}", "failed");
            await taskRepository.SaveAsync(cancellationToken);
            await PushStatus("failed", task.Id, task.Title);
            return Fail(task, ex.Message);
        }
        finally
        {
            if (!string.IsNullOrEmpty(localPath))
                gitHubService.CleanupLocalRepo(localPath);
        }
    }

    /// <summary>
    /// 草稿模式：使用 Claude Code 唯讀探索 codebase 後產出 UI 規格 Markdown。
    /// Stage 12：不建立 GitHub PR，不 commit 任何檔案。
    /// </summary>
    /// <param name="title">功能標題</param>
    /// <param name="description">功能描述</param>
    /// <param name="rules">設計規則</param>
    /// <param name="repoLocalPath">已 clone 的 repo 本地路徑（由 ShowProposalAsync 統一管理）</param>
    /// <param name="rosaIssues">Rosa 產出的 Issues（讓 Demi 確保規格涵蓋所有功能點）</param>
    /// <param name="images">老闆附的圖片（若有，透過 LLM 轉為文字描述）</param>
    /// <param name="previousUiSpec">✏️ 調整時帶入第一版規格（提示修改而非重做）</param>
    /// <param name="cancellationToken">CancellationToken</param>
    internal async Task<string> GenerateDraftAsync(
        string title,
        string description,
        IReadOnlyList<string> rules,
        string? repoLocalPath = null,
        IReadOnlyList<RequirementIssuePreview>? rosaIssues = null,
        IReadOnlyList<ImageAttachment>? images = null,
        string? previousUiSpec = null,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrEmpty(repoLocalPath))
        {
            var markdown = await RunClaudeCodeDesignAsync(
                title, description, rules, repoLocalPath, rosaIssues, images, previousUiSpec, cancellationToken);
            if (!string.IsNullOrWhiteSpace(markdown)) return markdown;

            logger.LogWarning("Claude Code 唯讀設計失敗，改用 LLM 直接呼叫");
        }

        // Fallback：直接呼叫 LLM
        return await GenerateWithLlmAsync(title, description, rules, rosaIssues, images, cancellationToken);
    }

    // ────────────── Claude Code 唯讀設計 ──────────────

    private async Task<string> RunClaudeCodeDesignAsync(
        string title,
        string description,
        IReadOnlyList<string> rules,
        string repoLocalPath,
        IReadOnlyList<RequirementIssuePreview>? rosaIssues,
        IReadOnlyList<ImageAttachment>? images,
        string? previousUiSpec,
        CancellationToken cancellationToken)
    {
        var claudeMdPath     = Path.Combine(repoLocalPath, "CLAUDE.md");
        var templatePath     = Path.Combine(AppContext.BaseDirectory, "Resources", "CLAUDE_Demi.md");
        var originalClaudeMd = File.Exists(claudeMdPath)
            ? await File.ReadAllTextAsync(claudeMdPath, cancellationToken)
            : null;

        try
        {
            if (File.Exists(templatePath))
                await File.WriteAllTextAsync(claudeMdPath,
                    await File.ReadAllTextAsync(templatePath, cancellationToken), cancellationToken);

            var prompt = await BuildClaudeCodePromptAsync(
                title, description, rules, rosaIssues, images, previousUiSpec, cancellationToken);
            var model  = configuration["Agents:Designer:Model"]
                      ?? configuration["Anthropic:DefaultModel"]
                      ?? "claude-sonnet-4-6";
            var apiKey = configuration["Anthropic:ApiKey"] ?? "";

            var result = await claudeCodeService.RunReadOnlyAsync(
                repoLocalPath, prompt, model, apiKey, cancellationToken);

            if (!result.Success)
            {
                logger.LogWarning("Demi Claude Code 執行未成功（exitCode={Code}）", result.ExitCode);
                return "";
            }

            return result.Output.Trim();
        }
        finally
        {
            if (originalClaudeMd is not null)
                await File.WriteAllTextAsync(claudeMdPath, originalClaudeMd, CancellationToken.None);
            else if (File.Exists(claudeMdPath))
                File.Delete(claudeMdPath);
        }
    }

    private async Task<string> BuildClaudeCodePromptAsync(
        string title,
        string description,
        IReadOnlyList<string> rules,
        IReadOnlyList<RequirementIssuePreview>? rosaIssues,
        IReadOnlyList<ImageAttachment>? images,
        string? previousUiSpec,
        CancellationToken cancellationToken)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## 任務標題");
        sb.AppendLine(title);
        sb.AppendLine();
        sb.AppendLine("## 功能需求描述");
        sb.AppendLine(description);
        sb.AppendLine();

        // 圖片描述
        if (images?.Count > 0)
        {
            var imageDesc = await DescribeImagesAsync(images, cancellationToken);
            if (!string.IsNullOrWhiteSpace(imageDesc))
            {
                sb.AppendLine("## 老闆附圖說明");
                sb.AppendLine(imageDesc);
                sb.AppendLine();
            }
        }

        // Rosa 的 Issues（Demi 需確保規格涵蓋所有功能點）
        if (rosaIssues?.Count > 0)
        {
            sb.AppendLine("## Rosa 需求 Issues（UI 規格必須涵蓋以下所有功能點）");
            foreach (var issue in rosaIssues)
                sb.AppendLine($"- {issue.Title}");
            sb.AppendLine();
        }

        // 專案規則
        if (rules.Count > 0)
        {
            sb.AppendLine("## 專案設計規則");
            foreach (var rule in rules)
                sb.AppendLine($"- {rule}");
            sb.AppendLine();
        }

        // ✏️ 調整模式
        if (!string.IsNullOrWhiteSpace(previousUiSpec))
        {
            sb.AppendLine("## 第一版 UI 規格（請依老闆意見修改，不要重做）");
            sb.AppendLine(previousUiSpec);
            sb.AppendLine();
        }

        sb.AppendLine("## 你的任務");
        if (!string.IsNullOrWhiteSpace(previousUiSpec))
            sb.AppendLine("基於第一版 UI 規格和老闆意見進行修改，探索相關 .razor 頁面後輸出修改後的完整 Markdown 規格文件。");
        else
            sb.AppendLine("探索 codebase 中相關的 .razor 頁面與 MudBlazor 元件使用方式，然後輸出完整的 Markdown UI 規格文件（含六個區塊）。直接輸出 Markdown，不加額外說明。");

        return sb.ToString();
    }

    // ────────────── LLM 直呼叫（Fallback） ──────────────

    private async Task<string> GenerateWithLlmAsync(
        string title,
        string description,
        IReadOnlyList<string> rules,
        IReadOnlyList<RequirementIssuePreview>? rosaIssues,
        IReadOnlyList<ImageAttachment>? images,
        CancellationToken cancellationToken)
    {
        var provider     = providerFactory.Create(AgentName);
        var systemPrompt = BuildDesignerSystemPrompt(rules, rosaIssues);
        var userMessage  = BuildDesignerUserMessage(title, description);
        var response     = await provider.CompleteAsync(systemPrompt, userMessage, cancellationToken, images);
        return response.Content.Trim();
    }

    /// <summary>
    /// 透過 LLM 將圖片轉為文字描述，供 Claude Code prompt 使用。
    /// </summary>
    private async Task<string> DescribeImagesAsync(
        IReadOnlyList<ImageAttachment> images,
        CancellationToken cancellationToken)
    {
        try
        {
            var provider = providerFactory.Create(AgentName);
            var response = await provider.CompleteAsync(
                "你是一位 UI/UX 設計師，請簡潔描述圖片中的頁面結構、UI 元件配置或設計問題（100-200 字）。",
                "請描述圖片內容，重點放在 UI 設計相關的資訊。",
                cancellationToken,
                images);
            return response.Content.Trim();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "圖片描述轉換失敗，略過");
            return "";
        }
    }

    // ────────────── Prompt 建構 ──────────────

    private static string BuildDesignerSystemPrompt(
        IReadOnlyList<string> rules,
        IReadOnlyList<RequirementIssuePreview>? rosaIssues)
    {
        var ruleList = rules.Count > 0
            ? string.Join("\n", rules.Select(r => $"- {r}"))
            : "（尚無額外規則）";

        var issueSection = rosaIssues?.Count > 0
            ? $"""

            ## Rosa 需求 Issues（規格必須涵蓋以下所有功能點）
            {string.Join("\n", rosaIssues.Select(i => $"- {i.Title}"))}
            """
            : "";

        return $$"""
            你是 UI/UX 規格設計師 Demi，負責把功能需求轉換為具體可實作的 UI 規格文件。
            你的輸出是給 Dev Agent（開發工程師）閱讀的技術規格，而非給終端使用者看的文件。

            ## 你熟悉的技術棧
            - Blazor Server（InteractiveServer render mode）
            - MudBlazor 8.x 元件庫（MudDataGrid、MudDialog、MudForm、MudChart 等）
            - C# / .NET / EF Core
            - SignalR（即時資料更新）

            ## 輸出規格
            規格文件必須包含以下區塊（Markdown 格式）：
            1. **頁面目的**：一句話說明這個頁面/功能要解決什麼問題
            2. **頁面結構**：描述頁面有哪些區塊、如何排版（文字描述即可）
            3. **元件規格**：列出每個 MudBlazor 元件、重要 Props、預期行為
            4. **資料來源**：每個資料從哪裡取得（API、SignalR、props 等）
            5. **互動行為**：使用者操作後系統的回應（篩選、刪除、確認對話框等）
            6. **注意事項**：Blazor Server 特殊考量、電路隔離、效能注意
            {{issueSection}}

            ## 限制
            - 禁止輸出程式碼，只輸出規格描述
            - 不做視覺設計（顏色、字體、品牌）
            - 以「可實作」為前提，不做超出目前技術棧的設計

            ## 專案規則
            {{ruleList}}

            ## 輸出語言
            使用繁體中文，專有名詞（元件名稱、Props）保留英文。
            """;
    }

    private static string BuildDesignerUserMessage(string title, string description)
        => $"""
            ## 任務標題
            {title}

            ## 功能需求描述
            {description}

            請依據以上需求，產出完整的 UI 規格文件。
            """;

    // ────────────── 輔助工具 ──────────────

    private void AddLog(TaskItem task, string step, string status)
        => task.Logs.Add(new TaskLog
        {
            TaskId    = task.Id,
            Agent     = AgentName,
            Step      = step,
            Status    = status,
            CreatedAt = DateTime.UtcNow
        });

    private async Task PushStatus(string status, Guid taskId, string title)
        => await dashboardPush.PushTaskUpdateAsync(new TaskUpdateViewModel
        {
            TaskId    = taskId,
            Title     = title,
            AgentName = AgentName,
            Status    = status
        });

    private static AgentExecutionResult Fail(TaskItem task, string message)
        => new(false, message);
}
