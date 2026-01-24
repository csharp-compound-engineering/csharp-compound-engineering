# Microsoft.Extensions.AI for RAG Applications

## Research Summary

This document provides comprehensive research on using Microsoft.Extensions.AI for Retrieval-Augmented Generation (RAG) scenarios in .NET applications.

---

## 1. Microsoft.Extensions.AI Overview

### What is Microsoft.Extensions.AI?

Microsoft.Extensions.AI is a unified abstraction layer for AI services in .NET. Think of it like `ILogger` but for AI - you program against an interface, and the implementation can be OpenAI, Azure OpenAI, Ollama, or whatever comes next.

**Key characteristics:**
- Lightweight, plug-and-play LLM interface
- Minimal, unopinionated, and simple to wire up
- Provides standard implementations for caching, telemetry, tool calling, and other common tasks
- Works with any provider implementing the interfaces

### Current Version and Maturity

- **GA Status**: Generally Available as of May 2025
- **Current Version**: Microsoft.Extensions.AI 10.2.0
- **Downloads**: Over 3 million downloads
- **Ecosystem**: Nearly 100 public NuGet packages depend on it

### NuGet Packages

```xml
<ItemGroup>
  <!-- Core abstractions (interfaces) -->
  <PackageReference Include="Microsoft.Extensions.AI.Abstractions" Version="10.2.0" />

  <!-- Higher-level utilities and middleware -->
  <PackageReference Include="Microsoft.Extensions.AI" Version="10.2.0" />

  <!-- Provider-specific packages -->
  <PackageReference Include="Microsoft.Extensions.AI.OpenAI" Version="10.2.0" />
  <PackageReference Include="Microsoft.Extensions.AI.Ollama" Version="10.2.0" />
  <PackageReference Include="Microsoft.Extensions.AI.AzureAIInference" Version="10.2.0" />

  <!-- Vector data abstractions -->
  <PackageReference Include="Microsoft.Extensions.VectorData.Abstractions" Version="9.7.0" />
</ItemGroup>
```

### Relationship with Semantic Kernel

| Aspect | Microsoft.Extensions.AI | Semantic Kernel |
|--------|------------------------|-----------------|
| Purpose | Foundational SDK - core abstractions | Specialized SDK - orchestration layer |
| Complexity | Minimal, bare-bones | Full-featured with agents, plugins, memory |
| Use Case | Simple chatbots, basic LLM calls | Complex workflows, agents, multi-step pipelines |
| Recommendation | Start here for simple needs | "Upgrade" when advanced features needed |

**Key insight**: Microsoft.Extensions.AI acts as the standard plumbing layer for generative AI in .NET. Semantic Kernel builds orchestration, planning, and agent logic on top of that foundation. Semantic Kernel now natively supports Microsoft.Extensions.AI's `IChatClient` interface.

---

## 2. Core Abstractions

### IChatClient Interface

The `IChatClient` interface defines a client abstraction for interacting with AI services that provide chat capabilities.

```csharp
public interface IChatClient
{
    Task<ChatCompletion> CompleteAsync(
        IList<ChatMessage> chatMessages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<StreamingChatCompletionUpdate> CompleteStreamingAsync(
        IList<ChatMessage> chatMessages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default);
}
```

**Capabilities:**
- Send and receive messages with multi-modal content (text, images, audio)
- Complete set delivery or streamed incremental responses
- Seamless integration across different AI services

### IEmbeddingGenerator Interface

The `IEmbeddingGenerator<TInput, TEmbedding>` interface represents a generic generator of embeddings.

```csharp
public interface IEmbeddingGenerator<TInput, TEmbedding>
    where TEmbedding : Embedding
{
    Task<GeneratedEmbeddings<TEmbedding>> GenerateAsync(
        IEnumerable<TInput> values,
        EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default);
}
```

**Type Parameters:**
- `TInput`: Type of input values being embedded (typically `string`)
- `TEmbedding`: Type of generated embedding (typically `Embedding<float>`)

### Provider-Agnostic Code Example

