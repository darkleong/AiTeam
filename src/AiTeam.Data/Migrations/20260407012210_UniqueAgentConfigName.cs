using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiTeam.Data.Migrations
{
    /// <inheritdoc />
    public partial class UniqueAgentConfigName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 刪除重複的 AgentConfig（保留每個 name 裡 id 最小的那筆）
            // 注意：PostgreSQL 資料表名稱為 snake_case（agent_configs），欄位同理
            migrationBuilder.Sql("""
                DELETE FROM agent_configs
                WHERE id NOT IN (
                    SELECT MIN(id)
                    FROM agent_configs
                    GROUP BY name
                );
                """);

            // 加 unique index，防止未來競態條件重複 seed
            migrationBuilder.CreateIndex(
                name: "IX_AgentConfigs_Name",
                table: "agent_configs",
                column: "name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AgentConfigs_Name",
                table: "agent_configs");
        }
    }
}
