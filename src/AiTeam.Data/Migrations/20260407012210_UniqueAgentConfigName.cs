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
            // 刪除重複的 AgentConfig（保留每個 Name 裡 Id 最小的那筆）
            migrationBuilder.Sql("""
                DELETE FROM "AgentConfigs"
                WHERE "Id" NOT IN (
                    SELECT MIN("Id")
                    FROM "AgentConfigs"
                    GROUP BY "Name"
                );
                """);

            // 加 unique index，防止未來競態條件重複 seed
            migrationBuilder.CreateIndex(
                name: "IX_AgentConfigs_Name",
                table: "AgentConfigs",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AgentConfigs_Name",
                table: "AgentConfigs");
        }
    }
}
