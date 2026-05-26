using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiTeam.Data.Migrations
{
    /// <inheritdoc />
    public partial class Stage91McpRecordSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "mcp_messages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    TeammateId = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskId = table.Column<Guid>(type: "uuid", nullable: true),
                    Role = table.Column<string>(type: "text", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    ToolCallJson = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mcp_messages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "mcp_tasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    TeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    TeammateId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    DependenciesJson = table.Column<string>(type: "jsonb", nullable: true),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ClaimedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mcp_tasks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "mcp_teammates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    TeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Model = table.Column<string>(type: "text", nullable: true),
                    Role = table.Column<string>(type: "text", nullable: false),
                    SpawnedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FinishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mcp_teammates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "mcp_teams",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ClosedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mcp_teams", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "mcp_token_usage",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    TeammateId = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskId = table.Column<Guid>(type: "uuid", nullable: true),
                    InputTokens = table.Column<int>(type: "integer", nullable: false),
                    OutputTokens = table.Column<int>(type: "integer", nullable: false),
                    CacheCreationTokens = table.Column<int>(type: "integer", nullable: true),
                    CacheReadTokens = table.Column<int>(type: "integer", nullable: true),
                    Model = table.Column<string>(type: "text", nullable: true),
                    EstimatedCostUsd = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mcp_token_usage", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_mcp_messages_TaskId",
                table: "mcp_messages",
                column: "TaskId");

            migrationBuilder.CreateIndex(
                name: "IX_mcp_messages_TeammateId_CreatedAt",
                table: "mcp_messages",
                columns: new[] { "TeammateId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_mcp_tasks_Status_CreatedAt",
                table: "mcp_tasks",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_mcp_tasks_TeamId",
                table: "mcp_tasks",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_mcp_teammates_TeamId",
                table: "mcp_teammates",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_mcp_teams_Status_CreatedAt",
                table: "mcp_teams",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_mcp_token_usage_TaskId",
                table: "mcp_token_usage",
                column: "TaskId");

            migrationBuilder.CreateIndex(
                name: "IX_mcp_token_usage_TeammateId_CreatedAt",
                table: "mcp_token_usage",
                columns: new[] { "TeammateId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "mcp_messages");

            migrationBuilder.DropTable(
                name: "mcp_tasks");

            migrationBuilder.DropTable(
                name: "mcp_teammates");

            migrationBuilder.DropTable(
                name: "mcp_teams");

            migrationBuilder.DropTable(
                name: "mcp_token_usage");
        }
    }
}
