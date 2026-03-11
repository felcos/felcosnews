using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ANews.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddActivityTrackingAndQuotas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SectionQuotas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    NewsSectionId = table.Column<int>(type: "integer", nullable: false),
                    MaxReadsPerMonth = table.Column<int>(type: "integer", nullable: false),
                    CurrentReads = table.Column<int>(type: "integer", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PlanTier = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SectionQuotas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SectionQuotas_NewsSections_NewsSectionId",
                        column: x => x.NewsSectionId,
                        principalTable: "NewsSections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserActivities",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    ActivityType = table.Column<int>(type: "integer", nullable: false),
                    NewsSectionId = table.Column<int>(type: "integer", nullable: true),
                    NewsEventId = table.Column<int>(type: "integer", nullable: true),
                    NewsArticleId = table.Column<int>(type: "integer", nullable: true),
                    StoryThreadId = table.Column<int>(type: "integer", nullable: true),
                    Metadata = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserActivities", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SectionQuotas_NewsSectionId",
                table: "SectionQuotas",
                column: "NewsSectionId");

            migrationBuilder.CreateIndex(
                name: "IX_SectionQuotas_UserId_NewsSectionId",
                table: "SectionQuotas",
                columns: new[] { "UserId", "NewsSectionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserActivities_CreatedAt",
                table: "UserActivities",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_UserActivities_UserId",
                table: "UserActivities",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserActivities_UserId_CreatedAt",
                table: "UserActivities",
                columns: new[] { "UserId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SectionQuotas");

            migrationBuilder.DropTable(
                name: "UserActivities");
        }
    }
}
