using Microsoft.Extensions.Configuration;

var builder = DistributedApplication.CreateBuilder(args);

// Determine Ollama mode from configuration
// Set Ollama:UseLocal=true to connect to a locally running Ollama instance
// instead of launching one in a container.
var useLocalOllama = builder.Configuration.GetValue<bool>("Ollama:UseLocal", false);

// Add PostgreSQL with pgvector + Liquibase (built from project Dockerfile)
var postgres = builder.AddPostgres("postgres")
    .WithDockerfile("docker/postgres")
    .WithBindMount("./docker/postgres/changelog", "/liquibase/changelog", isReadOnly: true)
    .WithDataVolume("compounddocs-postgres-data")
    .WithPgAdmin();

var postgresdb = postgres.AddDatabase("compounddocs");

// Add the MCP Server project with references to infrastructure
var mcpServer = builder.AddProject<Projects.CompoundDocs_McpServer>("mcpserver")
    .WithReference(postgresdb)
    .WaitFor(postgresdb);

if (useLocalOllama)
{
    // Use a locally running Ollama instance (e.g. `ollama serve` on the host)
    var localHost = builder.Configuration["Ollama:LocalHost"] ?? "127.0.0.1";
    var localPort = builder.Configuration.GetValue<int>("Ollama:LocalPort", 11434);

    mcpServer
        .WithEnvironment("OLLAMA_HOST", localHost)
        .WithEnvironment("OLLAMA_PORT", localPort.ToString());
}
else
{
    // Run Ollama in a container (default behavior)
    var ollama = builder.AddOllama("ollama")
        .WithDataVolume("compounddocs-ollama-data")
        .AddModel("mxbai-embed-large");

    mcpServer
        .WithReference(ollama)
        .WaitFor(ollama);
}

builder.Build().Run();
