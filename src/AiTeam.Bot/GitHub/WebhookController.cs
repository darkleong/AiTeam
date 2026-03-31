using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AiTeam.Bot.Agents;
using AiTeam.Bot.Configuration;
using AiTeam.Data;
using AiTeam.Data.Repositories;
using AiTeam.Bot.Notion;
using Microsoft.Extensions.DependencyInjection;
using DiscordNet = Discord;
using Discord.WebSocket;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace AiTeam.Bot.GitHub;

/// <summary>
/// 接收 GitHub Webhook 事件，驗證簽章後分派給對應 Agent。
/// </summary>
[ApiController]
[Route("webhook/github")]
public class WebhookController(
    IOptions<GitHubSettings> gitHubSettings,
    IOptions<DiscordSettings> discordSettings,
    DiscordSocketClient discordClient,
    NotionService notionService,
    IServiceProvider serviceProvider,
    ILogger<WebhookController> logger) : ControllerBase
{
    private readonly GitHubSettings _github = gitHubSettings.Value;
    private readonly DiscordSettings _discord = discordSettings.Value;

    [HttpPost]
    public async Task<IActionResult> Handle(CancellationToken cancellationToken)
    {
        // 1. 讀取 raw body
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var body = await reader.ReadToEndAsync(cancellationToken);

        // 2. 驗證簽章
        if (!VerifySignature(body, Request.Headers["X-Hub-Signature-256"].ToString()))
        {
            logger.LogWarning("GitHub Webhook 簽章驗證失敗，拒絕處理");
            return Unauthorized("Invalid signature");
        }

        var eventType = Request.Headers["X-GitHub-Event"].ToString();
        logger.LogInformation("收到 GitHub Webhook 事件：{EventType}", eventType);

        // 3. 根據事件類型分派
        _ = Task.Run(async () =>
        {
            try
            {
                await DispatchEventAsync(eventType, body, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "處理 GitHub Webhook 事件失敗：{EventType}", eventType);
            }
        }, cancellationToken);

        // 立即回 200，讓 GitHub 知道已收到（處理在背景進行）
        return Ok();
    }

    // ────────────── 事件分派 ──────────────

    private async Task DispatchEventAsync(string eventType, string body, CancellationToken cancellationToken)
    {
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        switch (eventType)
        {
            case "issues" when root.GetProperty("action").GetString() == "opened":
                await HandleIssueOpenedAsync(root, cancellationToken);
                break;

            case "pull_request" when root.GetProperty("action").GetString() == "opened":
                await HandlePrOpenedAsync(root, cancellationToken);
                break;

            case "push":
                await HandlePushAsync(root, cancellationToken);
                break;

            default:
                logger.LogDebug("忽略事件：{EventType}", eventType);
                break;
        }
    }

    /// <summary>
    /// Issue 建立 → 自動觸發 CEO Agent 分析，走雙層確認流程。
    /// </summary>
    private async Task HandleIssueOpenedAsync(JsonElement root, CancellationToken cancellationToken)
    {
        var issueTitle = root.GetProperty("issue").GetProperty("title").GetString() ?? "";
        var issueBody = root.GetProperty("issue").GetProperty("body").GetString() ?? "";
        var repoName = root.GetProperty("repository").GetProperty("name").GetString() ?? "";
        var issueUrl = root.GetProperty("issue").GetProperty("html_url").GetString() ?? "";

        logger.LogInformation("Issue 建立：{Title}（{Repo}）", issueTitle, repoName);

        await using var scope = serviceProvider.CreateAsyncScope();
        var ceoService = scope.ServiceProvider.GetRequiredService<CeoAgentService>();
        var taskRepo = scope.ServiceProvider.GetRequiredService<TaskRepository>();

        var agentRepo    = scope.ServiceProvider.GetRequiredService<AgentRepository>();
        var rules        = await notionService.GetRulesAsync(cancellationToken);
        var activeAgents = await agentRepo.GetActiveExecutorAgentsAsync(cancellationToken);
        var agentList    = activeAgents
            .Select(a => new AgentDescriptor(a.Name, a.Description))
            .ToList();

        var userInput = $"GitHub Issue 建立：{issueTitle}\n\n{issueBody}\n\nIssue URL：{issueUrl}";
        var ceoResponse = await ceoService.ProcessAsync(
            userInput, repoName, agentList, rules, cancellationToken);

        // 發送到 Discord #任務動態 頻道
        var channel = await FindChannelAsync(_discord.Channels.TaskUpdates);
        if (channel is null)
        {
            logger.LogWarning("找不到 Discord 頻道：{Channel}", _discord.Channels.TaskUpdates);
            return;
        }

        var embed = new DiscordNet.EmbedBuilder()
            .WithTitle("🐛 GitHub Issue 觸發 — CEO 決策")
            .WithColor(DiscordNet.Color.Purple)
            .AddField("Issue", $"[{issueTitle}]({issueUrl})")
            .AddField("Repo", repoName, inline: true)
            .AddField("CEO 回應", ceoResponse.Reply)
            .WithFooter("請到 #指令中心 使用 /task 指令確認執行")
            .Build();

        await channel.SendMessageAsync(embed: embed);
    }

    /// <summary>
    /// PR 開啟 → 通知老闆審查。
    /// </summary>
    private async Task HandlePrOpenedAsync(JsonElement root, CancellationToken cancellationToken)
    {
        var prTitle = root.GetProperty("pull_request").GetProperty("title").GetString() ?? "";
        var prUrl = root.GetProperty("pull_request").GetProperty("html_url").GetString() ?? "";
        var repoName = root.GetProperty("repository").GetProperty("name").GetString() ?? "";
        var author = root.GetProperty("pull_request").GetProperty("user").GetProperty("login").GetString() ?? "";

        logger.LogInformation("PR 開啟：{Title}（{Repo}）", prTitle, repoName);

        var channel = await FindChannelAsync(_discord.Channels.TaskUpdates);
        if (channel is null) return;

        var embed = new DiscordNet.EmbedBuilder()
            .WithTitle("📬 新 PR 等待審查")
            .WithColor(DiscordNet.Color.Green)
            .AddField("PR 標題", prTitle)
            .AddField("Repo", repoName, inline: true)
            .AddField("作者", author, inline: true)
            .AddField("連結", prUrl)
            .WithFooter("請審查程式碼後手動按下 Merge")
            .Build();

        await channel.SendMessageAsync(embed: embed);
    }

    /// <summary>
    /// Push 到 main → 通知 Ops Agent 監控部署。
    /// </summary>
    private async Task HandlePushAsync(JsonElement root, CancellationToken cancellationToken)
    {
        var refName = root.GetProperty("ref").GetString() ?? "";
        if (!refName.EndsWith("/main")) return; // 只處理 main branch

        var repoName = root.GetProperty("repository").GetProperty("name").GetString() ?? "";
        var pusher = root.GetProperty("pusher").GetProperty("name").GetString() ?? "";

        logger.LogInformation("Push 到 main：{Repo}（{Pusher}）", repoName, pusher);

        var channel = await FindChannelAsync(_discord.Channels.TaskUpdates);
        if (channel is null) return;

        var embed = new DiscordNet.EmbedBuilder()
            .WithTitle("🚀 Push 到 main — Ops Agent 待命")
            .WithColor(DiscordNet.Color.Orange)
            .AddField("Repo", repoName, inline: true)
            .AddField("推送者", pusher, inline: true)
            .WithDescription("GitHub Actions 部署流程已觸發，Ops Agent 將監控部署結果。")
            .Build();

        await channel.SendMessageAsync(embed: embed);
    }

    // ────────────── Helper ──────────────

    private async Task<DiscordNet.IMessageChannel?> FindChannelAsync(string channelName)
    {
        if (!ulong.TryParse(_discord.GuildId, out var guildId)) return null;
        var guild = discordClient.GetGuild(guildId);
        if (guild is null) return null;

        await guild.DownloadUsersAsync();
        return guild.TextChannels.FirstOrDefault(c => c.Name == channelName);
    }

    private bool VerifySignature(string body, string signatureHeader)
    {
        if (string.IsNullOrEmpty(signatureHeader)) return false;

        var secret = Encoding.UTF8.GetBytes(_github.WebhookSecret);
        var bodyBytes = Encoding.UTF8.GetBytes(body);

        using var hmac = new HMACSHA256(secret);
        var hash = hmac.ComputeHash(bodyBytes);
        var expected = "sha256=" + Convert.ToHexString(hash).ToLowerInvariant();

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(signatureHeader));
    }
}
