using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfiniteTextGame.Lib.Migrations
{
    /// <inheritdoc />
    public partial class AddWriterSpecific : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Specific",
                table: "StoryChapters",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Specific",
                table: "StoryChapters");
        }
    }
}
