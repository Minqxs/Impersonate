using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Impersonate.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskPatchCompositionTelemetry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ComposedTreeFingerprint",
                table: "TaskAttempts",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompositionStatus",
                table: "TaskAttempts",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CurrentRevisionPatchApplied",
                table: "TaskAttempts",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "DependencyPatchCount",
                table: "TaskAttempts",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "DependencyTaskIdsJson",
                table: "TaskAttempts",
                type: "nvarchar(max)",
                maxLength: 16000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "IncrementalPatchFileCount",
                table: "TaskAttempts",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "SourceBaseCommitSha",
                table: "TaskAttempts",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ComposedTreeFingerprint",
                table: "TaskAttempts");

            migrationBuilder.DropColumn(
                name: "CompositionStatus",
                table: "TaskAttempts");

            migrationBuilder.DropColumn(
                name: "CurrentRevisionPatchApplied",
                table: "TaskAttempts");

            migrationBuilder.DropColumn(
                name: "DependencyPatchCount",
                table: "TaskAttempts");

            migrationBuilder.DropColumn(
                name: "DependencyTaskIdsJson",
                table: "TaskAttempts");

            migrationBuilder.DropColumn(
                name: "IncrementalPatchFileCount",
                table: "TaskAttempts");

            migrationBuilder.DropColumn(
                name: "SourceBaseCommitSha",
                table: "TaskAttempts");
        }
    }
}
