using Impersonate.Application;
using Impersonate.Application.Projects;
using Impersonate.Domain.Projects;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Impersonate.Application.Tests;

public sealed class ProjectServiceTests
{
    [Fact]
    public async Task CreateGetUpdateAndChangeStatus_RoundTrip()
    {
        var (service, _) = CreateService();
        var created = await service.CreateAsync(new("Project", null, "https://github.com/example/repo", "main"), default);
        Assert.Equal(ProjectStatus.Idle, created.Status);
        Assert.Equal(created, await service.GetAsync(created.Id, default));

        var updated = await service.UpdateAsync(created.Id, new("Renamed", "Description", "https://github.com/example/renamed", "develop"), default);
        Assert.Equal("Renamed", updated!.Name);
        var active = await service.ChangeStatusAsync(created.Id, ProjectStatus.Active, default);
        Assert.Equal(ProjectStatus.Active, active!.Status);
    }

    [Fact]
    public async Task List_FiltersSearchesAndOrdersByStatusThenName()
    {
        var (service, _) = CreateService();
        var idle = await service.CreateAsync(new("Zulu", null, "https://github.com/example/zulu", "main"), default);
        await service.CreateAsync(new("Alpha", null, "https://github.com/example/alpha", "main", ProjectStatus.Active), default);
        await service.CreateAsync(new("Offline", null, "https://github.com/example/offline", "main", ProjectStatus.Off), default);
        Assert.Equal([ProjectStatus.Active, ProjectStatus.Idle, ProjectStatus.Off], (await service.ListAsync(null, null, default)).Select(x => x.Status));
        Assert.Single(await service.ListAsync(ProjectStatus.Idle, null, default));
        Assert.Equal(idle.Id, Assert.Single(await service.ListAsync(null, "zul", default)).Id);
    }

    [Fact]
    public async Task MissingProject_ReturnsNull()
    {
        var (service, _) = CreateService();
        Assert.Null(await service.GetAsync(Guid.NewGuid(), default));
        Assert.Null(await service.UpdateAsync(Guid.NewGuid(), new("Name", null, "https://github.com/example/repo", "main"), default));
        Assert.Null(await service.ChangeStatusAsync(Guid.NewGuid(), ProjectStatus.Off, default));
        Assert.Null(await service.GetHealthAsync(Guid.NewGuid(), default));
    }

    [Fact]
    public async Task InvalidCreate_IsRejectedBeforePersistence()
    {
        var (service, repository) = CreateService();
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(new("", null, "bad", ""), default));
        Assert.Empty(repository.Projects);
    }

    private static (IProjectService Service, FakeProjectRepository Repository) CreateService()
    {
        var repository = new FakeProjectRepository();
        var services = new ServiceCollection().AddSingleton<IProjectRepository>(repository).AddApplication().BuildServiceProvider();
        return (services.GetRequiredService<IProjectService>(), repository);
    }

    private sealed class FakeProjectRepository : IProjectRepository
    {
        public List<Project> Projects { get; } = [];
        public Task AddAsync(Project project, CancellationToken cancellationToken) { Projects.Add(project); return Task.CompletedTask; }
        public Task<Project?> GetAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(Projects.SingleOrDefault(x => x.Id == id));
        public Task<IReadOnlyList<Project>> ListAsync(ProjectStatus? status, string? search, CancellationToken cancellationToken)
        {
            IEnumerable<Project> query = Projects;
            if (status is not null) query = query.Where(x => x.Status == status);
            if (!string.IsNullOrWhiteSpace(search)) query = query.Where(x => x.Name.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase));
            return Task.FromResult<IReadOnlyList<Project>>(query.OrderBy(x => x.Status).ThenBy(x => x.Name).ToList());
        }
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
