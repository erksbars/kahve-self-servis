using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kahve.Api.Migrations
{
    /// <inheritdoc />
    public partial class FreeTextMusicRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "SongId",
                table: "SongRequests",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "RequestedText",
                table: "SongRequests",
                type: "character varying(240)",
                maxLength: 240,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_SongRequest_Selection",
                table: "SongRequests",
                sql: "(\"SongId\" IS NOT NULL AND \"RequestedText\" IS NULL) OR (\"SongId\" IS NULL AND \"RequestedText\" IS NOT NULL AND char_length(btrim(\"RequestedText\")) BETWEEN 1 AND 240)");

            migrationBuilder.AddForeignKey(
                name: "FK_SongRequests_Branches_TenantId_BranchId",
                table: "SongRequests",
                columns: new[] { "TenantId", "BranchId" },
                principalTable: "Branches",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Preserve suggestions: rolling back requires resolving free-text rows first.
            migrationBuilder.Sql("DO $$ BEGIN IF EXISTS (SELECT 1 FROM \"SongRequests\" WHERE \"SongId\" IS NULL) THEN RAISE EXCEPTION 'Resolve free-text song requests before rollback'; END IF; END $$;");
            migrationBuilder.DropForeignKey(
                name: "FK_SongRequests_Branches_TenantId_BranchId",
                table: "SongRequests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SongRequest_Selection",
                table: "SongRequests");

            migrationBuilder.DropColumn(
                name: "RequestedText",
                table: "SongRequests");

            migrationBuilder.AlterColumn<Guid>(
                name: "SongId",
                table: "SongRequests",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