```csharp
// Your service depends only on the abstraction
public class ChatService
{
    private readonly IChatClient _chatClient;
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;

    public ChatService(
        IChatClient chatClient,
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator)
    {
        _chatClient = chatClient;
        _embeddingGenerator = embeddingGenerator;
    }

    public async Task<string> GetResponseAsync(string userMessage)
    {
        var response = await _chatClient.CompleteAsync(userMessage);
        return response.Message.Text;
    }

    public async Task<ReadOnlyMemory<float>> GetEmbeddingAsync(string text)
    {
        var embedding = await _embeddingGenerator.GenerateVectorAsync(text);
        return embedding;
    }
}
```

---

## 3. RAG-Specific Features

### Embedding Generation

```csharp
// Azure OpenAI embedding generator
IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator =
    new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential())
        .GetEmbeddingClient("text-embedding-3-small")
        .AsIEmbeddingGenerator();

// Generate embedding for a single text
ReadOnlyMemory<float> embedding = await embeddingGenerator.GenerateVectorAsync(
    "What is Azure Blob Storage?");

// Generate embeddings for multiple texts
var embeddings = await embeddingGenerator.GenerateAsync(new[] {
    "First document text",
    "Second document text",
    "Third document text"
});
```

### Document/Text Chunking Utilities

Microsoft.Extensions.AI integrates with `Microsoft.ML.Tokenizers` for intelligent chunking:

```csharp
using Microsoft.ML.Tokenizers;

// Create a tokenizer for your model
Tokenizer tokenizer = TiktokenTokenizer.CreateForModel("gpt-4");

// Configure chunking options
IngestionChunkerOptions options = new(tokenizer)
{
    MaxTokensPerChunk = 2000,
    OverlapTokens = 100  // Overlap for context preservation
};

// Choose chunking strategy
IngestionChunker<string> chunker = new HeaderChunker(options);  // Header-based
// or
IngestionChunker<string> chunker = new SectionChunker(options); // Section-based
// or
IngestionChunker<string> chunker = new SemanticChunker(options); // Semantic-aware
```

**Available Chunking Strategies:**
- **Header-based chunking**: Splits on document headers
- **Section-based chunking**: Splits on sections (e.g., pages)
- **Semantic-aware chunking**: Preserves complete thoughts and meaning

### Memory and Context Management

The library handles context through the middleware pipeline and integration with vector stores:

```csharp
// Automatic embedding generation when upserting to vector store
VectorStoreCollection<int, Product> collection = new SqliteCollection<int, Product>(
    "Data Source=products.db",
    "products",
    new SqliteCollectionOptions { EmbeddingGenerator = embeddingGenerator });

// The text is automatically embedded before storage
await collection.UpsertAsync(new Product {
    Id = 1,
    Name = "Widget",
    Description = "A versatile widget for all your needs..."  // Auto-embedded
});
```

---

## 4. Provider Support

### Supported Providers

| Provider | Package | Status |
|----------|---------|--------|
| OpenAI | `Microsoft.Extensions.AI.OpenAI` | GA |
| Azure OpenAI | `Microsoft.Extensions.AI.OpenAI` | GA |
| Ollama | `Microsoft.Extensions.AI.Ollama` | GA |
| Azure AI Model Catalog | `Microsoft.Extensions.AI.AzureAIInference` | GA |
| GitHub Models | `Microsoft.Extensions.AI.AzureAIInference` | GA |
| Anthropic/Claude | Community packages | Varies |

### Provider Configuration Examples

#### OpenAI

```csharp
using OpenAI;
using Microsoft.Extensions.AI;

IChatClient chatClient = new OpenAIClient("your-api-key")
    .GetChatClient("gpt-4o")
    .AsIChatClient();

IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator =
    new OpenAIClient("your-api-key")
        .GetEmbeddingClient("text-embedding-3-small")
        .AsIEmbeddingGenerator();
```

#### Azure OpenAI

```csharp
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Extensions.AI;

IChatClient chatClient = new AzureOpenAIClient(
    new Uri(Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")!),
    new DefaultAzureCredential())
    .AsChatClient(modelId: "gpt-4o");

IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator =
    new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential())
        .GetEmbeddingClient("text-embedding-3-small")
        .AsIEmbeddingGenerator();
```

