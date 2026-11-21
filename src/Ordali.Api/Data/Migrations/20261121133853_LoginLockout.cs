using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ordali.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class LoginLockout : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FailedLoginCount",
                schema: "identity",
                table: "Users",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LockoutEnd",
                schema: "identity",
                table: "Users",
                type: "datetimeoffset",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FailedLoginCount",
                schema: "identity",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LockoutEnd",
                schema: "identity",
                table: "Users");
        }
    }
}
