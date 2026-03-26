using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArrowThing.Server.Migrations
{
    /// <inheritdoc />
    public partial class ReplacePasswordResetTokenWithCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "PasswordResetTokenHash",
                table: "Users",
                newName: "PasswordResetCodeExpiresAt"
            );

            migrationBuilder.RenameColumn(
                name: "PasswordResetTokenExpiresAt",
                table: "Users",
                newName: "PasswordResetCode"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "PasswordResetCodeExpiresAt",
                table: "Users",
                newName: "PasswordResetTokenHash"
            );

            migrationBuilder.RenameColumn(
                name: "PasswordResetCode",
                table: "Users",
                newName: "PasswordResetTokenExpiresAt"
            );
        }
    }
}
