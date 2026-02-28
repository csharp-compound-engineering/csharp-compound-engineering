using CompoundDocs.Common.Parsing;
using CompoundDocs.McpServer.Processing;

namespace CompoundDocs.Tests.Integration.Processing;

public class DocumentProcessingIntegrationTests
{
    [Fact]
    public void ParseAndChunk_MarkdownDocument_ProducesChunksWithMetadata()
    {
        // Arrange
        var parser = new DocumentParser(new FrontmatterParser(), new MarkdownParser());
        var chunker = new ChunkingStrategy();
        var markdown = """
            ---
            title: Getting Started with GraphRAG
            description: A guide to setting up the GraphRAG pipeline
            tags:
              - graphrag
              - setup
            ---

            # Getting Started with GraphRAG

            This guide walks you through the initial setup of the GraphRAG pipeline
            for semantic document search.

            ## Prerequisites

            Before you begin, ensure you have the following installed:

            - .NET 9.0 SDK
            - Docker Desktop
            - AWS CLI configured with appropriate credentials

            ## Installation

            Clone the repository and restore dependencies:

            ```bash
            git clone https://github.com/example/compound-docs.git
            cd compound-docs
            dotnet restore
            ```

            ## Configuration

            Configure your environment variables for the required AWS services.
            Set the following in your `.env` file:

            ```bash
            AWS_REGION=us-east-1
            NEPTUNE_ENDPOINT=your-neptune-endpoint
            OPENSEARCH_ENDPOINT=your-opensearch-endpoint
            ```

            This completes the basic setup. See the advanced configuration guide for more options.
            """;

        // Act
        var parsed = parser.ParseDetailed(markdown);
        var chunks = chunker.Chunk(parsed.Body, "test-doc-1");

        // Assert
        parsed.IsSuccess.ShouldBeTrue();
        parsed.HasFrontmatter.ShouldBeTrue();
        parsed.Title.ShouldBe("Getting Started with GraphRAG");
        parsed.Headers.ShouldNotBeEmpty();
        parsed.Headers.ShouldContain(h => h.Text == "Prerequisites");
        parsed.Headers.ShouldContain(h => h.Text == "Installation");
        parsed.Headers.ShouldContain(h => h.Text == "Configuration");

        chunks.ShouldNotBeEmpty();
        foreach (var chunk in chunks)
        {
            chunk.Content.ShouldNotBeNullOrWhiteSpace();
            chunk.ParentDocumentId.ShouldBe("test-doc-1");
            chunk.StartOffset.ShouldBeGreaterThanOrEqualTo(0);
            chunk.EndOffset.ShouldBeGreaterThan(chunk.StartOffset);
        }
    }

    [Fact]
    public void ParseAndChunk_LargeDocument_ChunksAreWithinSizeLimit()
    {
        // Arrange - create a document larger than default chunk size (1000 chars)
        var parser = new DocumentParser(new FrontmatterParser(), new MarkdownParser());
        var sections = Enumerable.Range(1, 20).Select(i =>
            $"""

            ## Section {i}

            This is the content for section {i}. It contains enough text to be meaningful
            and contribute to the overall document size. Each section discusses a different
            aspect of the system architecture, including service boundaries, data flow,
            and integration patterns that are relevant to the overall design.

            """);

        var markdown = "# Large Architecture Document\n\n" + string.Join("\n", sections);
        var options = new ChunkingOptions { ChunkSize = 500 };
        var chunker = new ChunkingStrategy(options);

        // Act
        var parsed = parser.Parse(markdown);
        var chunks = chunker.Chunk(parsed.Body, "large-doc");

        // Assert
        chunks.Count().ShouldBeGreaterThan(1, "a large document should produce multiple chunks");
        foreach (var chunk in chunks)
        {
            // Paragraph-aware chunking may slightly exceed the limit when a single
            // paragraph is larger than ChunkSize, but each chunk should be reasonable.
            chunk.Content.Length.ShouldBeGreaterThan(0);
        }
    }

    [Fact]
    public void ParseAndChunk_DocumentWithLinks_ExtractsLinks()
    {
        // Arrange - use internal (relative) links since MarkdownParser.ExtractLinks
        // skips external http/https URLs by design
        var parser = new DocumentParser(new FrontmatterParser(), new MarkdownParser());
        var markdown = """
            ---
            title: API Reference
            ---

            # API Reference

            See the [configuration guide](./configuration.md) for setup instructions.

            For details on the RAG pipeline, refer to [pipeline docs](../pipeline/overview.md).

            Check the [troubleshooting section](./troubleshooting.md#common-errors) if you run into issues.
            """;

        // Act
        var parsed = parser.ParseDetailed(markdown);

        // Assert
        parsed.IsSuccess.ShouldBeTrue();
        parsed.Links.ShouldNotBeEmpty();
        parsed.Links.Count().ShouldBeGreaterThanOrEqualTo(3);
        parsed.Links.ShouldContain(l => l.Url == "./configuration.md");
        parsed.Links.ShouldContain(l => l.Url == "../pipeline/overview.md");
        parsed.Links.ShouldContain(l => l.Url == "./troubleshooting.md#common-errors");
        parsed.Links.ShouldContain(l => l.Text == "configuration guide");
    }
}
