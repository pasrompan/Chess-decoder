using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChessDecoderApi.Migrations
{
    /// <summary>
    /// Adds <c>VariantsJson</c> to <c>ChessGames</c> to persist the variant
    /// tree captured in the frontend's Explore mode. Stored as a nullable
    /// TEXT blob so the schema can evolve without further migrations.
    /// </summary>
    public partial class AddChessGameVariantsJson : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "VariantsJson",
                table: "ChessGames",
                type: "TEXT",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "VariantsJson",
                table: "ChessGames");
        }
    }
}
