using Impersonate.Domain.Ai;

namespace Impersonate.Application.Planning;

public static class PlannerRequestPayload
{
    public static string Build(PlannerAgentRequest request) => System.Text.Json.JsonSerializer.Serialize(new { project = new { request.ProjectName, request.ProjectDescription, request.DefaultBranch }, request.FeatureRequest, constraints = new { request.MaximumTasks, repositoryInspectionAvailable = request.RepositoryContext is not null }, allowedRepositoryEvidencePaths = request.RepositoryContext?.EvidencePaths.Order(StringComparer.Ordinal).ToList() ?? [], repositoryContext = request.RepositoryContext is null ? null : new { request.RepositoryContext.Tree, request.RepositoryContext.RelevantFiles, request.RepositoryContext.Languages, request.RepositoryContext.Frameworks, request.RepositoryContext.Layers, request.RepositoryContext.TestLocations, request.RepositoryContext.MigrationLocations, request.RepositoryContext.Summary }, correctionContext = request.CorrectionContext }, new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase });
}
