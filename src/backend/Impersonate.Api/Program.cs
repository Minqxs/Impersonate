using Impersonate.Application;
using Impersonate.Application.Projects;
using Impersonate.Application.Pipelines;
using Impersonate.Application.Planning;
using Impersonate.Domain.Pipelines;
using Impersonate.Domain.Projects;
using Impersonate.Infrastructure;
using Impersonate.Application.Ai;
using Impersonate.Domain.Ai;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHealthChecks();
builder.Services.AddOpenApi();
builder.Services.AddCors(options => options.AddPolicy("FrontendDevelopment", policy =>
    policy.WithOrigins("http://localhost:5173")
        .AllowAnyHeader()
        .AllowAnyMethod()));
var app = builder.Build();
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
app.MapGet("/api/planner/readiness",(IPlannerReadiness readiness)=>Results.Ok(readiness.Get()));
var ai=app.MapGroup("/api/ai");
ai.MapGet("/providers",async(IAiProviderConnectionService service,CancellationToken ct)=>Results.Ok(new{supportedProviders=Enum.GetValues<ProviderType>().Where(x=>x is ProviderType.Anthropic or ProviderType.OpenAI or ProviderType.GoogleGemini or ProviderType.OpenRouter),connections=await service.ListAsync(ct)}));
ai.MapPost("/providers/{providerType}/connections",async(ProviderType providerType,CreateProviderConnectionRequest request,IAiProviderConnectionService service,CancellationToken ct)=>{try{var connection=await service.CreateAsync(providerType,request,ct);return Results.Created($"/api/ai/provider-connections/{connection.Id}",connection);}catch(ArgumentException ex){return Results.BadRequest(new ApiError("validation",ex.Message));}});
ai.MapPost("/provider-connections/{connectionId:guid}/validate",async(Guid connectionId,IAiProviderConnectionService service,CancellationToken ct)=>(await service.ValidateAsync(connectionId,ct)) is{}x?Results.Ok(x):Results.NotFound());
ai.MapPost("/provider-connections/{connectionId:guid}/sync-models",async(Guid connectionId,IAiProviderConnectionService service,CancellationToken ct)=>{try{return (await service.SynchroniseAsync(connectionId,ct)) is{}x?Results.Ok(x):Results.NotFound();}catch(InvalidOperationException ex){return Results.Conflict(new ApiError("connection_not_ready",ex.Message));}catch(HttpRequestException){return Results.Json(new ApiError("provider_unavailable","The provider could not synchronise models."),statusCode:503);}});
ai.MapGet("/provider-connections/{connectionId:guid}/models",async(Guid connectionId,IAiProviderConnectionService service,CancellationToken ct)=>(await service.ModelsAsync(connectionId,ct)) is{}x?Results.Ok(x):Results.NotFound());
ai.MapPut("/provider-connections/{connectionId:guid}/disable",async(Guid connectionId,IAiProviderConnectionService service,CancellationToken ct)=>await service.DisableAsync(connectionId,ct)?Results.NoContent():Results.NotFound());
ai.MapDelete("/provider-connections/{connectionId:guid}",async(Guid connectionId,IAiProviderConnectionService service,CancellationToken ct)=>await service.RemoveAsync(connectionId,ct)?Results.NoContent():Results.NotFound());
var projects = app.MapGroup("/api/projects");
projects.MapGet("", async (IProjectService service, ProjectStatus? status, string? search, CancellationToken ct) => Results.Ok(await service.ListAsync(status, search, ct)));
projects.MapGet("/{projectId:guid}", async (Guid projectId, IProjectService service, CancellationToken ct) => (await service.GetAsync(projectId, ct)) is { } project ? Results.Ok(project) : Results.NotFound());
projects.MapPost("", async (CreateProjectRequest request, IProjectService service, CancellationToken ct) => { try { var project = await service.CreateAsync(request, ct); return (IResult)Results.Created($"/api/projects/{project.Id}", project); } catch (ArgumentException ex) { return (IResult)Results.ValidationProblem(new Dictionary<string, string[]> { [ex.ParamName ?? "request"] = [ex.Message] }); } });
projects.MapPut("/{projectId:guid}", async (Guid projectId, UpdateProjectRequest request, IProjectService service, CancellationToken ct) => { try { return (IResult)((await service.UpdateAsync(projectId, request, ct)) is { } project ? Results.Ok(project) : Results.NotFound()); } catch (ArgumentException ex) { return (IResult)Results.ValidationProblem(new Dictionary<string, string[]> { [ex.ParamName ?? "request"] = [ex.Message] }); } });
projects.MapPatch("/{projectId:guid}/status", async (Guid projectId, ChangeStatusRequest request, IProjectService service, CancellationToken ct) => { try { return (IResult)((await service.ChangeStatusAsync(projectId, request.Status, ct)) is { } project ? Results.Ok(project) : Results.NotFound()); } catch (ArgumentOutOfRangeException ex) { return Results.ValidationProblem(new Dictionary<string, string[]> { [ex.ParamName ?? "status"] = [ex.Message] }); } });
projects.MapGet("/{projectId:guid}/health", async (Guid projectId, IProjectService service, CancellationToken ct) => (await service.GetHealthAsync(projectId, ct)) is { } summary ? Results.Ok(summary) : Results.NotFound());
var runs=projects.MapGroup("/{projectId:guid}/pipeline-runs");
projects.MapPost("/{projectId:guid}/ai/model-selection/preview",async(Guid projectId,ModelSelectionPreviewRequest request,IModelRouter router,CancellationToken ct)=>Results.Ok(await router.SelectAsync(new(projectId,null,request.Role,request.Description,request.ManualModelOverrideId),ct)));
projects.MapGet("/{projectId:guid}/ai/readiness",async(Guid projectId,[Microsoft.AspNetCore.Mvc.FromServices] IAiRoutingRepository repository,CancellationToken ct)=>{var connections=await repository.GetConnectionsAsync(ct);var models=await repository.GetModelsAsync(null,ct);var connected=connections.Where(x=>x.Status==ProviderConnectionStatus.Connected).ToList();var eligible=models.Count(x=>x.IsAvailable&&connected.Any(c=>c.Id==x.ProviderConnectionId));return Results.Ok(new{connectedProviderCount=connected.Count,validProviderCount=connected.Count,discoveredEligiblePlannerModels=eligible,routingStatus=eligible>0?"Ready":"Incomplete",blockers=eligible>0?Array.Empty<string>():connected.Count==0?["No connected providers."]:["No eligible Planner models."]});});
runs.MapPost("",async(Guid projectId,CreatePipelineRunRequest request,IPipelineRunService service,CancellationToken ct)=>ToResult(await service.CreateAsync(projectId,request,ct),r=>Results.Created($"/api/projects/{projectId}/pipeline-runs/{r.Id}",r)));
runs.MapGet("",async(Guid projectId,PipelineRunStatus? status,DateTimeOffset? createdFrom,DateTimeOffset? createdTo,IPipelineRunService service,CancellationToken ct)=>Results.Ok(await service.ListAsync(projectId,status,createdFrom,createdTo,ct)));
runs.MapGet("/{pipelineRunId:guid}",async(Guid projectId,Guid pipelineRunId,IPipelineRunService service,CancellationToken ct)=>(await service.GetAsync(projectId,pipelineRunId,ct)) is{}r?Results.Ok(r):Results.NotFound());
runs.MapGet("/{pipelineRunId:guid}/timeline",async(Guid projectId,Guid pipelineRunId,IPipelineRunService service,CancellationToken ct)=>(await service.TimelineAsync(projectId,pipelineRunId,ct)) is{}e?Results.Ok(e):Results.NotFound());
runs.MapPost("/{pipelineRunId:guid}/planning/start",async(Guid projectId,Guid pipelineRunId,IServiceProvider services,IPlannerReadiness legacyReadiness,CancellationToken ct)=>
{
    if(services.GetService<IAiRoutingRepository>() is null&&!legacyReadiness.Get().IsReady)return Results.Json(new ApiError("planner_configuration_unavailable",legacyReadiness.Get().Message),statusCode:503);
    var service=services.GetRequiredService<IPipelineRunService>();
    return ToResult(await service.StartPlanningAsync(projectId,pipelineRunId,ct),r=>Results.Accepted($"/api/projects/{projectId}/pipeline-runs/{pipelineRunId}/planning",r));
});
runs.MapGet("/{pipelineRunId:guid}/planning",async(Guid projectId,Guid pipelineRunId,IPipelineRunService service,CancellationToken ct)=>(await service.GetAsync(projectId,pipelineRunId,ct)) is{}r?Results.Ok(r):Results.NotFound());
runs.MapPost("/{pipelineRunId:guid}/cancel",async(Guid projectId,Guid pipelineRunId,IPipelineRunService service,CancellationToken ct)=>ToResult(await service.CancelAsync(projectId,pipelineRunId,ct),Results.Ok));
app.Run();
static IResult ToResult<T>(PipelineOperationResult<T> result,Func<T,IResult> success)=>result.Succeeded?success(result.Value!):result.Code switch{"not_found"=>Results.NotFound(new ApiError(result.Code,result.Error!)),"invalid_transition"=>Results.Conflict(new ApiError(result.Code,result.Error!)),"project_off"=>Results.Conflict(new ApiError(result.Code,result.Error!)),_=>Results.BadRequest(new ApiError(result.Code??"validation",result.Error!))};
public sealed record ChangeStatusRequest(ProjectStatus Status);
public sealed record ApiError(string Code,string Message);
public sealed record ModelSelectionPreviewRequest(AgentRole Role,string Description,Guid? ManualModelOverrideId);
public partial class Program;
