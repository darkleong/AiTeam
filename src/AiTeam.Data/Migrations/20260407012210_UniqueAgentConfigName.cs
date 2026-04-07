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
            // 刪除重複的 AgentConfig（保留每個 Name 裡最早建立（CreatedAt 最小）的那筆）
            // 注意：Id 為 UUID，PostgreSQL 不支援 MIN(uuid)，改用 ROW_NUMBER()
            migrationBuilder.Sql("""
                DELETE FROM agent_configs
                WHERE "Id" IN (
                    SELECT "Id"
                    FROM (
                        SELECT "Id",
                               ROW_NUMBER() OVER (PARTITION BY "Name" ORDER BY "CreatedAt") AS rn
                        FROM agent_configs
                    ) sub
                    WHERE rn > 1
                );
                """);

            // 加 unique index，防止未來競態條件重複 seed
            migrationBuilder.CreateIndex(
                name: "IX_AgentConfigs_Name",
                table: "agent_configs",
                column: "Name",
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
