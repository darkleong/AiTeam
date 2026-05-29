using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiTeam.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTeamProjectName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProjectName",
                table: "mcp_teams",
                type: "text",
                nullable: true);

            // backfill：既有 row 預設視為 AiTeam（v4 之前唯一 project）
            migrationBuilder.Sql(@"UPDATE mcp_teams SET ""ProjectName"" = 'AiTeam' WHERE ""ProjectName"" IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProjectName",
                table: "mcp_teams");
        }
    }
}
