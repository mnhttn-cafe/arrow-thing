using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArrowThing.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailAndTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "Users",
                type: "TEXT",
                maxLength: 254,
                nullable: false,
                defaultValue: ""
            );

            migrationBuilder.AddColumn<DateTime>(
                name: "EmailVerifiedAt",
                table: "Users",
                type: "TEXT",
                nullable: true
            );

            migrationBuilder.AddColumn<DateTime>(
                name: "LastPasswordResetEmailAt",
                table: "Users",
                type: "TEXT",
                nullable: true
            );

            migrationBuilder.AddColumn<DateTime>(
                name: "LastVerificationEmailAt",
                table: "Users",
                type: "TEXT",
                nullable: true
            );

            migrationBuilder.AddColumn<DateTime>(
                name: "PasswordResetTokenExpiresAt",
                table: "Users",
                type: "TEXT",
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "PasswordResetTokenHash",
                table: "Users",
                type: "TEXT",
                nullable: true
            );

            migrationBuilder.AddColumn<DateTime>(
                name: "VerificationTokenExpiresAt",
                table: "Users",
                type: "TEXT",
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "VerificationTokenHash",
                table: "Users",
                type: "TEXT",
                nullable: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_Users_Email", table: "Users");

            migrationBuilder.DropColumn(name: "Email", table: "Users");

            migrationBuilder.DropColumn(name: "EmailVerifiedAt", table: "Users");

            migrationBuilder.DropColumn(name: "LastPasswordResetEmailAt", table: "Users");

            migrationBuilder.DropColumn(name: "LastVerificationEmailAt", table: "Users");

            migrationBuilder.DropColumn(name: "PasswordResetTokenExpiresAt", table: "Users");

            migrationBuilder.DropColumn(name: "PasswordResetTokenHash", table: "Users");

            migrationBuilder.DropColumn(name: "VerificationTokenExpiresAt", table: "Users");

            migrationBuilder.DropColumn(name: "VerificationTokenHash", table: "Users");
        }
    }
}
