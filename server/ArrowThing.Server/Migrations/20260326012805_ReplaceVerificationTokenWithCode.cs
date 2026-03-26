using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArrowThing.Server.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceVerificationTokenWithCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "VerificationTokenHash",
                table: "Users",
                newName: "VerificationCodeExpiresAt"
            );

            migrationBuilder.RenameColumn(
                name: "VerificationTokenExpiresAt",
                table: "Users",
                newName: "VerificationCode"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "VerificationCodeExpiresAt",
                table: "Users",
                newName: "VerificationTokenHash"
            );

            migrationBuilder.RenameColumn(
                name: "VerificationCode",
                table: "Users",
                newName: "VerificationTokenExpiresAt"
            );
        }
    }
}
