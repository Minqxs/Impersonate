using Impersonate.Domain.Ai;

namespace Impersonate.Application.Ai;

public sealed record ProviderCredentialReadResult(ProviderCredentialReadStatus Status, ProviderCredential? Credential, string? SafeFailureCode, string? SafeFailureMessage);
