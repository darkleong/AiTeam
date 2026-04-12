using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiTeam.Data.Migrations
{
    /// <inheritdoc />
    public partial class Stage24TaskGroupFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DevPlanAppealLog",
                table: "task_groups",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DevPlanAppealRoundA",
                table: "task_groups",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "QaFixRound",
                table: "task_groups",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "TestReport",
                table: "task_groups",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DevPlanAppealLog",
                table: "task_groups");

            migrationBuilder.DropColumn(
                name: "DevPlanAppealRoundA",
                table: "task_groups");

            migrationBuilder.DropColumn(
                name: "QaFixRound",
                table: "task_groups");

            migrationBuilder.DropColumn(
                name: "TestReport",
                table: "task_groups");
        }
    }
}
