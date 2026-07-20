using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Impersonate.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPipelineAndLoopFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PipelineRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FeatureRequest = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CancelledAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    FailureReason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StopReason = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PipelineRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PipelineRuns_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LoopRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PipelineRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LoopDefinitionId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LoopDefinitionVersion = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CurrentStage = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    MaximumRevisionAttempts = table.Column<int>(type: "int", nullable: false),
                    ContinueOnTaskFailure = table.Column<bool>(type: "bit", nullable: false),
                    RetryCount = table.Column<int>(type: "int", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    StopReason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FailureReason = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoopRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LoopRuns_PipelineRuns_PipelineRunId",
                        column: x => x.PipelineRunId,
                        principalTable: "PipelineRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PipelineRunEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PipelineRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlannedTaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EventType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PreviousState = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    NewState = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PipelineRunEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PipelineRunEvents_PipelineRuns_PipelineRunId",
                        column: x => x.PipelineRunId,
                        principalTable: "PipelineRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PlannedTasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PipelineRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    RevisionCount = table.Column<int>(type: "int", nullable: false),
                    MaximumRevisionAttempts = table.Column<int>(type: "int", nullable: false),
                    SkipReason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FailureReason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlannedTasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlannedTasks_PipelineRuns_PipelineRunId",
                        column: x => x.PipelineRunId,
                        principalTable: "PipelineRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TaskAttempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlannedTaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AttemptNumber = table.Column<int>(type: "int", nullable: false),
                    AttemptType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Summary = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    FailureReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    OutputReference = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskAttempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaskAttempts_PlannedTasks_PlannedTaskId",
                        column: x => x.PlannedTaskId,
                        principalTable: "PlannedTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReviewDecisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlannedTaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TaskAttemptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Decision = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Feedback = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IsCurrent = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReviewDecisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReviewDecisions_PlannedTasks_PlannedTaskId",
                        column: x => x.PlannedTaskId,
                        principalTable: "PlannedTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReviewDecisions_TaskAttempts_TaskAttemptId",
                        column: x => x.TaskAttemptId,
                        principalTable: "TaskAttempts",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_LoopRuns_PipelineRunId",
                table: "LoopRuns",
                column: "PipelineRunId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PipelineRunEvents_PipelineRunId_Sequence",
                table: "PipelineRunEvents",
                columns: new[] { "PipelineRunId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PipelineRunEvents_ProjectId_CreatedAtUtc",
                table: "PipelineRunEvents",
                columns: new[] { "ProjectId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PipelineRuns_ProjectId_CreatedAtUtc",
                table: "PipelineRuns",
                columns: new[] { "ProjectId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PipelineRuns_ProjectId_Status",
                table: "PipelineRuns",
                columns: new[] { "ProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PlannedTasks_PipelineRunId_Sequence",
                table: "PlannedTasks",
                columns: new[] { "PipelineRunId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReviewDecisions_PlannedTaskId_CreatedAtUtc",
                table: "ReviewDecisions",
                columns: new[] { "PlannedTaskId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ReviewDecisions_TaskAttemptId",
                table: "ReviewDecisions",
                column: "TaskAttemptId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskAttempts_PlannedTaskId_AttemptNumber",
                table: "TaskAttempts",
                columns: new[] { "PlannedTaskId", "AttemptNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LoopRuns");

            migrationBuilder.DropTable(
                name: "PipelineRunEvents");

            migrationBuilder.DropTable(
                name: "ReviewDecisions");

            migrationBuilder.DropTable(
                name: "TaskAttempts");

            migrationBuilder.DropTable(
                name: "PlannedTasks");

            migrationBuilder.DropTable(
                name: "PipelineRuns");
        }
    }
}