#### Ollama (Local)

```csharp
using Microsoft.Extensions.AI;

// Dedicated Ollama client
IChatClient chatClient = new OllamaChatClient(
    new Uri("http://localhost:11434/"),
    "llama3.1");

IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator =
    new OllamaEmbeddingGenerator(
        new Uri("http://localhost:11434/"),
        "all-minilm");

// Or via OpenAI-compatible endpoint
IChatClient chatClient = new OpenAIClient(
    new ApiKeyCredential("ignored"),  // Ollama ignores API key
    new OpenAIClientOptions { Endpoint = new Uri("http://localhost:11434/v1") })
    .GetChatClient("llama3.1")
    .AsIChatClient();
```

#### Environment-Based Provider Selection

```csharp
IChatClient chatClient = builder.Environment.IsDevelopment()
    ? new OllamaChatClient(new Uri("http://localhost:11434/"), "llama3.1")
    : new AzureOpenAIClient(new Uri(azureEndpoint), new DefaultAzureCredential())
        .AsChatClient("gpt-4o");
```

---

## 5. Integration with Vector Stores

### Microsoft.Extensions.VectorData Abstractions

The library provides a consistent interface similar to Entity Framework but for vector data.

**Key Interfaces:**
- `IVectorStore`: Main entry point for vector store operations
- `IVectorStoreRecordCollection<TKey, TRecord>`: Collection-level operations

### Supported Vector Databases

| Database | NuGet Package |
|----------|---------------|
| Azure AI Search | `Microsoft.SemanticKernel.Connectors.AzureAISearch` |
| PostgreSQL/pgvector | `Microsoft.SemanticKernel.Connectors.PgVector` |
| Qdrant | `Microsoft.SemanticKernel.Connectors.Qdrant` |
| Redis | `Microsoft.SemanticKernel.Connectors.Redis` |
| Azure CosmosDB NoSQL | `Microsoft.SemanticKernel.Connectors.CosmosNoSQL` |
| Azure CosmosDB MongoDB | `Microsoft.SemanticKernel.Connectors.CosmosMongoDB` |
| MongoDB | `Microsoft.SemanticKernel.Connectors.MongoDB` |
| SQLite | `Microsoft.SemanticKernel.Connectors.SqliteVec` |
| SQL Server | `Microsoft.SemanticKernel.Connectors.SqlServer` |
| Pinecone | `Microsoft.SemanticKernel.Connectors.Pinecone` |
| Weaviate | `Microsoft.SemanticKernel.Connectors.Weaviate` |
| Elasticsearch | `Elastic.SemanticKernel.Connectors.Elasticsearch` |
| In-Memory | `Microsoft.SemanticKernel.Connectors.InMemory` |

### Data Model Definition

```csharp
using Microsoft.Extensions.VectorData;

public class Document
{
    [VectorStoreKey]
    public string Id { get; set; } = string.Empty;

    [VectorStoreData]
    public string Title { get; set; } = string.Empty;

    [VectorStoreData]
    public string Content { get; set; } = string.Empty;

    [VectorStoreData]
    public string Category { get; set; } = string.Empty;

    [VectorStoreData]
    public DateTime CreatedAt { get; set; }

    [VectorStoreVector(
        Dimensions: 1536,
        DistanceFunction = DistanceFunction.CosineSimilarity)]
    public ReadOnlyMemory<float> ContentEmbedding { get; set; }
}
```

**Attribute Descriptions:**
- `[VectorStoreKey]`: Unique identifier for the record
- `[VectorStoreData]`: Regular data fields (indexed for filtering)
- `[VectorStoreVector]`: Vector embedding with dimension and distance function

### Wiring Up Embeddings to Vector Stores

