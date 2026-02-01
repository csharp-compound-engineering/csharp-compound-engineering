using CompoundDocs.McpServer.Models;

namespace CompoundDocs.Tests.Utilities;

/// <summary>
/// Fluent builder for creating test ExternalDocument instances.
/// Provides convenient defaults while allowing customization of all properties.
/// </summary>
public sealed class TestExternalDocumentBuilder
{
    private string _id = Guid.NewGuid().ToString();
    private string _tenantKey = "test-project:main:abc123";
    private string _title = "External Test Document";
    private string _content = "# External Document\n\nThis is external documentation content.";
    private string _relativePath = "docs/external/test-doc.md";
    private string? _sourceUrl = "https://example.com/docs/test-doc.md";
    private DateTimeOffset? _lastSyncedAt = DateTimeOffset.UtcNow.AddHours(-1);
    private string _namespacePrefix = "external";
    private string _contentHash = "abc123def456";
    private int _charCount = 100;
    private ReadOnlyMemory<float>? _vector;

    /// <summary>
    /// Creates a new TestExternalDocumentBuilder with default values.
    /// </summary>
    public TestExternalDocumentBuilder()
    {
    }

    /// <summary>
    /// Sets the document ID.
    /// </summary>
    /// <param name="id">The document ID.</param>
    /// <returns>The builder instance for chaining.</returns>
    public TestExternalDocumentBuilder WithId(string id)
    {
        _id = id;
        return this;
    }

    /// <summary>
    /// Sets the tenant key.
    /// </summary>
    /// <param name="tenantKey">The tenant key.</param>
    /// <returns>The builder instance for chaining.</returns>
    public TestExternalDocumentBuilder WithTenantKey(string tenantKey)
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
    public TestExternalDocumentBuilder WithTenantKey(string projectName, string branchName, string pathHash)
    {
        _tenantKey = ExternalDocument.CreateTenantKey(projectName, branchName, pathHash);
        return this;
    }

    /// <summary>
    /// Sets the document title.
    /// </summary>
    /// <param name="title">The document title.</param>
    /// <returns>The builder instance for chaining.</returns>
    public TestExternalDocumentBuilder WithTitle(string title)
    {
        _title = title;
        return this;
    }

    /// <summary>
    /// Sets the document content.
    /// </summary>
    /// <param name="content">The document content.</param>
    /// <returns>The builder instance for chaining.</returns>
    public TestExternalDocumentBuilder WithContent(string content)
    {
        _content = content;
        _charCount = content.Length;
        return this;
    }

    /// <summary>
    /// Sets the relative path within the external docs folder.
    /// </summary>
    /// <param name="relativePath">The relative path.</param>
    /// <returns>The builder instance for chaining.</returns>
    public TestExternalDocumentBuilder WithRelativePath(string relativePath)
    {
        _relativePath = relativePath;
        return this;
    }

    /// <summary>
    /// Sets the source URL where the document was fetched from.
    /// </summary>
    /// <param name="sourceUrl">The source URL.</param>
    /// <returns>The builder instance for chaining.</returns>
    public TestExternalDocumentBuilder WithSourceUrl(string? sourceUrl)
    {
        _sourceUrl = sourceUrl;
        return this;
    }

    /// <summary>
    /// Sets the last synchronization timestamp.
    /// </summary>
    /// <param name="lastSyncedAt">The last sync timestamp.</param>
    /// <returns>The builder instance for chaining.</returns>
    public TestExternalDocumentBuilder WithLastSyncedAt(DateTimeOffset? lastSyncedAt)
    {
        _lastSyncedAt = lastSyncedAt;
        return this;
    }

    /// <summary>
    /// Sets the namespace prefix for categorization.
    /// </summary>
    /// <param name="namespacePrefix">The namespace prefix.</param>
    /// <returns>The builder instance for chaining.</returns>
    public TestExternalDocumentBuilder WithNamespacePrefix(string namespacePrefix)
    {
        _namespacePrefix = namespacePrefix;
        return this;
    }

    /// <summary>
    /// Sets the content hash for change detection.
    /// </summary>
    /// <param name="contentHash">The SHA256 content hash.</param>
    /// <returns>The builder instance for chaining.</returns>
    public TestExternalDocumentBuilder WithContentHash(string contentHash)
    {
        _contentHash = contentHash;
        return this;
    }

