using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiTeam.Data.Migrations
{
    /// <inheritdoc />
    public partial class Stage47AgentConfigTokenLimits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DailyTokenLimitK",
                table: "agent_configs",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MonthlyTokenLimitK",
                table: "agent_configs",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DailyTokenLimitK",
                table: "agent_configs");

            migrationBuilder.DropColumn(
                name: "MonthlyTokenLimitK",
                table: "agent_configs");
        }
    }
}
