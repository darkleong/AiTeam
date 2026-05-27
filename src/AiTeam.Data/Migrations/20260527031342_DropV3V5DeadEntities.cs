using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiTeam.Data.Migrations
{
    /// <inheritdoc />
    public partial class DropV3V5DeadEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "petra_inbox");

            migrationBuilder.DropTable(
                name: "petra_session_messages");

            migrationBuilder.DropTable(
                name: "skill_prompts");

            migrationBuilder.DropTable(
                name: "talent_memories");

            migrationBuilder.DropTable(
                name: "talent_prompts");

            migrationBuilder.DropTable(
                name: "talent_skills");

            migrationBuilder.DropTable(
                name: "task_memories");

            migrationBuilder.DropTable(
                name: "talents");

            migrationBuilder.DropTable(
                name: "petra_sessions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "petra_inbox",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    Attachments = table.Column<string>(type: "jsonb", nullable: true),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeadAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EnqueuedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    MaxAttempts = table.Column<int>(type: "integer", nullable: false),
                    NextRetryAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PetraSessionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Source = table.Column<string>(type: "text", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    UserInput = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_petra_inbox", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "petra_sessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    TaskGroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReplanIteration = table.Column<int>(type: "integer", nullable: false),
                    ResultPrUrl = table.Column<string>(type: "text", nullable: true),
                    SessionCostUsd = table.Column<decimal>(type: "numeric", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_petra_sessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_petra_sessions_task_groups_TaskGroupId",
                        column: x => x.TaskGroupId,
                        principalTable: "task_groups",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "skill_prompts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUser = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    PromptBody = table.Column<string>(type: "text", nullable: false),
                    SkillName = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_skill_prompts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "talents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DailyTokenLimitK = table.Column<int>(type: "integer", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: false),
                    DisplayName = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Model = table.Column<string>(type: "text", nullable: true),
                    MonthlyTokenLimitK = table.Column<int>(type: "integer", nullable: true),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Provider = table.Column<string>(type: "text", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_talents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_talents_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "petra_session_messages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Role = table.Column<string>(type: "text", nullable: false),
                    ToolCallId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_petra_session_messages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_petra_session_messages_petra_sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "petra_sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "task_memories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    Content = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByTalent = table.Column<string>(type: "text", nullable: false),
                    Key = table.Column<string>(type: "text", nullable: false),
                    PetraSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_task_memories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_task_memories_petra_sessions_PetraSessionId",
                        column: x => x.PetraSessionId,
                        principalTable: "petra_sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "talent_memories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    TalentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Key = table.Column<string>(type: "text", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    Tags = table.Column<List<string>>(type: "text[]", nullable: false),
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
                name: "talent_prompts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    TalentId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    PersonaBody = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false)
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

            migrationBuilder.CreateTable(
                name: "talent_skills",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    TalentId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    Model = table.Column<string>(type: "text", nullable: true),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    Provider = table.Column<string>(type: "text", nullable: true),
                    SkillName = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_talent_skills", x => x.Id);
                    table.ForeignKey(
                        name: "FK_talent_skills_talents_TalentId",
                        column: x => x.TalentId,
                        principalTable: "talents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_petra_inbox_status_enqueued",
                table: "petra_inbox",
                columns: new[] { "Status", "EnqueuedAt" });

            migrationBuilder.CreateIndex(
                name: "ix_petra_inbox_status_next_retry",
                table: "petra_inbox",
                columns: new[] { "Status", "NextRetryAt" });

            migrationBuilder.CreateIndex(
                name: "IX_petra_session_messages_SessionId_CreatedAt",
                table: "petra_session_messages",
                columns: new[] { "SessionId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_petra_sessions_Status_CreatedAt",
                table: "petra_sessions",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_petra_sessions_TaskGroupId",
                table: "petra_sessions",
                column: "TaskGroupId");

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
                name: "ix_talent_prompts_active_per_talent",
                table: "talent_prompts",
                column: "TalentId",
                unique: true,
                filter: "\"IsActive\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_talent_prompts_TalentId_VersionNumber",
                table: "talent_prompts",
                columns: new[] { "TalentId", "VersionNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_talent_skills_TalentId_SkillName",
                table: "talent_skills",
                columns: new[] { "TalentId", "SkillName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_talents_name_global",
                table: "talents",
                column: "Name",
                unique: true,
                filter: "\"ProjectId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_talents_project_name_per_project",
                table: "talents",
                columns: new[] { "ProjectId", "Name" },
                unique: true,
                filter: "\"ProjectId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_task_memories_PetraSessionId_CreatedAt",
                table: "task_memories",
                columns: new[] { "PetraSessionId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "ix_task_memories_session_key",
                table: "task_memories",
                columns: new[] { "PetraSessionId", "Key" },
                unique: true);
        }
    }
}
