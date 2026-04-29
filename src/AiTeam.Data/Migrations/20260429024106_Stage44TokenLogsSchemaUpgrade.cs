using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiTeam.Data.Migrations
{
    /// <inheritdoc />
    public partial class Stage44TokenLogsSchemaUpgrade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CacheCreationTokens",
                table: "token_logs",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CacheReadTokens",
                table: "token_logs",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Round",
                table: "token_logs",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Stage",
                table: "token_logs",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalCostUsd",
                table: "token_logs",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CacheCreationTokens",
                table: "token_logs");

            migrationBuilder.DropColumn(
                name: "CacheReadTokens",
                table: "token_logs");

            migrationBuilder.DropColumn(
                name: "Round",
                table: "token_logs");

            migrationBuilder.DropColumn(
                name: "Stage",
                table: "token_logs");

            migrationBuilder.DropColumn(
                name: "TotalCostUsd",
                table: "token_logs");
        }
    }
}
