using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kahve.Api.Migrations
{
    /// <inheritdoc />
    public partial class MusicCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BranchMusic",
                columns: table => new
                {
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BranchMusic", x => x.BranchId);
                    table.ForeignKey(
                        name: "FK_BranchMusic_Branches_TenantId_BranchId",
                        columns: x => new { x.TenantId, x.BranchId },
                        principalTable: "Branches",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Songs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Artist = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    SearchKey = table.Column<string>(type: "character varying(330)", maxLength: 330, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Songs", x => x.Id);
                    table.UniqueConstraint("AK_Songs_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_Songs_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BranchSongs",
                columns: table => new
                {
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    SongId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BranchSongs", x => new { x.BranchId, x.SongId });
                    table.UniqueConstraint("AK_BranchSongs_TenantId_BranchId_SongId", x => new { x.TenantId, x.BranchId, x.SongId });
                    table.ForeignKey(
                        name: "FK_BranchSongs_Branches_TenantId_BranchId",
                        columns: x => new { x.TenantId, x.BranchId },
                        principalTable: "Branches",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BranchSongs_Songs_TenantId_SongId",
                        columns: x => new { x.TenantId, x.SongId },
                        principalTable: "Songs",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SongRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    SongId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SongRequests", x => x.Id);
                    table.CheckConstraint("CK_SongRequest_Status", "\"Status\" IN ('Pending', 'Accepted', 'Rejected')");
                    table.ForeignKey(
                        name: "FK_SongRequests_BranchSongs_TenantId_BranchId_SongId",
                        columns: x => new { x.TenantId, x.BranchId, x.SongId },
                        principalTable: "BranchSongs",
                        principalColumns: new[] { "TenantId", "BranchId", "SongId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SongRequests_Orders_TenantId_BranchId_OrderId",
                        columns: x => new { x.TenantId, x.BranchId, x.OrderId },
                        principalTable: "Orders",
                        principalColumns: new[] { "TenantId", "BranchId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BranchMusic_TenantId_BranchId",
                table: "BranchMusic",
                columns: new[] { "TenantId", "BranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_BranchSongs_TenantId_SongId",
                table: "BranchSongs",
                columns: new[] { "TenantId", "SongId" });

            migrationBuilder.CreateIndex(
                name: "IX_SongRequests_OrderId",
                table: "SongRequests",
                column: "OrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SongRequests_TenantId_BranchId_OrderId",
                table: "SongRequests",
                columns: new[] { "TenantId", "BranchId", "OrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_SongRequests_TenantId_BranchId_SongId",
                table: "SongRequests",
                columns: new[] { "TenantId", "BranchId", "SongId" });

            migrationBuilder.CreateIndex(
                name: "IX_Songs_TenantId_SearchKey",
                table: "Songs",
                columns: new[] { "TenantId", "SearchKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BranchMusic");

            migrationBuilder.DropTable(
                name: "SongRequests");

            migrationBuilder.DropTable(
                name: "BranchSongs");

            migrationBuilder.DropTable(
                name: "Songs");
        }
    }
}
