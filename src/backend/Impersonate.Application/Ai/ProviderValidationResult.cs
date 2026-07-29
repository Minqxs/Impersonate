using Impersonate.Domain.Ai;

namespace Impersonate.Application.Ai;

public sealed record ProviderValidationResult(bool Succeeded, bool InvalidCredentials, string? FailureCode, string SafeMessage);
