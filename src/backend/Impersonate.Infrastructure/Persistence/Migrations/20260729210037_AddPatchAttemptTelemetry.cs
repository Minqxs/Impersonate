using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Impersonate.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddPatchAttemptTelemetry : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "FailedPatchCount",
            table: "ExecutionInvocations",
            type: "int",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<string>(
            name: "LastPatchFailureCode",
            table: "ExecutionInvocations",
            type: "nvarchar(100)",
            maxLength: 100,
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "PatchAttemptCount",
            table: "ExecutionInvocations",
            type: "int",
            nullable: false,
            defaultValue: 0);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "FailedPatchCount",
            table: "ExecutionInvocations");

        migrationBuilder.DropColumn(
            name: "LastPatchFailureCode",
            table: "ExecutionInvocations");

        migrationBuilder.DropColumn(
            name: "PatchAttemptCount",
            table: "ExecutionInvocations");
    }
}
