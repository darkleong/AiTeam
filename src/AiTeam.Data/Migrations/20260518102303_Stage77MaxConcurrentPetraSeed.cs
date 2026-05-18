using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiTeam.Data.Migrations
{
    /// <inheritdoc />
    public partial class Stage77MaxConcurrentPetraSeed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Stage 77：v5.5 Phase 3 補強 — MaxConcurrentPetra seed default 3（業界 Anthropic Tier 1-2 個人帳號保守紀律 / 配 Stage 76 retry path 兜底 transient 429/5xx）
            // idempotency 紀律明示（Aria 議題 4）：`Workflow:MaxConcurrentPetra` 是 Stage 77 new key in production（pre-check：0 existing row）/ Migration InsertData 0 conflict risk
            migrationBuilder.InsertData(
                table: "app_settings",
                columns: new[] { "Key", "Value", "Description", "UpdatedAt" },
                values: new object[]
                {
                    "Workflow:MaxConcurrentPetra",
                    "3",
                    "Stage 77：PetraDispatchWorker multi-consumer 並行上限（範圍 [1, 10] / 啟動時讀一次 / SQL UPDATE 需 Bot 重啟生效）",
                    DateTime.UtcNow,
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "app_settings",
                keyColumn: "Key",
                keyValue: "Workflow:MaxConcurrentPetra");
        }
    }
}
