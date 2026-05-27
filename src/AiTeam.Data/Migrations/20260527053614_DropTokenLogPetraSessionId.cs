using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiTeam.Data.Migrations
{
    /// <inheritdoc />
    public partial class DropTokenLogPetraSessionId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_token_logs_PetraSessionId",
                table: "token_logs");

            migrationBuilder.DropColumn(
                name: "PetraSessionId",
                table: "token_logs");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PetraSessionId",
                table: "token_logs",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_token_logs_PetraSessionId",
                table: "token_logs",
                column: "PetraSessionId");
        }
    }
}