```csharp
// Create embedding generator
IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator =
    new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential())
        .GetEmbeddingClient("text-embedding-3-small")
        .AsIEmbeddingGenerator();

// Create vector store with automatic embedding
using SqliteVectorStore vectorStore = new(
    "Data Source=documents.db;Pooling=false",
    new() { EmbeddingGenerator = embeddingGenerator });

// Get collection
var collection = vectorStore.GetCollection<string, Document>("documents");
await collection.EnsureCollectionExistsAsync();

// Upsert with automatic embedding generation
var document = new Document
{
    Id = Guid.NewGuid().ToString(),
    Title = "Azure Storage Overview",
    Content = "Azure Blob Storage is Microsoft's object storage solution...",
    Category = "Azure",
    CreatedAt = DateTime.UtcNow
};

// Generate embedding manually if needed
document.ContentEmbedding = await embeddingGenerator.GenerateVectorAsync(document.Content);

await collection.UpsertAsync(document);
```

---

## 6. Building RAG Pipelines

### Complete End-to-End RAG Workflow

#### Data Model

```csharp
using Microsoft.Extensions.VectorData;

public class KnowledgeDocument
{
    [VectorStoreKey]
    public int Id { get; set; }

    [VectorStoreData]
    public string Title { get; set; } = string.Empty;

    [VectorStoreData]
    public string Content { get; set; } = string.Empty;

    [VectorStoreData]
    public string Source { get; set; } = string.Empty;

    [VectorStoreVector(Dimensions: 1536, DistanceFunction = DistanceFunction.CosineSimilarity)]
    public ReadOnlyMemory<float> Embedding { get; set; }
}
```

#### Ingestion Pipeline (Chunking -> Embedding -> Storing)

```csharp
public class RagIngestionService
{
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;
    private readonly IVectorStoreRecordCollection<int, KnowledgeDocument> _collection;
    private readonly Tokenizer _tokenizer;

    public RagIngestionService(
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
        IVectorStoreRecordCollection<int, KnowledgeDocument> collection)
    {
        _embeddingGenerator = embeddingGenerator;
        _collection = collection;
        _tokenizer = TiktokenTokenizer.CreateForModel("gpt-4");
    }

    public async Task IngestDocumentAsync(string title, string content, string source)
    {
        // 1. Chunk the document
        var chunks = ChunkDocument(content, maxTokens: 500, overlap: 50);

        // 2. Generate embeddings for all chunks
        var embeddings = await _embeddingGenerator.GenerateAsync(chunks);

        // 3. Store chunks with embeddings
        int id = 1;
        foreach (var (chunk, embedding) in chunks.Zip(embeddings.Select(e => e.Vector)))
        {
            var document = new KnowledgeDocument
            {
                Id = id++,
                Title = title,
                Content = chunk,
                Source = source,
                Embedding = embedding
            };

            await _collection.UpsertAsync(document);
        }
    }

    private IEnumerable<string> ChunkDocument(string content, int maxTokens, int overlap)
    {
        var sentences = content.Split(new[] { ". ", "! ", "? " }, StringSplitOptions.RemoveEmptyEntries);
        var currentChunk = new List<string>();
        var currentTokenCount = 0;

        foreach (var sentence in sentences)
        {
            var sentenceTokens = _tokenizer.CountTokens(sentence);

            if (currentTokenCount + sentenceTokens > maxTokens && currentChunk.Count > 0)
            {
                yield return string.Join(". ", currentChunk) + ".";

                // Keep overlap sentences
                var overlapTokens = 0;
                var overlapSentences = new List<string>();
                for (int i = currentChunk.Count - 1; i >= 0 && overlapTokens < overlap; i--)
                {
                    overlapSentences.Insert(0, currentChunk[i]);
                    overlapTokens += _tokenizer.CountTokens(currentChunk[i]);
                }

                currentChunk = overlapSentences;
                currentTokenCount = overlapTokens;
            }

            currentChunk.Add(sentence);
            currentTokenCount += sentenceTokens;
        }

        if (currentChunk.Count > 0)
        {
            yield return string.Join(". ", currentChunk) + ".";
        }
    }
}
```

#### Query Pipeline (Embed Query -> Search -> Augment -> Generate)

