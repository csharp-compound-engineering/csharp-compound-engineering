using CompoundDocs.McpServer.Models;

namespace CompoundDocs.Tests.Utilities;

/// <summary>
/// Fluent builder for creating test DocumentChunk instances.
/// Provides convenient defaults while allowing customization of all properties.
/// </summary>
public sealed class TestChunkBuilder
{
    private string _id = Guid.NewGuid().ToString();
    private string _documentId = Guid.NewGuid().ToString();
    private string _tenantKey = "test-project:main:abc123";
    private string _headerPath = "## Overview";
    private int _startLine = 1;
    private int _endLine = 50;
    private string _content = "This is the content of the test chunk. It contains relevant information about the topic.";
    private string _promotionLevel = PromotionLevels.Standard;
    private ReadOnlyMemory<float>? _vector;

    /// <summary>
    /// Creates a new TestChunkBuilder with default values.
    /// </summary>
    public TestChunkBuilder()
    {
    }

    /// <summary>
    /// Sets the chunk ID.
    /// </summary>
    /// <param name="id">The chunk ID.</param>
    /// <returns>The builder instance for chaining.</returns>
    public TestChunkBuilder WithId(string id)
    {
        _id = id;
        return this;
    }

    /// <summary>
    /// Sets the parent document ID.
    /// </summary>
    /// <param name="documentId">The parent document ID.</param>
    /// <returns>The builder instance for chaining.</returns>
    public TestChunkBuilder WithDocumentId(string documentId)
    {
        _documentId = documentId;
        return this;
    }

    /// <summary>
    /// Sets the parent document from a CompoundDocument instance.
    /// </summary>
    /// <param name="document">The parent document.</param>
    /// <returns>The builder instance for chaining.</returns>
    public TestChunkBuilder WithParentDocument(CompoundDocument document)
    {
        _documentId = document.Id;
        _tenantKey = document.TenantKey;
        _promotionLevel = document.PromotionLevel;
        return this;
    }

    /// <summary>
    /// Sets the tenant key.
    /// </summary>
    /// <param name="tenantKey">The tenant key.</param>
    /// <returns>The builder instance for chaining.</returns>
    public TestChunkBuilder WithTenantKey(string tenantKey)
    {
        _tenantKey = tenantKey;
        return this;
    }

    /// <summary>
    /// Sets the tenant key from component parts.
    /// </summary>
    /// <param name="projectName">The project name.</param>
    /// <param name="branchName">The branch name.</param>
    /// <param name="pathHash">The path hash.</param>
    /// <returns>The builder instance for chaining.</returns>
    public TestChunkBuilder WithTenantKey(string projectName, string branchName, string pathHash)
    {
        _tenantKey = CompoundDocument.CreateTenantKey(projectName, branchName, pathHash);
        return this;
    }

    /// <summary>
    /// Sets the header path representing the chunk's location in the document structure.
    /// </summary>
    /// <param name="headerPath">The header path.</param>
    /// <returns>The builder instance for chaining.</returns>
    public TestChunkBuilder WithHeaderPath(string headerPath)
    {
        _headerPath = headerPath;
        return this;
    }

    /// <summary>
    /// Sets the starting line number.
    /// </summary>
    /// <param name="startLine">The starting line number (1-indexed).</param>
    /// <returns>The builder instance for chaining.</returns>
    public TestChunkBuilder WithStartLine(int startLine)
    {
        _startLine = startLine;
        return this;
    }

    /// <summary>
    /// Sets the ending line number.
    /// </summary>
    /// <param name="endLine">The ending line number (1-indexed, inclusive).</param>
    /// <returns>The builder instance for chaining.</returns>
    public TestChunkBuilder WithEndLine(int endLine)
    {
        _endLine = endLine;
        return this;
    }

    /// <summary>
    /// Sets both start and end line numbers.
    /// </summary>
    /// <param name="startLine">The starting line number.</param>
    /// <param name="endLine">The ending line number.</param>
    /// <returns>The builder instance for chaining.</returns>
    public TestChunkBuilder WithLineRange(int startLine, int endLine)
    {
        _startLine = startLine;
        _endLine = endLine;
        return this;
    }

    /// <summary>
    /// Sets the chunk content.
    /// </summary>
    /// <param name="content">The chunk content.</param>
    /// <returns>The builder instance for chaining.</returns>
    public TestChunkBuilder WithContent(string content)
    {
        _content = content;
        return this;
    }

    /// <summary>
    /// Sets the promotion level.
    /// </summary>
    /// <param name="promotionLevel">The promotion level.</param>
    /// <returns>The builder instance for chaining.</returns>
    public TestChunkBuilder WithPromotionLevel(string promotionLevel)
    {
        _promotionLevel = promotionLevel;
        return this;
    }

    /// <summary>
    /// Sets the chunk as standard promotion level.
    /// </summary>
    /// <returns>The builder instance for chaining.</returns>
    public TestChunkBuilder AsStandard()
    {
        _promotionLevel = PromotionLevels.Standard;
        return this;
    }

    /// <summary>
    /// Sets the chunk as promoted.
    /// </summary>
    /// <returns>The builder instance for chaining.</returns>
    public TestChunkBuilder AsPromoted()
    {
        _promotionLevel = PromotionLevels.Promoted;
        return this;
    }

