using AiTeam.Data.Records;
using Microsoft.EntityFrameworkCore;

namespace AiTeam.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    // Stage 91：v4-rewrite MCP record system 5 個新表（Agent* prefix 區分既有 Team entity / table mcp_* prefix）
    public DbSet<AgentTeam>       AgentTeams       => Set<AgentTeam>();
    public DbSet<AgentTeammate>   AgentTeammates   => Set<AgentTeammate>();
    public DbSet<AgentTask>       AgentTasks       => Set<AgentTask>();
    public DbSet<AgentMessage>    AgentMessages    => Set<AgentMessage>();
    public DbSet<AgentTokenUsage> AgentTokenUsages => Set<AgentTokenUsage>();

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

    // v4.0.5 F1：v3-v5 dead entity DbSet × 9 砍（PetraSession / PetraSessionMessage / Talent / TalentSkill /
    //                                          TaskMemory / TalentMemory / SkillPrompt / TalentPrompt / PetraInbox）
    // 對應 DROP TABLE 9 個 / 對齊 v4.0.0 純記錄系統定位

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

        // v4.0.5 F1：v3-v5 dead entity OnModelCreating fluent × 9 砍
        // PetraSession / PetraSessionMessage / Talent / TalentSkill / TaskMemory / TalentMemory /
        // SkillPrompt / TalentPrompt / PetraInbox — 對應 9 個表 Migration DROP TABLE

        // ─────────────────────────────────────────────────────────────────────
        // Stage 91：v4-rewrite MCP record system — 5 個新表
        // ─────────────────────────────────────────────────────────────────────

        modelBuilder.Entity<AgentTeam>(e =>
        {
            e.ToTable("mcp_teams");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            e.HasIndex(x => new { x.Status, x.CreatedAt });
        });

        modelBuilder.Entity<AgentTeammate>(e =>
        {
            e.ToTable("mcp_teammates");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            e.HasIndex(x => x.TeamId);
        });

        modelBuilder.Entity<AgentTask>(e =>
        {
            e.ToTable("mcp_tasks");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.DependenciesJson).HasColumnType("jsonb");
            e.HasIndex(x => x.TeamId);
            e.HasIndex(x => new { x.Status, x.CreatedAt });
        });

        modelBuilder.Entity<AgentMessage>(e =>
        {
            e.ToTable("mcp_messages");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.Content).HasColumnType("text");
            e.Property(x => x.ToolCallJson).HasColumnType("jsonb");
            e.HasIndex(x => new { x.TeammateId, x.CreatedAt });
            e.HasIndex(x => x.TaskId);
        });

        modelBuilder.Entity<AgentTokenUsage>(e =>
        {
            e.ToTable("mcp_token_usage");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            e.HasIndex(x => new { x.TeammateId, x.CreatedAt });
            e.HasIndex(x => x.TaskId);
        });
    }
}
