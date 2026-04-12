using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiTeam.Data.Migrations
{
    /// <inheritdoc />
    public partial class Stage23TaskGroupFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ImplementationNote",
                table: "task_groups",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewAppealLog",
                table: "task_groups",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReviewAppealRoundA",
                table: "task_groups",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "SkipReviewerAfterArbitration",
                table: "task_groups",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImplementationNote",
                table: "task_groups");

            migrationBuilder.DropColumn(
                name: "ReviewAppealLog",
                table: "task_groups");

            migrationBuilder.DropColumn(
                name: "ReviewAppealRoundA",
                table: "task_groups");

            migrationBuilder.DropColumn(
                name: "SkipReviewerAfterArbitration",
                table: "task_groups");
        }
    }
}
