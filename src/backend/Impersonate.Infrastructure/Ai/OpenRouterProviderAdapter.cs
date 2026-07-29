using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Impersonate.Application.Ai;
using Impersonate.Application.Execution;
using Impersonate.Domain.Ai;
using Microsoft.Extensions.Options;

namespace Impersonate.Infrastructure.Ai;

internal sealed class OpenRouterProviderAdapter(HttpClient http, IOptions<ExecutionOptions>? options = null, ProviderCapacityCoordinator? coordinator = null, TimeProvider? clock = null) : OpenAiProviderAdapter(http, options, coordinator, clock)
{
    public override ProviderType ProviderType => ProviderType.OpenRouter;
}
