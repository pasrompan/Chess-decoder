using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChessDecoderApi.Migrations
{
    public partial class AddGameSoftDeleteFields : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "ChessGames",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "ChessGames",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "ChessGames");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "ChessGames");
        }
    }
}