```csharp
public class RagQueryService
{
    private readonly IChatClient _chatClient;
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;
    private readonly IVectorStoreRecordCollection<int, KnowledgeDocument> _collection;

    public RagQueryService(
        IChatClient chatClient,
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
        IVectorStoreRecordCollection<int, KnowledgeDocument> collection)
    {
        _chatClient = chatClient;
        _embeddingGenerator = embeddingGenerator;
        _collection = collection;
    }

    public async Task<string> QueryAsync(string userQuestion, int topK = 3)
    {
        // 1. Generate embedding for the query
        var queryEmbedding = await _embeddingGenerator.GenerateVectorAsync(userQuestion);

        // 2. Search for relevant documents
        var searchOptions = new VectorSearchOptions
        {
            Top = topK,
            VectorPropertyName = nameof(KnowledgeDocument.Embedding)
        };

        var searchResults = await _collection.VectorizedSearchAsync(queryEmbedding, searchOptions);

        // 3. Build context from retrieved documents
        var contextBuilder = new StringBuilder();
        var sources = new List<string>();

        await foreach (var result in searchResults.Results)
        {
            contextBuilder.AppendLine($"--- Document (Score: {result.Score:F3}) ---");
            contextBuilder.AppendLine(result.Record.Content);
            contextBuilder.AppendLine();
            sources.Add(result.Record.Source);
        }

        // 4. Augment prompt with context
        var systemPrompt = """
            You are a helpful assistant that answers questions based on the provided context.
            Only use information from the context to answer. If the answer is not in the context,
            say "I don't have enough information to answer that question."

            Context:
            """ + contextBuilder.ToString();

        // 5. Generate response
        var messages = new List<ChatMessage>
        {
            new ChatMessage(ChatRole.System, systemPrompt),
            new ChatMessage(ChatRole.User, userQuestion)
        };

        var response = await _chatClient.CompleteAsync(messages);

        // 6. Return response with sources
        return $"{response.Message.Text}\n\nSources: {string.Join(", ", sources.Distinct())}";
    }
}
```

#### Full Pipeline with IngestionPipeline Class

```csharp
using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;

// Set up components
var embeddingGenerator = new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential())
    .GetEmbeddingClient("text-embedding-3-small")
    .AsIEmbeddingGenerator();

using var vectorStore = new SqliteVectorStore(
    "Data Source=vectors.db;Pooling=false",
    new() { EmbeddingGenerator = embeddingGenerator });

using var writer = new VectorStoreWriter<string>(vectorStore, dimensionCount: 1536);

// Configure chunking
var tokenizer = TiktokenTokenizer.CreateForModel("gpt-4");
var chunkerOptions = new IngestionChunkerOptions(tokenizer)
{
    MaxTokensPerChunk = 2000,
    OverlapTokens = 100
};
var chunker = new HeaderChunker(chunkerOptions);

// Configure document reader
var reader = new MarkdownDocumentReader();

// Configure enrichers (optional)
var summaryEnricher = new SummaryEnricher(chatClient);
var imageAltTextEnricher = new ImageAlternativeTextEnricher(chatClient);

// Build and run pipeline
using var pipeline = new IngestionPipeline<string>(reader, chunker, writer, loggerFactory)
{
    DocumentProcessors = { imageAltTextEnricher },
    ChunkProcessors = { summaryEnricher }
};

await foreach (var result in pipeline.ProcessAsync(new DirectoryInfo("./docs"), "*.md"))
{
    Console.WriteLine($"Processed '{result.DocumentId}'. Success: {result.Succeeded}");
}
```

---

## 7. Middleware and Extensibility

### ChatClientBuilder Pipeline

```csharp
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

// Build middleware pipeline
IChatClient chatClient = new ChatClientBuilder(innerClient)
    .UseLogging(loggerFactory)
    .UseDistributedCache(distributedCache)
    .UseOpenTelemetry(loggerFactory, sourceName: "MyApp.AI")
    .UseFunctionInvocation()
    .Build();
```

### Logging Middleware

```csharp
builder.Services.AddLogging(config => config.AddConsole());

builder.Services.AddChatClient(innerClient =>
    new ChatClientBuilder(innerClient)
        .UseLogging()
        .Build());
```

