using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiTeam.Data.Migrations
{
    /// <inheritdoc />
    public partial class Stage69MemorySchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "talent_memories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    TalentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    Key = table.Column<string>(type: "text", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    Tags = table.Column<List<string>>(type: "text[]", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_talent_memories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_talent_memories_talents_TalentId",
                        column: x => x.TalentId,
                        principalTable: "talents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "task_memories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    TaskGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    Key = table.Column<string>(type: "text", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    CreatedByTalent = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_task_memories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_task_memories_task_groups_TaskGroupId",
                        column: x => x.TaskGroupId,
                        principalTable: "task_groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_talent_memories_talent_key_global",
                table: "talent_memories",
                columns: new[] { "TalentId", "Key" },
                unique: true,
                filter: "\"ProjectId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_talent_memories_talent_key_project",
                table: "talent_memories",
                columns: new[] { "TalentId", "Key", "ProjectId" },
                unique: true,
                filter: "\"ProjectId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_talent_memories_TalentId_CreatedAt",
                table: "talent_memories",
                columns: new[] { "TalentId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "ix_task_memories_group_key",
                table: "task_memories",
                columns: new[] { "TaskGroupId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_task_memories_TaskGroupId_CreatedAt",
                table: "task_memories",
                columns: new[] { "TaskGroupId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "talent_memories");

            migrationBuilder.DropTable(
                name: "task_memories");
        }
    }
}
