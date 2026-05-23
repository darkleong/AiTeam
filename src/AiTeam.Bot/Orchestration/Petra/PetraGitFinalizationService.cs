using AiTeam.Bot.GitHub;
using AiTeam.Data;
using LibGit2Sharp;
using Microsoft.EntityFrameworkCore;

namespace AiTeam.Bot.Orchestration.Petra;

/// <summary>
/// Stage 84：FinalizeGitAsync 從 PetraOrchestratorService 拆出獨立 service（最獨立 / 0 跨 service / 0 Petra LLM call）。
/// 對齊 Stage 83 Bug 4 path A+ prUrl 寫回行為（caller 自行 PetraSession.ResultPrUrl 寫入）。
///
/// 範圍邊界（同 v4 紀律）：
/// - 無 git diff → 不誤建 PR
/// - 非 git repo → 捕例外 log warning 不擋流程
/// - 缺 GitHub:Owner / GitHub:DefaultRepo 設定 → skip + warning
/// </summary>
public class PetraGitFinalizationService(
    GitHubService gitHubService,
    AppDbContext db,
    IConfiguration configuration,
    ILogger<PetraGitFinalizationService> logger)
{
    /// <summary>Stage 64 子項 2：Workers 完成後 git commit/push/PR 接通（沿用 v4 GitHubService.CommitAll/Push/OpenPullRequestAsync API）。
    /// Stage 67：picks 抽 dispatchNames 後簽名 — v5 既有 worker name 與 v5.5 talent name 共用一致 typed string list。</summary>
    public async Task<string?> FinalizeGitAsync(
        PetraSessionContext ctx,
        string taskInput,
        IReadOnlyList<string> caps,
        IReadOnlyList<string> dispatchNames,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(ctx.WorkingDir) || !Directory.Exists(Path.Combine(ctx.WorkingDir, ".git")))
        {
            logger.LogInformation("Petra FinalizeGitAsync skip — workingDir 非 git repo（Mock 階段或 spike forward path）sessionId={SessionId}", ctx.SessionId);
            return null;
        }

        var owner = configuration["GitHub:Owner"] ?? "";
        var repo = configuration["GitHub:DefaultRepo"] ?? "";
        if (string.IsNullOrEmpty(owner) || string.IsNullOrEmpty(repo))
        {
            logger.LogWarning("Petra FinalizeGitAsync skip — GitHub:Owner 或 GitHub:DefaultRepo 未設定 sessionId={SessionId}", ctx.SessionId);
            return null;
        }

        try
        {
            // Check diff (LibGit2Sharp)
            using (var gitRepo = new Repository(ctx.WorkingDir))
            {
                var status = gitRepo.RetrieveStatus();
                if (!status.IsDirty)
                {
                    logger.LogInformation("Petra FinalizeGitAsync skip — workingDir 無 git diff（worker 0 變更不誤建 PR）sessionId={SessionId}", ctx.SessionId);
                    return null;
                }
            }

            // 自動產 branch name：petra/{taskGroup-8}-{session-8}-{yyyyMMddHHmm}
            // taskGroupId Empty.Guid → 走 spike- prefix；timestamp 防同 session retry 撞 branch
            var taskGroupShort = ctx.TaskGroupId == Guid.Empty
                ? "spike"
                : ctx.TaskGroupId.ToString("N")[..8];
            var sessionShort = ctx.SessionId.ToString("N")[..8];
            var ts = DateTime.UtcNow.ToString("yyyyMMddHHmm");
            var branchName = $"petra/{taskGroupShort}-{sessionShort}-{ts}";

            gitHubService.CreateAndCheckoutBranch(ctx.WorkingDir, branchName);
            logger.LogInformation("Petra branch 建立 + checkout：{Branch} sessionId={SessionId}", branchName, ctx.SessionId);

            // commit message：Petra dispatch summary + 第一行任務截短
            var taskFirstLine = (taskInput ?? "").Split('\n').FirstOrDefault() ?? "";
            var taskSummary = taskFirstLine.Length > 60 ? taskFirstLine[..60] + "..." : taskFirstLine;
            var commitMessage = $"[Petra] {taskSummary}\n\nDispatch: {string.Join(" → ", caps)}";
            gitHubService.CommitAll(ctx.WorkingDir, commitMessage);
            gitHubService.Push(ctx.WorkingDir, branchName);

            // PR body：Petra 決策 + worker summary（從 PetraSessionMessages tool role 取）
            var workerSummaries = await db.PetraSessionMessages
                .Where(m => m.SessionId == ctx.SessionId && m.Role == "tool")
                .OrderBy(m => m.CreatedAt)
                .Select(m => m.Content)
                .ToListAsync(ct);

            var prBody = BuildPrBody(taskInput, caps, dispatchNames, workerSummaries);
            var prTitle = $"[Petra v5] {taskSummary}";

            var prUrl = await gitHubService.OpenPullRequestAsync(owner, repo, prTitle, prBody, branchName);
            logger.LogInformation("Petra PR 開啟：{PrUrl} sessionId={SessionId}", prUrl, ctx.SessionId);
            return prUrl;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Petra FinalizeGitAsync 失敗（不影響 session complete）sessionId={SessionId}", ctx.SessionId);
            return null;
        }
    }

    private static string BuildPrBody(
        string? taskInput,
        IReadOnlyList<string> caps,
        IReadOnlyList<string> dispatchNames,
        IReadOnlyList<string> workerSummaries)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("## 任務");
        sb.AppendLine(taskInput ?? "");
        sb.AppendLine();
        sb.AppendLine("## Petra 動態決策");
        sb.AppendLine($"- Capability / Skill 序列：`{string.Join(" | ", caps)}`");
        sb.AppendLine($"- Workers / Talents dispatch 順序：{string.Join(" → ", dispatchNames)}");
        sb.AppendLine();
        sb.AppendLine("## Worker 完成 summary");
        if (workerSummaries.Count == 0)
        {
            sb.AppendLine("（無 tool role 紀錄）");
        }
        else
        {
            foreach (var s in workerSummaries)
            {
                sb.AppendLine($"- {s}");
            }
        }
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine("🤖 由 AiTeam Petra Orchestrator（v5 動態架構 PoC）自動產出");
        return sb.ToString();
    }
}
