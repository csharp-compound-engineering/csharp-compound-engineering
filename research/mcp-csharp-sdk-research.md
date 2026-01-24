# MCP C# SDK Comprehensive Research Report

**Research Date:** January 21, 2026
**Purpose:** Building an MCP Server for RAG using Semantic Kernel, Ollama, and PostgreSQL

---

## Table of Contents

1. [MCP Protocol Overview](#1-mcp-protocol-overview)
2. [MCP C# SDK Architecture](#2-mcp-c-sdk-architecture)
3. [Server Implementation (PRIMARY FOCUS)](#3-server-implementation)
4. [Tools Implementation](#4-tools-implementation)
5. [Resources Implementation](#5-resources-implementation)
6. [Prompts Implementation](#6-prompts-implementation)
7. [Client Implementation](#7-client-implementation)
8. [Transport Layer](#8-transport-layer)
9. [Error Handling](#9-error-handling)
10. [Semantic Kernel Integration](#10-semantic-kernel-integration)
11. [Complete Code Examples](#11-complete-code-examples)
12. [Sources and References](#12-sources-and-references)

---

## 1. MCP Protocol Overview

### What is the Model Context Protocol?

The Model Context Protocol (MCP) is an open protocol that standardizes how applications provide context to Large Language Models (LLMs). It was introduced by Anthropic and has gained significant traction in the AI ecosystem.

### Core Concepts

- **MCP Hosts**: Programs that want to access data through MCP (e.g., AI assistants, IDE plugins)
- **MCP Clients**: Protocol clients that maintain connections with servers
- **MCP Servers**: Lightweight programs that expose capabilities via the standardized protocol

### Key Features

- **Interoperability**: Standard protocol for LLM-tool communication
- **Flexibility**: Support for multiple transport mechanisms
- **Tool Integration**: Enable AI agents to interact with UIs, APIs, and data sources
- **Context Management**: Standardized way to provide context to LLMs

### Communication Architecture

MCP uses a client-server model where:
1. Clients connect to servers via supported transports
2. Servers expose tools, resources, and prompts
3. LLMs can discover and invoke these capabilities through clients

---

## 2. MCP C# SDK Architecture

### NuGet Packages

The SDK consists of three main NuGet packages:

| Package | Description | Use Case |
|---------|-------------|----------|
| `ModelContextProtocol` | Main package with hosting and DI extensions | Most projects without HTTP server requirements |
| `ModelContextProtocol.AspNetCore` | HTTP-based MCP servers | Web APIs with HTTP/SSE transport |
| `ModelContextProtocol.Core` | Minimal package for low-level APIs | Client-only use cases or custom implementations |

### Installation

```bash
# Main package (recommended for most scenarios)
dotnet add package ModelContextProtocol --prerelease

# For HTTP-based servers
dotnet add package ModelContextProtocol.AspNetCore --prerelease

# For hosting support
dotnet add package Microsoft.Extensions.Hosting
```

### .NET Version Compatibility

- **.NET 8.0** or higher (recommended)
- **.NET 10.0** for the official MCP server template

### Key Namespaces

| Namespace | Purpose |
|-----------|---------|
| `ModelContextProtocol` | Core types, exceptions, utilities |
| `ModelContextProtocol.Server` | Server implementation classes |
| `ModelContextProtocol.Client` | Client implementation classes |
| `ModelContextProtocol.Protocol` | Protocol-level types and contracts |

### Project Status

> **Note**: The project is in preview; breaking changes can be introduced without prior notice.

---

## 3. Server Implementation

### Basic Server Setup

The simplest MCP server uses the hosting pattern with dependency injection:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

var builder = Host.CreateApplicationBuilder(args);

// Configure logging to stderr (required for stdio transport)
builder.Logging.AddConsole(consoleLogOptions =>
{
    consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace;
});

// Add MCP server with stdio transport and auto-discover tools
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();
```

### McpServerBuilder Methods

The `IMcpServerBuilder` interface provides fluent configuration:

```csharp
builder.Services
    .AddMcpServer()
    // Transport configuration
    .WithStdioServerTransport()           // Standard I/O transport
    .WithHttpTransport()                   // HTTP/SSE transport (requires AspNetCore package)

    // Tool registration
    .WithToolsFromAssembly()               // Auto-discover [McpServerToolType] classes
    .WithTools<MyToolsClass>()             // Register specific tool class
    .WithTools(toolInstance)               // Register tool instance
    .WithTools(toolCollection)             // Register collection of tools

    // Resource registration
    .WithResourcesFromAssembly()           // Auto-discover resources
    .WithResources<MyResourcesClass>()     // Register specific resource class

    // Prompt registration
    .WithPromptsFromAssembly()             // Auto-discover prompts
    .WithPrompts<MyPromptsClass>()         // Register specific prompt class

    // Custom handlers
    .WithListToolsHandler(handler)         // Custom tool listing
    .WithCallToolHandler(handler)          // Custom tool invocation
    .WithListResourcesHandler(handler)     // Custom resource listing
    .WithReadResourceHandler(handler);     // Custom resource reading
```

### McpServerOptions Configuration

```csharp
builder.Services.AddOptions<McpServerOptions>()
    .Configure(options =>
    {
        // Server identity
        options.ServerInfo = new() { Name = "MyServer", Version = "1.0.0" };

        // Protocol version (YYYY-MM-DD format)
        options.ProtocolVersion = "2025-06-18";

        // Initialization timeout
        options.InitializationTimeout = TimeSpan.FromSeconds(30);

        // Request scoping (default: true)
        options.ScopeRequests = true;

        // Server instructions (sent as LLM system message)
        options.ServerInstructions = "This server provides RAG capabilities.";

        // Max sampling tokens
        options.MaxSamplingOutputTokens = 1000;

        // Collections
        options.ToolCollection = new McpServerPrimitiveCollection<McpServerTool>();
        options.PromptCollection = new McpServerPrimitiveCollection<McpServerPrompt>();
        options.ResourceCollection = new McpServerResourceCollection();
    });
```

### ASP.NET Core Integration (HTTP Transport)

For HTTP-based MCP servers, use the AspNetCore package:

```csharp
using ModelContextProtocol.Server;

var builder = WebApplication.CreateBuilder(args);

// Add MCP server with HTTP transport
builder.Services
    .AddMcpServer()
    .WithHttpTransport(options =>
    {
        options.IdleTimeout = TimeSpan.FromHours(2);
        options.MaxIdleSessionCount = 100_000;
    })
    .WithToolsFromAssembly();

// Add authentication if needed
builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer();

var app = builder.Build();

// Map MCP endpoints with optional authorization
app.MapMcp("/api/mcp")
    .RequireAuthorization();

app.Run();
```

### HTTP Endpoint Behavior

The `MapMcp()` method creates these endpoints:

| HTTP Method | Path | Content Type | Purpose |
|-------------|------|--------------|---------|
| POST | {pattern} | application/json | Send JSON-RPC requests |
| GET | {pattern} | text/event-stream | Receive SSE events |
| DELETE | {pattern} | - | Close session |

### Session Management

Sessions are automatically created on first request:

1. Server generates a session ID and returns it in the `mcp-session-id` header
2. Client includes this header in subsequent requests
3. Sessions persist across request/response cycles

---

## 4. Tools Implementation

### Attribute-Based Tool Registration

Tools are defined using attributes on methods:

```csharp
using System.ComponentModel;
using ModelContextProtocol.Server;

[McpServerToolType]
public static class MyTools
{
    [McpServerTool]
    [Description("Echoes the message back to the client.")]
    public static string Echo(string message) => $"Echo: {message}";

    [McpServerTool]
    [Description("Reverses the provided text.")]
    public static string ReverseText(
        [Description("The text to reverse")] string text)
    {
        return new string(text.Reverse().ToArray());
    }
}
```

### Async Tools with Dependency Injection

```csharp
[McpServerToolType]
public static class RagTools
{
    [McpServerTool]
    [Description("Searches the knowledge base for relevant documents.")]
    public static async Task<string> SearchKnowledgeBase(
        McpServer server,                    // Injected: MCP server instance
        IVectorStore vectorStore,            // Injected: Custom service from DI
        ILogger<RagTools> logger,            // Injected: Logging
        [Description("The search query")] string query,
        [Description("Max results")] int maxResults = 5,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Searching for: {Query}", query);

        var results = await vectorStore.SearchAsync(query, maxResults, cancellationToken);
        return JsonSerializer.Serialize(results);
    }
}
```

### Tool Parameter Binding

Parameters are automatically bound from the request with these special cases:

| Parameter Type | Source | In Schema? |
|---------------|--------|------------|
| `CancellationToken` | MCP server | No |
| `McpServer` | Server instance | No |
| `IServiceProvider` | Request context | No |
| `IProgress<ProgressNotificationValue>` | Progress notifications | No |
| Services registered in DI | Service provider | No |
| All other parameters | `Arguments` dictionary | Yes |

### Tool Return Types

| Return Type | Behavior |
|-------------|----------|
| `string` | Single text content block |
| `ContentBlock` | Direct content block |
| `IEnumerable<ContentBlock>` | Multiple content blocks |
| `CallToolResult` | Returned unchanged |
| `AIContent` types | Converted to content blocks |
| Other types | JSON-serialized as text |
| `null` | Empty content list |

### Custom Tool Creation

For advanced scenarios, create tools programmatically:

```csharp
var tool = McpServerTool.Create(
    (string input) => $"Processed: {input}",
    new McpServerToolCreateOptions
    {
        Name = "process",
        Description = "Processes the input"
    });

builder.Services.AddSingleton(tool);
```

### Instance-Based Tools

```csharp
public class StatefulTool
{
    private readonly IDatabase _database;

    public StatefulTool(IDatabase database)
    {
        _database = database;
    }

    [McpServerTool]
    [Description("Queries the database")]
    public async Task<string> QueryDatabase(string sql)
    {
        return await _database.ExecuteAsync(sql);
    }
}

// Registration
builder.Services.AddSingleton<StatefulTool>();
builder.Services
    .AddMcpServer()
    .WithTools<StatefulTool>();
```

---

## 5. Resources Implementation

### Attribute-Based Resource Registration

Resources provide URI-addressable data:

```csharp
using System.ComponentModel;
using ModelContextProtocol.Server;

[McpServerResourceType]
public static class MyResources
{
    // Static resource (no URI parameters)
    [McpServerResource]
    [Description("Returns the API documentation URL")]
    public static string GetApiDocs() => "https://api.example.com/docs";

    // Templated resource (with URI parameters)
    [McpServerResource]
    [Description("Returns user profile data")]
    public static async Task<string> GetUserProfile(
        IUserService userService,
        [Description("The user ID")] string userId)
    {
        var user = await userService.GetUserAsync(userId);
        return JsonSerializer.Serialize(user);
    }
}
```

### URI Template Processing

The SDK automatically generates URI templates from method signatures:

```csharp
// Method signature
public static string GetFile(string path, int? maxLines = null)

// Generated URI template: resource://mcp/file/{path}?maxLines={maxLines}
```

### Resource Return Types

| Return Type | Purpose |
|-------------|---------|
| `string` | Text content with default MIME type |
| `ResourceContents` | Direct protocol content control |
| `ReadResourceResult` | Complete protocol response |
| `TextContent` / `DataContent` | AI framework integration |
| `IEnumerable<ResourceContents>` | Multiple content blocks |

### Custom Resource Handlers

```csharp
builder.Services
    .AddMcpServer()
    .WithListResourcesHandler(async (request, ct) =>
    {
        return new ListResourcesResult
        {
            Resources = new List<Resource>
            {
                new() { Uri = "resource://db/schema", Name = "Database Schema" },
                new() { Uri = "resource://docs/readme", Name = "README" }
            }
        };
    })
    .WithReadResourceHandler(async (request, ct) =>
    {
        var uri = request.Params.Uri;
        var content = await LoadResourceContent(uri);

        return new ReadResourceResult
        {
            Contents = new List<ResourceContents>
            {
                new TextResourceContents { Text = content }
            }
        };
    });
```

---

## 6. Prompts Implementation

### Attribute-Based Prompt Registration

Prompts are template-based conversation starters:

```csharp
using System.ComponentModel;
using ModelContextProtocol.Server;
using Microsoft.Extensions.AI;

[McpServerPromptType]
public static class MyPrompts
{
    [McpServerPrompt]
    [Description("Creates a prompt to summarize content")]
    public static string Summarize(
        [Description("The content to summarize")] string content)
    {
        return $"Please summarize this content into a single sentence: {content}";
    }

    [McpServerPrompt]
    [Description("Creates a code review prompt")]
    public static ChatMessage CodeReview(
        [Description("Programming language")] string language,
        [Description("Code to review")] string code)
    {
        return new ChatMessage(
            ChatRole.User,
            $"Review this {language} code for best practices and potential issues:\n\n{code}");
    }
}
```

### Prompt Return Types

| Return Type | Use Case |
|-------------|----------|
| `string` | Converted to single user message |
| `ChatMessage` | Microsoft.Extensions.AI integration |
| `PromptMessage` | Direct protocol control |
| `IEnumerable<PromptMessage>` | Multi-turn conversations |
| `GetPromptResult` | Complete response control |

### Custom Prompt Handlers

```csharp
builder.Services
    .AddMcpServer()
    .WithListPromptsHandler(async (request, ct) =>
    {
        return new ListPromptsResult
        {
            Prompts = new List<Prompt>
            {
                new()
                {
                    Name = "rag_query",
                    Description = "Query the knowledge base",
                    Arguments = new List<PromptArgument>
                    {
                        new() { Name = "query", Description = "Search query", Required = true }
                    }
                }
            }
        };
    })
    .WithGetPromptHandler(async (request, ct) =>
    {
        var query = request.Params.Arguments?["query"]?.ToString();

        return new GetPromptResult
        {
            Messages = new List<PromptMessage>
            {
                new()
                {
                    Role = Role.User,
                    Content = new TextContent { Text = $"Search the knowledge base for: {query}" }
                }
            }
        };
    });
```

---

## 7. Client Implementation

### Basic Client Setup

```csharp
using ModelContextProtocol.Client;

// Create MCP client with stdio transport
await using IMcpClient mcpClient = await McpClientFactory.CreateAsync(
    new StdioClientTransport(new()
    {
        Command = "dotnet",
        Arguments = ["run", "--project", "path/to/McpServer.csproj"],
        Name = "My MCP Server"
    }));
```

### Client Configuration Options

```csharp
var clientOptions = new McpClientOptions
{
    ClientInfo = new() { Name = "demo-client", Version = "1.0.0" }
};

var serverConfig = new McpServerConfig
{
    Id = "demo-server",
    Name = "Demo Server",
    TransportType = TransportTypes.StdIo,
    TransportOptions = new Dictionary<string, string>
    {
        ["command"] = @"path\to\McpServer.exe"
    }
};

await using var mcpClient = await McpClientFactory.CreateAsync(
    serverConfig,
    clientOptions,
    loggerFactory: loggerFactory);
```

### Listing and Invoking Tools

```csharp
// List available tools
IList<McpClientTool> tools = await mcpClient.ListToolsAsync();
foreach (McpClientTool tool in tools)
{
    Console.WriteLine($"Tool: {tool.Name} - {tool.Description}");
}

// Call a tool
var result = await mcpClient.CallToolAsync(
    "search_documents",
    new Dictionary<string, object?>
    {
        ["query"] = "machine learning",
        ["maxResults"] = 10
    });

Console.WriteLine(result.Content);
```

### Reading Resources

```csharp
// List resources
var resources = await mcpClient.ListResourcesAsync();

// Read a specific resource
var content = await mcpClient.ReadResourceAsync("resource://db/schema");
```

### HTTP Client Transport

```csharp
await using var mcpClient = await McpClientFactory.CreateAsync(
    new HttpClientTransport(new HttpClientTransportOptions
    {
        Endpoint = new Uri("https://mcp-server.example.com/api/mcp"),
        HttpClient = httpClient,
        TransportMode = HttpTransportMode.StreamableHttp
    }));
```

---

## 8. Transport Layer

### Available Transports

| Transport | Class | Use Case |
|-----------|-------|----------|
| Standard I/O | `StdioServerTransport` / `StdioClientTransport` | Local process communication |
| HTTP/SSE | `StreamableHttpServerTransport` / `HttpClientTransport` | Remote server access |
| Stream | `StreamServerTransport` | Custom stream-based communication |

### Stdio Transport (Default)

Best for local tool servers and CLI applications:

```csharp
// Server
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport();

// Client
var transport = new StdioClientTransport(new()
{
    Name = "My Server",
    Command = "dotnet",
    Arguments = ["run", "--project", "server.csproj"]
});
```

### HTTP/SSE Transport

For web-based MCP servers:

```csharp
// Server (requires ModelContextProtocol.AspNetCore)
builder.Services
    .AddMcpServer()
    .WithHttpTransport(options =>
    {
        options.IdleTimeout = TimeSpan.FromHours(2);
        options.MaxIdleSessionCount = 100_000;
    });

// Client
var transport = new HttpClientTransport(new()
{
    Endpoint = new Uri("https://server.example.com/mcp"),
    TransportMode = HttpTransportMode.StreamableHttp
});
```

### StreamableHttpServerTransport Properties

| Property | Type | Purpose |
|----------|------|---------|
| `SessionId` | `string?` | Associates messages with a specific session |
| `Stateless` | `bool` | Enables load-balanced deployments |
| `MessageReader` | `ChannelReader<JsonRpcMessage>` | Access to incoming messages |

### Stateless Mode

For horizontally-scaled environments:

```csharp
builder.Services
    .AddMcpServer()
    .WithHttpTransport(options =>
    {
        options.Stateless = true; // Disables unsolicited server-to-client messages
    });
```

---

## 9. Error Handling

### Exception Types

| Exception | Purpose |
|-----------|---------|
| `McpException` | Base exception for MCP errors |
| `McpProtocolException` | Protocol-level errors with error codes |
| `McpTransportException` | Transport layer issues |
| `McpToolExecutionException` | Errors during tool execution |

### McpErrorCode Enum

Standard JSON-RPC error codes:

```csharp
public enum McpErrorCode
{
    ParseError = -32700,
    InvalidRequest = -32600,
    MethodNotFound = -32601,
    InvalidParams = -32602,
    InternalError = -32603,
    // Additional MCP-specific codes...
}
```

### Throwing Validation Errors

```csharp
[McpServerTool]
public static string ProcessData(string data)
{
    if (string.IsNullOrEmpty(data))
    {
        throw new McpProtocolException(
            "Missing required argument 'data'",
            McpErrorCode.InvalidParams);
    }

    return ProcessInternal(data);
}
```

### Error Handling Best Practices

1. **Validate Early**: Check inputs before processing
2. **Catch Specific First**: Handle known exceptions with targeted responses
3. **Log Internally**: Record full error details for debugging
4. **Sanitize Responses**: Return user-safe messages without system information

```csharp
[McpServerTool]
public static async Task<string> SafeOperation(string input)
{
    try
    {
        // Validate input
        if (string.IsNullOrWhiteSpace(input))
            throw new McpProtocolException("Input is required", McpErrorCode.InvalidParams);

        return await DoWorkAsync(input);
    }
    catch (McpProtocolException)
    {
        throw; // Re-throw protocol exceptions
    }
    catch (Exception ex)
    {
        // Log the full exception internally
        logger.LogError(ex, "Error in SafeOperation");

        // Return sanitized error to client
        throw new McpException("An error occurred processing your request");
    }
}
```

---

## 10. Semantic Kernel Integration

### Why Use Semantic Kernel with MCP?

1. **Interoperability**: Expose SK plugins as MCP tools for non-SK applications
2. **Content Safety**: Validate tool calls using SK Filters
3. **Observability**: Collect logs, traces, and metrics through SK infrastructure

### Exposing SK Plugins as MCP Tools

```csharp
using Microsoft.SemanticKernel;
using ModelContextProtocol.Server;

// Define SK plugins
internal sealed class DateTimePlugin
{
    [KernelFunction]
    [Description("Gets the current date and time in UTC")]
    public static string GetCurrentDateTimeUtc()
    {
        return DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
    }
}

// Create kernel and add plugins
IKernelBuilder kernelBuilder = Kernel.CreateBuilder();
kernelBuilder.Plugins.AddFromType<DateTimePlugin>();
Kernel kernel = kernelBuilder.Build();

// Create MCP server with SK plugins
var builder = Host.CreateEmptyApplicationBuilder(settings: null);
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools(kernel.Plugins); // Extension method to convert SK plugins to MCP tools

await builder.Build().RunAsync();
```

### Extension Method for SK Plugin Integration

```csharp
public static class McpServerBuilderExtensions
{
    public static IMcpServerBuilder WithTools(
        this IMcpServerBuilder builder,
        KernelPluginCollection plugins)
    {
        foreach (var plugin in plugins)
        {
            foreach (var function in plugin)
            {
                builder.Services.AddSingleton(services =>
                    McpServerTool.Create(function.AsAIFunction()));
            }
        }
        return builder;
    }
}
```

### Consuming MCP Tools in Semantic Kernel

```csharp
using Microsoft.SemanticKernel;
using ModelContextProtocol.Client;

// Create MCP client
await using IMcpClient mcpClient = await McpClientFactory.CreateAsync(
    new StdioClientTransport(new()
    {
        Name = "RAG Server",
        Command = "path/to/rag-server.exe"
    }));

// Get tools from MCP server
var tools = await mcpClient.ListToolsAsync();

// Create kernel and add MCP tools as functions
var kernelBuilder = Kernel.CreateBuilder();
kernelBuilder.Services.AddOpenAIChatCompletion(
    modelId: "gpt-4o",
    apiKey: apiKey);

kernelBuilder.Plugins.AddFromFunctions(
    "MCPTools",
    tools.Select(t => t.AsKernelFunction()));

var kernel = kernelBuilder.Build();

// Use with automatic function calling
var settings = new OpenAIPromptExecutionSettings
{
    Temperature = 0,
    FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(
        options: new() { RetainArgumentTypes = true })
};

var result = await kernel.InvokePromptAsync(
    "Search for documents about machine learning",
    new(settings));
```

### Using with SK Agents

```csharp
ChatCompletionAgent agent = new()
{
    Instructions = "You are a helpful assistant with access to a knowledge base.",
    Name = "RagAgent",
    Kernel = kernel,
    Arguments = new KernelArguments(new PromptExecutionSettings
    {
        FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(
            options: new() { RetainArgumentTypes = true })
    })
};

var response = await agent.InvokeAsync(
    "What documents do we have about PostgreSQL?").FirstAsync();
```

---

## 11. Complete Code Examples

### Example 1: RAG MCP Server with PostgreSQL and Ollama

```csharp
// Program.cs
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using Npgsql;
using OllamaSharp;

var builder = Host.CreateApplicationBuilder(args);

// Configure logging
builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Trace;
});

// Register services
builder.Services.AddSingleton<NpgsqlDataSource>(sp =>
{
    var connectionString = builder.Configuration.GetConnectionString("PostgreSQL");
    return NpgsqlDataSource.Create(connectionString!);
});

builder.Services.AddSingleton<IOllamaApiClient>(sp =>
{
    var ollamaUri = new Uri(builder.Configuration["Ollama:Endpoint"] ?? "http://localhost:11434");
    return new OllamaApiClient(ollamaUri);
});

builder.Services.AddSingleton<IVectorStore, PostgresVectorStore>();
builder.Services.AddSingleton<IEmbeddingGenerator, OllamaEmbeddingGenerator>();

// Configure MCP server
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly()
    .WithResourcesFromAssembly()
    .WithPromptsFromAssembly();

await builder.Build().RunAsync();
```

```csharp
// RagTools.cs
using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;

[McpServerToolType]
public static class RagTools
{
    [McpServerTool]
    [Description("Searches the knowledge base using semantic similarity.")]
    public static async Task<string> SemanticSearch(
        IVectorStore vectorStore,
        IEmbeddingGenerator embedder,
        ILogger<RagTools> logger,
        [Description("The search query")] string query,
        [Description("Maximum number of results")] int maxResults = 5,
        [Description("Minimum similarity score (0-1)")] double minScore = 0.7,
        CancellationToken ct = default)
    {
        logger.LogInformation("Semantic search: {Query}", query);

        // Generate embedding for query
        var queryEmbedding = await embedder.GenerateEmbeddingAsync(query, ct);

        // Search vector store
        var results = await vectorStore.SearchAsync(
            queryEmbedding,
            maxResults,
            minScore,
            ct);

        return JsonSerializer.Serialize(results, new JsonSerializerOptions
        {
            WriteIndented = true
        });
    }

    [McpServerTool]
    [Description("Indexes a document into the knowledge base.")]
    public static async Task<string> IndexDocument(
        IVectorStore vectorStore,
        IEmbeddingGenerator embedder,
        ILogger<RagTools> logger,
        [Description("Document content to index")] string content,
        [Description("Document metadata (JSON)")] string? metadata = null,
        CancellationToken ct = default)
    {
        logger.LogInformation("Indexing document: {Length} chars", content.Length);

        var embedding = await embedder.GenerateEmbeddingAsync(content, ct);

        var doc = new Document
        {
            Content = content,
            Embedding = embedding,
            Metadata = metadata != null
                ? JsonSerializer.Deserialize<Dictionary<string, object>>(metadata)
                : new()
        };

        var id = await vectorStore.IndexAsync(doc, ct);

        return JsonSerializer.Serialize(new { success = true, documentId = id });
    }

    [McpServerTool]
    [Description("Generates an answer using RAG (retrieval-augmented generation).")]
    public static async Task<string> RagQuery(
        IVectorStore vectorStore,
        IEmbeddingGenerator embedder,
        IOllamaApiClient ollama,
        ILogger<RagTools> logger,
        [Description("The question to answer")] string question,
        [Description("Number of context documents")] int contextDocs = 3,
        [Description("LLM model to use")] string model = "llama3.2",
        CancellationToken ct = default)
    {
        logger.LogInformation("RAG query: {Question}", question);

        // Retrieve relevant documents
        var queryEmbedding = await embedder.GenerateEmbeddingAsync(question, ct);
        var docs = await vectorStore.SearchAsync(queryEmbedding, contextDocs, 0.5, ct);

        // Build context
        var context = string.Join("\n\n---\n\n", docs.Select(d => d.Content));

        // Generate response with Ollama
        var prompt = $"""
            Based on the following context, answer the question.
            If the answer is not in the context, say so.

            Context:
            {context}

            Question: {question}

            Answer:
            """;

        var response = await ollama.GenerateAsync(model, prompt, ct);

        return response.Response ?? "No response generated.";
    }
}
```

```csharp
// RagResources.cs
using System.ComponentModel;
using ModelContextProtocol.Server;

[McpServerResourceType]
public static class RagResources
{
    [McpServerResource]
    [Description("Returns the database schema for the vector store.")]
    public static async Task<string> GetDatabaseSchema(
        NpgsqlDataSource dataSource,
        CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();

        cmd.CommandText = """
            SELECT table_name, column_name, data_type
            FROM information_schema.columns
            WHERE table_schema = 'public'
            ORDER BY table_name, ordinal_position;
            """;

        var results = new List<object>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            results.Add(new
            {
                Table = reader.GetString(0),
                Column = reader.GetString(1),
                Type = reader.GetString(2)
            });
        }

        return JsonSerializer.Serialize(results, new JsonSerializerOptions
        {
            WriteIndented = true
        });
    }

    [McpServerResource]
    [Description("Returns statistics about the knowledge base.")]
    public static async Task<string> GetKnowledgeBaseStats(
        IVectorStore vectorStore,
        CancellationToken ct = default)
    {
        var stats = await vectorStore.GetStatisticsAsync(ct);
        return JsonSerializer.Serialize(stats, new JsonSerializerOptions
        {
            WriteIndented = true
        });
    }
}
```

```csharp
// RagPrompts.cs
using System.ComponentModel;
using ModelContextProtocol.Server;
using Microsoft.Extensions.AI;

[McpServerPromptType]
public static class RagPrompts
{
    [McpServerPrompt]
    [Description("Creates a prompt for question-answering with context.")]
    public static string QuestionAnswering(
        [Description("The context documents")] string context,
        [Description("The user's question")] string question)
    {
        return $"""
            You are a helpful assistant that answers questions based on provided context.

            Context:
            {context}

            Question: {question}

            Instructions:
            - Answer based only on the provided context
            - If the answer is not in the context, say "I don't have enough information"
            - Cite relevant parts of the context in your answer
            - Be concise but thorough
            """;
    }

    [McpServerPrompt]
    [Description("Creates a prompt for document summarization.")]
    public static ChatMessage Summarize(
        [Description("The document to summarize")] string document,
        [Description("Maximum summary length")] int maxWords = 100)
    {
        return new ChatMessage(ChatRole.User, $"""
            Summarize the following document in {maxWords} words or less:

            {document}
            """);
    }
}
```

### Example 2: ASP.NET Core HTTP MCP Server

```csharp
// Program.cs
using ModelContextProtocol.Server;
using Microsoft.AspNetCore.Authentication.JwtBearer;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddSingleton<IVectorStore, PostgresVectorStore>();
builder.Services.AddHttpClient();

// Configure authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["Auth:Authority"];
        options.Audience = builder.Configuration["Auth:Audience"];
    });

builder.Services.AddAuthorization();

// Configure MCP server with HTTP transport
builder.Services
    .AddMcpServer()
    .WithHttpTransport(options =>
    {
        options.IdleTimeout = TimeSpan.FromHours(2);
        options.MaxIdleSessionCount = 10_000;
    })
    .WithToolsFromAssembly()
    .WithResourcesFromAssembly();

var app = builder.Build();

// Configure middleware
app.UseAuthentication();
app.UseAuthorization();

// Map MCP endpoints with authorization
app.MapMcp("/api/mcp")
    .RequireAuthorization();

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.Run();
```

### Example 3: VS Code Configuration

Create `.vscode/mcp.json` for development:

```json
{
  "servers": {
    "RagMcpServer": {
      "type": "stdio",
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "src/RagMcpServer/RagMcpServer.csproj"
      ],
      "env": {
        "ConnectionStrings__PostgreSQL": "Host=localhost;Database=rag;Username=postgres;Password=secret",
        "Ollama__Endpoint": "http://localhost:11434"
      }
    }
  }
}
```

### Example 4: MCP Client Integration

```csharp
// MCP Client using the RAG server
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using Azure.AI.OpenAI;
using Azure.Identity;

// Create chat client (Azure OpenAI)
IChatClient chatClient = new ChatClientBuilder(
    new AzureOpenAIClient(
        new Uri("https://your-openai.openai.azure.com/"),
        new DefaultAzureCredential())
        .GetChatClient("gpt-4o")
        .AsIChatClient())
    .UseFunctionInvocation()
    .Build();

// Create MCP client
await using IMcpClient mcpClient = await McpClientFactory.CreateAsync(
    new StdioClientTransport(new()
    {
        Command = "dotnet",
        Arguments = ["run", "--project", "src/RagMcpServer/RagMcpServer.csproj"],
        Name = "RAG MCP Server"
    }));

// Get available tools
var tools = await mcpClient.ListToolsAsync();
Console.WriteLine("Available tools:");
foreach (var tool in tools)
{
    Console.WriteLine($"  - {tool.Name}: {tool.Description}");
}

// Interactive chat loop
List<ChatMessage> messages = [];
while (true)
{
    Console.Write("\nYou: ");
    var input = Console.ReadLine();
    if (string.IsNullOrEmpty(input)) break;

    messages.Add(new ChatMessage(ChatRole.User, input));

    Console.Write("Assistant: ");
    var updates = new List<ChatResponseUpdate>();

    await foreach (var update in chatClient.GetStreamingResponseAsync(
        messages,
        new ChatOptions { Tools = [.. tools] }))
    {
        Console.Write(update.Text);
        updates.Add(update);
    }

    Console.WriteLine();
    messages.AddMessages(updates);
}
```

---

## 12. Sources and References

### Official Documentation

- [GitHub - MCP C# SDK](https://github.com/modelcontextprotocol/csharp-sdk)
- [MCP C# SDK API Documentation](https://modelcontextprotocol.github.io/csharp-sdk/api/ModelContextProtocol.html)
- [MCP Server API Documentation](https://modelcontextprotocol.github.io/csharp-sdk/api/ModelContextProtocol.Server.html)
- [MCP Client API Documentation](https://modelcontextprotocol.github.io/csharp-sdk/api/ModelContextProtocol.Client.html)

### Microsoft Documentation

- [Build a Model Context Protocol (MCP) Server in C# - .NET Blog](https://devblogs.microsoft.com/dotnet/build-a-model-context-protocol-mcp-server-in-csharp/)
- [Create a Minimal MCP Server using C# - Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/ai/quickstarts/build-mcp-server)
- [Create a Minimal MCP Client using .NET - Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/ai/quickstarts/build-mcp-client)
- [MCP C# SDK Gets Major Update - .NET Blog](https://devblogs.microsoft.com/dotnet/mcp-csharp-sdk-2025-06-18-update/)

### Semantic Kernel Integration

- [Integrating MCP Tools with Semantic Kernel - Semantic Kernel Blog](https://devblogs.microsoft.com/semantic-kernel/integrating-model-context-protocol-tools-with-semantic-kernel-a-step-by-step-guide/)
- [Building an MCP Server with Semantic Kernel - Semantic Kernel Blog](https://devblogs.microsoft.com/semantic-kernel/building-a-model-context-protocol-server-with-semantic-kernel/)
- [GitHub - mcp-with-semantic-kernel](https://github.com/LiteObject/mcp-with-semantic-kernel)

### Community Resources

- [DeepWiki - MCP C# SDK Server Prompts and Resources](https://deepwiki.com/modelcontextprotocol/csharp-sdk/2.2-server-prompts)
- [DeepWiki - Server Configuration and Builder](https://deepwiki.com/modelcontextprotocol/csharp-sdk/2.4-server-configuration-and-builder)
- [DeepWiki - ASP.NET Core Integration](https://deepwiki.com/donaldmucci/mcp-csharp-sdk/5-asp.net-core-integration)
- [McpServerOptions API Documentation](https://modelcontextprotocol.github.io/csharp-sdk/api/ModelContextProtocol.Server.McpServerOptions.html)
- [McpServerTool API Documentation](https://modelcontextprotocol.github.io/csharp-sdk/api/ModelContextProtocol.Server.McpServerTool.html)
- [StreamableHttpServerTransport API Documentation](https://modelcontextprotocol.github.io/csharp-sdk/api/ModelContextProtocol.Server.StreamableHttpServerTransport.html)

### Transport Documentation

- [MCP Transports - Model Context Protocol](https://modelcontextprotocol.io/legacy/concepts/transports)
- [MCP Server Transports - Roo Code Documentation](https://docs.roocode.com/features/mcp/server-transports)

### Error Handling

- [Error Handling in MCP Servers - MCPcat](https://mcpcat.io/guides/error-handling-custom-mcp-servers/)
- [Error Handling and Debugging MCP Servers - Stainless](https://www.stainless.com/mcp/error-handling-and-debugging-mcp-servers)

---

## Appendix: Quick Reference

### NuGet Packages

```bash
dotnet add package ModelContextProtocol --prerelease
dotnet add package ModelContextProtocol.AspNetCore --prerelease
dotnet add package Microsoft.Extensions.Hosting
dotnet add package Microsoft.SemanticKernel
```

### Key Attributes

| Attribute | Purpose |
|-----------|---------|
| `[McpServerToolType]` | Marks a class containing MCP tools |
| `[McpServerTool]` | Marks a method as an MCP tool |
| `[McpServerResourceType]` | Marks a class containing MCP resources |
| `[McpServerResource]` | Marks a method as an MCP resource |
| `[McpServerPromptType]` | Marks a class containing MCP prompts |
| `[McpServerPrompt]` | Marks a method as an MCP prompt |
| `[Description]` | Provides description for tools/parameters |

### Builder Pattern Summary

```csharp
builder.Services
    .AddMcpServer()
    // Transport (choose one)
    .WithStdioServerTransport()
    .WithHttpTransport()
    // Tools
    .WithToolsFromAssembly()
    .WithTools<T>()
    // Resources
    .WithResourcesFromAssembly()
    .WithResources<T>()
    // Prompts
    .WithPromptsFromAssembly()
    .WithPrompts<T>();
```

### Common Injected Services

| Type | Description |
|------|-------------|
| `McpServer` | Current MCP server instance |
| `CancellationToken` | Request cancellation token |
| `ILogger<T>` | Logging service |
| `IProgress<ProgressNotificationValue>` | Progress reporting |
| Custom services | Any service registered in DI |
