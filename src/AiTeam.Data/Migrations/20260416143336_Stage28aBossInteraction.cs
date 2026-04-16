using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiTeam.Data.Migrations
{
    /// <inheritdoc />
    public partial class Stage28aBossInteraction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "boss_interactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    TaskGroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    TaskItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    InteractionType = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Project = table.Column<string>(type: "text", nullable: true),
                    AgentName = table.Column<string>(type: "text", nullable: true),
                    AvailableActionsJson = table.Column<string>(type: "text", nullable: false),
                    ResponseAction = table.Column<string>(type: "text", nullable: true),
                    ResponseSource = table.Column<string>(type: "text", nullable: true),
                    RespondedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DiscordMessageId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    ContextJson = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ProcessedByBot = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_boss_interactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_boss_interactions_task_groups_TaskGroupId",
                        column: x => x.TaskGroupId,
                        principalTable: "task_groups",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_boss_interactions_tasks_TaskItemId",
                        column: x => x.TaskItemId,
                        principalTable: "tasks",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_boss_interactions_DiscordMessageId",
                table: "boss_interactions",
                column: "DiscordMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_boss_interactions_Status_CreatedAt",
                table: "boss_interactions",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_boss_interactions_TaskGroupId",
                table: "boss_interactions",
                column: "TaskGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_boss_interactions_TaskItemId",
                table: "boss_interactions",
                column: "TaskItemId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "boss_interactions");
        }
    }
}
