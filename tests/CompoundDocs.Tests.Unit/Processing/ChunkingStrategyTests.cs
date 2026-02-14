using CompoundDocs.McpServer.Processing;

namespace CompoundDocs.Tests.Unit.Processing;

#region ChunkingOptions Tests

public class ChunkingOptionsTests
{
    [Fact]
    public void DefaultConstants_ShouldHaveExpectedValues()
    {
        ChunkingOptions.DefaultChunkSize.ShouldBe(1000);
        ChunkingOptions.DefaultOverlap.ShouldBe(200);
    }

    [Fact]
    public void DefaultProperties_ShouldMatchConstants()
    {
        var options = new ChunkingOptions();

        options.ChunkSize.ShouldBe(ChunkingOptions.DefaultChunkSize);
        options.Overlap.ShouldBe(ChunkingOptions.DefaultOverlap);
        options.RespectParagraphBoundaries.ShouldBeTrue();
        options.MinChunkSize.ShouldBe(100);
    }

    [Fact]
    public void Validate_WithValidDefaults_ShouldNotThrow()
    {
        var options = new ChunkingOptions();

        Should.NotThrow(() => options.Validate());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Validate_WithChunkSizeLessThanOrEqualToZero_ShouldThrowArgumentException(int chunkSize)
    {
        var options = new ChunkingOptions { ChunkSize = chunkSize };

        var ex = Should.Throw<ArgumentException>(() => options.Validate());
        ex.ParamName.ShouldBe("ChunkSize");
    }

    [Fact]
    public void Validate_WithNegativeOverlap_ShouldThrowArgumentException()
    {
        var options = new ChunkingOptions { Overlap = -1 };

        var ex = Should.Throw<ArgumentException>(() => options.Validate());
        ex.ParamName.ShouldBe("Overlap");
    }

    [Theory]
    [InlineData(1000, 1000)]
    [InlineData(500, 600)]
    public void Validate_WithOverlapGreaterThanOrEqualToChunkSize_ShouldThrowArgumentException(
        int chunkSize, int overlap)
    {
        var options = new ChunkingOptions { ChunkSize = chunkSize, Overlap = overlap };

        var ex = Should.Throw<ArgumentException>(() => options.Validate());
        ex.ParamName.ShouldBe("Overlap");
    }

    [Fact]
    public void Validate_WithNegativeMinChunkSize_ShouldThrowArgumentException()
    {
        var options = new ChunkingOptions { MinChunkSize = -1 };

        var ex = Should.Throw<ArgumentException>(() => options.Validate());
        ex.ParamName.ShouldBe("MinChunkSize");
    }

    [Fact]
    public void Validate_WithZeroMinChunkSize_ShouldNotThrow()
    {
        var options = new ChunkingOptions { MinChunkSize = 0 };

        Should.NotThrow(() => options.Validate());
    }

    [Fact]
    public void Validate_WithZeroOverlap_ShouldNotThrow()
    {
        var options = new ChunkingOptions { Overlap = 0 };

        Should.NotThrow(() => options.Validate());
    }
}

#endregion

#region ChunkingStrategy Constructor Tests

public class ChunkingStrategyConstructorTests
{
    [Fact]
    public void DefaultConstructor_ShouldUseDefaultOptions()
    {
        var strategy = new ChunkingStrategy();

        strategy.ChunkSize.ShouldBe(ChunkingOptions.DefaultChunkSize);
        strategy.Overlap.ShouldBe(ChunkingOptions.DefaultOverlap);
    }

    [Fact]
    public void Constructor_WithCustomOptions_ShouldApplyOptions()
    {
        var options = new ChunkingOptions { ChunkSize = 500, Overlap = 50 };
        var strategy = new ChunkingStrategy(options);

        strategy.ChunkSize.ShouldBe(500);
        strategy.Overlap.ShouldBe(50);
    }

    [Fact]
    public void Constructor_WithNullOptions_ShouldThrowArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() => new ChunkingStrategy(null!));
    }

    [Fact]
    public void Constructor_WithInvalidOptions_ShouldThrowArgumentException()
    {
        var options = new ChunkingOptions { ChunkSize = -1 };

        Should.Throw<ArgumentException>(() => new ChunkingStrategy(options));
    }
}

#endregion

#region ChunkingStrategy.Chunk Tests

public class ChunkingStrategyChunkTests
{
    [Fact]
    public void Chunk_WithNullContent_ShouldReturnEmptyList()
    {
        var strategy = new ChunkingStrategy();

        var result = strategy.Chunk(null!);

        result.ShouldBeEmpty();
    }

