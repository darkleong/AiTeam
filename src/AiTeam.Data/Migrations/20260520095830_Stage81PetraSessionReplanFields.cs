using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiTeam.Data.Migrations
{
    /// <inheritdoc />
    public partial class Stage81PetraSessionReplanFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Stage 81 子項 5：PetraSession 加 ReplanIteration + SessionCostUsd 兩 column（追蹤動態 replan 輪數 + 累計 cost）。
            // W5 紀律：default 0 / 既有 row 不擾 / backwards-compatible。
            migrationBuilder.AddColumn<int>(
                name: "ReplanIteration",
                table: "petra_sessions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "SessionCostUsd",
                table: "petra_sessions",
                type: "numeric(18,6)",
                nullable: false,
                defaultValue: 0m);

            // Stage 81 議題 #5 路線 A：TokenLog 加 PetraSessionId nullable column — UpdateSessionCostUsdAsync WHERE PetraSessionId=... 精準累計用。
            // 補強 #B 紀律：non-unique non-partial index 對齊既有 TaskId FK pattern / 高頻 SumAsync query 性能保險。
            migrationBuilder.AddColumn<Guid>(
                name: "PetraSessionId",
                table: "token_logs",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_token_logs_PetraSessionId",
                table: "token_logs",
                column: "PetraSessionId");

            // Stage 81：3 AppSetting seed default false / 3 / 5（Trial_v25 開時 SQL UPDATE 切 true → 結案切回 false 紀律）。
            // idempotency 紀律明示（對齊 Stage 77 / 79 / 80 既有 InsertData pattern）：3 新 key in production / 0 existing row / 0 conflict risk。
            migrationBuilder.InsertData(
                table: "app_settings",
                columns: new[] { "Key", "Value", "Description", "UpdatedAt" },
                values: new object[,]
                {
                    {
                        "Workflow:UseDynamicReplanning",
                        "false",
                        "Stage 81：動態 re-planning + HITL retry gate（LangGraph cycles 業界紀律）feature flag。default false 守 Stage 80 baseline / Trial_v25 開時切 true → 結案切回 false。真實生效需 UseHITLPlanConfirmation=true 為前置（補強 #A 紀律 — ContinueChainFromSubtaskAsync 取 plan_confirm ContextJson 是 single source of truth）。",
                        DateTime.UtcNow
                    },
                    {
                        "Workflow:MaxReplanIterations",
                        "3",
                        "Stage 81：max replan iterations hard cap（業界 LangGraph best practice）。default 3 / 範圍守 [1, 10]。approve / edit / respond 各 +1 / reject 不算。",
                        DateTime.UtcNow
                    },
                    {
                        "Workflow:ReplanCostCapUsd",
                        "5",
                        "Stage 81：replan session cost soft cap USD（雙重保險）。default 5 USD / 須 > 0。達上限升 intervention 卡讓 Christ 拍板介入。",
                        DateTime.UtcNow
                    }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "app_settings",
                keyColumn: "Key",
                keyValue: "Workflow:UseDynamicReplanning");

            migrationBuilder.DeleteData(
                table: "app_settings",
                keyColumn: "Key",
                keyValue: "Workflow:MaxReplanIterations");

            migrationBuilder.DeleteData(
                table: "app_settings",
                keyColumn: "Key",
                keyValue: "Workflow:ReplanCostCapUsd");

            migrationBuilder.DropIndex(
                name: "IX_token_logs_PetraSessionId",
                table: "token_logs");

            migrationBuilder.DropColumn(
                name: "PetraSessionId",
                table: "token_logs");

            migrationBuilder.DropColumn(
                name: "SessionCostUsd",
                table: "petra_sessions");

            migrationBuilder.DropColumn(
                name: "ReplanIteration",
                table: "petra_sessions");
        }
    }
}
