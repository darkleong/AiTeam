using AiTeam.Dashboard.Settings;
using Microsoft.Extensions.Options;
using Notion.Client;

namespace AiTeam.Dashboard.Services;

/// <summary>
/// 信任等級直接寫回 Notion，不存 PostgreSQL，避免兩邊資料不一致。
/// Notion 是信任等級的唯一來源。
/// </summary>
public class NotionTrustLevelService(
    INotionClient notionClient,
    IOptions<NotionSettings> settings,
    ILogger<NotionTrustLevelService> logger)
{
    private readonly NotionSettings _settings = settings.Value;

    #region Public Methods

    /// <summary>
    /// 更新指定 Agent 的信任等級，寫回 Notion AgentStatus 資料庫。
    /// </summary>
    public async Task UpdateTrustLevelAsync(
        string agentName,
        int trustLevel,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var query = await notionClient.Databases.QueryAsync(
                _settings.AgentStatusDatabaseId,
                new DatabasesQueryParameters
                {
                    Filter = new TitleFilter("Agent Name", equal: agentName)
                });

            if (query.Results.Count == 0)
            {
                logger.LogWarning("Notion 中找不到 Agent {AgentName} 的設定頁面", agentName);
                return;
            }

            var page = (Page)query.Results[0];
            await notionClient.Pages.UpdateAsync(page.Id, new PagesUpdateParameters
            {
                Properties = new Dictionary<string, PropertyValue>
                {
                    ["Trust Level"] = new NumberPropertyValue { Number = trustLevel }
                }
            });

            logger.LogInformation("已更新 Agent {AgentName} 信任等級為 {TrustLevel}", agentName, trustLevel);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "更新 Agent {AgentName} 信任等級失敗", agentName);
            throw;
        }
    }

    /// <summary>取得指定 Agent 的 Notion 規則清單（唯讀）。</summary>
    public async Task<IReadOnlyList<string>> GetRulesAsync(
        string agentName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var query = await notionClient.Databases.QueryAsync(
                _settings.RulesDatabaseId,
                new DatabasesQueryParameters
                {
                    Filter = new SelectFilter("Agent", equal: agentName)
                });

            var rules = new List<string>();
            foreach (var result in query.Results)
            {
                var page = (Page)result;
                if (page.Properties.TryGetValue("Rule Content", out var val)
                    && val is RichTextPropertyValue rt)
                {
                    var text = string.Concat(rt.RichText.Select(t => t.PlainText));
                    if (!string.IsNullOrEmpty(text))
                        rules.Add(text);
                }
            }
            return rules;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "讀取 Agent {AgentName} 規則失敗", agentName);
            return [];
        }
    }

    #endregion
}
