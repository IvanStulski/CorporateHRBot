using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CorporateHRBot.Migrations
{
    /// <inheritdoc />
    public partial class AddEmbeddingToDocumentChunksTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EmbeddingJson",
                table: "DocumentChunks",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EmbeddingJson",
                table: "DocumentChunks");
        }
    }
}
