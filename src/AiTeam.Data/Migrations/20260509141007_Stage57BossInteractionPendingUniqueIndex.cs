using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiTeam.Data.Migrations
{
    /// <inheritdoc />
    public partial class Stage57BossInteractionPendingUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_boss_interactions_TaskGroupId",
                table: "boss_interactions");

            migrationBuilder.CreateIndex(
                name: "ix_boss_interactions_pending_per_group_type",
                table: "boss_interactions",
                columns: new[] { "TaskGroupId", "InteractionType" },
                unique: true,
                filter: "\"Status\" = 'pending'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_boss_interactions_pending_per_group_type",
                table: "boss_interactions");

            migrationBuilder.CreateIndex(
                name: "IX_boss_interactions_TaskGroupId",
                table: "boss_interactions",
                column: "TaskGroupId");
        }
    }
}
