using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Impersonate.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddCoderExecutionPhase : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "CurrentPhase",
            table: "ExecutionInvocations",
            type: "nvarchar(30)",
            maxLength: 30,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "RequestedProhibitedTool",
            table: "ExecutionInvocations",
            type: "nvarchar(50)",
            maxLength: 50,
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "CurrentPhase",
            table: "ExecutionInvocations");

        migrationBuilder.DropColumn(
            name: "RequestedProhibitedTool",
            table: "ExecutionInvocations");
    }
}
