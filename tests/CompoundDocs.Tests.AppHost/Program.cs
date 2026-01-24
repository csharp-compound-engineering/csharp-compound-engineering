var builder = DistributedApplication.CreateBuilder(args);

// Neo4j as Neptune stand-in (openCypher via Bolt protocol)
var neo4j = builder.AddContainer("neo4j", "neo4j", "5-community")
    .WithEnvironment("NEO4J_AUTH", "neo4j/testpassword")
    .WithHttpEndpoint(targetPort: 7474, name: "http")
    .WithEndpoint(targetPort: 7687, name: "bolt", scheme: "tcp");

// OpenSearch with k-NN plugin (included by default in official image)
var opensearch = builder.AddContainer("opensearch", "opensearchproject/opensearch", "2.19.0")
    .WithEnvironment("discovery.type", "single-node")
    .WithEnvironment("DISABLE_SECURITY_PLUGIN", "true")
    .WithEnvironment("OPENSEARCH_INITIAL_ADMIN_PASSWORD", "Test_Pass1!")
    .WithHttpEndpoint(targetPort: 9200, name: "http");

// WireMock as Bedrock stub (mounted JSON response fixtures)
var wiremock = builder.AddContainer("bedrock-mock", "wiremock/wiremock", "latest")
    .WithBindMount("../TestFixtures/wiremock", "/home/wiremock")
    .WithHttpEndpoint(targetPort: 8080, name: "http");

// MCP Server project under test — references all backends
var mcpServer = builder.AddProject<Projects.CompoundDocs_McpServer>("mcp-server")
    .WithReference(neo4j.GetEndpoint("bolt"))
    .WithReference(opensearch.GetEndpoint("http"))
    .WithReference(wiremock.GetEndpoint("http"))
    .WithEnvironment("Bedrock__ServiceURL", wiremock.GetEndpoint("http"));

builder.Build().Run();
