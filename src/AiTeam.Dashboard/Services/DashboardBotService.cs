using System.Net.Http.Json;

namespace AiTeam.Dashboard.Services;

/// <summary>
/// 呼叫 Bot 內部 API（重啟等管理操作）。
/// </summary>
public class DashboardBotService(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<DashboardBotService> logger)
{
    private readonly string _botInternalUrl  = configuration["Bot:InternalUrl"]  ?? "http://aiteam-bot:8080";
    private readonly string _botInternalKey  = configuration["Bot:InternalApiKey"] ?? "";

    /// <summary>呼叫 /internal/reload-cache，清除 Bot 端 Cache，回傳是否成功。</summary>
    public async Task<bool> ReloadCacheAsync(string scope = "all", CancellationToken cancellationToken = default)
    {
        try
        {
            var client = httpClientFactory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Post,
                $"{_botInternalUrl.TrimEnd('/')}/internal/reload-cache?scope={scope}");
            request.Headers.Add("X-Api-Key", _botInternalKey);
            var response = await client.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
            logger.LogInformation("Bot 快取套用指令已送出（scope={Scope}）", scope);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "送出 Bot 快取套用指令失敗");
            return false;
        }
    }

    /// <summary>呼叫 /internal/tasks/{taskId}/requeue，將失敗 / 取消的任務重新入佇列。</summary>
    public async Task<bool> RequeueTaskAsync(Guid taskId, CancellationToken cancellationToken = default)
    {
        try
        {
            var client = httpClientFactory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Post,
                $"{_botInternalUrl.TrimEnd('/')}/internal/tasks/{taskId}/requeue");
            request.Headers.Add("X-Api-Key", _botInternalKey);
            var response = await client.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
            logger.LogInformation("TaskItem {Id} 重新入佇列指令已送出", taskId);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "送出重新入佇列指令失敗（TaskId={Id}）", taskId);
            return false;
        }
    }

    /// <summary>
    /// Stage 32：呼叫 /internal/mock/scenario，觸發 Mock 情境（fire-and-forget）。
    /// scenario 對應 Discord /mock workflow 選項（new_feature / bug_fix / tech_improvement /
    /// new_feature_with_proposal / fail_review / fail_qa / fail_dev_plan / review_skipped /
    /// dev_plan_fail_retry / dev_plan_fail_escalate / dev_failed_intervention /
    /// qa_failed_fix_then_intervention）— Stage 39 / 43 擴充。
    /// 字串純透傳到 Bot /internal/mock/scenario，由 MockScenarioService 處理；新增場景無需改動此 Service。
    /// </summary>
    public async Task<bool> TriggerMockScenarioAsync(
        string scenario, string? title, string? project, CancellationToken cancellationToken = default)
    {
        try
        {
            var client = httpClientFactory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Post,
                $"{_botInternalUrl.TrimEnd('/')}/internal/mock/scenario");
            request.Headers.Add("X-Api-Key", _botInternalKey);
            request.Content = JsonContent.Create(new { scenario, title, project });
            var response = await client.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
            logger.LogInformation("Mock 情境觸發指令已送出（scenario={Scenario}）", scenario);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "送出 Mock 情境觸發指令失敗（scenario={Scenario}）", scenario);
            return false;
        }
    }

    /// <summary>Stage 33：暫停指定 Agent 佇列消費（Dashboard 用）。</summary>
    public Task<bool> PauseAgentAsync(string agent, CancellationToken cancellationToken = default)
        => PostQueueControlAsync($"queue/{agent}/pause", $"pause {agent}", cancellationToken);

    /// <summary>Stage 33：恢復指定 Agent 佇列消費（Dashboard 用）。</summary>
    public Task<bool> ResumeAgentAsync(string agent, CancellationToken cancellationToken = default)
        => PostQueueControlAsync($"queue/{agent}/resume", $"resume {agent}", cancellationToken);

    /// <summary>Stage 33：緊急停止所有 Agent（Dashboard 用）。</summary>
    public Task<bool> StopAllAsync(CancellationToken cancellationToken = default)
        => PostQueueControlAsync("queue/stop-all", "stop-all", cancellationToken);

    /// <summary>Stage 33：恢復所有 Agent 佇列消費（Dashboard 用）。</summary>
    public Task<bool> ResumeAllAsync(CancellationToken cancellationToken = default)
        => PostQueueControlAsync("queue/resume-all", "resume-all", cancellationToken);

    private async Task<bool> PostQueueControlAsync(string path, string actionForLog, CancellationToken cancellationToken)
    {
        try
        {
            var client = httpClientFactory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Post,
                $"{_botInternalUrl.TrimEnd('/')}/internal/{path}");
            request.Headers.Add("X-Api-Key", _botInternalKey);
            var response = await client.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
            logger.LogInformation("佇列控制指令已送出（{Action}）", actionForLog);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "送出佇列控制指令失敗（{Action}）", actionForLog);
            return false;
        }
    }

    /// <summary>呼叫 /internal/restart，回傳是否成功。</summary>
    public async Task<bool> RestartBotAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var client = httpClientFactory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{_botInternalUrl.TrimEnd('/')}/internal/restart");
            request.Headers.Add("X-Api-Key", _botInternalKey);

            var response = await client.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
            logger.LogInformation("Bot 重啟指令已送出");
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "送出 Bot 重啟指令失敗");
            return false;
        }
    }
}
