using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Impersonate.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddTaskDeliveryReviews : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "DeliveryRepairAttemptCount",
            table: "TaskDeliveries",
            type: "int",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<int>(
            name: "DeliveryReviewAttemptCount",
            table: "TaskDeliveries",
            type: "int",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.CreateTable(
            name: "TaskDeliveryReviews",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TaskDeliveryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ReviewAttemptNumber = table.Column<int>(type: "int", nullable: false),
                Provider = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                Model = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                ExactHeadSha = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                Decision = table.Column<int>(type: "int", nullable: false),
                Summary = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                Feedback = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                FindingsJson = table.Column<string>(type: "nvarchar(max)", maxLength: 16000, nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                SupersededAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TaskDeliveryReviews", x => x.Id);
                table.ForeignKey(
                    name: "FK_TaskDeliveryReviews_TaskDeliveries_TaskDeliveryId",
                    column: x => x.TaskDeliveryId,
                    principalTable: "TaskDeliveries",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_TaskDeliveryReviews_TaskDeliveryId_ExactHeadSha",
            table: "TaskDeliveryReviews",
            columns: new[] { "TaskDeliveryId", "ExactHeadSha" });

        migrationBuilder.CreateIndex(
            name: "IX_TaskDeliveryReviews_TaskDeliveryId_ReviewAttemptNumber",
            table: "TaskDeliveryReviews",
            columns: new[] { "TaskDeliveryId", "ReviewAttemptNumber" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "TaskDeliveryReviews");

        migrationBuilder.DropColumn(
            name: "DeliveryRepairAttemptCount",
            table: "TaskDeliveries");

        migrationBuilder.DropColumn(
            name: "DeliveryReviewAttemptCount",
            table: "TaskDeliveries");
    }
}
