using CompoundDocs.Bedrock.DependencyInjection;
using CompoundDocs.Common.Configuration;
using CompoundDocs.GitSync.DependencyInjection;
using CompoundDocs.Graph.DependencyInjection;
using CompoundDocs.Vector.DependencyInjection;
using CompoundDocs.Worker;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .CreateLogger();

try
{
    Log.Information("Starting CompoundDocs Worker");

    var builder = Host.CreateDefaultBuilder(args)
        .UseSerilog()
        .ConfigureServices((context, services) =>
        {
            var config = context.Configuration;

            services.AddCompoundDocsCloudConfig(config);
            services.AddNeptuneGraph(config);
            services.AddOpenSearchVector(config);
            services.AddBedrockServices(config);
            services.AddGitSync(config);

            services.AddHostedService<WorkerService>();
        });

    var host = builder.Build();
    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Worker terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}
