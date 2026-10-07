using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SyteQuery.Migrations
{
    /// <inheritdoc />
    public partial class RemoveSchedulingTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ScheduledJobStepExecutions");

            migrationBuilder.DropTable(
                name: "UserNotificationConnectors");

            migrationBuilder.DropTable(
                name: "ScheduledJobExecutions");

            migrationBuilder.DropTable(
                name: "ScheduledJobSteps");

            migrationBuilder.DropTable(
                name: "ScheduledJobs");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ScheduledJobs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CronExpression = table.Column<string>(type: "TEXT", nullable: true),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    EmailConnectorId = table.Column<Guid>(type: "TEXT", nullable: true),
                    EnvironmentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    EnvironmentName = table.Column<string>(type: "TEXT", nullable: false),
                    ErrorEmailRecipients = table.Column<string>(type: "TEXT", nullable: true),
                    ExportFormat = table.Column<string>(type: "TEXT", nullable: false, defaultValue: "Excel"),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    IsRecurring = table.Column<bool>(type: "INTEGER", nullable: false),
                    JobId = table.Column<Guid>(type: "TEXT", nullable: false),
                    LastRunAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    NextRunAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ResultEmailRecipients = table.Column<string>(type: "TEXT", nullable: true),
                    RunOnceAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    SendErrorsToEmail = table.Column<bool>(type: "INTEGER", nullable: false),
                    SendResultsToEmail = table.Column<bool>(type: "INTEGER", nullable: false),
                    SendResultsToWebhook = table.Column<bool>(type: "INTEGER", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    WebhookConnectorId = table.Column<Guid>(type: "TEXT", nullable: true),
                    WebhookHeaders = table.Column<string>(type: "TEXT", nullable: true),
                    WebhookUrl = table.Column<string>(type: "TEXT", nullable: true)
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
                name: "UserNotificationConnectors",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ConnectorId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ConnectorType = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    FromAddress = table.Column<string>(type: "TEXT", nullable: true),
                    FromName = table.Column<string>(type: "TEXT", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    IsDefault = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    SmtpHost = table.Column<string>(type: "TEXT", nullable: true),
                    SmtpPassword = table.Column<string>(type: "TEXT", nullable: true),
                    SmtpPort = table.Column<int>(type: "INTEGER", nullable: true),
                    SmtpUsername = table.Column<string>(type: "TEXT", nullable: true),
                    UseSsl = table.Column<bool>(type: "INTEGER", nullable: true),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    WebhookHeaders = table.Column<string>(type: "TEXT", nullable: true),
                    WebhookUrl = table.Column<string>(type: "TEXT", nullable: true)
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
                name: "ScheduledJobExecutions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ScheduledJobId = table.Column<int>(type: "INTEGER", nullable: false),
                    EmailError = table.Column<string>(type: "TEXT", nullable: true),
                    EmailSent = table.Column<bool>(type: "INTEGER", nullable: true),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: true),
                    ExecutedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Success = table.Column<bool>(type: "INTEGER", nullable: false),
                    TotalDurationMs = table.Column<long>(type: "INTEGER", nullable: true),
                    WebhookError = table.Column<string>(type: "TEXT", nullable: true),
                    WebhookSent = table.Column<bool>(type: "INTEGER", nullable: true)
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
                    ContinueOnError = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Query = table.Column<string>(type: "TEXT", nullable: false),
                    StepId = table.Column<Guid>(type: "TEXT", nullable: false),
                    StepName = table.Column<string>(type: "TEXT", nullable: true),
                    StepOrder = table.Column<int>(type: "INTEGER", nullable: false)
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
                    DurationMs = table.Column<long>(type: "INTEGER", nullable: true),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: true),
                    ExecutedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    RowCount = table.Column<int>(type: "INTEGER", nullable: true),
                    StepName = table.Column<string>(type: "TEXT", nullable: true),
                    StepOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    Success = table.Column<bool>(type: "INTEGER", nullable: false)
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
                name: "IX_UserNotificationConnectors_UserId_ConnectorId",
                table: "UserNotificationConnectors",
                columns: new[] { "UserId", "ConnectorId" },
                unique: true);
        }
    }
}
