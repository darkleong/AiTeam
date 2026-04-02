using Microsoft.EntityFrameworkCore;

namespace AiTeam.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<AgentConfig> AgentConfigs => Set<AgentConfig>();
    public DbSet<TaskItem> Tasks => Set<TaskItem>();
    public DbSet<TaskLog> TaskLogs => Set<TaskLog>();
    public DbSet<TokenLog> TokenLogs => Set<TokenLog>();
    public DbSet<Rule> Rules => Set<Rule>();
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();

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
        });

        modelBuilder.Entity<TaskItem>(e =>
        {
            e.ToTable("tasks");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            e.HasOne(x => x.Team).WithMany(t => t.Tasks).HasForeignKey(x => x.TeamId).IsRequired(false);
            e.HasOne(x => x.Project).WithMany(p => p.Tasks).HasForeignKey(x => x.ProjectId).IsRequired(false);
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
        });

        modelBuilder.Entity<AppSetting>(e =>
        {
            e.ToTable("app_settings");
            e.HasKey(x => x.Key);
        });
    }
}
