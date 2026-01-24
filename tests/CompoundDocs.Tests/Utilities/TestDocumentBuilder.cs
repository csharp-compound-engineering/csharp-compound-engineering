using CompoundDocs.McpServer.Models;

namespace CompoundDocs.Tests.Utilities;

/// <summary>
/// Fluent builder for creating test CompoundDocument instances.
/// </summary>
public sealed class TestDocumentBuilder
{
    private string _id = Guid.NewGuid().ToString();
    private string _title = "Test Document";
    private string _content = "# Test Document\n\nThis is test content for the document.";
    private string? _docType = "doc";
    private string _filePath = "docs/test-document.md";
    private DateTimeOffset _lastModified = DateTimeOffset.UtcNow;
    private string? _links;

    public TestDocumentBuilder WithId(string id) { _id = id; return this; }
    public TestDocumentBuilder WithTitle(string title) { _title = title; return this; }
    public TestDocumentBuilder WithContent(string content) { _content = content; return this; }
    public TestDocumentBuilder WithDocType(string? docType) { _docType = docType; return this; }
    public TestDocumentBuilder WithFilePath(string filePath) { _filePath = filePath; return this; }
    public TestDocumentBuilder WithLastModified(DateTimeOffset lastModified) { _lastModified = lastModified; return this; }
    public TestDocumentBuilder WithLinks(string links) { _links = links; return this; }

    public TestDocumentBuilder WithLinks(params string[] linkPaths)
    {
        _links = System.Text.Json.JsonSerializer.Serialize(linkPaths);
        return this;
    }

    public CompoundDocument Build()
    {
        return new CompoundDocument
        {
            Id = _id,
            Title = _title,
            Content = _content,
            DocType = _docType,
            FilePath = _filePath,
            LastModified = _lastModified,
            Links = _links
        };
    }

    public IReadOnlyList<CompoundDocument> BuildMany(int count)
    {
        var documents = new List<CompoundDocument>(count);
        for (int i = 0; i < count; i++)
        {
            documents.Add(new CompoundDocument
            {
                Id = $"{_id}-{i}",
                Title = $"{_title} {i + 1}",
                Content = $"{_content}\n\nDocument {i + 1}",
                DocType = _docType,
                FilePath = $"{Path.GetDirectoryName(_filePath)}/{Path.GetFileNameWithoutExtension(_filePath)}-{i + 1}{Path.GetExtension(_filePath)}",
                LastModified = _lastModified.AddMinutes(i),
                Links = _links
            });
        }
        return documents;
    }

    public static TestDocumentBuilder Create() => new();
}