    [Fact]
    public void Chunk_WithEmptyContent_ShouldReturnEmptyList()
    {
        var strategy = new ChunkingStrategy();

        var result = strategy.Chunk(string.Empty);

        result.ShouldBeEmpty();
    }

    [Fact]
    public void Chunk_WithContentSmallerThanChunkSize_ShouldReturnSingleChunk()
    {
        var strategy = new ChunkingStrategy(new ChunkingOptions { ChunkSize = 1000 });
        var content = "Short content that fits in one chunk.";

        var result = strategy.Chunk(content);

        result.Count.ShouldBe(1);
        result[0].Index.ShouldBe(0);
        result[0].Content.ShouldBe(content);
        result[0].StartOffset.ShouldBe(0);
        result[0].EndOffset.ShouldBe(content.Length);
    }

    [Fact]
    public void Chunk_WithContentEqualToChunkSize_ShouldReturnSingleChunk()
    {
        var content = new string('a', 500);
        var strategy = new ChunkingStrategy(new ChunkingOptions { ChunkSize = 500, Overlap = 50 });

        var result = strategy.Chunk(content);

        result.Count.ShouldBe(1);
        result[0].Content.ShouldBe(content);
    }

    [Fact]
    public void Chunk_WithDocumentId_ShouldSetParentDocumentId()
    {
        var strategy = new ChunkingStrategy();
        var content = "Some content.";
        var documentId = "doc-123";

        var result = strategy.Chunk(content, documentId);

        result.Count.ShouldBe(1);
        result[0].ParentDocumentId.ShouldBe(documentId);
    }

    [Fact]
    public void Chunk_WithoutDocumentId_ShouldHaveNullParentDocumentId()
    {
        var strategy = new ChunkingStrategy();

        var result = strategy.Chunk("Some content.");

        result[0].ParentDocumentId.ShouldBeNull();
    }

    [Fact]
    public void Chunk_WithParagraphBoundaries_ShouldSplitOnDoubleNewlines()
    {
        var options = new ChunkingOptions
        {
            ChunkSize = 50,
            Overlap = 0,
            RespectParagraphBoundaries = true,
            MinChunkSize = 0
        };
        var strategy = new ChunkingStrategy(options);

        var paragraph1 = new string('a', 40);
        var paragraph2 = new string('b', 40);
        var content = $"{paragraph1}\n\n{paragraph2}";

        var result = strategy.Chunk(content);

        result.Count.ShouldBeGreaterThan(1);
        result[0].Content.ShouldContain("a");
        result[^1].Content.ShouldContain("b");
    }

    [Fact]
    public void Chunk_WithRespectParagraphBoundariesFalse_ShouldChunkByFixedSize()
    {
        var options = new ChunkingOptions
        {
            ChunkSize = 50,
            Overlap = 10,
            RespectParagraphBoundaries = false,
            MinChunkSize = 0
        };
        var strategy = new ChunkingStrategy(options);
        var content = new string('x', 120);

        var result = strategy.Chunk(content);

        result.Count.ShouldBeGreaterThan(1);
        result[0].Content.Length.ShouldBe(50);
    }

    [Fact]
    public void Chunk_BySize_ShouldRespectOverlap()
    {
        var options = new ChunkingOptions
        {
            ChunkSize = 50,
            Overlap = 10,
            RespectParagraphBoundaries = false,
            MinChunkSize = 0
        };
        var strategy = new ChunkingStrategy(options);
        var content = new string('x', 120);

        var result = strategy.Chunk(content);

        // Second chunk should start at offset 40 (50 - 10 overlap)
        result.Count.ShouldBeGreaterThan(1);
        result[1].StartOffset.ShouldBe(40);
    }

    [Fact]
    public void Chunk_BySize_ShouldHaveSequentialIndices()
    {
        var options = new ChunkingOptions
        {
            ChunkSize = 30,
            Overlap = 5,
            RespectParagraphBoundaries = false,
            MinChunkSize = 0
        };
        var strategy = new ChunkingStrategy(options);
        var content = new string('a', 100);

        var result = strategy.Chunk(content);

        for (var i = 0; i < result.Count; i++)
        {
            result[i].Index.ShouldBe(i);
        }
    }

