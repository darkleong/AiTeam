using System.Text;
using AiTeam.Bot.GitHub;
using AiTeam.Bot.Services;
using AiTeam.Data;
using AiTeam.Data.Repositories;
using AiTeam.Shared.ViewModels;

namespace AiTeam.Bot.Agents;

/// <summary>
/// Designer Agent（Demi）：將功能需求轉換為 MudBlazor UI 規格文件（Markdown）。
/// 純文字輸出型 Agent，不需要 Git Clone 操作。
/// Stage 10 起一律將規格文件提交到 GitHub（Orchestrator 需要永久連結）。
/// </summary>
public class DesignerAgentService(
    LlmProviderFactory providerFactory,
    GitHubService gitHubService,
    TaskRepository taskRepository,
    DashboardPushService dashboardPush,
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

        try
        {
            // 1. 呼叫 LLM 產出 UI 規格文件
            var provider     = providerFactory.Create(AgentName);
            var systemPrompt = BuildDesignerSystemPrompt(rules);
            var userMessage  = BuildDesignerUserMessage(task);

            var response = await provider.CompleteAsync(systemPrompt, userMessage, cancellationToken);
            var markdown = response.Content.Trim();

            if (string.IsNullOrWhiteSpace(markdown))
                return Fail(task, "LLM 未產出任何 UI 規格內容。");

            AddLog(task, "UI 規格文件產出完成", "running");
            await taskRepository.SaveAsync(cancellationToken);

            // 2. 將 Markdown 存入 TaskLog.Payload，供 CommandHandler 傳送至 Discord
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

            // 3. 一律提交規格文件到 GitHub（Stage 10：Orchestrator 需要規格文件連結）
            var slug  = ToSlug(task.Title);
            var path  = $"docs/ui-specs/{slug}.md";
            string? prUrl = null;

            try
            {
                await gitHubService.CreateOrUpdateFileAsync(
                    owner, repo, path, markdown,
                    $"docs: add UI spec - {task.Title}");
                prUrl = $"https://github.com/{owner}/{repo}/blob/main/{path}";
                AddLog(task, $"UI 規格文件已提交：{path}", "done");
                await taskRepository.SaveAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "提交 UI 規格文件至 GitHub 失敗，仍回傳規格內容");
            }

            var summary = prUrl is not null
                ? $"UI 規格文件已完成並提交至 GitHub（{task.Title}）"
                : $"UI 規格文件已完成（{task.Title}）";

            AddLog(task, summary, "done");
            await taskRepository.SaveAsync(cancellationToken);
            await PushStatus("done", task.Id, task.Title);

            return new AgentExecutionResult(true, summary, prUrl);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Designer Agent 執行失敗（TaskId={Id}）", task.Id);
            AddLog(task, $"執行失敗：{ex.Message}", "failed");
            await taskRepository.SaveAsync(cancellationToken);
            await PushStatus("failed", task.Id, task.Title);
            return Fail(task, ex.Message);
        }
    }

    /// <summary>
    /// 草稿模式：僅呼叫 LLM 產出 UI 規格 Markdown，不建立 GitHub PR。
    /// 供 CEO 提案模式使用。
    /// </summary>
    internal async Task<string> GenerateDraftAsync(
        string title,
        string description,
        IReadOnlyList<string> rules,
        CancellationToken cancellationToken = default)
    {
        var provider     = providerFactory.Create(AgentName);
        var systemPrompt = BuildDesignerSystemPrompt(rules);
        var userMessage  = BuildDesignerUserMessage(new TaskItem { Title = title, Description = description });
        var response     = await provider.CompleteAsync(systemPrompt, userMessage, cancellationToken);
        return response.Content.Trim();
    }

    // ────────────── Prompt 建構 ──────────────

    private static string BuildDesignerSystemPrompt(IReadOnlyList<string> rules)
    {
        var ruleList = rules.Count > 0
            ? string.Join("\n", rules.Select(r => $"- {r}"))
            : "（尚無額外規則）";

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

    private static string BuildDesignerUserMessage(TaskItem task)
        => $"""
            ## 任務標題
            {task.Title}

            ## 功能需求描述
            {task.Description ?? task.Title}

            請依據以上需求，產出完整的 UI 規格文件。
            """;

    // ────────────── 輔助工具 ──────────────

    private static string ToSlug(string title)
    {
        var sb = new StringBuilder();
        foreach (var c in title.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c))  sb.Append(c);
            else if (c == ' ' || c == '-') sb.Append('-');
        }
        var slug = sb.ToString().Trim('-');
        return slug.Length > 60 ? slug[..60] : slug;
    }

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
