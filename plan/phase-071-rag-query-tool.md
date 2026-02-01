# Phase 071: RAG Query MCP Tool

> **Status**: PLANNED
> **Category**: MCP Tools
> **Estimated Effort**: L
> **Prerequisites**: Phase 025 (Tool Registration), Phase 032 (RAG Generation Service), Phase 051 (Vector Search Service)

---

## Spec References

- [mcp-server/tools.md - RAG Query Tool](../spec/mcp-server/tools.md#1-rag-query-tool) - Complete tool specification
- [mcp-server/ollama-integration.md - RAG Generation Model](../spec/mcp-server/ollama-integration.md#rag-generation-model) - Ollama integration patterns
- [research/semantic-kernel-ollama-rag-research.md](../research/semantic-kernel-ollama-rag-research.md) - RAG pipeline implementation

---

## Objectives

1. Implement the `rag_query` MCP tool for answering questions using RAG with compounding docs
2. Define all tool parameters with proper `[Description]` attributes for LLM schema generation
3. Integrate with vector search service for document retrieval
4. Integrate with RAG generation service for synthesized responses
5. Implement linked document traversal via Markdig parsing
6. Build comprehensive source attribution in responses
7. Handle critical document inclusion regardless of relevance score

---

## Acceptance Criteria

### Tool Registration

- [ ] Tool registered with `[McpServerTool(Name = "rag_query")]` attribute
- [ ] Tool class decorated with `[McpServerToolType]` attribute
- [ ] Tool description matches spec: "Answer questions using RAG with compounding docs"
- [ ] All parameters have `[Description]` attributes for schema generation

### Parameter Definition

- [ ] `query` (string, required) - Natural language question
- [ ] `doc_types` (string[]?, optional) - Filter to specific doc-types (default: all)
- [ ] `max_sources` (int?, optional) - Maximum documents to use (default: 3)
- [ ] `min_relevance_score` (float?, optional) - Minimum relevance score (default: 0.7)
- [ ] `min_promotion_level` (string?, optional) - Only return docs at or above this level: `standard`, `important`, `critical` (default: `standard`)
- [ ] `include_critical` (bool?, optional) - Prepend critical docs to context regardless of relevance (default: true)

### Document Retrieval

- [ ] Generates embedding for query using `IEmbeddingService`
- [ ] Performs vector similarity search using `IVectorSearchService`
- [ ] Filters by doc_types when specified
- [ ] Filters by min_relevance_score threshold
- [ ] Filters by min_promotion_level
- [ ] Respects max_sources limit after filtering

### Critical Document Handling

- [ ] When `include_critical=true`, retrieves all critical documents for active project
- [ ] Critical documents prepended to context regardless of relevance score
- [ ] Critical documents do not count against max_sources limit
- [ ] Critical documents are deduplicated if also matched by relevance

### Linked Document Traversal

- [ ] Parses retrieved documents with Markdig to extract links
- [ ] Follows markdown links to other compounding docs
- [ ] Linked documents included in `linked_docs` response array
- [ ] Linked documents attributed back to source document
- [ ] Maximum link depth configurable (default: 1)

### RAG Synthesis

- [ ] Sends retrieved documents + query to `IRagGenerationService`
- [ ] Respects context window limits via token estimation
- [ ] Returns synthesized answer with source citations

### Response Schema

- [ ] Response includes `answer` (string) - Synthesized response
- [ ] Response includes `sources` (array) - Direct matches with metadata
- [ ] Response includes `linked_docs` (array) - Documents from link traversal
- [ ] Source metadata includes: `path`, `title`, `char_count`, `relevance_score`
- [ ] Linked doc metadata includes: `path`, `title`, `char_count`, `linked_from`

### Error Handling

- [ ] Returns `PROJECT_NOT_ACTIVATED` if no project context
- [ ] Returns `INVALID_DOC_TYPE` if specified doc-type doesn't exist
- [ ] Returns `EMBEDDING_SERVICE_ERROR` if Ollama unavailable
- [ ] Returns `DATABASE_ERROR` if PostgreSQL query fails
- [ ] All errors follow standard error response format

---

## Implementation Notes

### 1. RagQueryTool Class

Create `src/CompoundDocs.McpServer/Tools/RagTools.cs`:

```csharp
using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using CompoundDocs.McpServer.Services;
using CompoundDocs.McpServer.Models;
using CompoundDocs.McpServer.Exceptions;

namespace CompoundDocs.McpServer.Tools;

/// <summary>
/// MCP tools for RAG (Retrieval-Augmented Generation) operations.
/// </summary>
[McpServerToolType]
public class RagTools
{
    private readonly IProjectContext _projectContext;
    private readonly IEmbeddingService _embeddingService;
    private readonly IVectorSearchService _vectorSearchService;
    private readonly IRagGenerationService _ragService;
    private readonly IDocumentLinkParser _linkParser;
    private readonly ICompoundDocumentRepository _documentRepository;
    private readonly ILogger<RagTools> _logger;

    public RagTools(
        IProjectContext projectContext,
        IEmbeddingService embeddingService,
        IVectorSearchService vectorSearchService,
        IRagGenerationService ragService,
        IDocumentLinkParser linkParser,
        ICompoundDocumentRepository documentRepository,
        ILogger<RagTools> logger)
    {
        _projectContext = projectContext;
        _embeddingService = embeddingService;
        _vectorSearchService = vectorSearchService;
        _ragService = ragService;
        _linkParser = linkParser;
        _documentRepository = documentRepository;
        _logger = logger;
    }

    [McpServerTool(Name = "rag_query")]
    [Description("Answer questions using RAG with compounding docs. Returns synthesized response with source metadata.")]
    public async Task<string> RagQueryAsync(
        [Description("Natural language question")]
        string query,

        [Description("Filter to specific doc-types (default: all)")]
        string[]? docTypes = null,

        [Description("Maximum documents to use (default: 3)")]
        int maxSources = 3,

        [Description("Minimum relevance score threshold (default: 0.7)")]
        float minRelevanceScore = 0.7f,

        [Description("Only return docs at or above this level: standard, important, critical (default: standard)")]
        string minPromotionLevel = "standard",

        [Description("Prepend critical docs to context regardless of relevance (default: true)")]
        bool includeCritical = true,

        CancellationToken cancellationToken = default)
    {
        // Validate project is activated
        if (!_projectContext.IsActivated)
        {
            return CreateErrorResponse(
                ToolErrorCodes.ProjectNotActivated,
                "No project is currently activated. Call activate_project first.",
                new { requiredTool = "activate_project" });
        }

        // Validate parameters
        if (string.IsNullOrWhiteSpace(query))
        {
            return CreateErrorResponse(
                ToolErrorCodes.InvalidParams,
                "Query parameter is required and cannot be empty.");
        }

        if (!IsValidPromotionLevel(minPromotionLevel))
        {
            return CreateErrorResponse(
                ToolErrorCodes.InvalidParams,
                $"Invalid promotion level: {minPromotionLevel}. Must be one of: standard, important, critical");
        }

        // Validate doc types if specified
        if (docTypes != null && docTypes.Length > 0)
        {
            var invalidDocTypes = await ValidateDocTypesAsync(docTypes, cancellationToken);
            if (invalidDocTypes.Any())
            {
                return CreateErrorResponse(
                    ToolErrorCodes.InvalidDocType,
                    $"Invalid doc-types: {string.Join(", ", invalidDocTypes)}",
                    new { invalidDocTypes, validDocTypes = await GetValidDocTypesAsync(cancellationToken) });
            }
        }

        _logger.LogInformation(
            "RAG query: {Query} (maxSources={MaxSources}, minRelevance={MinRelevance}, promotionLevel={PromotionLevel})",
            query, maxSources, minRelevanceScore, minPromotionLevel);

        try
        {
            // Execute RAG pipeline
            var result = await ExecuteRagPipelineAsync(
                query,
                docTypes,
                maxSources,
                minRelevanceScore,
                minPromotionLevel,
                includeCritical,
                cancellationToken);

            return JsonSerializer.Serialize(result, JsonOptions.Default);
        }
        catch (EmbeddingServiceException ex)
        {
            _logger.LogError(ex, "Embedding service error during RAG query");
            return CreateErrorResponse(
                ToolErrorCodes.EmbeddingServiceError,
                "Failed to generate embeddings. Ensure Ollama is running.",
                new { innerMessage = ex.Message });
        }
        catch (DatabaseException ex)
        {
            _logger.LogError(ex, "Database error during RAG query");
            return CreateErrorResponse(
                ToolErrorCodes.DatabaseError,
                "Database operation failed during document retrieval.",
                new { innerMessage = ex.Message });
        }
    }

    private async Task<RagQueryResponse> ExecuteRagPipelineAsync(
        string query,
        string[]? docTypes,
        int maxSources,
        float minRelevanceScore,
        string minPromotionLevel,
        bool includeCritical,
        CancellationToken cancellationToken)
    {
        var tenantContext = _projectContext.GetTenantContext();

        // Step 1: Generate embedding for query
        _logger.LogDebug("Generating embedding for query");
        var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(query, cancellationToken);

        // Step 2: Retrieve critical documents if requested
        var criticalDocs = new List<RetrievedDocument>();
        if (includeCritical)
        {
            criticalDocs = await RetrieveCriticalDocumentsAsync(
                tenantContext,
                docTypes,
                cancellationToken);

            _logger.LogDebug("Retrieved {Count} critical documents", criticalDocs.Count);
        }

        // Step 3: Perform vector similarity search
        var searchOptions = new VectorSearchOptions
        {
            TenantContext = tenantContext,
            Embedding = queryEmbedding,
            TopK = maxSources + criticalDocs.Count, // Get extra to account for deduplication
            MinRelevanceScore = minRelevanceScore,
            DocTypes = docTypes,
            MinPromotionLevel = minPromotionLevel
        };

        var searchResults = await _vectorSearchService.SearchAsync(searchOptions, cancellationToken);

        _logger.LogDebug("Vector search returned {Count} results", searchResults.Count);

        // Step 4: Merge and deduplicate results (critical docs take priority)
        var criticalPaths = criticalDocs.Select(d => d.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var relevanceResults = searchResults
            .Where(r => !criticalPaths.Contains(r.Path))
            .Take(maxSources)
            .ToList();

        var allDocuments = criticalDocs.Concat(relevanceResults).ToList();

        if (!allDocuments.Any())
        {
            _logger.LogInformation("No documents found for RAG query");
            return new RagQueryResponse(
                Answer: "No relevant documents were found to answer your question.",
                Sources: [],
                LinkedDocs: []);
        }

        // Step 5: Parse linked documents
        var linkedDocs = await TraverseLinkedDocumentsAsync(
            allDocuments,
            tenantContext,
            cancellationToken);

        _logger.LogDebug("Found {Count} linked documents", linkedDocs.Count);

        // Step 6: Prepare documents for RAG generation (include linked docs in context)
        var contextDocuments = allDocuments
            .Concat(linkedDocs.Select(ld => new RetrievedDocument(
                ld.Path,
                ld.Title,
                ld.Content,
                ld.CharCount,
                0.0, // Linked docs don't have relevance scores
                "", // Doc type not tracked for linked
                "standard",
                null)))
            .ToList();

        // Step 7: Generate RAG response
        var ragResponse = await _ragService.GenerateResponseAsync(
            query,
            contextDocuments,
            new RagGenerationOptions { IncludeLinkedDocs = true },
            cancellationToken);

        // Step 8: Build response
        return new RagQueryResponse(
            Answer: ragResponse.Answer,
            Sources: allDocuments.Select(d => new SourceDocument(
                d.Path,
                d.Title,
                d.CharCount,
                (float)d.RelevanceScore)).ToList(),
            LinkedDocs: linkedDocs.Select(ld => new LinkedDocumentInfo(
                ld.Path,
                ld.Title,
                ld.CharCount,
                ld.LinkedFrom)).ToList());
    }

    private async Task<List<RetrievedDocument>> RetrieveCriticalDocumentsAsync(
        TenantContext tenantContext,
        string[]? docTypes,
        CancellationToken cancellationToken)
    {
        var criticalDocs = await _documentRepository.GetByPromotionLevelAsync(
            tenantContext,
            "critical",
            docTypes,
            cancellationToken);

        return criticalDocs.Select(doc => new RetrievedDocument(
            doc.Path,
            doc.Title,
            doc.Content,
            doc.Content.Length,
            1.0, // Critical docs treated as maximum relevance
            doc.DocType,
            "critical",
            doc.Date)).ToList();
    }

    private async Task<List<LinkedDocument>> TraverseLinkedDocumentsAsync(
        IReadOnlyList<RetrievedDocument> documents,
        TenantContext tenantContext,
        CancellationToken cancellationToken)
    {
        var linkedDocs = new List<LinkedDocument>();
        var processedPaths = documents.Select(d => d.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var doc in documents)
        {
            // Parse markdown to extract links
            var links = _linkParser.ExtractDocumentLinks(doc.Content);

            foreach (var link in links)
            {
                // Skip if already processed or external link
                if (processedPaths.Contains(link.TargetPath) || !IsCompoundingDocPath(link.TargetPath))
                {
                    continue;
                }

                // Try to retrieve the linked document
                var linkedDoc = await _documentRepository.GetByPathAsync(
                    tenantContext,
                    link.TargetPath,
                    cancellationToken);

                if (linkedDoc != null)
                {
                    linkedDocs.Add(new LinkedDocument(
                        linkedDoc.Path,
                        linkedDoc.Title,
                        linkedDoc.Content,
                        linkedDoc.Content.Length,
                        doc.Path));

                    processedPaths.Add(linkedDoc.Path);
                }
            }
        }

        return linkedDocs;
    }

    private static bool IsCompoundingDocPath(string path)
    {
        return path.StartsWith("./csharp-compounding-docs/", StringComparison.OrdinalIgnoreCase) ||
               path.Contains("/csharp-compounding-docs/", StringComparison.OrdinalIgnoreCase) ||
               (!path.StartsWith("http://") && !path.StartsWith("https://") && path.EndsWith(".md"));
    }

    private static bool IsValidPromotionLevel(string level)
    {
        return level.Equals("standard", StringComparison.OrdinalIgnoreCase) ||
               level.Equals("important", StringComparison.OrdinalIgnoreCase) ||
               level.Equals("critical", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<List<string>> ValidateDocTypesAsync(
        string[] docTypes,
        CancellationToken cancellationToken)
    {
        var validTypes = await GetValidDocTypesAsync(cancellationToken);
        return docTypes
            .Where(dt => !validTypes.Contains(dt, StringComparer.OrdinalIgnoreCase))
            .ToList();
    }

    private async Task<IReadOnlyList<string>> GetValidDocTypesAsync(CancellationToken cancellationToken)
    {
        var docTypes = await _documentRepository.GetDocTypesAsync(
            _projectContext.GetTenantContext(),
            cancellationToken);
        return docTypes;
    }

    private static string CreateErrorResponse(string code, string message, object? details = null)
    {
        var error = new ToolErrorResponse(
            Error: true,
            Code: code,
            Message: message,
            Details: details);
        return JsonSerializer.Serialize(error, JsonOptions.Default);
    }
}
```

### 2. Response DTOs

Create `src/CompoundDocs.McpServer/Models/RagQueryResponse.cs`:

```csharp
namespace CompoundDocs.McpServer.Models;

/// <summary>
/// Response from the rag_query tool.
/// </summary>
public record RagQueryResponse(
    /// <summary>
    /// The synthesized answer from RAG generation.
    /// </summary>
    string Answer,

    /// <summary>
    /// Source documents that directly matched the query.
    /// </summary>
    IReadOnlyList<SourceDocument> Sources,

    /// <summary>
    /// Documents linked from source documents.
    /// </summary>
    IReadOnlyList<LinkedDocumentInfo> LinkedDocs);

/// <summary>
/// A source document with relevance metadata.
/// </summary>
public record SourceDocument(
    /// <summary>
    /// Relative path to the document within csharp-compounding-docs.
    /// </summary>
    string Path,

    /// <summary>
    /// Document title from frontmatter.
    /// </summary>
    string Title,

    /// <summary>
    /// Character count of the document content.
    /// </summary>
    int CharCount,

    /// <summary>
    /// Relevance score from vector search (0.0 to 1.0).
    /// </summary>
    float RelevanceScore);

/// <summary>
/// A linked document with attribution.
/// </summary>
public record LinkedDocumentInfo(
    /// <summary>
    /// Relative path to the linked document.
    /// </summary>
    string Path,

    /// <summary>
    /// Document title from frontmatter.
    /// </summary>
    string Title,

    /// <summary>
    /// Character count of the document content.
    /// </summary>
    int CharCount,

    /// <summary>
    /// Path of the document that linked to this one.
    /// </summary>
    string LinkedFrom);
```

### 3. Document Link Parser Interface

Create `src/CompoundDocs.McpServer/Services/IDocumentLinkParser.cs`:

```csharp
namespace CompoundDocs.McpServer.Services;

/// <summary>
/// Parses markdown documents to extract links to other compounding docs.
/// </summary>
public interface IDocumentLinkParser
{
    /// <summary>
    /// Extracts all document links from markdown content.
    /// </summary>
    /// <param name="markdownContent">The markdown content to parse.</param>
    /// <returns>List of extracted document links.</returns>
    IReadOnlyList<DocumentLink> ExtractDocumentLinks(string markdownContent);
}

/// <summary>
/// A link to another document.
/// </summary>
public record DocumentLink(
    /// <summary>
    /// The target path of the link.
    /// </summary>
    string TargetPath,

    /// <summary>
    /// The link text (if any).
    /// </summary>
    string? LinkText);
```

### 4. Markdig Link Parser Implementation

Create `src/CompoundDocs.McpServer/Services/MarkdigDocumentLinkParser.cs`:

```csharp
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace CompoundDocs.McpServer.Services;

/// <summary>
/// Parses markdown documents using Markdig to extract links.
/// </summary>
public class MarkdigDocumentLinkParser : IDocumentLinkParser
{
    private readonly MarkdownPipeline _pipeline;

    public MarkdigDocumentLinkParser()
    {
        _pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .Build();
    }

    public IReadOnlyList<DocumentLink> ExtractDocumentLinks(string markdownContent)
    {
        if (string.IsNullOrWhiteSpace(markdownContent))
        {
            return [];
        }

        var document = Markdown.Parse(markdownContent, _pipeline);
        var links = new List<DocumentLink>();

        foreach (var descendant in document.Descendants())
        {
            if (descendant is LinkInline linkInline && !string.IsNullOrEmpty(linkInline.Url))
            {
                // Skip external URLs
                if (linkInline.Url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    linkInline.Url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // Only include markdown files
                if (linkInline.Url.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
                {
                    var linkText = ExtractLinkText(linkInline);
                    links.Add(new DocumentLink(linkInline.Url, linkText));
                }
            }
        }

        return links;
    }

    private static string? ExtractLinkText(LinkInline linkInline)
    {
        var firstChild = linkInline.FirstChild;
        if (firstChild is LiteralInline literal)
        {
            return literal.Content.ToString();
        }
        return null;
    }
}
```

### 5. Vector Search Options

Ensure `src/CompoundDocs.McpServer/Models/VectorSearchOptions.cs` includes:

```csharp
namespace CompoundDocs.McpServer.Models;

/// <summary>
/// Options for vector similarity search.
/// </summary>
public class VectorSearchOptions
{
    /// <summary>
    /// Tenant context for scoping the search.
    /// </summary>
    public required TenantContext TenantContext { get; init; }

    /// <summary>
    /// The query embedding vector.
    /// </summary>
    public required ReadOnlyMemory<float> Embedding { get; init; }

    /// <summary>
    /// Maximum number of results to return.
    /// </summary>
    public int TopK { get; init; } = 10;

    /// <summary>
    /// Minimum relevance score threshold (0.0 to 1.0).
    /// </summary>
    public float MinRelevanceScore { get; init; } = 0.5f;

    /// <summary>
    /// Filter to specific document types.
    /// </summary>
    public string[]? DocTypes { get; init; }

    /// <summary>
    /// Minimum promotion level filter.
    /// </summary>
    public string MinPromotionLevel { get; init; } = "standard";
}
```

### 6. Service Registration

Add to `src/CompoundDocs.McpServer/Extensions/ServiceCollectionExtensions.cs`:

```csharp
/// <summary>
/// Registers RAG query tool dependencies.
/// </summary>
public static IServiceCollection AddRagQueryToolServices(this IServiceCollection services)
{
    services.AddSingleton<IDocumentLinkParser, MarkdigDocumentLinkParser>();
    return services;
}
```

### 7. Program.cs Integration

Ensure tools are registered:

```csharp
builder.Services.AddRagQueryToolServices();

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly(); // Auto-discovers RagTools
```

---

## Dependencies

### Depends On

- **Phase 025**: Tool Registration Infrastructure - `[McpServerTool]` attribute pattern and tool discovery
- **Phase 032**: RAG Generation Service - `IRagGenerationService` for synthesized responses
- **Phase 051**: Vector Search Service - `IVectorSearchService` for document retrieval

### Implicitly Depends On

- **Phase 029**: Embedding Service - `IEmbeddingService` for query embedding
- **Phase 038**: Tenant Context - `IProjectContext` for active project
- **Phase 015**: Markdown Parser - Markdig integration
- **Phase 042**: Compound Document Model - Document repository

### Blocks

- **Phase 080+**: Integration testing phases
- **Phase 090+**: End-to-end testing phases

---

## Testing Verification

### Unit Tests

Create `tests/CompoundDocs.McpServer.Tests/Tools/RagToolsTests.cs`:

```csharp
public class RagToolsTests
{
    private readonly Mock<IProjectContext> _projectContextMock;
    private readonly Mock<IEmbeddingService> _embeddingServiceMock;
    private readonly Mock<IVectorSearchService> _vectorSearchMock;
    private readonly Mock<IRagGenerationService> _ragServiceMock;
    private readonly Mock<IDocumentLinkParser> _linkParserMock;
    private readonly Mock<ICompoundDocumentRepository> _repositoryMock;
    private readonly RagTools _tools;

    public RagToolsTests()
    {
        _projectContextMock = new Mock<IProjectContext>();
        _embeddingServiceMock = new Mock<IEmbeddingService>();
        _vectorSearchMock = new Mock<IVectorSearchService>();
        _ragServiceMock = new Mock<IRagGenerationService>();
        _linkParserMock = new Mock<IDocumentLinkParser>();
        _repositoryMock = new Mock<ICompoundDocumentRepository>();

        _tools = new RagTools(
            _projectContextMock.Object,
            _embeddingServiceMock.Object,
            _vectorSearchMock.Object,
            _ragServiceMock.Object,
            _linkParserMock.Object,
            _repositoryMock.Object,
            Mock.Of<ILogger<RagTools>>());
    }

    [Fact]
    public async Task RagQueryAsync_WhenProjectNotActivated_ReturnsError()
    {
        // Arrange
        _projectContextMock.Setup(x => x.IsActivated).Returns(false);

        // Act
        var result = await _tools.RagQueryAsync("test query");

        // Assert
        var response = JsonSerializer.Deserialize<ToolErrorResponse>(result);
        Assert.True(response?.Error);
        Assert.Equal("PROJECT_NOT_ACTIVATED", response?.Code);
    }

    [Fact]
    public async Task RagQueryAsync_WithValidQuery_ReturnsAnswer()
    {
        // Arrange
        SetupActivatedProject();
        SetupEmbeddingService();
        SetupVectorSearch(new[]
        {
            CreateRetrievedDocument("doc1.md", "Test Doc", 0.9f)
        });
        SetupRagGeneration("This is the answer.");
        _linkParserMock.Setup(x => x.ExtractDocumentLinks(It.IsAny<string>()))
            .Returns(new List<DocumentLink>());

        // Act
        var result = await _tools.RagQueryAsync("What is this about?");

        // Assert
        var response = JsonSerializer.Deserialize<RagQueryResponse>(result);
        Assert.NotNull(response);
        Assert.Equal("This is the answer.", response.Answer);
        Assert.Single(response.Sources);
    }

    [Fact]
    public async Task RagQueryAsync_WithInvalidDocType_ReturnsError()
    {
        // Arrange
        SetupActivatedProject();
        _repositoryMock.Setup(x => x.GetDocTypesAsync(It.IsAny<TenantContext>(), default))
            .ReturnsAsync(new[] { "problem", "insight" });

        // Act
        var result = await _tools.RagQueryAsync(
            "test query",
            docTypes: new[] { "invalid_type" });

        // Assert
        var response = JsonSerializer.Deserialize<ToolErrorResponse>(result);
        Assert.True(response?.Error);
        Assert.Equal("INVALID_DOC_TYPE", response?.Code);
    }

    [Fact]
    public async Task RagQueryAsync_WithIncludeCritical_PrependsCriticalDocs()
    {
        // Arrange
        SetupActivatedProject();
        SetupEmbeddingService();

        var criticalDoc = CreateCompoundDocument("critical.md", "Critical Doc", "critical");
        _repositoryMock.Setup(x => x.GetByPromotionLevelAsync(
                It.IsAny<TenantContext>(), "critical", It.IsAny<string[]?>(), default))
            .ReturnsAsync(new[] { criticalDoc });

        SetupVectorSearch(new[]
        {
            CreateRetrievedDocument("regular.md", "Regular Doc", 0.85f)
        });
        SetupRagGeneration("Answer with critical context.");
        _linkParserMock.Setup(x => x.ExtractDocumentLinks(It.IsAny<string>()))
            .Returns(new List<DocumentLink>());

        // Act
        var result = await _tools.RagQueryAsync(
            "test query",
            includeCritical: true);

        // Assert
        var response = JsonSerializer.Deserialize<RagQueryResponse>(result);
        Assert.NotNull(response);
        Assert.Equal(2, response.Sources.Count);
        // Critical doc should be first
        Assert.Equal("critical.md", response.Sources[0].Path);
    }

    [Fact]
    public async Task RagQueryAsync_WithLinkedDocs_IncludesLinkedDocuments()
    {
        // Arrange
        SetupActivatedProject();
        SetupEmbeddingService();
        SetupVectorSearch(new[]
        {
            CreateRetrievedDocument("main.md", "Main Doc", 0.9f)
        });

        _linkParserMock.Setup(x => x.ExtractDocumentLinks(It.IsAny<string>()))
            .Returns(new List<DocumentLink>
            {
                new("./linked.md", "Linked Doc")
            });

        var linkedDoc = CreateCompoundDocument("./linked.md", "Linked Document", "standard");
        _repositoryMock.Setup(x => x.GetByPathAsync(
                It.IsAny<TenantContext>(), "./linked.md", default))
            .ReturnsAsync(linkedDoc);

        SetupRagGeneration("Answer with linked context.");

        // Act
        var result = await _tools.RagQueryAsync("test query");

        // Assert
        var response = JsonSerializer.Deserialize<RagQueryResponse>(result);
        Assert.NotNull(response);
        Assert.Single(response.LinkedDocs);
        Assert.Equal("main.md", response.LinkedDocs[0].LinkedFrom);
    }

    [Fact]
    public async Task RagQueryAsync_WithNoResults_ReturnsNoDocumentsMessage()
    {
        // Arrange
        SetupActivatedProject();
        SetupEmbeddingService();
        SetupVectorSearch(Array.Empty<RetrievedDocument>());
        _repositoryMock.Setup(x => x.GetByPromotionLevelAsync(
                It.IsAny<TenantContext>(), "critical", It.IsAny<string[]?>(), default))
            .ReturnsAsync(Array.Empty<CompoundDocument>());

        // Act
        var result = await _tools.RagQueryAsync(
            "query with no results",
            includeCritical: false);

        // Assert
        var response = JsonSerializer.Deserialize<RagQueryResponse>(result);
        Assert.NotNull(response);
        Assert.Contains("No relevant documents", response.Answer);
        Assert.Empty(response.Sources);
    }

    [Fact]
    public async Task RagQueryAsync_WithInvalidPromotionLevel_ReturnsError()
    {
        // Arrange
        SetupActivatedProject();

        // Act
        var result = await _tools.RagQueryAsync(
            "test query",
            minPromotionLevel: "invalid");

        // Assert
        var response = JsonSerializer.Deserialize<ToolErrorResponse>(result);
        Assert.True(response?.Error);
        Assert.Equal("INVALID_PARAMS", response?.Code);
    }

    // Helper methods
    private void SetupActivatedProject()
    {
        _projectContextMock.Setup(x => x.IsActivated).Returns(true);
        _projectContextMock.Setup(x => x.GetTenantContext())
            .Returns(new TenantContext("test-project", "main", "abc123"));
    }

    private void SetupEmbeddingService()
    {
        _embeddingServiceMock.Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), default))
            .ReturnsAsync(new ReadOnlyMemory<float>(new float[1024]));
    }

    private void SetupVectorSearch(IEnumerable<RetrievedDocument> results)
    {
        _vectorSearchMock.Setup(x => x.SearchAsync(It.IsAny<VectorSearchOptions>(), default))
            .ReturnsAsync(results.ToList());
    }

    private void SetupRagGeneration(string answer)
    {
        _ragServiceMock.Setup(x => x.GenerateResponseAsync(
                It.IsAny<string>(),
                It.IsAny<IReadOnlyList<RetrievedDocument>>(),
                It.IsAny<RagGenerationOptions?>(),
                default))
            .ReturnsAsync(new RagResponse(answer, [], [], TimeSpan.FromMilliseconds(100)));
    }

    private static RetrievedDocument CreateRetrievedDocument(
        string path, string title, float relevance) =>
        new(path, title, "Content", 100, relevance, "problem", "standard", DateTime.UtcNow);

    private static CompoundDocument CreateCompoundDocument(
        string path, string title, string promotionLevel) =>
        new()
        {
            Path = path,
            Title = title,
            Content = "Document content",
            DocType = "problem",
            PromotionLevel = promotionLevel,
            Date = DateTime.UtcNow
        };
}
```

### Integration Tests

```csharp
[Trait("Category", "Integration")]
public class RagQueryIntegrationTests : IClassFixture<McpServerFixture>
{
    private readonly McpServerFixture _fixture;

    public RagQueryIntegrationTests(McpServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task RagQuery_FullPipeline_ReturnsValidResponse()
    {
        // Arrange - Activate project first
        await _fixture.ActivateProjectAsync("./test-project");

        // Act
        var result = await _fixture.InvokeToolAsync("rag_query", new
        {
            query = "How do I configure database connections?",
            max_sources = 3,
            min_relevance_score = 0.5
        });

        // Assert
        Assert.NotNull(result.Answer);
        Assert.NotEmpty(result.Sources);
    }
}
```

### Manual Verification

```bash
# Start MCP server
dotnet run --project src/CompoundDocs.McpServer

# Test via MCP client
mcp-cli call rag_query \
  --query "How do I solve N+1 query problems?" \
  --max_sources 5 \
  --include_critical true
```

---

## Files Created/Modified

| File | Action | Description |
|------|--------|-------------|
| `src/CompoundDocs.McpServer/Tools/RagTools.cs` | Create | RAG query tool implementation |
| `src/CompoundDocs.McpServer/Models/RagQueryResponse.cs` | Create | Response DTOs |
| `src/CompoundDocs.McpServer/Services/IDocumentLinkParser.cs` | Create | Link parser interface |
| `src/CompoundDocs.McpServer/Services/MarkdigDocumentLinkParser.cs` | Create | Markdig implementation |
| `src/CompoundDocs.McpServer/Extensions/ServiceCollectionExtensions.cs` | Modify | Add service registration |
| `tests/CompoundDocs.McpServer.Tests/Tools/RagToolsTests.cs` | Create | Unit tests |
| `tests/CompoundDocs.IntegrationTests/Tools/RagQueryIntegrationTests.cs` | Create | Integration tests |

---

## Risk Mitigation

| Risk | Mitigation |
|------|------------|
| Large result sets | max_sources parameter limits documents; critical docs tracked separately |
| Context window overflow | RAG service handles token estimation and truncation |
| Slow linked doc traversal | Single level depth by default; paths cached |
| Circular links | HashSet of processed paths prevents infinite loops |
| Ollama unavailable | Circuit breaker pattern in RAG service; clear error message |
| Empty search results | Graceful message when no documents match |
| Invalid doc types | Upfront validation with list of valid types |

---

## Notes

- The tool follows the exact parameter names from the spec (`doc_types`, `max_sources`, `min_relevance_score`, etc.)
- Critical document handling ensures important context is never missed even with low relevance scores
- Linked document traversal is limited to depth 1 to prevent excessive document loading
- All responses include full source attribution for transparency
- Error handling follows the standard error response format from Phase 027
- The tool is stateless beyond the project context, making it thread-safe
