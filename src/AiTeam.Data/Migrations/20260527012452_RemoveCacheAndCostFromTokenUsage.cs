using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiTeam.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveCacheAndCostFromTokenUsage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CacheCreationTokens",
                table: "mcp_token_usage");

            migrationBuilder.DropColumn(
                name: "CacheReadTokens",
                table: "mcp_token_usage");

            migrationBuilder.DropColumn(
                name: "EstimatedCostUsd",
                table: "mcp_token_usage");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CacheCreationTokens",
                table: "mcp_token_usage",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CacheReadTokens",
                table: "mcp_token_usage",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "EstimatedCostUsd",
                table: "mcp_token_usage",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);
        }
    }
}
