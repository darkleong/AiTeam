using Microsoft.EntityFrameworkCore;

namespace AiTeam.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<AgentConfig> AgentConfigs => Set<AgentConfig>();
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

        modelBuilder.Entity<AgentConfig>(e =>
        {
            e.ToTable("agent_configs");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            e.HasOne(x => x.Team).WithMany(t => t.Agents).HasForeignKey(x => x.TeamId);
            // Stage 16：防止競態條件重複 seed（Bot + Dashboard 同時啟動）
            e.HasIndex(x => x.Name).IsUnique();
        });

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
        });
    }
}
