using Impersonate.Application.Delivery;
using Xunit;

namespace Impersonate.Application.Tests;

public sealed class TaskBranchNameGeneratorTests
{
    [Fact]
    public void Branch_is_deterministic_task_scoped_and_patch_sensitive()
    {
        var run = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var first = TaskBranchNameGenerator.Create(run, 2, "Add API endpoint!", "abcdef0123456789");
        Assert.Equal(first, TaskBranchNameGenerator.Create(run, 2, "Add API endpoint!", "abcdef0123456789"));
        Assert.Contains("/002-add-api-endpoint-abcdef01", first);
        Assert.NotEqual(first, TaskBranchNameGenerator.Create(run, 3, "Add API endpoint!", "abcdef0123456789"));
        Assert.NotEqual(first, TaskBranchNameGenerator.Create(run, 2, "Add API endpoint!", "1234567823456789"));
    }
}
