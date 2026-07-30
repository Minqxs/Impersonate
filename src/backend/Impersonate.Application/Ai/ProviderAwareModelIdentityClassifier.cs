using System.Text.Json;
using Impersonate.Domain.Ai;

namespace Impersonate.Application.Ai;

internal sealed class ProviderAwareModelIdentityClassifier : IModelIdentityClassifier
{
    private static readonly System.Text.RegularExpressions.Regex OpenAi = new(@"^(?<base>gpt-(?<generation>4\.1|5(?:\.\d+)?))(?<variant>-mini|-nano|-pro|-codex|-sol|-terra|-luna)?(?<snapshot>-\d{4}-\d{2}-\d{2})?$", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant | System.Text.RegularExpressions.RegexOptions.Compiled);
    public ModelIdentity Classify(ProviderType provider, string modelId)
    {
        var id = (modelId ?? string.Empty).Trim().ToLowerInvariant();
        if (id.Length == 0 || id.Any(char.IsWhiteSpace))
            return new(provider, "unknown", "unknown", null, ModelVariant.Unknown, ProviderEndpoint.Unknown, "unknown", false, true);
        if (provider == ProviderType.OpenAI)
        {
            var m = OpenAi.Match(id);
            if (m.Success)
            {
                var root = m.Groups["base"].Value;
                var suffix = m.Groups["variant"].Value;
                var variant = suffix switch
                {
                    "-mini" => ModelVariant.Mini,
                    "-nano" => ModelVariant.Nano,
                    "-pro" => ModelVariant.Pro,
                    "-codex" => ModelVariant.Coding,
                    "-sol" => ModelVariant.Pro,
                    "-terra" => ModelVariant.Balanced,
                    "-luna" => ModelVariant.Mini,
                    _ => ModelVariant.Flagship
                };
                var canonical = root + suffix;
                var endpoint = ProviderEndpoint.Responses;
                return new(provider, root, canonical, m.Groups["snapshot"].Success ? m.Groups["snapshot"].Value[1..] : null, variant, endpoint, canonical, true);
            }

            if (id.StartsWith("o3", StringComparison.Ordinal) || id.StartsWith("o4", StringComparison.Ordinal))
                return new(provider, id.Split('-')[0], System.Text.RegularExpressions.Regex.Replace(id, @"-\d{4}-\d{2}-\d{2}$", ""), null, id.Contains("mini") ? ModelVariant.Mini : ModelVariant.Flagship, ProviderEndpoint.Responses, System.Text.RegularExpressions.Regex.Replace(id, @"-\d{4}-\d{2}-\d{2}$", ""), true);
        }

        if (provider == ProviderType.Anthropic && id.StartsWith("claude-", StringComparison.Ordinal))
            return new(provider, id.Contains("haiku") ? "claude-haiku" : "claude-sonnet", System.Text.RegularExpressions.Regex.Replace(id, @"-\d{8}$", ""), null, id.Contains("haiku") ? ModelVariant.Mini : ModelVariant.Flagship, ProviderEndpoint.Messages, System.Text.RegularExpressions.Regex.Replace(id, @"-\d{8}$", ""), true);
        if (provider == ProviderType.GoogleGemini && id.StartsWith("gemini-", StringComparison.Ordinal))
            return new(provider, id.Contains("flash") ? "gemini-flash" : "gemini-pro", id, null, id.Contains("flash") ? ModelVariant.Balanced : ModelVariant.Pro, ProviderEndpoint.GenerateContent, id, true);
        return new(provider, "unknown", id, null, ModelVariant.Unknown, ProviderEndpoint.Unknown, id, false);
    }
}
