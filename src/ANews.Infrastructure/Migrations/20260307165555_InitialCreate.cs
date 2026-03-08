using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ANews.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AiProviderConfigs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Provider = table.Column<int>(type: "integer", nullable: false),
                    Model = table.Column<string>(type: "text", nullable: false),
                    EncryptedApiKey = table.Column<string>(type: "text", nullable: false),
                    BaseUrl = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    CostPerInputTokenK = table.Column<decimal>(type: "numeric(10,6)", precision: 10, scale: 6, nullable: false),
                    CostPerOutputTokenK = table.Column<decimal>(type: "numeric(10,6)", precision: 10, scale: 6, nullable: false),
                    MaxTokens = table.Column<int>(type: "integer", nullable: false),
                    RateLimitPerMinute = table.Column<int>(type: "integer", nullable: false),
                    TotalRequestsToday = table.Column<int>(type: "integer", nullable: false),
                    TotalRequestsMonth = table.Column<int>(type: "integer", nullable: false),
                    TotalCostToday = table.Column<decimal>(type: "numeric(10,4)", precision: 10, scale: 4, nullable: false),
                    TotalCostMonth = table.Column<decimal>(type: "numeric(10,4)", precision: 10, scale: 4, nullable: false),
                    MonthlyBudgetLimit = table.Column<decimal>(type: "numeric(10,4)", precision: 10, scale: 4, nullable: false),
                    LastUsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastResetDailyAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExtraConfig = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiProviderConfigs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ApiTokens",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    TokenHash = table.Column<string>(type: "text", nullable: false),
                    TokenPrefix = table.Column<string>(type: "text", nullable: false),
                    Scopes = table.Column<string[]>(type: "text[]", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastUsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UsageCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiTokens", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    UserEmail = table.Column<string>(type: "text", nullable: false),
                    Action = table.Column<string>(type: "text", nullable: false),
                    EntityType = table.Column<string>(type: "text", nullable: false),
                    EntityId = table.Column<string>(type: "text", nullable: true),
                    OldValues = table.Column<string>(type: "text", nullable: true),
                    NewValues = table.Column<string>(type: "text", nullable: true),
                    IpAddress = table.Column<string>(type: "text", nullable: true),
                    UserAgent = table.Column<string>(type: "text", nullable: true),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NewsSections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Slug = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    IsPublic = table.Column<bool>(type: "boolean", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    IsSystemSection = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IconClass = table.Column<string>(type: "text", nullable: true),
                    Color = table.Column<string>(type: "text", nullable: true),
                    Keywords = table.Column<List<string>>(type: "jsonb", nullable: false),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NewsSections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NotificationChannels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Config = table.Column<string>(type: "text", nullable: false),
                    IsVerified = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    VerificationToken = table.Column<string>(type: "text", nullable: true),
                    VerifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TotalSent = table.Column<int>(type: "integer", nullable: false),
                    TotalFailed = table.Column<int>(type: "integer", nullable: false),
                    LastUsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationChannels", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "roles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserModules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    NotificationsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    NotificationFrequency = table.Column<int>(type: "integer", nullable: false),
                    IconClass = table.Column<string>(type: "text", nullable: true),
                    Color = table.Column<string>(type: "text", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    RssFeedToken = table.Column<string>(type: "text", nullable: true),
                    TotalMatchedEvents = table.Column<int>(type: "integer", nullable: false),
                    LastMatchAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserModules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DisplayName = table.Column<string>(type: "text", nullable: false),
                    AvatarUrl = table.Column<string>(type: "text", nullable: true),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastLoginAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReceiveDigest = table.Column<bool>(type: "boolean", nullable: false),
                    DigestFrequency = table.Column<int>(type: "integer", nullable: false),
                    ApiTokenPrefix = table.Column<string>(type: "text", nullable: true),
                    UserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: true),
                    SecurityStamp = table.Column<string>(type: "text", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true),
                    PhoneNumber = table.Column<string>(type: "text", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AgentExecutions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AgentType = table.Column<int>(type: "integer", nullable: false),
                    AgentName = table.Column<string>(type: "text", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Error = table.Column<string>(type: "text", nullable: true),
                    ItemsProcessed = table.Column<int>(type: "integer", nullable: false),
                    ItemsCreated = table.Column<int>(type: "integer", nullable: false),
                    ItemsUpdated = table.Column<int>(type: "integer", nullable: false),
                    AiCost = table.Column<decimal>(type: "numeric", nullable: false),
                    AiProviderConfigId = table.Column<int>(type: "integer", nullable: true),
                    TriggerReason = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentExecutions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgentExecutions_AiProviderConfigs_AiProviderConfigId",
                        column: x => x.AiProviderConfigId,
                        principalTable: "AiProviderConfigs",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "NewsEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    ImpactScore = table.Column<decimal>(type: "numeric", nullable: false),
                    Category = table.Column<string>(type: "text", nullable: true),
                    EventType = table.Column<string>(type: "text", nullable: true),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Location = table.Column<string>(type: "text", nullable: true),
                    Tags = table.Column<string[]>(type: "jsonb", nullable: false),
                    Trend = table.Column<int>(type: "integer", nullable: false),
                    NewsSectionId = table.Column<int>(type: "integer", nullable: false),
                    ParentEventId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NewsEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NewsEvents_NewsEvents_ParentEventId",
                        column: x => x.ParentEventId,
                        principalTable: "NewsEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_NewsEvents_NewsSections_NewsSectionId",
                        column: x => x.NewsSectionId,
                        principalTable: "NewsSections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NewsSources",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Url = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    CredibilityScore = table.Column<int>(type: "integer", nullable: false),
                    Language = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    NewsSectionId = table.Column<int>(type: "integer", nullable: false),
                    LastScannedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TotalArticlesFound = table.Column<int>(type: "integer", nullable: false),
                    SuccessfulScans = table.Column<int>(type: "integer", nullable: false),
                    FailedScans = table.Column<int>(type: "integer", nullable: false),
                    LastError = table.Column<string>(type: "text", nullable: true),
                    CustomHeaders = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NewsSources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NewsSources_NewsSections_NewsSectionId",
                        column: x => x.NewsSectionId,
                        principalTable: "NewsSections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "role_claims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RoleId = table.Column<int>(type: "integer", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_role_claims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_role_claims_roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ModuleKeywords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Keyword = table.Column<string>(type: "text", nullable: false),
                    Weight = table.Column<decimal>(type: "numeric", nullable: false),
                    IsExact = table.Column<bool>(type: "boolean", nullable: false),
                    UserModuleId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModuleKeywords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModuleKeywords_UserModules_UserModuleId",
                        column: x => x.UserModuleId,
                        principalTable: "UserModules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_claims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_claims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_claims_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_logins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    ProviderKey = table.Column<string>(type: "text", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "text", nullable: true),
                    UserId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_logins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_user_logins_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_roles",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    RoleId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_roles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_user_roles_roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_roles_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_tokens",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_tokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_user_tokens_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AgentLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AgentExecutionId = table.Column<int>(type: "integer", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    Message = table.Column<string>(type: "text", nullable: false),
                    Data = table.Column<string>(type: "text", nullable: true),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgentLogs_AgentExecutions_AgentExecutionId",
                        column: x => x.AgentExecutionId,
                        principalTable: "AgentExecutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CostEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AiProviderConfigId = table.Column<int>(type: "integer", nullable: false),
                    AgentExecutionId = table.Column<int>(type: "integer", nullable: true),
                    Operation = table.Column<string>(type: "text", nullable: false),
                    InputTokens = table.Column<int>(type: "integer", nullable: false),
                    OutputTokens = table.Column<int>(type: "integer", nullable: false),
                    Cost = table.Column<decimal>(type: "numeric(10,6)", precision: 10, scale: 6, nullable: false),
                    Date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CostEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CostEntries_AgentExecutions_AgentExecutionId",
                        column: x => x.AgentExecutionId,
                        principalTable: "AgentExecutions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CostEntries_AiProviderConfigs_AiProviderConfigId",
                        column: x => x.AiProviderConfigId,
                        principalTable: "AiProviderConfigs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AlertTriggers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NewsEventId = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Message = table.Column<string>(type: "text", nullable: false),
                    Severity = table.Column<int>(type: "integer", nullable: false),
                    IsAcknowledged = table.Column<bool>(type: "boolean", nullable: false),
                    AcknowledgedByUserId = table.Column<int>(type: "integer", nullable: true),
                    AcknowledgedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NotificationSent = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlertTriggers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AlertTriggers_NewsEvents_NewsEventId",
                        column: x => x.NewsEventId,
                        principalTable: "NewsEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NotificationLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    NotificationChannelId = table.Column<int>(type: "integer", nullable: true),
                    UserModuleId = table.Column<int>(type: "integer", nullable: true),
                    NewsEventId = table.Column<int>(type: "integer", nullable: true),
                    Message = table.Column<string>(type: "text", nullable: false),
                    Sent = table.Column<bool>(type: "boolean", nullable: false),
                    Error = table.Column<string>(type: "text", nullable: true),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NotificationLogs_NewsEvents_NewsEventId",
                        column: x => x.NewsEventId,
                        principalTable: "NewsEvents",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_NotificationLogs_NotificationChannels_NotificationChannelId",
                        column: x => x.NotificationChannelId,
                        principalTable: "NotificationChannels",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_NotificationLogs_UserModules_UserModuleId",
                        column: x => x.UserModuleId,
                        principalTable: "UserModules",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "NewsArticles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: true),
                    Summary = table.Column<string>(type: "text", nullable: true),
                    SourceUrl = table.Column<string>(type: "text", nullable: false),
                    SourceName = table.Column<string>(type: "text", nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ArticleType = table.Column<int>(type: "integer", nullable: false),
                    Relevance = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    SentimentScore = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    CredibilityScore = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    Language = table.Column<string>(type: "text", nullable: false),
                    ImageUrl = table.Column<string>(type: "text", nullable: true),
                    Keywords = table.Column<string[]>(type: "jsonb", nullable: false),
                    ContentHash = table.Column<string>(type: "text", nullable: true),
                    NewsEventId = table.Column<int>(type: "integer", nullable: false),
                    NewsSourceId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NewsArticles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NewsArticles_NewsEvents_NewsEventId",
                        column: x => x.NewsEventId,
                        principalTable: "NewsEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_NewsArticles_NewsSources_NewsSourceId",
                        column: x => x.NewsSourceId,
                        principalTable: "NewsSources",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Bookmarks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    NewsArticleId = table.Column<int>(type: "integer", nullable: false),
                    Note = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bookmarks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Bookmarks_NewsArticles_NewsArticleId",
                        column: x => x.NewsArticleId,
                        principalTable: "NewsArticles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AgentExecutions_AiProviderConfigId",
                table: "AgentExecutions",
                column: "AiProviderConfigId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentLogs_AgentExecutionId",
                table: "AgentLogs",
                column: "AgentExecutionId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentLogs_Timestamp",
                table: "AgentLogs",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_AlertTriggers_NewsEventId",
                table: "AlertTriggers",
                column: "NewsEventId");

            migrationBuilder.CreateIndex(
                name: "IX_ApiTokens_TokenHash",
                table: "ApiTokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Bookmarks_NewsArticleId",
                table: "Bookmarks",
                column: "NewsArticleId");

            migrationBuilder.CreateIndex(
                name: "IX_Bookmarks_UserId_NewsArticleId",
                table: "Bookmarks",
                columns: new[] { "UserId", "NewsArticleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CostEntries_AgentExecutionId",
                table: "CostEntries",
                column: "AgentExecutionId");

            migrationBuilder.CreateIndex(
                name: "IX_CostEntries_AiProviderConfigId_Date",
                table: "CostEntries",
                columns: new[] { "AiProviderConfigId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_ModuleKeywords_UserModuleId",
                table: "ModuleKeywords",
                column: "UserModuleId");

            migrationBuilder.CreateIndex(
                name: "IX_NewsArticles_ContentHash",
                table: "NewsArticles",
                column: "ContentHash");

            migrationBuilder.CreateIndex(
                name: "IX_NewsArticles_NewsEventId",
                table: "NewsArticles",
                column: "NewsEventId");

            migrationBuilder.CreateIndex(
                name: "IX_NewsArticles_NewsSourceId",
                table: "NewsArticles",
                column: "NewsSourceId");

            migrationBuilder.CreateIndex(
                name: "IX_NewsArticles_PublishedAt",
                table: "NewsArticles",
                column: "PublishedAt");

            migrationBuilder.CreateIndex(
                name: "IX_NewsEvents_CreatedAt",
                table: "NewsEvents",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_NewsEvents_NewsSectionId",
                table: "NewsEvents",
                column: "NewsSectionId");

            migrationBuilder.CreateIndex(
                name: "IX_NewsEvents_ParentEventId",
                table: "NewsEvents",
                column: "ParentEventId");

            migrationBuilder.CreateIndex(
                name: "IX_NewsEvents_Priority_ImpactScore",
                table: "NewsEvents",
                columns: new[] { "Priority", "ImpactScore" });

            migrationBuilder.CreateIndex(
                name: "IX_NewsSections_Slug",
                table: "NewsSections",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NewsSources_NewsSectionId",
                table: "NewsSources",
                column: "NewsSectionId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationLogs_NewsEventId",
                table: "NotificationLogs",
                column: "NewsEventId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationLogs_NotificationChannelId",
                table: "NotificationLogs",
                column: "NotificationChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationLogs_UserModuleId",
                table: "NotificationLogs",
                column: "UserModuleId");

            migrationBuilder.CreateIndex(
                name: "IX_role_claims_RoleId",
                table: "role_claims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "roles",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_claims_UserId",
                table: "user_claims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_user_logins_UserId",
                table: "user_logins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_user_roles_RoleId",
                table: "user_roles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "users",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "users",
                column: "NormalizedUserName",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgentLogs");

            migrationBuilder.DropTable(
                name: "AlertTriggers");

            migrationBuilder.DropTable(
                name: "ApiTokens");

            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "Bookmarks");

            migrationBuilder.DropTable(
                name: "CostEntries");

            migrationBuilder.DropTable(
                name: "ModuleKeywords");

            migrationBuilder.DropTable(
                name: "NotificationLogs");

            migrationBuilder.DropTable(
                name: "role_claims");

            migrationBuilder.DropTable(
                name: "user_claims");

            migrationBuilder.DropTable(
                name: "user_logins");

            migrationBuilder.DropTable(
                name: "user_roles");

            migrationBuilder.DropTable(
                name: "user_tokens");

            migrationBuilder.DropTable(
                name: "NewsArticles");

            migrationBuilder.DropTable(
                name: "AgentExecutions");

            migrationBuilder.DropTable(
                name: "NotificationChannels");

            migrationBuilder.DropTable(
                name: "UserModules");

            migrationBuilder.DropTable(
                name: "roles");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "NewsEvents");

            migrationBuilder.DropTable(
                name: "NewsSources");

            migrationBuilder.DropTable(
                name: "AiProviderConfigs");

            migrationBuilder.DropTable(
                name: "NewsSections");
        }
    }
}
