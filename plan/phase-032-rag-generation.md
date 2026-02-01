# Phase 032: RAG Generation Service

> **Status**: NOT_STARTED
> **Effort Estimate**: 6-8 hours
> **Category**: MCP Server Core
> **Prerequisites**: Phase 028 (Embedding Service), Phase 029 (Vector Store Service)

---

## Spec References

This phase implements the RAG generation service defined in:

- **spec/mcp-server/ollama-integration.md** - [RAG Generation Model](../spec/mcp-server/ollama-integration.md#rag-generation-model)
- **spec/mcp-server/tools.md** - [RAG Query Tool](../spec/mcp-server/tools.md#1-rag-query-tool)
- **research/semantic-kernel-ollama-rag-research.md** - Complete RAG pipeline implementation patterns

---

## Objectives

1. Define the `IRagGenerationService` interface for RAG synthesis operations
2. Implement `SemanticKernelRagGenerationService` using Ollama chat completion
3. Create configurable model selection (default: mistral) from `ollama-config.json`
4. Implement context window management with token estimation
5. Build source attribution into generated responses
6. Design prompt engineering system for RAG synthesis quality
7. Integrate with the embedding service and vector store from prerequisites

---

## Acceptance Criteria

### Interface Definition

- [ ] `IRagGenerationService` interface defined in `Services/` with:
  - [ ] `GenerateResponseAsync(query, documents, options, ct)` method
  - [ ] `GenerateResponseStreamingAsync(query, documents, options, ct)` method returning `IAsyncEnumerable<string>`
  - [ ] `CheckHealthAsync(ct)` method for Ollama availability check

### Implementation

- [ ] `SemanticKernelRagGenerationService` class implements `IRagGenerationService`
- [ ] Service registered as singleton in DI container
- [ ] Uses `IChatCompletionService` from Semantic Kernel Ollama connector
- [ ] Pragma warning `SKEXP0070` applied for experimental Ollama connector

### Model Configuration

- [ ] Model name loaded from `OllamaOptions.RagModel` (default: `mistral`)
- [ ] Model endpoint loaded from `OllamaOptions.Endpoint` (default: `http://localhost:11434`)
- [ ] Timeout configuration from `OllamaOptions.TimeoutSeconds`
- [ ] Model auto-pull handled gracefully with clear error messages

### Context Window Management

- [ ] Context size estimation using token approximation (4 chars per token)
- [ ] `ContextWindowOptions` class with:
  - [ ] `MaxContextTokens` property (default: 24000 for mistral's 32K window)
  - [ ] `ReservedResponseTokens` property (default: 2000)
  - [ ] `ReservedSystemPromptTokens` property (default: 500)
- [ ] Documents truncated or excluded when exceeding context limits
- [ ] Prioritization: critical docs > higher relevance score > chronological order

### Source Attribution

- [ ] Response includes source document paths
- [ ] Response includes relevance scores for each source
- [ ] Response includes character count for each source
- [ ] Linked documents tracked separately from direct matches

### Prompt Engineering

- [ ] System prompt template optimized for RAG synthesis
- [ ] Instructions for citing sources by document path
- [ ] Handling of insufficient context with clear acknowledgment
- [ ] Code example extraction prioritization
- [ ] Configurable system prompt override capability

---

## Implementation Notes

### IRagGenerationService Interface

Create `Services/IRagGenerationService.cs`:

```csharp
namespace CompoundDocs.McpServer.Services;

/// <summary>
/// Service for generating RAG responses using retrieved document context.
/// </summary>
public interface IRagGenerationService
{
    /// <summary>
    /// Generates a synthesized response based on the query and retrieved documents.
    /// </summary>
    /// <param name="query">The user's question or query.</param>
    /// <param name="documents">Retrieved documents with relevance scores.</param>
    /// <param name="options">Optional generation options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>RAG response with answer and source attribution.</returns>
    Task<RagResponse> GenerateResponseAsync(
        string query,
        IReadOnlyList<RetrievedDocument> documents,
        RagGenerationOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a streaming response for real-time display.
    /// </summary>
    /// <param name="query">The user's question or query.</param>
    /// <param name="documents">Retrieved documents with relevance scores.</param>
    /// <param name="options">Optional generation options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Async enumerable of response chunks.</returns>
    IAsyncEnumerable<string> GenerateResponseStreamingAsync(
        string query,
        IReadOnlyList<RetrievedDocument> documents,
        RagGenerationOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if the Ollama RAG model is available.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the service is healthy and the model is available.</returns>
    Task<bool> CheckHealthAsync(CancellationToken cancellationToken = default);
}
```

### Response and Options Models

Create `Models/RagModels.cs`:

```csharp
namespace CompoundDocs.McpServer.Models;

/// <summary>
/// A document retrieved from vector search for RAG context.
/// </summary>
public record RetrievedDocument(
    string Path,
    string Title,
    string Content,
    int CharCount,
    double RelevanceScore,
    string DocType,
    string PromotionLevel,
    DateTime? Date = null);

/// <summary>
/// A document included via link traversal.
/// </summary>
public record LinkedDocument(
    string Path,
    string Title,
    string Content,
    int CharCount,
    string LinkedFrom);

/// <summary>
/// RAG generation response with source attribution.
/// </summary>
public record RagResponse(
    string Answer,
    IReadOnlyList<SourceAttribution> Sources,
    IReadOnlyList<LinkedDocumentAttribution> LinkedDocs,
    TimeSpan ProcessingTime);

/// <summary>
/// Source attribution for a retrieved document.
/// </summary>
public record SourceAttribution(
    string Path,
    string Title,
    int CharCount,
    double RelevanceScore);

/// <summary>
/// Attribution for a linked document.
/// </summary>
public record LinkedDocumentAttribution(
    string Path,
    string Title,
    int CharCount,
    string LinkedFrom);

/// <summary>
/// Options for RAG generation.
/// </summary>
public class RagGenerationOptions
{
    /// <summary>
    /// Maximum tokens for the context window.
    /// </summary>
    public int MaxContextTokens { get; set; } = 24000;

    /// <summary>
    /// Tokens reserved for the response generation.
    /// </summary>
    public int ReservedResponseTokens { get; set; } = 2000;

    /// <summary>
    /// Custom system prompt override (null uses default).
    /// </summary>
    public string? SystemPromptOverride { get; set; }

    /// <summary>
    /// Whether to include linked documents in context.
    /// </summary>
    public bool IncludeLinkedDocs { get; set; } = true;

    /// <summary>
    /// Maximum number of linked documents to include.
    /// </summary>
    public int MaxLinkedDocs { get; set; } = 5;
}
```

### SemanticKernelRagGenerationService Implementation

Create `Services/SemanticKernelRagGenerationService.cs`:

```csharp
#pragma warning disable SKEXP0070 // Ollama connector is experimental

using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using CompoundDocs.McpServer.Models;
using CompoundDocs.McpServer.Options;

namespace CompoundDocs.McpServer.Services;

/// <summary>
/// RAG generation service using Semantic Kernel with Ollama chat completion.
/// </summary>
public class SemanticKernelRagGenerationService : IRagGenerationService
{
    private readonly IChatCompletionService _chatService;
    private readonly ILogger<SemanticKernelRagGenerationService> _logger;
    private readonly OllamaOptions _options;

    // Approximate chars per token for context estimation
    private const int CharsPerToken = 4;

    private const string DefaultSystemPrompt = """
        You are a helpful assistant that answers questions based on the provided documentation context.

        Guidelines:
        - Always cite your sources by referencing the document paths when using information from them
        - If the context doesn't contain enough information to answer the question, say so clearly
        - Focus on accuracy over completeness - only state what is supported by the context
        - Use code examples from the context when relevant and helpful
        - If documents have different or conflicting information, acknowledge this
        - Prioritize information from documents marked as 'critical' or 'important' promotion levels

        Format your citations like: (source: path/to/document.md)
        """;

    public SemanticKernelRagGenerationService(
        IChatCompletionService chatService,
        IOptions<OllamaOptions> options,
        ILogger<SemanticKernelRagGenerationService> logger)
    {
        _chatService = chatService;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<RagResponse> GenerateResponseAsync(
        string query,
        IReadOnlyList<RetrievedDocument> documents,
        RagGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new RagGenerationOptions();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        _logger.LogDebug(
            "Generating RAG response for query with {DocumentCount} documents",
            documents.Count);

        // Build context respecting token limits
        var (contextText, includedDocs) = BuildContext(documents, options);

        // Create chat history
        var chatHistory = new ChatHistory();
        chatHistory.AddSystemMessage(options.SystemPromptOverride ?? DefaultSystemPrompt);
        chatHistory.AddSystemMessage($"Context:\n{contextText}");
        chatHistory.AddUserMessage(query);

        // Generate response
        var response = await _chatService.GetChatMessageContentAsync(
            chatHistory,
            cancellationToken: cancellationToken);

        stopwatch.Stop();

        var answer = response.Content ?? "Unable to generate response.";

        _logger.LogInformation(
            "Generated RAG response in {ElapsedMs}ms using {SourceCount} sources",
            stopwatch.ElapsedMilliseconds,
            includedDocs.Count);

        return new RagResponse(
            Answer: answer,
            Sources: includedDocs.Select(d => new SourceAttribution(
                d.Path,
                d.Title,
                d.CharCount,
                d.RelevanceScore)).ToList(),
            LinkedDocs: [], // Linked docs handled by caller
            ProcessingTime: stopwatch.Elapsed);
    }

    public async IAsyncEnumerable<string> GenerateResponseStreamingAsync(
        string query,
        IReadOnlyList<RetrievedDocument> documents,
        RagGenerationOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        options ??= new RagGenerationOptions();

        _logger.LogDebug(
            "Starting streaming RAG response for query with {DocumentCount} documents",
            documents.Count);

        // Build context respecting token limits
        var (contextText, _) = BuildContext(documents, options);

        // Create chat history
        var chatHistory = new ChatHistory();
        chatHistory.AddSystemMessage(options.SystemPromptOverride ?? DefaultSystemPrompt);
        chatHistory.AddSystemMessage($"Context:\n{contextText}");
        chatHistory.AddUserMessage(query);

        // Stream response
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

    public async Task<bool> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(5));

            // Simple health check - try to get a minimal response
            var chatHistory = new ChatHistory();
            chatHistory.AddUserMessage("Reply with 'ok'");

            var response = await _chatService.GetChatMessageContentAsync(
                chatHistory,
                cancellationToken: cts.Token);

            return !string.IsNullOrEmpty(response.Content);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "RAG generation health check failed");
            return false;
        }
    }

    /// <summary>
    /// Builds context string from documents, respecting token limits.
    /// </summary>
    private (string Context, List<RetrievedDocument> IncludedDocs) BuildContext(
        IReadOnlyList<RetrievedDocument> documents,
        RagGenerationOptions options)
    {
        var availableTokens = options.MaxContextTokens - options.ReservedResponseTokens;
        var usedTokens = EstimateTokens(DefaultSystemPrompt); // Account for system prompt

        var contextBuilder = new StringBuilder();
        var includedDocs = new List<RetrievedDocument>();

        // Sort documents: critical first, then by relevance score
        var sortedDocs = documents
            .OrderByDescending(d => d.PromotionLevel == "critical" ? 2 :
                                   d.PromotionLevel == "important" ? 1 : 0)
            .ThenByDescending(d => d.RelevanceScore)
            .ToList();

        contextBuilder.AppendLine("Retrieved Documents:");
        contextBuilder.AppendLine();

        foreach (var doc in sortedDocs)
        {
            var docSection = FormatDocumentSection(doc);
            var docTokens = EstimateTokens(docSection);

            if (usedTokens + docTokens > availableTokens)
            {
                _logger.LogDebug(
                    "Excluding document {Path} due to context window limit ({UsedTokens}/{AvailableTokens} tokens)",
                    doc.Path,
                    usedTokens,
                    availableTokens);
                continue;
            }

            contextBuilder.Append(docSection);
            includedDocs.Add(doc);
            usedTokens += docTokens;
        }

        if (!includedDocs.Any())
        {
            contextBuilder.AppendLine("No relevant documents found for this query.");
        }

        _logger.LogDebug(
            "Built context with {DocCount} documents using approximately {TokenCount} tokens",
            includedDocs.Count,
            usedTokens);

        return (contextBuilder.ToString(), includedDocs);
    }

    /// <summary>
    /// Formats a document for inclusion in the context.
    /// </summary>
    private static string FormatDocumentSection(RetrievedDocument doc)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"--- Document: {doc.Title} ---");
        sb.AppendLine($"Path: {doc.Path}");
        sb.AppendLine($"Type: {doc.DocType}");
        sb.AppendLine($"Relevance: {doc.RelevanceScore:P0}");

        if (!string.IsNullOrEmpty(doc.PromotionLevel) && doc.PromotionLevel != "standard")
        {
            sb.AppendLine($"Priority: {doc.PromotionLevel}");
        }

        if (doc.Date.HasValue)
        {
            sb.AppendLine($"Date: {doc.Date:yyyy-MM-dd}");
        }

        sb.AppendLine();
        sb.AppendLine(doc.Content);
        sb.AppendLine();

        return sb.ToString();
    }

    /// <summary>
    /// Estimates token count for a string using character approximation.
    /// </summary>
    private static int EstimateTokens(string text)
    {
        return (text.Length + CharsPerToken - 1) / CharsPerToken;
    }
}
```

### Service Registration

Add to `Extensions/ServiceCollectionExtensions.cs`:

```csharp
/// <summary>
/// Registers RAG generation services.
/// </summary>
public static IServiceCollection AddRagGenerationServices(
    this IServiceCollection services,
    IConfiguration configuration)
{
    // Get Ollama configuration
    var ollamaOptions = configuration
        .GetSection(OllamaOptions.SectionName)
        .Get<OllamaOptions>() ?? new OllamaOptions();

    // Create Semantic Kernel with Ollama chat completion
    var kernelBuilder = Kernel.CreateBuilder();

    #pragma warning disable SKEXP0070
    kernelBuilder.AddOllamaChatCompletion(
        modelId: ollamaOptions.RagModel,
        endpoint: new Uri(ollamaOptions.Endpoint));
    #pragma warning restore SKEXP0070

    var kernel = kernelBuilder.Build();

    // Register chat completion service
    services.AddSingleton(kernel.GetRequiredService<IChatCompletionService>());

    // Register RAG generation service
    services.AddSingleton<IRagGenerationService, SemanticKernelRagGenerationService>();

    return services;
}
```

### Options Class Extension

Add to `Options/OllamaOptions.cs` (if not already present):

```csharp
public class OllamaOptions
{
    public const string SectionName = "Ollama";

    [Required]
    [Url]
    public string Endpoint { get; set; } = "http://localhost:11434";

    [Required]
    public string EmbeddingModel { get; set; } = "mxbai-embed-large";

    [Required]
    public string RagModel { get; set; } = "mistral";

    [Range(1, 600)]
    public int TimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// Maximum context window tokens for RAG generation.
    /// </summary>
    [Range(1000, 128000)]
    public int MaxContextTokens { get; set; } = 24000;
}
```

### Context Window Recommendations by Model

| Model | Total Context | Recommended MaxContextTokens | Notes |
|-------|---------------|------------------------------|-------|
| `mistral` (default) | 32K | 24,000 | Leave room for response |
| `llama3.2:8b` | 128K | 100,000 | Large context available |
| `qwen2.5:7b` | 128K | 100,000 | Multilingual support |
| `phi3:mini` | 4K-128K | Varies | Check specific variant |

### Error Handling

The service should handle common Ollama errors gracefully:

```csharp
public async Task<RagResponse> GenerateResponseAsync(...)
{
    try
    {
        // ... generation logic
    }
    catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable)
    {
        _logger.LogError(ex, "Ollama service unavailable");
        throw new RagGenerationException(
            "EMBEDDING_SERVICE_ERROR",
            "Ollama service is unavailable. Please ensure Ollama is running.",
            ex);
    }
    catch (TaskCanceledException ex) when (ex.CancellationToken != cancellationToken)
    {
        _logger.LogError(ex, "Ollama request timed out after {Timeout}s", _options.TimeoutSeconds);
        throw new RagGenerationException(
            "EMBEDDING_SERVICE_ERROR",
            $"RAG generation timed out after {_options.TimeoutSeconds} seconds.",
            ex);
    }
    catch (Exception ex) when (ex.Message.Contains("model") && ex.Message.Contains("not found"))
    {
        _logger.LogError(ex, "RAG model {Model} not found", _options.RagModel);
        throw new RagGenerationException(
            "MODEL_NOT_FOUND",
            $"RAG model '{_options.RagModel}' not found. Please run: ollama pull {_options.RagModel}",
            ex);
    }
}
```

---

## Dependencies

### Depends On

- **Phase 028**: Embedding Service - For generating query embeddings during RAG pipeline
- **Phase 029**: Vector Store Service - For retrieving relevant documents

### Blocks

- **Phase 033**: `rag_query` Tool Implementation - Requires RAG generation service
- **Phase 034**: `rag_query_external` Tool Implementation - Requires RAG generation service
- Integration testing phases

---

## Verification Steps

After completing this phase, verify:

1. **Service registration**:
   ```bash
   dotnet build src/CompoundDocs.McpServer/
   # Should compile without errors
   ```

2. **Unit tests pass**:
   ```bash
   dotnet test tests/CompoundDocs.McpServer.Tests/ --filter "RagGeneration"
   ```

3. **Integration test with Ollama** (requires running Ollama):
   ```csharp
   [Fact]
   public async Task GenerateResponseAsync_WithValidDocuments_ReturnsAnswer()
   {
       // Arrange
       var service = _serviceProvider.GetRequiredService<IRagGenerationService>();
       var documents = new List<RetrievedDocument>
       {
           new("test/doc.md", "Test Doc", "This is test content about RAG.", 100, 0.95, "problem", "standard")
       };

       // Act
       var response = await service.GenerateResponseAsync(
           "What is this about?",
           documents);

       // Assert
       Assert.NotNull(response.Answer);
       Assert.NotEmpty(response.Answer);
       Assert.Single(response.Sources);
   }
   ```

4. **Health check**:
   ```csharp
   var isHealthy = await service.CheckHealthAsync();
   Assert.True(isHealthy);
   ```

5. **Context window limiting**:
   ```csharp
   [Fact]
   public async Task GenerateResponseAsync_WithTooManyDocuments_ExcludesLowRelevance()
   {
       // Create documents that exceed context limit
       var documents = Enumerable.Range(1, 100)
           .Select(i => new RetrievedDocument(
               $"doc{i}.md", $"Doc {i}",
               new string('x', 5000), 5000,
               1.0 - (i * 0.01), "problem", "standard"))
           .ToList();

       var options = new RagGenerationOptions { MaxContextTokens = 8000 };

       var response = await service.GenerateResponseAsync("test", documents, options);

       // Should include fewer than all documents
       Assert.True(response.Sources.Count < 100);
   }
   ```

---

## Files Created/Modified

| File | Action | Description |
|------|--------|-------------|
| `src/CompoundDocs.McpServer/Services/IRagGenerationService.cs` | Create | Interface definition |
| `src/CompoundDocs.McpServer/Services/SemanticKernelRagGenerationService.cs` | Create | Implementation |
| `src/CompoundDocs.McpServer/Models/RagModels.cs` | Create | Response and options models |
| `src/CompoundDocs.McpServer/Models/RagGenerationException.cs` | Create | Custom exception type |
| `src/CompoundDocs.McpServer/Extensions/ServiceCollectionExtensions.cs` | Modify | Add service registration |
| `src/CompoundDocs.McpServer/Options/OllamaOptions.cs` | Modify | Add RAG-specific options |
| `tests/CompoundDocs.McpServer.Tests/Services/RagGenerationServiceTests.cs` | Create | Unit tests |

---

## Risk Mitigation

| Risk | Mitigation |
|------|------------|
| Ollama model not installed | Clear error message with `ollama pull` command |
| Context window exceeded | Automatic document exclusion with logging |
| Response quality issues | Configurable system prompt, thorough prompt engineering |
| Timeout on large contexts | Configurable timeout, streaming support |
| Memory pressure | Token estimation prevents excessive context building |
| Model hallucination | System prompt emphasizes citing sources, admitting uncertainty |

---

## Notes

- The service is designed to be stateless and thread-safe for singleton registration
- Streaming support allows for responsive user experiences in future UI integrations
- The context building algorithm prioritizes critical/important documents before considering relevance scores
- Token estimation uses a simple character-based approximation (4 chars per token) which is conservative for most models
- The default system prompt is carefully engineered to encourage source citation and honest uncertainty acknowledgment
- Consider implementing prompt versioning in future phases for A/B testing different prompts
