# Semantic Kernel + Ollama RAG Research Report

## Executive Summary

This comprehensive research report covers using Microsoft Semantic Kernel with Ollama for building a Retrieval Augmented Generation (RAG) system. The focus is on an MCP server architecture that uses Ollama for both embeddings and chat completion, with PostgreSQL/pgvector as the vector store.

---

## 1. Ollama Overview for RAG

### What is Ollama?

[Ollama](https://ollama.com/) is an open-source tool for running large language models (LLMs) locally. It provides a simple interface for downloading, running, and managing AI models on your own hardware, eliminating the need for cloud API dependencies.

### Role in Local AI

Ollama serves as the local inference engine for both:
- **Embedding Generation**: Converting text into vector representations for semantic search
- **Chat Completion**: Generating natural language responses based on context

### Available Embedding Models

| Model | Dimensions | Memory | Context Length | MTEB Score | Speed | Best For |
|-------|------------|--------|----------------|------------|-------|----------|
| [mxbai-embed-large](https://ollama.com/library/mxbai-embed-large) | 1,024 | ~1.2GB | 512 | 64.68 | Fast | Context-heavy queries, high accuracy |
| [nomic-embed-text](https://ollama.com/library/nomic-embed-text) | 1,024 | ~0.5GB | 8,192 | 53.01 | Very Fast | Long context, short queries |
| [all-minilm](https://ollama.com/library/all-minilm) | 384 | ~100MB | 512 | Lower | Fastest | Resource-constrained environments |
| [snowflake-arctic-embed](https://ollama.com/library/snowflake-arctic-embed) | 1,024 | ~1.1GB | 512 | High | Fast | General purpose |
| [qwen3-embedding](https://ollama.com/library/qwen3-embedding) | 1,024 | Varies | 8,192 | High | Fast | Multilingual support |

### Available Chat/Completion Models

| Model | Parameters | Context Window | Memory Required | Features |
|-------|------------|----------------|-----------------|----------|
| [llama3.2](https://ollama.com/library/llama3.2) | 1B-90B | 128K | 8GB-64GB+ | Latest Meta model, tool calling |
| [llama3.1](https://ollama.com/library/llama3.1) | 8B-405B | 128K | 16GB-256GB+ | Strong reasoning |
| [mistral](https://ollama.com/library/mistral) | 7B | 32K | 8GB | Fast, efficient |
| [qwen2.5](https://ollama.com/library/qwen2.5) | 0.5B-72B | 128K | 4GB-128GB+ | Multilingual, tool calling |
| [phi3](https://ollama.com/library/phi3) | 3.8B | 4K-128K | 4GB | Microsoft, compact |
| [deepseek-r1](https://ollama.com/library/deepseek-r1) | 1.5B-671B | 64K | 8GB-256GB+ | Reasoning focused |

### Model Comparison for RAG

**Recommended Embedding Model**: `mxbai-embed-large`
- Best MTEB retrieval score (64.68)
- Matches OpenAI text-embedding-3-large performance
- 1,024 dimensions (good balance of quality and storage)

**Alternative Embedding Model**: `nomic-embed-text`
- Faster processing (12,450 tokens/sec vs 8,920 on RTX 4090)
- Longer context window (8,192 vs 512 tokens)
- Better for short/direct queries

**Recommended Chat Model**: `llama3.2:8b` or `qwen2.5:7b`
- Good balance of quality and resource usage
- Function calling support for agentic RAG
- 128K context window for large retrieved contexts

### Running Ollama

**Local Installation**:
```bash
# macOS/Linux
curl -fsSL https://ollama.com/install.sh | sh

# Pull models
ollama pull mxbai-embed-large
ollama pull llama3.2
ollama pull qwen2.5:7b

# List models
ollama list

# Run interactively
ollama run llama3.2
```

**Docker (CPU)**:
```bash
docker run -d -v ollama:/root/.ollama -p 11434:11434 --name ollama ollama/ollama
```

**Docker (GPU - NVIDIA)**:
```bash
docker run -d --gpus=all -v ollama:/root/.ollama -p 11434:11434 --name ollama ollama/ollama
```

---

## 2. Semantic Kernel Ollama Connector

### Package Information

| Package | Version | Status |
|---------|---------|--------|
| [Microsoft.SemanticKernel.Connectors.Ollama](https://www.nuget.org/packages/Microsoft.SemanticKernel.Connectors.Ollama) | 1.68.0-alpha | Prerelease (Alpha) |

### Installation

```bash
dotnet add package Microsoft.SemanticKernel.Connectors.Ollama --prerelease
```

### Dependencies

- **OllamaSharp** (>= 5.4.11) - Native Ollama API bindings
- **Microsoft.SemanticKernel.Abstractions** (>= 1.68.0)
- **Microsoft.SemanticKernel.Core** (>= 1.68.0)

### Required Pragma Warnings

The Ollama connector is experimental and requires pragma warning suppression:

```csharp
#pragma warning disable SKEXP0070

// Your code using Ollama connector
```

Alternatively, use the attribute on classes:
```csharp
[Experimental("SKEXP0070")]
public class MyRagService
{
    // Implementation
}
```

### Supported Features

| Feature | Status |
|---------|--------|
| Chat Completion | Supported |
| Text Generation | Supported |
| Text Embeddings | Supported |
| Streaming Responses | Supported (Ollama v0.8.0+) |
| Function Calling | Supported (streaming since v0.8.0) |
| Vision/Multimodal | Supported (for supported models) |

---

## 3. Embedding Generation with Ollama

### AddOllamaTextEmbeddingGeneration() Configuration

```csharp
#pragma warning disable SKEXP0070

using Microsoft.SemanticKernel;

IKernelBuilder kernelBuilder = Kernel.CreateBuilder();

kernelBuilder.AddOllamaTextEmbeddingGeneration(
    modelId: "mxbai-embed-large",              // Embedding model name
    endpoint: new Uri("http://localhost:11434"), // Ollama endpoint
    serviceId: "ollama-embeddings"              // Optional service ID
);

Kernel kernel = kernelBuilder.Build();
```

### ITextEmbeddingGenerationService Interface

The core interface for embedding generation:

```csharp
public interface ITextEmbeddingGenerationService
{
    // Generate embeddings for multiple texts
    Task<IList<ReadOnlyMemory<float>>> GenerateEmbeddingsAsync(
        IList<string> data,
        Kernel? kernel = null,
        CancellationToken cancellationToken = default);
}

// Extension method for single embedding
public static async Task<ReadOnlyMemory<float>> GenerateEmbeddingAsync(
    this ITextEmbeddingGenerationService service,
    string data,
    Kernel? kernel = null,
    CancellationToken cancellationToken = default);
```

### Generating Embeddings for Documents

```csharp
#pragma warning disable SKEXP0070

using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Embeddings;

// Get embedding service from kernel
var embeddingService = kernel.GetRequiredService<ITextEmbeddingGenerationService>();

// Single document embedding
string documentContent = "This is the document content to embed.";
ReadOnlyMemory<float> embedding = await embeddingService.GenerateEmbeddingAsync(documentContent);

Console.WriteLine($"Embedding dimensions: {embedding.Length}"); // Output: 1024 for mxbai-embed-large
```

### Generating Embeddings for Queries

```csharp
// Query embedding (same process as document embedding)
string userQuery = "What are the key features of the product?";
ReadOnlyMemory<float> queryEmbedding = await embeddingService.GenerateEmbeddingAsync(userQuery);

// Use queryEmbedding for vector search
```

### Batch Embedding Generation

```csharp
// Batch generation for multiple texts
var texts = new List<string>
{
    "Document 1 content",
    "Document 2 content",
    "Document 3 content",
    "Document 4 content"
};

IList<ReadOnlyMemory<float>> embeddings =
    await embeddingService.GenerateEmbeddingsAsync(texts);

for (int i = 0; i < embeddings.Count; i++)
{
    Console.WriteLine($"Document {i + 1}: {embeddings[i].Length} dimensions");
}
```

### Standalone Ollama Service (Without Kernel)

```csharp
#pragma warning disable SKEXP0070

using Microsoft.SemanticKernel.Embeddings;
using OllamaSharp;

// Create OllamaSharp client directly
using var ollamaClient = new OllamaApiClient(
    uriString: "http://localhost:11434",
    defaultModel: "mxbai-embed-large"
);

// Convert to Semantic Kernel embedding service
ITextEmbeddingGenerationService embeddingService =
    ollamaClient.AsTextEmbeddingGenerationService();

// Generate embeddings
ReadOnlyMemory<float> embedding = await embeddingService.GenerateEmbeddingAsync("Sample text");
```

### Embedding Dimensions by Model

| Model | Dimensions | Notes |
|-------|------------|-------|
| mxbai-embed-large | 1,024 | Recommended for RAG |
| nomic-embed-text | 1,024 | Good for long context |
| all-minilm | 384 | Lightweight, fast |
| text-embedding-ada-002 (OpenAI) | 1,536 | For comparison |

**Important**: Ensure your vector store schema dimensions match your embedding model output.

---

## 4. Chat Completion with Ollama

### AddOllamaChatCompletion() Configuration

```csharp
#pragma warning disable SKEXP0070

using Microsoft.SemanticKernel;

IKernelBuilder kernelBuilder = Kernel.CreateBuilder();

kernelBuilder.AddOllamaChatCompletion(
    modelId: "llama3.2",                        // Chat model name
    endpoint: new Uri("http://localhost:11434"), // Ollama endpoint
    serviceId: "ollama-chat"                     // Optional service ID
);

Kernel kernel = kernelBuilder.Build();
```

### IChatCompletionService Interface

```csharp
public interface IChatCompletionService
{
    // Non-streaming response
    Task<IReadOnlyList<ChatMessageContent>> GetChatMessageContentsAsync(
        ChatHistory chatHistory,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        CancellationToken cancellationToken = default);

    // Streaming response
    IAsyncEnumerable<StreamingChatMessageContent> GetStreamingChatMessageContentsAsync(
        ChatHistory chatHistory,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        CancellationToken cancellationToken = default);
}
```

### ChatHistory Management

```csharp
using Microsoft.SemanticKernel.ChatCompletion;

// Create chat history
var chatHistory = new ChatHistory();

// Add system message (sets behavior/context)
chatHistory.AddSystemMessage(
    "You are a helpful assistant that answers questions based on the provided context. " +
    "If you don't know the answer, say so.");

// Add user message
chatHistory.AddUserMessage("What is the capital of France?");

// Get chat service
var chatService = kernel.GetRequiredService<IChatCompletionService>();

// Generate response
var response = await chatService.GetChatMessageContentAsync(chatHistory);

// Add assistant response to history for multi-turn conversations
chatHistory.AddAssistantMessage(response.Content ?? "");

Console.WriteLine(response.Content);
```

### Streaming Responses

```csharp
var chatHistory = new ChatHistory();
chatHistory.AddSystemMessage("You are a helpful assistant.");
chatHistory.AddUserMessage("Explain RAG in simple terms.");

var chatService = kernel.GetRequiredService<IChatCompletionService>();

// Stream the response
Console.Write("Assistant: ");
var fullResponse = new StringBuilder();

await foreach (var chunk in chatService.GetStreamingChatMessageContentsAsync(chatHistory))
{
    Console.Write(chunk.Content);
    fullResponse.Append(chunk.Content);
}

Console.WriteLine();

// Add complete response to history
chatHistory.AddAssistantMessage(fullResponse.ToString());
```

### System Prompts and Context Injection

```csharp
// Build context from retrieved documents
string retrievedContext = """
    Document 1: RAG stands for Retrieval Augmented Generation...
    Document 2: Vector databases store embeddings...
    Document 3: Semantic Kernel is Microsoft's SDK...
    """;

var chatHistory = new ChatHistory();

// Inject context via system message
chatHistory.AddSystemMessage($"""
    You are a knowledgeable assistant. Use the following context to answer questions.
    If the context doesn't contain relevant information, say "I don't have information about that."

    Context:
    {retrievedContext}
    """);

chatHistory.AddUserMessage("What is RAG?");

var response = await chatService.GetChatMessageContentAsync(chatHistory);
```

### Standalone Ollama Chat Service

```csharp
#pragma warning disable SKEXP0070

using Microsoft.SemanticKernel.ChatCompletion;
using OllamaSharp;

using var ollamaClient = new OllamaApiClient(
    uriString: "http://localhost:11434",
    defaultModel: "llama3.2"
);

IChatCompletionService chatService = ollamaClient.AsChatCompletionService();

var chatHistory = new ChatHistory();
chatHistory.AddUserMessage("Hello!");

var response = await chatService.GetChatMessageContentAsync(chatHistory);
Console.WriteLine(response.Content);
```

---

## 5. RAG Pipeline with Ollama

### Complete RAG Flow

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                           RAG Pipeline with Ollama                          │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  1. User Query Received                                                     │
│     └─── "What is semantic search?"                                         │
│                                                                             │
│  2. Generate Query Embedding (Ollama - mxbai-embed-large)                   │
│     └─── [0.123, -0.456, 0.789, ...] (1024 dimensions)                     │
│                                                                             │
│  3. Search Vector Store (PostgreSQL/pgvector)                               │
│     └─── Cosine similarity search, top-k results                            │
│                                                                             │
│  4. Retrieve Relevant Documents                                             │
│     └─── Doc1: "Semantic search uses embeddings..."                         │
│     └─── Doc2: "Unlike keyword search, semantic..."                         │
│     └─── Doc3: "Vector databases enable semantic..."                        │
│                                                                             │
│  5. Build Context from Documents                                            │
│     └─── Concatenate documents with metadata                                │
│                                                                             │
│  6. Generate Response with Context (Ollama - llama3.2)                      │
│     └─── System: "Use context to answer questions"                          │
│     └─── Context: Retrieved documents                                       │
│     └─── User: "What is semantic search?"                                   │
│     └─── Response: "Semantic search is a technique that..."                 │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

### Complete RAG Implementation

```csharp
#pragma warning disable SKEXP0070

using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Embeddings;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.PgVector;
using Microsoft.Extensions.VectorData;
using Npgsql;
using System.Text;

namespace RagWithOllama;

// 1. Define Document Model
public class KnowledgeDocument
{
    [VectorStoreKey(StorageName = "id")]
    public string Id { get; set; } = string.Empty;

    [VectorStoreData(StorageName = "title")]
    public string Title { get; set; } = string.Empty;

    [VectorStoreData(StorageName = "content")]
    public string Content { get; set; } = string.Empty;

    [VectorStoreData(StorageName = "source")]
    public string? Source { get; set; }

    [VectorStoreData(StorageName = "created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [VectorStoreVector(
        Dimensions: 1024,  // Match mxbai-embed-large output
        DistanceFunction.CosineDistance,
        IndexKind.Hnsw,
        StorageName = "content_embedding")]
    public ReadOnlyMemory<float> ContentEmbedding { get; set; }
}

// 2. RAG Service Implementation
public class RagService
{
    private readonly Kernel _kernel;
    private readonly ITextEmbeddingGenerationService _embeddingService;
    private readonly IChatCompletionService _chatService;
    private readonly PostgresCollection<string, KnowledgeDocument> _collection;

    private const string SystemPrompt = """
        You are a knowledgeable assistant that answers questions based on the provided context.

        Guidelines:
        - Use ONLY the information from the provided context to answer questions
        - If the context doesn't contain relevant information, say "I don't have information about that in my knowledge base"
        - Cite specific documents when possible
        - Be concise but thorough
        - If asked about something outside the context, acknowledge the limitation
        """;

    public RagService(
        Kernel kernel,
        PostgresCollection<string, KnowledgeDocument> collection)
    {
        _kernel = kernel;
        _embeddingService = kernel.GetRequiredService<ITextEmbeddingGenerationService>();
        _chatService = kernel.GetRequiredService<IChatCompletionService>();
        _collection = collection;
    }

    public async Task InitializeAsync()
    {
        await _collection.EnsureCollectionExistsAsync();
    }

    // 3. Index Documents
    public async Task IndexDocumentAsync(
        string id,
        string title,
        string content,
        string? source = null)
    {
        // Generate embedding for document content
        ReadOnlyMemory<float> embedding = await _embeddingService.GenerateEmbeddingAsync(content);

        // Store in vector database
        await _collection.UpsertAsync(new KnowledgeDocument
        {
            Id = id,
            Title = title,
            Content = content,
            Source = source,
            ContentEmbedding = embedding,
            CreatedAt = DateTime.UtcNow
        });
    }

    // 4. Batch Index Documents
    public async Task IndexDocumentsAsync(IEnumerable<(string Id, string Title, string Content, string? Source)> documents)
    {
        var docList = documents.ToList();
        var contents = docList.Select(d => d.Content).ToList();

        // Batch generate embeddings
        var embeddings = await _embeddingService.GenerateEmbeddingsAsync(contents);

        // Store all documents
        for (int i = 0; i < docList.Count; i++)
        {
            var doc = docList[i];
            await _collection.UpsertAsync(new KnowledgeDocument
            {
                Id = doc.Id,
                Title = doc.Title,
                Content = doc.Content,
                Source = doc.Source,
                ContentEmbedding = embeddings[i],
                CreatedAt = DateTime.UtcNow
            });
        }
    }

    // 5. Retrieve Relevant Documents
    public async Task<IReadOnlyList<(KnowledgeDocument Document, double Score)>> RetrieveAsync(
        string query,
        int topK = 5,
        double minScore = 0.5)
    {
        // Generate query embedding
        ReadOnlyMemory<float> queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(query);

        // Search vector store
        var results = new List<(KnowledgeDocument, double)>();
        var searchResults = _collection.SearchAsync(queryEmbedding, top: topK);

        await foreach (var result in searchResults)
        {
            double score = result.Score ?? 0;
            if (score >= minScore)
            {
                results.Add((result.Record, score));
            }
        }

        return results;
    }

    // 6. Build Context from Documents
    private string BuildContext(IReadOnlyList<(KnowledgeDocument Document, double Score)> documents)
    {
        if (!documents.Any())
        {
            return "No relevant documents found.";
        }

        var contextBuilder = new StringBuilder();
        contextBuilder.AppendLine("Relevant Documents:");
        contextBuilder.AppendLine();

        foreach (var (doc, score) in documents)
        {
            contextBuilder.AppendLine($"--- Document: {doc.Title} (Relevance: {score:P0}) ---");
            contextBuilder.AppendLine(doc.Content);
            if (!string.IsNullOrEmpty(doc.Source))
            {
                contextBuilder.AppendLine($"Source: {doc.Source}");
            }
            contextBuilder.AppendLine();
        }

        return contextBuilder.ToString();
    }

    // 7. Generate Response (Non-Streaming)
    public async Task<string> GenerateResponseAsync(string query, int topK = 5)
    {
        // Retrieve relevant documents
        var documents = await RetrieveAsync(query, topK);

        // Build context
        string context = BuildContext(documents);

        // Create chat history with context
        var chatHistory = new ChatHistory();
        chatHistory.AddSystemMessage(SystemPrompt);
        chatHistory.AddSystemMessage($"Context:\n{context}");
        chatHistory.AddUserMessage(query);

        // Generate response
        var response = await _chatService.GetChatMessageContentAsync(chatHistory);
        return response.Content ?? "Unable to generate response.";
    }

    // 8. Generate Response (Streaming)
    public async IAsyncEnumerable<string> GenerateResponseStreamingAsync(
        string query,
        int topK = 5,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Retrieve relevant documents
        var documents = await RetrieveAsync(query, topK);

        // Build context
        string context = BuildContext(documents);

        // Create chat history with context
        var chatHistory = new ChatHistory();
        chatHistory.AddSystemMessage(SystemPrompt);
        chatHistory.AddSystemMessage($"Context:\n{context}");
        chatHistory.AddUserMessage(query);

        // Stream response
        await foreach (var chunk in _chatService.GetStreamingChatMessageContentsAsync(
            chatHistory, cancellationToken: cancellationToken))
        {
            if (!string.IsNullOrEmpty(chunk.Content))
            {
                yield return chunk.Content;
            }
        }
    }

    // 9. Multi-Turn Conversation with RAG
    public async Task<string> GenerateConversationalResponseAsync(
        ChatHistory conversationHistory,
        string query,
        int topK = 5)
    {
        // Retrieve relevant documents for current query
        var documents = await RetrieveAsync(query, topK);
        string context = BuildContext(documents);

        // Create new history with system prompt and context
        var ragHistory = new ChatHistory();
        ragHistory.AddSystemMessage(SystemPrompt);
        ragHistory.AddSystemMessage($"Context:\n{context}");

        // Add conversation history
        foreach (var message in conversationHistory)
        {
            ragHistory.Add(message);
        }

        // Add current query
        ragHistory.AddUserMessage(query);

        // Generate response
        var response = await _chatService.GetChatMessageContentAsync(ragHistory);
        return response.Content ?? "Unable to generate response.";
    }
}
```

### Prompt Engineering for RAG with Ollama Models

**Effective System Prompts**:

```csharp
// For Llama 3.x models
string llamaSystemPrompt = """
    <|begin_of_text|><|start_header_id|>system<|end_header_id|>
    You are a helpful assistant that answers questions using the provided context.
    Always base your answers on the context. If unsure, say so.
    <|eot_id|>
    """;

// For Qwen models
string qwenSystemPrompt = """
    You are Qwen, a helpful AI assistant. Answer questions based on the provided context.
    Be accurate, concise, and cite sources when possible.
    """;

// For Mistral models
string mistralSystemPrompt = """
    [INST] You are a knowledgeable assistant. Use the following context to answer questions.
    If the context doesn't contain the answer, acknowledge that limitation. [/INST]
    """;
```

**Context Window Considerations**:

| Model | Context Window | Recommended RAG Context Size |
|-------|----------------|------------------------------|
| llama3.2 | 128K tokens | Up to 100K tokens |
| mistral | 32K tokens | Up to 24K tokens |
| qwen2.5 | 128K tokens | Up to 100K tokens |
| phi3 | 4K-128K | Depends on variant |

---

## 6. Model Selection for RAG

### Best Embedding Models for RAG

| Rank | Model | Dimensions | MTEB Score | Speed | Memory | Recommendation |
|------|-------|------------|------------|-------|--------|----------------|
| 1 | mxbai-embed-large | 1,024 | 64.68 | Fast | 1.2GB | **Best overall quality** |
| 2 | nomic-embed-text | 1,024 | 53.01 | Very Fast | 0.5GB | **Best for long context** |
| 3 | snowflake-arctic-embed | 1,024 | High | Fast | 1.1GB | Good alternative |
| 4 | all-minilm | 384 | Lower | Fastest | 100MB | **Resource-constrained** |

### Best Chat Models for RAG Responses

| Model | Parameters | Quality | Speed | Memory | Use Case |
|-------|------------|---------|-------|--------|----------|
| llama3.2:8b | 8B | Excellent | Good | 8GB | **Production RAG** |
| qwen2.5:7b | 7B | Excellent | Good | 8GB | **Multilingual RAG** |
| mistral:7b | 7B | Very Good | Fast | 8GB | Quick responses |
| llama3.2:3b | 3B | Good | Fast | 4GB | Low-resource RAG |
| phi3:mini | 3.8B | Good | Fast | 4GB | Edge deployment |

### Recommended Combinations

**High Quality RAG**:
- Embedding: `mxbai-embed-large` (1,024 dims)
- Chat: `llama3.2:8b` or `qwen2.5:7b`
- Memory: 16GB+ RAM

**Balanced RAG**:
- Embedding: `nomic-embed-text` (1,024 dims)
- Chat: `mistral:7b`
- Memory: 8-16GB RAM

**Lightweight RAG**:
- Embedding: `all-minilm` (384 dims)
- Chat: `llama3.2:3b` or `phi3:mini`
- Memory: 8GB RAM

### GPU Requirements

| Configuration | GPU VRAM | Models Supported |
|---------------|----------|------------------|
| Entry | 8GB | 7B models, all embedding models |
| Standard | 16GB | 13B models, multiple concurrent models |
| Professional | 24GB | 30B models, high concurrency |
| Enterprise | 48GB+ | 70B+ models |

### Pulling Models

```bash
# Pull embedding model
ollama pull mxbai-embed-large

# Pull chat models
ollama pull llama3.2:8b
ollama pull qwen2.5:7b

# Verify models
ollama list

# Check model details
ollama show mxbai-embed-large
```

---

## 7. Ollama Configuration

### Default Endpoint

Ollama runs on `http://localhost:11434` by default.

### Environment Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `OLLAMA_HOST` | Host and port for API | `127.0.0.1:11434` |
| `OLLAMA_MODELS` | Directory for model storage | `~/.ollama/models` |
| `OLLAMA_KEEP_ALIVE` | Model keep-alive duration | `5m` |
| `OLLAMA_NUM_PARALLEL` | Max parallel requests per model | Auto (1-4) |
| `OLLAMA_MAX_LOADED_MODELS` | Max models in memory | Auto |
| `OLLAMA_MAX_QUEUE` | Max queued requests | 512 |
| `OLLAMA_DEBUG` | Enable debug logging | `false` |
| `OLLAMA_FLASH_ATTENTION` | Enable flash attention | `false` |

### Model Management

```bash
# List installed models
ollama list

# Pull a model
ollama pull mxbai-embed-large

# Delete a model
ollama rm mxbai-embed-large

# Show model info
ollama show llama3.2

# Copy/create model variant
ollama cp llama3.2 my-llama

# Run with custom parameters
ollama run llama3.2 --keepalive 1h
```

### Concurrent Request Handling

Ollama handles concurrent requests through queuing:

```bash
# Set max parallel requests (per model)
export OLLAMA_NUM_PARALLEL=4

# Set max queue size
export OLLAMA_MAX_QUEUE=512

# If queue is full, server returns 503
```

### GPU vs CPU Inference

**GPU (CUDA/ROCm)**:
- 10-50x faster inference
- Required for larger models (13B+)
- Automatic detection when available

**CPU**:
- Viable for 7B models and smaller
- Use quantized models (Q4, Q5) for better performance
- Enable `OLLAMA_CPU_THREADS` for optimization

```bash
# Check GPU usage
nvidia-smi  # NVIDIA
rocm-smi    # AMD

# Force CPU mode
export OLLAMA_HOST=0.0.0.0:11434
export CUDA_VISIBLE_DEVICES=""  # Disable GPU
```

---

## 8. Error Handling and Resilience

### Handling Ollama Unavailability

```csharp
public class ResilientOllamaService
{
    private readonly Kernel _kernel;
    private readonly ILogger<ResilientOllamaService> _logger;

    public async Task<bool> CheckOllamaHealthAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var response = await httpClient.GetAsync(
                "http://localhost:11434/api/tags",
                cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ollama health check failed");
            return false;
        }
    }

    public async Task<string?> GenerateWithFallbackAsync(
        string prompt,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var chatService = _kernel.GetRequiredService<IChatCompletionService>();
            var history = new ChatHistory();
            history.AddUserMessage(prompt);

            var response = await chatService.GetChatMessageContentAsync(
                history,
                cancellationToken: cancellationToken);

            return response.Content;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Ollama request failed");
            return null; // Or implement fallback logic
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "Ollama request timed out");
            return null;
        }
    }
}
```

### Timeout Configuration

```csharp
// Configure HttpClient with timeout
var httpClient = new HttpClient
{
    Timeout = TimeSpan.FromMinutes(5) // For large model responses
};

var ollamaClient = new OllamaApiClient(httpClient)
{
    DefaultModel = "llama3.2"
};

// Per-request timeout with CancellationToken
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
var response = await chatService.GetChatMessageContentAsync(
    history,
    cancellationToken: cts.Token);
```

**Recommended Timeout Values**:

| Model Size | Timeout |
|------------|---------|
| 7B models | 60 seconds |
| 13-14B models | 90-120 seconds |
| 30B+ models | 180+ seconds |
| Embeddings | 30 seconds |

### Retry Patterns

```csharp
using Polly;
using Polly.Retry;

public class RetryableOllamaService
{
    private readonly AsyncRetryPolicy<string?> _retryPolicy;

    public RetryableOllamaService()
    {
        _retryPolicy = Policy<string?>
            .Handle<HttpRequestException>()
            .Or<TaskCanceledException>()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt =>
                    TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), // Exponential backoff
                onRetry: (outcome, timespan, retryAttempt, context) =>
                {
                    Console.WriteLine($"Retry {retryAttempt} after {timespan.TotalSeconds}s");
                });
    }

    public async Task<string?> GenerateWithRetryAsync(
        IChatCompletionService chatService,
        ChatHistory history)
    {
        return await _retryPolicy.ExecuteAsync(async () =>
        {
            var response = await chatService.GetChatMessageContentAsync(history);
            return response.Content;
        });
    }
}
```

### Fallback Strategies

```csharp
public class FallbackRagService
{
    private readonly IChatCompletionService _primaryService;   // Ollama
    private readonly IChatCompletionService? _fallbackService; // Optional cloud fallback
    private readonly ILogger _logger;

    public async Task<string> GenerateWithFallbackAsync(ChatHistory history)
    {
        try
        {
            var response = await _primaryService.GetChatMessageContentAsync(history);
            return response.Content ?? "No response";
        }
        catch (Exception ex) when (_fallbackService != null)
        {
            _logger.LogWarning(ex, "Primary Ollama failed, using fallback");

            var fallbackResponse = await _fallbackService.GetChatMessageContentAsync(history);
            return fallbackResponse.Content ?? "No response from fallback";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "All services failed");
            return "Service temporarily unavailable. Please try again.";
        }
    }
}
```

---

## 9. Performance Optimization

### Embedding Caching Strategies

```csharp
using Microsoft.Extensions.Caching.Memory;
using System.Security.Cryptography;
using System.Text;

public class CachedEmbeddingService
{
    private readonly ITextEmbeddingGenerationService _embeddingService;
    private readonly IMemoryCache _cache;
    private readonly MemoryCacheEntryOptions _cacheOptions;

    public CachedEmbeddingService(
        ITextEmbeddingGenerationService embeddingService,
        IMemoryCache cache)
    {
        _embeddingService = embeddingService;
        _cache = cache;
        _cacheOptions = new MemoryCacheEntryOptions
        {
            SlidingExpiration = TimeSpan.FromHours(24),
            Size = 1 // Each embedding counts as 1 unit
        };
    }

    private static string ComputeHash(string text)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToBase64String(bytes);
    }

    public async Task<ReadOnlyMemory<float>> GetEmbeddingAsync(string text)
    {
        string cacheKey = $"embedding:{ComputeHash(text)}";

        if (_cache.TryGetValue(cacheKey, out ReadOnlyMemory<float> cached))
        {
            return cached;
        }

        var embedding = await _embeddingService.GenerateEmbeddingAsync(text);
        _cache.Set(cacheKey, embedding, _cacheOptions);

        return embedding;
    }

    public async Task<IList<ReadOnlyMemory<float>>> GetEmbeddingsAsync(IList<string> texts)
    {
        var results = new ReadOnlyMemory<float>[texts.Count];
        var textsToGenerate = new List<(int Index, string Text)>();

        // Check cache first
        for (int i = 0; i < texts.Count; i++)
        {
            string cacheKey = $"embedding:{ComputeHash(texts[i])}";
            if (_cache.TryGetValue(cacheKey, out ReadOnlyMemory<float> cached))
            {
                results[i] = cached;
            }
            else
            {
                textsToGenerate.Add((i, texts[i]));
            }
        }

        // Generate missing embeddings in batch
        if (textsToGenerate.Any())
        {
            var newTexts = textsToGenerate.Select(t => t.Text).ToList();
            var newEmbeddings = await _embeddingService.GenerateEmbeddingsAsync(newTexts);

            for (int i = 0; i < textsToGenerate.Count; i++)
            {
                var (originalIndex, text) = textsToGenerate[i];
                results[originalIndex] = newEmbeddings[i];

                string cacheKey = $"embedding:{ComputeHash(text)}";
                _cache.Set(cacheKey, newEmbeddings[i], _cacheOptions);
            }
        }

        return results;
    }
}
```

### Batch Processing

```csharp
public class BatchProcessor
{
    private const int BatchSize = 32; // Optimal batch size for most models

    public async Task IndexDocumentsAsync(
        IEnumerable<Document> documents,
        ITextEmbeddingGenerationService embeddingService,
        PostgresCollection<string, Document> collection)
    {
        var batches = documents
            .Select((doc, index) => new { doc, index })
            .GroupBy(x => x.index / BatchSize)
            .Select(g => g.Select(x => x.doc).ToList());

        foreach (var batch in batches)
        {
            var contents = batch.Select(d => d.Content).ToList();
            var embeddings = await embeddingService.GenerateEmbeddingsAsync(contents);

            var tasks = batch.Zip(embeddings, (doc, embedding) =>
            {
                doc.ContentEmbedding = embedding;
                return collection.UpsertAsync(doc);
            });

            await Task.WhenAll(tasks);
        }
    }
}
```

### Connection Pooling with OllamaSharp

```csharp
// Register as singleton for connection reuse
services.AddSingleton<IOllamaApiClient>(sp =>
{
    var httpClient = sp.GetRequiredService<IHttpClientFactory>()
        .CreateClient("Ollama");

    return new OllamaApiClient(httpClient)
    {
        DefaultModel = "llama3.2"
    };
});

// Configure HttpClient factory
services.AddHttpClient("Ollama", client =>
{
    client.BaseAddress = new Uri("http://localhost:11434");
    client.Timeout = TimeSpan.FromMinutes(5);
})
.ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
{
    PooledConnectionLifetime = TimeSpan.FromMinutes(15),
    MaxConnectionsPerServer = 10,
    EnableMultipleHttp2Connections = true
});
```

### Async Patterns

```csharp
public class OptimizedRagService
{
    public async Task<RagResponse> ProcessQueryAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        // Parallel embedding and context preparation
        var embeddingTask = _embeddingService.GenerateEmbeddingAsync(query);
        var contextTask = PrepareSystemContextAsync();

        await Task.WhenAll(embeddingTask, contextTask);

        var queryEmbedding = embeddingTask.Result;
        var systemContext = contextTask.Result;

        // Search with the embedding
        var documents = await SearchVectorStoreAsync(queryEmbedding, cancellationToken);

        // Generate response
        return await GenerateResponseAsync(query, documents, systemContext, cancellationToken);
    }

    // Use SemaphoreSlim for concurrency control
    private readonly SemaphoreSlim _concurrencyLimit = new(4); // Max 4 concurrent requests

    public async Task<string> GenerateWithConcurrencyControlAsync(
        string prompt,
        CancellationToken cancellationToken = default)
    {
        await _concurrencyLimit.WaitAsync(cancellationToken);
        try
        {
            return await GenerateAsync(prompt, cancellationToken);
        }
        finally
        {
            _concurrencyLimit.Release();
        }
    }
}
```

---

## 10. Complete Code Examples

### Setting Up Kernel with Ollama Services

```csharp
#pragma warning disable SKEXP0070

using Microsoft.SemanticKernel;
using Microsoft.Extensions.DependencyInjection;

public static class KernelSetup
{
    public static Kernel CreateKernelWithOllama(
        string ollamaEndpoint = "http://localhost:11434",
        string chatModel = "llama3.2",
        string embeddingModel = "mxbai-embed-large")
    {
        var builder = Kernel.CreateBuilder();

        // Add Ollama chat completion
        builder.AddOllamaChatCompletion(
            modelId: chatModel,
            endpoint: new Uri(ollamaEndpoint),
            serviceId: "ollama-chat"
        );

        // Add Ollama embedding generation
        builder.AddOllamaTextEmbeddingGeneration(
            modelId: embeddingModel,
            endpoint: new Uri(ollamaEndpoint),
            serviceId: "ollama-embeddings"
        );

        return builder.Build();
    }

    public static IServiceCollection AddOllamaRagServices(
        this IServiceCollection services,
        string connectionString,
        string ollamaEndpoint = "http://localhost:11434")
    {
        // Add Kernel
        services.AddSingleton(sp => CreateKernelWithOllama(ollamaEndpoint));

        // Add PostgreSQL vector store
        services.AddSingleton<NpgsqlDataSource>(sp =>
        {
            var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
            dataSourceBuilder.UseVector();
            return dataSourceBuilder.Build();
        });

        services.AddSingleton<PostgresVectorStore>(sp =>
        {
            var dataSource = sp.GetRequiredService<NpgsqlDataSource>();
            return new PostgresVectorStore(dataSource, ownsDataSource: false);
        });

        // Add RAG service
        services.AddScoped<RagService>();

        return services;
    }
}
```

### RAG Service Implementation

```csharp
#pragma warning disable SKEXP0070

using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Embeddings;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.PgVector;
using Microsoft.Extensions.VectorData;
using System.Runtime.CompilerServices;
using System.Text;

namespace OllamaRag;

public record RagDocument(
    string Id,
    string Title,
    string Content,
    string? Source = null,
    Dictionary<string, string>? Metadata = null
);

public record RagResponse(
    string Answer,
    IReadOnlyList<RetrievedDocument> Sources,
    TimeSpan ProcessingTime
);

public record RetrievedDocument(
    string Id,
    string Title,
    string Content,
    double RelevanceScore
);

public class RagService
{
    private readonly Kernel _kernel;
    private readonly ITextEmbeddingGenerationService _embeddingService;
    private readonly IChatCompletionService _chatService;
    private readonly PostgresCollection<string, KnowledgeDocument> _collection;
    private readonly ILogger<RagService>? _logger;

    private const int DefaultTopK = 5;
    private const double DefaultMinScore = 0.5;

    public RagService(
        Kernel kernel,
        PostgresVectorStore vectorStore,
        string collectionName = "knowledge_base",
        ILogger<RagService>? logger = null)
    {
        _kernel = kernel;
        _embeddingService = kernel.GetRequiredService<ITextEmbeddingGenerationService>();
        _chatService = kernel.GetRequiredService<IChatCompletionService>();
        _collection = vectorStore.GetCollection<string, KnowledgeDocument>(collectionName);
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await _collection.EnsureCollectionExistsAsync(cancellationToken);
        _logger?.LogInformation("RAG collection initialized");
    }

    public async Task IndexAsync(
        RagDocument document,
        CancellationToken cancellationToken = default)
    {
        var embedding = await _embeddingService.GenerateEmbeddingAsync(document.Content);

        await _collection.UpsertAsync(new KnowledgeDocument
        {
            Id = document.Id,
            Title = document.Title,
            Content = document.Content,
            Source = document.Source,
            ContentEmbedding = embedding,
            CreatedAt = DateTime.UtcNow
        }, cancellationToken);

        _logger?.LogDebug("Indexed document: {DocumentId}", document.Id);
    }

    public async Task IndexBatchAsync(
        IEnumerable<RagDocument> documents,
        int batchSize = 32,
        CancellationToken cancellationToken = default)
    {
        var docList = documents.ToList();
        var batches = docList.Chunk(batchSize);

        foreach (var batch in batches)
        {
            var contents = batch.Select(d => d.Content).ToList();
            var embeddings = await _embeddingService.GenerateEmbeddingsAsync(contents);

            for (int i = 0; i < batch.Length; i++)
            {
                var doc = batch[i];
                await _collection.UpsertAsync(new KnowledgeDocument
                {
                    Id = doc.Id,
                    Title = doc.Title,
                    Content = doc.Content,
                    Source = doc.Source,
                    ContentEmbedding = embeddings[i],
                    CreatedAt = DateTime.UtcNow
                }, cancellationToken);
            }
        }

        _logger?.LogInformation("Indexed {Count} documents", docList.Count);
    }

    public async Task<RagResponse> QueryAsync(
        string query,
        int topK = DefaultTopK,
        double minScore = DefaultMinScore,
        string? systemPrompt = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // 1. Generate query embedding
        var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(query);

        // 2. Search vector store
        var retrievedDocs = new List<RetrievedDocument>();
        var searchResults = _collection.SearchAsync(queryEmbedding, top: topK);

        await foreach (var result in searchResults.WithCancellation(cancellationToken))
        {
            double score = result.Score ?? 0;
            if (score >= minScore)
            {
                retrievedDocs.Add(new RetrievedDocument(
                    result.Record.Id,
                    result.Record.Title,
                    result.Record.Content,
                    score
                ));
            }
        }

        // 3. Build context
        var context = BuildContext(retrievedDocs);

        // 4. Generate response
        var chatHistory = new ChatHistory();
        chatHistory.AddSystemMessage(systemPrompt ?? GetDefaultSystemPrompt());
        chatHistory.AddSystemMessage($"Context:\n{context}");
        chatHistory.AddUserMessage(query);

        var response = await _chatService.GetChatMessageContentAsync(
            chatHistory,
            cancellationToken: cancellationToken);

        stopwatch.Stop();

        return new RagResponse(
            response.Content ?? "Unable to generate response.",
            retrievedDocs,
            stopwatch.Elapsed
        );
    }

    public async IAsyncEnumerable<string> QueryStreamingAsync(
        string query,
        int topK = DefaultTopK,
        double minScore = DefaultMinScore,
        string? systemPrompt = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // 1. Generate query embedding
        var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(query);

        // 2. Search and build context
        var contextBuilder = new StringBuilder("Relevant Documents:\n\n");
        var searchResults = _collection.SearchAsync(queryEmbedding, top: topK);

        await foreach (var result in searchResults.WithCancellation(cancellationToken))
        {
            double score = result.Score ?? 0;
            if (score >= minScore)
            {
                contextBuilder.AppendLine($"--- {result.Record.Title} (Relevance: {score:P0}) ---");
                contextBuilder.AppendLine(result.Record.Content);
                contextBuilder.AppendLine();
            }
        }

        // 3. Stream response
        var chatHistory = new ChatHistory();
        chatHistory.AddSystemMessage(systemPrompt ?? GetDefaultSystemPrompt());
        chatHistory.AddSystemMessage($"Context:\n{contextBuilder}");
        chatHistory.AddUserMessage(query);

        await foreach (var chunk in _chatService.GetStreamingChatMessageContentsAsync(
            chatHistory,
            cancellationToken: cancellationToken))
        {
            if (!string.IsNullOrEmpty(chunk.Content))
            {
                yield return chunk.Content;
            }
        }
    }

    private static string BuildContext(IReadOnlyList<RetrievedDocument> documents)
    {
        if (!documents.Any())
            return "No relevant documents found.";

        var builder = new StringBuilder("Relevant Documents:\n\n");
        foreach (var doc in documents)
        {
            builder.AppendLine($"--- {doc.Title} (Relevance: {doc.RelevanceScore:P0}) ---");
            builder.AppendLine(doc.Content);
            builder.AppendLine();
        }
        return builder.ToString();
    }

    private static string GetDefaultSystemPrompt() => """
        You are a knowledgeable assistant that answers questions based on the provided context.

        Guidelines:
        - Base your answers ONLY on the provided context
        - If the context doesn't contain relevant information, say so clearly
        - Cite document titles when referencing specific information
        - Be concise but thorough
        """;
}

// Data model for vector store
public class KnowledgeDocument
{
    [VectorStoreKey(StorageName = "id")]
    public string Id { get; set; } = string.Empty;

    [VectorStoreData(StorageName = "title")]
    public string Title { get; set; } = string.Empty;

    [VectorStoreData(StorageName = "content")]
    public string Content { get; set; } = string.Empty;

    [VectorStoreData(StorageName = "source")]
    public string? Source { get; set; }

    [VectorStoreData(StorageName = "created_at")]
    public DateTime CreatedAt { get; set; }

    [VectorStoreVector(
        Dimensions: 1024,
        DistanceFunction.CosineDistance,
        IndexKind.Hnsw,
        StorageName = "content_embedding")]
    public ReadOnlyMemory<float> ContentEmbedding { get; set; }
}
```

### Integration with MCP Tools

```csharp
#pragma warning disable SKEXP0070

using ModelContextProtocol.Server;
using System.ComponentModel;

public class RagMcpTools
{
    private readonly RagService _ragService;

    public RagMcpTools(RagService ragService)
    {
        _ragService = ragService;
    }

    [McpTool("rag_query")]
    [Description("Query the knowledge base using semantic search and generate an AI response")]
    public async Task<RagQueryResult> QueryAsync(
        [Description("The question or query to search for")] string query,
        [Description("Number of documents to retrieve (default: 5)")] int topK = 5,
        [Description("Minimum relevance score (0-1, default: 0.5)")] double minScore = 0.5,
        CancellationToken cancellationToken = default)
    {
        var response = await _ragService.QueryAsync(query, topK, minScore, cancellationToken: cancellationToken);

        return new RagQueryResult
        {
            Answer = response.Answer,
            Sources = response.Sources.Select(s => new SourceInfo
            {
                Id = s.Id,
                Title = s.Title,
                RelevanceScore = s.RelevanceScore
            }).ToList(),
            ProcessingTimeMs = response.ProcessingTime.TotalMilliseconds
        };
    }

    [McpTool("rag_index")]
    [Description("Index a document into the knowledge base")]
    public async Task<IndexResult> IndexDocumentAsync(
        [Description("Unique document ID")] string id,
        [Description("Document title")] string title,
        [Description("Document content to index")] string content,
        [Description("Optional source URL or reference")] string? source = null,
        CancellationToken cancellationToken = default)
    {
        await _ragService.IndexAsync(
            new RagDocument(id, title, content, source),
            cancellationToken);

        return new IndexResult
        {
            Success = true,
            DocumentId = id,
            Message = $"Document '{title}' indexed successfully"
        };
    }

    [McpTool("rag_search")]
    [Description("Search the knowledge base without generating a response (retrieval only)")]
    public async Task<SearchResult> SearchAsync(
        [Description("The search query")] string query,
        [Description("Number of results to return (default: 10)")] int limit = 10,
        CancellationToken cancellationToken = default)
    {
        var embedding = await _ragService.GenerateEmbeddingAsync(query);
        var results = await _ragService.SearchAsync(embedding, limit, cancellationToken);

        return new SearchResult
        {
            Query = query,
            Results = results.Select(r => new DocumentMatch
            {
                Id = r.Id,
                Title = r.Title,
                ContentPreview = r.Content.Length > 200
                    ? r.Content[..200] + "..."
                    : r.Content,
                RelevanceScore = r.RelevanceScore
            }).ToList()
        };
    }
}

public record RagQueryResult
{
    public string Answer { get; init; } = "";
    public List<SourceInfo> Sources { get; init; } = new();
    public double ProcessingTimeMs { get; init; }
}

public record SourceInfo
{
    public string Id { get; init; } = "";
    public string Title { get; init; } = "";
    public double RelevanceScore { get; init; }
}

public record IndexResult
{
    public bool Success { get; init; }
    public string DocumentId { get; init; } = "";
    public string Message { get; init; } = "";
}

public record SearchResult
{
    public string Query { get; init; } = "";
    public List<DocumentMatch> Results { get; init; } = new();
}

public record DocumentMatch
{
    public string Id { get; init; } = "";
    public string Title { get; init; } = "";
    public string ContentPreview { get; init; } = "";
    public double RelevanceScore { get; init; }
}
```

---

## 11. Ollama Docker Setup

### Basic Docker Run

```bash
# CPU only
docker run -d \
    --name ollama \
    -p 11434:11434 \
    -v ollama:/root/.ollama \
    ollama/ollama

# Pull models after container starts
docker exec ollama ollama pull mxbai-embed-large
docker exec ollama ollama pull llama3.2
```

### GPU Passthrough (NVIDIA)

**Prerequisites**:
1. NVIDIA GPU with CUDA support
2. NVIDIA Container Toolkit installed

```bash
# Install NVIDIA Container Toolkit (Ubuntu/Debian)
curl -fsSL https://nvidia.github.io/libnvidia-container/gpgkey | sudo gpg --dearmor -o /usr/share/keyrings/nvidia-container-toolkit-keyring.gpg
curl -s -L https://nvidia.github.io/libnvidia-container/stable/deb/nvidia-container-toolkit.list | \
    sed 's#deb https://#deb [signed-by=/usr/share/keyrings/nvidia-container-toolkit-keyring.gpg] https://#g' | \
    sudo tee /etc/apt/sources.list.d/nvidia-container-toolkit.list
sudo apt-get update && sudo apt-get install -y nvidia-container-toolkit
sudo nvidia-ctk runtime configure --runtime=docker
sudo systemctl restart docker

# Run with GPU
docker run -d \
    --name ollama \
    --gpus all \
    -p 11434:11434 \
    -v ollama:/root/.ollama \
    ollama/ollama
```

### Docker Compose Configuration

```yaml
# docker-compose.yml
version: '3.8'

services:
  ollama:
    image: ollama/ollama:latest
    container_name: ollama
    ports:
      - "11434:11434"
    volumes:
      - ollama_data:/root/.ollama
    environment:
      - OLLAMA_HOST=0.0.0.0:11434
      - OLLAMA_KEEP_ALIVE=24h
      - OLLAMA_NUM_PARALLEL=4
      - OLLAMA_MAX_LOADED_MODELS=2
    deploy:
      resources:
        reservations:
          devices:
            - driver: nvidia
              count: all
              capabilities: [gpu]
    restart: unless-stopped
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:11434/api/tags"]
      interval: 30s
      timeout: 10s
      retries: 3
      start_period: 40s

  postgres:
    image: pgvector/pgvector:pg16
    container_name: postgres-vector
    ports:
      - "5432:5432"
    environment:
      POSTGRES_USER: postgres
      POSTGRES_PASSWORD: postgres
      POSTGRES_DB: ragdb
    volumes:
      - postgres_data:/var/lib/postgresql/data
    restart: unless-stopped

volumes:
  ollama_data:
  postgres_data:
```

### Docker Compose with Model Initialization

```yaml
# docker-compose-with-init.yml
version: '3.8'

services:
  ollama:
    image: ollama/ollama:latest
    container_name: ollama
    ports:
      - "11434:11434"
    volumes:
      - ollama_data:/root/.ollama
    environment:
      - OLLAMA_HOST=0.0.0.0:11434
    deploy:
      resources:
        reservations:
          devices:
            - driver: nvidia
              count: all
              capabilities: [gpu]
    restart: unless-stopped

  ollama-init:
    image: ollama/ollama:latest
    container_name: ollama-init
    depends_on:
      - ollama
    entrypoint: ["/bin/sh", "-c"]
    command:
      - |
        sleep 10
        ollama pull mxbai-embed-large
        ollama pull llama3.2
        echo "Models downloaded successfully"
    environment:
      - OLLAMA_HOST=ollama:11434
    restart: "no"

volumes:
  ollama_data:
```

### Health Checks

```yaml
healthcheck:
  test: ["CMD-SHELL", "curl -sf http://localhost:11434/api/tags || exit 1"]
  interval: 30s
  timeout: 10s
  retries: 5
  start_period: 60s
```

```csharp
// C# health check
public async Task<bool> CheckOllamaHealthAsync()
{
    try
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        var response = await client.GetAsync("http://localhost:11434/api/tags");
        return response.IsSuccessStatusCode;
    }
    catch
    {
        return false;
    }
}
```

### Volume Mounting for Models

```bash
# Mount local directory for model persistence
docker run -d \
    --name ollama \
    -p 11434:11434 \
    -v /path/to/local/ollama:/root/.ollama \
    ollama/ollama

# Models stored in: /path/to/local/ollama/models/
```

---

## Required NuGet Packages Summary

```xml
<ItemGroup>
  <!-- Core Semantic Kernel -->
  <PackageReference Include="Microsoft.SemanticKernel" Version="1.69.0" />

  <!-- Ollama Connector (prerelease) -->
  <PackageReference Include="Microsoft.SemanticKernel.Connectors.Ollama" Version="1.68.0-alpha" />

  <!-- PostgreSQL Vector Store (prerelease) -->
  <PackageReference Include="Microsoft.SemanticKernel.Connectors.PgVector" Version="1.68.0-preview" />

  <!-- Vector Store Abstractions -->
  <PackageReference Include="Microsoft.Extensions.VectorData.Abstractions" Version="9.0.0-preview.1.25161.3" />

  <!-- Caching (optional) -->
  <PackageReference Include="Microsoft.Extensions.Caching.Memory" Version="9.0.0" />

  <!-- Resilience (optional) -->
  <PackageReference Include="Polly" Version="8.5.0" />
</ItemGroup>
```

---

## Sources

### Microsoft Documentation
- [Add embedding generation services to Semantic Kernel | Microsoft Learn](https://learn.microsoft.com/en-us/semantic-kernel/concepts/ai-services/embedding-generation/)
- [Add chat completion services to Semantic Kernel | Microsoft Learn](https://learn.microsoft.com/en-us/semantic-kernel/concepts/ai-services/chat-completion/)
- [Adding RAG to Semantic Kernel Agents | Microsoft Learn](https://learn.microsoft.com/en-us/semantic-kernel/frameworks/agent/agent-rag)
- [Prompt engineering with Semantic Kernel | Microsoft Learn](https://learn.microsoft.com/en-us/semantic-kernel/concepts/prompts/)

### Semantic Kernel Blog & Resources
- [Introducing new Ollama Connector for Local Models | Semantic Kernel Blog](https://devblogs.microsoft.com/semantic-kernel/introducing-new-ollama-connector-for-local-models/)
- [Transitioning to new IEmbeddingGenerator interface | Semantic Kernel Blog](https://devblogs.microsoft.com/semantic-kernel/transitioning-to-new-iembeddinggenerator-interface/)

### NuGet Packages
- [Microsoft.SemanticKernel.Connectors.Ollama | NuGet](https://www.nuget.org/packages/Microsoft.SemanticKernel.Connectors.Ollama)
- [OllamaSharp | NuGet](https://www.nuget.org/packages/OllamaSharp)

### Ollama Resources
- [Ollama Library](https://ollama.com/library)
- [Ollama Embedding Models Blog](https://ollama.com/blog/embedding-models)
- [mxbai-embed-large](https://ollama.com/library/mxbai-embed-large)
- [nomic-embed-text](https://ollama.com/library/nomic-embed-text)
- [all-minilm](https://ollama.com/library/all-minilm)
- [Ollama Docker](https://docs.ollama.com/docker)

### GitHub Resources
- [OllamaSharp GitHub](https://github.com/awaescher/OllamaSharp)
- [Semantic Kernel Ollama Samples](https://github.com/microsoft/semantic-kernel/blob/main/dotnet/samples/Concepts/Memory/Ollama_EmbeddingGeneration.cs)
- [Semantic Kernel Ollama Streaming Sample](https://github.com/microsoft/semantic-kernel/blob/main/dotnet/samples/Concepts/ChatCompletion/Ollama_ChatCompletionStreaming.cs)

### Community Tutorials
- [RAG without the cloud: .NET + Semantic Kernel + Ollama | DEV Community](https://dev.to/frankiey/rag-without-the-cloud-net-semantic-kernel-ollama-on-your-laptop-1hg7)
- [Build a RAG Chat App in C# Using Semantic Kernel, Ollama, and QDrant](https://rajeevpentyala.com/2025/07/29/build-a-rag-chat-app-in-c-using-semantic-kernel-ollama-and-qdrant/)
- [Choosing Ollama Models: The Complete 2025 Guide | Collabnix](https://collabnix.com/choosing-ollama-models-the-complete-2025-guide-for-developers-and-enterprises/)
- [Ollama Embedded Models: Complete Guide for 2025 | Collabnix](https://collabnix.com/ollama-embedded-models-the-complete-technical-guide-for-2025-enterprise-deployment/)
- [Running Ollama with Docker Compose and GPUs | DEV Community](https://dev.to/ajeetraina/running-ollama-with-docker-compose-and-gpus-lkn)
- [How to run Ollama with docker compose and GPU support | sleeplessbeastie](https://sleeplessbeastie.eu/2025/12/04/how-to-run-ollama-with-docker-compose-and-gpu-support/)

### Performance & Best Practices
- [13 Best Embedding Models in 2026 | elephas](https://elephas.app/blog/best-embedding-models)
- [How to Optimize RAG Retrieval Accuracy with Ollama Models | Markaicode](https://markaicode.com/optimize-rag-retrieval-accuracy-ollama-models/)
- [How to Implement Retry Logic for LLM API Failures | Markaicode](https://markaicode.com/llm-api-retry-logic-implementation/)
