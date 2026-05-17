using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiTeam.Data.Migrations
{
    /// <inheritdoc />
    public partial class Stage76RetrySchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AttemptCount",
                table: "petra_inbox",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeadAt",
                table: "petra_inbox",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxAttempts",
                table: "petra_inbox",
                type: "integer",
                nullable: false,
                defaultValue: 3);   // Stage 76：既有 row 預設給 3（對齊 entity C# initializer / 避免 retry path 上限=0 永遠 fail-fast）

            migrationBuilder.AddColumn<DateTime>(
                name: "NextRetryAt",
                table: "petra_inbox",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_petra_inbox_status_next_retry",
                table: "petra_inbox",
                columns: new[] { "Status", "NextRetryAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_petra_inbox_status_next_retry",
                table: "petra_inbox");

            migrationBuilder.DropColumn(
                name: "AttemptCount",
                table: "petra_inbox");

            migrationBuilder.DropColumn(
                name: "DeadAt",
                table: "petra_inbox");

            migrationBuilder.DropColumn(
                name: "MaxAttempts",
                table: "petra_inbox");

            migrationBuilder.DropColumn(
                name: "NextRetryAt",
                table: "petra_inbox");
        }
    }
}
