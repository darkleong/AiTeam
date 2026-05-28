using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiTeam.Data.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// 純 data fix：歷史 3 筆違反 v4 命名規範的 teammate name 改回 cody-{n} 標準。
    /// 純 UPDATE / 無 schema 變更 / Bot 容器下次 recreate 自動套用。
    /// </summary>
    public partial class FixHistoricalTeammateNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"UPDATE mcp_teammates SET ""Name"" = 'cody-1' WHERE ""Name"" = 'cody-f-deadcfg-1';");
            migrationBuilder.Sql(@"UPDATE mcp_teammates SET ""Name"" = 'cody-2' WHERE ""Name"" = 'cody-cde-patch1-1';");
            migrationBuilder.Sql(@"UPDATE mcp_teammates SET ""Name"" = 'cody-3' WHERE ""Name"" = 'cody-a-patch2-1';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 反向 UPDATE 回原名（慣例補齊 / 不預期實際 rollback）
            migrationBuilder.Sql(@"UPDATE mcp_teammates SET ""Name"" = 'cody-f-deadcfg-1' WHERE ""Name"" = 'cody-1';");
            migrationBuilder.Sql(@"UPDATE mcp_teammates SET ""Name"" = 'cody-cde-patch1-1' WHERE ""Name"" = 'cody-2';");
            migrationBuilder.Sql(@"UPDATE mcp_teammates SET ""Name"" = 'cody-a-patch2-1' WHERE ""Name"" = 'cody-3';");
        }
    }
}
