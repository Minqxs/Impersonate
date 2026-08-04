using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Impersonate.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class IntroduceTaskDeliveryFoundation : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "TaskDeliveries",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                PipelineRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                PlannedTaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TaskSequence = table.Column<int>(type: "int", nullable: false),
                SourceBaseCommitSha = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                ApprovedPatchArtifactReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                ApprovedPatchSha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                ApprovedReviewDecisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                IdempotencyKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                BranchName = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                CommitSha = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                PullRequestProvider = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                PullRequestRepository = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                PullRequestNumber = table.Column<long>(type: "bigint", nullable: true),
                PullRequestUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                FailureCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                FailureMessage = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TaskDeliveries", x => x.Id);
                table.ForeignKey(
                    name: "FK_TaskDeliveries_PipelineRuns_PipelineRunId",
                    column: x => x.PipelineRunId,
                    principalTable: "PipelineRuns",
                    principalColumn: "Id");
                table.ForeignKey(
                    name: "FK_TaskDeliveries_PlannedTasks_PlannedTaskId",
                    column: x => x.PlannedTaskId,
                    principalTable: "PlannedTasks",
                    principalColumn: "Id");
                table.ForeignKey(
                    name: "FK_TaskDeliveries_Projects_ProjectId",
                    column: x => x.ProjectId,
                    principalTable: "Projects",
                    principalColumn: "Id");
                table.ForeignKey(
                    name: "FK_TaskDeliveries_ReviewDecisions_ApprovedReviewDecisionId",
                    column: x => x.ApprovedReviewDecisionId,
                    principalTable: "ReviewDecisions",
                    principalColumn: "Id");
            });

        migrationBuilder.CreateIndex(
            name: "IX_TaskDeliveries_ApprovedReviewDecisionId",
            table: "TaskDeliveries",
            column: "ApprovedReviewDecisionId");

        migrationBuilder.CreateIndex(
            name: "IX_TaskDeliveries_IdempotencyKey",
            table: "TaskDeliveries",
            column: "IdempotencyKey",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_TaskDeliveries_PipelineRunId_Status",
            table: "TaskDeliveries",
            columns: new[] { "PipelineRunId", "Status" });

        migrationBuilder.CreateIndex(
            name: "IX_TaskDeliveries_PlannedTaskId",
            table: "TaskDeliveries",
            column: "PlannedTaskId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_TaskDeliveries_ProjectId_BranchName",
            table: "TaskDeliveries",
            columns: new[] { "ProjectId", "BranchName" },
            unique: true,
            filter: "[BranchName] IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_TaskDeliveries_ProjectId_Status",
            table: "TaskDeliveries",
            columns: new[] { "ProjectId", "Status" });

        migrationBuilder.CreateIndex(
            name: "IX_TaskDeliveries_PullRequestRepository_PullRequestNumber",
            table: "TaskDeliveries",
            columns: new[] { "PullRequestRepository", "PullRequestNumber" },
            unique: true,
            filter: "[PullRequestRepository] IS NOT NULL AND [PullRequestNumber] IS NOT NULL");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "TaskDeliveries");
    }
}
