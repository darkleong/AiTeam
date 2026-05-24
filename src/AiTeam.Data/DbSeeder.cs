using AiTeam.Data.Repositories;
using AiTeam.Data.SeedContent;
using Microsoft.EntityFrameworkCore;

namespace AiTeam.Data;

/// <summary>
/// 啟動時的基礎資料 Seed（幂等，安全地從 Bot 或 Dashboard 呼叫）。
/// </summary>
public static class DbSeeder
{
    // Stage 87 A4：AgentSeeds 陣列砍（v4 9 角色 seed 整段 / agent_configs 表 DROP TABLE / Petra LLM 配置 SoT 收回 talents.Name="Petra" row）

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

        // Stage 87 A4：v4 AgentConfig seed loop 砍（agent_configs 表 DROP TABLE / Talent baseline 已由 EnsureTalentsAsync seed 6 row）

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
        // Stage 85：SkipCeoConfirm seed 砍（v4 dead flag / CommandHandler caller 已砍）
        await EnsureSettingAsync(db, "TokenPricing:InputPer1kUsd",    "0.003",  "每千個 Input Token 費用（USD），預設 Sonnet 費率");
        await EnsureSettingAsync(db, "TokenPricing:OutputPer1kUsd",   "0.015",  "每千個 Output Token 費用（USD），預設 Sonnet 費率");
        await db.SaveChangesAsync();

        // Stage 67：v5.5 Phase 1 Step 2 — baseline 6 Talent + 6 Skill assignment seed（幂等）
        await EnsureTalentsAsync(db);
        await db.SaveChangesAsync();

        // Stage 72：v5.5 Phase 2 Step 5 — baseline 6 SkillPrompts seed（幂等 + race-safe / TalentPrompts 0 row baseline）
        await EnsureSkillPromptsAsync(db);

        // Stage 73：v5.5 Phase 3 Step 7 — 升級 6 SkillPrompts 到 v2「品質 > 做法」content（幂等：v2 active 已存在則 skip）
        await UpgradeSkillPromptsToV2Async(db);

