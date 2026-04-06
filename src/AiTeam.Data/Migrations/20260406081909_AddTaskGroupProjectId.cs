using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiTeam.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskGroupProjectId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ProjectId",
                table: "task_groups",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_task_groups_ProjectId",
                table: "task_groups",
                column: "ProjectId");

            migrationBuilder.AddForeignKey(
                name: "FK_task_groups_projects_ProjectId",
                table: "task_groups",
                column: "ProjectId",
                principalTable: "projects",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_task_groups_projects_ProjectId",
                table: "task_groups");

            migrationBuilder.DropIndex(
                name: "IX_task_groups_ProjectId",
                table: "task_groups");

            migrationBuilder.DropColumn(
                name: "ProjectId",
                table: "task_groups");
        }
    }
}
