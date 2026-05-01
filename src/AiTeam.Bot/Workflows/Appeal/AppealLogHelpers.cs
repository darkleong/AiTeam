using AiTeam.Data;

namespace AiTeam.Bot.Workflows.Appeal;

/// <summary>
/// Stage 49：framework path 寫 group.ReviewAppealLog / DevPlanAppealLog 的 helper（對齊 legacy 行為）。
///
/// 為什麼複製而非共用：
///   - legacy AppealOrchestrationService.AppendAppealLog / AppendDevPlanAppealLog 是 private static
///   - 改 internal 動 legacy code 違反「保留 legacy path 完全運作」原則
///   - 內容只有 5 行 string concat，複製成本低
///   - Stage 54 收尾 legacy path 砍掉時，本 helper 升格為唯一寫入點
/// </summary>
internal static class AppealLogHelpers
{
    public static void AppendReviewAppealLog(TaskGroup group, int round, string content)
    {
        var entry = $"\n\n### Appeal Round {round} — {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC\n\n{content}";
        group.ReviewAppealLog = (group.ReviewAppealLog ?? "# Review Appeal 紀錄\n") + entry;
    }

    public static void AppendDevPlanAppealLog(TaskGroup group, int round, string content)
    {
        var entry = $"\n\n### DevPlan Appeal Round {round} — {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC\n\n{content}";
        group.DevPlanAppealLog = (group.DevPlanAppealLog ?? "# Dev_plan Appeal 紀錄\n") + entry;
    }
}
