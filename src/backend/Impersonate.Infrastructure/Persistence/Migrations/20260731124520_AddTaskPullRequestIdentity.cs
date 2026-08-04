using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Impersonate.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddTaskPullRequestIdentity : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "PullRequestBaseBranch",
            table: "TaskDeliveries",
            type: "nvarchar(200)",
            maxLength: 200,
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "PullRequestCreatedAtUtc",
            table: "TaskDeliveries",
            type: "datetimeoffset",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "PullRequestHeadBranch",
            table: "TaskDeliveries",
            type: "nvarchar(250)",
            maxLength: 250,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "PullRequestObservedHeadSha",
            table: "TaskDeliveries",
            type: "nvarchar(64)",
            maxLength: 64,
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "PullRequestBaseBranch",
            table: "TaskDeliveries");

        migrationBuilder.DropColumn(
            name: "PullRequestCreatedAtUtc",
            table: "TaskDeliveries");

        migrationBuilder.DropColumn(
            name: "PullRequestHeadBranch",
            table: "TaskDeliveries");

        migrationBuilder.DropColumn(
            name: "PullRequestObservedHeadSha",
            table: "TaskDeliveries");
    }
}
