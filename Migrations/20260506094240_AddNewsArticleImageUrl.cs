using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BPFL.API.Migrations
{
    /// <inheritdoc />
    public partial class AddNewsArticleImageUrl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                table: "NewsArticles",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImageUrl",
                table: "NewsArticles");
        }
    }
}
