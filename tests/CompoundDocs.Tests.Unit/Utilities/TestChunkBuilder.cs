using CompoundDocs.McpServer.Models;

namespace CompoundDocs.Tests.Unit.Utilities;

/// <summary>
/// Fluent builder for creating test DocumentChunk instances.
/// </summary>
public sealed class TestChunkBuilder
{
    private string _id = Guid.NewGuid().ToString();
    private string _documentId = Guid.NewGuid().ToString();
    private string _headerPath = "## Overview";
    private int _startLine = 1;
    private int _endLine = 50;
    private string _content = "This is the content of the test chunk. It contains relevant information about the topic.";

    public TestChunkBuilder WithId(string id) { _id = id; return this; }
    public TestChunkBuilder WithDocumentId(string documentId) { _documentId = documentId; return this; }

    public TestChunkBuilder WithParentDocument(CompoundDocument document)
    {
        _documentId = document.Id;
        return this;
    }

    public TestChunkBuilder WithHeaderPath(string headerPath) { _headerPath = headerPath; return this; }
    public TestChunkBuilder WithStartLine(int startLine) { _startLine = startLine; return this; }
    public TestChunkBuilder WithEndLine(int endLine) { _endLine = endLine; return this; }

    public TestChunkBuilder WithLineRange(int startLine, int endLine)
    {
        _startLine = startLine;
        _endLine = endLine;
        return this;
    }

    public TestChunkBuilder WithContent(string content) { _content = content; return this; }

    public DocumentChunk Build()
    {
        return new DocumentChunk
        {
            Id = _id,
            DocumentId = _documentId,
            HeaderPath = _headerPath,
            StartLine = _startLine,
            EndLine = _endLine,
            Content = _content
        };
    }

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
                HeaderPath = $"{_headerPath} > Section {i + 1}",
                StartLine = startLine,
                EndLine = endLine,
                Content = $"{_content}\n\nSection {i + 1} content."
            });
        }
        return chunks;
    }

    public static IReadOnlyList<DocumentChunk> CreateFromDocument(CompoundDocument document, int chunkCount = 3)
    {
        return new TestChunkBuilder()
            .WithParentDocument(document)
            .BuildMany(chunkCount);
    }

    public static TestChunkBuilder Create() => new();
}
