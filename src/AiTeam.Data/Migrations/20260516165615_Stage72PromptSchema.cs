using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiTeam.Data.Migrations
{
    /// <inheritdoc />
    public partial class Stage72PromptSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "skill_prompts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    SkillName = table.Column<string>(type: "text", nullable: false),
                    PromptBody = table.Column<string>(type: "text", nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedByUser = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_skill_prompts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "talent_prompts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    TalentId = table.Column<Guid>(type: "uuid", nullable: false),
                    PersonaBody = table.Column<string>(type: "text", nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_talent_prompts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_talent_prompts_talents_TalentId",
                        column: x => x.TalentId,
                        principalTable: "talents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_skill_prompts_active_per_skill",
                table: "skill_prompts",
                column: "SkillName",
                unique: true,
                filter: "\"IsActive\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_skill_prompts_SkillName_VersionNumber",
                table: "skill_prompts",
                columns: new[] { "SkillName", "VersionNumber" });

            migrationBuilder.CreateIndex(
                name: "ix_talent_prompts_active_per_talent",
                table: "talent_prompts",
                column: "TalentId",
                unique: true,
                filter: "\"IsActive\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_talent_prompts_TalentId_VersionNumber",
                table: "talent_prompts",
                columns: new[] { "TalentId", "VersionNumber" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "skill_prompts");

            migrationBuilder.DropTable(
                name: "talent_prompts");
        }
    }
}
