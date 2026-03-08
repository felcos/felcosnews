using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ANews.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEventCoordinates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "NewsEvents",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "NewsEvents",
                type: "double precision",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "NewsEvents");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "NewsEvents");
        }
    }
}
