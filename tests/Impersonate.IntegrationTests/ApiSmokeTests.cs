using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Impersonate.Application.Projects;
using Impersonate.Domain.Projects;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Configuration;
using Impersonate.Application.AiModels;
using Impersonate.Domain.AiModels;
using Xunit;

namespace Impersonate.IntegrationTests;

public sealed class ApiSmokeTests : IClassFixture<ProjectApiFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    private readonly HttpClient client;

    public ApiSmokeTests(ProjectApiFactory factory) => client = factory.CreateClient();

    [Fact]
    public async Task RootAndHealth_ReturnSuccess()
    {
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health")).StatusCode);
    }

    [Fact]
    public async Task ProjectWorkflow_CreateReadUpdateStatusFilterAndHealth()
    {
        var create = await client.PostAsJsonAsync("/api/projects", new CreateProjectRequest("Integration Project", null, "https://github.com/example/integration", "main"));
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var project = (await create.Content.ReadFromJsonAsync<ProjectDto>(JsonOptions))!;
        Assert.Equal(ProjectStatus.Idle, project.Status);

        Assert.Equal(project.Id, (await client.GetFromJsonAsync<ProjectDto>($"/api/projects/{project.Id}", JsonOptions))!.Id);
        var update = await client.PutAsJsonAsync($"/api/projects/{project.Id}", new UpdateProjectRequest("Updated Project", "Details", "https://github.com/example/updated.git", "develop"));
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var status = await client.PatchAsJsonAsync($"/api/projects/{project.Id}/status", new { status = "Active" });
        Assert.Equal(ProjectStatus.Active, (await status.Content.ReadFromJsonAsync<ProjectDto>(JsonOptions))!.Status);

        var filtered = await client.GetFromJsonAsync<List<ProjectDto>>("/api/projects?status=Active&search=updated", JsonOptions);
        Assert.Contains(filtered!, x => x.Id == project.Id);
        var health = await client.GetFromJsonAsync<ProjectHealthSummaryDto>($"/api/projects/{project.Id}/health");
        Assert.Equal("Unknown", health!.OverallStatus);
        Assert.Equal(2, health.Checks.Count);
    }

    [Fact]
    public async Task MissingAndValidation_ReturnExpectedResponses()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/projects/{Guid.NewGuid()}" )).StatusCode);
        var invalid = await client.PostAsJsonAsync("/api/projects", new CreateProjectRequest("", null, "not-a-url", ""));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal("application/problem+json", invalid.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Development_ExposesSwaggerUiAndOpenApiDocument()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseEnvironment("Development"));
        using var developmentClient = factory.CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await developmentClient.GetAsync("/swagger/index.html")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await developmentClient.GetAsync("/openapi/v1.json")).StatusCode);
    }

    [Fact]
    public async Task Development_AllowsViteOriginCorsPreflight()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseEnvironment("Development"));
        using var developmentClient = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/projects");
        request.Headers.Add("Origin", "http://localhost:5173");
        request.Headers.Add("Access-Control-Request-Method", "GET");

        using var response = await developmentClient.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("http://localhost:5173", response.Headers.GetValues("Access-Control-Allow-Origin").Single());
    }

    [Fact]
    public async Task PlannerReadiness_DoesNotExposeCredentials_AndStartReturnsStructuredUnavailable()
    {
        using var readiness = await client.GetAsync("/api/planner/readiness");
        var readinessJson = await readiness.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, readiness.StatusCode);
        Assert.DoesNotContain("\"status\":\"Ready\"", readinessJson);
        Assert.DoesNotContain("api-key", readinessJson, StringComparison.OrdinalIgnoreCase);

        var projectResponse=await client.PostAsJsonAsync("/api/projects",new CreateProjectRequest("Readiness Project",null,"https://github.com/example/readiness","main"));var project=(await projectResponse.Content.ReadFromJsonAsync<ProjectDto>(JsonOptions))!;
        using var start = await client.PostAsync($"/api/projects/{project.Id}/pipeline-runs/{Guid.NewGuid()}/planning/start", null);
        var error = await start.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.ServiceUnavailable, start.StatusCode);
        Assert.Contains("planner_configuration_unavailable", error);
    }

    [Fact]
    public async Task PlannerReadiness_IsReadyWhenAllSafeConfigurationIsPresent()
    {
        await using var factory = new ProjectApiFactory().WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?> { ["Agents:Planner:Provider"] = "Anthropic", ["Agents:Planner:Model"] = "configured-test-model", ["ANTHROPIC_API_KEY"] = "not-returned-test-secret" })));
        using var configuredClient = factory.CreateClient();
        var json = await configuredClient.GetStringAsync("/api/planner/readiness");
        Assert.Contains("Ready", json);
        Assert.DoesNotContain("not-returned-test-secret", json);
    }

    [Fact]
    public async Task AiModels_CatalogueAssignmentsAndProjectOverride_AreScoped()
    {
        var created=await client.PostAsJsonAsync("/api/ai/models",new AiModelProfileRequest("Primary Planner","Anthropic","catalogue-model",null));Assert.Equal(HttpStatusCode.Created,created.StatusCode);var model=(await created.Content.ReadFromJsonAsync<AiModelProfileDto>(JsonOptions))!;
        var duplicate=await client.PostAsJsonAsync("/api/ai/models",new AiModelProfileRequest("Duplicate","Anthropic","catalogue-model",null));Assert.Equal(HttpStatusCode.Conflict,duplicate.StatusCode);
        var projectResponse=await client.PostAsJsonAsync("/api/projects",new CreateProjectRequest("Model Project",null,"https://github.com/example/models","main"));var project=(await projectResponse.Content.ReadFromJsonAsync<ProjectDto>(JsonOptions))!;
        var global=await client.PutAsJsonAsync("/api/ai/role-assignments/Planner",new{aiModelProfileId=model.Id});Assert.Equal(HttpStatusCode.OK,global.StatusCode);var globalEffective=await client.GetFromJsonAsync<List<EffectiveAgentModelDto>>($"/api/projects/{project.Id}/ai/role-assignments",JsonOptions);Assert.Equal(ModelResolutionSource.GlobalDefault,globalEffective!.Single(x=>x.AgentRole==AgentRole.Planner).Source);
        var assignment=await client.PutAsJsonAsync($"/api/projects/{project.Id}/ai/role-assignments/Planner",new{aiModelProfileId=model.Id});Assert.Equal(HttpStatusCode.OK,assignment.StatusCode);
        var effective=await client.GetFromJsonAsync<List<EffectiveAgentModelDto>>($"/api/projects/{project.Id}/ai/role-assignments",JsonOptions);Assert.Equal(ModelResolutionSource.ProjectOverride,effective!.Single(x=>x.AgentRole==AgentRole.Planner).Source);
        var other=await client.GetAsync($"/api/projects/{Guid.NewGuid()}/ai/role-assignments");Assert.Equal(HttpStatusCode.NotFound,other.StatusCode);
        await client.DeleteAsync($"/api/projects/{project.Id}/ai/role-assignments/Planner");var inherited=await client.GetFromJsonAsync<List<EffectiveAgentModelDto>>($"/api/projects/{project.Id}/ai/role-assignments",JsonOptions);Assert.Equal(ModelResolutionSource.GlobalDefault,inherited!.Single(x=>x.AgentRole==AgentRole.Planner).Source);
        await client.PatchAsJsonAsync($"/api/ai/models/{model.Id}/status",new{isEnabled=false});var disabledAssign=await client.PutAsJsonAsync($"/api/ai/role-assignments/Planner",new{aiModelProfileId=model.Id});Assert.Equal(HttpStatusCode.Conflict,disabledAssign.StatusCode);
    }
}

