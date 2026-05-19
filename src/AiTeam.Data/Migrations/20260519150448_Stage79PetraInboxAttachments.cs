using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiTeam.Data.Migrations
{
    /// <inheritdoc />
    public partial class Stage79PetraInboxAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Stage 79：v5.5 image flow 補完 — 半抽象 future-friendly schema（Attachments jsonb + Type discriminator）
            // JSON 結構：[{ "type": "image", "base64Data": "...", "mediaType": "image/png" }, ...]
            // Stage 79 baseline 只實作 Type="image" / 未來擴展 PDF / document 0 schema migration。
            // nullable 對齊 backwards-compatible（既有 row Attachments=NULL 不擾）。
            migrationBuilder.AddColumn<string>(
                name: "Attachments",
                table: "petra_inbox",
                type: "jsonb",
                nullable: true);

            // Stage 79：限制紀律 AppSetting seed（對齊 Stage 77 既有 InsertData pattern）
            // idempotency 紀律明示：兩個 key 是 Stage 79 new key in production / 0 existing row / 0 conflict risk
            migrationBuilder.InsertData(
                table: "app_settings",
                columns: new[] { "Key", "Value", "Description", "UpdatedAt" },
                values: new object[,]
                {
                    { "Workflow:MaxAttachmentsPerTask", "5", "Stage 79：per task max attachment count（對齊 Claude Code CLI + Claude API 真實上限）", DateTime.UtcNow },
                    { "Workflow:MaxAttachmentSizeMB",  "5", "Stage 79：per attachment max size MB（對齊 Claude Code CLI + Claude API 5 MB per image 真實上限）", DateTime.UtcNow },
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "app_settings",
                keyColumn: "Key",
                keyValues: new object[] { "Workflow:MaxAttachmentsPerTask", "Workflow:MaxAttachmentSizeMB" });

            migrationBuilder.DropColumn(
                name: "Attachments",
                table: "petra_inbox");
        }
    }
}
