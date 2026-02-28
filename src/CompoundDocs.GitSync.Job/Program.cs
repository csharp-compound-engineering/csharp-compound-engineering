using CompoundDocs.Bedrock.DependencyInjection;
using CompoundDocs.Common.Configuration;
using CompoundDocs.Common.DependencyInjection;
using CompoundDocs.GitSync.DependencyInjection;
using CompoundDocs.GitSync.Job;
using CompoundDocs.Graph.DependencyInjection;
using CompoundDocs.GraphRag.DependencyInjection;
using CompoundDocs.Vector.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(args);
var config = builder.Configuration;

builder.Services.AddCompoundDocsCloudConfig(config);
builder.Services.AddCompoundDocsCommon();
builder.Services.AddBedrockServices(config);
builder.Services.AddNeptuneGraph(config);
builder.Services.AddOpenSearchVector(config);
builder.Services.AddGraphRag();
builder.Services.AddGitSync(config);
builder.Services.AddSingleton<IGitSyncRunner, GitSyncRunner>();

var host = builder.Build();
var runner = host.Services.GetRequiredService<IGitSyncRunner>();
var cloudConfig = host.Services.GetRequiredService<IOptions<CompoundDocsCloudConfig>>().Value;

foreach (var repo in cloudConfig.Repositories)
{
    await runner.RunAsync(repo.Name, CancellationToken.None);
}
