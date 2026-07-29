using Impersonate.Application.Ai;
using Impersonate.Domain.Pipelines;
using Impersonate.Domain.Projects;

namespace Impersonate.Application.Pipelines;

public sealed record ModelPreviewDto(bool Ready, Guid? ModelId, string? Provider, string? Model, string? SelectionSource, string? Explanation, string? Blocker, int TotalScore = 0, IReadOnlyList<ScoreComponent>? ScoreBreakdown = null, string? MetadataVersion = null, TaskProfile? Profile = null, IReadOnlyList<SelectedModel>? Alternatives = null);
