using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiTeam.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskGroupDevPlan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DevPlan",
                table: "task_groups",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DevPlanRevision",
                table: "task_groups",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DevPlan",
                table: "task_groups");

            migrationBuilder.DropColumn(
                name: "DevPlanRevision",
                table: "task_groups");
        }
    }
}
