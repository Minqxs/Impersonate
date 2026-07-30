using Impersonate.Domain.Ai;

namespace Impersonate.Application.Ai;

public sealed class ProviderCredentialStorageException : Exception
{
    public ProviderCredentialStorageException() : base("The provider credential could not be stored safely.")
    {
    }
}
