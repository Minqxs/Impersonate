using Impersonate.Domain.Ai;

namespace Impersonate.Application.Planning;

public sealed record PlannerAgentRequest(Guid ProjectId, string ProjectName, string? ProjectDescription, string RepositoryUrl, string DefaultBranch, string FeatureRequest, int MaximumTasks, string PromptVersion, PlannerCorrectionContext? CorrectionContext = null, Guid? ProviderConnectionId = null, ProviderType? RoutedProvider = null, string? RoutedModel = null, PlanningRepositoryContext? RepositoryContext = null);
