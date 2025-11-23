using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyDigialLibrary.Migrations
{
    /// <inheritdoc />
    public partial class AddCollections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "collection_rules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CollectionId = table.Column<int>(type: "int", nullable: false),
                    RuleType = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RuleValue = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_collection_rules", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_collection_rules_CollectionId_RuleType_RuleValue",
                table: "collection_rules",
                columns: new[] { "CollectionId", "RuleType", "RuleValue" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "collection_rules");
        }
    }
}
