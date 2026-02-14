using CompoundDocs.GitSync;
using CompoundDocs.GitSync.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CompoundDocs.Tests.Unit.GitSync;

public sealed class GitSyncServiceCollectionExtensionsTests
{
    [Fact]
    public void AddGitSync_ConfiguresGitSyncConfig()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["CompoundDocs:GitSync:CloneBaseDirectory"] = "/custom/repos"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddGitSync(config);

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<GitSyncConfig>>().Value;

        options.CloneBaseDirectory.ShouldBe("/custom/repos");
    }

    [Fact]
    public void AddGitSync_RegistersIGitSyncService()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddGitSync(config);

        var descriptors = services.ToList();
        descriptors.ShouldContain(d => d.ServiceType == typeof(IGitSyncService));
    }
}
