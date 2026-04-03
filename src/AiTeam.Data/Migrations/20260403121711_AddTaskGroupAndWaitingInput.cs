using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiTeam.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskGroupAndWaitingInput : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "GroupId",
                table: "tasks",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "task_groups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Project = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    WorkflowType = table.Column<string>(type: "text", nullable: false),
                    IssueUrls = table.Column<string>(type: "jsonb", nullable: true),
                    UiSpecPath = table.Column<string>(type: "text", nullable: true),
                    DevPrUrl = table.Column<string>(type: "text", nullable: true),
                    FixIteration = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_task_groups", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_tasks_GroupId",
                table: "tasks",
                column: "GroupId");

            migrationBuilder.AddForeignKey(
                name: "FK_tasks_task_groups_GroupId",
                table: "tasks",
                column: "GroupId",
                principalTable: "task_groups",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_tasks_task_groups_GroupId",
                table: "tasks");

            migrationBuilder.DropTable(
                name: "task_groups");

            migrationBuilder.DropIndex(
                name: "IX_tasks_GroupId",
                table: "tasks");

            migrationBuilder.DropColumn(
                name: "GroupId",
                table: "tasks");
        }
    }
}
