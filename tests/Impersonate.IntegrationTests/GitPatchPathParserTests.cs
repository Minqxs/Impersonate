using Impersonate.Infrastructure.Delivery;
using Xunit;

namespace Impersonate.IntegrationTests;

public sealed class GitPatchPathParserTests
{
    [Fact]
    public void Parses_exact_live_crlf_nested_path()
    {
        const string path = "backend/src/HomeTaskSA.Domain/Entities/User.cs";
        var patch = $"diff --git a/{path} b/{path}\r\n--- a/{path}\r\n+++ b/{path}\r\n@@ -1 +1 @@\r\n-before\r\n+after\r\n";
        Assert.Equal([path], GitPatchPathParser.Parse(patch));
    }

    [Theory]
    [InlineData("folder/file.cs")]
    [InlineData("folder/file with spaces.cs")]
    public void Parses_modified_added_and_deleted_paths(string path)
    {
        var header = $"diff --git a/{path} b/{path}\n";
        Assert.Equal([path], GitPatchPathParser.Parse(header + $"--- a/{path}\n+++ b/{path}\n"));
        Assert.Equal([path], GitPatchPathParser.Parse(header + $"--- /dev/null\n+++ b/{path}\n"));
        Assert.Equal([path], GitPatchPathParser.Parse(header + $"--- a/{path}\n+++ /dev/null\n"));
    }

    [Fact]
    public void Decodes_git_quoted_utf8_path_and_rejects_traversal()
    {
        Assert.Equal(["café.cs"], GitPatchPathParser.Parse("diff --git \"a/caf\\303\\251.cs\" \"b/caf\\303\\251.cs\"\n"));
        Assert.Throws<InvalidOperationException>(() => GitPatchPathParser.Parse("diff --git a/../secret b/../secret\n"));
    }

    [Theory]
    [InlineData("staged")]
    [InlineData("commit")]
    public void Exact_file_set_reports_stage_missing_and_unexpected_paths(string stage)
    {
        var error = Assert.Throws<InvalidOperationException>(() => RepositoryFileSetVerifier.Verify(stage, ["approved.cs", "unexpected.cs"], ["approved.cs", "missing.cs"]));
        Assert.StartsWith($"delivery_{stage}_file_set_mismatch:", error.Message);
        Assert.Contains("category=delivery_changed_files_mismatch", error.Message);
        Assert.Contains("missing=[missing.cs]", error.Message);
        Assert.Contains("unexpected=[unexpected.cs]", error.Message);
    }

    [Fact]
    public void Rename_is_rejected_by_explicit_delivery_policy()
    {
        var error = Assert.Throws<InvalidOperationException>(() => GitPatchPathParser.Parse("diff --git a/old.cs b/new.cs\nsimilarity index 100%\nrename from old.cs\nrename to new.cs\n"));
        Assert.Equal("delivery_patch_path_unsafe", error.Message);
    }
}
