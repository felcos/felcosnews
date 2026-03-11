using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ANews.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSourceAnalysisAndReaderProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReaderProfiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    SemanticProfile = table.Column<string>(type: "text", nullable: true),
                    TopInterests = table.Column<string[]>(type: "jsonb", nullable: false),
                    AvoidTopics = table.Column<string[]>(type: "jsonb", nullable: false),
                    PreferredDepth = table.Column<string>(type: "text", nullable: true),
                    ArticlesRead = table.Column<int>(type: "integer", nullable: false),
                    EventsOpened = table.Column<int>(type: "integer", nullable: false),
                    LastAnalyzedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastActivityAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReaderProfiles", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReaderProfiles_UserId",
                table: "ReaderProfiles",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReaderProfiles");
        }
    }
}