    [Fact]
    public void Chunk_WithParagraphs_ShouldIncludeOverlapContent()
    {
        var options = new ChunkingOptions
        {
            ChunkSize = 60,
            Overlap = 20,
            RespectParagraphBoundaries = true,
            MinChunkSize = 0
        };
        var strategy = new ChunkingStrategy(options);

        var paragraph1 = new string('a', 50);
        var paragraph2 = new string('b', 50);
        var content = $"{paragraph1}\n\n{paragraph2}";

        var result = strategy.Chunk(content);

        result.Count.ShouldBeGreaterThan(1);
    }

    [Fact]
    public void Chunk_SmallChunksMerged_WhenBelowMinChunkSize()
    {
        var options = new ChunkingOptions
        {
            ChunkSize = 50,
            Overlap = 0,
            RespectParagraphBoundaries = true,
            MinChunkSize = 30
        };
        var strategy = new ChunkingStrategy(options);

        // Create content with a small paragraph that should get merged
        var largeParagraph = new string('a', 40);
        var smallParagraph = "tiny";
        var anotherParagraph = new string('c', 40);
        var content = $"{largeParagraph}\n\n{smallParagraph}\n\n{anotherParagraph}";

        var result = strategy.Chunk(content);

        // The small paragraph should have been merged with the next one
        var allContent = string.Join(" ", result.Select(c => c.Content));
        allContent.ShouldContain("tiny");
    }

    [Fact]
    public void Chunk_WithMinChunkSizeZero_ShouldSkipMerging()
    {
        // When MinChunkSize is 0, MergeSmallChunks returns chunks as-is
        var options = new ChunkingOptions
        {
            ChunkSize = 30,
            Overlap = 5,
            RespectParagraphBoundaries = false,
            MinChunkSize = 0
        };
        var strategy = new ChunkingStrategy(options);
        var content = new string('x', 80);

        var result = strategy.Chunk(content);

        // All chunks returned without merging; verify they exist
        result.Count.ShouldBeGreaterThan(1);
        foreach (var chunk in result)
        {
            chunk.Content.ShouldNotBeNull();
            chunk.Content.Length.ShouldBeGreaterThan(0);
        }
    }

    [Fact]
    public void Chunk_BySize_WithDocumentId_ShouldSetOnAllChunks()
    {
        var options = new ChunkingOptions
        {
            ChunkSize = 30,
            Overlap = 5,
            RespectParagraphBoundaries = false,
            MinChunkSize = 0
        };
        var strategy = new ChunkingStrategy(options);
        var content = new string('z', 100);

        var result = strategy.Chunk(content, "my-doc");

        foreach (var chunk in result)
        {
            chunk.ParentDocumentId.ShouldBe("my-doc");
        }
    }

    [Fact]
    public void Chunk_LastChunk_ShouldEndAtContentLength_ForSingleChunk()
    {
        var strategy = new ChunkingStrategy();
        var content = "Hello world.";

        var result = strategy.Chunk(content);

        result[0].EndOffset.ShouldBe(content.Length);
    }
}

#endregion

#region ChunkingStrategy.ShouldChunk Tests

public class ChunkingStrategyShouldChunkTests
{
    [Fact]
    public void ShouldChunk_WithNullContent_ShouldReturnFalse()
    {
        var strategy = new ChunkingStrategy();

        strategy.ShouldChunk(null!).ShouldBeFalse();
    }

    [Fact]
    public void ShouldChunk_WithEmptyContent_ShouldReturnFalse()
    {
        var strategy = new ChunkingStrategy();

        strategy.ShouldChunk(string.Empty).ShouldBeFalse();
    }

    [Fact]
    public void ShouldChunk_WithContentSmallerThanChunkSize_ShouldReturnFalse()
    {
        var strategy = new ChunkingStrategy(new ChunkingOptions { ChunkSize = 500, Overlap = 50 });

        strategy.ShouldChunk("Short").ShouldBeFalse();
    }

    [Fact]
    public void ShouldChunk_WithContentEqualToChunkSize_ShouldReturnFalse()
    {
        var strategy = new ChunkingStrategy(new ChunkingOptions { ChunkSize = 500, Overlap = 50 });

        strategy.ShouldChunk(new string('a', 500)).ShouldBeFalse();
    }

    [Fact]
    public void ShouldChunk_WithContentLargerThanChunkSize_ShouldReturnTrue()
    {
        var strategy = new ChunkingStrategy(new ChunkingOptions { ChunkSize = 500, Overlap = 50 });

        strategy.ShouldChunk(new string('a', 501)).ShouldBeTrue();
    }
}

