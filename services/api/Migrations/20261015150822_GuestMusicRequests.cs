using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kahve.Api.Migrations
{
    /// <inheritdoc />
    public partial class GuestMusicRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SongRequests_Orders_TenantId_BranchId_OrderId",
                table: "SongRequests");

            migrationBuilder.AlterColumn<Guid>(
                name: "OrderId",
                table: "SongRequests",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "GuestKeyHash",
                table: "SongRequests",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SongRequests_TenantId_BranchId_GuestKeyHash_CreatedAt",
                table: "SongRequests",
                columns: new[] { "TenantId", "BranchId", "GuestKeyHash", "CreatedAt" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_SongRequest_Source",
                table: "SongRequests",
                sql: "(\"OrderId\" IS NOT NULL AND \"GuestKeyHash\" IS NULL) OR (\"OrderId\" IS NULL AND \"GuestKeyHash\" IS NOT NULL)");

            migrationBuilder.AddForeignKey(
                name: "FK_SongRequests_Orders_TenantId_BranchId_OrderId",
                table: "SongRequests",
                columns: new[] { "TenantId", "BranchId", "OrderId" },
                principalTable: "Orders",
                principalColumns: new[] { "TenantId", "BranchId", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SongRequests_Orders_TenantId_BranchId_OrderId",
                table: "SongRequests");

            migrationBuilder.DropIndex(
                name: "IX_SongRequests_TenantId_BranchId_GuestKeyHash_CreatedAt",
                table: "SongRequests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SongRequest_Source",
                table: "SongRequests");

            migrationBuilder.DropColumn(
                name: "GuestKeyHash",
                table: "SongRequests");

            migrationBuilder.AlterColumn<Guid>(
                name: "OrderId",
                table: "SongRequests",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_SongRequests_Orders_TenantId_BranchId_OrderId",
                table: "SongRequests",
                columns: new[] { "TenantId", "BranchId", "OrderId" },
                principalTable: "Orders",
                principalColumns: new[] { "TenantId", "BranchId", "Id" },
                onDelete: ReferentialAction.Cascade);
        }
    }
}
