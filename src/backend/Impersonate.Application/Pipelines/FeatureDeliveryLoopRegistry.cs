using System.Text.Json;
using Impersonate.Application.Ai;
using Impersonate.Application.Execution;
using Impersonate.Application.Planning;
using Impersonate.Application.Projects;
using Impersonate.Domain.Ai;
using Impersonate.Domain.Pipelines;
using Impersonate.Domain.Projects;
using Microsoft.Extensions.Options;

namespace Impersonate.Application.Pipelines;

internal sealed class FeatureDeliveryLoopRegistry(IOptions<PipelineOptions> options) : ILoopDefinitionRegistry
{
    public LoopDefinition Get(string id, string? version = null)
    {
        if (id != "feature-delivery" || version is not null and not "1")
            throw new KeyNotFoundException("Loop definition was not found.");
        var o = options.Value;
        return new(id, "Feature Delivery", "1", Enum.GetValues<LoopStage>(), o.MaximumRevisionAttempts, o.ContinueOnTaskFailure);
    }
}
