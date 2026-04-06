using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiTeam.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCeoConversationAndMemory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ceo_conversations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    Role = table.Column<string>(type: "text", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ceo_conversations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ceo_memories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    Category = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ceo_memories", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ceo_conversations_CreatedAt",
                table: "ceo_conversations",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ceo_conversations_UserId_SessionId",
                table: "ceo_conversations",
                columns: new[] { "UserId", "SessionId" });

            migrationBuilder.CreateIndex(
                name: "IX_ceo_memories_UserId_IsActive",
                table: "ceo_memories",
                columns: new[] { "UserId", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ceo_conversations");

            migrationBuilder.DropTable(
                name: "ceo_memories");
        }
    }
}
