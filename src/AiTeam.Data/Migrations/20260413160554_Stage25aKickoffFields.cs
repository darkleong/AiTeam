using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiTeam.Data.Migrations
{
    /// <inheritdoc />
    public partial class Stage25aKickoffFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "KickoffMeetingLog",
                table: "task_groups",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "KickoffRound",
                table: "task_groups",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "TaskPlan",
                table: "task_groups",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "KickoffMeetingLog",
                table: "task_groups");

            migrationBuilder.DropColumn(
                name: "KickoffRound",
                table: "task_groups");

            migrationBuilder.DropColumn(
                name: "TaskPlan",
                table: "task_groups");
        }
    }
}
