using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChessDecoderApi.Migrations
{
    /// <inheritdoc />
    public partial class AddCloudStorageFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CloudStorageObjectName",
                table: "GameImages",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CloudStorageUrl",
                table: "GameImages",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsStoredInCloud",
                table: "GameImages",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CloudStorageObjectName",
                table: "GameImages");

            migrationBuilder.DropColumn(
                name: "CloudStorageUrl",
                table: "GameImages");

            migrationBuilder.DropColumn(
                name: "IsStoredInCloud",
                table: "GameImages");
        }
    }
}
