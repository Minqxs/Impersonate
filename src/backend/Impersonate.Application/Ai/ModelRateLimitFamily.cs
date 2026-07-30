using Impersonate.Domain.Ai;

namespace Impersonate.Application.Ai;

public static class ModelRateLimitFamily
{
    public static string Get(ProviderType provider, string model) => provider == ProviderType.OpenAI ? System.Text.RegularExpressions.Regex.Replace(model, @"-\d{4}-\d{2}-\d{2}$", string.Empty, System.Text.RegularExpressions.RegexOptions.CultureInvariant).ToLowerInvariant() : model.ToLowerInvariant();
    public static bool Matches(ProviderType provider, string left, string right) => Get(provider, left) == Get(provider, right);
}