### Caching Middleware

```csharp
// Add distributed cache
builder.Services.AddDistributedMemoryCache(); // Or Redis, SQL Server, etc.

// Configure chat client with caching
builder.Services.AddChatClient(sp =>
{
    var innerClient = new OllamaChatClient(new Uri("http://localhost:11434"), "llama3.1");
    var cache = sp.GetRequiredService<IDistributedCache>();

    return new ChatClientBuilder(innerClient)
        .UseDistributedCache(cache)
        .Build();
});
```

### Rate Limiting (Custom Middleware)

```csharp
public class RateLimitingChatClient : DelegatingChatClient
{
    private readonly SemaphoreSlim _semaphore;
    private readonly TimeSpan _delay;

    public RateLimitingChatClient(IChatClient innerClient, int maxConcurrent, TimeSpan delay)
        : base(innerClient)
    {
        _semaphore = new SemaphoreSlim(maxConcurrent);
        _delay = delay;
    }

    public override async Task<ChatCompletion> CompleteAsync(
        IList<ChatMessage> chatMessages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            await Task.Delay(_delay, cancellationToken);
            return await base.CompleteAsync(chatMessages, options, cancellationToken);
        }
        finally
        {
            _semaphore.Release();
        }
    }
}

// Usage
IChatClient rateLimitedClient = new RateLimitingChatClient(
    innerClient,
    maxConcurrent: 5,
    delay: TimeSpan.FromMilliseconds(100));
```

### Function/Tool Calling

```csharp
using Microsoft.Extensions.AI;

// Define tools using AIFunctionFactory
var weatherTool = AIFunctionFactory.Create(
    (string location) => $"The weather in {location} is sunny, 72F",
    "get_weather",
    "Gets the current weather for a location");

var searchTool = AIFunctionFactory.Create(
    async (string query) => await SearchDatabaseAsync(query),
    "search_database",
    "Searches the knowledge base for relevant information");

// Configure chat client with function invocation
IChatClient chatClient = new ChatClientBuilder(innerClient)
    .UseLogging(loggerFactory)
    .UseFunctionInvocation()
    .Build();

// Use with tools
var options = new ChatOptions
{
    Tools = new[] { weatherTool, searchTool }
};

var response = await chatClient.CompleteAsync(
    "What's the weather in Seattle and find documents about Azure?",
    options);
```

---

## 8. Comparison with Alternatives

### When to Use Microsoft.Extensions.AI

**Best for:**
- Simple chatbots and conversational interfaces
- Basic LLM calls without complex orchestration
- Applications needing provider flexibility
- Microservices requiring lightweight AI integration
- Building reusable AI libraries

**Example scenario:** A customer support chat widget that calls an LLM for responses.

### When to Use Semantic Kernel

**Best for:**
- Complex multi-step workflows
- Agent-based applications
- Plugin architectures
- Memory and context management across sessions
- Planning and reasoning tasks

**Example scenario:** An AI assistant that can browse the web, execute code, and maintain conversation history across sessions.

### When to Use Direct SDK Calls

**Best for:**
- Provider-specific features not exposed by abstractions
- Maximum performance with no overhead
- Simple scripts or one-off tools
- Features only available in latest SDK versions

**Example scenario:** Using OpenAI's specific JSON mode or fine-tuned model features.

### Comparison Matrix

| Feature | Extensions.AI | Semantic Kernel | Direct SDK |
|---------|--------------|-----------------|------------|
| Provider abstraction | Excellent | Good | None |
| Learning curve | Low | Medium | Varies |
| Flexibility | High | Medium | Highest |
| Built-in features | Middleware | Full orchestration | Provider-specific |
| RAG support | Basic | Advanced | Manual |
| Agent support | Via tools | Native | Manual |
| Community | Growing | Large | Provider forums |

---

## 9. Best Practices

### Dependency Injection Patterns

