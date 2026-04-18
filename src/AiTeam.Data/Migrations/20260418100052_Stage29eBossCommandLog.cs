using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiTeam.Data.Migrations
{
    /// <inheritdoc />
    public partial class Stage29eBossCommandLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "boss_command_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    Text = table.Column<string>(type: "text", nullable: false),
                    Images = table.Column<string>(type: "jsonb", nullable: true),
                    Source = table.Column<string>(type: "text", nullable: false),
                    CeoResponseRaw = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_boss_command_logs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_boss_command_logs_CreatedAt",
                table: "boss_command_logs",
                column: "CreatedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "boss_command_logs");
        }
    }
}
