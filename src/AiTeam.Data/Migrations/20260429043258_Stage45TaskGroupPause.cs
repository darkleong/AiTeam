using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiTeam.Data.Migrations
{
    /// <inheritdoc />
    public partial class Stage45TaskGroupPause : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPaused",
                table: "task_groups",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "PausedAt",
                table: "task_groups",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PausedBy",
                table: "task_groups",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PendingStepsJson",
                table: "task_groups",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsPaused",
                table: "task_groups");

            migrationBuilder.DropColumn(
                name: "PausedAt",
                table: "task_groups");

            migrationBuilder.DropColumn(
                name: "PausedBy",
                table: "task_groups");

            migrationBuilder.DropColumn(
                name: "PendingStepsJson",
                table: "task_groups");
        }
    }
}
