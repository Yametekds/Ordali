using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ordali.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class DeliveryNotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "shipping");

            migrationBuilder.CreateTable(
                name: "DeliveryNotes",
                schema: "shipping",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    ScheduledEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Customer = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Address = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Carrier = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    VehiclePlate = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Reference = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeliveryNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeliveryNotes_ScheduledEvents_ScheduledEventId",
                        column: x => x.ScheduledEventId,
                        principalSchema: "planning",
                        principalTable: "ScheduledEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DeliveryNoteLines",
                schema: "shipping",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeliveryNoteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Lot = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Pallet = table.Column<int>(type: "int", nullable: false),
                    Position = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeliveryNoteLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeliveryNoteLines_DeliveryNotes_DeliveryNoteId",
                        column: x => x.DeliveryNoteId,
                        principalSchema: "shipping",
                        principalTable: "DeliveryNotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DeliveryNoteLines_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "catalog",
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryNoteLines_DeliveryNoteId",
                schema: "shipping",
                table: "DeliveryNoteLines",
                column: "DeliveryNoteId");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryNoteLines_ProductId",
                schema: "shipping",
                table: "DeliveryNoteLines",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryNotes_Code",
                schema: "shipping",
                table: "DeliveryNotes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryNotes_CreatedAt",
                schema: "shipping",
                table: "DeliveryNotes",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryNotes_ScheduledEventId",
                schema: "shipping",
                table: "DeliveryNotes",
                column: "ScheduledEventId",
                unique: true,
                filter: "[ScheduledEventId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeliveryNoteLines",
                schema: "shipping");

            migrationBuilder.DropTable(
                name: "DeliveryNotes",
                schema: "shipping");
        }
    }
}
