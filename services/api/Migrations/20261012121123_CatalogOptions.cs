using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kahve.Api.Migrations
{
    /// <inheritdoc />
    public partial class CatalogOptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CategoryId",
                table: "Products",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "Categories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.Id);
                    table.UniqueConstraint("AK_Categories_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.ForeignKey(
                        name: "FK_Categories_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OptionGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    MinSelect = table.Column<int>(type: "integer", nullable: false),
                    MaxSelect = table.Column<int>(type: "integer", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OptionGroups", x => x.Id);
                    table.UniqueConstraint("AK_OptionGroups_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_OptionGroup_Limits", "\"MinSelect\" >= 0 AND \"MaxSelect\" >= \"MinSelect\" AND \"MaxSelect\" <= 10");
                    table.ForeignKey(
                        name: "FK_OptionGroups_Products_TenantId_ProductId",
                        columns: x => new { x.TenantId, x.ProductId },
                        principalTable: "Products",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OptionValues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OptionGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    PriceDelta = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    Available = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OptionValues", x => x.Id);
                    table.UniqueConstraint("AK_OptionValues_TenantId_Id", x => new { x.TenantId, x.Id });
                    table.CheckConstraint("CK_OptionValue_Price", "\"PriceDelta\" >= 0");
                    table.ForeignKey(
                        name: "FK_OptionValues_OptionGroups_TenantId_OptionGroupId",
                        columns: x => new { x.TenantId, x.OptionGroupId },
                        principalTable: "OptionGroups",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BranchOptions",
                columns: table => new
                {
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    OptionValueId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    PriceOverride = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    Available = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BranchOptions", x => new { x.BranchId, x.OptionValueId });
                    table.CheckConstraint("CK_BranchOption_Price", "\"PriceOverride\" IS NULL OR \"PriceOverride\" >= 0");
                    table.ForeignKey(
                        name: "FK_BranchOptions_Branches_TenantId_BranchId",
                        columns: x => new { x.TenantId, x.BranchId },
                        principalTable: "Branches",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BranchOptions_OptionValues_TenantId_OptionValueId",
                        columns: x => new { x.TenantId, x.OptionValueId },
                        principalTable: "OptionValues",
                        principalColumns: new[] { "TenantId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Products_TenantId_CategoryId",
                table: "Products",
                columns: new[] { "TenantId", "CategoryId" });

            migrationBuilder.CreateIndex(
                name: "IX_BranchOptions_TenantId_BranchId",
                table: "BranchOptions",
                columns: new[] { "TenantId", "BranchId" });

            migrationBuilder.CreateIndex(
                name: "IX_BranchOptions_TenantId_OptionValueId",
                table: "BranchOptions",
                columns: new[] { "TenantId", "OptionValueId" });

            migrationBuilder.CreateIndex(
                name: "IX_OptionGroups_TenantId_ProductId",
                table: "OptionGroups",
                columns: new[] { "TenantId", "ProductId" });

            migrationBuilder.CreateIndex(
                name: "IX_OptionValues_TenantId_OptionGroupId",
                table: "OptionValues",
                columns: new[] { "TenantId", "OptionGroupId" });

            // Preserve the existing catalog while converting category text to tenant-scoped rows.
            migrationBuilder.Sql("""
                INSERT INTO "Categories" ("Id", "TenantId", "Name", "SortOrder")
                SELECT gen_random_uuid(), "TenantId", "Category", MIN("SortOrder")
                FROM "Products" GROUP BY "TenantId", "Category";
                UPDATE "Products" p SET "CategoryId" = c."Id"
                FROM "Categories" c WHERE p."TenantId" = c."TenantId" AND p."Category" = c."Name";
                """);
            migrationBuilder.DropColumn(name: "Category", table: "Products");
            migrationBuilder.AddForeignKey(
                name: "FK_Products_Categories_TenantId_CategoryId",
                table: "Products",
                columns: new[] { "TenantId", "CategoryId" },
                principalTable: "Categories",
                principalColumns: new[] { "TenantId", "Id" },
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(name: "Category", table: "Products", type: "text", nullable: false, defaultValue: "");
            migrationBuilder.Sql("""
                UPDATE "Products" p SET "Category" = c."Name"
                FROM "Categories" c WHERE p."TenantId" = c."TenantId" AND p."CategoryId" = c."Id";
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_Products_Categories_TenantId_CategoryId",
                table: "Products");

            migrationBuilder.DropTable(
                name: "BranchOptions");

            migrationBuilder.DropTable(
                name: "Categories");

            migrationBuilder.DropTable(
                name: "OptionValues");

            migrationBuilder.DropTable(
                name: "OptionGroups");

            migrationBuilder.DropIndex(
                name: "IX_Products_TenantId_CategoryId",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "CategoryId",
                table: "Products");

        }
    }
}
