using System.Text.Json;
using AiTeam.Bot.Configuration;
using AiTeam.Bot.Orchestration.Petra;
using AiTeam.Shared.Constants;

namespace AiTeam.Bot.Agents;

/// <summary>
/// CEO Agent 核心邏輯：v5.5 path 純 PetraInbox flag forward only。
///
/// Stage 78a：v4 Claude Code fallback path 砍 — IClaudeCodeService claudeCodeService / IConfiguration configuration / WorkflowSettingsResolver workflowResolver / VictoriaLock /
/// CeoConversationRepository conversationRepository / CeoMemoryRepository memoryRepository / TokenLogService tokenLogService / PetraOrchestratorService petraOrchestrator 全砍
///（ProcessWithClaudeCodeAsync v4 fallback + BuildVictoriaPrompt + TryParseActionBlock + Session 解析 + 對話歷史 + 長期記憶 + Claude Code token log 砍後 0 caller）。
///
/// Stage 78b：v4 ProcessAsync + BuildSystemPrompt + BuildUserMessageAsync + BuildGitHubContextAsync + TryParseResponse 砍
///（providerFactory / taskRepository / gitHubService / gitHubSettings ctor 4 dep 砍 — 0 v5.5 caller after / 2 v4 caller WebhookController.HandleIssueOpenedAsync + SlashCommandRouter.HandleTaskCommandAsync 同步砍）。
/// CeoAgentService 縮為純 v5.5 path：ProcessWithClaudeCodeAsync 寫 PetraInbox + return ack（Stage 75 PetraInbox flag forward only）。
///
/// Stage 79：v5.5 image flow 補完 — Stage 75 method body line 39 漏接 images 修根因。
/// 補：① 限制紀律守（MaxAttachmentsPerTask + MaxAttachmentSizeMB / 三層守第三層）② JSON 序列化含 Type discriminator（半抽象 future-friendly）③ Repository.Enqueue 接通 attachmentsJson。
/// 新 ctor dep：WorkflowSettingsResolver workflowResolver（限制紀律守用）。
/// </summary>
public class CeoAgentService(
    AiTeam.Data.Repositories.PetraInboxRepository petraInboxRepository,   // Stage 75
    AiTeam.Data.AppDbContext db,                                            // Stage 75（CeoAgentService 是 Scoped — 安全）
    WorkflowSettingsResolver workflowResolver,                              // Stage 79
    ILogger<CeoAgentService> logger)
{
    /// <summary>Stage 79：camelCase JSON 序列化對齊 CeoCommandController.ImagesJsonOptions（既有 pattern）。</summary>
    private static readonly JsonSerializerOptions AttachmentsJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>
    /// Stage 15：Victoria CEO 的主要處理路徑（Claude Code 模式）。
    /// Stage 78a：v4 fallback 砍 — 強制走 v5.5 path（v4 path 0 production active 連續 17 次 Trial 累積 / SQL flag row 保留 Phase 5+ 評估）。
    /// 對應 Stage 63B/75/76 v5.5 path 設計：flag forward only / Victoria 不直接 call LLM / 寫 PetraInbox + return ack。
    /// Stage 79：v5.5 image flow 補完 — images 接通 PetraInbox.Attachments（半抽象 future-friendly JSON + Type discriminator）。
    /// </summary>
    public async Task<CeoResponse> ProcessWithClaudeCodeAsync(
        string userInput,
        string userId,
        string projectName,
        IReadOnlyList<AgentDescriptor> agentList,
        IReadOnlyList<string> rules,
        CancellationToken cancellationToken = default,
        IReadOnlyList<ImageAttachment>? images = null,
        IReadOnlyList<string>? availableProjects = null)
    {
        // Stage 79：限制紀律守（Dashboard MaxFiles UI 阻止 + CeoCommandController API 後備層 + 此處 Repository 終守 — 三層守第三層）
        if (images is { Count: > 0 })
        {
            var maxCount = await workflowResolver.GetMaxAttachmentsPerTaskAsync(cancellationToken);
            var maxSizeBytes = (long)(await workflowResolver.GetMaxAttachmentSizeMBAsync(cancellationToken)) * 1024 * 1024;

            var filtered = new List<ImageAttachment>(Math.Min(images.Count, maxCount));
            foreach (var img in images.Take(maxCount))
            {
                var sizeBytes = (img.Base64Data.Length * 3L) / 4;   // base64 size 估算
                if (sizeBytes > maxSizeBytes)
                {
                    logger.LogWarning("Stage 79：attachment 超 MaxAttachmentSizeMB sizeBytes={Size} skip", sizeBytes);
                    continue;
                }
                filtered.Add(img);
            }
            if (filtered.Count < images.Count)
            {
                logger.LogWarning("Stage 79：attachment 收 {Count} → 留 {Filtered}（MaxCount={MaxCount}）",
                    images.Count, filtered.Count, maxCount);
            }
            images = filtered;
        }

        // Stage 79：v5.5 image flow 補完 — 序列化 images 含 Type discriminator（半抽象 future-friendly）
        string? attachmentsJson = null;
        if (images is { Count: > 0 })
        {
            var dtos = images.Select(img => new
            {
                type       = "image",   // Stage 79 baseline 唯一 type / 未來擴展 PDF/document 加新 type
                base64Data = img.Base64Data,
                mediaType  = img.MediaType,
            }).ToList();
            attachmentsJson = JsonSerializer.Serialize(dtos, AttachmentsJsonOptions);
        }

        // 來源紀律：對齊 BossCommandLog.Source 既有 pattern — Dashboard / Discord 兩通道（CeoCommandController 是目前唯一 caller / Discord 直接呼叫 path 留未來）。
        var source = "dashboard";
        var row = petraInboxRepository.Enqueue(userInput, source, attachmentsJson);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Victoria → 寫 PetraInbox row={Id} source={Source} attachments={Count}（Stage 79：v5.5 image flow 補完 / Stage 75 漏接根因修）",
            row.Id, source, images?.Count ?? 0);

        return new CeoResponse
        {
            Reply = $"[v5.5] Task 已接收（inbox={row.Id.ToString("N")[..8]}）— Petra 將依 FIFO 順序拆解派工，請於 Dashboard 操作中心追蹤進度。",
            Action = CeoResponseActions.PetraV5Dispatched,
        };
    }
}
