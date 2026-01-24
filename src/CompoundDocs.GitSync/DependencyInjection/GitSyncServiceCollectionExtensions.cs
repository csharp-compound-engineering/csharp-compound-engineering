using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CompoundDocs.GitSync.DependencyInjection;

public static class GitSyncServiceCollectionExtensions
{
    public static IServiceCollection AddGitSync(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<GitSyncConfig>(
            configuration.GetSection("CompoundDocs:GitSync"));
        services.AddSingleton<IGitSyncService, GitSyncService>();
        return services;
    }
}