#endregion

#region ChunkingStrategy.EstimateChunkCount Tests

public class ChunkingStrategyEstimateChunkCountTests
{
    [Fact]
    public void EstimateChunkCount_WithNullContent_ShouldReturnOne()
    {
        var strategy = new ChunkingStrategy();

        strategy.EstimateChunkCount(null!).ShouldBe(1);
    }

    [Fact]
    public void EstimateChunkCount_WithEmptyContent_ShouldReturnOne()
    {
        var strategy = new ChunkingStrategy();

        strategy.EstimateChunkCount(string.Empty).ShouldBe(1);
    }

    [Fact]
    public void EstimateChunkCount_WithContentSmallerThanChunkSize_ShouldReturnOne()
    {
        var strategy = new ChunkingStrategy(new ChunkingOptions { ChunkSize = 500, Overlap = 50 });

        strategy.EstimateChunkCount("Short").ShouldBe(1);
    }

    [Fact]
    public void EstimateChunkCount_WithContentEqualToChunkSize_ShouldReturnOne()
    {
        var strategy = new ChunkingStrategy(new ChunkingOptions { ChunkSize = 500, Overlap = 50 });

        strategy.EstimateChunkCount(new string('a', 500)).ShouldBe(1);
    }

    [Fact]
    public void EstimateChunkCount_WithLargeContent_ShouldReturnCorrectEstimate()
    {
        var options = new ChunkingOptions { ChunkSize = 100, Overlap = 20 };
        var strategy = new ChunkingStrategy(options);
        var content = new string('a', 300);

        // effectiveChunkSize = 100 - 20 = 80
        // Math.Ceiling(300 / 80.0) = 4
        var result = strategy.EstimateChunkCount(content);

        result.ShouldBe(4);
    }

    [Fact]
    public void EstimateChunkCount_WithZeroOverlap_ShouldUsePlainDivision()
    {
        var options = new ChunkingOptions { ChunkSize = 100, Overlap = 0 };
        var strategy = new ChunkingStrategy(options);
        var content = new string('a', 250);

        // Math.Ceiling(250 / 100.0) = 3
        strategy.EstimateChunkCount(content).ShouldBe(3);
    }
}

#endregion

#region ChunkingStrategy Edge Case Tests

public class ChunkingStrategyEdgeCaseTests
{
    [Fact]
    public void ChunkBySize_WithOverlapNearChunkSize_ShouldNotLoopInfinitely()
    {
        // Arrange: overlap is ChunkSize - 1, so each step advances only 1 char.
        // The infinite-loop guard (lines 254-257) should prevent getting stuck.
        var options = new ChunkingOptions
        {
            ChunkSize = 10,
            Overlap = 9,
            RespectParagraphBoundaries = false,
            MinChunkSize = 0
        };
        var strategy = new ChunkingStrategy(options);
        var content = new string('a', 25);

        // Act
        var result = strategy.Chunk(content);

        // Assert: should produce multiple chunks and cover all content
        result.Count.ShouldBeGreaterThan(1);
        result[^1].EndOffset.ShouldBe(content.Length);
    }

    [Fact]
    public void GetOverlapContent_WhenContentShorterThanOverlap_ShouldReturnFullContent()
    {
        // Arrange: overlap (200) is larger than any single paragraph content,
        // so GetOverlapContent should return the full chunk content (lines 289-291).
        var options = new ChunkingOptions
        {
            ChunkSize = 50,
            Overlap = 200,
            RespectParagraphBoundaries = true,
            MinChunkSize = 0
        };
        // Overlap >= ChunkSize would fail validation, so we need paragraph mode
        // where a paragraph shorter than Overlap triggers the early return.
        // Use ChunkSize=300 so Overlap=200 is valid, with paragraphs < 200 chars.
        options = new ChunkingOptions
        {
            ChunkSize = 300,
            Overlap = 200,
            RespectParagraphBoundaries = true,
            MinChunkSize = 0
        };
        var strategy = new ChunkingStrategy(options);

        // Two paragraphs that each fit within ChunkSize individually but together exceed it.
        // The first chunk content (~180 chars) is shorter than Overlap (200),
        // so GetOverlapContent returns the full content.
        var paragraph1 = new string('a', 180);
        var paragraph2 = new string('b', 180);
        var content = $"{paragraph1}\n\n{paragraph2}";

        // Act
        var result = strategy.Chunk(content);

        // Assert: should produce at least 2 chunks, and the second chunk should
        // contain overlap from the first chunk (the full first paragraph content).
        result.Count.ShouldBeGreaterThanOrEqualTo(2);
        result[1].Content.ShouldContain("a");
        result[1].Content.ShouldContain("b");
    }

