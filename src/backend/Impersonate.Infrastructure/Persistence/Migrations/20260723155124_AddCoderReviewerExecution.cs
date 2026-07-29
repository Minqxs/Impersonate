using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Impersonate.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddCoderReviewerExecution : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.RenameColumn(
            name: "OutputReference",
            newName: "PatchArtifactReference",
            table: "TaskAttempts");

        migrationBuilder.AlterColumn<string>(
            name: "PatchArtifactReference",
            table: "TaskAttempts",
            type: "nvarchar(500)",
            maxLength: 500,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "nvarchar(max)",
            oldNullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ChangedFilesJson",
            table: "TaskAttempts",
            type: "nvarchar(max)",
            maxLength: 16000,
            nullable: false,
            defaultValue: "[]");

        migrationBuilder.AddColumn<string>(
            name: "FailureCode",
            table: "TaskAttempts",
            type: "nvarchar(100)",
            maxLength: 100,
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "InputTokenCount",
            table: "TaskAttempts",
            type: "int",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Model",
            table: "TaskAttempts",
            type: "nvarchar(300)",
            maxLength: 300,
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "OutputTokenCount",
            table: "TaskAttempts",
            type: "int",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "PatchSha256",
            table: "TaskAttempts",
            type: "nvarchar(64)",
            maxLength: 64,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "PromptVersion",
            table: "TaskAttempts",
            type: "nvarchar(50)",
            maxLength: 50,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Provider",
            table: "TaskAttempts",
            type: "nvarchar(50)",
            maxLength: 50,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ProviderRequestId",
            table: "TaskAttempts",
            type: "nvarchar(200)",
            maxLength: 200,
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "ToolStepCount",
            table: "TaskAttempts",
            type: "int",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<string>(
            name: "ValidationSummaryJson",
            table: "TaskAttempts",
            type: "nvarchar(max)",
            maxLength: 16000,
            nullable: false,
            defaultValue: "[]");

        migrationBuilder.AddColumn<string>(
            name: "FindingsJson",
            table: "ReviewDecisions",
            type: "nvarchar(max)",
            maxLength: 16000,
            nullable: false,
            defaultValue: "[]");

        migrationBuilder.AddColumn<int>(
            name: "InputTokenCount",
            table: "ReviewDecisions",
            type: "int",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Model",
            table: "ReviewDecisions",
            type: "nvarchar(300)",
            maxLength: 300,
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "OutputTokenCount",
            table: "ReviewDecisions",
            type: "int",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "PromptVersion",
            table: "ReviewDecisions",
            type: "nvarchar(50)",
            maxLength: 50,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Provider",
            table: "ReviewDecisions",
            type: "nvarchar(50)",
            maxLength: 50,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ProviderRequestId",
            table: "ReviewDecisions",
            type: "nvarchar(200)",
            maxLength: 200,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ReviewedPatchSha256",
            table: "ReviewDecisions",
            type: "nvarchar(64)",
            maxLength: 64,
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "CoderModelOverrideId",
            table: "PlannedTasks",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "ReviewerModelOverrideId",
            table: "PlannedTasks",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "ExecutionClaimExpiresAtUtc",
            table: "PipelineRuns",
            type: "datetimeoffset",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "ExecutionClaimId",
            table: "PipelineRuns",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "ExecutionClaimedAtUtc",
            table: "PipelineRuns",
            type: "datetimeoffset",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "ExecutionClaimedTaskId",
            table: "PipelineRuns",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ExecutionWorkerId",
            table: "PipelineRuns",
            type: "nvarchar(200)",
            maxLength: 200,
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "PlannedTaskId",
            table: "ModelSelectionDecisions",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "TaskAttemptId",
            table: "ModelSelectionDecisions",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_TaskAttempts_Status",
            table: "TaskAttempts",
            column: "Status");

        migrationBuilder.CreateIndex(
            name: "IX_ReviewDecisions_PlannedTaskId_IsCurrent",
            table: "ReviewDecisions",
            columns: new[] { "PlannedTaskId", "IsCurrent" });

        migrationBuilder.CreateIndex(
            name: "IX_PipelineRuns_Status_ExecutionClaimExpiresAtUtc",
            table: "PipelineRuns",
            columns: new[] { "Status", "ExecutionClaimExpiresAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_ModelSelectionDecisions_PlannedTaskId_TaskAttemptId_Role",
            table: "ModelSelectionDecisions",
            columns: new[] { "PlannedTaskId", "TaskAttemptId", "Role" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_TaskAttempts_Status",
            table: "TaskAttempts");

        migrationBuilder.DropIndex(
            name: "IX_ReviewDecisions_PlannedTaskId_IsCurrent",
            table: "ReviewDecisions");

        migrationBuilder.DropIndex(
            name: "IX_PipelineRuns_Status_ExecutionClaimExpiresAtUtc",
            table: "PipelineRuns");

        migrationBuilder.DropIndex(
            name: "IX_ModelSelectionDecisions_PlannedTaskId_TaskAttemptId_Role",
            table: "ModelSelectionDecisions");

        migrationBuilder.DropColumn(
            name: "ChangedFilesJson",
            table: "TaskAttempts");

        migrationBuilder.DropColumn(
            name: "FailureCode",
            table: "TaskAttempts");

        migrationBuilder.DropColumn(
            name: "InputTokenCount",
            table: "TaskAttempts");

        migrationBuilder.DropColumn(
            name: "Model",
            table: "TaskAttempts");

        migrationBuilder.DropColumn(
            name: "OutputTokenCount",
            table: "TaskAttempts");

        migrationBuilder.DropColumn(
            name: "PatchSha256",
            table: "TaskAttempts");

        migrationBuilder.DropColumn(
            name: "PromptVersion",
            table: "TaskAttempts");

        migrationBuilder.DropColumn(
            name: "Provider",
            table: "TaskAttempts");

        migrationBuilder.DropColumn(
            name: "ProviderRequestId",
            table: "TaskAttempts");

        migrationBuilder.DropColumn(
            name: "ToolStepCount",
            table: "TaskAttempts");

        migrationBuilder.DropColumn(
            name: "ValidationSummaryJson",
            table: "TaskAttempts");

        migrationBuilder.DropColumn(
            name: "FindingsJson",
            table: "ReviewDecisions");

        migrationBuilder.DropColumn(
            name: "InputTokenCount",
            table: "ReviewDecisions");

        migrationBuilder.DropColumn(
            name: "Model",
            table: "ReviewDecisions");

        migrationBuilder.DropColumn(
            name: "OutputTokenCount",
            table: "ReviewDecisions");

        migrationBuilder.DropColumn(
            name: "PromptVersion",
            table: "ReviewDecisions");

        migrationBuilder.DropColumn(
            name: "Provider",
            table: "ReviewDecisions");

        migrationBuilder.DropColumn(
            name: "ProviderRequestId",
            table: "ReviewDecisions");

        migrationBuilder.DropColumn(
            name: "ReviewedPatchSha256",
            table: "ReviewDecisions");

        migrationBuilder.DropColumn(
            name: "CoderModelOverrideId",
            table: "PlannedTasks");

        migrationBuilder.DropColumn(
            name: "ReviewerModelOverrideId",
            table: "PlannedTasks");

        migrationBuilder.DropColumn(
            name: "ExecutionClaimExpiresAtUtc",
            table: "PipelineRuns");

        migrationBuilder.DropColumn(
            name: "ExecutionClaimId",
            table: "PipelineRuns");

        migrationBuilder.DropColumn(
            name: "ExecutionClaimedAtUtc",
            table: "PipelineRuns");

        migrationBuilder.DropColumn(
            name: "ExecutionClaimedTaskId",
            table: "PipelineRuns");

        migrationBuilder.DropColumn(
            name: "ExecutionWorkerId",
            table: "PipelineRuns");

        migrationBuilder.DropColumn(
            name: "PlannedTaskId",
            table: "ModelSelectionDecisions");

        migrationBuilder.DropColumn(
            name: "TaskAttemptId",
            table: "ModelSelectionDecisions");

        migrationBuilder.AlterColumn<string>(
            name: "PatchArtifactReference",
            table: "TaskAttempts",
            type: "nvarchar(max)",
            nullable: true,
            oldClrType: typeof(string),
            oldType: "nvarchar(500)",
            oldMaxLength: 500,
            oldNullable: true);

        migrationBuilder.RenameColumn(
            name: "PatchArtifactReference",
            newName: "OutputReference",
            table: "TaskAttempts");
    }
}
