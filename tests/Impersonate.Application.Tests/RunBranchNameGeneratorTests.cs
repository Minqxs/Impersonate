using Impersonate.Application.Delivery;
using Xunit;

namespace Impersonate.Application.Tests;

public sealed class RunBranchNameGeneratorTests
{
    [Fact]
    public void Name_is_deterministic_run_scoped_and_a_valid_bounded_ref()
    {
        var run = Guid.NewGuid();
        var first = RunBranchNameGenerator.Create(run, "Feature Name / with punctuation");
        Assert.Equal(first, RunBranchNameGenerator.Create(run, "Feature Name / with punctuation"));
        Assert.NotEqual(first, RunBranchNameGenerator.Create(Guid.NewGuid(), "Feature Name / with punctuation"));
        Assert.StartsWith("impersonate/run-", first);
        Assert.DoesNotContain(' ', first);
        Assert.True(first.Length <= 250);
    }
}
