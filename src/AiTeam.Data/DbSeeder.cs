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
        (AgentNames.Ceo,          "負責解讀老闆指令、協調 AI 團隊分工、發起任務並監控進度",          true),
        (AgentNames.Dev,          "負責程式碼開發、Bug 修復、功能實作，操作 GitHub PR",             true),
        (AgentNames.Ops,          "負責部署監控、健康檢查、自動回滾，處理基礎設施問題",             true),
        (AgentNames.Qa,           "負責自動化測試，讀取 PR 變更後產生測試案例，開 PR 提交測試檔案", false),
        (AgentNames.Doc,          "負責文件生成，讀取原始碼產出 Markdown 文件或 XML 註解，開 PR",   false),
        (AgentNames.Requirements, "負責需求分析，將原始需求拆解為 GitHub Issues 結構化清單",       false),
        (AgentNames.Reviewer,     "負責 Code Review，讀取 PR 差異產出分級審查報告，並在 PR 上留下 Review Comments", false),
        (AgentNames.Release,      "負責版本發佈，彙整 Commits 與 merged PRs，建立 Release tag 並產出 Changelog",   false),
        (AgentNames.Designer,     "負責 UI 規格設計，將功能需求轉換為 MudBlazor 元件規格文件（Markdown）",          false),
        (AgentNames.Pm,           "負責審核 Rosa/Demi/Cody/Vera 的產出品質，決定 approve/revise/escalate",          false),
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

        // Seed 初始規則（如果 rules 表完全為空才新增）
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

        // Seed 初始動態設定（逐一確認，缺少才新增，方便版本升級補 key）
        await EnsureSettingAsync(db, "SkipCeoConfirm",                "false",  "跳過 CEO 派工確認，直接進入 Agent 執行確認（true/false）");
        await EnsureSettingAsync(db, "TokenPricing:InputPer1kUsd",    "0.003",  "每千個 Input Token 費用（USD），預設 Sonnet 費率");
        await EnsureSettingAsync(db, "TokenPricing:OutputPer1kUsd",   "0.015",  "每千個 Output Token 費用（USD），預設 Sonnet 費率");
        await db.SaveChangesAsync();

        // Stage 67：v5.5 Phase 1 Step 2 — baseline 6 Talent + 6 Skill assignment seed（幂等）
        await EnsureTalentsAsync(db);
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Stage 67：v5.5 Phase 1 Step 2 — 確保 baseline 6 Talent + Talent-Skill assignment 存在（幂等）。
    /// 對齊 Phase 1 Step 1 Baseline 拍板（Christ 2026-05-15）：Victoria / Petra 0 skill orchestrator + Cody 兼 3 skill + Vera/Quinn/Sage 主 1 skill。
    /// ProjectId = null 全域共用（Christ 決議 2 per-Project 隔離 baseline 全 null）。
    /// Provider / Model = null（runtime fallback Agents:Dev:Model 既有 BuildSessionContext pattern）。
    /// </summary>
    private static async Task EnsureTalentsAsync(AppDbContext db)
    {
        // baseline 6 Talent — 對齊 Phase 1 Step 1 Baseline 拍板
        var talentSeeds = new (string Name, string DisplayName, string Description)[]
        {
            ("Victoria", "Victoria", "Orchestrator - 接 Christ 指令、forward 任務（無 Skill assignment）"),
            ("Petra",    "Petra",    "Orchestrator - 拆解任務 / 派 Talent / 吸收 requirements_extraction 紀律（無 Skill assignment）"),
            ("Cody",     "Cody",     "code_implementation 主 + ui_design + release_publishing 兼"),
            ("Vera",     "Vera",     "code_review 主"),
            ("Quinn",    "Quinn",    "qa_testing 主"),
            ("Sage",     "Sage",     "documentation 主"),
        };

        // Talent <-> Skill 多對多 baseline assignment（Victoria / Petra orchestrator 0 skill）
        var skillSeeds = new (string TalentName, string SkillName, bool IsPrimary, int Priority)[]
        {
            ("Cody",  "code_implementation", true,  0),
            ("Cody",  "ui_design",           false, 0),
            ("Cody",  "release_publishing",  false, 1),
            ("Vera",  "code_review",         true,  0),
            ("Quinn", "qa_testing",          true,  0),
            ("Sage",  "documentation",       true,  0),
        };

        foreach (var (name, displayName, desc) in talentSeeds)
        {
            // 對齊 AppDbContext.HasIndex (ProjectId, Name).IsUnique() — ProjectId null + Name 唯一檢查
            var existing = await db.Talents.FirstOrDefaultAsync(t => t.ProjectId == null && t.Name == name);
            if (existing is null)
            {
                db.Talents.Add(new Talent
                {
                    Name        = name,
                    DisplayName = displayName,
                    Description = desc,
                    ProjectId   = null,
                    Provider    = null,
                    Model       = null,
                    IsActive    = true,
                });
            }
        }

        // 先 SaveChanges 取得 Talent.Id (DB-generated gen_random_uuid())
        await db.SaveChangesAsync();

        // Talent name -> Id lookup（baseline 全 ProjectId=null 全域 Talent）
        var talentIdByName = await db.Talents
            .Where(t => t.ProjectId == null)
            .ToDictionaryAsync(t => t.Name, t => t.Id);

        foreach (var (talentName, skillName, isPrimary, priority) in skillSeeds)
        {
            if (!talentIdByName.TryGetValue(talentName, out var talentId)) continue;

            // 對齊 AppDbContext.HasIndex (TalentId, SkillName).IsUnique() — 同 Talent 同 Skill 不重複 assign
            var existing = await db.TalentSkills.FirstOrDefaultAsync(s => s.TalentId == talentId && s.SkillName == skillName);
            if (existing is null)
            {
                db.TalentSkills.Add(new TalentSkill
                {
                    TalentId  = talentId,
                    SkillName = skillName,
                    IsPrimary = isPrimary,
                    Priority  = priority,
                });
            }
        }
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
