using Impersonate.Application;
using Impersonate.Application.Projects;
using Impersonate.Application.Pipelines;
using Impersonate.Application.Planning;
using Impersonate.Application.AiModels;
using Impersonate.Domain.AiModels;
using Impersonate.Domain.Pipelines;
using Impersonate.Domain.Projects;
using Impersonate.Infrastructure;
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
app.MapGet("/api/planner/readiness",async(IAiModelConfigurationService models,CancellationToken ct)=>ToAiResult(await models.ReadinessAsync(null,ct),Results.Ok));
var ai=app.MapGroup("/api/ai");
ai.MapGet("/models",async(IAiModelConfigurationService service,CancellationToken ct)=>Results.Ok(await service.ListModelsAsync(ct)));
ai.MapPost("/models",async(AiModelProfileRequest request,IAiModelConfigurationService service,CancellationToken ct)=>ToAiResult(await service.SaveModelAsync(null,request,ct),m=>Results.Created($"/api/ai/models/{m.Id}",m)));
ai.MapPut("/models/{modelId:guid}",async(Guid modelId,AiModelProfileRequest request,IAiModelConfigurationService service,CancellationToken ct)=>ToAiResult(await service.SaveModelAsync(modelId,request,ct),Results.Ok));
ai.MapPatch("/models/{modelId:guid}/status",async(Guid modelId,ModelStatusRequest request,IAiModelConfigurationService service,CancellationToken ct)=>ToAiResult(await service.SetEnabledAsync(modelId,request.IsEnabled,ct),Results.Ok));
ai.MapGet("/role-assignments",async(IAiModelConfigurationService service,CancellationToken ct)=>Results.Ok(await service.ListAssignmentsAsync(null,ct)));
ai.MapPut("/role-assignments/{role}",async(AgentRole role,SetModelAssignmentRequest request,IAiModelConfigurationService service,CancellationToken ct)=>ToAiResult(await service.SetAssignmentAsync(role,request.AiModelProfileId,null,ct),Results.Ok));
ai.MapDelete("/role-assignments/{role}",async(AgentRole role,IAiModelConfigurationService service,CancellationToken ct)=>ToAiResult(await service.RemoveAssignmentAsync(role,null,ct),_=>Results.NoContent()));
var projects = app.MapGroup("/api/projects");
projects.MapGet("", async (IProjectService service, ProjectStatus? status, string? search, CancellationToken ct) => Results.Ok(await service.ListAsync(status, search, ct)));
projects.MapGet("/{projectId:guid}", async (Guid projectId, IProjectService service, CancellationToken ct) => (await service.GetAsync(projectId, ct)) is { } project ? Results.Ok(project) : Results.NotFound());
projects.MapPost("", async (CreateProjectRequest request, IProjectService service, CancellationToken ct) => { try { var project = await service.CreateAsync(request, ct); return (IResult)Results.Created($"/api/projects/{project.Id}", project); } catch (ArgumentException ex) { return (IResult)Results.ValidationProblem(new Dictionary<string, string[]> { [ex.ParamName ?? "request"] = [ex.Message] }); } });
projects.MapPut("/{projectId:guid}", async (Guid projectId, UpdateProjectRequest request, IProjectService service, CancellationToken ct) => { try { return (IResult)((await service.UpdateAsync(projectId, request, ct)) is { } project ? Results.Ok(project) : Results.NotFound()); } catch (ArgumentException ex) { return (IResult)Results.ValidationProblem(new Dictionary<string, string[]> { [ex.ParamName ?? "request"] = [ex.Message] }); } });
projects.MapPatch("/{projectId:guid}/status", async (Guid projectId, ChangeStatusRequest request, IProjectService service, CancellationToken ct) => { try { return (IResult)((await service.ChangeStatusAsync(projectId, request.Status, ct)) is { } project ? Results.Ok(project) : Results.NotFound()); } catch (ArgumentOutOfRangeException ex) { return Results.ValidationProblem(new Dictionary<string, string[]> { [ex.ParamName ?? "status"] = [ex.Message] }); } });
projects.MapGet("/{projectId:guid}/health", async (Guid projectId, IProjectService service, CancellationToken ct) => (await service.GetHealthAsync(projectId, ct)) is { } summary ? Results.Ok(summary) : Results.NotFound());
var runs=projects.MapGroup("/{projectId:guid}/pipeline-runs");
projects.MapGet("/{projectId:guid}/planner/readiness",async(Guid projectId,IAiModelConfigurationService service,CancellationToken ct)=>ToAiResult(await service.ReadinessAsync(projectId,ct),Results.Ok));
projects.MapGet("/{projectId:guid}/ai/role-assignments",async(Guid projectId,IAiModelConfigurationService service,CancellationToken ct)=>await service.EffectiveAsync(projectId,ct)is{} result?ToAiResult(result,Results.Ok):Results.NotFound());
projects.MapPut("/{projectId:guid}/ai/role-assignments/{role}",async(Guid projectId,AgentRole role,SetModelAssignmentRequest request,IAiModelConfigurationService service,CancellationToken ct)=>ToAiResult(await service.SetAssignmentAsync(role,request.AiModelProfileId,projectId,ct),Results.Ok));
projects.MapDelete("/{projectId:guid}/ai/role-assignments/{role}",async(Guid projectId,AgentRole role,IAiModelConfigurationService service,CancellationToken ct)=>ToAiResult(await service.RemoveAssignmentAsync(role,projectId,ct),_=>Results.NoContent()));
runs.MapPost("",async(Guid projectId,CreatePipelineRunRequest request,IPipelineRunService service,CancellationToken ct)=>ToResult(await service.CreateAsync(projectId,request,ct),r=>Results.Created($"/api/projects/{projectId}/pipeline-runs/{r.Id}",r)));
runs.MapGet("",async(Guid projectId,PipelineRunStatus? status,DateTimeOffset? createdFrom,DateTimeOffset? createdTo,IPipelineRunService service,CancellationToken ct)=>Results.Ok(await service.ListAsync(projectId,status,createdFrom,createdTo,ct)));
runs.MapGet("/{pipelineRunId:guid}",async(Guid projectId,Guid pipelineRunId,IPipelineRunService service,CancellationToken ct)=>(await service.GetAsync(projectId,pipelineRunId,ct)) is{}r?Results.Ok(r):Results.NotFound());
runs.MapGet("/{pipelineRunId:guid}/timeline",async(Guid projectId,Guid pipelineRunId,IPipelineRunService service,CancellationToken ct)=>(await service.TimelineAsync(projectId,pipelineRunId,ct)) is{}e?Results.Ok(e):Results.NotFound());
runs.MapPost("/{pipelineRunId:guid}/planning/start",async(Guid projectId,Guid pipelineRunId,IServiceProvider services,IAiModelConfigurationService models,CancellationToken ct)=>
{
    var readiness=await models.ReadinessAsync(projectId,ct);if(!readiness.Succeeded)return ToAiResult(readiness,Results.Ok);var state=readiness.Value!;
    if(!state.IsReady)return Results.Json(new ApiError("planner_configuration_unavailable",state.Message),statusCode:503);
    var service=services.GetRequiredService<IPipelineRunService>();
    return ToResult(await service.StartPlanningAsync(projectId,pipelineRunId,ct),r=>Results.Accepted($"/api/projects/{projectId}/pipeline-runs/{pipelineRunId}/planning",r));
});
runs.MapGet("/{pipelineRunId:guid}/planning",async(Guid projectId,Guid pipelineRunId,IPipelineRunService service,CancellationToken ct)=>(await service.GetAsync(projectId,pipelineRunId,ct)) is{}r?Results.Ok(r):Results.NotFound());
runs.MapPost("/{pipelineRunId:guid}/cancel",async(Guid projectId,Guid pipelineRunId,IPipelineRunService service,CancellationToken ct)=>ToResult(await service.CancelAsync(projectId,pipelineRunId,ct),Results.Ok));
app.Run();
static IResult ToResult<T>(PipelineOperationResult<T> result,Func<T,IResult> success)=>result.Succeeded?success(result.Value!):result.Code switch{"not_found"=>Results.NotFound(new ApiError(result.Code,result.Error!)),"invalid_transition"=>Results.Conflict(new ApiError(result.Code,result.Error!)),"project_off"=>Results.Conflict(new ApiError(result.Code,result.Error!)),_=>Results.BadRequest(new ApiError(result.Code??"validation",result.Error!))};
static IResult ToAiResult<T>(AiModelResult<T> result,Func<T,IResult> success)=>result.Succeeded?success(result.Value!):result.Code switch{"not_found"=>Results.NotFound(new ApiError(result.Code,result.Error!)),"duplicate_model" or "model_disabled" or "provider_unsupported"=>Results.Conflict(new ApiError(result.Code,result.Error!)),_=>Results.BadRequest(new ApiError(result.Code??"validation",result.Error!))};
public sealed record ChangeStatusRequest(ProjectStatus Status);
public sealed record ApiError(string Code,string Message);
public sealed record ModelStatusRequest(bool IsEnabled);public sealed record SetModelAssignmentRequest(Guid AiModelProfileId);
public partial class Program;
