using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiTeam.Data.Migrations
{
    /// <inheritdoc />
    public partial class CleanupTestTeamsAndStaleNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Step 1: cascade 砍 4 個 test team 的 child + team 本身
            // 按 FK 安全順序：messages → token_usage → tasks → teammates → teams
            migrationBuilder.Sql(@"
DELETE FROM mcp_messages
WHERE ""TeammateId"" IN (
    SELECT ""Id"" FROM mcp_teammates
    WHERE ""TeamId"" IN (
        SELECT ""Id"" FROM mcp_teams
        WHERE ""Name"" IN ('hello-world-test-team', 'signalr-verify', 'v4.1.3-hello-world-smoke', 'records-table-followup')
    )
);");

            migrationBuilder.Sql(@"
DELETE FROM mcp_token_usage
WHERE ""TeammateId"" IN (
    SELECT ""Id"" FROM mcp_teammates
    WHERE ""TeamId"" IN (
        SELECT ""Id"" FROM mcp_teams
        WHERE ""Name"" IN ('hello-world-test-team', 'signalr-verify', 'v4.1.3-hello-world-smoke', 'records-table-followup')
    )
);");

            migrationBuilder.Sql(@"
DELETE FROM mcp_tasks
WHERE ""TeamId"" IN (
    SELECT ""Id"" FROM mcp_teams
    WHERE ""Name"" IN ('hello-world-test-team', 'signalr-verify', 'v4.1.3-hello-world-smoke', 'records-table-followup')
);");

            migrationBuilder.Sql(@"
DELETE FROM mcp_teammates
WHERE ""TeamId"" IN (
    SELECT ""Id"" FROM mcp_teams
    WHERE ""Name"" IN ('hello-world-test-team', 'signalr-verify', 'v4.1.3-hello-world-smoke', 'records-table-followup')
);");

            migrationBuilder.Sql(@"
DELETE FROM mcp_teams
WHERE ""Name"" IN ('hello-world-test-team', 'signalr-verify', 'v4.1.3-hello-world-smoke', 'records-table-followup');");

            // Step 2: UPDATE 剩餘 petra-pm → petra
            migrationBuilder.Sql(@"
UPDATE mcp_teammates SET ""Name"" = 'petra' WHERE ""Name"" = 'petra-pm';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // DELETE 不可逆 / 純歷史 cleanup migration / 無回復路徑
        }
    }
}
