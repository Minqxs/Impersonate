using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Impersonate.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCoderSpendGuardrails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ConsecutiveReadOnlyRounds",
                table: "ExecutionInvocations",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MaximumSingleRequestInput",
                table: "ExecutionInvocations",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "NoProgressCorrectionCount",
                table: "ExecutionInvocations",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PaidProviderRequestCount",
                table: "ExecutionInvocations",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ProviderIncompleteReason",
                table: "ExecutionInvocations",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderResponseStatus",
                table: "ExecutionInvocations",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProviderRoundTripCount",
                table: "ExecutionInvocations",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "StructuredOutputRepairCount",
                table: "ExecutionInvocations",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConsecutiveReadOnlyRounds",
                table: "ExecutionInvocations");

            migrationBuilder.DropColumn(
                name: "MaximumSingleRequestInput",
                table: "ExecutionInvocations");

            migrationBuilder.DropColumn(
                name: "NoProgressCorrectionCount",
                table: "ExecutionInvocations");

            migrationBuilder.DropColumn(
                name: "PaidProviderRequestCount",
                table: "ExecutionInvocations");

            migrationBuilder.DropColumn(
                name: "ProviderIncompleteReason",
                table: "ExecutionInvocations");

            migrationBuilder.DropColumn(
                name: "ProviderResponseStatus",
                table: "ExecutionInvocations");

            migrationBuilder.DropColumn(
                name: "ProviderRoundTripCount",
                table: "ExecutionInvocations");

            migrationBuilder.DropColumn(
                name: "StructuredOutputRepairCount",
                table: "ExecutionInvocations");
        }
    }
}