public sealed class ProjectApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IProjectRepository>();
            services.AddSingleton<IProjectRepository, InMemoryProjectRepository>();
            services.RemoveAll<IAiModelConfigurationRepository>();
            services.AddSingleton<IAiModelConfigurationRepository, InMemoryAiModelConfigurationRepository>();
        });
    }
}

internal sealed class InMemoryAiModelConfigurationRepository:IAiModelConfigurationRepository
{
 private readonly List<AiModelProfile> models=[];private readonly List<AgentModelAssignment> assignments=[];
 public Task<IReadOnlyList<AiModelProfile>> ListModelsAsync(CancellationToken ct)=>Task.FromResult<IReadOnlyList<AiModelProfile>>(models.ToList());public Task<AiModelProfile?> GetModelAsync(Guid id,CancellationToken ct)=>Task.FromResult(models.SingleOrDefault(x=>x.Id==id));public Task<AiModelProfile?> FindModelAsync(string p,string i,CancellationToken ct)=>Task.FromResult(models.SingleOrDefault(x=>x.Provider==p&&x.ModelIdentifier==i));public Task AddModelAsync(AiModelProfile m,CancellationToken ct){models.Add(m);return Task.CompletedTask;}public Task<IReadOnlyList<AgentModelAssignment>> ListAssignmentsAsync(Guid? p,CancellationToken ct)=>Task.FromResult<IReadOnlyList<AgentModelAssignment>>(assignments.Where(x=>x.ProjectId==p).ToList());public Task<AgentModelAssignment?> GetAssignmentAsync(AgentRole r,Guid? p,CancellationToken ct)=>Task.FromResult(assignments.SingleOrDefault(x=>x.AgentRole==r&&x.ProjectId==p));public Task AddAssignmentAsync(AgentModelAssignment a,CancellationToken ct){assignments.Add(a);return Task.CompletedTask;}public void RemoveAssignment(AgentModelAssignment a)=>assignments.Remove(a);public Task SaveChangesAsync(CancellationToken ct)=>Task.CompletedTask;
}

internal sealed class InMemoryProjectRepository : IProjectRepository
{
    private readonly List<Project> projects = [];
    public Task AddAsync(Project project, CancellationToken cancellationToken) { lock (projects) projects.Add(project); return Task.CompletedTask; }
    public Task<Project?> GetAsync(Guid id, CancellationToken cancellationToken) { lock (projects) return Task.FromResult(projects.SingleOrDefault(x => x.Id == id)); }
    public Task<IReadOnlyList<Project>> ListAsync(ProjectStatus? status, string? search, CancellationToken cancellationToken)
    {
        lock (projects)
        {
            IEnumerable<Project> query = projects.ToList();
            if (status is not null) query = query.Where(x => x.Status == status);
            if (!string.IsNullOrWhiteSpace(search)) query = query.Where(x => x.Name.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase));
            return Task.FromResult<IReadOnlyList<Project>>(query.OrderBy(x => x.Status).ThenBy(x => x.Name).ToList());
        }
    }
    public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
