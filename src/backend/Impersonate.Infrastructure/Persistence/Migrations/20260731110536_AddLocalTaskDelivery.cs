using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Impersonate.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddLocalTaskDelivery : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "ClaimExpiresAtUtc",
            table: "TaskDeliveries",
            type: "datetimeoffset",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "ClaimId",
            table: "TaskDeliveries",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ClaimOwner",
            table: "TaskDeliveries",
            type: "nvarchar(200)",
            maxLength: 200,
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "ClaimedAtUtc",
            table: "TaskDeliveries",
            type: "datetimeoffset",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "DeliveryBaseCommitSha",
            table: "TaskDeliveries",
            type: "nvarchar(64)",
            maxLength: 64,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ValidationSummaryJson",
            table: "TaskDeliveries",
            type: "nvarchar(max)",
            maxLength: 16000,
            nullable: false,
            defaultValue: "[]");

        migrationBuilder.CreateIndex(
            name: "IX_TaskDeliveries_Status_ClaimExpiresAtUtc_TaskSequence",
            table: "TaskDeliveries",
            columns: new[] { "Status", "ClaimExpiresAtUtc", "TaskSequence" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_TaskDeliveries_Status_ClaimExpiresAtUtc_TaskSequence",
            table: "TaskDeliveries");

        migrationBuilder.DropColumn(
            name: "ClaimExpiresAtUtc",
            table: "TaskDeliveries");

        migrationBuilder.DropColumn(
            name: "ClaimId",
            table: "TaskDeliveries");

        migrationBuilder.DropColumn(
            name: "ClaimOwner",
            table: "TaskDeliveries");

        migrationBuilder.DropColumn(
            name: "ClaimedAtUtc",
            table: "TaskDeliveries");

        migrationBuilder.DropColumn(
            name: "DeliveryBaseCommitSha",
            table: "TaskDeliveries");

        migrationBuilder.DropColumn(
            name: "ValidationSummaryJson",
            table: "TaskDeliveries");
    }
}
