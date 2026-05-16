using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiTeam.Data.Migrations
{
    /// <inheritdoc />
    public partial class Stage69PivotTaskMemoryToSession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_task_memories_task_groups_TaskGroupId",
                table: "task_memories");

            migrationBuilder.RenameColumn(
                name: "TaskGroupId",
                table: "task_memories",
                newName: "PetraSessionId");

            migrationBuilder.RenameIndex(
                name: "IX_task_memories_TaskGroupId_CreatedAt",
                table: "task_memories",
                newName: "IX_task_memories_PetraSessionId_CreatedAt");

            migrationBuilder.RenameIndex(
                name: "ix_task_memories_group_key",
                table: "task_memories",
                newName: "ix_task_memories_session_key");

            migrationBuilder.AddForeignKey(
                name: "FK_task_memories_petra_sessions_PetraSessionId",
                table: "task_memories",
                column: "PetraSessionId",
                principalTable: "petra_sessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_task_memories_petra_sessions_PetraSessionId",
                table: "task_memories");

            migrationBuilder.RenameColumn(
                name: "PetraSessionId",
                table: "task_memories",
                newName: "TaskGroupId");

            migrationBuilder.RenameIndex(
                name: "ix_task_memories_session_key",
                table: "task_memories",
                newName: "ix_task_memories_group_key");

            migrationBuilder.RenameIndex(
                name: "IX_task_memories_PetraSessionId_CreatedAt",
                table: "task_memories",
                newName: "IX_task_memories_TaskGroupId_CreatedAt");

            migrationBuilder.AddForeignKey(
                name: "FK_task_memories_task_groups_TaskGroupId",
                table: "task_memories",
                column: "TaskGroupId",
                principalTable: "task_groups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