    /// <summary>
    /// Sets the character count.
    /// </summary>
    /// <param name="charCount">The character count.</param>
    /// <returns>The builder instance for chaining.</returns>
    public TestExternalDocumentBuilder WithCharCount(int charCount)
    {
        _charCount = charCount;
        return this;
    }

    /// <summary>
    /// Sets the vector embedding.
    /// </summary>
    /// <param name="vector">The embedding vector.</param>
    /// <returns>The builder instance for chaining.</returns>
    public TestExternalDocumentBuilder WithVector(ReadOnlyMemory<float> vector)
    {
        _vector = vector;
        return this;
    }

    /// <summary>
    /// Sets a random vector embedding of the specified dimensions.
    /// </summary>
    /// <param name="dimensions">The vector dimensions (default: 1024).</param>
    /// <returns>The builder instance for chaining.</returns>
    public TestExternalDocumentBuilder WithRandomVector(int dimensions = 1024)
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
    /// Builds the ExternalDocument instance.
    /// </summary>
    /// <returns>A new ExternalDocument instance with the configured values.</returns>
    public ExternalDocument Build()
    {
        return new ExternalDocument
        {
            Id = _id,
            TenantKey = _tenantKey,
            Title = _title,
            Content = _content,
            RelativePath = _relativePath,
            SourceUrl = _sourceUrl,
            LastSyncedAt = _lastSyncedAt,
            NamespacePrefix = _namespacePrefix,
            ContentHash = _contentHash,
            CharCount = _charCount,
            Vector = _vector
        };
    }

    /// <summary>
    /// Builds multiple documents with sequential IDs and paths.
    /// </summary>
    /// <param name="count">The number of documents to build.</param>
    /// <returns>A list of ExternalDocument instances.</returns>
    public IReadOnlyList<ExternalDocument> BuildMany(int count)
    {
        var documents = new List<ExternalDocument>(count);
        for (int i = 0; i < count; i++)
        {
            documents.Add(new ExternalDocument
            {
                Id = $"{_id}-{i}",
                TenantKey = _tenantKey,
                Title = $"{_title} {i + 1}",
                Content = $"{_content}\n\nDocument {i + 1}",
                RelativePath = $"{Path.GetDirectoryName(_relativePath)}/{Path.GetFileNameWithoutExtension(_relativePath)}-{i + 1}{Path.GetExtension(_relativePath)}",
                SourceUrl = _sourceUrl != null ? $"{_sourceUrl}?version={i + 1}" : null,
                LastSyncedAt = _lastSyncedAt?.AddMinutes(i),
                NamespacePrefix = _namespacePrefix,
                ContentHash = $"{_contentHash}-{i}",
                CharCount = _charCount + (i * 10),
                Vector = _vector
            });
        }
        return documents;
    }

    /// <summary>
    /// Creates a new builder with default test values.
    /// </summary>
    /// <returns>A new TestExternalDocumentBuilder instance.</returns>
    public static TestExternalDocumentBuilder Create() => new();

    /// <summary>
    /// Creates a builder for an API documentation source.
    /// </summary>
    /// <returns>A new TestExternalDocumentBuilder configured for API docs.</returns>
    public static TestExternalDocumentBuilder CreateApiDoc()
    {
        return new TestExternalDocumentBuilder()
            .WithTitle("API Reference")
            .WithContent("# API Reference\n\n## Authentication\n\nUse Bearer tokens.\n\n## Endpoints\n\n### GET /api/v1/users")
            .WithRelativePath("api/reference.md")
            .WithSourceUrl("https://api.example.com/docs/reference.md")
            .WithNamespacePrefix("api-docs");
    }

    /// <summary>
    /// Creates a builder for a framework documentation source.
    /// </summary>
    /// <returns>A new TestExternalDocumentBuilder configured for framework docs.</returns>
    public static TestExternalDocumentBuilder CreateFrameworkDoc()
    {
        return new TestExternalDocumentBuilder()
            .WithTitle("Framework Guide")
            .WithContent("# Framework Guide\n\n## Getting Started\n\nInstall the framework via NuGet.\n\n## Configuration\n\nConfigure services in Startup.cs.")
            .WithRelativePath("framework/getting-started.md")
            .WithSourceUrl("https://framework.example.com/docs/guide.md")
            .WithNamespacePrefix("framework-docs");
    }
}
