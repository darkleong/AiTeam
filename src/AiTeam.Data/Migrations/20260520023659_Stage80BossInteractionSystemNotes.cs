using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiTeam.Data.Migrations
{
    /// <inheritdoc />
    public partial class Stage80BossInteractionSystemNotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Stage 80 議題 4 W2：BossInteraction.SystemNotes 後端欄位（取代前端 ParseDescription string pattern 耦合）。
            // nullable 對齊 backwards-compatible（既有 row SystemNotes=NULL 不擾 / 前端純 render Description）。
            migrationBuilder.AddColumn<string>(
                name: "SystemNotes",
                table: "boss_interactions",
                type: "text",
                nullable: true);

            // Stage 80：HITL plan confirmation 閘門 feature flag seed default false（守 v5.5 baseline auto dispatch）。
            // idempotency 紀律明示（對齊 Stage 77 / 79 既有 InsertData pattern）：
            //   `Workflow:UseHITLPlanConfirmation` 是 Stage 80 new key in production / 0 existing row / 0 conflict risk。
            migrationBuilder.InsertData(
                table: "app_settings",
                columns: new[] { "Key", "Value", "Description", "UpdatedAt" },
                values: new object[]
                {
                    "Workflow:UseHITLPlanConfirmation",
                    "false",
                    "Stage 80：HITL plan confirmation 閘門 — Petra 拆完 plan 開 BossInteraction plan_confirm 卡 + Christ 4 decision pattern 拍板（approve / edit / reject / respond）。default false 守 v5.5 baseline auto dispatch / Trial_v24 開時切 true → 結案切回 false。",
                    DateTime.UtcNow,
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "app_settings",
                keyColumn: "Key",
                keyValue: "Workflow:UseHITLPlanConfirmation");

            migrationBuilder.DropColumn(
                name: "SystemNotes",
                table: "boss_interactions");
        }
    }
}
