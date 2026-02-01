using CompoundDocs.Cleanup;
using CompoundDocs.Common.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

try
{
    var builder = Host.CreateApplicationBuilder(args);

    // Check for --once flag
    var runOnce = args.Contains("--once");
    var dryRun = args.Contains("--dry-run");

    // Configure services
    builder.Services.Configure<CleanupOptions>(options =>
    {
        options.RunOnce = runOnce;
        options.DryRun = dryRun;
        options.IntervalMinutes = builder.Configuration.GetValue("Cleanup:IntervalMinutes", 60);
        options.GracePeriodMinutes = builder.Configuration.GetValue("Cleanup:GracePeriodMinutes", 0);
    });

    // Register configuration loader
    builder.Services.AddSingleton<ConfigurationLoader>();

    // Register NpgsqlDataSource
    builder.Services.AddSingleton(sp =>
    {
        var configLoader = sp.GetRequiredService<ConfigurationLoader>();
        var globalConfig = configLoader.LoadGlobalConfig();
        var connectionString = globalConfig.Postgres.GetConnectionString();
        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
        return dataSourceBuilder.Build();
    });

    // Register the cleanup worker
    builder.Services.AddHostedService<CleanupWorker>();

    builder.Services.AddSerilog();

    var host = builder.Build();
    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
    return 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}

return 0;
