using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Impersonate.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddRunDeliveryReviews : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "RunDeliveryReviews",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                RunDeliveryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                AttemptNumber = table.Column<int>(type: "int", nullable: false),
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
                table.PrimaryKey("PK_RunDeliveryReviews", x => x.Id);
                table.ForeignKey(
                    name: "FK_RunDeliveryReviews_RunDeliveries_RunDeliveryId",
                    column: x => x.RunDeliveryId,
                    principalTable: "RunDeliveries",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_RunDeliveryReviews_RunDeliveryId_AttemptNumber",
            table: "RunDeliveryReviews",
            columns: new[] { "RunDeliveryId", "AttemptNumber" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_RunDeliveryReviews_RunDeliveryId_ExactHeadSha",
            table: "RunDeliveryReviews",
            columns: new[] { "RunDeliveryId", "ExactHeadSha" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "RunDeliveryReviews");
    }
}
