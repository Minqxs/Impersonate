using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Impersonate.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class IntroduceRunDelivery : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "RunDeliveries",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                PipelineRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Status = table.Column<int>(type: "int", nullable: false),
                SourceDefaultBranch = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                SourceBaseCommitSha = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                RunBranchName = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                RunBranchHeadSha = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                AggregateValidationSummaryJson = table.Column<string>(type: "nvarchar(max)", maxLength: 16000, nullable: false),
                FinalReviewDecisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                FinalReviewedHeadSha = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                FinalPullRequestProvider = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                FinalPullRequestRepository = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                FinalPullRequestNumber = table.Column<long>(type: "bigint", nullable: true),
                FinalPullRequestUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                FinalPullRequestHeadSha = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                FinalPullRequestBaseBranch = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                FinalPullRequestMergeableState = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                RequiredChecksState = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                FailureCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                FailureMessage = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                ClaimId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                ClaimOwner = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                ClaimedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                ClaimExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_RunDeliveries", x => x.Id);
                table.ForeignKey(
                    name: "FK_RunDeliveries_PipelineRuns_PipelineRunId",
                    column: x => x.PipelineRunId,
                    principalTable: "PipelineRuns",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_RunDeliveries_Projects_ProjectId",
                    column: x => x.ProjectId,
                    principalTable: "Projects",
                    principalColumn: "Id");
            });

        migrationBuilder.CreateIndex(
            name: "IX_RunDeliveries_FinalPullRequestRepository_FinalPullRequestNumber",
            table: "RunDeliveries",
            columns: new[] { "FinalPullRequestRepository", "FinalPullRequestNumber" },
            unique: true,
            filter: "[FinalPullRequestNumber] IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_RunDeliveries_PipelineRunId",
            table: "RunDeliveries",
            column: "PipelineRunId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_RunDeliveries_ProjectId_RunBranchName",
            table: "RunDeliveries",
            columns: new[] { "ProjectId", "RunBranchName" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_RunDeliveries_ProjectId_Status",
            table: "RunDeliveries",
            columns: new[] { "ProjectId", "Status" });

        migrationBuilder.CreateIndex(
            name: "IX_RunDeliveries_Status_ClaimExpiresAtUtc_CreatedAtUtc",
            table: "RunDeliveries",
            columns: new[] { "Status", "ClaimExpiresAtUtc", "CreatedAtUtc" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "RunDeliveries");
    }
}
