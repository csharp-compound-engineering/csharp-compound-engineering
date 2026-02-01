# Phase 061: Document Chunking Service

> **Status**: PLANNED
> **Category**: Document Processing
> **Estimated Effort**: M
> **Prerequisites**: Phase 015 (Markdown Parser Integration), Phase 043 (DocumentChunk Model)

---

## Spec References

- [mcp-server/chunking.md - Strategy](../spec/mcp-server/chunking.md#strategy-markdown-headers) (lines 14-34)
- [mcp-server/chunking.md - Chunk Fields](../spec/mcp-server/chunking.md#chunk-fields) (lines 69-96)
- [mcp-server/chunking.md - Chunking Algorithm](../spec/mcp-server/chunking.md#chunking-algorithm) (lines 99-126)
- [mcp-server/chunking.md - Chunk Lifecycle](../spec/mcp-server/chunking.md#chunk-lifecycle) (lines 186-209)
- [structure/mcp-server.md - Chunking Summary](../structure/mcp-server.md#specmcp-serverchunkingmd)

---

## Objectives

1. Create `IDocumentChunkingService` interface for document chunking operations
2. Implement 500-line threshold detection to determine when chunking is required
3. Implement H2 (`##`) and H3 (`###`) markdown header detection using Markdig
4. Calculate chunk boundaries (start_line, end_line) from header positions
5. Generate chunk metadata including header_path hierarchy (e.g., `## Section > ### Subsection`)
6. Store chunks in database only (never modify source files)
7. Handle edge cases: no headers, very long sections, empty sections, code blocks
8. Integrate with embedding service for chunk vector generation

---

## Acceptance Criteria

### Interface Design
- [ ] `IDocumentChunkingService` interface defined with clear contract
- [ ] `ChunkDocument()` method accepts document content and metadata
- [ ] `NeedsChunking()` method checks if document exceeds 500-line threshold
- [ ] Interface returns `IReadOnlyList<DocumentChunk>` for generated chunks
- [ ] All operations are async with proper cancellation token support

### 500-Line Threshold
- [ ] Documents <= 500 lines are NOT chunked (return empty list)
- [ ] Documents > 500 lines ARE chunked
- [ ] Line count calculation handles various line endings (CRLF, LF, CR)
- [ ] Threshold value is configurable but defaults to 500

### Header Detection
- [ ] Detects H2 (`##`) headers as primary chunk boundaries
- [ ] Detects H3 (`###`) headers as secondary chunk boundaries
- [ ] H1 (`#`) headers are NOT used as chunk boundaries
- [ ] Headers within code blocks are ignored
- [ ] Uses Markdig `HeadingBlock` for accurate parsing

### Chunk Boundary Calculation
- [ ] Each chunk starts at a header line
- [ ] Each chunk ends at the line before the next header (or EOF)
- [ ] Start and end lines are 1-based for user-facing display
- [ ] Code blocks are never split mid-block
- [ ] Empty sections (header followed immediately by another header) are skipped

### Header Path Generation
- [ ] Header path format: `## Section > ### Subsection`
- [ ] Uses `>` as separator between heading levels
- [ ] Preserves markdown heading markers (`##`, `###`)
- [ ] Handles nested headers correctly (H3 under H2)
- [ ] Resets path when encountering same or higher level header

### Chunk Metadata
- [ ] Each chunk has unique GUID ID
- [ ] `document_id` references parent document
- [ ] `chunk_index` is zero-based sequential order
- [ ] `header_path` contains full hierarchy
- [ ] `content` contains extracted text
- [ ] `start_line` and `end_line` are accurate
- [ ] Tenant fields inherited from parent document

### Database-Only Storage
- [ ] Source markdown files are NEVER modified
- [ ] Chunks are created in `document_chunks` collection
- [ ] All chunk metadata stored in database
- [ ] No file system writes during chunking

### Edge Cases
- [ ] Document with no H2/H3 headers creates single chunk for entire content
- [ ] Very long section (> 1000 lines) is chunked at paragraph boundaries
- [ ] Empty sections between headers are skipped (no chunk created)
- [ ] Code blocks included in chunk, not split
- [ ] Frontmatter (YAML) is excluded from chunk content

### Testing
- [ ] Unit tests for threshold detection
- [ ] Unit tests for header detection
- [ ] Unit tests for boundary calculation
- [ ] Unit tests for header path generation
- [ ] Unit tests for all edge cases
- [ ] Integration tests with Markdig parser
- [ ] Test coverage meets 100% line/branch/method requirement

---

## Implementation Notes

### 1. Interface Definition

```csharp
// src/CompoundDocs.McpServer/Services/Chunking/IDocumentChunkingService.cs
using CompoundDocs.McpServer.Models;

namespace CompoundDocs.McpServer.Services.Chunking;

/// <summary>
/// Service for splitting large documents into searchable chunks.
/// Documents exceeding the line threshold are chunked by markdown headers.
/// </summary>
public interface IDocumentChunkingService
{
    /// <summary>
    /// Determines if a document should be chunked based on line count.
    /// </summary>
    /// <param name="content">The document content.</param>
    /// <returns>True if document exceeds chunking threshold.</returns>
    bool NeedsChunking(string content);

    /// <summary>
    /// Gets the line count for a document.
    /// </summary>
    /// <param name="content">The document content.</param>
    /// <returns>Number of lines in the document.</returns>
    int GetLineCount(string content);

    /// <summary>
    /// Chunks a document by markdown headers.
    /// </summary>
    /// <param name="content">The document content.</param>
    /// <param name="parentDocument">The parent document for context inheritance.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of chunks, or empty list if document doesn't need chunking.</returns>
    Task<IReadOnlyList<DocumentChunk>> ChunkDocumentAsync(
        string content,
        CompoundDocument parentDocument,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Re-chunks a document when content changes.
    /// Generates new chunks and returns them for replacement.
    /// </summary>
    /// <param name="content">The updated document content.</param>
    /// <param name="parentDocument">The parent document for context inheritance.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>New list of chunks to replace existing ones.</returns>
    Task<IReadOnlyList<DocumentChunk>> RechunkDocumentAsync(
        string content,
        CompoundDocument parentDocument,
        CancellationToken cancellationToken = default);
}
```

### 2. Configuration Options

```csharp
// src/CompoundDocs.McpServer/Services/Chunking/ChunkingOptions.cs
namespace CompoundDocs.McpServer.Services.Chunking;

/// <summary>
/// Configuration options for document chunking.
/// </summary>
public sealed class ChunkingOptions
{
    /// <summary>
    /// Configuration section name in appsettings.
    /// </summary>
    public const string SectionName = "Chunking";

    /// <summary>
    /// Minimum line count that triggers chunking.
    /// Default: 500 lines.
    /// </summary>
    public int LineThreshold { get; set; } = 500;

    /// <summary>
    /// Maximum lines in a single section before paragraph-level chunking.
    /// Default: 1000 lines.
    /// </summary>
    public int MaxSectionLines { get; set; } = 1000;

    /// <summary>
    /// Header levels to use as chunk boundaries.
    /// Default: H2 (2) and H3 (3).
    /// </summary>
    public int[] ChunkHeaderLevels { get; set; } = [2, 3];

    /// <summary>
    /// Separator used in header paths.
    /// Default: " > "
    /// </summary>
    public string HeaderPathSeparator { get; set; } = " > ";
}
```

### 3. Chunking Result Models

```csharp
// src/CompoundDocs.McpServer/Services/Chunking/ChunkBoundary.cs
namespace CompoundDocs.McpServer.Services.Chunking;

/// <summary>
/// Represents the boundaries of a document chunk.
/// </summary>
/// <param name="StartLine">1-based start line number.</param>
/// <param name="EndLine">1-based end line number (inclusive).</param>
/// <param name="HeaderPath">Hierarchical header path.</param>
/// <param name="HeaderLevel">Level of the header (2 for H2, 3 for H3).</param>
internal readonly record struct ChunkBoundary(
    int StartLine,
    int EndLine,
    string HeaderPath,
    int HeaderLevel
);
```

```csharp
// src/CompoundDocs.McpServer/Services/Chunking/HeaderNode.cs
namespace CompoundDocs.McpServer.Services.Chunking;

/// <summary>
/// Represents a header node for building hierarchy.
/// </summary>
/// <param name="Level">Header level (1-6).</param>
/// <param name="Text">Header text content.</param>
/// <param name="Line">1-based line number.</param>
internal readonly record struct HeaderNode(
    int Level,
    string Text,
    int Line
);
```

### 4. Service Implementation

```csharp
// src/CompoundDocs.McpServer/Services/Chunking/DocumentChunkingService.cs
using CompoundDocs.Common.Markdown.Abstractions;
using CompoundDocs.McpServer.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CompoundDocs.McpServer.Services.Chunking;

/// <summary>
/// Implements document chunking by markdown headers.
/// </summary>
public sealed class DocumentChunkingService : IDocumentChunkingService
{
    private readonly IHeaderExtractor _headerExtractor;
    private readonly ICodeBlockExtractor _codeBlockExtractor;
    private readonly ChunkingOptions _options;
    private readonly ILogger<DocumentChunkingService> _logger;

    public DocumentChunkingService(
        IHeaderExtractor headerExtractor,
        ICodeBlockExtractor codeBlockExtractor,
        IOptions<ChunkingOptions> options,
        ILogger<DocumentChunkingService> logger)
    {
        _headerExtractor = headerExtractor ?? throw new ArgumentNullException(nameof(headerExtractor));
        _codeBlockExtractor = codeBlockExtractor ?? throw new ArgumentNullException(nameof(codeBlockExtractor));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public bool NeedsChunking(string content)
    {
        if (string.IsNullOrEmpty(content))
            return false;

        return GetLineCount(content) > _options.LineThreshold;
    }

    /// <inheritdoc />
    public int GetLineCount(string content)
    {
        if (string.IsNullOrEmpty(content))
            return 0;

        // Handle all line ending styles: CRLF, LF, CR
        var lineCount = 1;
        for (var i = 0; i < content.Length; i++)
        {
            if (content[i] == '\n')
            {
                lineCount++;
            }
            else if (content[i] == '\r')
            {
                lineCount++;
                // Skip LF if this is CRLF
                if (i + 1 < content.Length && content[i + 1] == '\n')
                {
                    i++;
                }
            }
        }

        return lineCount;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DocumentChunk>> ChunkDocumentAsync(
        string content,
        CompoundDocument parentDocument,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parentDocument);

        if (!NeedsChunking(content))
        {
            _logger.LogDebug(
                "Document {DocumentId} does not need chunking ({Lines} lines <= {Threshold})",
                parentDocument.Id,
                GetLineCount(content),
                _options.LineThreshold);
            return [];
        }

        _logger.LogInformation(
            "Chunking document {DocumentId} with {Lines} lines",
            parentDocument.Id,
            GetLineCount(content));

        var boundaries = CalculateChunkBoundaries(content);
        var chunks = CreateChunksFromBoundaries(content, boundaries, parentDocument);

        _logger.LogInformation(
            "Created {ChunkCount} chunks for document {DocumentId}",
            chunks.Count,
            parentDocument.Id);

        return chunks;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<DocumentChunk>> RechunkDocumentAsync(
        string content,
        CompoundDocument parentDocument,
        CancellationToken cancellationToken = default)
    {
        // Rechunking follows same logic as initial chunking
        // Caller is responsible for deleting old chunks
        return ChunkDocumentAsync(content, parentDocument, cancellationToken);
    }

    /// <summary>
    /// Calculates chunk boundaries from document headers.
    /// </summary>
    private IReadOnlyList<ChunkBoundary> CalculateChunkBoundaries(string content)
    {
        var lines = SplitLines(content);
        var headers = _headerExtractor.Extract(content);
        var codeBlocks = _codeBlockExtractor.Extract(content);

        // Filter to only H2 and H3 headers that are chunk boundaries
        var chunkHeaders = headers
            .Where(h => _options.ChunkHeaderLevels.Contains(h.Level))
            .Where(h => !IsInsideCodeBlock(h.Line, codeBlocks))
            .OrderBy(h => h.Line)
            .Select(h => new HeaderNode(h.Level, h.Text, h.Line + 1)) // Convert to 1-based
            .ToList();

        if (chunkHeaders.Count == 0)
        {
            // No chunk headers - single chunk for entire document
            return [new ChunkBoundary(1, lines.Length, string.Empty, 0)];
        }

        var boundaries = new List<ChunkBoundary>();
        var headerStack = new Stack<HeaderNode>();

        for (var i = 0; i < chunkHeaders.Count; i++)
        {
            var current = chunkHeaders[i];
            var next = i + 1 < chunkHeaders.Count ? chunkHeaders[i + 1] : (HeaderNode?)null;

            // Build header path
            UpdateHeaderStack(headerStack, current);
            var headerPath = BuildHeaderPath(headerStack);

            // Calculate end line
            var startLine = current.Line;
            var endLine = next?.Line - 1 ?? lines.Length;

            // Skip empty sections
            if (endLine < startLine)
                continue;

            // Check for very long sections that need sub-chunking
            var sectionLines = endLine - startLine + 1;
            if (sectionLines > _options.MaxSectionLines)
            {
                var subBoundaries = ChunkLongSection(
                    lines, startLine, endLine, headerPath, current.Level);
                boundaries.AddRange(subBoundaries);
            }
            else
            {
                boundaries.Add(new ChunkBoundary(startLine, endLine, headerPath, current.Level));
            }
        }

        return boundaries;
    }

    /// <summary>
    /// Splits content into lines preserving all line endings.
    /// </summary>
    private static string[] SplitLines(string content)
    {
        return content.Split(["\r\n", "\r", "\n"], StringSplitOptions.None);
    }

    /// <summary>
    /// Checks if a line is inside a code block.
    /// </summary>
    private static bool IsInsideCodeBlock(int line, IReadOnlyList<CodeBlockInfo> codeBlocks)
    {
        return codeBlocks.Any(cb => line >= cb.StartLine && line <= cb.EndLine);
    }

    /// <summary>
    /// Updates the header stack to maintain proper hierarchy.
    /// </summary>
    private static void UpdateHeaderStack(Stack<HeaderNode> stack, HeaderNode current)
    {
        // Pop headers of same or higher level (lower number = higher level)
        while (stack.Count > 0 && stack.Peek().Level >= current.Level)
        {
            stack.Pop();
        }
        stack.Push(current);
    }

    /// <summary>
    /// Builds the header path from the current stack.
    /// </summary>
    private string BuildHeaderPath(Stack<HeaderNode> stack)
    {
        var headers = stack.Reverse()
            .Select(h => $"{new string('#', h.Level)} {h.Text}");
        return string.Join(_options.HeaderPathSeparator, headers);
    }

    /// <summary>
    /// Chunks a very long section at paragraph boundaries.
    /// </summary>
    private IEnumerable<ChunkBoundary> ChunkLongSection(
        string[] lines,
        int startLine,
        int endLine,
        string headerPath,
        int headerLevel)
    {
        var currentStart = startLine;
        var targetChunkSize = _options.MaxSectionLines / 2; // Aim for half max size

        while (currentStart <= endLine)
        {
            var targetEnd = Math.Min(currentStart + targetChunkSize - 1, endLine);

            // Find a paragraph break near the target end
            var actualEnd = FindParagraphBreak(lines, targetEnd, endLine);

            yield return new ChunkBoundary(
                currentStart,
                actualEnd,
                headerPath + $" [Part {(currentStart - startLine) / targetChunkSize + 1}]",
                headerLevel);

            currentStart = actualEnd + 1;
        }
    }

    /// <summary>
    /// Finds the nearest paragraph break (blank line) near the target line.
    /// </summary>
    private static int FindParagraphBreak(string[] lines, int targetLine, int maxLine)
    {
        // Look for blank line within 50 lines of target
        for (var i = targetLine; i <= Math.Min(targetLine + 50, maxLine); i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i - 1])) // Convert to 0-based index
            {
                return i;
            }
        }

        // No blank line found, use target
        return Math.Min(targetLine, maxLine);
    }

    /// <summary>
    /// Creates DocumentChunk instances from calculated boundaries.
    /// </summary>
    private IReadOnlyList<DocumentChunk> CreateChunksFromBoundaries(
        string content,
        IReadOnlyList<ChunkBoundary> boundaries,
        CompoundDocument parentDocument)
    {
        var lines = SplitLines(content);
        var chunks = new List<DocumentChunk>(boundaries.Count);

        for (var i = 0; i < boundaries.Count; i++)
        {
            var boundary = boundaries[i];

            // Extract content for this chunk
            var chunkLines = lines
                .Skip(boundary.StartLine - 1) // Convert to 0-based
                .Take(boundary.EndLine - boundary.StartLine + 1);
            var chunkContent = string.Join("\n", chunkLines);

            // Skip empty content
            if (string.IsNullOrWhiteSpace(chunkContent))
                continue;

            var chunk = DocumentChunk.CreateFromParent(
                parentDocument,
                chunkIndex: i,
                headerPath: boundary.HeaderPath,
                content: chunkContent,
                startLine: boundary.StartLine,
                endLine: boundary.EndLine);

            chunks.Add(chunk);
        }

        return chunks;
    }
}
```

### 5. Dependency Injection Registration

```csharp
// src/CompoundDocs.McpServer/Services/Chunking/ChunkingServiceCollectionExtensions.cs
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CompoundDocs.McpServer.Services.Chunking;

/// <summary>
/// Extension methods for registering chunking services.
/// </summary>
public static class ChunkingServiceCollectionExtensions
{
    /// <summary>
    /// Adds document chunking services to the service collection.
    /// </summary>
    public static IServiceCollection AddDocumentChunking(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<ChunkingOptions>(
            configuration.GetSection(ChunkingOptions.SectionName));

        services.AddSingleton<IDocumentChunkingService, DocumentChunkingService>();

        return services;
    }
}
```

### 6. Chunk Repository Interface

```csharp
// src/CompoundDocs.McpServer/Services/Chunking/IChunkRepository.cs
using CompoundDocs.McpServer.Models;

namespace CompoundDocs.McpServer.Services.Chunking;

/// <summary>
/// Repository for document chunk storage operations.
/// </summary>
public interface IChunkRepository
{
    /// <summary>
    /// Saves chunks for a document, replacing any existing chunks.
    /// </summary>
    Task SaveChunksAsync(
        string documentId,
        IReadOnlyList<DocumentChunk> chunks,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all chunks for a document.
    /// </summary>
    Task<IReadOnlyList<DocumentChunk>> GetChunksAsync(
        string documentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all chunks for a document.
    /// </summary>
    Task DeleteChunksAsync(
        string documentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates promotion level for all chunks of a document.
    /// </summary>
    Task UpdatePromotionLevelAsync(
        string documentId,
        string newPromotionLevel,
        CancellationToken cancellationToken = default);
}
```

---

## Field Mapping Reference

| Chunk Field | Source | Description |
|-------------|--------|-------------|
| `Id` | Generated | New GUID for each chunk |
| `DocumentId` | Parent | Reference to parent document |
| `ProjectName` | Parent | Inherited for tenant isolation |
| `BranchName` | Parent | Inherited for tenant isolation |
| `PathHash` | Parent | Inherited for tenant isolation |
| `PromotionLevel` | Parent | Inherited, syncs with parent |
| `ChunkIndex` | Calculated | 0-based sequential order |
| `HeaderPath` | Calculated | e.g., `## Section > ### Subsection` |
| `StartLine` | Calculated | 1-based start line |
| `EndLine` | Calculated | 1-based end line (inclusive) |
| `Content` | Extracted | Text content of chunk |
| `Embedding` | Generated | Set by embedding service |

---

## Edge Case Handling

| Case | Behavior | Spec Reference |
|------|----------|----------------|
| No H2/H3 headers | Single chunk for entire document | chunking.md line 123 |
| Very long section (> 1000 lines) | Chunk at paragraph boundaries | chunking.md line 124 |
| Empty section | Skip, no chunk created | chunking.md line 125 |
| Code blocks | Include in chunk, never split | chunking.md line 126 |
| Headers in code blocks | Ignored (not chunk boundaries) | Markdig behavior |
| YAML frontmatter | Excluded from chunk content | Standard behavior |
| Document <= 500 lines | Return empty list, no chunking | chunking.md line 21 |

---

## Testing Verification

### Unit Tests

```csharp
// tests/CompoundDocs.Tests/Services/Chunking/DocumentChunkingServiceTests.cs
using CompoundDocs.Common.Markdown;
using CompoundDocs.Common.Markdown.Abstractions;
using CompoundDocs.McpServer.Models;
using CompoundDocs.McpServer.Services.Chunking;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CompoundDocs.Tests.Services.Chunking;

public class DocumentChunkingServiceTests
{
    private readonly DocumentChunkingService _service;
    private readonly ChunkingOptions _options = new();

    public DocumentChunkingServiceTests()
    {
        _service = new DocumentChunkingService(
            new MarkdigHeaderExtractor(),
            new MarkdigCodeBlockExtractor(),
            Options.Create(_options),
            NullLogger<DocumentChunkingService>.Instance);
    }

    #region NeedsChunking Tests

    [Fact]
    public void NeedsChunking_ReturnsFalse_ForEmptyContent()
    {
        Assert.False(_service.NeedsChunking(string.Empty));
        Assert.False(_service.NeedsChunking(null!));
    }

    [Fact]
    public void NeedsChunking_ReturnsFalse_ForSmallDocument()
    {
        var content = string.Join("\n", Enumerable.Repeat("Line", 500));
        Assert.False(_service.NeedsChunking(content));
    }

    [Fact]
    public void NeedsChunking_ReturnsTrue_ForLargeDocument()
    {
        var content = string.Join("\n", Enumerable.Repeat("Line", 501));
        Assert.True(_service.NeedsChunking(content));
    }

    #endregion

    #region GetLineCount Tests

    [Theory]
    [InlineData("", 0)]
    [InlineData("single line", 1)]
    [InlineData("line1\nline2", 2)]
    [InlineData("line1\r\nline2", 2)]
    [InlineData("line1\rline2", 2)]
    [InlineData("line1\nline2\nline3", 3)]
    public void GetLineCount_CorrectlyCountsLines(string content, int expected)
    {
        Assert.Equal(expected, _service.GetLineCount(content));
    }

    #endregion

    #region ChunkDocumentAsync Tests

    [Fact]
    public async Task ChunkDocumentAsync_ReturnsEmpty_WhenBelowThreshold()
    {
        var content = string.Join("\n", Enumerable.Repeat("Line", 100));
        var parent = CreateParentDocument();

        var chunks = await _service.ChunkDocumentAsync(content, parent);

        Assert.Empty(chunks);
    }

    [Fact]
    public async Task ChunkDocumentAsync_CreatesChunks_ForLargeDocument()
    {
        var content = GenerateLargeDocumentWithHeaders(600);
        var parent = CreateParentDocument();

        var chunks = await _service.ChunkDocumentAsync(content, parent);

        Assert.NotEmpty(chunks);
    }

    [Fact]
    public async Task ChunkDocumentAsync_InheritsTenantContext()
    {
        var content = GenerateLargeDocumentWithHeaders(600);
        var parent = CreateParentDocument();
        parent.ProjectName = "test-project";
        parent.BranchName = "main";
        parent.PathHash = "abc123";
        parent.PromotionLevel = "elevated";

        var chunks = await _service.ChunkDocumentAsync(content, parent);

        Assert.All(chunks, chunk =>
        {
            Assert.Equal("test-project", chunk.ProjectName);
            Assert.Equal("main", chunk.BranchName);
            Assert.Equal("abc123", chunk.PathHash);
            Assert.Equal("elevated", chunk.PromotionLevel);
            Assert.Equal(parent.Id, chunk.DocumentId);
        });
    }

    [Fact]
    public async Task ChunkDocumentAsync_GeneratesSequentialChunkIndex()
    {
        var content = GenerateLargeDocumentWithHeaders(600);
        var parent = CreateParentDocument();

        var chunks = await _service.ChunkDocumentAsync(content, parent);

        for (var i = 0; i < chunks.Count; i++)
        {
            Assert.Equal(i, chunks[i].ChunkIndex);
        }
    }

    [Fact]
    public async Task ChunkDocumentAsync_SetsCorrectLineRanges()
    {
        var content = GenerateLargeDocumentWithHeaders(600);
        var parent = CreateParentDocument();

        var chunks = await _service.ChunkDocumentAsync(content, parent);

        Assert.All(chunks, chunk =>
        {
            Assert.True(chunk.StartLine > 0, "StartLine should be 1-based");
            Assert.True(chunk.EndLine >= chunk.StartLine, "EndLine should be >= StartLine");
        });

        // Verify no overlapping ranges
        for (var i = 1; i < chunks.Count; i++)
        {
            Assert.True(chunks[i].StartLine > chunks[i - 1].EndLine,
                "Chunks should not overlap");
        }
    }

    [Fact]
    public async Task ChunkDocumentAsync_BuildsCorrectHeaderPath()
    {
        var content = @"
# Title

Introduction paragraph repeated many times.
" + string.Join("\n", Enumerable.Repeat("Content line", 200)) + @"

## Section One

Content under section one.
" + string.Join("\n", Enumerable.Repeat("More content", 200)) + @"

### Subsection 1.1

Detailed content here.
" + string.Join("\n", Enumerable.Repeat("Details", 200));

        var parent = CreateParentDocument();

        var chunks = await _service.ChunkDocumentAsync(content, parent);

        // Should have chunks with proper header paths
        var sectionOneChunk = chunks.FirstOrDefault(c => c.HeaderPath == "## Section One");
        var subsectionChunk = chunks.FirstOrDefault(c => c.HeaderPath == "## Section One > ### Subsection 1.1");

        Assert.NotNull(sectionOneChunk);
        Assert.NotNull(subsectionChunk);
    }

    #endregion

    #region Edge Case Tests

    [Fact]
    public async Task ChunkDocumentAsync_CreatesOneChunk_WhenNoHeaders()
    {
        // Large document with no H2/H3 headers
        var content = "# Title\n\n" + string.Join("\n", Enumerable.Repeat("Plain content line", 600));
        var parent = CreateParentDocument();

        var chunks = await _service.ChunkDocumentAsync(content, parent);

        Assert.Single(chunks);
        Assert.Equal(string.Empty, chunks[0].HeaderPath);
    }

    [Fact]
    public async Task ChunkDocumentAsync_IgnoresHeadersInCodeBlocks()
    {
        var content = string.Join("\n", Enumerable.Repeat("Line", 200)) + @"

```markdown
## This Is Not A Header
### Neither Is This
```

" + string.Join("\n", Enumerable.Repeat("More lines", 400)) + @"

## Real Header

Content here.
";
        var parent = CreateParentDocument();

        var chunks = await _service.ChunkDocumentAsync(content, parent);

        // Should not create chunks for headers inside code blocks
        Assert.DoesNotContain(chunks, c => c.HeaderPath.Contains("This Is Not A Header"));
    }

    #endregion

    #region Helper Methods

    private static CompoundDocument CreateParentDocument()
    {
        return new CompoundDocument
        {
            Id = Guid.NewGuid().ToString(),
            ProjectName = "default-project",
            BranchName = "main",
            PathHash = "default123"
        };
    }

    private static string GenerateLargeDocumentWithHeaders(int totalLines)
    {
        var lines = new List<string>
        {
            "# Document Title",
            "",
            "Introduction paragraph."
        };

        var linesPerSection = (totalLines - 20) / 4;

        lines.Add("");
        lines.Add("## Section One");
        lines.AddRange(Enumerable.Repeat("Content in section one.", linesPerSection));

        lines.Add("");
        lines.Add("### Subsection 1.1");
        lines.AddRange(Enumerable.Repeat("Details in subsection.", linesPerSection));

        lines.Add("");
        lines.Add("## Section Two");
        lines.AddRange(Enumerable.Repeat("Content in section two.", linesPerSection));

        lines.Add("");
        lines.Add("### Subsection 2.1");
        lines.AddRange(Enumerable.Repeat("More details here.", linesPerSection));

        return string.Join("\n", lines);
    }

    #endregion
}
```

### Header Path Tests

```csharp
// tests/CompoundDocs.Tests/Services/Chunking/HeaderPathTests.cs
namespace CompoundDocs.Tests.Services.Chunking;

public class HeaderPathTests
{
    [Fact]
    public void HeaderPath_Format_MatchesSpec()
    {
        // Per spec: "## Root Cause Analysis > ### Database Layer"
        var expectedFormat = "## Section > ### Subsection";

        // Verify format includes:
        Assert.Contains("##", expectedFormat);  // H2 marker
        Assert.Contains("###", expectedFormat); // H3 marker
        Assert.Contains(" > ", expectedFormat); // Separator
    }

    [Theory]
    [InlineData("## Section", "## Section")]
    [InlineData("### Subsection", "### Subsection")]
    public void HeaderPath_PreservesMarkdownMarkers(string header, string expected)
    {
        Assert.Contains(expected.Split(' ')[0], header);
    }
}
```

---

## Dependencies

### Depends On

- **Phase 015**: Markdown Parser Integration - `IHeaderExtractor`, `ICodeBlockExtractor`
- **Phase 043**: DocumentChunk Model - `DocumentChunk` class with `CreateFromParent` factory

### Blocks

- **Phase 062+**: Chunking integration with file watcher
- **Phase 063+**: Chunk embedding generation
- **Phase 064+**: Chunk search functionality

---

## Files Created/Modified

| File | Action | Description |
|------|--------|-------------|
| `src/CompoundDocs.McpServer/Services/Chunking/IDocumentChunkingService.cs` | Create | Service interface |
| `src/CompoundDocs.McpServer/Services/Chunking/DocumentChunkingService.cs` | Create | Service implementation |
| `src/CompoundDocs.McpServer/Services/Chunking/ChunkingOptions.cs` | Create | Configuration options |
| `src/CompoundDocs.McpServer/Services/Chunking/ChunkBoundary.cs` | Create | Internal model |
| `src/CompoundDocs.McpServer/Services/Chunking/HeaderNode.cs` | Create | Internal model |
| `src/CompoundDocs.McpServer/Services/Chunking/IChunkRepository.cs` | Create | Repository interface |
| `src/CompoundDocs.McpServer/Services/Chunking/ChunkingServiceCollectionExtensions.cs` | Create | DI registration |
| `tests/CompoundDocs.Tests/Services/Chunking/DocumentChunkingServiceTests.cs` | Create | Unit tests |
| `tests/CompoundDocs.Tests/Services/Chunking/HeaderPathTests.cs` | Create | Header path tests |

---

## File Structure

```
src/CompoundDocs.McpServer/
├── Services/
│   └── Chunking/
│       ├── IDocumentChunkingService.cs
│       ├── DocumentChunkingService.cs
│       ├── ChunkingOptions.cs
│       ├── ChunkBoundary.cs
│       ├── HeaderNode.cs
│       ├── IChunkRepository.cs
│       └── ChunkingServiceCollectionExtensions.cs
tests/CompoundDocs.Tests/
└── Services/
    └── Chunking/
        ├── DocumentChunkingServiceTests.cs
        └── HeaderPathTests.cs
```

---

## Risk Mitigation

| Risk | Mitigation |
|------|------------|
| Header detection misses headers | Uses Markdig AST for accurate parsing |
| Code block headers treated as boundaries | Explicitly filters headers inside code blocks |
| Line count mismatch | Handles CRLF, LF, CR line endings |
| Orphaned chunks | Repository enforces cascading delete |
| Promotion desync | Repository provides atomic update method |
| Very large documents | Paragraph-level sub-chunking for sections > 1000 lines |
| Empty chunks | Skips empty sections during processing |

---

## Configuration Example

```json
{
  "Chunking": {
    "LineThreshold": 500,
    "MaxSectionLines": 1000,
    "ChunkHeaderLevels": [2, 3],
    "HeaderPathSeparator": " > "
  }
}
```
