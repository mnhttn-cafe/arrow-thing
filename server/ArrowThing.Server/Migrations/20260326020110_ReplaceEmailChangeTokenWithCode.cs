using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArrowThing.Server.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceEmailChangeTokenWithCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "PendingEmailTokenHash",
                table: "Users",
                newName: "PendingEmailCodeExpiresAt"
            );

            migrationBuilder.RenameColumn(
                name: "PendingEmailTokenExpiresAt",
                table: "Users",
                newName: "PendingEmailCode"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "PendingEmailCodeExpiresAt",
                table: "Users",
                newName: "PendingEmailTokenHash"
            );

            migrationBuilder.RenameColumn(
                name: "PendingEmailCode",
                table: "Users",
                newName: "PendingEmailTokenExpiresAt"
            );
        }
    }
}
