using AiTeam.Bot.Configuration;
using Microsoft.Extensions.Options;
using Notion.Client;

namespace AiTeam.Bot.Notion;

/// <summary>
/// Notion API 串接：讀取規則（含 TTL Cache）、寫入任務摘要。
/// </summary>
public class NotionService(
    INotionClient notionClient,
    IOptions<NotionSettings> notionSettings,
    IOptions<AgentSettings> agentSettings,
    ILogger<NotionService> logger)
{
    private readonly NotionSettings _notion = notionSettings.Value;
    private readonly AgentSettings _agent = agentSettings.Value;

    private List<string> _cachedRules = [];
    private DateTime _cacheExpiry = DateTime.MinValue;
    private readonly SemaphoreSlim _cacheLock = new(1, 1);

    /// <summary>
    /// 取得規則清單（TTL Cache，到期自動重新拉取）。
    /// </summary>
    public async Task<IReadOnlyList<string>> GetRulesAsync(CancellationToken cancellationToken = default)
    {
        if (DateTime.UtcNow < _cacheExpiry)
            return _cachedRules;

        await _cacheLock.WaitAsync(cancellationToken);
        try
        {
            // 雙重檢查，避免多執行緒重複拉取
            if (DateTime.UtcNow < _cacheExpiry)
                return _cachedRules;

            _cachedRules = await FetchRulesFromNotionAsync(cancellationToken);
            _cacheExpiry = DateTime.UtcNow.AddMinutes(_agent.NotionCacheTtlMinutes);
            logger.LogInformation("Notion 規則已更新，共 {Count} 條，下次更新：{Expiry:HH:mm}", _cachedRules.Count, _cacheExpiry);
        }
        finally
        {
            _cacheLock.Release();
        }

        return _cachedRules;
    }

    /// <summary>
    /// 強制清除 Cache，下次呼叫 GetRulesAsync 時重新拉取。
    /// </summary>
    public void InvalidateCache()
    {
        _cacheExpiry = DateTime.MinValue;
        logger.LogInformation("Notion 規則 Cache 已清除");
    }

    /// <summary>
    /// 寫入任務摘要到 Notion。
    /// </summary>
    public async Task WriteTaskSummaryAsync(
        string title,
        string agentName,
        string originalCommand,
        string result,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var properties = new Dictionary<string, PropertyValue>
            {
                ["Name"] = new TitlePropertyValue
                {
                    Title = [new RichTextText { Text = new Text { Content = title } }]
                },
                ["Agent"] = new RichTextPropertyValue
                {
                    RichText = [new RichTextText { Text = new Text { Content = agentName } }]
                },
                ["Command"] = new RichTextPropertyValue
                {
                    RichText = [new RichTextText { Text = new Text { Content = originalCommand } }]
                },
                ["Result"] = new RichTextPropertyValue
                {
                    RichText = [new RichTextText { Text = new Text { Content = result } }]
                },
                ["Date"] = new DatePropertyValue
                {
                    Date = new Date { Start = DateTime.UtcNow }
                }
            };

            await notionClient.Pages.CreateAsync(new PagesCreateParameters
            {
                Parent = new DatabaseParentInput { DatabaseId = _notion.TaskSummaryDatabaseId },
                Properties = properties
            }, cancellationToken);

            logger.LogInformation("任務摘要已寫入 Notion：{Title}", title);
        }
        catch (Exception ex)
        {
            // 寫入失敗不影響主流程，只記 log
            logger.LogError(ex, "寫入 Notion 任務摘要失敗：{Title}", title);
        }
    }

    private async Task<List<string>> FetchRulesFromNotionAsync(CancellationToken cancellationToken)
    {
        var rules = new List<string>();

        try
        {
            var response = await notionClient.Databases.QueryAsync(
                _notion.RulesDatabaseId,
                new DatabasesQueryParameters(),
                cancellationToken);

            foreach (var page in response.Results.OfType<Page>())
            {
                if (page.Properties.TryGetValue("Rule Content", out var prop)
                    && prop is RichTextPropertyValue richText
                    && richText.RichText.Count > 0)
                {
                    var text = string.Concat(richText.RichText.Select(r => r.PlainText));
                    if (!string.IsNullOrWhiteSpace(text))
                        rules.Add(text);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "從 Notion 拉取規則失敗，使用上次 Cache");
        }

        return rules;
    }
}
