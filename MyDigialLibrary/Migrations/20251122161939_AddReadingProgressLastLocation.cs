using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyDigialLibrary.Migrations
{
    /// <inheritdoc />
    public partial class AddReadingProgressLastLocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LastLocation",
                table: "reading_progress",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastLocation",
                table: "reading_progress");
        }
    }
}
