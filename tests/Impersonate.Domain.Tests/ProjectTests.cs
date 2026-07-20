using Impersonate.Domain.Projects;
using Xunit;

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

    [Theory]
    [InlineData("", "https://github.com/example/repo", "main")]
    [InlineData("Project", "", "main")]
    [InlineData("Project", "https://github.com/example/repo", "")]
    public void Create_RejectsRequiredValues(string name, string repositoryUrl, string defaultBranch) =>
        Assert.Throws<ArgumentException>(() => Project.Create(name, null, repositoryUrl, defaultBranch));

    [Fact]
    public void Create_RejectsExcessiveLengths() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Project.Create(new string('a', Project.NameMaxLength + 1), null, "https://github.com/example/repo", "main"));

    [Theory]
    [InlineData("http://github.com/example/repo")]
    [InlineData("https://gitlab.com/example/repo")]
    [InlineData("https://github.com/example")]
    [InlineData("https://github.com/example/repo/issues")]
    public void Create_RejectsMalformedRepositoryUrls(string repositoryUrl) =>
        Assert.Throws<ArgumentException>(() => Project.Create("Project", null, repositoryUrl, "main"));

    [Fact]
    public void UpdateDetails_NormalizesValuesAndAdvancesTimestamp()
    {
        var project = Project.Create("Project", null, "https://github.com/example/repo", "main");
        var then = project.UpdatedAtUtc.AddMinutes(1);
        project.UpdateDetails(" Renamed ", " Details ", " https://github.com/example/renamed.git ", " develop ", then);
        Assert.Equal("Renamed", project.Name);
        Assert.Equal("Details", project.Description);
        Assert.Equal("develop", project.DefaultBranch);
        Assert.Equal(then, project.UpdatedAtUtc);
    }

    [Fact]
    public void ChangeStatus_RejectsUndefinedStatus()
    {
        var project = Project.Create("Project", null, "https://github.com/example/repo", "main");
        Assert.Throws<ArgumentOutOfRangeException>(() => project.ChangeStatus((ProjectStatus)99));
    }
}