        // Stage 73：v5.5 Phase 3 Step 7 — Petra TalentPrompt persona seed（幂等 + race-safe）
        await EnsurePetraTalentPromptAsync(db);
    }

    /// <summary>
    /// Stage 72：v5.5 Phase 2 Step 5 — 把 v5.5 path 既有 hardcoded prompt seed 進 skill_prompts table（議題 4 內容不動 / 只搬家）。
    ///
    /// 6 個 SkillPrompts（baseline VersionNumber=1 / IsActive=true）：
    /// - code_implementation  ← Resources/CLAUDE_Cody.md content
    /// - code_review          ← Resources/CLAUDE_Vera.md content
    /// - qa_testing           ← Resources/CLAUDE_Quinn.md content
    /// - documentation        ← Resources/CLAUDE_Sage.md content
    /// - ceo_orchestration    ← Resources/CLAUDE_Victoria.md content（seed only — Stage 72 無 CEO read path 整合）
    /// - petra_orchestration  ← PetraPromptTemplate.Template（議題 1 路線 A — 不含 CLAUDE_Petra.md content / 對齊 BuildPetraSystemPrompt 既有 template literal）
    ///
    /// race-safe（對齊 Stage 67 EnsureTalentsAsync pattern）：per-row SaveChanges + catch DbUpdateException + Detach 還原 EF context state。
    /// 檔案不存在 → log warning + skip（不阻擋其他 seed）— production 補檔後重啟 Bot 重新 seed。
    /// </summary>
    private static async Task EnsureSkillPromptsAsync(AppDbContext db)
    {
        var baseDir = AppContext.BaseDirectory;

        // (SkillName, FilePath or "<<INLINE>>" for PetraPromptTemplate)
        var seeds = new (string SkillName, string Source)[]
        {
            ("code_implementation", Path.Combine(baseDir, "Resources", "CLAUDE_Cody.md")),
            ("code_review",         Path.Combine(baseDir, "Resources", "CLAUDE_Vera.md")),
            ("qa_testing",          Path.Combine(baseDir, "Resources", "CLAUDE_Quinn.md")),
            ("documentation",       Path.Combine(baseDir, "Resources", "CLAUDE_Sage.md")),
            ("ceo_orchestration",   Path.Combine(baseDir, "Resources", "CLAUDE_Victoria.md")),
            ("petra_orchestration", "<<INLINE>>"),
        };

        foreach (var (skillName, source) in seeds)
        {
            // 幂等：同 SkillName 已有 active row → skip（重啟 / Bot+Dashboard 並行 seed 場景都安全）
            var existing = await db.SkillPrompts
                .FirstOrDefaultAsync(s => s.SkillName == skillName && s.IsActive);
            if (existing is not null) continue;

            string body;
            if (source == "<<INLINE>>")
            {
                body = PetraPromptTemplate.Template;
            }
            else
            {
                if (!File.Exists(source))
                {
                    // log via Console（DbSeeder 為 static class 無 ILogger 注入 — 對齊既有 pattern 不引入 logger 包袱）
                    Console.WriteLine($"[DbSeeder][Stage72] SkillPrompt 來源檔不存在 skip seed：{skillName} ← {source}");
                    continue;
                }
                body = await File.ReadAllTextAsync(source);
            }

            var seed = new SkillPrompt
            {
                SkillName     = skillName,
                PromptBody    = body,
                VersionNumber = 1,
                IsActive      = true,
                CreatedByUser = null,   // baseline seed = system / Phase 3 audit 才寫 user
            };
            db.SkillPrompts.Add(seed);

            try
            {
                await db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // race loser — 另一個 process 已 seed 同 SkillName → detach 還原 EF context state 繼續下個 seed
                db.Entry(seed).State = EntityState.Detached;
            }
        }
    }

    /// <summary>
    /// Stage 67：v5.5 Phase 1 Step 2 — 確保 baseline 6 Talent + Talent-Skill assignment 存在（幂等 + race-safe）。
    /// 對齊 Phase 1 Step 1 Baseline 拍板（Christ 2026-05-15）：Victoria / Petra 0 skill orchestrator + Cody 兼 3 skill + Vera/Quinn/Sage 主 1 skill。
    /// ProjectId = null 全域共用（Christ 決議 2 per-Project 隔離 baseline 全 null）。
    /// Provider / Model = null（runtime fallback Agents:Dev:Model 既有 BuildSessionContext pattern）。
    ///
    /// Stage 67 Forge 自驗 follow-up fix（2026-05-15）：
    /// - Bot + Dashboard 啟動同時跑 DbSeeder.SeedAsync → race condition 塞重複 row
    /// - PostgreSQL `NULL ≠ NULL` unique constraint 對 ProjectId=null 不阻擋（partial unique index 才解 — 對齊 Stage67FixTalentPartialUniqueIndex Migration）
    /// - 三層 race-safe：① per-Talent SaveChanges + catch DbUpdateException 忽略 race loser ② ToDictionaryAsync dedupe 防禦（GroupBy.First）③ 失敗後 detach entity 還原 EF context state
    /// </summary>
    private static async Task EnsureTalentsAsync(AppDbContext db)
    {
        // baseline 6 Talent — 對齊 Phase 1 Step 1 Baseline 拍板
        var talentSeeds = new (string Name, string DisplayName, string Description)[]
        {
            ("Victoria", "Victoria", "Orchestrator - 接 Christ 指令、forward 任務（無 Skill assignment）"),
            ("Petra",    "Petra",    "Orchestrator - 拆解任務 / 派 Talent / 吸收 requirements_extraction 紀律（無 Skill assignment）"),
            ("Cody",     "Cody",     "code_implementation 主"),   // Stage 78a：砍 ui_design + release_publishing 兼 — 對應 SkillRegistry 縮為 4 Skill baseline
            ("Vera",     "Vera",     "code_review 主"),
            ("Quinn",    "Quinn",    "qa_testing 主"),
            ("Sage",     "Sage",     "documentation 主"),
        };

        // Talent <-> Skill 多對多 baseline assignment（Victoria / Petra orchestrator 0 skill）
        var skillSeeds = new (string TalentName, string SkillName, bool IsPrimary, int Priority)[]
        {
            ("Cody",  "code_implementation", true,  0),
            // Stage 78a：砍 Cody-ui_design + Cody-release_publishing 兼 Skill assignment — 對應 SkillRegistry 縮為 4 Skill baseline / production 既有 TalentSkill row 不自動清（無 fresh seed reference）
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
                var talent = new Talent
                {
                    Name        = name,
                    DisplayName = displayName,
                    Description = desc,
                    ProjectId   = null,
                    Provider    = null,
                    Model       = null,
                    IsActive    = true,
                };
                db.Talents.Add(talent);

                // per-Talent SaveChanges + catch DbUpdateException race loser（Bot + Dashboard 並行 seed 場景）
                try
                {
                    await db.SaveChangesAsync();
                }
                catch (DbUpdateException)
                {
                    // race loser — 另一個 process 已 seed 同名 Talent → detach 還原 EF context state 繼續下個 Talent
                    db.Entry(talent).State = Microsoft.EntityFrameworkCore.EntityState.Detached;
                }
            }
        }

        // Talent name -> Id lookup（baseline 全 ProjectId=null 全域 Talent）
        // 防禦 dedupe：partial unique index 修根因前的歷史 DB 可能有重複 row — GroupBy.First 取最早一筆避免 ToDictionaryAsync 爆
        var talentList = await db.Talents
            .Where(t => t.ProjectId == null)
            .OrderBy(t => t.CreatedAt)
            .ToListAsync();
        var talentIdByName = talentList
            .GroupBy(t => t.Name)
            .ToDictionary(g => g.Key, g => g.First().Id);

        foreach (var (talentName, skillName, isPrimary, priority) in skillSeeds)
        {
            if (!talentIdByName.TryGetValue(talentName, out var talentId)) continue;

            // 對齊 AppDbContext.HasIndex (TalentId, SkillName).IsUnique() — 同 Talent 同 Skill 不重複 assign
            var existing = await db.TalentSkills.FirstOrDefaultAsync(s => s.TalentId == talentId && s.SkillName == skillName);
            if (existing is null)
            {
                var ts = new TalentSkill
                {
                    TalentId  = talentId,
                    SkillName = skillName,
                    IsPrimary = isPrimary,
                    Priority  = priority,
                };
                db.TalentSkills.Add(ts);

                try
                {
                    await db.SaveChangesAsync();
                }
                catch (DbUpdateException)
                {
                    // race loser — 另一個 process 已 seed 同 Talent 同 Skill
                    db.Entry(ts).State = Microsoft.EntityFrameworkCore.EntityState.Detached;
                }
            }
        }
    }

    /// <summary>
    /// Stage 73：v5.5 Phase 3 Step 7 — 把 6 SkillPrompt 從 v1 baseline 升級到 v2「品質 > 做法」content。
    ///
    /// 走 PromptRepository.UpsertSkillPromptAsync versioning path：
    /// - 同 SkillName 已存在 VersionNumber >= 2 active row → skip（幂等：重啟 / 並行 seed 安全）
    /// - 否則 Upsert → 舊 v1 切 IsActive=false（audit trail 保留）+ 新 v2 IsActive=true / VersionNumber=max+1
    ///
    /// race-safe（對齊 Stage 72 EnsureSkillPromptsAsync pattern）：per-skill SaveChanges + catch DbUpdateException + 單 entity Detach。
    /// 檔案不存在 → log warning + skip（不阻擋其他 seed）。
    ///
    /// 已知 trade-off：fresh DB 首次部署會 seed v1 → 立刻升 v2 → v1 archive row 看起來像「歷史」但其實是 0 秒前 seed。
    /// 接受現狀理由：fresh DB rare event / production 升級才是主場景 / 改進方案會破壞 Stage 72 既有 EnsureSkillPromptsAsync 幂等紀律。
    /// </summary>
    private static async Task UpgradeSkillPromptsToV2Async(AppDbContext db)
    {
        var baseDir = AppContext.BaseDirectory;
        var seeds = new (string SkillName, string Source)[]
        {
            ("code_implementation", Path.Combine(baseDir, "Resources", "CLAUDE_Cody.md")),
            ("code_review",         Path.Combine(baseDir, "Resources", "CLAUDE_Vera.md")),
            ("qa_testing",          Path.Combine(baseDir, "Resources", "CLAUDE_Quinn.md")),
            ("documentation",       Path.Combine(baseDir, "Resources", "CLAUDE_Sage.md")),
            ("ceo_orchestration",   Path.Combine(baseDir, "Resources", "CLAUDE_Victoria.md")),
            ("petra_orchestration", "<<INLINE>>"),
        };

        var repo = new PromptRepository(db);

        foreach (var (skillName, source) in seeds)
        {
            // 幂等：同 SkillName VersionNumber >= 2 active row 已存在 → skip
            var active = await db.SkillPrompts
                .FirstOrDefaultAsync(s => s.SkillName == skillName && s.IsActive);
            if (active is not null && active.VersionNumber >= 2) continue;

            string body;
            if (source == "<<INLINE>>")
            {
                body = PetraPromptTemplate.Template;
            }
            else
            {
                if (!File.Exists(source))
                {
                    Console.WriteLine($"[DbSeeder][Stage73] SkillPrompt 來源檔不存在 skip upgrade：{skillName} ← {source}");
                    continue;
                }
                body = await File.ReadAllTextAsync(source);
            }

            var newEntity = await repo.UpsertSkillPromptAsync(skillName, body, createdByUser: "stage73-upgrade");

            try
            {
                await db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // race loser — 另一個 process 已 upgrade 同 SkillName → 只 detach 失敗那一個（對齊 Stage 72 EnsureSkillPromptsAsync pattern）
                db.Entry(newEntity).State = EntityState.Detached;
            }
        }
    }

    /// <summary>
    /// Stage 73：v5.5 Phase 3 Step 7 — Petra TalentPrompt persona seed（VersionNumber=1 / IsActive=true）。
    ///
    /// 走 PromptRepository.UpsertTalentPromptAsync：
    /// - Petra Talent 已存在 active TalentPrompt → skip（幂等）
    /// - 否則 Upsert 新 row
    ///
    /// race-safe per-Talent SaveChanges + catch DbUpdateException + 單 entity Detach（對齊 Stage 67 EnsureTalentsAsync pattern）。
    /// Petra Talent 不存在（EnsureTalentsAsync 失敗）→ log warning + skip。
    /// </summary>
    private static async Task EnsurePetraTalentPromptAsync(AppDbContext db)
    {
        var petra = await db.Talents
            .FirstOrDefaultAsync(t => t.ProjectId == null && t.Name == "Petra");
        if (petra is null)
        {
            Console.WriteLine("[DbSeeder][Stage73] Petra Talent 不存在 skip TalentPrompt seed（檢查 EnsureTalentsAsync log）");
            return;
        }

        var existing = await db.TalentPrompts
            .FirstOrDefaultAsync(t => t.TalentId == petra.Id && t.IsActive);
        if (existing is not null) return;   // 幂等：active row 已存在

        var repo = new PromptRepository(db);
        var newEntity = await repo.UpsertTalentPromptAsync(petra.Id, PetraPersonaSeed.PersonaBody);

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // race loser — 對齊 Stage 67 EnsureTalentsAsync 單 entity detach pattern
            db.Entry(newEntity).State = EntityState.Detached;
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
