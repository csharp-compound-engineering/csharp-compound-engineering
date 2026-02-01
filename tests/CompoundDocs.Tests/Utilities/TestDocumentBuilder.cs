using CompoundDocs.McpServer.Models;

namespace CompoundDocs.Tests.Utilities;

/// <summary>
/// Fluent builder for creating test CompoundDocument instances.
/// Provides convenient defaults while allowing customization of all properties.
/// </summary>
public sealed class TestDocumentBuilder
{
    private string _id = Guid.NewGuid().ToString();
    private string _tenantKey = "test-project:main:abc123";
    private string _title = "Test Document";
    private string _content = "# Test Document\n\nThis is test content for the document.";
    private string _docType = DocumentTypes.Doc;
    private string _promotionLevel = PromotionLevels.Standard;
    private string _filePath = "docs/test-document.md";
    private DateTimeOffset _lastModified = DateTimeOffset.UtcNow;
    private string? _links;
    private ReadOnlyMemory<float>? _vector;

    /// <summary>
    /// Creates a new TestDocumentBuilder with default values.
    /// </summary>
    public TestDocumentBuilder()
    {
    }

    /// <summary>
    /// Sets the document ID.
    /// </summary>
    /// <param name="id">The document ID.</param>
    /// <returns>The builder instance for chaining.</returns>
    public TestDocumentBuilder WithId(string id)
    {
        _id = id;
        return this;
    }

    /// <summary>
    /// Sets the tenant key.
    /// </summary>
    /// <param name="tenantKey">The tenant key.</param>
    /// <returns>The builder instance for chaining.</returns>
    public TestDocumentBuilder WithTenantKey(string tenantKey)
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
    public TestDocumentBuilder WithTenantKey(string projectName, string branchName, string pathHash)
    {
        _tenantKey = CompoundDocument.CreateTenantKey(projectName, branchName, pathHash);
        return this;
    }

    /// <summary>
    /// Sets the document title.
    /// </summary>
    /// <param name="title">The document title.</param>
    /// <returns>The builder instance for chaining.</returns>
    public TestDocumentBuilder WithTitle(string title)
    {
        _title = title;
        return this;
    }

    /// <summary>
    /// Sets the document content.
    /// </summary>
    /// <param name="content">The document content.</param>
    /// <returns>The builder instance for chaining.</returns>
    public TestDocumentBuilder WithContent(string content)
    {
        _content = content;
        return this;
    }

    /// <summary>
    /// Sets the document type.
    /// </summary>
    /// <param name="docType">The document type.</param>
    /// <returns>The builder instance for chaining.</returns>
    public TestDocumentBuilder WithDocType(string docType)
    {
        _docType = docType;
        return this;
    }

    /// <summary>
    /// Sets the document as a specification type.
    /// </summary>
    /// <returns>The builder instance for chaining.</returns>
    public TestDocumentBuilder AsSpec()
    {
        _docType = DocumentTypes.Spec;
        return this;
    }

    /// <summary>
    /// Sets the document as an ADR type.
    /// </summary>
    /// <returns>The builder instance for chaining.</returns>
    public TestDocumentBuilder AsAdr()
    {
        _docType = DocumentTypes.Adr;
        return this;
    }

    /// <summary>
    /// Sets the document as a research type.
    /// </summary>
    /// <returns>The builder instance for chaining.</returns>
    public TestDocumentBuilder AsResearch()
    {
        _docType = DocumentTypes.Research;
        return this;
    }

    /// <summary>
    /// Sets the promotion level.
    /// </summary>
    /// <param name="promotionLevel">The promotion level.</param>
    /// <returns>The builder instance for chaining.</returns>
    public TestDocumentBuilder WithPromotionLevel(string promotionLevel)
    {
        _promotionLevel = promotionLevel;
        return this;
    }

    /// <summary>
    /// Sets the document as standard promotion level.
    /// </summary>
    /// <returns>The builder instance for chaining.</returns>
    public TestDocumentBuilder AsStandard()
    {
        _promotionLevel = PromotionLevels.Standard;
        return this;
    }

    /// <summary>
    /// Sets the document as promoted.
    /// </summary>
    /// <returns>The builder instance for chaining.</returns>
    public TestDocumentBuilder AsPromoted()
    {
        _promotionLevel = PromotionLevels.Promoted;
        return this;
    }

    /// <summary>
    /// Sets the document as pinned.
    /// </summary>
    /// <returns>The builder instance for chaining.</returns>
    public TestDocumentBuilder AsPinned()
    {
        _promotionLevel = PromotionLevels.Pinned;
        return this;
    }

    /// <summary>
    /// Sets the file path.
    /// </summary>
    /// <param name="filePath">The relative file path.</param>
    /// <returns>The builder instance for chaining.</returns>
    public TestDocumentBuilder WithFilePath(string filePath)
    {
        _filePath = filePath;
        return this;
    }

