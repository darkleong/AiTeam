using Microsoft.EntityFrameworkCore;

namespace AiTeam.Data;

/// <summary>
/// 啟動時的基礎資料 Seed（幂等，安全地從 Bot 或 Dashboard 呼叫）。
/// v4-rewrite：6 Talent / SkillPrompt / TalentPrompt seed 砍（執行端搬到 Claude Code Agent Team）。
/// 保留：Team default + Rules baseline + AppSettings TokenPricing。
/// </summary>
public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db)
    {
        var team = await db.Teams.FirstOrDefaultAsync();
        if (team is null)
        {
            team = new Team { Name = "預設團隊", Description = "AI 自動化開發團隊" };
            db.Teams.Add(team);
            await db.SaveChangesAsync();
        }

        if (!await db.Rules.AnyAsync())
        {
            var starterRules = new[]
            {
                (Content: "回應使用繁體中文，語氣專業但親切", SortOrder: 1),
                (Content: "執行任務前必須先確認任務範圍與目標，避免做超出範圍的事", SortOrder: 2),
                (Content: "程式碼修改必須遵循現有專案的命名慣例與架構設計", SortOrder: 3),
                (Content: "有不確定之處應主動提問，而非自行假設", SortOrder: 4),
                (Content: "所有 PR 說明必須包含：變更原因、影響範圍、測試方式", SortOrder: 5),
            };

            foreach (var (content, sortOrder) in starterRules)
            {
                db.Rules.Add(new Rule
                {
                    TeamId    = team.Id,
                    Content   = content,
                    IsActive  = true,
                    SortOrder = sortOrder
                });
            }

            await db.SaveChangesAsync();
        }

        await EnsureSettingAsync(db, "TokenPricing:InputPer1kUsd",  "0.003", "每千個 Input Token 費用（USD），預設 Sonnet 費率");
        await EnsureSettingAsync(db, "TokenPricing:OutputPer1kUsd", "0.015", "每千個 Output Token 費用（USD），預設 Sonnet 費率");
        await db.SaveChangesAsync();
    }

    private static async Task EnsureSettingAsync(AppDbContext db, string key, string defaultValue, string description)
    {
        if (!await db.AppSettings.AnyAsync(s => s.Key == key))
        {
            db.AppSettings.Add(new AppSetting
            {
                Key         = key,
                Value       = defaultValue,
                Description = description,
                UpdatedAt   = DateTime.UtcNow
            });
        }
    }
}
