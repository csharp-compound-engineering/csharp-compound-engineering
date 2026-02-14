using CompoundDocs.GitSync;

namespace CompoundDocs.Tests.Unit.GitSync;

public sealed class GitSyncConfigTests
{
    [Fact]
    public void DefaultCloneBaseDirectory_ContainsCompoundDocsRepos()
    {
        var config = new GitSyncConfig();
        config.CloneBaseDirectory.ShouldContain("compound-docs-repos");
    }

    [Fact]
    public void CloneBaseDirectory_CanBeOverridden()
    {
        var config = new GitSyncConfig
        {
            CloneBaseDirectory = "/custom/path"
        };
        config.CloneBaseDirectory.ShouldBe("/custom/path");
    }
}
