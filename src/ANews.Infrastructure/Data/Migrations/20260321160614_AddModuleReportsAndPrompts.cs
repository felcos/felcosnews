using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ANews.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddModuleReportsAndPrompts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Prompt",
                table: "UserModules",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PromptRejectionReason",
                table: "UserModules",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PromptStatus",
                table: "UserModules",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "ModuleReports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserModuleId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    Summary = table.Column<string>(type: "text", nullable: true),
                    SourceEventIds = table.Column<List<int>>(type: "jsonb", nullable: false),
                    EventsAnalyzed = table.Column<int>(type: "integer", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModuleReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModuleReports_UserModules_UserModuleId",
                        column: x => x.UserModuleId,
                        principalTable: "UserModules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ModuleReports_UserId",
                table: "ModuleReports",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ModuleReports_UserModuleId_CreatedAt",
                table: "ModuleReports",
                columns: new[] { "UserModuleId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ModuleReports");

            migrationBuilder.DropColumn(
                name: "Prompt",
                table: "UserModules");

            migrationBuilder.DropColumn(
                name: "PromptRejectionReason",
                table: "UserModules");

            migrationBuilder.DropColumn(
                name: "PromptStatus",
                table: "UserModules");
        }
    }
}