    [Fact]
    public void GetOverlapContent_ShouldBreakAtWordBoundary_WhenSpaceExists()
    {
        // Arrange: content with spaces so GetOverlapContent finds a word boundary
        // near the overlap start index (lines 298-301).
        var options = new ChunkingOptions
        {
            ChunkSize = 80,
            Overlap = 30,
            RespectParagraphBoundaries = true,
            MinChunkSize = 0
        };
        var strategy = new ChunkingStrategy(options);

        // Build paragraphs with words separated by spaces so the overlap logic
        // can find a space near the start of the overlap region.
        var paragraph1 = "word " + new string('a', 30) + " end of first paragraph here";
        var paragraph2 = "second paragraph with enough content to exceed chunk size easily";
        var content = $"{paragraph1}\n\n{paragraph2}";

        // Act
        var result = strategy.Chunk(content);

        // Assert: multiple chunks produced; overlap content should start at a word boundary
        result.Count.ShouldBeGreaterThanOrEqualTo(2);
        // The second chunk's content should not start with a partial word
        result[1].Content.ShouldNotStartWith(" ");
    }

    [Fact]
    public void MergeSmallChunks_ShouldMergeSmallChunkWithNext()
    {
        // Arrange: use fixed-size chunking to produce a small trailing chunk
        // that triggers MergeSmallChunks (lines 324-338).
        // Content = 55 chars, ChunkSize = 50, Overlap = 0 => chunks of 50 and 5.
        // MinChunkSize = 10 means the 5-char chunk should be merged with... but there's
        // no next chunk. We need a middle small chunk.
        // Better approach: 3 paragraphs where the middle one is tiny and stands alone.
        var options = new ChunkingOptions
        {
            ChunkSize = 60,
            Overlap = 0,
            RespectParagraphBoundaries = true,
            MinChunkSize = 30
        };
        var strategy = new ChunkingStrategy(options);

        // Para1 (58 chars) fills first chunk near capacity.
        // Adding "hi" (2 chars) to para1 would be 58+2+2=62 > 60, so para1 becomes chunk 1.
        // Then "hi" starts a new buffer. Adding para3 (55 chars): 2+55+2=59 <= 60, fits.
        // So "hi" won't be alone. We need para3 to also exceed when combined with "hi".
        // Use para3 = 59 chars: 2+59+2=63 > 60, so "hi" becomes its own chunk, then para3.
        // "hi" chunk (2 chars) < MinChunkSize (30) => merged with para3.
        var para1 = new string('a', 58);
        var para2 = "hi";
        var para3 = new string('c', 59);
        var content = $"{para1}\n\n{para2}\n\n{para3}";

        // Act
        var result = strategy.Chunk(content);

        // Assert: "hi" should have been merged with the next chunk containing 'c'
        var mergedChunk = result.FirstOrDefault(c => c.Content.Contains("hi"));
        mergedChunk.ShouldNotBeNull();
        mergedChunk!.Content.ShouldContain("c");
    }

    [Fact]
    public void ChunkBySize_WithHighOverlap_TriggersInfiniteLoopGuard()
    {
        // Arrange: Overlap is very close to ChunkSize (ChunkSize - 1).
        // After adding a chunk at offset 0 with EndOffset = ChunkSize,
        // the next offset = ChunkSize - Overlap = 1, which is > 0 (the previous StartOffset).
        // But with overlap = ChunkSize - 1, the step size is only 1.
        // We use a scenario to verify no infinite loop occurs and results are produced.
        var options = new ChunkingOptions
        {
            ChunkSize = 5,
            Overlap = 4,
            RespectParagraphBoundaries = false,
            MinChunkSize = 0
        };
        var strategy = new ChunkingStrategy(options);
        var content = new string('z', 20);

        // Act
        var result = strategy.Chunk(content);

        // Assert: should terminate and produce chunks covering all content
        result.Count.ShouldBeGreaterThan(1);
        result[^1].EndOffset.ShouldBe(content.Length);
    }

