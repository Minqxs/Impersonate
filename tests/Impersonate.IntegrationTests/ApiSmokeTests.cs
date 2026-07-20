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
        });
    }
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
