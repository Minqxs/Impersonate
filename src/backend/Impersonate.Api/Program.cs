using System.Text.Json.Serialization;
using Impersonate.Application;
using Impersonate.Application.Ai;
using Impersonate.Application.Delivery;
using Impersonate.Application.Execution;
using Impersonate.Application.Pipelines;
using Impersonate.Application.Planning;
using Impersonate.Application.Projects;
using Impersonate.Application.Quality;
using Impersonate.Domain.Ai;
using Impersonate.Domain.Pipelines;
using Impersonate.Domain.Projects;
using Impersonate.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddScoped<ITaskControlService, TaskControlService>();
builder.Services.AddMemoryCache();
builder.Services.AddScoped<IProjectQualityService, ProjectQualityService>();
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);
builder.Services.AddHealthChecks();
builder.Services.AddOpenApi();
builder.Services.AddCors(options => options.AddPolicy("FrontendDevelopment", policy => policy.WithOrigins("http://localhost:5173").AllowAnyHeader().AllowAnyMethod()));
builder.Services.Configure<HostOptions>(options => options.ShutdownTimeout = TimeSpan.FromSeconds(15));
var app = builder.Build();
app.Lifetime.ApplicationStopping.Register(() => app.Logger.LogInformation("Impersonate API cancellation started."));
app.Lifetime.ApplicationStopped.Register(() => app.Logger.LogInformation("Impersonate API shutdown completed."));
app.Logger.LogInformation("Data Protection key ring: {DataProtectionKeyRingPath}", app.Services.GetRequiredService<Impersonate.Infrastructure.Ai.DataProtectionKeyRingLocation>().Path);
app.Logger.LogInformation("Starting Impersonate API");
if (app.Environment.IsDevelopment())
{
    app.UseCors("FrontendDevelopment");
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "Impersonate API v1");
        options.RoutePrefix = "swagger";
    });
}