```csharp
// Program.cs or Startup.cs
var builder = WebApplication.CreateBuilder(args);

// Register embedding generator
builder.Services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(sp =>
{
    return new AzureOpenAIClient(
        new Uri(builder.Configuration["Azure:OpenAI:Endpoint"]!),
        new DefaultAzureCredential())
        .GetEmbeddingClient("text-embedding-3-small")
        .AsIEmbeddingGenerator();
});

// Register chat client with middleware
builder.Services.AddChatClient(sp =>
{
    var innerClient = new AzureOpenAIClient(
        new Uri(builder.Configuration["Azure:OpenAI:Endpoint"]!),
        new DefaultAzureCredential())
        .AsChatClient("gpt-4o");

    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
    var cache = sp.GetRequiredService<IDistributedCache>();

    return new ChatClientBuilder(innerClient)
        .UseLogging(loggerFactory)
        .UseDistributedCache(cache)
        .UseOpenTelemetry(loggerFactory)
        .UseFunctionInvocation()
        .Build();
});

// Register vector store
builder.Services.AddSingleton<IVectorStore>(sp =>
{
    var embeddingGenerator = sp.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();
    return new SqliteVectorStore(
        builder.Configuration.GetConnectionString("VectorDb")!,
        new() { EmbeddingGenerator = embeddingGenerator });
});

// Register RAG services
builder.Services.AddScoped<RagIngestionService>();
builder.Services.AddScoped<RagQueryService>();
```

### Error Handling and Resilience

```csharp
using Microsoft.Extensions.Http.Resilience;
using Polly;

// Configure resilience for HTTP-based AI clients
builder.Services.AddHttpClient("OpenAI")
    .AddStandardResilienceHandler(options =>
    {
        options.Retry.MaxRetryAttempts = 3;
        options.Retry.Delay = TimeSpan.FromSeconds(2);
        options.Retry.BackoffType = DelayBackoffType.ExponentialWithJitter;
        options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
        options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(30);
        options.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(2);
    });

// Custom retry wrapper for non-HTTP clients
public class ResilientChatClient : DelegatingChatClient
{
    private readonly ResiliencePipeline _pipeline;

    public ResilientChatClient(IChatClient innerClient, ResiliencePipeline pipeline)
        : base(innerClient)
    {
        _pipeline = pipeline;
    }

    public override async Task<ChatCompletion> CompleteAsync(
        IList<ChatMessage> chatMessages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        return await _pipeline.ExecuteAsync(async ct =>
            await base.CompleteAsync(chatMessages, options, ct),
            cancellationToken);
    }
}

// Build resilience pipeline
var pipeline = new ResiliencePipelineBuilder()
    .AddRetry(new RetryStrategyOptions
    {
        MaxRetryAttempts = 3,
        Delay = TimeSpan.FromSeconds(2),
        BackoffType = DelayBackoffType.ExponentialWithJitter,
        ShouldHandle = new PredicateBuilder().Handle<HttpRequestException>()
    })
    .AddTimeout(TimeSpan.FromMinutes(2))
    .Build();
```

### Performance Considerations

```csharp
// 1. Batch embedding generation
var texts = documents.Select(d => d.Content).ToList();
var embeddings = await embeddingGenerator.GenerateAsync(texts); // Single API call

// 2. Use streaming for long responses
await foreach (var update in chatClient.CompleteStreamingAsync(messages))
{
    Console.Write(update.Text);
}

// 3. Cache embeddings for frequently accessed content
builder.Services.AddMemoryCache();

public class CachingEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>
{
    private readonly IEmbeddingGenerator<string, Embedding<float>> _inner;
    private readonly IMemoryCache _cache;

    public async Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values,
        EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var results = new List<Embedding<float>>();
        var uncached = new List<string>();

        foreach (var value in values)
        {
            var cacheKey = $"embedding:{ComputeHash(value)}";
            if (_cache.TryGetValue<Embedding<float>>(cacheKey, out var cached))
            {
                results.Add(cached);
            }
            else
            {
                uncached.Add(value);
            }
        }

        if (uncached.Any())
        {
            var generated = await _inner.GenerateAsync(uncached, options, cancellationToken);
            foreach (var (text, embedding) in uncached.Zip(generated))
            {
                var cacheKey = $"embedding:{ComputeHash(text)}";
                _cache.Set(cacheKey, embedding, TimeSpan.FromHours(24));
                results.Add(embedding);
            }
        }

        return new GeneratedEmbeddings<Embedding<float>>(results);
    }
}

// 4. Parallel processing for large ingestion jobs
await Parallel.ForEachAsync(
    documents,
    new ParallelOptions { MaxDegreeOfParallelism = 4 },
    async (doc, ct) =>
    {
        var embedding = await embeddingGenerator.GenerateVectorAsync(doc.Content);
        doc.Embedding = embedding;
        await collection.UpsertAsync(doc);
    });
```

