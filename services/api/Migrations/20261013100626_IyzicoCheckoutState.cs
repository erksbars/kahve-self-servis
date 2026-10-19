using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kahve.Api.Migrations
{
    /// <inheritdoc />
    public partial class IyzicoCheckoutState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "InitializationState",
                table: "Payments",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "LastProviderError",
                table: "Payments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastVerifiedAt",
                table: "Payments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProtectedPaymentUrl",
                table: "Payments",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ProtectedProviderToken",
                table: "Payments",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ProviderResultJson",
                table: "Payments",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InitializationState",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "LastProviderError",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "LastVerifiedAt",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "ProtectedPaymentUrl",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "ProtectedProviderToken",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "ProviderResultJson",
                table: "Payments");
        }
    }
}
