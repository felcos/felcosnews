using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ANews.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMorningBriefRegion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MorningBriefs_BriefDate",
                table: "MorningBriefs");

            migrationBuilder.AddColumn<string>(
                name: "Region",
                table: "MorningBriefs",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MorningBriefs_BriefDate_Region",
                table: "MorningBriefs",
                columns: new[] { "BriefDate", "Region" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MorningBriefs_BriefDate_Region",
                table: "MorningBriefs");

            migrationBuilder.DropColumn(
                name: "Region",
                table: "MorningBriefs");

            migrationBuilder.CreateIndex(
                name: "IX_MorningBriefs_BriefDate",
                table: "MorningBriefs",
                column: "BriefDate",
                unique: true);
        }
    }
}
