using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Impersonate.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddExecutionInvocationTelemetry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExecutionInvocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TaskAttemptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    AgentRole = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Model = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    SelectionDecisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PromptVersion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ProviderRequestId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    InputTokenCount = table.Column<int>(type: "int", nullable: true),
                    OutputTokenCount = table.Column<int>(type: "int", nullable: true),
                    ResponseType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    ToolStepCount = table.Column<int>(type: "int", nullable: false),
                    SuccessfulReadCount = table.Column<int>(type: "int", nullable: false),
                    SuccessfulSearchCount = table.Column<int>(type: "int", nullable: false),
                    SuccessfulPatchCount = table.Column<int>(type: "int", nullable: false),
                    FallbackSequence = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FailureCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    FailureReason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExecutionInvocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExecutionInvocations_ModelSelectionDecisions_SelectionDecisionId",
                        column: x => x.SelectionDecisionId,
                        principalTable: "ModelSelectionDecisions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ExecutionInvocations_TaskAttempts_TaskAttemptId",
                        column: x => x.TaskAttemptId,
                        principalTable: "TaskAttempts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionInvocations_SelectionDecisionId",
                table: "ExecutionInvocations",
                column: "SelectionDecisionId");

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionInvocations_TaskAttemptId_Sequence",
                table: "ExecutionInvocations",
                columns: new[] { "TaskAttemptId", "Sequence" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExecutionInvocations");
        }
    }
}
