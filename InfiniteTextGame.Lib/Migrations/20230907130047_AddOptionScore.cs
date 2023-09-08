using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfiniteTextGame.Lib.Migrations
{
    /// <inheritdoc />
    public partial class AddOptionScore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ComplexityScore",
                table: "StoryChapterOptions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ImpactScore",
                table: "StoryChapterOptions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PositivityScore",
                table: "StoryChapterOptions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ComplexityScore",
                table: "StoryChapterOptions");

            migrationBuilder.DropColumn(
                name: "ImpactScore",
                table: "StoryChapterOptions");

            migrationBuilder.DropColumn(
                name: "PositivityScore",
                table: "StoryChapterOptions");
        }
    }
}
