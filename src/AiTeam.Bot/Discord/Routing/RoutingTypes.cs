using AiTeam.Bot.Agents;

namespace AiTeam.Bot.Discord.Routing;

/// <summary>
/// Stage 36：等待確認的暫存資料（原 CommandHandler 內部 internal record）。
///
/// Stage 78c：v4 Pipeline framework 整套砍後 record 縮為純 v5.5 generic confirmation：
///   3 個 base field（CeoResponse / Project / Description）— ceo_confirm BossInteraction Discord button → confirm_yes Stage 68 短路 ack。
///
/// 砍 v4 fields：TaskId / GroupId / UiSpecMarkdown / UiSpecPath / IsProposal / Images / EscalateStage
///（0 v5.5 caller after Stage 78c 鏈 A+B+C+E 砍）。
/// </summary>
public record PendingConfirmation(
    CeoResponse CeoResponse,
    string Project,
    string Description);
