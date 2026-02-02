var builder = DistributedApplication.CreateBuilder(args);

var mcpServer = builder.AddProject<Projects.CompoundDocs_McpServer>("mcpserver");

builder.Build().Run();
