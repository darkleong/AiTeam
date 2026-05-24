using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiTeam.Data.Migrations
{
    /// <inheritdoc />
    public partial class Stage87TalentTokenLimitsAndAgentConfigDrop : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Stage 87 A0：talents 表加 TokenLimit 兩欄位（對齊 Stage 47 AgentConfig 既有 schema / 對齊 Stage 74 三層 fallback chain Talent 層）
            migrationBuilder.AddColumn<int>(
                name: "DailyTokenLimitK",
                table: "talents",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MonthlyTokenLimitK",
                table: "talents",
                type: "integer",
                nullable: true);

            // Stage 87 A1：Petra LLM 配置 + Token Limit 資料遷移（agent_configs.Name="PM" row → talents.Name="Petra" row）
            // 用 COALESCE 守 idempotency（已有值不覆蓋 / 多次跑同結果）— 對齊 Stage 67 既有 race-safe pattern
            migrationBuilder.Sql(@"
                UPDATE talents
                SET ""Provider""           = COALESCE(talents.""Provider"",           a.""Provider""),
                    ""Model""              = COALESCE(talents.""Model"",              a.""Model""),
                    ""DailyTokenLimitK""   = COALESCE(talents.""DailyTokenLimitK"",   a.""DailyTokenLimitK""),
                    ""MonthlyTokenLimitK"" = COALESCE(talents.""MonthlyTokenLimitK"", a.""MonthlyTokenLimitK"")
                FROM agent_configs a
                WHERE a.""Name"" = 'PM' AND talents.""Name"" = 'Petra';
            ");

            // Stage 87 C0：Rules v4 9 row DELETE（Stage 86 子項 0 A 路線僅動 UI fallback / Stage 87 純資料 cleanup）
            // 對齊 Stage 85 「0 Migration 純 SQL cleanup」紀律繼承（拼進同 Migration / 不獨立 Migration）
            migrationBuilder.Sql(@"
                DELETE FROM rules
                WHERE ""AgentName"" IN ('Dev','Ops','Qa','Doc','Requirements','Reviewer','Release','Designer','PM');
            ");

            // Stage 87 A1 後置：agent_configs 表 DROP TABLE（v4 collapse 最後殘留收口 / Petra LLM 配置 SoT 唯一性收回 talents.Name="Petra" row）
            migrationBuilder.DropTable(
                name: "agent_configs");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DailyTokenLimitK",
                table: "talents");

            migrationBuilder.DropColumn(
                name: "MonthlyTokenLimitK",
                table: "talents");

            migrationBuilder.CreateTable(
                name: "agent_configs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    TeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DailyTokenLimitK = table.Column<int>(type: "integer", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: false),
                    DiscordChannelId = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Model = table.Column<string>(type: "text", nullable: true),
                    MonthlyTokenLimitK = table.Column<int>(type: "integer", nullable: true),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Provider = table.Column<string>(type: "text", nullable: true),
                    TrustLevel = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agent_configs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_agent_configs_teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_agent_configs_Name",
                table: "agent_configs",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_agent_configs_TeamId",
                table: "agent_configs",
                column: "TeamId");
        }
    }
}
