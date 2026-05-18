using AiTeam.Bot.Agents;

namespace AiTeam.Bot.Discord.Routing;

/// <summary>
/// Stage 36：等待確認的暫存資料（原 CommandHandler 內部 internal record）。
/// IsProposal = true 時代表 CEO 提案書，確認後才建立 Issues。
/// 升級為 public 以便 Routing/PendingConfirmationStore 與 Orchestration/ProposalConfirmationService 共用。
/// </summary>
public record PendingConfirmation(
    CeoResponse CeoResponse,
    string Project,
    string Description,
    Guid TaskId = default,
    Guid GroupId = default,
    string? UiSpecMarkdown = null,
    string? UiSpecPath = null,
    bool IsProposal = false,
    IReadOnlyList<ImageAttachment>? Images = null,
    string EscalateStage = "");  // Stage 78a：Rosa/Demi class 砍後 EscalateStage 由 SlashCommandRouter / Petra orchestrator 設定 escalate path 用
