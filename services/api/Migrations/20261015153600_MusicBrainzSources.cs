using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kahve.Api.Migrations
{
    /// <inheritdoc />
    public partial class MusicBrainzSources : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "MusicBrainzRecordingId",
                table: "Songs",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Songs_TenantId_MusicBrainzRecordingId",
                table: "Songs",
                columns: new[] { "TenantId", "MusicBrainzRecordingId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Songs_TenantId_MusicBrainzRecordingId",
                table: "Songs");

            migrationBuilder.DropColumn(
                name: "MusicBrainzRecordingId",
                table: "Songs");
        }
    }
}
