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
/// </summary>
public class CeoAgentService(
    AiTeam.Data.Repositories.PetraInboxRepository petraInboxRepository,   // Stage 75
    AiTeam.Data.AppDbContext db,                                            // Stage 75（CeoAgentService 是 Scoped — 安全）
    ILogger<CeoAgentService> logger)
{
    /// <summary>
    /// Stage 15：Victoria CEO 的主要處理路徑（Claude Code 模式）。
    /// Stage 78a：v4 fallback 砍 — 強制走 v5.5 path（v4 path 0 production active 連續 17 次 Trial 累積 / SQL flag row 保留 Phase 5+ 評估）。
    /// 對應 Stage 63B/75/76 v5.5 path 設計：flag forward only / Victoria 不直接 call LLM / 寫 PetraInbox + return ack。
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
        // 來源紀律：對齊 BossCommandLog.Source 既有 pattern — Dashboard / Discord 兩通道（CeoCommandController 是目前唯一 caller / Discord 直接呼叫 path 留未來）。
        var source = "dashboard";
        var row = petraInboxRepository.Enqueue(userInput, source);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Victoria → 寫 PetraInbox row={Id} source={Source}（Stage 78a：v4 fallback 砍 / v5.5 path 強制 / 議題 1 拍板實踐 — 多 task 並存 / Stage 76 簡化顯示）",
            row.Id, source);

        return new CeoResponse
        {
            Reply = $"[v5.5] Task 已接收（inbox={row.Id.ToString("N")[..8]}）— Petra 將依 FIFO 順序拆解派工，請於 Dashboard 操作中心追蹤進度。",
            Action = CeoResponseActions.PetraV5Dispatched,
        };
    }
}
