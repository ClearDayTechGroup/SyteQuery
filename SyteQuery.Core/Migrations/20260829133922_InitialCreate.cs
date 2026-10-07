using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SyteQuery.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<string>(type: "TEXT", nullable: true),
                    Action = table.Column<string>(type: "TEXT", nullable: false),
                    EntityType = table.Column<string>(type: "TEXT", nullable: true),
                    EntityId = table.Column<string>(type: "TEXT", nullable: true),
                    Details = table.Column<string>(type: "TEXT", nullable: true),
                    IpAddress = table.Column<string>(type: "TEXT", nullable: true),
                    UserAgent = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IdoDefinitions",
                columns: table => new
                {
                    IdoName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    RevisionNo = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    XmlDefinition = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdoDefinitions", x => x.IdoName);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    UserName = table.Column<string>(type: "TEXT", nullable: true),
                    Email = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "QueryHistory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    HistoryId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Query = table.Column<string>(type: "TEXT", nullable: false),
                    EnvironmentName = table.Column<string>(type: "TEXT", nullable: false),
                    ExecutedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    WasSuccessful = table.Column<bool>(type: "INTEGER", nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: true),
                    RowsAffected = table.Column<int>(type: "INTEGER", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QueryHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QueryHistory_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "QuerySnippets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    SnippetId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    Category = table.Column<string>(type: "TEXT", nullable: true),
                    Query = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuerySnippets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuerySnippets_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ScheduledJobs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    JobId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    EnvironmentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    EnvironmentName = table.Column<string>(type: "TEXT", nullable: false),
                    IsRecurring = table.Column<bool>(type: "INTEGER", nullable: false),
                    RunOnceAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CronExpression = table.Column<string>(type: "TEXT", nullable: true),
                    SendResultsToEmail = table.Column<bool>(type: "INTEGER", nullable: false),
                    EmailConnectorId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ResultEmailRecipients = table.Column<string>(type: "TEXT", nullable: true),
                    ExportFormat = table.Column<string>(type: "TEXT", nullable: false, defaultValue: "Excel"),
                    SendResultsToWebhook = table.Column<bool>(type: "INTEGER", nullable: false),
                    WebhookConnectorId = table.Column<Guid>(type: "TEXT", nullable: true),
                    WebhookUrl = table.Column<string>(type: "TEXT", nullable: true),
                    WebhookHeaders = table.Column<string>(type: "TEXT", nullable: true),
                    SendErrorsToEmail = table.Column<bool>(type: "INTEGER", nullable: false),
                    ErrorEmailRecipients = table.Column<string>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    LastRunAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    NextRunAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduledJobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScheduledJobs_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserEnvironments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    EnvironmentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Url = table.Column<string>(type: "TEXT", nullable: false),
                    ConfigName = table.Column<string>(type: "TEXT", nullable: false),
                    Username = table.Column<string>(type: "TEXT", nullable: false),
                    EncryptedPassword = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserEnvironments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserEnvironments_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserNotificationConnectors",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    ConnectorId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    ConnectorType = table.Column<string>(type: "TEXT", nullable: false),
                    IsDefault = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    SmtpHost = table.Column<string>(type: "TEXT", nullable: true),
                    SmtpPort = table.Column<int>(type: "INTEGER", nullable: true),
                    UseSsl = table.Column<bool>(type: "INTEGER", nullable: true),
                    SmtpUsername = table.Column<string>(type: "TEXT", nullable: true),
                    SmtpPassword = table.Column<string>(type: "TEXT", nullable: true),
                    FromAddress = table.Column<string>(type: "TEXT", nullable: true),
                    FromName = table.Column<string>(type: "TEXT", nullable: true),
                    WebhookUrl = table.Column<string>(type: "TEXT", nullable: true),
                    WebhookHeaders = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserNotificationConnectors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserNotificationConnectors_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserProfiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    FirstName = table.Column<string>(type: "TEXT", nullable: true),
                    LastName = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    ShowAllDataByDefault = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserProfiles_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ScheduledJobExecutions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ScheduledJobId = table.Column<int>(type: "INTEGER", nullable: false),
                    ExecutedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    TotalDurationMs = table.Column<long>(type: "INTEGER", nullable: true),
                    Success = table.Column<bool>(type: "INTEGER", nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: true),
                    EmailSent = table.Column<bool>(type: "INTEGER", nullable: true),
                    EmailError = table.Column<string>(type: "TEXT", nullable: true),
                    WebhookSent = table.Column<bool>(type: "INTEGER", nullable: true),
                    WebhookError = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduledJobExecutions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScheduledJobExecutions_ScheduledJobs_ScheduledJobId",
                        column: x => x.ScheduledJobId,
                        principalTable: "ScheduledJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ScheduledJobSteps",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ScheduledJobId = table.Column<int>(type: "INTEGER", nullable: false),
                    StepId = table.Column<Guid>(type: "TEXT", nullable: false),
                    StepOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    StepName = table.Column<string>(type: "TEXT", nullable: true),
                    Query = table.Column<string>(type: "TEXT", nullable: false),
                    ContinueOnError = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduledJobSteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScheduledJobSteps_ScheduledJobs_ScheduledJobId",
                        column: x => x.ScheduledJobId,
                        principalTable: "ScheduledJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ScheduledJobStepExecutions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ScheduledJobExecutionId = table.Column<long>(type: "INTEGER", nullable: false),
                    ScheduledJobStepId = table.Column<int>(type: "INTEGER", nullable: false),
                    StepOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    StepName = table.Column<string>(type: "TEXT", nullable: true),
                    ExecutedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DurationMs = table.Column<long>(type: "INTEGER", nullable: true),
                    RowCount = table.Column<int>(type: "INTEGER", nullable: true),
                    Success = table.Column<bool>(type: "INTEGER", nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduledJobStepExecutions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScheduledJobStepExecutions_ScheduledJobExecutions_ScheduledJobExecutionId",
                        column: x => x.ScheduledJobExecutionId,
                        principalTable: "ScheduledJobExecutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ScheduledJobStepExecutions_ScheduledJobSteps_ScheduledJobStepId",
                        column: x => x.ScheduledJobStepId,
                        principalTable: "ScheduledJobSteps",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Action_CreatedAt",
                table: "AuditLogs",
                columns: new[] { "Action", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_UserId_CreatedAt",
                table: "AuditLogs",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_QueryHistory_UserId_ExecutedAt",
                table: "QueryHistory",
                columns: new[] { "UserId", "ExecutedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_QueryHistory_UserId_HistoryId",
                table: "QueryHistory",
                columns: new[] { "UserId", "HistoryId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuerySnippets_UserId_SnippetId",
                table: "QuerySnippets",
                columns: new[] { "UserId", "SnippetId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledJobExecutions_ScheduledJobId_ExecutedAt",
                table: "ScheduledJobExecutions",
                columns: new[] { "ScheduledJobId", "ExecutedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledJobs_IsActive_NextRunAt",
                table: "ScheduledJobs",
                columns: new[] { "IsActive", "NextRunAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledJobs_UserId_JobId",
                table: "ScheduledJobs",
                columns: new[] { "UserId", "JobId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledJobStepExecutions_ScheduledJobExecutionId_StepOrder",
                table: "ScheduledJobStepExecutions",
                columns: new[] { "ScheduledJobExecutionId", "StepOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledJobStepExecutions_ScheduledJobStepId",
                table: "ScheduledJobStepExecutions",
                column: "ScheduledJobStepId");

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledJobSteps_ScheduledJobId_StepOrder",
                table: "ScheduledJobSteps",
                columns: new[] { "ScheduledJobId", "StepOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserEnvironments_UserId_EnvironmentId",
                table: "UserEnvironments",
                columns: new[] { "UserId", "EnvironmentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserNotificationConnectors_UserId_ConnectorId",
                table: "UserNotificationConnectors",
                columns: new[] { "UserId", "ConnectorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserProfiles_UserId",
                table: "UserProfiles",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "IdoDefinitions");

            migrationBuilder.DropTable(
                name: "QueryHistory");

            migrationBuilder.DropTable(
                name: "QuerySnippets");

            migrationBuilder.DropTable(
                name: "ScheduledJobStepExecutions");

            migrationBuilder.DropTable(
                name: "UserEnvironments");

            migrationBuilder.DropTable(
                name: "UserNotificationConnectors");

            migrationBuilder.DropTable(
                name: "UserProfiles");

            migrationBuilder.DropTable(
                name: "ScheduledJobExecutions");

            migrationBuilder.DropTable(
                name: "ScheduledJobSteps");

            migrationBuilder.DropTable(
                name: "ScheduledJobs");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
