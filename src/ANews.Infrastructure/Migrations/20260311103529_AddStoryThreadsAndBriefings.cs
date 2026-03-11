using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ANews.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStoryThreadsAndBriefings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Bias",
                table: "NewsSources",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CorrectionCount",
                table: "NewsSources",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "FactDensityAvg",
                table: "NewsSources",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "SpeedScore",
                table: "NewsSources",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CrossReferenceCount",
                table: "NewsEvents",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SourceDiversity",
                table: "NewsEvents",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "StoryThreadId",
                table: "NewsEvents",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MorningBriefs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BriefDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Headline = table.Column<string>(type: "text", nullable: false),
                    TopStories = table.Column<string>(type: "text", nullable: false),
                    DeepDive = table.Column<string>(type: "text", nullable: true),
                    Developing = table.Column<string>(type: "text", nullable: true),
                    Surprise = table.Column<string>(type: "text", nullable: true),
                    TopStoriesCount = table.Column<int>(type: "integer", nullable: false),
                    TotalEventsAnalyzed = table.Column<int>(type: "integer", nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MorningBriefs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StoryThreads",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Summary = table.Column<string>(type: "text", nullable: true),
                    WhyItMatters = table.Column<string>(type: "text", nullable: true),
                    Background = table.Column<string>(type: "text", nullable: true),
                    WhatToWatch = table.Column<string>(type: "text", nullable: true),
                    KeyActors = table.Column<string[]>(type: "jsonb", nullable: false),
                    Tags = table.Column<string[]>(type: "jsonb", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    MaxPriority = table.Column<int>(type: "integer", nullable: false),
                    MaxImpactScore = table.Column<decimal>(type: "numeric", nullable: false),
                    FirstEventDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastEventDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastBriefingAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EventCount = table.Column<int>(type: "integer", nullable: false),
                    TotalArticles = table.Column<int>(type: "integer", nullable: false),
                    PrimarySectionId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoryThreads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StoryThreads_NewsSections_PrimarySectionId",
                        column: x => x.PrimarySectionId,
                        principalTable: "NewsSections",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "EventBriefings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NewsEventId = table.Column<int>(type: "integer", nullable: true),
                    StoryThreadId = table.Column<int>(type: "integer", nullable: true),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    WhyItMatters = table.Column<string>(type: "text", nullable: true),
                    Background = table.Column<string>(type: "text", nullable: true),
                    KeyActors = table.Column<string>(type: "text", nullable: true),
                    WhatToWatch = table.Column<string>(type: "text", nullable: true),
                    FullContent = table.Column<string>(type: "text", nullable: true),
                    GeneratedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SourceArticleCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventBriefings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EventBriefings_NewsEvents_NewsEventId",
                        column: x => x.NewsEventId,
                        principalTable: "NewsEvents",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EventBriefings_StoryThreads_StoryThreadId",
                        column: x => x.StoryThreadId,
                        principalTable: "StoryThreads",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_NewsEvents_StoryThreadId",
                table: "NewsEvents",
                column: "StoryThreadId");

            migrationBuilder.CreateIndex(
                name: "IX_EventBriefings_NewsEventId",
                table: "EventBriefings",
                column: "NewsEventId");

            migrationBuilder.CreateIndex(
                name: "IX_EventBriefings_StoryThreadId",
                table: "EventBriefings",
                column: "StoryThreadId");

            migrationBuilder.CreateIndex(
                name: "IX_EventBriefings_Type",
                table: "EventBriefings",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_MorningBriefs_BriefDate",
                table: "MorningBriefs",
                column: "BriefDate",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StoryThreads_LastEventDate",
                table: "StoryThreads",
                column: "LastEventDate");

            migrationBuilder.CreateIndex(
                name: "IX_StoryThreads_PrimarySectionId",
                table: "StoryThreads",
                column: "PrimarySectionId");

            migrationBuilder.CreateIndex(
                name: "IX_StoryThreads_Status",
                table: "StoryThreads",
                column: "Status");

            migrationBuilder.AddForeignKey(
                name: "FK_NewsEvents_StoryThreads_StoryThreadId",
                table: "NewsEvents",
                column: "StoryThreadId",
                principalTable: "StoryThreads",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_NewsEvents_StoryThreads_StoryThreadId",
                table: "NewsEvents");

            migrationBuilder.DropTable(
                name: "EventBriefings");

            migrationBuilder.DropTable(
                name: "MorningBriefs");

            migrationBuilder.DropTable(
                name: "StoryThreads");

            migrationBuilder.DropIndex(
                name: "IX_NewsEvents_StoryThreadId",
                table: "NewsEvents");

            migrationBuilder.DropColumn(
                name: "Bias",
                table: "NewsSources");

            migrationBuilder.DropColumn(
                name: "CorrectionCount",
                table: "NewsSources");

            migrationBuilder.DropColumn(
                name: "FactDensityAvg",
                table: "NewsSources");

            migrationBuilder.DropColumn(
                name: "SpeedScore",
                table: "NewsSources");

            migrationBuilder.DropColumn(
                name: "CrossReferenceCount",
                table: "NewsEvents");

            migrationBuilder.DropColumn(
                name: "SourceDiversity",
                table: "NewsEvents");

            migrationBuilder.DropColumn(
                name: "StoryThreadId",
                table: "NewsEvents");
        }
    }
}
