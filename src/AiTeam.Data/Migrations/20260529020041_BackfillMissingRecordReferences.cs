using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiTeam.Data.Migrations
{
    /// <inheritdoc />
    public partial class BackfillMissingRecordReferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Step 1 — 補 mcp_tasks.TeammateId：先補 task 才能讓後續 token usage backfill 對得起來
            // 規則：同 team 內第一個 role='member' 的 teammate（pool 概念 / 沒辦法精準復原原本指派誰）
            migrationBuilder.Sql(@"
UPDATE mcp_tasks
SET ""TeammateId"" = (
    SELECT ""Id"" FROM mcp_teammates
    WHERE ""TeamId"" = mcp_tasks.""TeamId"" AND ""Role"" = 'member'
    LIMIT 1
)
WHERE ""TeammateId"" IS NULL;
");

            // Step 2 — 補 mcp_token_usage.TaskId：用 teammate 所屬 team 內最新 created 且早於 usage CreatedAt 的 task
            migrationBuilder.Sql(@"
UPDATE mcp_token_usage tu
SET ""TaskId"" = (
    SELECT tk.""Id"" FROM mcp_tasks tk
    WHERE tk.""TeamId"" = (SELECT ""TeamId"" FROM mcp_teammates WHERE ""Id"" = tu.""TeammateId"")
      AND tk.""CreatedAt"" <= tu.""CreatedAt""
    ORDER BY tk.""CreatedAt"" DESC
    LIMIT 1
)
WHERE tu.""TaskId"" IS NULL;
");

            // Step 3 — 補 mcp_teammates.FinishedAt for role='lead'：team 已 closed 但 lead 沒 finish 的、用 team.ClosedAt 補
            migrationBuilder.Sql(@"
UPDATE mcp_teammates tm
SET ""FinishedAt"" = t.""ClosedAt""
FROM mcp_teams t
WHERE tm.""TeamId"" = t.""Id""
  AND tm.""FinishedAt"" IS NULL
  AND tm.""Role"" = 'lead'
  AND t.""ClosedAt"" IS NOT NULL;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // data backfill 不可逆 / 補進去的 reference 無原始 null 紀錄 / 無回復路徑
        }
    }
}
