# Microsoft Semantic Kernel SDK Research Report

## Executive Summary

This comprehensive research report covers the Microsoft Semantic Kernel SDK for building an MCP Server that serves RAG (Retrieval Augmented Generation) using Ollama for embeddings and PostgreSQL as the vector database. The research is based on official Microsoft documentation, developer blogs, and community resources.

> **UPDATE (January 24, 2026):** This document has been updated to reflect the latest API changes. Key changes include:
> - Package renamed from `Microsoft.SemanticKernel.Connectors.Postgres` to `Microsoft.SemanticKernel.Connectors.PgVector`
> - Class renamed from `PostgresVectorStoreRecordCollection` to `PostgresCollection<TKey, TRecord>`
> - Method renamed from `CreateCollectionIfNotExistsAsync` to `EnsureCollectionExistsAsync`
> See [semantic-kernel-pgvector-package-update.md](./semantic-kernel-pgvector-package-update.md) for complete details.

---

## 1. Microsoft.SemanticKernel Core Concepts

### Architecture Overview

[Semantic Kernel](https://learn.microsoft.com/en-us/semantic-kernel/) is Microsoft's open-source SDK for building AI agent systems. It provides a lightweight, extensible framework for integrating Large Language Models (LLMs) with conventional programming languages.

#### Key Abstractions

| Component | Description |
|-----------|-------------|
| **Kernel** | The central orchestration object that manages services, plugins, and AI interactions |
| **Plugins** | Logical containers for related functions that extend AI capabilities |
| **Functions** | Can be semantic (prompt templates) or native (code-based) |
| **Planners** | Translate user goals into executable chains of function calls |
| **Agents** | Autonomous entities that leverage the Kernel for task execution |

#### Core Design Patterns

The Kernel acts as the dependency injection container and orchestrator:

```csharp
using Microsoft.SemanticKernel;

IKernelBuilder kernelBuilder = Kernel.CreateBuilder();

// Add AI services
kernelBuilder.AddOpenAIChatCompletion("gpt-4", "YOUR_API_KEY");

// Add plugins
kernelBuilder.Plugins.AddFromType<MyPlugin>();

// Build the kernel
Kernel kernel = kernelBuilder.Build();
```

### 2025-2026 Roadmap Updates

According to the [Semantic Kernel Roadmap H1 2025](https://devblogs.microsoft.com/semantic-kernel/semantic-kernel-roadmap-h1-2025-accelerating-agents-processes-and-integration/):

- The **Agent Framework** transitioned from preview to general availability (GA) in Q1 2025
- Strategic convergence with **AutoGen** is underway
- The **Process Framework** was released from preview in Q2 2025
- [Microsoft Agent Framework](https://visualstudiomagazine.com/articles/2025/10/01/semantic-kernel-autogen--open-source-microsoft-agent-framework.aspx) now unifies Semantic Kernel and AutoGen

---

## 2. Embeddings with Ollama

### Ollama Connector Overview

The [Ollama Connector](https://devblogs.microsoft.com/semantic-kernel/introducing-new-ollama-connector-for-local-models/) is an alpha pre-release feature that enables local model deployment using the OllamaSharp library.

#### NuGet Package Installation

```bash
dotnet add package Microsoft.SemanticKernel.Connectors.Ollama --prerelease
```

**Current Version**: 1.68.0-alpha

#### Dependencies
- OllamaSharp (>= 5.4.11)
- Microsoft.SemanticKernel.Abstractions (>= 1.68.0)
- Microsoft.SemanticKernel.Core (>= 1.68.0)

### Configuration for Ollama Embeddings

**Important**: The Ollama embedding connector is experimental and requires pragma warning suppression:

```csharp
#pragma warning disable SKEXP0070

using Microsoft.SemanticKernel;

IKernelBuilder kernelBuilder = Kernel.CreateBuilder();

// Add Ollama text embedding generation
kernelBuilder.AddOllamaTextEmbeddingGeneration(
    modelId: "mxbai-embed-large",          // Embedding model
    endpoint: new Uri("http://localhost:11434")
);

Kernel kernel = kernelBuilder.Build();
```

### Standalone Ollama Service

```csharp
#pragma warning disable SKEXP0070

using Microsoft.SemanticKernel.Embeddings;
using OllamaSharp;

// Create Ollama client
using var ollamaClient = new OllamaApiClient(
    uriString: "http://localhost:11434",
    defaultModel: "mxbai-embed-large"
);

// Convert to embedding service
ITextEmbeddingGenerationService embeddingService =
    ollamaClient.AsTextEmbeddingGenerationService();

// Generate embeddings
ReadOnlyMemory<float> embedding =
    await embeddingService.GenerateEmbeddingAsync("sample text");
```

### Supported Ollama Features

| Feature | Status |
|---------|--------|
| Chat Completion | Supported |
| Text Generation | Supported |
| Text Embeddings | Supported |
| Streaming | Supported (Ollama v0.8.0+) |
| Function Calling | Supported (streaming since v0.8.0) |

### Docker Setup for Ollama

```bash
# CPU-only
docker run -d -v "c:\temp\ollama:/root/.ollama" -p 11434:11434 --name ollama ollama/ollama

# With GPU support
docker run -d --gpus=all -v "c:\temp\ollama:/root/.ollama" -p 11434:11434 --name ollama ollama/ollama

# Pull embedding model
ollama pull mxbai-embed-large
```

---

## 3. Vector Database Integration with PostgreSQL

### PostgreSQL Vector Store Connector

The [PostgreSQL connector](https://learn.microsoft.com/en-us/semantic-kernel/concepts/vector-store-connectors/out-of-the-box-connectors/postgres-connector) uses pgvector for efficient vector operations.

#### NuGet Package Installation

```bash
dotnet add package Microsoft.SemanticKernel.Connectors.PgVector --prerelease
```

**Note:** The package was renamed from `Microsoft.SemanticKernel.Connectors.Postgres` (deprecated) to `Microsoft.SemanticKernel.Connectors.PgVector`. The namespace remains `Microsoft.SemanticKernel.Connectors.Postgres`.

### Connection Setup

#### Option 1: Dependency Injection with Connection String

```csharp
using Microsoft.Extensions.DependencyInjection;

var kernelBuilder = Kernel.CreateBuilder();
kernelBuilder.Services.AddPostgresVectorStore(
    "Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=postgres"
);
```

#### Option 2: Custom NpgsqlDataSource (Recommended)

```csharp
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<NpgsqlDataSource>(sp =>
{
    NpgsqlDataSourceBuilder dataSourceBuilder = new(
        "Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=postgres"
    );
    dataSourceBuilder.UseVector();  // CRITICAL: Enable pgvector support
    return dataSourceBuilder.Build();
});

builder.Services.AddPostgresVectorStore();
```

#### Option 3: Direct Instantiation

```csharp
using Microsoft.SemanticKernel.Connectors.PgVector;
using Npgsql;

// Create data source with vector support
NpgsqlDataSourceBuilder dataSourceBuilder = new("<Connection String>");
dataSourceBuilder.UseVector();  // CRITICAL: Must call this
NpgsqlDataSource dataSource = dataSourceBuilder.Build();

// Create vector store
var vectorStore = new PostgresVectorStore(dataSource, ownsDataSource: true);

// Or create collection directly
var collection = new PostgresCollection<string, Hotel>("<Connection String>", "skhotels");
```

### Data Model Definition

```csharp
using Microsoft.Extensions.VectorData;

public class Hotel
{
    [VectorStoreKey(StorageName = "hotel_id")]
    public int HotelId { get; set; }

    [VectorStoreData(StorageName = "hotel_name")]
    public string HotelName { get; set; }

    [VectorStoreData(StorageName = "hotel_description")]
    public string Description { get; set; }

    [VectorStoreVector(
        Dimensions: 1024,  // Match your embedding model
        DistanceFunction = DistanceFunction.CosineDistance,
        IndexKind = IndexKind.Hnsw,
        StorageName = "hotel_description_embedding")]
    public ReadOnlyMemory<float>? DescriptionEmbedding { get; set; }
}
```

### Generated SQL Schema

```sql
CREATE TABLE IF NOT EXISTS public."Hotels" (
    "hotel_id" INTEGER PRIMARY KEY NOT NULL,
    hotel_name TEXT,
    hotel_description TEXT,
    hotel_description_embedding VECTOR(1024)
);
```

### PostgreSQL with pgvector Docker Setup

```bash
docker run --name postgres \
    -e POSTGRES_PASSWORD=mysecretpassword \
    -p 5432:5432 \
    -d pgvector/pgvector:pg16
```

### Supported Data Types

| Type Category | Supported Types |
|---------------|-----------------|
| **Key Properties** | short, int, long, string, Guid |
| **Data Properties** | bool, short, int, long, float, double, decimal, string, DateTime, DateTimeOffset, Guid, byte[], and enumerables |
| **Vector Properties** | `ReadOnlyMemory<float>`, `Embedding<float>`, `float[]`, `ReadOnlyMemory<Half>`, `Embedding<Half>`, `Half[]`, `BitArray`, `Pgvector.SparseVector` |

### Vector Index Support

| Feature | Status |
|---------|--------|
| Index Types | HNSW (auto-created) |
| Distance Functions | CosineDistance, CosineSimilarity, DotProductSimilarity, EuclideanDistance, ManhattanDistance |
| Filter Operations | AnyTagEqualTo, EqualTo |
| Multiple Vectors | Supported |
| Hybrid Search | Not Supported |

### May 2025 API Changes

Per the [Vector Store changes - May 2025](https://learn.microsoft.com/en-us/semantic-kernel/support/migration/vectorstore-may-2025):

| Old Name | New Name |
|----------|----------|
| `PostgresVectorStoreRecordCollection` | `PostgresCollection<TKey, TRecord>` |
| `PostgresVectorStoreRecordCollectionOptions` | `PostgresCollectionOptions` |
| `CreateCollectionIfNotExistsAsync` | `EnsureCollectionExistsAsync` |
| `DeleteAsync` | `EnsureCollectionDeletedAsync` |
| `IVectorStoreRecordCollection` | `VectorStoreCollection` (abstract class) |
| `VectorStoreOperationException` | `VectorStoreException` |

---

## 4. RAG Implementation

### RAG Architecture in Semantic Kernel

According to [Microsoft's RAG documentation](https://learn.microsoft.com/en-us/semantic-kernel/concepts/plugins/using-data-retrieval-functions-for-rag), RAG combines semantic search with LLM capabilities.

#### Search Types

| Type | Use Case |
|------|----------|
| **Semantic Search** | Context-aware queries using embeddings |
| **Classic Search** | Exact attribute/keyword matching |
| **Hybrid Search** | Combines both approaches |

### Data Retrieval Strategies

1. **Dynamic Data Retrieval**: Data fetched on-demand based on user queries
2. **Pre-fetched Data Retrieval**: Static data provided upfront when always needed

### Complete RAG Implementation Example

```csharp
#pragma warning disable SKEXP0070

using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Embeddings;
using Microsoft.SemanticKernel.Connectors.PgVector;
using Npgsql;

// 1. Define your data model
public class Document
{
    [VectorStoreKey(StorageName = "id")]
    public string Id { get; set; }

    [VectorStoreData(StorageName = "content")]
    public string Content { get; set; }

    [VectorStoreVector(Dimensions: 1024,
        DistanceFunction.CosineDistance,
        StorageName = "content_embedding")]
    public ReadOnlyMemory<float> ContentEmbedding { get; set; }
}

// 2. Build the Kernel with Ollama services
IKernelBuilder kernelBuilder = Kernel.CreateBuilder();

// Add chat completion
kernelBuilder.Services.AddOllamaChatCompletion(
    modelId: "llama3.1",
    endpoint: new Uri("http://localhost:11434")
);

// Add embedding generation
kernelBuilder.Services.AddOllamaTextEmbeddingGeneration(
    modelId: "mxbai-embed-large",
    endpoint: new Uri("http://localhost:11434")
);

Kernel kernel = kernelBuilder.Build();

// 3. Set up PostgreSQL vector store
NpgsqlDataSourceBuilder dataSourceBuilder = new(
    "Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=postgres"
);
dataSourceBuilder.UseVector();
NpgsqlDataSource dataSource = dataSourceBuilder.Build();

var vectorStore = new PostgresVectorStore(dataSource, ownsDataSource: true);
var collection = vectorStore.GetCollection<string, Document>("documents");

// 4. Create collection if it doesn't exist
await collection.EnsureCollectionExistsAsync();

// 5. Get embedding service
var embeddingService = kernel.GetRequiredService<ITextEmbeddingGenerationService>();

// 6. Store a document with embedding
string documentContent = "The capital of France is Paris. Paris is known for the Eiffel Tower.";
ReadOnlyMemory<float> embedding = await embeddingService.GenerateEmbeddingAsync(documentContent);

await collection.UpsertAsync(new Document
{
    Id = "doc-001",
    Content = documentContent,
    ContentEmbedding = embedding
});

// 7. Perform RAG retrieval
string userQuery = "What is the capital of France?";
ReadOnlyMemory<float> queryEmbedding = await embeddingService.GenerateEmbeddingAsync(userQuery);

var searchResults = collection.SearchAsync(queryEmbedding, top: 3);

// 8. Build context from retrieved documents
var contextBuilder = new StringBuilder();
await foreach (var result in searchResults)
{
    contextBuilder.AppendLine($"Document: {result.Record.Content}");
    contextBuilder.AppendLine($"Relevance: {result.Score}");
}

// 9. Generate response using LLM with retrieved context
var chatService = kernel.GetRequiredService<IChatCompletionService>();
var chatHistory = new ChatHistory();

chatHistory.AddSystemMessage($"Use the following context to answer questions:\n{contextBuilder}");
chatHistory.AddUserMessage(userQuery);

var response = await chatService.GetChatMessageContentAsync(chatHistory);
Console.WriteLine(response.Content);
```

### RAG Plugin Implementation

```csharp
using System.ComponentModel;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Embeddings;

public class RAGPlugin
{
    private readonly ITextEmbeddingGenerationService _embeddingService;
    private readonly PostgresCollection<string, Document> _collection;

    public RAGPlugin(
        ITextEmbeddingGenerationService embeddingService,
        PostgresCollection<string, Document> collection)
    {
        _embeddingService = embeddingService;
        _collection = collection;
    }

    [KernelFunction("SearchDocuments")]
    [Description("Search for documents similar to the given query.")]
    public async Task<string> SearchDocumentsAsync(string query)
    {
        // Generate query embedding
        ReadOnlyMemory<float> queryEmbedding =
            await _embeddingService.GenerateEmbeddingAsync(query);

        // Search vector store
        var results = _collection.SearchAsync(queryEmbedding, top: 3);

        var resultText = new StringBuilder();
        await foreach (var result in results)
        {
            resultText.AppendLine(result.Record.Content);
        }

        return resultText.ToString();
    }
}

// Register plugin with kernel
kernel.ImportPluginFromObject(
    new RAGPlugin(embeddingService, collection),
    "RAG"
);
```

### Automatic Embedding Generation

Semantic Kernel supports automatic embedding generation during upsert and search operations:

```csharp
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel.Connectors.PgVector;

// Data model with string-typed vector property
public class AutoEmbedDocument
{
    [VectorStoreKey]
    public string Id { get; set; }

    [VectorStoreData]
    public string Content { get; set; }

    [VectorStoreVector(1024)]
    public string Embedding => this.Content;  // Auto-embed from Content
}

// Configure embedding generator at vector store level
var embeddingGenerator = /* your embedding generator */;

var vectorStore = new PostgresVectorStore(dataSource, new PostgresVectorStoreOptions
{
    EmbeddingGenerator = embeddingGenerator
});

var collection = vectorStore.GetCollection<string, AutoEmbedDocument>("documents");

// Embeddings generated automatically on upsert
await collection.UpsertAsync(new AutoEmbedDocument
{
    Id = "1",
    Content = "Sample document content"
});

// Embeddings generated automatically on search
var results = collection.SearchAsync("search query", top: 3);
```

---

## 5. Required NuGet Packages

### Core Packages

| Package | Version | Purpose |
|---------|---------|---------|
| `Microsoft.SemanticKernel` | 1.69.0 | Core SDK |
| `Microsoft.Extensions.VectorData.Abstractions` | Latest | Vector store interfaces |

### Connector Packages

| Package | Version | Purpose |
|---------|---------|---------|
| `Microsoft.SemanticKernel.Connectors.Ollama` | 1.68.0-alpha (prerelease) | Ollama integration |
| `Microsoft.SemanticKernel.Connectors.PgVector` | Latest (prerelease) | PostgreSQL pgvector support |

### Additional Dependencies

| Package | Purpose |
|---------|---------|
| `Npgsql` | PostgreSQL .NET driver |
| `OllamaSharp` | Native Ollama API access (auto-included) |

### Installation Commands

```bash
# Core package
dotnet add package Microsoft.SemanticKernel

# Vector store abstractions
dotnet add package Microsoft.Extensions.VectorData.Abstractions

# Ollama connector (prerelease)
dotnet add package Microsoft.SemanticKernel.Connectors.Ollama --prerelease

# PostgreSQL connector (prerelease)
dotnet add package Microsoft.SemanticKernel.Connectors.PgVector --prerelease
```

### Complete Project File (.csproj)

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.SemanticKernel" Version="1.69.0" />
    <PackageReference Include="Microsoft.Extensions.VectorData.Abstractions" Version="9.0.0-preview.*" />
    <PackageReference Include="Microsoft.SemanticKernel.Connectors.Ollama" Version="1.68.0-alpha" />
    <PackageReference Include="Microsoft.SemanticKernel.Connectors.PgVector" Version="1.70.0-preview" />
  </ItemGroup>
</Project>
```

---

## 6. Complete Code Examples

### Full MCP Server RAG Implementation Pattern

Here is a complete implementation pattern for your MCP Server:

```csharp
// File: Program.cs
#pragma warning disable SKEXP0070

using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Embeddings;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.PgVector;
using Microsoft.Extensions.VectorData;
using Npgsql;
using System.Text;

namespace McpServerRag;

// Data Model
public class KnowledgeDocument
{
    [VectorStoreKey(StorageName = "id")]
    public string Id { get; set; } = string.Empty;

    [VectorStoreData(StorageName = "title")]
    public string Title { get; set; } = string.Empty;

    [VectorStoreData(StorageName = "content")]
    public string Content { get; set; } = string.Empty;

    [VectorStoreData(StorageName = "metadata")]
    public string? Metadata { get; set; }

    [VectorStoreVector(
        Dimensions: 1024,  // mxbai-embed-large produces 1024 dimensions
        DistanceFunction.CosineDistance,
        IndexKind.Hnsw,
        StorageName = "content_embedding")]
    public ReadOnlyMemory<float> ContentEmbedding { get; set; }
}

// RAG Service
public class RagService
{
    private readonly Kernel _kernel;
    private readonly ITextEmbeddingGenerationService _embeddingService;
    private readonly PostgresCollection<string, KnowledgeDocument> _collection;

    public RagService(
        Kernel kernel,
        PostgresCollection<string, KnowledgeDocument> collection)
    {
        _kernel = kernel;
        _embeddingService = kernel.GetRequiredService<ITextEmbeddingGenerationService>();
        _collection = collection;
    }

    public async Task InitializeAsync()
    {
        await _collection.EnsureCollectionExistsAsync();
    }

    public async Task IndexDocumentAsync(string id, string title, string content, string? metadata = null)
    {
        // Generate embedding for content
        ReadOnlyMemory<float> embedding = await _embeddingService.GenerateEmbeddingAsync(content);

        // Store document
        await _collection.UpsertAsync(new KnowledgeDocument
        {
            Id = id,
            Title = title,
            Content = content,
            Metadata = metadata,
            ContentEmbedding = embedding
        });
    }

    public async Task<IReadOnlyList<(KnowledgeDocument Document, double Score)>> RetrieveAsync(
        string query,
        int topK = 5)
    {
        // Generate query embedding
        ReadOnlyMemory<float> queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(query);

        // Search vector store
        var results = new List<(KnowledgeDocument, double)>();
        var searchResults = _collection.SearchAsync(queryEmbedding, top: topK);

        await foreach (var result in searchResults)
        {
            results.Add((result.Record, result.Score ?? 0));
        }

        return results;
    }

    public async Task<string> GenerateResponseAsync(string query, int topK = 5)
    {
        // Retrieve relevant documents
        var documents = await RetrieveAsync(query, topK);

        // Build context
        var contextBuilder = new StringBuilder();
        contextBuilder.AppendLine("Relevant documents:");
        foreach (var (doc, score) in documents)
        {
            contextBuilder.AppendLine($"---");
            contextBuilder.AppendLine($"Title: {doc.Title}");
            contextBuilder.AppendLine($"Content: {doc.Content}");
            contextBuilder.AppendLine($"Relevance: {score:F4}");
        }

        // Generate response using LLM
        var chatService = _kernel.GetRequiredService<IChatCompletionService>();
        var chatHistory = new ChatHistory();

        chatHistory.AddSystemMessage(
            "You are a helpful assistant. Use the following context to answer questions. " +
            "If the context doesn't contain relevant information, say so.\n\n" +
            contextBuilder.ToString());

        chatHistory.AddUserMessage(query);

        var response = await chatService.GetChatMessageContentAsync(chatHistory);
        return response.Content ?? "No response generated.";
    }
}

// Main Program
public class Program
{
    public static async Task Main(string[] args)
    {
        // Configuration
        const string ollamaEndpoint = "http://localhost:11434";
        const string chatModel = "llama3.1";
        const string embeddingModel = "mxbai-embed-large";
        const string postgresConnectionString =
            "Host=localhost;Port=5432;Database=ragdb;Username=postgres;Password=postgres";
        const string collectionName = "knowledge_base";

        // 1. Build Kernel with Ollama services
        var kernelBuilder = Kernel.CreateBuilder();

        kernelBuilder.Services.AddOllamaChatCompletion(
            modelId: chatModel,
            endpoint: new Uri(ollamaEndpoint)
        );

        kernelBuilder.Services.AddOllamaTextEmbeddingGeneration(
            modelId: embeddingModel,
            endpoint: new Uri(ollamaEndpoint)
        );

        var kernel = kernelBuilder.Build();

        // 2. Set up PostgreSQL vector store
        var dataSourceBuilder = new NpgsqlDataSourceBuilder(postgresConnectionString);
        dataSourceBuilder.UseVector();
        var dataSource = dataSourceBuilder.Build();

        var vectorStore = new PostgresVectorStore(dataSource, ownsDataSource: true);
        var collection = vectorStore.GetCollection<string, KnowledgeDocument>(collectionName);

        // 3. Initialize RAG service
        var ragService = new RagService(kernel, collection);
        await ragService.InitializeAsync();

        // 4. Index sample documents
        Console.WriteLine("Indexing sample documents...");

        await ragService.IndexDocumentAsync(
            id: "doc-001",
            title: "Introduction to RAG",
            content: "Retrieval Augmented Generation (RAG) is a technique that combines " +
                     "information retrieval with text generation. It first retrieves relevant " +
                     "documents from a knowledge base, then uses them as context for an LLM " +
                     "to generate accurate and informed responses."
        );

        await ragService.IndexDocumentAsync(
            id: "doc-002",
            title: "Vector Databases",
            content: "Vector databases store data as high-dimensional vectors (embeddings). " +
                     "They enable semantic search by finding vectors that are similar to a " +
                     "query vector. PostgreSQL with pgvector extension is a popular choice " +
                     "for vector storage."
        );

        await ragService.IndexDocumentAsync(
            id: "doc-003",
            title: "Semantic Kernel Overview",
            content: "Microsoft Semantic Kernel is an open-source SDK for building AI applications. " +
                     "It provides abstractions for AI services, plugins, planners, and memory. " +
                     "It supports multiple AI providers including OpenAI, Azure OpenAI, and Ollama."
        );

        Console.WriteLine("Documents indexed successfully!");

        // 5. Interactive query loop
        Console.WriteLine("\nRAG System Ready. Type 'quit' to exit.\n");

        while (true)
        {
            Console.Write("Query: ");
            var query = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(query) || query.ToLower() == "quit")
                break;

            Console.WriteLine("\nSearching and generating response...\n");

            var response = await ragService.GenerateResponseAsync(query);

            Console.WriteLine($"Response: {response}\n");
            Console.WriteLine(new string('-', 50));
        }

        // Cleanup
        await dataSource.DisposeAsync();
    }
}
```

---

## Key Considerations and Best Practices

### Security Best Practices for RAG

| Strategy | Description |
|----------|-------------|
| Use user's auth token | Verify user access to retrieved data |
| Store references in vector DBs | Store pointers instead of duplicating sensitive content |
| Reuse existing access controls | Leverage existing filtering and permissions |

### Embedding Model Dimensions

| Model | Dimensions |
|-------|------------|
| mxbai-embed-large (Ollama) | 1024 |
| text-embedding-ada-002 (OpenAI) | 1536 |
| text-embedding-004 (Google) | 768 (default) |

**Critical**: Ensure your vector property dimensions match your embedding model output.

### Current Limitations

- Vector Store functionality is in **Preview** status
- Ollama connector is in **Alpha** pre-release
- Breaking changes may occur before GA release
- Hybrid search is **not supported** for PostgreSQL connector
- Some features require experimental pragma warnings

---

## Sources

- [Introduction to Semantic Kernel | Microsoft Learn](https://learn.microsoft.com/en-us/semantic-kernel/overview/)
- [Semantic Kernel Components | Microsoft Learn](https://learn.microsoft.com/en-us/semantic-kernel/concepts/semantic-kernel-components)
- [Semantic Kernel Roadmap H1 2025](https://devblogs.microsoft.com/semantic-kernel/semantic-kernel-roadmap-h1-2025-accelerating-agents-processes-and-integration/)
- [Introducing new Ollama Connector | Semantic Kernel Blog](https://devblogs.microsoft.com/semantic-kernel/introducing-new-ollama-connector-for-local-models/)
- [Add embedding generation services | Microsoft Learn](https://learn.microsoft.com/en-us/semantic-kernel/concepts/ai-services/embedding-generation/)
- [PostgreSQL Vector Store connector | Microsoft Learn](https://learn.microsoft.com/en-us/semantic-kernel/concepts/vector-store-connectors/out-of-the-box-connectors/postgres-connector)
- [Vector Store changes - May 2025 | Microsoft Learn](https://learn.microsoft.com/en-us/semantic-kernel/support/migration/vectorstore-may-2025)
- [What are Semantic Kernel Vector Stores? | Microsoft Learn](https://learn.microsoft.com/en-us/semantic-kernel/concepts/vector-store-connectors/)
- [Using plugins for RAG | Microsoft Learn](https://learn.microsoft.com/en-us/semantic-kernel/concepts/plugins/using-data-retrieval-functions-for-rag)
- [Semantic Kernel Vector Store code samples | Microsoft Learn](https://learn.microsoft.com/en-us/semantic-kernel/concepts/vector-store-connectors/code-samples)
- [Generating embeddings for Vector Store connectors | Microsoft Learn](https://learn.microsoft.com/en-us/semantic-kernel/concepts/vector-store-connectors/embedding-generation)
- [Microsoft.SemanticKernel.Connectors.Ollama NuGet](https://www.nuget.org/packages/Microsoft.SemanticKernel.Connectors.Ollama)
- [PostgreSQL Semantic Kernel Examples | GitHub](https://github.com/Azure-Samples/postgres-semantic-kernel-examples)
- [Microsoft Agent Framework | Visual Studio Magazine](https://visualstudiomagazine.com/articles/2025/10/01/semantic-kernel-autogen--open-source-microsoft-agent-framework.aspx)
- [Implementing Simple RAG in local environment | DEV.to](https://dev.to/rufo123/implementing-simple-rag-in-local-environment-w-net-c-3mfo)
- [Building Semantic Search in ASP.NET Core using PostgreSQL | DEV.to](https://dev.to/ohalay/how-to-build-semantic-search-in-aspnet-core-using-postgresql-28m8)
