using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Impersonate.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddTaskDeliveryPush : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "PushedAtUtc",
            table: "TaskDeliveries",
            type: "datetimeoffset",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "PushedCommitSha",
            table: "TaskDeliveries",
            type: "nvarchar(64)",
            maxLength: 64,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "RecoveryStatus",
            table: "TaskDeliveries",
            type: "nvarchar(40)",
            maxLength: 40,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "RemoteBranchName",
            table: "TaskDeliveries",
            type: "nvarchar(250)",
            maxLength: 250,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "RemoteName",
            table: "TaskDeliveries",
            type: "nvarchar(50)",
            maxLength: 50,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "RemoteRepository",
            table: "TaskDeliveries",
            type: "nvarchar(300)",
            maxLength: 300,
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_TaskDeliveries_RemoteRepository_RemoteBranchName",
            table: "TaskDeliveries",
            columns: new[] { "RemoteRepository", "RemoteBranchName" },
            unique: true,
            filter: "[RemoteRepository] IS NOT NULL AND [RemoteBranchName] IS NOT NULL");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_TaskDeliveries_RemoteRepository_RemoteBranchName",
            table: "TaskDeliveries");

        migrationBuilder.DropColumn(
            name: "PushedAtUtc",
            table: "TaskDeliveries");

        migrationBuilder.DropColumn(
            name: "PushedCommitSha",
            table: "TaskDeliveries");

        migrationBuilder.DropColumn(
            name: "RecoveryStatus",
            table: "TaskDeliveries");

        migrationBuilder.DropColumn(
            name: "RemoteBranchName",
            table: "TaskDeliveries");

        migrationBuilder.DropColumn(
            name: "RemoteName",
            table: "TaskDeliveries");

        migrationBuilder.DropColumn(
            name: "RemoteRepository",
            table: "TaskDeliveries");
    }
}
