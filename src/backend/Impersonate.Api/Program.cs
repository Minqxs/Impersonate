using Impersonate.Application;
using Impersonate.Application.Projects;
using Impersonate.Application.Pipelines;
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
var app = builder.Build();
app.Logger.LogInformation("Starting Impersonate API");
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "Impersonate API v1");
        options.RoutePrefix = "swagger";
    });
}
app.MapGet("/", () => Results.Ok(new { Name = "Impersonate API", Status = "Running" }));
app.MapHealthChecks("/health");
var projects = app.MapGroup("/api/projects");
projects.MapGet("", async (IProjectService service, ProjectStatus? status, string? search, CancellationToken ct) => Results.Ok(await service.ListAsync(status, search, ct)));
projects.MapGet("/{projectId:guid}", async (Guid projectId, IProjectService service, CancellationToken ct) => (await service.GetAsync(projectId, ct)) is { } project ? Results.Ok(project) : Results.NotFound());
projects.MapPost("", async (CreateProjectRequest request, IProjectService service, CancellationToken ct) => { try { var project = await service.CreateAsync(request, ct); return (IResult)Results.Created($"/api/projects/{project.Id}", project); } catch (ArgumentException ex) { return (IResult)Results.ValidationProblem(new Dictionary<string, string[]> { [ex.ParamName ?? "request"] = [ex.Message] }); } });
projects.MapPut("/{projectId:guid}", async (Guid projectId, UpdateProjectRequest request, IProjectService service, CancellationToken ct) => { try { return (IResult)((await service.UpdateAsync(projectId, request, ct)) is { } project ? Results.Ok(project) : Results.NotFound()); } catch (ArgumentException ex) { return (IResult)Results.ValidationProblem(new Dictionary<string, string[]> { [ex.ParamName ?? "request"] = [ex.Message] }); } });
projects.MapPatch("/{projectId:guid}/status", async (Guid projectId, ChangeStatusRequest request, IProjectService service, CancellationToken ct) => { try { return (IResult)((await service.ChangeStatusAsync(projectId, request.Status, ct)) is { } project ? Results.Ok(project) : Results.NotFound()); } catch (ArgumentOutOfRangeException ex) { return Results.ValidationProblem(new Dictionary<string, string[]> { [ex.ParamName ?? "status"] = [ex.Message] }); } });
projects.MapGet("/{projectId:guid}/health", async (Guid projectId, IProjectService service, CancellationToken ct) => (await service.GetHealthAsync(projectId, ct)) is { } summary ? Results.Ok(summary) : Results.NotFound());
var runs=projects.MapGroup("/{projectId:guid}/pipeline-runs");
runs.MapPost("",async(Guid projectId,CreatePipelineRunRequest request,IPipelineRunService service,CancellationToken ct)=>ToResult(await service.CreateAsync(projectId,request,ct),r=>Results.Created($"/api/projects/{projectId}/pipeline-runs/{r.Id}",r)));
runs.MapGet("",async(Guid projectId,PipelineRunStatus? status,DateTimeOffset? createdFrom,DateTimeOffset? createdTo,IPipelineRunService service,CancellationToken ct)=>Results.Ok(await service.ListAsync(projectId,status,createdFrom,createdTo,ct)));
runs.MapGet("/{pipelineRunId:guid}",async(Guid projectId,Guid pipelineRunId,IPipelineRunService service,CancellationToken ct)=>(await service.GetAsync(projectId,pipelineRunId,ct)) is{}r?Results.Ok(r):Results.NotFound());
runs.MapGet("/{pipelineRunId:guid}/timeline",async(Guid projectId,Guid pipelineRunId,IPipelineRunService service,CancellationToken ct)=>(await service.TimelineAsync(projectId,pipelineRunId,ct)) is{}e?Results.Ok(e):Results.NotFound());
runs.MapPost("/{pipelineRunId:guid}/planning/start",async(Guid projectId,Guid pipelineRunId,IPipelineRunService service,IConfiguration config,CancellationToken ct)=>string.IsNullOrWhiteSpace(config["Agents:Planner:Model"])||string.IsNullOrWhiteSpace(config["ANTHROPIC_API_KEY"]??config["Anthropic:ApiKey"])?Results.Problem("Planner configuration is unavailable.",statusCode:503):ToResult(await service.StartPlanningAsync(projectId,pipelineRunId,ct),r=>Results.Accepted($"/api/projects/{projectId}/pipeline-runs/{pipelineRunId}/planning",r)));
runs.MapGet("/{pipelineRunId:guid}/planning",async(Guid projectId,Guid pipelineRunId,IPipelineRunService service,CancellationToken ct)=>(await service.GetAsync(projectId,pipelineRunId,ct)) is{}r?Results.Ok(r):Results.NotFound());
runs.MapPost("/{pipelineRunId:guid}/cancel",async(Guid projectId,Guid pipelineRunId,IPipelineRunService service,CancellationToken ct)=>ToResult(await service.CancelAsync(projectId,pipelineRunId,ct),Results.Ok));
app.Run();
static IResult ToResult<T>(PipelineOperationResult<T> result,Func<T,IResult> success)=>result.Succeeded?success(result.Value!):result.Code switch{"not_found"=>Results.NotFound(),"invalid_transition"=>Results.Conflict(new{result.Code,result.Error}),"project_off"=>Results.Conflict(new{result.Code,result.Error}),_=>Results.ValidationProblem(new Dictionary<string,string[]>{{"request",[result.Error!]}})};
public sealed record ChangeStatusRequest(ProjectStatus Status);
public partial class Program;
