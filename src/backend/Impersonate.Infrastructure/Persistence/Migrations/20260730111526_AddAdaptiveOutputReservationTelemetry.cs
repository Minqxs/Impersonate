using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Impersonate.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddAdaptiveOutputReservationTelemetry : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "LastRateLimitScope",
            table: "ExecutionInvocations",
            type: "nvarchar(max)",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "MaximumRequestedOutputReservation",
            table: "ExecutionInvocations",
            type: "int",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<string>(
            name: "OutputReservationReasonsJson",
            table: "ExecutionInvocations",
            type: "nvarchar(max)",
            nullable: false,
            defaultValue: "[]");

        migrationBuilder.AddColumn<long>(
            name: "ProviderCapacityWaitMilliseconds",
            table: "ExecutionInvocations",
            type: "bigint",
            nullable: false,
            defaultValue: 0L);

        migrationBuilder.AddColumn<bool>(
            name: "ProviderResetUsed",
            table: "ExecutionInvocations",
            type: "bit",
            nullable: false,
            defaultValue: false);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "LastRateLimitScope",
            table: "ExecutionInvocations");

        migrationBuilder.DropColumn(
            name: "MaximumRequestedOutputReservation",
            table: "ExecutionInvocations");

        migrationBuilder.DropColumn(
            name: "OutputReservationReasonsJson",
            table: "ExecutionInvocations");

        migrationBuilder.DropColumn(
            name: "ProviderCapacityWaitMilliseconds",
            table: "ExecutionInvocations");

        migrationBuilder.DropColumn(
            name: "ProviderResetUsed",
            table: "ExecutionInvocations");
    }
}
