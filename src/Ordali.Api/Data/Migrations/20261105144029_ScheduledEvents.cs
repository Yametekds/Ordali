using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ordali.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class ScheduledEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "planning");

            migrationBuilder.CreateTable(
                name: "ScheduledEvents",
                schema: "planning",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Partner = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    StartsAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EndsAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DockLocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    SeriesId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedByName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedByName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Outcome = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduledEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScheduledEvents_Locations_DockLocationId",
                        column: x => x.DockLocationId,
                        principalSchema: "locations",
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ScheduledEventLines",
                schema: "planning",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduledEventLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScheduledEventLines_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "catalog",
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScheduledEventLines_ScheduledEvents_EventId",
                        column: x => x.EventId,
                        principalSchema: "planning",
                        principalTable: "ScheduledEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledEventLines_EventId",
                schema: "planning",
                table: "ScheduledEventLines",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledEventLines_ProductId",
                schema: "planning",
                table: "ScheduledEventLines",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledEvents_DockLocationId",
                schema: "planning",
                table: "ScheduledEvents",
                column: "DockLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledEvents_SeriesId",
                schema: "planning",
                table: "ScheduledEvents",
                column: "SeriesId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledEvents_StartsAt_EndsAt",
                schema: "planning",
                table: "ScheduledEvents",
                columns: new[] { "StartsAt", "EndsAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ScheduledEventLines",
                schema: "planning");

            migrationBuilder.DropTable(
                name: "ScheduledEvents",
                schema: "planning");
        }
    }
}
