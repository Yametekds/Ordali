using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ordali.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class ReceiptsAndPutAway : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Locations_SiteId_Code",
                schema: "locations",
                table: "Locations");

            migrationBuilder.AddColumn<string>(
                name: "Reference",
                schema: "inventory",
                table: "StockMovements",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DefaultLocationId",
                schema: "catalog",
                table: "Products",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Receipts",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    DockLocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Receipts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Receipts_Locations_DockLocationId",
                        column: x => x.DockLocationId,
                        principalSchema: "locations",
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReceiptLines",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReceiptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Lot = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    PutAwayQuantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Version = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReceiptLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReceiptLines_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "catalog",
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReceiptLines_Receipts_ReceiptId",
                        column: x => x.ReceiptId,
                        principalSchema: "inventory",
                        principalTable: "Receipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Products_DefaultLocationId",
                schema: "catalog",
                table: "Products",
                column: "DefaultLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_Locations_SiteId_Code",
                schema: "locations",
                table: "Locations",
                columns: new[] { "SiteId", "Code" });

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptLines_ProductId",
                schema: "inventory",
                table: "ReceiptLines",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceiptLines_ReceiptId",
                schema: "inventory",
                table: "ReceiptLines",
                column: "ReceiptId");

            migrationBuilder.CreateIndex(
                name: "IX_Receipts_Code",
                schema: "inventory",
                table: "Receipts",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Receipts_CompletedAt",
                schema: "inventory",
                table: "Receipts",
                column: "CompletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Receipts_DockLocationId",
                schema: "inventory",
                table: "Receipts",
                column: "DockLocationId");

            migrationBuilder.AddForeignKey(
                name: "FK_Products_Locations_DefaultLocationId",
                schema: "catalog",
                table: "Products",
                column: "DefaultLocationId",
                principalSchema: "locations",
                principalTable: "Locations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Products_Locations_DefaultLocationId",
                schema: "catalog",
                table: "Products");

            migrationBuilder.DropTable(
                name: "ReceiptLines",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "Receipts",
                schema: "inventory");

            migrationBuilder.DropIndex(
                name: "IX_Products_DefaultLocationId",
                schema: "catalog",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Locations_SiteId_Code",
                schema: "locations",
                table: "Locations");

            migrationBuilder.DropColumn(
                name: "Reference",
                schema: "inventory",
                table: "StockMovements");

            migrationBuilder.DropColumn(
                name: "DefaultLocationId",
                schema: "catalog",
                table: "Products");

            migrationBuilder.CreateIndex(
                name: "IX_Locations_SiteId_Code",
                schema: "locations",
                table: "Locations",
                columns: new[] { "SiteId", "Code" },
                unique: true);
        }
    }
}
