using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArrowThing.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddPendingEmailChange : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PendingEmail",
                table: "Users",
                type: "TEXT",
                nullable: true
            );

            migrationBuilder.AddColumn<DateTime>(
                name: "PendingEmailTokenExpiresAt",
                table: "Users",
                type: "TEXT",
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "PendingEmailTokenHash",
                table: "Users",
                type: "TEXT",
                nullable: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "PendingEmail", table: "Users");

            migrationBuilder.DropColumn(name: "PendingEmailTokenExpiresAt", table: "Users");

            migrationBuilder.DropColumn(name: "PendingEmailTokenHash", table: "Users");
        }
    }
}
