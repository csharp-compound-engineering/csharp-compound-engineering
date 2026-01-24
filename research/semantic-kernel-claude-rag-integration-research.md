# Semantic Kernel + Claude Integration for RAG: Comprehensive Research

**Research Date:** January 2026
**Focus:** Integrating Anthropic Claude with Microsoft Semantic Kernel for RAG applications in C#

> **UPDATE (January 24, 2026):** This document has been updated to reflect the latest API changes. The PostgreSQL connector package has been renamed from `Microsoft.SemanticKernel.Connectors.Postgres` to `Microsoft.SemanticKernel.Connectors.PgVector`. See [semantic-kernel-pgvector-package-update.md](./semantic-kernel-pgvector-package-update.md) for complete details on the breaking changes.

---

## Table of Contents

1. [Executive Summary](#executive-summary)
2. [Semantic Kernel Claude Connectors](#semantic-kernel-claude-connectors)
3. [RAG Architecture with Semantic Kernel](#rag-architecture-with-semantic-kernel)
4. [Claude-Specific Considerations](#claude-specific-considerations)
5. [Embedding Options](#embedding-options)
6. [Code Examples](#code-examples)
7. [Best Practices](#best-practices)
8. [Alternatives and Comparisons](#alternatives-and-comparisons)
9. [Known Issues and Limitations](#known-issues-and-limitations)
10. [Sources](#sources)

---

## Executive Summary

Integrating Claude with Microsoft Semantic Kernel for RAG applications requires understanding the current connector landscape, which is evolving rapidly. As of early 2026:

- **No Official Native Connector**: Microsoft has not released a native `Microsoft.SemanticKernel.Connectors.Anthropic` package for direct Claude API access
- **Recommended Paths**:
  1. **Anthropic.SDK** (third-party) - Best option for direct Claude API access with full Semantic Kernel integration
  2. **Amazon Bedrock Connector** (official, experimental) - For Claude access via AWS Bedrock
- **Microsoft.Extensions.AI** is the new abstraction layer that both approaches leverage
- Claude does not provide embedding services, requiring a hybrid approach with OpenAI, Azure OpenAI, or local models like Ollama

---

## Semantic Kernel Claude Connectors

### Option 1: Anthropic.SDK (Recommended for Direct API Access)

The [Anthropic.SDK](https://github.com/tghamm/Anthropic.SDK) by tghamm is the most mature option for integrating Claude with Semantic Kernel.

**Installation:**
```bash
dotnet add package Anthropic.SDK
```

**Current Version:** 5.9.0 (as of January 2026)

**Target Frameworks:** NetStandard 2.0, .NET 8.0, .NET 10.0

**Key Features:**
- Full IChatClient implementation from Microsoft.Extensions.AI
- Streaming support
- Function calling/tool use
- Extended thinking (Claude 3.7+)
- Prompt caching
- Vision/image analysis
- Token counting

**Basic Integration:**
```csharp
using Anthropic.SDK;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;

// Create the chat client with function invocation support
IChatClient CreateChatClient(IServiceProvider _) =>
    new ChatClientBuilder(new AnthropicClient().Messages)
        .UseFunctionInvocation()
        .Build();

// Build the Semantic Kernel
var kernelBuilder = Kernel.CreateBuilder();
kernelBuilder.Plugins.AddFromType<MyPlugins>("MyPlugins");
kernelBuilder.Services.AddSingleton(CreateChatClient);

var kernel = kernelBuilder.Build();
```

### Option 2: Amazon Bedrock Connector (Official, Experimental)

For organizations using AWS, the official Amazon connector provides Claude access through Bedrock.

**Installation:**
```bash
dotnet add package Microsoft.SemanticKernel.Connectors.Amazon --prerelease
```

**Important:** This connector is experimental and requires pragma warnings to be disabled:
```csharp
#pragma warning disable SKEXP0070
```

**Configuration:**
```csharp
using Amazon;
using Amazon.BedrockRuntime;
using Microsoft.SemanticKernel;

var bedrockClient = new AmazonBedrockRuntimeClient(RegionEndpoint.USEast1);

var kernelBuilder = Kernel.CreateBuilder();
kernelBuilder.AddBedrockChatCompletionService(
    modelId: "anthropic.claude-3-5-sonnet-20241022-v2:0",
    bedrockRuntime: bedrockClient
);
```

**Known Limitations:**
- Function calling may not work properly with Anthropic models via this connector
- Still in alpha stage
- Limited feature parity compared to OpenAI connectors

### Why No Native Connector?

Microsoft's direction is toward **Microsoft.Extensions.AI** as the universal abstraction layer. Since Anthropic does not provide an official .NET SDK, the community has filled this gap with Anthropic.SDK, which implements the IChatClient interface.

---

## RAG Architecture with Semantic Kernel

### Core Components

1. **Vector Store Connectors** - Store and retrieve embeddings
2. **Embedding Services** - Generate vector representations of text
3. **Text Chunking** - Split documents into manageable pieces
4. **Retrieval Plugins** - Search and retrieve relevant context
5. **Chat Completion** - Generate responses using retrieved context

### Vector Store Options

**Available Connectors:**

| Store | Package | Status |
|-------|---------|--------|
| In-Memory | `Microsoft.SemanticKernel` (built-in) | Stable |
| Azure AI Search | `Microsoft.SemanticKernel.Connectors.AzureAISearch` | Stable |
| PostgreSQL/pgvector | `Microsoft.SemanticKernel.Connectors.PgVector` | Preview | *(Note: Package renamed from `Microsoft.SemanticKernel.Connectors.Postgres` - deprecated)*
| Qdrant | `Microsoft.SemanticKernel.Connectors.Qdrant` | Preview |
| Redis | `Microsoft.SemanticKernel.Connectors.Redis` | Preview |
| Elasticsearch | `Microsoft.SemanticKernel.Connectors.Elasticsearch` | Preview |
| Pinecone | `Microsoft.SemanticKernel.Connectors.Pinecone` | Preview |
| Weaviate | `Microsoft.SemanticKernel.Connectors.Weaviate` | Preview |
| Chroma | `Microsoft.SemanticKernel.Connectors.Chroma` | Preview |

### Vector Store Abstractions

The `Microsoft.Extensions.VectorData.Abstractions` package provides the core interfaces:

```bash
dotnet add package Microsoft.Extensions.VectorData.Abstractions
```

**Data Model with Annotations:**
```csharp
using Microsoft.Extensions.VectorData;

public class Document
{
    [VectorStoreKey]
    public string Id { get; set; }

    [VectorStoreData(IsIndexed = true)]
    public string Title { get; set; }

    [VectorStoreData(IsFullTextIndexed = true)]
    public string Content { get; set; }

    [VectorStoreData]
    public string Source { get; set; }

    [VectorStoreData]
    public DateTime CreatedAt { get; set; }

    [VectorStoreVector(Dimensions: 1536,
        DistanceFunction = DistanceFunction.CosineSimilarity,
        IndexKind = IndexKind.Hnsw)]
    public ReadOnlyMemory<float>? ContentEmbedding { get; set; }
}
```

### PostgreSQL/pgvector Configuration

```csharp
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.Postgres;
using Npgsql;

#pragma warning disable SKEXP0020

// Configure the data source with pgvector support
var dataSourceBuilder = new NpgsqlDataSourceBuilder(
    "Host=localhost;Port=5432;Database=ragdb;Username=postgres;Password=postgres"
);
dataSourceBuilder.UseVector();
var dataSource = dataSourceBuilder.Build();

// Add to Kernel
var kernelBuilder = Kernel.CreateBuilder();
kernelBuilder.Services.AddSingleton(dataSource);
kernelBuilder.Services.AddPostgresVectorStore();

// Or create directly
var vectorStore = new PostgresVectorStore(dataSource);
var collection = vectorStore.GetCollection<string, Document>("documents");
```

### Qdrant Configuration

```csharp
using Microsoft.SemanticKernel.Connectors.Qdrant;
using Qdrant.Client;

#pragma warning disable SKEXP0020

var qdrantClient = new QdrantClient("localhost", 6334);
var vectorStore = new QdrantVectorStore(qdrantClient, ownsClient: true);
var collection = vectorStore.GetCollection<ulong, Document>("documents");
```

**Docker Setup for Qdrant:**
```bash
docker run -d --name qdrant -p 6333:6333 -p 6334:6334 qdrant/qdrant:latest
```

### TextSearchProvider for RAG

Semantic Kernel provides `TextSearchProvider` for RAG scenarios:

```csharp
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Data;

#pragma warning disable SKEXP0001

// Create a text search store with your vector store
var textSearchStore = new TextSearchStore<string, Document>(
    vectorStore: vectorStore.GetCollection<string, Document>("documents"),
    embeddingGenerator: embeddingService
);

// Create the search provider
var searchProvider = new TextSearchProvider(textSearchStore);

// Use in agent/plugin context
kernel.Plugins.AddFromObject(searchProvider, "Search");
```

---

## Claude-Specific Considerations

### Context Window Utilization

Claude models offer substantial context windows:
- **Claude 3.5 Sonnet/Haiku:** 200K tokens
- **Claude 3 Opus:** 200K tokens

**Strategies for Large Context:**

1. **Direct Document Injection:**
   ```csharp
   // For documents under 200K tokens, inject directly
   var chatHistory = new ChatHistory();
   chatHistory.AddSystemMessage($"""
       You are a helpful assistant. Use the following documents to answer questions:

       {documentContent}

       Always cite your sources when answering.
       """);
   chatHistory.AddUserMessage(userQuery);
   ```

2. **Chunked Retrieval for Larger Corpora:**
   ```csharp
   // Retrieve top-K relevant chunks
   var searchResults = await textSearch.SearchAsync(query, new TextSearchOptions
   {
       Top = 5
   });

   var context = string.Join("\n\n---\n\n",
       searchResults.Results.Select(r => r.Record.Content));
   ```

### Claude Citations API

Anthropic launched the [Citations API](https://claude.com/blog/introducing-citations-api) in 2025, enabling grounded responses with source references.

**Key Features:**
- Automatic sentence-level chunking of source documents
- Built-in citation generation without complex prompting
- Support for PDF and plain text documents
- Cost savings (cited_text doesn't count toward output tokens)
- Up to 15% improvement in recall accuracy vs. prompt-based citations

**Using Citations with Anthropic.SDK:**
```csharp
using Anthropic.SDK;
using Anthropic.SDK.Messaging;

var client = new AnthropicClient();

// Create message with document source for citations
var parameters = new MessageParameters
{
    Model = AnthropicModels.Claude35Sonnet,
    MaxTokens = 4096,
    Messages = new List<Message>
    {
        new Message(RoleType.User, new List<ContentBase>
        {
            new DocumentContent
            {
                Type = "document",
                Source = new DocumentSource
                {
                    Type = "text",
                    MediaType = "text/plain",
                    Data = documentContent
                },
                CacheControl = new CacheControl { Type = "ephemeral" }
            },
            new TextContent
            {
                Type = "text",
                Text = "Based on the document, what are the key points? Provide citations."
            }
        })
    }
};

var response = await client.Messages.GetClaudeMessageAsync(parameters);
// Response includes citation blocks with source references
```

**Note:** The Citations API integration through Semantic Kernel's IChatClient abstraction may require custom handling, as the standard IChatClient interface doesn't have native citation support.

### Streaming Support

**Using Anthropic.SDK with Streaming:**
```csharp
using Anthropic.SDK;
using Anthropic.SDK.Extensions;

var client = new AnthropicClient();
IChatClient chatClient = client.Messages;

var messages = new List<ChatMessage>
{
    new ChatMessage(ChatRole.User, "Explain RAG architecture in detail.")
};

var options = new ChatOptions
{
    ModelId = AnthropicModels.Claude35Sonnet,
    MaxOutputTokens = 4096,
    Temperature = 0.7f
};

await foreach (var update in chatClient.GetStreamingResponseAsync(messages, options))
{
    Console.Write(update.Text);
}
```

**Using Semantic Kernel's Streaming Interface:**
```csharp
var chatHistory = new ChatHistory();
chatHistory.AddUserMessage("Explain RAG architecture.");

await foreach (var chunk in kernel.InvokePromptStreamingAsync<StreamingChatMessageContent>(
    "{{$input}}",
    new KernelArguments { ["input"] = "Explain RAG architecture." }))
{
    Console.Write(chunk.Content);
}
```

### Extended Thinking (Claude 3.7+)

For complex reasoning tasks:
```csharp
using Anthropic.SDK.Extensions;

var options = new ChatOptions
{
    ModelId = AnthropicModels.Claude37Sonnet,
    MaxOutputTokens = 8192,
    Temperature = 1.0f  // Required for thinking
}.WithThinking(budgetTokens: 4000);  // Enable extended thinking

var response = await chatClient.GetResponseAsync(messages, options);
```

---

## Embedding Options

Since Claude does not provide embedding services, you need a separate embedding provider.

### Option 1: OpenAI Embeddings

```csharp
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Embeddings;

#pragma warning disable SKEXP0010

var kernelBuilder = Kernel.CreateBuilder();
kernelBuilder.AddOpenAITextEmbeddingGeneration(
    modelId: "text-embedding-3-small",  // or "text-embedding-3-large"
    apiKey: Environment.GetEnvironmentVariable("OPENAI_API_KEY")!
);

var kernel = kernelBuilder.Build();
var embeddingService = kernel.GetRequiredService<ITextEmbeddingGenerationService>();

// Generate embeddings
var embedding = await embeddingService.GenerateEmbeddingAsync("Sample text to embed");
```

**Dimensions:**
- `text-embedding-3-small`: 1536 dimensions (default), configurable down to 512
- `text-embedding-3-large`: 3072 dimensions (default), configurable

### Option 2: Azure OpenAI Embeddings

```csharp
#pragma warning disable SKEXP0010

var kernelBuilder = Kernel.CreateBuilder();
kernelBuilder.AddAzureOpenAITextEmbeddingGeneration(
    deploymentName: "text-embedding-3-small",
    endpoint: Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")!,
    apiKey: Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY")!
);
```

### Option 3: Ollama (Local, Free)

```csharp
using Microsoft.SemanticKernel.Connectors.Ollama;

#pragma warning disable SKEXP0070

var kernelBuilder = Kernel.CreateBuilder();
kernelBuilder.AddOllamaTextEmbeddingGeneration(
    modelId: "nomic-embed-text",  // or "mxbai-embed-large"
    endpoint: new Uri("http://localhost:11434")
);
```

**Setting Up Ollama:**
```bash
# Start Ollama
docker run -d -v ollama:/root/.ollama -p 11434:11434 --name ollama ollama/ollama

# Pull embedding model
docker exec -it ollama ollama pull nomic-embed-text
# or
docker exec -it ollama ollama pull mxbai-embed-large
```

**Popular Ollama Embedding Models:**
| Model | Dimensions | Notes |
|-------|------------|-------|
| nomic-embed-text | 768 | Good general-purpose |
| mxbai-embed-large | 1024 | Higher quality |
| all-minilm | 384 | Smaller, faster |

### Option 4: ONNX (Local, High Performance)

```csharp
using Microsoft.SemanticKernel.Connectors.Onnx;

#pragma warning disable SKEXP0070

kernelBuilder.AddBertOnnxTextEmbeddingGeneration(
    modelPath: "./models/all-MiniLM-L6-v2",
    vocabPath: "./models/vocab.txt"
);
```

### Hybrid Architecture Recommendation

For production RAG with Claude:

```csharp
// Recommended: Azure OpenAI for embeddings + Claude for generation
var kernelBuilder = Kernel.CreateBuilder();

// Embeddings from Azure OpenAI (cost-effective, high quality)
kernelBuilder.AddAzureOpenAITextEmbeddingGeneration(
    deploymentName: "text-embedding-3-small",
    endpoint: azureEndpoint,
    apiKey: azureApiKey
);

// Chat completion from Claude via Anthropic.SDK
IChatClient claudeClient = new ChatClientBuilder(new AnthropicClient().Messages)
    .UseFunctionInvocation()
    .Build();
kernelBuilder.Services.AddSingleton(claudeClient);
```

---

## Code Examples

### Complete RAG Pipeline with Claude and PostgreSQL

```csharp
using Anthropic.SDK;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.Postgres;
using Microsoft.SemanticKernel.Data;
using Microsoft.SemanticKernel.Embeddings;
using Npgsql;

#pragma warning disable SKEXP0001, SKEXP0010, SKEXP0020

// --- Configuration ---
var anthropicApiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")!;
var openAiApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")!;
var connectionString = "Host=localhost;Port=5432;Database=ragdb;Username=postgres;Password=postgres";

// --- Data Model ---
public class KnowledgeDocument
{
    [VectorStoreKey]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [VectorStoreData(IsIndexed = true)]
    public string Title { get; set; } = string.Empty;

    [VectorStoreData(IsFullTextIndexed = true)]
    public string Content { get; set; } = string.Empty;

    [VectorStoreData]
    public string Source { get; set; } = string.Empty;

    [VectorStoreData]
    public DateTime IndexedAt { get; set; } = DateTime.UtcNow;

    [VectorStoreVector(Dimensions: 1536,
        DistanceFunction = DistanceFunction.CosineSimilarity,
        IndexKind = IndexKind.Hnsw)]
    public ReadOnlyMemory<float>? ContentEmbedding { get; set; }
}

// --- Service Setup ---
var services = new ServiceCollection();

// 1. Configure PostgreSQL with pgvector
var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
dataSourceBuilder.UseVector();
var dataSource = dataSourceBuilder.Build();
services.AddSingleton(dataSource);

// 2. Configure Embedding Service (OpenAI)
services.AddOpenAITextEmbeddingGeneration(
    modelId: "text-embedding-3-small",
    apiKey: openAiApiKey
);

// 3. Configure Claude Chat Client
services.AddSingleton<IChatClient>(sp =>
{
    var client = new AnthropicClient(anthropicApiKey);
    return new ChatClientBuilder(client.Messages)
        .UseFunctionInvocation()
        .Build();
});

// 4. Configure Vector Store
services.AddPostgresVectorStore();

// 5. Build Kernel
services.AddKernel();

var serviceProvider = services.BuildServiceProvider();
var kernel = serviceProvider.GetRequiredService<Kernel>();
var embeddingService = serviceProvider.GetRequiredService<ITextEmbeddingGenerationService>();
var chatClient = serviceProvider.GetRequiredService<IChatClient>();
var vectorStore = serviceProvider.GetRequiredService<IVectorStore>();

// --- Document Ingestion ---
async Task IngestDocumentAsync(string title, string content, string source)
{
    var collection = vectorStore.GetCollection<string, KnowledgeDocument>("knowledge_base");
    await collection.EnsureCollectionExistsAsync();  // Note: Renamed from CreateCollectionIfNotExistsAsync in May 2025

    // Generate embedding
    var embedding = await embeddingService.GenerateEmbeddingAsync(content);

    var document = new KnowledgeDocument
    {
        Title = title,
        Content = content,
        Source = source,
        ContentEmbedding = embedding
    };

    await collection.UpsertAsync(document);
    Console.WriteLine($"Indexed: {title}");
}

// --- RAG Query ---
async Task<string> QueryWithRagAsync(string userQuery, int topK = 5)
{
    var collection = vectorStore.GetCollection<string, KnowledgeDocument>("knowledge_base");

    // 1. Generate query embedding
    var queryEmbedding = await embeddingService.GenerateEmbeddingAsync(userQuery);

    // 2. Search for relevant documents
    var searchResults = await collection.VectorizedSearchAsync(
        queryEmbedding,
        new VectorSearchOptions { Top = topK }
    );

    // 3. Build context from results
    var contextBuilder = new StringBuilder();
    var sources = new List<string>();

    await foreach (var result in searchResults.Results)
    {
        contextBuilder.AppendLine($"## {result.Record.Title}");
        contextBuilder.AppendLine(result.Record.Content);
        contextBuilder.AppendLine($"Source: {result.Record.Source}");
        contextBuilder.AppendLine("---");
        sources.Add(result.Record.Source);
    }

    // 4. Create prompt with context
    var messages = new List<ChatMessage>
    {
        new ChatMessage(ChatRole.System, $"""
            You are a helpful assistant that answers questions based on the provided context.
            Always cite your sources when answering.
            If the context doesn't contain relevant information, say so.

            ## Context:
            {contextBuilder}
            """),
        new ChatMessage(ChatRole.User, userQuery)
    };

    // 5. Get response from Claude
    var options = new ChatOptions
    {
        ModelId = AnthropicModels.Claude35Sonnet,
        MaxOutputTokens = 4096,
        Temperature = 0.3f
    };

    var response = await chatClient.GetResponseAsync(messages, options);

    return response.Text;
}

// --- Example Usage ---
// Ingest some documents
await IngestDocumentAsync(
    "Introduction to RAG",
    "Retrieval-Augmented Generation (RAG) is a technique that combines...",
    "docs/rag-intro.md"
);

// Query
var answer = await QueryWithRagAsync("What is RAG and how does it work?");
Console.WriteLine(answer);
```

### RAG Plugin for Semantic Kernel Agents

```csharp
using Microsoft.SemanticKernel;
using System.ComponentModel;

public class RagPlugin
{
    private readonly IVectorStore _vectorStore;
    private readonly ITextEmbeddingGenerationService _embeddingService;

    public RagPlugin(IVectorStore vectorStore, ITextEmbeddingGenerationService embeddingService)
    {
        _vectorStore = vectorStore;
        _embeddingService = embeddingService;
    }

    [KernelFunction("SearchKnowledgeBase")]
    [Description("Searches the knowledge base for information relevant to the query")]
    public async Task<string> SearchKnowledgeBaseAsync(
        [Description("The search query")] string query,
        [Description("Maximum number of results")] int maxResults = 3)
    {
        var collection = _vectorStore.GetCollection<string, KnowledgeDocument>("knowledge_base");
        var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(query);

        var results = await collection.VectorizedSearchAsync(
            queryEmbedding,
            new VectorSearchOptions { Top = maxResults }
        );

        var resultText = new StringBuilder();
        await foreach (var result in results.Results)
        {
            resultText.AppendLine($"**{result.Record.Title}**");
            resultText.AppendLine(result.Record.Content);
            resultText.AppendLine($"_Source: {result.Record.Source}_");
            resultText.AppendLine();
        }

        return resultText.ToString();
    }
}

// Register with Kernel
kernel.Plugins.AddFromObject(
    new RagPlugin(vectorStore, embeddingService),
    "RAG"
);
```

### Streaming RAG Response

```csharp
async IAsyncEnumerable<string> StreamRagResponseAsync(string query)
{
    // Get context (same as before)
    var context = await GetRelevantContextAsync(query);

    var messages = new List<ChatMessage>
    {
        new ChatMessage(ChatRole.System, $"Context:\n{context}"),
        new ChatMessage(ChatRole.User, query)
    };

    var options = new ChatOptions
    {
        ModelId = AnthropicModels.Claude35Sonnet,
        MaxOutputTokens = 4096
    };

    await foreach (var update in chatClient.GetStreamingResponseAsync(messages, options))
    {
        if (!string.IsNullOrEmpty(update.Text))
        {
            yield return update.Text;
        }
    }
}

// Usage
await foreach (var chunk in StreamRagResponseAsync("Explain the RAG process"))
{
    Console.Write(chunk);
}
```

---

## Best Practices

### Token Management

**1. Track Token Usage:**
```csharp
// Using Anthropic.SDK
var response = await client.Messages.GetClaudeMessageAsync(parameters);
Console.WriteLine($"Input tokens: {response.Usage.InputTokens}");
Console.WriteLine($"Output tokens: {response.Usage.OutputTokens}");

// Estimated cost (Claude 3.5 Sonnet)
var inputCost = response.Usage.InputTokens * 0.003m / 1000;
var outputCost = response.Usage.OutputTokens * 0.015m / 1000;
Console.WriteLine($"Estimated cost: ${inputCost + outputCost:F4}");
```

**2. Chat History Reduction:**
```csharp
using Microsoft.SemanticKernel.ChatCompletion;

// Simple: Keep last N messages
var recentHistory = chatHistory.TakeLast(10).ToList();

// Better: Use Semantic Kernel's ChatHistoryReducer
// (when available in your SK version)
```

**3. Context Window Optimization:**
```csharp
// For large document sets, chunk and retrieve only what's needed
var relevantChunks = await RetrieveTopKChunksAsync(query, k: 5);
var totalTokens = EstimateTokens(relevantChunks);

if (totalTokens > 150000) // Leave room for response
{
    relevantChunks = relevantChunks.Take(3).ToList();
}
```

### Caching Strategies

**1. Semantic Caching with Filters:**
```csharp
using Microsoft.SemanticKernel;

public class SemanticCacheFilter : IFunctionInvocationFilter
{
    private readonly IVectorStore _cacheStore;
    private readonly ITextEmbeddingGenerationService _embedding;
    private readonly float _similarityThreshold = 0.95f;

    public async Task OnFunctionInvocationAsync(
        FunctionInvocationContext context,
        Func<FunctionInvocationContext, Task> next)
    {
        // Generate embedding for the prompt
        var promptEmbedding = await _embedding.GenerateEmbeddingAsync(
            context.Arguments.ToString());

        // Check cache for similar prompts
        var cached = await SearchCacheAsync(promptEmbedding);
        if (cached != null && cached.Similarity >= _similarityThreshold)
        {
            context.Result = new FunctionResult(context.Function, cached.Response);
            return;
        }

        // Execute function
        await next(context);

        // Cache the result
        await CacheResultAsync(promptEmbedding, context.Result.ToString());
    }
}
```

**2. Claude's Built-in Prompt Caching:**
```csharp
// Using Anthropic.SDK with cache control
var parameters = new MessageParameters
{
    Model = AnthropicModels.Claude35Sonnet,
    System = new List<SystemMessage>
    {
        new SystemMessage
        {
            Text = largeSystemPrompt,
            CacheControl = new CacheControl { Type = "ephemeral" }
        }
    },
    Messages = messages
};
```

### Error Handling and Retries

**1. Using Microsoft.Extensions.Http.Resilience:**
```csharp
using Microsoft.Extensions.Http.Resilience;

services.AddHttpClient("anthropic")
    .AddStandardResilienceHandler()
    .Configure(options =>
    {
        options.Retry.MaxRetryAttempts = 5;
        options.Retry.BackoffType = DelayBackoffType.Exponential;
        options.Retry.UseJitter = true;
        options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
    });

// Use this HttpClient with AnthropicClient
services.AddSingleton<AnthropicClient>(sp =>
{
    var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
    var httpClient = httpClientFactory.CreateClient("anthropic");
    return new AnthropicClient(apiKey, httpClient);
});
```

**2. Rate Limit Handling:**
```csharp
public async Task<T> WithRetryAsync<T>(Func<Task<T>> action, int maxRetries = 3)
{
    for (int i = 0; i < maxRetries; i++)
    {
        try
        {
            return await action();
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests)
        {
            var delay = TimeSpan.FromSeconds(Math.Pow(2, i) * 1000 + Random.Shared.Next(0, 1000));
            await Task.Delay(delay);
        }
    }
    throw new Exception($"Failed after {maxRetries} retries");
}
```

### Performance Optimization

**1. Parallel Embedding Generation:**
```csharp
public async Task<List<KnowledgeDocument>> IngestDocumentsAsync(
    List<(string title, string content, string source)> documents)
{
    // Generate embeddings in parallel (batch)
    var contents = documents.Select(d => d.content).ToList();
    var embeddings = await embeddingService.GenerateEmbeddingsAsync(contents);

    var results = new List<KnowledgeDocument>();
    for (int i = 0; i < documents.Count; i++)
    {
        results.Add(new KnowledgeDocument
        {
            Title = documents[i].title,
            Content = documents[i].content,
            Source = documents[i].source,
            ContentEmbedding = embeddings[i]
        });
    }

    return results;
}
```

**2. Connection Pooling for Vector Stores:**
```csharp
// PostgreSQL connection pooling
var connectionString = "Host=localhost;...;Pooling=true;MinPoolSize=5;MaxPoolSize=100";
```

---

## Alternatives and Comparisons

### Using Claude Directly vs Through Semantic Kernel

| Aspect | Direct Claude API | Through Semantic Kernel |
|--------|-------------------|------------------------|
| **Setup Complexity** | Lower | Higher |
| **Feature Access** | Full (Citations, Thinking, etc.) | May require workarounds |
| **Abstraction** | None | Provider-agnostic |
| **Plugin Ecosystem** | Manual | Built-in |
| **Memory/RAG** | Manual | Integrated |
| **Switching Models** | Code changes | Configuration |
| **Overhead** | Minimal | Some abstraction cost |

### When Semantic Kernel Adds Value

**Use SK when:**
- Building complex agent systems
- Need provider flexibility (might switch to OpenAI/Azure)
- Want pre-built plugins and memory
- Using multiple AI services together
- Need standardized patterns across teams

**Use Direct API when:**
- Need full access to Claude-specific features (Citations, Extended Thinking)
- Building simple, focused applications
- Performance is critical
- Claude is your only/primary model

### Microsoft.Extensions.AI Position

Microsoft.Extensions.AI is becoming the foundation:
- **IChatClient** - Standard interface for chat completion
- **IEmbeddingGenerator** - Standard interface for embeddings
- Both SK and direct SDK integration use these abstractions

```csharp
// Same code works with any IChatClient implementation
IChatClient client = useAnthropic
    ? new AnthropicClient().Messages
    : openAIClient.AsChatClient();

var response = await client.GetResponseAsync(messages, options);
```

---

## Known Issues and Limitations

### Amazon Bedrock Connector
- Function calling may not work properly with Anthropic models
- Still in alpha/experimental stage
- Requires `#pragma warning disable SKEXP0070`

### Anthropic.SDK (Third-Party)
- Unofficial - not backed by Anthropic or Microsoft
- May lag behind API updates
- Some advanced features require manual handling

### General Limitations
- Claude's Citations API not natively exposed through IChatClient
- No native embedding support from Claude (requires hybrid approach)
- Extended Thinking requires specific model versions
- Vector store connectors mostly in preview status

### Workarounds

**For Citations:** Use Anthropic.SDK directly for citation-heavy use cases:
```csharp
// Direct API call when Citations are needed
var anthropicClient = new AnthropicClient();
var citationResponse = await anthropicClient.Messages.GetClaudeMessageAsync(
    citationParameters);

// Process citations separately, then continue with SK
```

**For Missing Features:** Create custom wrappers:
```csharp
public class ClaudeWithCitationsService : IChatCompletionService
{
    private readonly AnthropicClient _client;

    // Implement interface, add citation handling
}
```

---

## Package Reference Summary

```xml
<ItemGroup>
  <!-- Core Semantic Kernel -->
  <PackageReference Include="Microsoft.SemanticKernel" Version="1.30.0" />

  <!-- Claude via Anthropic.SDK -->
  <PackageReference Include="Anthropic.SDK" Version="5.9.0" />

  <!-- Embeddings (choose one or more) -->
  <PackageReference Include="Microsoft.SemanticKernel.Connectors.OpenAI" Version="1.30.0" />
  <PackageReference Include="Microsoft.SemanticKernel.Connectors.AzureOpenAI" Version="1.30.0" />
  <PackageReference Include="Microsoft.SemanticKernel.Connectors.Ollama" Version="1.30.0-alpha" />

  <!-- Vector Stores (choose one) -->
  <!-- Note: Package renamed from Microsoft.SemanticKernel.Connectors.Postgres (deprecated) -->
  <PackageReference Include="Microsoft.SemanticKernel.Connectors.PgVector" Version="1.70.0-preview" />
  <PackageReference Include="Microsoft.SemanticKernel.Connectors.Qdrant" Version="1.30.0-alpha" />
  <PackageReference Include="Microsoft.SemanticKernel.Connectors.Redis" Version="1.30.0-alpha" />
  <PackageReference Include="Microsoft.SemanticKernel.Connectors.AzureAISearch" Version="1.30.0" />

  <!-- Optional: AWS Bedrock (experimental) -->
  <PackageReference Include="Microsoft.SemanticKernel.Connectors.Amazon" Version="1.30.0-alpha" />

  <!-- Core Abstractions -->
  <PackageReference Include="Microsoft.Extensions.AI" Version="10.2.0" />
  <PackageReference Include="Microsoft.Extensions.VectorData.Abstractions" Version="10.2.0" />
</ItemGroup>
```

---

## Sources

### Official Documentation
- [Microsoft Semantic Kernel Documentation](https://learn.microsoft.com/en-us/semantic-kernel/)
- [Semantic Kernel Vector Store Connectors](https://learn.microsoft.com/en-us/semantic-kernel/concepts/vector-store-connectors/)
- [Semantic Kernel RAG Documentation](https://learn.microsoft.com/en-us/semantic-kernel/concepts/plugins/using-data-retrieval-functions-for-rag)
- [Microsoft.Extensions.AI Documentation](https://learn.microsoft.com/en-us/dotnet/ai/microsoft-extensions-ai)
- [Claude Citations Documentation](https://platform.claude.com/docs/en/build-with-claude/citations)

### GitHub Repositories
- [Anthropic.SDK (tghamm)](https://github.com/tghamm/Anthropic.SDK)
- [Microsoft Semantic Kernel](https://github.com/microsoft/semantic-kernel)
- [Semantic Kernel Streaming Sample](https://github.com/microsoft/semantic-kernel/blob/main/dotnet/samples/Concepts/ChatCompletion/OpenAI_ChatCompletionStreaming.cs)
- [Semantic Caching Sample](https://github.com/microsoft/semantic-kernel/blob/main/dotnet/samples/Concepts/Caching/SemanticCachingWithFilters.cs)
- [HttpClient Resiliency Sample](https://github.com/microsoft/semantic-kernel/blob/main/dotnet/samples/Concepts/DependencyInjection/HttpClient_Resiliency.cs)

### NuGet Packages
- [Anthropic.SDK on NuGet](https://www.nuget.org/packages/Anthropic.SDK)
- [Microsoft.Extensions.AI.Abstractions](https://www.nuget.org/packages/Microsoft.Extensions.AI.Abstractions/)

### Community Resources
- [Semantic Kernel Anthropic Discussion](https://github.com/microsoft/semantic-kernel/discussions/10335)
- [Anthropic.SDK Semantic Kernel Integration (DeepWiki)](https://deepwiki.com/tghamm/Anthropic.SDK/4.3-semantic-kernel-integration)
- [Building RAG with Semantic Kernel and Qdrant](https://rajeevpentyala.com/2025/07/29/build-a-rag-chat-app-in-c-using-semantic-kernel-ollama-and-qdrant/)
- [Token Usage Tracking in Semantic Kernel](https://devblogs.microsoft.com/semantic-kernel/track-your-token-usage-and-costs-with-semantic-kernel/)
- [Managing Chat History](https://devblogs.microsoft.com/semantic-kernel/managing-chat-history-for-large-language-models-llms/)

### Anthropic Resources
- [Introducing Citations API](https://claude.com/blog/introducing-citations-api)
- [Citations API on AWS Bedrock](https://aws.amazon.com/about-aws/whats-new/2025/06/citations-api-pdf-claude-models-amazon-bedrock/)
