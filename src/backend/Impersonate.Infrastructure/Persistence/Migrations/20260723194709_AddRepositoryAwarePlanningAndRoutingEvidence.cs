using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Impersonate.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddRepositoryAwarePlanningAndRoutingEvidence : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "PreferReviewerDiversity",
            table: "ProjectAiRoutingPolicies",
            type: "bit",
            nullable: false,
            defaultValue: true);

        migrationBuilder.AddColumn<int>(
            name: "ReviewerDiversityWeight",
            table: "ProjectAiRoutingPolicies",
            type: "int",
            nullable: false,
            defaultValue: 12);

        migrationBuilder.AddColumn<string>(
            name: "AffectedAreasJson",
            table: "PlannedTasks",
            type: "nvarchar(max)",
            maxLength: 8000,
            nullable: false,
            defaultValue: "[]");

        migrationBuilder.AddColumn<string>(
            name: "ChangeType",
            table: "PlannedTasks",
            type: "nvarchar(100)",
            maxLength: 100,
            nullable: false,
            defaultValue: "Unknown");

        migrationBuilder.AddColumn<string>(
            name: "ConflictRisk",
            table: "PlannedTasks",
            type: "nvarchar(30)",
            maxLength: 30,
            nullable: false,
            defaultValue: "Unknown");

        migrationBuilder.AddColumn<string>(
            name: "DependsOnTaskIdsJson",
            table: "PlannedTasks",
            type: "nvarchar(max)",
            maxLength: 8000,
            nullable: false,
            defaultValue: "[]");

        migrationBuilder.AddColumn<bool>(
            name: "EstablishesSharedContract",
            table: "PlannedTasks",
            type: "bit",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<string>(
            name: "ExecutionReason",
            table: "PlannedTasks",
            type: "nvarchar(1000)",
            maxLength: 1000,
            nullable: true);

        migrationBuilder.AddColumn<bool>(
            name: "OrderAdjusted",
            table: "PlannedTasks",
            type: "bit",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<string>(
            name: "OrderAdjustmentReason",
            table: "PlannedTasks",
            type: "nvarchar(1000)",
            maxLength: 1000,
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "OriginalPlannerSequence",
            table: "PlannedTasks",
            type: "int",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<string>(
            name: "RepositoryEvidenceJson",
            table: "PlannedTasks",
            type: "nvarchar(max)",
            maxLength: 16000,
            nullable: false,
            defaultValue: "[]");

        migrationBuilder.AddColumn<string>(
            name: "Risk",
            table: "PlannedTasks",
            type: "nvarchar(30)",
            maxLength: 30,
            nullable: false,
            defaultValue: "Unknown");

        migrationBuilder.AddColumn<string>(
            name: "PlanningContextArtifactReference",
            table: "PipelineRuns",
            type: "nvarchar(500)",
            maxLength: 500,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "PlanningContextSummary",
            table: "PipelineRuns",
            type: "nvarchar(2000)",
            maxLength: 2000,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "PlanningFrameworksJson",
            table: "PipelineRuns",
            type: "nvarchar(4000)",
            maxLength: 4000,
            nullable: false,
            defaultValue: "[]");

        migrationBuilder.AddColumn<string>(
            name: "PlanningLanguagesJson",
            table: "PipelineRuns",
            type: "nvarchar(4000)",
            maxLength: 4000,
            nullable: false,
            defaultValue: "[]");

        migrationBuilder.AlterColumn<string>(
            name: "TaskProfileJson",
            table: "ModelSelectionDecisions",
            type: "nvarchar(max)",
            maxLength: 12000,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "nvarchar(4000)",
            oldMaxLength: 4000);

        migrationBuilder.AlterColumn<string>(
            name: "Explanation",
            table: "ModelSelectionDecisions",
            type: "nvarchar(4000)",
            maxLength: 4000,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "nvarchar(2000)",
            oldMaxLength: 2000);

        migrationBuilder.AddColumn<string>(
            name: "MetadataVersion",
            table: "ModelSelectionDecisions",
            type: "nvarchar(100)",
            maxLength: 100,
            nullable: false,
            defaultValue: "catalog-2026-07-v1");

        migrationBuilder.AddColumn<string>(
            name: "ScoreBreakdownJson",
            table: "ModelSelectionDecisions",
            type: "nvarchar(max)",
            maxLength: 12000,
            nullable: false,
            defaultValue: "[]");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "PreferReviewerDiversity",
            table: "ProjectAiRoutingPolicies");

        migrationBuilder.DropColumn(
            name: "ReviewerDiversityWeight",
            table: "ProjectAiRoutingPolicies");

        migrationBuilder.DropColumn(
            name: "AffectedAreasJson",
            table: "PlannedTasks");

        migrationBuilder.DropColumn(
            name: "ChangeType",
            table: "PlannedTasks");

        migrationBuilder.DropColumn(
            name: "ConflictRisk",
            table: "PlannedTasks");

        migrationBuilder.DropColumn(
            name: "DependsOnTaskIdsJson",
            table: "PlannedTasks");

        migrationBuilder.DropColumn(
            name: "EstablishesSharedContract",
            table: "PlannedTasks");

        migrationBuilder.DropColumn(
            name: "ExecutionReason",
            table: "PlannedTasks");

        migrationBuilder.DropColumn(
            name: "OrderAdjusted",
            table: "PlannedTasks");

        migrationBuilder.DropColumn(
            name: "OrderAdjustmentReason",
            table: "PlannedTasks");

        migrationBuilder.DropColumn(
            name: "OriginalPlannerSequence",
            table: "PlannedTasks");

        migrationBuilder.DropColumn(
            name: "RepositoryEvidenceJson",
            table: "PlannedTasks");

        migrationBuilder.DropColumn(
            name: "Risk",
            table: "PlannedTasks");

        migrationBuilder.DropColumn(
            name: "PlanningContextArtifactReference",
            table: "PipelineRuns");

        migrationBuilder.DropColumn(
            name: "PlanningContextSummary",
            table: "PipelineRuns");

        migrationBuilder.DropColumn(
            name: "PlanningFrameworksJson",
            table: "PipelineRuns");

        migrationBuilder.DropColumn(
            name: "PlanningLanguagesJson",
            table: "PipelineRuns");

        migrationBuilder.DropColumn(
            name: "MetadataVersion",
            table: "ModelSelectionDecisions");

        migrationBuilder.DropColumn(
            name: "ScoreBreakdownJson",
            table: "ModelSelectionDecisions");

        migrationBuilder.AlterColumn<string>(
            name: "TaskProfileJson",
            table: "ModelSelectionDecisions",
            type: "nvarchar(4000)",
            maxLength: 4000,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "nvarchar(max)",
            oldMaxLength: 12000);

        migrationBuilder.AlterColumn<string>(
            name: "Explanation",
            table: "ModelSelectionDecisions",
            type: "nvarchar(2000)",
            maxLength: 2000,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "nvarchar(4000)",
            oldMaxLength: 4000);
    }
}
