using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Impersonate.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AlignRunDeliveryClaimIndex : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_RunDeliveries_Status_ClaimExpiresAtUtc_CreatedAtUtc",
            table: "RunDeliveries");

        migrationBuilder.CreateIndex(
            name: "IX_RunDeliveries_Status_ClaimExpiresAtUtc_UpdatedAtUtc_Id",
            table: "RunDeliveries",
            columns: new[] { "Status", "ClaimExpiresAtUtc", "UpdatedAtUtc", "Id" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_RunDeliveries_Status_ClaimExpiresAtUtc_UpdatedAtUtc_Id",
            table: "RunDeliveries");

        migrationBuilder.CreateIndex(
            name: "IX_RunDeliveries_Status_ClaimExpiresAtUtc_CreatedAtUtc",
            table: "RunDeliveries",
            columns: new[] { "Status", "ClaimExpiresAtUtc", "CreatedAtUtc" });
    }
}