    /// <summary>
    /// Sets the last modified timestamp.
    /// </summary>
    /// <param name="lastModified">The last modified timestamp.</param>
    /// <returns>The builder instance for chaining.</returns>
    public TestDocumentBuilder WithLastModified(DateTimeOffset lastModified)
    {
        _lastModified = lastModified;
        return this;
    }

    /// <summary>
    /// Sets the document links (JSON-serialized).
    /// </summary>
    /// <param name="links">The links JSON string.</param>
    /// <returns>The builder instance for chaining.</returns>
    public TestDocumentBuilder WithLinks(string links)
    {
        _links = links;
        return this;
    }

    /// <summary>
    /// Sets the links from an array of paths.
    /// </summary>
    /// <param name="linkPaths">The link paths.</param>
    /// <returns>The builder instance for chaining.</returns>
    public TestDocumentBuilder WithLinks(params string[] linkPaths)
    {
        _links = System.Text.Json.JsonSerializer.Serialize(linkPaths);
        return this;
    }

    /// <summary>
    /// Sets the vector embedding.
    /// </summary>
    /// <param name="vector">The embedding vector.</param>
    /// <returns>The builder instance for chaining.</returns>
    public TestDocumentBuilder WithVector(ReadOnlyMemory<float> vector)
    {
        _vector = vector;
        return this;
    }

    /// <summary>
    /// Sets a random vector embedding of the specified dimensions.
    /// </summary>
    /// <param name="dimensions">The vector dimensions (default: 1024).</param>
    /// <returns>The builder instance for chaining.</returns>
    public TestDocumentBuilder WithRandomVector(int dimensions = 1024)
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
    /// Builds the CompoundDocument instance.
    /// </summary>
    /// <returns>A new CompoundDocument instance with the configured values.</returns>
    public CompoundDocument Build()
    {
        return new CompoundDocument
        {
            Id = _id,
            TenantKey = _tenantKey,
            Title = _title,
            Content = _content,
            DocType = _docType,
            PromotionLevel = _promotionLevel,
            FilePath = _filePath,
            LastModified = _lastModified,
            Links = _links,
            Vector = _vector
        };
    }

    /// <summary>
    /// Builds multiple documents with sequential IDs and file paths.
    /// </summary>
    /// <param name="count">The number of documents to build.</param>
    /// <returns>A list of CompoundDocument instances.</returns>
    public IReadOnlyList<CompoundDocument> BuildMany(int count)
    {
        var documents = new List<CompoundDocument>(count);
        for (int i = 0; i < count; i++)
        {
            documents.Add(new CompoundDocument
            {
                Id = $"{_id}-{i}",
                TenantKey = _tenantKey,
                Title = $"{_title} {i + 1}",
                Content = $"{_content}\n\nDocument {i + 1}",
                DocType = _docType,
                PromotionLevel = _promotionLevel,
                FilePath = $"{Path.GetDirectoryName(_filePath)}/{Path.GetFileNameWithoutExtension(_filePath)}-{i + 1}{Path.GetExtension(_filePath)}",
                LastModified = _lastModified.AddMinutes(i),
                Links = _links,
                Vector = _vector
            });
        }
        return documents;
    }

    /// <summary>
    /// Creates a new builder with default test values.
    /// </summary>
    /// <returns>A new TestDocumentBuilder instance.</returns>
    public static TestDocumentBuilder Create() => new();

    /// <summary>
    /// Creates a builder for a spec document with typical spec content.
    /// </summary>
    /// <returns>A new TestDocumentBuilder configured for spec documents.</returns>
    public static TestDocumentBuilder CreateSpec()
    {
        return new TestDocumentBuilder()
            .AsSpec()
            .WithTitle("API Specification")
            .WithContent("# API Specification\n\n## Overview\n\nThis document describes the API.\n\n## Endpoints\n\n### GET /api/items\n\nReturns a list of items.")
            .WithFilePath("specs/api-spec.md");
    }

    /// <summary>
    /// Creates a builder for an ADR document with typical ADR content.
    /// </summary>
    /// <returns>A new TestDocumentBuilder configured for ADR documents.</returns>
    public static TestDocumentBuilder CreateAdr()
    {
        return new TestDocumentBuilder()
            .AsAdr()
            .WithTitle("ADR-001: Use PostgreSQL")
            .WithContent("# ADR-001: Use PostgreSQL\n\n## Status\nAccepted\n\n## Context\nWe need a database.\n\n## Decision\nWe will use PostgreSQL.\n\n## Consequences\nPositive: Reliable database.")
            .WithFilePath("docs/adr/001-use-postgresql.md");
    }

    /// <summary>
    /// Creates a builder for a research document.
    /// </summary>
    /// <returns>A new TestDocumentBuilder configured for research documents.</returns>
    public static TestDocumentBuilder CreateResearch()
    {
        return new TestDocumentBuilder()
            .AsResearch()
            .WithTitle("Vector Database Research")
            .WithContent("# Vector Database Research\n\n## Summary\nResearch findings on vector databases.\n\n## Options Evaluated\n- pgvector\n- Pinecone\n- Qdrant")
            .WithFilePath("research/vector-db-research.md");
    }
}