    /// <summary>
    /// Sets the chunk as pinned.
    /// </summary>
    /// <returns>The builder instance for chaining.</returns>
    public TestChunkBuilder AsPinned()
    {
        _promotionLevel = PromotionLevels.Pinned;
        return this;
    }

    /// <summary>
    /// Sets the vector embedding.
    /// </summary>
    /// <param name="vector">The embedding vector.</param>
    /// <returns>The builder instance for chaining.</returns>
    public TestChunkBuilder WithVector(ReadOnlyMemory<float> vector)
    {
        _vector = vector;
        return this;
    }

    /// <summary>
    /// Sets a random vector embedding of the specified dimensions.
    /// </summary>
    /// <param name="dimensions">The vector dimensions (default: 1024).</param>
    /// <returns>The builder instance for chaining.</returns>
    public TestChunkBuilder WithRandomVector(int dimensions = 1024)
    {
        var random = new Random();
        var vector = new float[dimensions];
        for (int i = 0; i < dimensions; i++)
        {
            vector[i] = (float)random.NextDouble();
        }
        // Normalize the vector
        var magnitude = (float)Math.Sqrt(vector.Sum(v => v * v));
        for (int i = 0; i < dimensions; i++)
        {
            vector[i] /= magnitude;
        }
        _vector = new ReadOnlyMemory<float>(vector);
        return this;
    }

    /// <summary>
    /// Builds the DocumentChunk instance.
    /// </summary>
    /// <returns>A new DocumentChunk instance with the configured values.</returns>
    public DocumentChunk Build()
    {
        return new DocumentChunk
        {
            Id = _id,
            DocumentId = _documentId,
            TenantKey = _tenantKey,
            HeaderPath = _headerPath,
            StartLine = _startLine,
            EndLine = _endLine,
            Content = _content,
            PromotionLevel = _promotionLevel,
            Vector = _vector
        };
    }

    /// <summary>
    /// Builds multiple chunks with sequential IDs and line ranges.
    /// </summary>
    /// <param name="count">The number of chunks to build.</param>
    /// <param name="linesPerChunk">Lines per chunk (default: 50).</param>
    /// <returns>A list of DocumentChunk instances.</returns>
    public IReadOnlyList<DocumentChunk> BuildMany(int count, int linesPerChunk = 50)
    {
        var chunks = new List<DocumentChunk>(count);
        for (int i = 0; i < count; i++)
        {
            int startLine = i * linesPerChunk + 1;
            int endLine = startLine + linesPerChunk - 1;

            chunks.Add(new DocumentChunk
            {
                Id = $"{_id}-{i}",
                DocumentId = _documentId,
                TenantKey = _tenantKey,
                HeaderPath = $"{_headerPath} > Section {i + 1}",
                StartLine = startLine,
                EndLine = endLine,
                Content = $"{_content}\n\nSection {i + 1} content.",
                PromotionLevel = _promotionLevel,
                Vector = _vector
            });
        }
        return chunks;
    }

    /// <summary>
    /// Creates chunks from a parent document.
    /// </summary>
    /// <param name="document">The parent document.</param>
    /// <param name="chunkCount">Number of chunks to create.</param>
    /// <returns>A list of DocumentChunk instances linked to the parent.</returns>
    public static IReadOnlyList<DocumentChunk> CreateFromDocument(CompoundDocument document, int chunkCount = 3)
    {
        return new TestChunkBuilder()
            .WithParentDocument(document)
            .BuildMany(chunkCount);
    }

    /// <summary>
    /// Creates a new builder with default test values.
    /// </summary>
    /// <returns>A new TestChunkBuilder instance.</returns>
    public static TestChunkBuilder Create() => new();

    /// <summary>
    /// Creates a builder for a chunk with overview content.
    /// </summary>
    /// <returns>A new TestChunkBuilder configured for overview chunks.</returns>
    public static TestChunkBuilder CreateOverviewChunk()
    {
        return new TestChunkBuilder()
            .WithHeaderPath("## Overview")
            .WithLineRange(1, 30)
            .WithContent("# Document Title\n\n## Overview\n\nThis document provides an overview of the system architecture and design decisions.");
    }

    /// <summary>
    /// Creates a builder for a chunk with implementation details.
    /// </summary>
    /// <returns>A new TestChunkBuilder configured for implementation chunks.</returns>
    public static TestChunkBuilder CreateImplementationChunk()
    {
        return new TestChunkBuilder()
            .WithHeaderPath("## Implementation")
            .WithLineRange(31, 80)
            .WithContent("## Implementation\n\nThe implementation follows these patterns:\n\n1. Repository pattern for data access\n2. Dependency injection for services\n3. Unit tests for all business logic");
    }

    /// <summary>
    /// Creates a builder for a chunk with configuration details.
    /// </summary>
    /// <returns>A new TestChunkBuilder configured for configuration chunks.</returns>
    public static TestChunkBuilder CreateConfigurationChunk()
    {
        return new TestChunkBuilder()
            .WithHeaderPath("## Configuration > ### Database Settings")
            .WithLineRange(81, 120)
            .WithContent("## Configuration\n\n### Database Settings\n\nThe database connection string should be configured in appsettings.json:\n\n```json\n{\n  \"ConnectionStrings\": {\n    \"DefaultConnection\": \"Host=localhost;Database=mydb\"\n  }\n}\n```");
    }
}
