using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiTeam.Data.Migrations
{
    /// <inheritdoc />
    public partial class Stage46TaskGroupEpic : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EpicPaused",
                table: "task_groups",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ParentGroupId",
                table: "task_groups",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhaseDescription",
                table: "task_groups",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PhaseNumber",
                table: "task_groups",
                type: "integer",
                nullable: true);

            // Stage 46：partial index 加速 sub-task 查詢（Sequential 鏈執行 + Dashboard epic 折疊查詢）
            migrationBuilder.Sql("""
                CREATE INDEX IF NOT EXISTS idx_task_groups_parent
                ON task_groups("ParentGroupId")
                WHERE "ParentGroupId" IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS idx_task_groups_parent;");

            migrationBuilder.DropColumn(
                name: "EpicPaused",
                table: "task_groups");

            migrationBuilder.DropColumn(
                name: "ParentGroupId",
                table: "task_groups");

            migrationBuilder.DropColumn(
                name: "PhaseDescription",
                table: "task_groups");

            migrationBuilder.DropColumn(
                name: "PhaseNumber",
                table: "task_groups");
        }
    }
}
