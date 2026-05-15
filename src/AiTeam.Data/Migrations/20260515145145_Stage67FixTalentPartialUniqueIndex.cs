using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiTeam.Data.Migrations
{
    /// <inheritdoc />
    public partial class Stage67FixTalentPartialUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_talents_ProjectId_Name",
                table: "talents");

            migrationBuilder.CreateIndex(
                name: "ix_talents_name_global",
                table: "talents",
                column: "Name",
                unique: true,
                filter: "\"ProjectId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_talents_project_name_per_project",
                table: "talents",
                columns: new[] { "ProjectId", "Name" },
                unique: true,
                filter: "\"ProjectId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_talents_name_global",
                table: "talents");

            migrationBuilder.DropIndex(
                name: "ix_talents_project_name_per_project",
                table: "talents");

            migrationBuilder.CreateIndex(
                name: "IX_talents_ProjectId_Name",
                table: "talents",
                columns: new[] { "ProjectId", "Name" },
                unique: true);
        }
    }
}