app.MapGet("/", () => Results.Ok(new { Name = "Impersonate API", Status = "Running" }));
app.MapHealthChecks("/health");
app.MapGet("/api/planner/readiness", (IPlannerReadiness readiness) => Results.Ok(readiness.Get()));
app.MapGet("/api/execution/readiness", async (IExecutionEnvironmentReadinessService readiness, CancellationToken ct) => Results.Ok(await readiness.CheckAsync(ct)));
app.MapGet("/api/development/preflight", async (string? targetRepository, Impersonate.Infrastructure.Delivery.Mcp.DevelopmentPreflightService preflight, CancellationToken ct) => Results.Ok(await preflight.CheckAsync(targetRepository ?? "Minqxs/TaskIt", ct)));
var ai = app.MapGroup("/api/ai");
ai.MapGet("/providers", async (IAiProviderConnectionService service, CancellationToken ct) => Results.Ok(new { supportedProviders = Enum.GetValues<ProviderType>().Where(x => x is ProviderType.Anthropic or ProviderType.OpenAI or ProviderType.GoogleGemini or ProviderType.OpenRouter), connections = await service.ListAsync(ct) }));
ai.MapGet("/usage/models", async ([Microsoft.AspNetCore.Mvc.FromQuery] int? days, [Microsoft.AspNetCore.Mvc.FromServices] IModelUsageService service, CancellationToken ct) => Results.Ok(new { days = Math.Clamp(days ?? 30, 1, 365), models = await service.GetPlanningUsageAsync(days ?? 30, ct) }));
ai.MapPost("/providers/{providerType}/connections", async (ProviderType providerType, CreateProviderConnectionRequest request, IAiProviderConnectionService service, CancellationToken ct) =>
{
    try
    {
        var connection = await service.CreateAsync(providerType, request, ct);
        return Results.Created($"/api/ai/provider-connections/{connection.Id}", connection);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new ApiError("validation", ex.Message));
    }
    catch (InvalidOperationException ex)
    {
        return Results.Conflict(new ApiError("provider_connection_exists", ex.Message));
    }
    catch (ProviderCredentialStorageException ex)
    {
        return Results.Json(new ApiError("credential_storage_failed", ex.Message), statusCode: 500);
    }
});
ai.MapPut("/provider-connections/{connectionId:guid}/credentials", async (Guid connectionId, ReplaceProviderCredentialRequest request, IAiProviderConnectionService service, CancellationToken ct) =>
{
    try
    {
        return (await service.ReplaceCredentialsAsync(connectionId, request, ct)) is { } connection ? Results.Ok(connection) : Results.NotFound(new ApiError("not_found", "Provider connection was not found."));
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new ApiError("validation", ex.Message));
    }
    catch (ProviderCredentialStorageException ex)
    {
        return Results.Json(new ApiError("credential_storage_failed", ex.Message), statusCode: 500);
    }
});
ai.MapPost("/provider-connections/{connectionId:guid}/validate", async (Guid connectionId, IAiProviderConnectionService service, CancellationToken ct) => (await service.ValidateAsync(connectionId, ct)) is { } x ? Results.Ok(x) : Results.NotFound());
ai.MapPost("/provider-connections/{connectionId:guid}/sync-models", async (Guid connectionId, IAiProviderConnectionService service, CancellationToken ct) =>
{
    try
    {
        return (await service.SynchroniseAsync(connectionId, ct)) is { } x ? Results.Ok(x) : Results.NotFound();
    }
    catch (ProviderCredentialUnavailableException ex)
    {
        return Results.Conflict(new ApiError(ex.Code, ex.Message));
    }
    catch (InvalidOperationException ex)
    {
        return Results.Conflict(new ApiError("connection_not_ready", ex.Message));
    }
    catch (HttpRequestException)
    {
        return Results.Json(new ApiError("provider_unavailable", "The provider could not synchronise models."), statusCode: 503);
    }
});
ai.MapGet("/provider-connections/{connectionId:guid}/models", async (Guid connectionId, IAiProviderConnectionService service, CancellationToken ct) => (await service.ModelsAsync(connectionId, ct)) is { } x ? Results.Ok(x) : Results.NotFound());
ai.MapPut("/provider-connections/{connectionId:guid}/disable", async (Guid connectionId, IAiProviderConnectionService service, CancellationToken ct) => await service.DisableAsync(connectionId, ct) ? Results.NoContent() : Results.NotFound());
ai.MapDelete("/provider-connections/{connectionId:guid}", async (Guid connectionId, IAiProviderConnectionService service, CancellationToken ct) => await service.RemoveAsync(connectionId, ct) ? Results.NoContent() : Results.NotFound());
var projects = app.MapGroup("/api/projects");
projects.MapGet("", async (IProjectService service, ProjectStatus? status, string? search, CancellationToken ct) => Results.Ok(await service.ListAsync(status, search, ct)));
projects.MapGet("/{projectId:guid}", async (Guid projectId, IProjectService service, CancellationToken ct) => (await service.GetAsync(projectId, ct)) is { } project ? Results.Ok(project) : Results.NotFound());
projects.MapPost("", async (CreateProjectRequest request, IProjectService service, CancellationToken ct) =>
{
    try
    {
        var project = await service.CreateAsync(request, ct);
        return (IResult)Results.Created($"/api/projects/{project.Id}", project);
    }
    catch (ArgumentException ex)
    {
        return (IResult)Results.ValidationProblem(new Dictionary<string, string[]> { [ex.ParamName ?? "request"] = [ex.Message] });
    }
});
projects.MapPut("/{projectId:guid}", async (Guid projectId, UpdateProjectRequest request, IProjectService service, CancellationToken ct) =>
{
    try
    {
        return (IResult)((await service.UpdateAsync(projectId, request, ct)) is { } project ? Results.Ok(project) : Results.NotFound());
    }
    catch (ArgumentException ex)
    {
        return (IResult)Results.ValidationProblem(new Dictionary<string, string[]> { [ex.ParamName ?? "request"] = [ex.Message] });
    }
});
projects.MapPatch("/{projectId:guid}/status", async (Guid projectId, ChangeStatusRequest request, IProjectService service, CancellationToken ct) =>
{
    try
    {
        return (IResult)((await service.ChangeStatusAsync(projectId, request.Status, ct)) is { } project ? Results.Ok(project) : Results.NotFound());
    }
    catch (ArgumentOutOfRangeException ex)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]> { [ex.ParamName ?? "status"] = [ex.Message] });
    }
});
projects.MapGet("/{projectId:guid}/health", async (Guid projectId, IProjectService service, CancellationToken ct) => (await service.GetHealthAsync(projectId, ct)) is { } summary ? Results.Ok(summary) : Results.NotFound());
projects.MapGet("/{projectId:guid}/quality/configuration", async (Guid projectId, IProjectQualityService service, CancellationToken ct) => (await service.GetConfigurationAsync(projectId, ct)) is { } value ? Results.Ok(value) : Results.NotFound(new ApiError("not_found", "Project was not found.")));
projects.MapPut("/{projectId:guid}/quality/configuration", async (Guid projectId, SaveProjectQualityConfigurationRequest request, IProjectQualityService service, CancellationToken ct) => { try { return (await service.SaveAsync(projectId, request, ct)) is { } value ? Results.Ok(value) : Results.NotFound(new ApiError("not_found", "Project was not found.")); } catch (ArgumentException ex) { return Results.BadRequest(new ApiError("quality_configuration_invalid", ex.Message)); } });
projects.MapPost("/{projectId:guid}/quality/validate", async (Guid projectId, IProjectQualityService service, CancellationToken ct) => (await service.ValidateAsync(projectId, ct)) is { } value ? Results.Ok(value) : Results.NotFound(new ApiError("not_found", "Project was not found.")));
projects.MapGet("/{projectId:guid}/quality/summary", async (Guid projectId, IProjectQualityService service, CancellationToken ct) => (await service.GetSummaryAsync(projectId, false, ct)) is { } value ? Results.Ok(value) : Results.NotFound(new ApiError("not_found", "Project was not found.")));
projects.MapPost("/{projectId:guid}/quality/refresh", async (Guid projectId, IProjectQualityService service, CancellationToken ct) => (await service.GetSummaryAsync(projectId, true, ct)) is { } value ? Results.Ok(value) : Results.NotFound(new ApiError("not_found", "Project was not found.")));
projects.MapDelete("/{projectId:guid}/quality/configuration", async (Guid projectId, IProjectQualityService service, CancellationToken ct) => await service.RemoveAsync(projectId, ct) ? Results.NoContent() : Results.NotFound(new ApiError("quality_not_configured", "Code quality is not configured.")));
var runs = projects.MapGroup("/{projectId:guid}/pipeline-runs");
projects.MapPost("/{projectId:guid}/ai/model-selection/preview", async (Guid projectId, ModelSelectionPreviewRequest request, IProjectAiService service, CancellationToken ct) => (await service.PreviewAsync(projectId, request.Role, request.Description, request.ManualModelOverrideId, ct)) is { } result ? Results.Ok(result) : Results.NotFound(new ApiError("not_found", "Project was not found.")));
projects.MapGet("/{projectId:guid}/ai/readiness", async (Guid projectId, IProjectAiService service, CancellationToken ct) => (await service.GetReadinessAsync(projectId, ct)) is { } result ? Results.Ok(result) : Results.NotFound(new ApiError("not_found", "Project was not found.")));
runs.MapPost("", async (Guid projectId, CreatePipelineRunRequest request, IPipelineRunService service, CancellationToken ct) => ToResult(await service.CreateAsync(projectId, request, ct), r => Results.Created($"/api/projects/{projectId}/pipeline-runs/{r.Id}", r)));
runs.MapGet("", async (Guid projectId, PipelineRunStatus? status, DateTimeOffset? createdFrom, DateTimeOffset? createdTo, IPipelineRunService service, CancellationToken ct) => Results.Ok(await service.ListAsync(projectId, status, createdFrom, createdTo, ct)));
runs.MapGet("/{pipelineRunId:guid}", async (Guid projectId, Guid pipelineRunId, IPipelineRunService service, CancellationToken ct) => (await service.GetAsync(projectId, pipelineRunId, ct)) is { } r ? Results.Ok(r) : Results.NotFound());
runs.MapGet("/{pipelineRunId:guid}/timeline", async (Guid projectId, Guid pipelineRunId, IPipelineRunService service, CancellationToken ct) => (await service.TimelineAsync(projectId, pipelineRunId, ct)) is { } e ? Results.Ok(e) : Results.NotFound());
runs.MapPost("/{pipelineRunId:guid}/planning/start", async (Guid projectId, Guid pipelineRunId, IServiceProvider services, IPlannerReadiness legacyReadiness, CancellationToken ct) =>
{
    if (services.GetService<IPipelineRunRepository>() is null && !legacyReadiness.Get().IsReady)
        return Results.Json(new ApiError("planner_configuration_unavailable", legacyReadiness.Get().Message), statusCode: 503);
    var service = services.GetRequiredService<IPipelineRunService>();
    return ToResult(await service.StartPlanningAsync(projectId, pipelineRunId, ct), r => Results.Accepted($"/api/projects/{projectId}/pipeline-runs/{pipelineRunId}/planning", r));
});
runs.MapGet("/{pipelineRunId:guid}/planning", async (Guid projectId, Guid pipelineRunId, IPipelineRunService service, CancellationToken ct) => (await service.GetAsync(projectId, pipelineRunId, ct)) is { } r ? Results.Ok(r) : Results.NotFound());
runs.MapGet("/{pipelineRunId:guid}/execution/readiness", async (Guid projectId, Guid pipelineRunId, IPipelineRunService service, CancellationToken ct) => ToResult(await service.ExecutionReadinessAsync(projectId, pipelineRunId, ct), Results.Ok));
runs.MapGet("/{pipelineRunId:guid}/intelligence", async (Guid projectId, Guid pipelineRunId, IPipelineRunService service, CancellationToken ct) => ToResult(await service.IntelligenceAsync(projectId, pipelineRunId, ct), Results.Ok));
runs.MapPost("/{pipelineRunId:guid}/execution/start", async (Guid projectId, Guid pipelineRunId, IPipelineRunService service, CancellationToken ct) => ToResult(await service.StartExecutionAsync(projectId, pipelineRunId, ct), r => Results.Accepted($"/api/projects/{projectId}/pipeline-runs/{pipelineRunId}", r)));
runs.MapPost("/{pipelineRunId:guid}/execution/retry", async (Guid projectId, Guid pipelineRunId, IPipelineRunService service, CancellationToken ct) => ToResult(await service.RetryExecutionAsync(projectId, pipelineRunId, ct), r => Results.Accepted($"/api/projects/{projectId}/pipeline-runs/{pipelineRunId}", r)));
runs.MapPost("/{pipelineRunId:guid}/deliveries/{deliveryId:guid}/retry", async (Guid projectId, Guid pipelineRunId, Guid deliveryId, ITaskDeliveryRecoveryService service, CancellationToken ct) => ToDeliveryResult(await service.RetryAsync(projectId, pipelineRunId, deliveryId, ct), r => Results.Accepted($"/api/projects/{projectId}/pipeline-runs/{pipelineRunId}", r)));
runs.MapPost("/{pipelineRunId:guid}/delivery/merge-to-main", async (Guid projectId, Guid pipelineRunId, IFinalRunDeliveryService service, CancellationToken ct) => ToDeliveryResult(await service.MergeAsync(projectId, pipelineRunId, ct), r => Results.Ok(r)));
runs.MapPost("/{pipelineRunId:guid}/tasks/{taskId:guid}/execution/start", async (Guid projectId, Guid pipelineRunId, Guid taskId, ITaskControlService service, CancellationToken ct) => ToResult(await service.ExecuteAsync(projectId, pipelineRunId, taskId, ct), value => Results.Accepted($"/api/projects/{projectId}/pipeline-runs/{pipelineRunId}", value)));
runs.MapPost("/{pipelineRunId:guid}/tasks/{taskId:guid}/execution/retry", async (Guid projectId, Guid pipelineRunId, Guid taskId, ITaskControlService service, CancellationToken ct) => ToResult(await service.ExecuteAsync(projectId, pipelineRunId, taskId, ct), value => Results.Accepted($"/api/projects/{projectId}/pipeline-runs/{pipelineRunId}", value)));
runs.MapPut("/{pipelineRunId:guid}/tasks/{taskId:guid}/model-overrides", async (Guid projectId, Guid pipelineRunId, Guid taskId, TaskModelOverridesRequest request, IPipelineRunService service, CancellationToken ct) => ToResult(await service.SetTaskModelOverridesAsync(projectId, pipelineRunId, taskId, request, ct), Results.Ok));
runs.MapGet("/{pipelineRunId:guid}/tasks/{taskId:guid}/attempts", async (Guid projectId, Guid pipelineRunId, Guid taskId, IPipelineRunService service, CancellationToken ct) => (await service.GetAsync(projectId, pipelineRunId, ct))?.Tasks.SingleOrDefault(x => x.Id == taskId) is { } task ? Results.Ok(task.Attempts) : Results.NotFound());
runs.MapGet("/{pipelineRunId:guid}/tasks/{taskId:guid}/reviews", async (Guid projectId, Guid pipelineRunId, Guid taskId, IPipelineRunService service, CancellationToken ct) => (await service.GetAsync(projectId, pipelineRunId, ct))?.Tasks.SingleOrDefault(x => x.Id == taskId) is { } task ? Results.Ok(task.Reviews) : Results.NotFound());
runs.MapGet("/{pipelineRunId:guid}/tasks/{taskId:guid}/attempts/{attemptId:guid}/diff", async (Guid projectId, Guid pipelineRunId, Guid taskId, Guid attemptId, IPipelineRunService service, IExecutionArtifactStore artifacts, CancellationToken ct) =>
{
    var task = (await service.GetAsync(projectId, pipelineRunId, ct))?.Tasks.SingleOrDefault(x => x.Id == taskId);
    var attempt = task?.Attempts.SingleOrDefault(x => x.Id == attemptId);
    if (attempt?.PatchArtifactReference is null)
        return Results.NotFound();
    try
    {
        return Results.Text(await artifacts.ReadTextAsync(attempt.PatchArtifactReference, 200_000, ct), "text/plain; charset=utf-8");
    }
    catch (FileNotFoundException)
    {
        return Results.NotFound();
    }
});
runs.MapPost("/{pipelineRunId:guid}/cancel", async (Guid projectId, Guid pipelineRunId, IPipelineRunService service, CancellationToken ct) => ToResult(await service.CancelAsync(projectId, pipelineRunId, ct), Results.Ok));
runs.MapDelete("/{pipelineRunId:guid}", async (Guid projectId, Guid pipelineRunId, IPipelineRunService service, CancellationToken ct) => ToResult(await service.DeleteAsync(projectId, pipelineRunId, ct), _ => Results.NoContent()));
app.Run();
static IResult ToResult<T>(PipelineOperationResult<T> result, Func<T, IResult> success) => result.Succeeded ? success(result.Value!) : result.Code switch
{
    "not_found" => Results.NotFound(new ApiError(result.Code, result.Error!)),
    "invalid_transition" or "project_off" or "conflict" or "execution_not_ready" => Results.Conflict(new ApiError(result.Code, result.Error!)),
    _ => Results.BadRequest(new ApiError(result.Code ?? "validation", result.Error!))
};
static IResult ToDeliveryResult<T>(DeliveryOperationResult<T> result, Func<T, IResult> success) => result.Succeeded ? success(result.Value!) : result.Code switch
{
    "delivery_not_found" => Results.NotFound(new ApiError(result.Code, result.Error!)),
    "delivery_retry_state_invalid" or "delivery_retry_claim_active" or "delivery_retry_handoff_changed" or "delivery_retry_checkpoint_invalid" or "delivery_retry_conflict" => Results.Conflict(new ApiError(result.Code, result.Error!)),
    _ => Results.BadRequest(new ApiError(result.Code ?? "delivery_retry_failed", result.Error!))
};
public partial class Program;
