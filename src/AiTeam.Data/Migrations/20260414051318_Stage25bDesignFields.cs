using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiTeam.Data.Migrations
{
    /// <inheritdoc />
    public partial class Stage25bDesignFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DesignMeetingLog",
                table: "task_groups",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DesignPlan",
                table: "task_groups",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DesignRound",
                table: "task_groups",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DesignMeetingLog",
                table: "task_groups");

            migrationBuilder.DropColumn(
                name: "DesignPlan",
                table: "task_groups");

            migrationBuilder.DropColumn(
                name: "DesignRound",
                table: "task_groups");
        }
    }
}
