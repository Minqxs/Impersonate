using Impersonate.Domain.Projects;

namespace Impersonate.Domain.Tests;

public sealed class ProjectTests
{
    [Fact]
    public void Create_NormalizesValues_AndDefaultsToIdle()
    {
        var project = Project.Create("  Impersonate  ", " Description ", " https://github.com/example/repo.git ", " main ");
        Assert.Equal("Impersonate", project.Name); Assert.Equal(ProjectStatus.Idle, project.Status); Assert.Equal("https://github.com/example/repo.git", project.RepositoryUrl); Assert.Equal(project.CreatedAtUtc, project.UpdatedAtUtc);
    }
    [Theory] [InlineData("")] [InlineData(" ")]
    public void Create_RejectsMissingName(string name) => Assert.Throws<ArgumentException>(() => Project.Create(name, null, "https://github.com/example/repo", "main"));
    [Fact] public void Create_RejectsNonGithubRepositoryUrl() => Assert.Throws<ArgumentException>(() => Project.Create("Project", null, "https://example.com/repo", "main"));
    [Fact] public void ChangeStatus_UpdatesStatusAndTimestamp() { var project = Project.Create("Project", null, "https://github.com/example/repo", "main"); var then = project.UpdatedAtUtc.AddMinutes(1); project.ChangeStatus(ProjectStatus.Active, then); Assert.Equal(ProjectStatus.Active, project.Status); Assert.Equal(then, project.UpdatedAtUtc); }
}