### Configuration Best Practices

```json
// appsettings.json
{
  "AI": {
    "Provider": "AzureOpenAI",
    "AzureOpenAI": {
      "Endpoint": "https://your-resource.openai.azure.com/",
      "ChatDeployment": "gpt-4o",
      "EmbeddingDeployment": "text-embedding-3-small"
    },
    "OpenAI": {
      "ApiKey": "your-api-key",
      "ChatModel": "gpt-4o",
      "EmbeddingModel": "text-embedding-3-small"
    },
    "Ollama": {
      "Endpoint": "http://localhost:11434",
      "ChatModel": "llama3.1",
      "EmbeddingModel": "all-minilm"
    }
  },
  "VectorStore": {
    "Provider": "Qdrant",
    "ConnectionString": "http://localhost:6333",
    "CollectionName": "documents"
  },
  "Rag": {
    "ChunkSize": 500,
    "ChunkOverlap": 50,
    "TopK": 5,
    "MinRelevanceScore": 0.7
  }
}
```

---

## 10. Sample Project Structure

```
src/
  MyRagApp/
    Program.cs
    Models/
      KnowledgeDocument.cs
    Services/
      RagIngestionService.cs
      RagQueryService.cs
      DocumentChunker.cs
    Middleware/
      RateLimitingChatClient.cs
      CachingEmbeddingGenerator.cs
    Configuration/
      AiOptions.cs
      VectorStoreOptions.cs
    appsettings.json
    appsettings.Development.json
tests/
  MyRagApp.Tests/
    Services/
      RagIngestionServiceTests.cs
      RagQueryServiceTests.cs
```

---

## References

### Official Documentation
- [Microsoft.Extensions.AI Libraries](https://learn.microsoft.com/en-us/dotnet/ai/microsoft-extensions-ai)
- [Build a minimal .NET AI RAG app](https://learn.microsoft.com/en-us/dotnet/ai/quickstarts/build-vector-search-app)
- [Data Ingestion for .NET AI](https://learn.microsoft.com/en-us/dotnet/ai/conceptual/data-ingestion)
- [Microsoft.Extensions.VectorData Preview](https://devblogs.microsoft.com/dotnet/introducing-microsoft-extensions-vector-data/)

### Blog Posts
- [AI and Vector Data Extensions GA Announcement](https://devblogs.microsoft.com/dotnet/ai-vector-data-dotnet-extensions-ga/)
- [Semantic Kernel and Microsoft.Extensions.AI: Better Together](https://devblogs.microsoft.com/semantic-kernel/semantic-kernel-and-microsoft-extensions-ai-better-together-part-1/)
- [Configuring Microsoft.AI.Extensions with multiple providers](https://weblog.west-wind.com/posts/2025/May/30/Configuring-MicrosoftAIExtension-with-multiple-providers)

### NuGet Packages
- [Microsoft.Extensions.AI](https://www.nuget.org/packages/Microsoft.Extensions.AI/)
- [Microsoft.Extensions.AI.Abstractions](https://www.nuget.org/packages/Microsoft.Extensions.AI.Abstractions/)
- [Microsoft.Extensions.VectorData.Abstractions](https://www.nuget.org/packages/Microsoft.Extensions.VectorData.Abstractions/)

### Sample Code
- [dotnet/ai-samples GitHub](https://aka.ms/meai-samples)
- [eShopSupport End-to-End Sample](https://github.com/dotnet/eShopSupport)
