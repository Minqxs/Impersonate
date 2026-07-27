using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Impersonate.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddExecutionInfrastructureBlocking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "InfrastructureBlockedTaskId",
                table: "PipelineRuns",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InfrastructureFailureCode",
                table: "PipelineRuns",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InfrastructureFailureMessage",
                table: "PipelineRuns",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InfrastructureBlockedTaskId",
                table: "PipelineRuns");

            migrationBuilder.DropColumn(
                name: "InfrastructureFailureCode",
                table: "PipelineRuns");

            migrationBuilder.DropColumn(
                name: "InfrastructureFailureMessage",
                table: "PipelineRuns");
        }
    }
}
