using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArrowThing.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddScores : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Scores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    GameId = table.Column<Guid>(type: "uuid", nullable: false),
                    Seed = table.Column<int>(type: "integer", nullable: false),
                    BoardWidth = table.Column<int>(type: "integer", nullable: false),
                    BoardHeight = table.Column<int>(type: "integer", nullable: false),
                    MaxArrowLength = table.Column<int>(type: "integer", nullable: false),
                    Time = table.Column<double>(type: "double precision", nullable: false),
                    ReplayJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    UpdatedAt = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Scores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Scores_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_Scores_BoardWidth_BoardHeight_Time",
                table: "Scores",
                columns: new[] { "BoardWidth", "BoardHeight", "Time" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_Scores_UserId_BoardWidth_BoardHeight",
                table: "Scores",
                columns: new[] { "UserId", "BoardWidth", "BoardHeight" },
                unique: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "Scores");
        }
    }
}
