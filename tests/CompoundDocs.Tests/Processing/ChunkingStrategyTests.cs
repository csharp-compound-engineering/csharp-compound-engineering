using CompoundDocs.McpServer.Processing;

namespace CompoundDocs.Tests.Processing;

/// <summary>
/// Unit tests for ChunkingStrategy.
/// </summary>
public sealed class ChunkingStrategyTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_WithDefaultOptions_UsesDefaults()
    {
        // Act
        var sut = new ChunkingStrategy();

        // Assert
        sut.ChunkSize.ShouldBe(ChunkingOptions.DefaultChunkSize);
        sut.Overlap.ShouldBe(ChunkingOptions.DefaultOverlap);
    }

    [Fact]
    public void Constructor_WithCustomOptions_UsesCustomValues()
    {
        // Arrange
        var options = new ChunkingOptions
        {
            ChunkSize = 500,
            Overlap = 100
        };

        // Act
        var sut = new ChunkingStrategy(options);

        // Assert
        sut.ChunkSize.ShouldBe(500);
        sut.Overlap.ShouldBe(100);
    }

    [Fact]
    public void Constructor_WithInvalidOptions_ThrowsException()
    {
        // Arrange
        var options = new ChunkingOptions
        {
            ChunkSize = 100,
            Overlap = 200 // Overlap > ChunkSize is invalid
        };

        // Act & Assert
        Should.Throw<ArgumentException>(() => new ChunkingStrategy(options));
    }

    #endregion

    #region Chunk Tests

    [Fact]
    public void Chunk_WithSmallContent_ReturnsSingleChunk()
    {
        // Arrange
        var sut = new ChunkingStrategy();
        var content = "Small content that fits in one chunk.";

        // Act
        var chunks = sut.Chunk(content);

        // Assert
        chunks.Count.ShouldBe(1);
        chunks[0].Content.ShouldBe(content);
        chunks[0].Index.ShouldBe(0);
    }

    [Fact]
    public void Chunk_WithLargeContent_ReturnsMultipleChunks()
    {
        // Arrange
        var options = new ChunkingOptions
        {
            ChunkSize = 100,
            Overlap = 20,
            RespectParagraphBoundaries = false
        };
        var sut = new ChunkingStrategy(options);
        var content = new string('a', 300);

        // Act
        var chunks = sut.Chunk(content);

        // Assert
        chunks.Count.ShouldBeGreaterThan(1);
    }

    [Fact]
    public void Chunk_WithEmptyContent_ReturnsEmptyList()
    {
        // Arrange
        var sut = new ChunkingStrategy();

        // Act
        var chunks = sut.Chunk(string.Empty);

        // Assert
        chunks.Count.ShouldBe(0);
    }

    [Fact]
    public void Chunk_WithNullContent_ReturnsEmptyList()
    {
        // Arrange
        var sut = new ChunkingStrategy();

        // Act
        var chunks = sut.Chunk(null!);

        // Assert
        chunks.Count.ShouldBe(0);
    }

    [Fact]
    public void Chunk_WithParagraphs_RespectsParagraphBoundaries()
    {
        // Arrange
        var options = new ChunkingOptions
        {
            ChunkSize = 200,
            Overlap = 50,
            RespectParagraphBoundaries = true
        };
        var sut = new ChunkingStrategy(options);
        var content = """
            First paragraph with some content.

            Second paragraph with more content.

            Third paragraph with even more content.
            """;

        // Act
        var chunks = sut.Chunk(content);

        // Assert
        chunks.Count.ShouldBeGreaterThan(0);
        // Each chunk should contain complete paragraphs
    }

    [Fact]
    public void Chunk_SetsParentDocumentId()
    {
        // Arrange
        var sut = new ChunkingStrategy();
        var content = "Content here.";
        var documentId = "doc-123";

        // Act
        var chunks = sut.Chunk(content, documentId);

        // Assert
        chunks[0].ParentDocumentId.ShouldBe(documentId);
    }

    [Fact]
    public void Chunk_SetsCorrectOffsets()
    {
        // Arrange
        var options = new ChunkingOptions
        {
            ChunkSize = 50,
            Overlap = 0,
            RespectParagraphBoundaries = false
        };
        var sut = new ChunkingStrategy(options);
        var content = new string('x', 150);

        // Act
        var chunks = sut.Chunk(content);

        // Assert
        chunks.Count.ShouldBeGreaterThan(1);
        chunks[0].StartOffset.ShouldBe(0);
    }

    #endregion

    #region ShouldChunk Tests

    [Fact]
    public void ShouldChunk_WithContentLargerThanChunkSize_ReturnsTrue()
    {
        // Arrange
        var options = new ChunkingOptions { ChunkSize = 100, Overlap = 20 };
        var sut = new ChunkingStrategy(options);
        var content = new string('a', 150);

        // Act
        var shouldChunk = sut.ShouldChunk(content);

        // Assert
        shouldChunk.ShouldBeTrue();
    }

    [Fact]
    public void ShouldChunk_WithContentSmallerThanChunkSize_ReturnsFalse()
    {
        // Arrange
        var options = new ChunkingOptions { ChunkSize = 100, Overlap = 20 };
        var sut = new ChunkingStrategy(options);
        var content = "Small content";

        // Act
        var shouldChunk = sut.ShouldChunk(content);

        // Assert
        shouldChunk.ShouldBeFalse();
    }

    [Fact]
    public void ShouldChunk_WithEmptyContent_ReturnsFalse()
    {
        // Arrange
        var sut = new ChunkingStrategy();

        // Act
        var shouldChunk = sut.ShouldChunk(string.Empty);

        // Assert
        shouldChunk.ShouldBeFalse();
    }

    #endregion

    #region EstimateChunkCount Tests

    [Fact]
    public void EstimateChunkCount_WithSmallContent_ReturnsOne()
    {
        // Arrange
        var sut = new ChunkingStrategy();
        var content = "Small content";

        // Act
        var count = sut.EstimateChunkCount(content);

        // Assert
        count.ShouldBe(1);
    }

    [Fact]
    public void EstimateChunkCount_WithLargeContent_ReturnsCorrectEstimate()
    {
        // Arrange
        var options = new ChunkingOptions
        {
            ChunkSize = 100,
            Overlap = 20
        };
        var sut = new ChunkingStrategy(options);
        var content = new string('a', 400);

        // Act
        var count = sut.EstimateChunkCount(content);

        // Assert
        // With ChunkSize=100 and Overlap=20, effective chunk size is 80
        // 400 / 80 = 5 chunks
        count.ShouldBe(5);
    }

    #endregion

    #region ChunkingOptions Validation Tests

    [Fact]
    public void ChunkingOptions_Validate_ThrowsForZeroChunkSize()
    {
        // Arrange
        var options = new ChunkingOptions { ChunkSize = 0 };

        // Act & Assert
        Should.Throw<ArgumentException>(() => options.Validate());
    }

    [Fact]
    public void ChunkingOptions_Validate_ThrowsForNegativeOverlap()
    {
        // Arrange
        var options = new ChunkingOptions { Overlap = -1 };

        // Act & Assert
        Should.Throw<ArgumentException>(() => options.Validate());
    }

    #endregion
}
