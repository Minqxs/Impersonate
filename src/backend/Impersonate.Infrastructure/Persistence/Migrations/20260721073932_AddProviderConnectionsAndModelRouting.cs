using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Impersonate.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddProviderConnectionsAndModelRouting : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "AiProviderConnections",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ProviderType = table.Column<int>(type: "int", nullable: false),
                DisplayName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                Status = table.Column<int>(type: "int", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                LastValidatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                LastModelSyncAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                LastFailureCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                LastSafeFailureMessage = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AiProviderConnections", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "ModelSelectionDecisions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                PipelineRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                Role = table.Column<int>(type: "int", nullable: false),
                ProviderConnectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                DiscoveredModelId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                Provider = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                Model = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                SelectionSource = table.Column<int>(type: "int", nullable: false),
                Score = table.Column<int>(type: "int", nullable: false),
                TaskProfileJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                Explanation = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                CandidateSummaryJson = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: false),
                EscalatedFromDecisionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ModelSelectionDecisions", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "ProjectAiRoutingPolicies",
            columns: table => new
            {
                ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CostPreference = table.Column<int>(type: "int", nullable: false),
                LatencyPreference = table.Column<int>(type: "int", nullable: false),
                AllowPreviewModels = table.Column<bool>(type: "bit", nullable: false),
                AllowAutomaticEscalation = table.Column<bool>(type: "bit", nullable: false),
                MaximumEscalationCount = table.Column<int>(type: "int", nullable: false),
                PreferredProvider = table.Column<int>(type: "int", nullable: true),
                FixedModelOverrideId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                AllowedProvidersJson = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                BlockedProvidersJson = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ProjectAiRoutingPolicies", x => x.ProjectId);
            });

        migrationBuilder.CreateTable(
            name: "ProviderCredentialSecrets",
            columns: table => new
            {
                ConnectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ProtectedPayload = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ProviderCredentialSecrets", x => x.ConnectionId);
            });

        migrationBuilder.CreateTable(
            name: "DiscoveredModels",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ProviderConnectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ProviderType = table.Column<int>(type: "int", nullable: false),
                ProviderModelId = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                DisplayName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                LifecycleStatus = table.Column<int>(type: "int", nullable: false),
                DiscoveredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                LastSeenAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                IsAvailable = table.Column<bool>(type: "bit", nullable: false),
                CapabilitySource = table.Column<int>(type: "int", nullable: false),
                CapabilitiesJson = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                ContextWindowSize = table.Column<int>(type: "int", nullable: true),
                MaximumOutputSize = table.Column<int>(type: "int", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_DiscoveredModels", x => x.Id);
                table.ForeignKey(
                    name: "FK_DiscoveredModels_AiProviderConnections_ProviderConnectionId",
                    column: x => x.ProviderConnectionId,
                    principalTable: "AiProviderConnections",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_DiscoveredModels_ProviderConnectionId_ProviderModelId",
            table: "DiscoveredModels",
            columns: new[] { "ProviderConnectionId", "ProviderModelId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_ModelSelectionDecisions_ProjectId_PipelineRunId",
            table: "ModelSelectionDecisions",
            columns: new[] { "ProjectId", "PipelineRunId" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "DiscoveredModels");

        migrationBuilder.DropTable(
            name: "ModelSelectionDecisions");

        migrationBuilder.DropTable(
            name: "ProjectAiRoutingPolicies");

        migrationBuilder.DropTable(
            name: "ProviderCredentialSecrets");

        migrationBuilder.DropTable(
            name: "AiProviderConnections");
    }
}