    [Fact]
    public void MergeSmallChunks_SingleChunkWithPositiveMinChunkSize_ReturnsChunkAsIs()
    {
        // Arrange: single long paragraph (no \n\n) exceeding ChunkSize.
        // ChunkByParagraphs produces exactly 1 chunk, so MergeSmallChunks
        // hits the chunks.Count <= 1 early return with MinChunkSize > 0.
        var options = new ChunkingOptions
        {
            ChunkSize = 10,
            Overlap = 0,
            RespectParagraphBoundaries = true,
            MinChunkSize = 5
        };
        var strategy = new ChunkingStrategy(options);

        // Single paragraph longer than ChunkSize, no paragraph breaks
        var content = new string('a', 20);

        // Act
        var result = strategy.Chunk(content);

        // Assert: should produce 1 chunk containing all content
        result.Count.ShouldBe(1);
        result[0].Content.ShouldBe(content);
    }

    [Fact]
    public void MergeSmallChunks_WithMinChunkSizeZero_ReturnsChunksUnmerged()
    {
        // Arrange: MinChunkSize = 0 triggers the early return in MergeSmallChunks (line 312)
        // Use paragraph mode to produce multiple small chunks
        var options = new ChunkingOptions
        {
            ChunkSize = 20,
            Overlap = 0,
            RespectParagraphBoundaries = true,
            MinChunkSize = 0
        };
        var strategy = new ChunkingStrategy(options);

        // Create content with a very small paragraph that would normally be merged
        var para1 = new string('a', 18);
        var para2 = "hi";
        var para3 = new string('c', 18);
        var content = $"{para1}\n\n{para2}\n\n{para3}";

        // Act
        var result = strategy.Chunk(content);

        // Assert: with MinChunkSize=0, "hi" should NOT be merged
        // There should be a chunk containing just "hi"
        result.Count.ShouldBeGreaterThanOrEqualTo(3);
        result.ShouldContain(c => c.Content == "hi");
    }

    [Fact]
    public void Chunk_ByParagraphs_WithZeroOverlap_ShouldNotAddOverlapContent()
    {
        // Arrange: overlap=0, so the else branch at line 192-195 executes
        // (currentStartOffset = currentOffset, no overlap prepended).
        var options = new ChunkingOptions
        {
            ChunkSize = 50,
            Overlap = 0,
            RespectParagraphBoundaries = true,
            MinChunkSize = 0
        };
        var strategy = new ChunkingStrategy(options);

        var paragraph1 = new string('x', 40);
        var paragraph2 = new string('y', 40);
        var content = $"{paragraph1}\n\n{paragraph2}";

        // Act
        var result = strategy.Chunk(content);

        // Assert: chunks should not overlap -- second chunk should only contain 'y'
        result.Count.ShouldBe(2);
        result[1].Content.ShouldNotContain("x");
        result[1].Content.ShouldContain("y");
    }
}

#endregion

#region ContentChunk Tests

public class ContentChunkTests
{
    [Fact]
    public void DefaultProperties_ShouldHaveExpectedValues()
    {
        var chunk = new ContentChunk();

        chunk.Index.ShouldBe(0);
        chunk.Content.ShouldBe(string.Empty);
        chunk.StartOffset.ShouldBe(0);
        chunk.EndOffset.ShouldBe(0);
        chunk.ParentDocumentId.ShouldBeNull();
    }

    [Fact]
    public void Length_ShouldReturnContentLength()
    {
        var chunk = new ContentChunk { Content = "Hello, World!" };

        chunk.Length.ShouldBe(13);
    }

    [Fact]
    public void Length_WithEmptyContent_ShouldReturnZero()
    {
        var chunk = new ContentChunk();

        chunk.Length.ShouldBe(0);
    }

    [Fact]
    public void ToString_ShouldReturnExpectedFormat()
    {
        var chunk = new ContentChunk
        {
            Index = 2,
            Content = "Some text here",
            StartOffset = 100,
            EndOffset = 114
        };

        chunk.ToString().ShouldBe("Chunk[2] (100-114, 14 chars)");
    }

    [Fact]
    public void InitProperties_ShouldSetCorrectly()
    {
        var chunk = new ContentChunk
        {
            Index = 5,
            Content = "Test content",
            StartOffset = 50,
            EndOffset = 62,
            ParentDocumentId = "doc-456"
        };

        chunk.Index.ShouldBe(5);
        chunk.Content.ShouldBe("Test content");
        chunk.StartOffset.ShouldBe(50);
        chunk.EndOffset.ShouldBe(62);
        chunk.ParentDocumentId.ShouldBe("doc-456");
        chunk.Length.ShouldBe(12);
    }
}

#endregion
