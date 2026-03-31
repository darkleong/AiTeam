using AiTeam.Shared.Constants;
using Microsoft.EntityFrameworkCore;

namespace AiTeam.Data;

/// <summary>
/// 啟動時的基礎資料 Seed（幂等，安全地從 Bot 或 Dashboard 呼叫）。
/// </summary>
public static class DbSeeder
{
    private static readonly (string Name, string Description, bool Active)[] AgentSeeds =
    [
        (AgentNames.Dev,          "負責程式碼開發、Bug 修復、功能實作，操作 GitHub PR",             true),
        (AgentNames.Ops,          "負責部署監控、健康檢查、自動回滾，處理基礎設施問題",             true),
        (AgentNames.Qa,           "負責自動化測試，讀取 PR 變更後產生測試案例，開 PR 提交測試檔案", false),
        (AgentNames.Doc,          "負責文件生成，讀取原始碼產出 Markdown 文件或 XML 註解，開 PR",   false),
        (AgentNames.Requirements, "負責需求分析，將原始需求拆解為 GitHub Issues 結構化清單",       false),
    ];

    /// <summary>
    /// 確保預設 Team 與所有 Agent 設定存在。重複執行安全。
    /// </summary>
    public static async Task SeedAsync(AppDbContext db)
    {
        var team = await db.Teams.FirstOrDefaultAsync();
        if (team is null)
        {
            team = new Team { Name = "預設團隊", Description = "AI 自動化開發團隊" };
            db.Teams.Add(team);
            await db.SaveChangesAsync();
        }

        foreach (var (name, desc, active) in AgentSeeds)
        {
            var existing = await db.AgentConfigs.FirstOrDefaultAsync(a => a.Name == name);
            if (existing is null)
            {
                db.AgentConfigs.Add(new AgentConfig
                {
                    TeamId      = team.Id,
                    Name        = name,
                    Description = desc,
                    TrustLevel  = 1,
                    IsActive    = active
                });
            }
            else if (string.IsNullOrEmpty(existing.Description))
            {
                existing.Description = desc;
            }
        }

        await db.SaveChangesAsync();
    }
}
