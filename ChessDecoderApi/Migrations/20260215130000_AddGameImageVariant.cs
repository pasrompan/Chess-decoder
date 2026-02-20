using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChessDecoderApi.Migrations
{
    public partial class AddGameImageVariant : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Variant",
                table: "GameImages",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                defaultValue: "original");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Variant",
                table: "GameImages");
        }
    }
}
