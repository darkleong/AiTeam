using Microsoft.EntityFrameworkCore;

namespace AiTeam.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<Project> Projects => Set<Project>();
    // Stage 87 A4：AgentConfigs DbSet 砍（agent_configs 表 DROP TABLE / Petra LLM 配置 SoT 收回 talents.Name="Petra" row）
    public DbSet<TaskGroup> TaskGroups => Set<TaskGroup>();
    public DbSet<TaskItem> Tasks => Set<TaskItem>();
    public DbSet<TaskLog> TaskLogs => Set<TaskLog>();
    public DbSet<TokenLog> TokenLogs => Set<TokenLog>();
    public DbSet<Rule> Rules => Set<Rule>();
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();
    public DbSet<CeoConversation> CeoConversations => Set<CeoConversation>();
    public DbSet<CeoMemory> CeoMemories => Set<CeoMemory>();
    public DbSet<BossInteraction> BossInteractions => Set<BossInteraction>();
    public DbSet<BossCommandLog>  BossCommandLogs  => Set<BossCommandLog>();

    // Stage 63B：Petra Orchestrator session 持久化（v5 動態架構 PoC）
    public DbSet<PetraSession>        PetraSessions        => Set<PetraSession>();
    public DbSet<PetraSessionMessage> PetraSessionMessages => Set<PetraSessionMessage>();

    // Stage 67：v5.5 Phase 1 Step 2 — Talent registry + Talent-Skill 多對多 assignment（baseline 6 Talent）
    public DbSet<Talent>      Talents      => Set<Talent>();
    public DbSet<TalentSkill> TalentSkills => Set<TalentSkill>();

    // Stage 69：v5.5 Phase 2 Step 3 — 跨 session 長期持久記憶（per-Task 共用 + per-Talent 私有 hybrid 雙層）
    public DbSet<TaskMemory>   TaskMemories   => Set<TaskMemory>();
    public DbSet<TalentMemory> TalentMemories => Set<TalentMemory>();

    // Stage 72：v5.5 Phase 2 Step 5 — Prompt DB 化（職位層 SkillPrompt + 個性層 TalentPrompt 兩層 schema）
    public DbSet<SkillPrompt>  SkillPrompts  => Set<SkillPrompt>();
    public DbSet<TalentPrompt> TalentPrompts => Set<TalentPrompt>();

    // Stage 75：v5.5 Phase 3 — Petra 接收層 queue（FIFO DB-as-Queue / BackgroundService polling）
    public DbSet<PetraInbox> PetraInbox => Set<PetraInbox>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Team>(e =>
        {
            e.ToTable("teams");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
        });

        modelBuilder.Entity<Project>(e =>
        {
            e.ToTable("projects");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.TechStack).HasColumnType("jsonb");
            e.HasOne(x => x.Team).WithMany(t => t.Projects).HasForeignKey(x => x.TeamId);
        });

        // Stage 87 A4：AgentConfig entity 配置砍（agent_configs 表 DROP TABLE）

        modelBuilder.Entity<TaskGroup>(e =>
        {
            e.ToTable("task_groups");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.IssueUrls).HasColumnType("jsonb");
            e.HasIndex(x => x.Status);
            // Stage 13：新增 ProjectId FK
            e.HasOne(x => x.ProjectRef)
             .WithMany()
             .HasForeignKey(x => x.ProjectId)
             .IsRequired(false);
        });

        modelBuilder.Entity<TaskItem>(e =>
        {
            e.ToTable("tasks");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            e.HasOne(x => x.Team).WithMany(t => t.Tasks).HasForeignKey(x => x.TeamId).IsRequired(false);
            e.HasOne(x => x.Project).WithMany(p => p.Tasks).HasForeignKey(x => x.ProjectId).IsRequired(false);
            e.HasOne(x => x.Group).WithMany(g => g.Tasks).HasForeignKey(x => x.GroupId).IsRequired(false);
        });

        modelBuilder.Entity<TaskLog>(e =>
        {
            e.ToTable("task_logs");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.Payload).HasColumnType("jsonb");
            e.HasOne(x => x.Task).WithMany(t => t.Logs).HasForeignKey(x => x.TaskId);
        });

        modelBuilder.Entity<Rule>(e =>
        {
            e.ToTable("rules");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            e.HasOne(x => x.Team).WithMany().HasForeignKey(x => x.TeamId).IsRequired(false);
        });

        modelBuilder.Entity<TokenLog>(e =>
        {
            e.ToTable("token_logs");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            e.HasOne(x => x.Task).WithMany().HasForeignKey(x => x.TaskId).IsRequired(false);
            // Stage 44：對齊 Anthropic 帳單精度（USD 第六位，如 $0.008487）
            e.Property(x => x.TotalCostUsd).HasPrecision(18, 6);
            // Stage 81 補強 #B：PetraSessionId non-unique index — `UpdateSessionCostUsdAsync WHERE PetraSessionId=...` 高頻 SumAsync query 性能保險。
            // 對齊既有 TaskId FK 紀律（無顯式 HasIndex 但 FK 自動 index）— PetraSessionId 非 FK / 需顯式 HasIndex。
            e.HasIndex(x => x.PetraSessionId);
        });

        modelBuilder.Entity<AppSetting>(e =>
        {
            e.ToTable("app_settings");
            e.HasKey(x => x.Key);
        });

        modelBuilder.Entity<CeoConversation>(e =>
        {
            e.ToTable("ceo_conversations");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            e.HasIndex(x => new { x.UserId, x.SessionId });
            e.HasIndex(x => x.CreatedAt);
        });

        modelBuilder.Entity<CeoMemory>(e =>
        {
            e.ToTable("ceo_memories");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            e.HasIndex(x => new { x.UserId, x.IsActive });
        });

        // Stage 29-5：老闆從 Dashboard 下達的指令記錄
        modelBuilder.Entity<BossCommandLog>(e =>
        {
            e.ToTable("boss_command_logs");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.Images).HasColumnType("jsonb");
            e.Property(x => x.CeoResponseRaw).HasColumnType("text");
            e.HasIndex(x => x.CreatedAt);
        });

        // Stage 28a：老闆互動雙通道記錄
        modelBuilder.Entity<BossInteraction>(e =>
        {
            e.ToTable("boss_interactions");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.DiscordMessageId).HasColumnType("numeric(20,0)");
            e.Property(x => x.ResponseContent).HasColumnType("text");
            e.HasIndex(x => new { x.Status, x.CreatedAt });
            e.HasIndex(x => x.DiscordMessageId);
            e.HasOne(x => x.TaskGroup).WithMany().HasForeignKey(x => x.TaskGroupId).IsRequired(false);
            e.HasOne(x => x.TaskItem).WithMany().HasForeignKey(x => x.TaskItemId).IsRequired(false);

            // Stage 57-fix（FF 五十一 fire 端 race window 補強，路線 a Christ 拍板）：
            // partial unique index — 同 (TaskGroupId, InteractionType) 只允許 1 row Status='pending'
            // DB-level race-free，補 TryCreateUniqueInteractionAsync read-then-write TOCTOU window
            e.HasIndex(x => new { x.TaskGroupId, x.InteractionType })
                .HasFilter("\"Status\" = 'pending'")
                .IsUnique()
                .HasDatabaseName("ix_boss_interactions_pending_per_group_type");
        });

        // Stage 63B：Petra Orchestrator session（v5 動態架構 PoC）
        modelBuilder.Entity<PetraSession>(e =>
        {
            e.ToTable("petra_sessions");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            e.HasOne(x => x.TaskGroup).WithMany().HasForeignKey(x => x.TaskGroupId).IsRequired(false);
            e.HasIndex(x => x.TaskGroupId);
            e.HasIndex(x => new { x.Status, x.CreatedAt });
        });

        modelBuilder.Entity<PetraSessionMessage>(e =>
        {
            e.ToTable("petra_session_messages");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.Content).HasColumnType("text");
            e.HasOne(x => x.Session).WithMany(s => s.Messages).HasForeignKey(x => x.SessionId);
            e.HasIndex(x => new { x.SessionId, x.CreatedAt });
        });

        // Stage 67：v5.5 Phase 1 Step 2 — Talent registry（DB-driven baseline 6 instance / per-Project nullable）
        // Stage 67 Forge 自驗 follow-up fix（2026-05-15）：PostgreSQL `NULL ≠ NULL` unique constraint 對 ProjectId=null 不阻擋
        // → 拆成兩個 partial unique index 真正 enforce：
        //   1. (ProjectId, Name) WHERE ProjectId IS NOT NULL — per-Project Talent name 唯一
        //   2. (Name) WHERE ProjectId IS NULL — 全域 Talent name 唯一
        modelBuilder.Entity<Talent>(e =>
        {
            e.ToTable("talents");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            // per-Project unique index（ProjectId NOT NULL 群組）
            e.HasIndex(x => new { x.ProjectId, x.Name })
                .IsUnique()
                .HasFilter("\"ProjectId\" IS NOT NULL")
                .HasDatabaseName("ix_talents_project_name_per_project");
            // 全域 unique index（ProjectId NULL 群組 — 解 PostgreSQL NULL ≠ NULL 雷）
            e.HasIndex(x => x.Name)
                .IsUnique()
                .HasFilter("\"ProjectId\" IS NULL")
                .HasDatabaseName("ix_talents_name_global");
            e.HasOne(x => x.ProjectRef).WithMany().HasForeignKey(x => x.ProjectId).IsRequired(false);
        });

        // Stage 67：v5.5 Phase 1 Step 2 — Talent ↔ Skill 多對多 assignment
        modelBuilder.Entity<TalentSkill>(e =>
        {
            e.ToTable("talent_skills");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            // 防 Talent 同 Skill 重複 assign
            e.HasIndex(x => new { x.TalentId, x.SkillName }).IsUnique();
            e.HasOne(x => x.Talent).WithMany(t => t.Skills).HasForeignKey(x => x.TalentId);
        });

        // Stage 69 v2.1：v5.5 Phase 2 Step 3 — per-Task 共用記憶 scope = PetraSession（不是 v4 TaskGroup）
        // 對齊 v5.5「每次 CEO 觸發 = 一個 PetraSession = 一個 Task event」設計精神
        modelBuilder.Entity<TaskMemory>(e =>
        {
            e.ToTable("task_memories");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            // PetraSessionId required FK — 對齊 petra_sessions（cascade delete 自然清理）
            e.HasOne<PetraSession>()
                .WithMany()
                .HasForeignKey(x => x.PetraSessionId)
                .OnDelete(DeleteBehavior.Cascade);
            // (PetraSessionId, Key) 唯一 — 同 session 同 key 視為 upsert
            e.HasIndex(x => new { x.PetraSessionId, x.Key })
                .IsUnique()
                .HasDatabaseName("ix_task_memories_session_key");
            // compact 排序常用 — (PetraSessionId, CreatedAt) 查 oldest
            e.HasIndex(x => new { x.PetraSessionId, x.CreatedAt });
        });

        // Stage 69：v5.5 Phase 2 Step 3 — per-Talent 私有記憶（個人記憶 / 跨 task 累積）
        // 對齊 Stage 67 Talent schema partial unique index 紀律（docs/conventions/ef-core.md Stage 68 新段）：
        //   1. (TalentId, Key, ProjectId) WHERE ProjectId IS NOT NULL — per-Project 隔離 talent memory key 唯一
        //   2. (TalentId, Key) WHERE ProjectId IS NULL — 全域 Talent memory key 唯一（解 PostgreSQL NULL ≠ NULL 雷）
        modelBuilder.Entity<TalentMemory>(e =>
        {
            e.ToTable("talent_memories");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            // Tags 用 PostgreSQL text[] 對齊 Roadmap 設計（簡單 keyword search 起步）
            e.Property(x => x.Tags).HasColumnType("text[]");
            // Talent FK
            e.HasOne(x => x.Talent).WithMany().HasForeignKey(x => x.TalentId);
            // per-Project unique index（ProjectId NOT NULL 群組）
            e.HasIndex(x => new { x.TalentId, x.Key, x.ProjectId })
                .IsUnique()
                .HasFilter("\"ProjectId\" IS NOT NULL")
                .HasDatabaseName("ix_talent_memories_talent_key_project");
            // 全域 unique index（ProjectId NULL 群組 — 解 PostgreSQL NULL ≠ NULL 雷）
            e.HasIndex(x => new { x.TalentId, x.Key })
                .IsUnique()
                .HasFilter("\"ProjectId\" IS NULL")
                .HasDatabaseName("ix_talent_memories_talent_key_global");
            // compact 排序常用 — (TalentId, CreatedAt)
            e.HasIndex(x => new { x.TalentId, x.CreatedAt });
        });

        // Stage 72：v5.5 Phase 2 Step 5 — SkillPrompt（職位層）
        // partial unique index：同 SkillName 只一條 IsActive=true（version archive row IsActive=false 不衝突）
        modelBuilder.Entity<SkillPrompt>(e =>
        {
            e.ToTable("skill_prompts");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.PromptBody).HasColumnType("text");
            // partial unique — 對齊 Stage 67/69 既有紀律（ef-core.md Stage 68 段 PostgreSQL NULL ≠ NULL 雷 partial filter 修法延伸）
            e.HasIndex(x => x.SkillName)
                .IsUnique()
                .HasFilter("\"IsActive\" = true")
                .HasDatabaseName("ix_skill_prompts_active_per_skill");
            // 版本歷史查詢用
            e.HasIndex(x => new { x.SkillName, x.VersionNumber });
        });

        // Stage 72：v5.5 Phase 2 Step 5 — TalentPrompt（個性層 / per-Talent）
        modelBuilder.Entity<TalentPrompt>(e =>
        {
            e.ToTable("talent_prompts");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.PersonaBody).HasColumnType("text");
            e.HasOne(x => x.Talent).WithMany().HasForeignKey(x => x.TalentId);
            // partial unique — 同 TalentId 只一條 IsActive=true
            e.HasIndex(x => x.TalentId)
                .IsUnique()
                .HasFilter("\"IsActive\" = true")
                .HasDatabaseName("ix_talent_prompts_active_per_talent");
            e.HasIndex(x => new { x.TalentId, x.VersionNumber });
        });

        // Stage 75：v5.5 Phase 3 — PetraInbox（接收層 queue）
        modelBuilder.Entity<PetraInbox>(e =>
        {
            e.ToTable("petra_inbox");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.UserInput).HasColumnType("text");
            e.Property(x => x.ErrorMessage).HasColumnType("text");
            // Stage 79：v5.5 image flow 補完 — Attachments jsonb（半抽象 future-friendly）
            e.Property(x => x.Attachments).HasColumnType("jsonb");
            // polling 用 index（Status + EnqueuedAt — FIFO 紀律）
            e.HasIndex(x => new { x.Status, x.EnqueuedAt })
                .HasDatabaseName("ix_petra_inbox_status_enqueued");
            // Stage 76：retry path polling 紀律（Status + NextRetryAt — 守 backoff timing）
            e.HasIndex(x => new { x.Status, x.NextRetryAt })
                .HasDatabaseName("ix_petra_inbox_status_next_retry");
        });
    }
}
