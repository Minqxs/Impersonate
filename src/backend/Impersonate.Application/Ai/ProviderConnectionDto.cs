using Impersonate.Domain.Ai;

namespace Impersonate.Application.Ai;

public sealed record ProviderConnectionDto(Guid Id, ProviderType ProviderType, string DisplayName, ProviderConnectionStatus Status, DateTimeOffset? LastValidatedAtUtc, DateTimeOffset? LastModelSyncAtUtc, int AvailableModelCount, string? LastFailureCode, string? LastSafeFailureMessage);
