using CompoundDocs.McpServer.Models;
using CompoundDocs.Tests.Utilities;

namespace CompoundDocs.Tests.Features;

/// <summary>
/// Unit tests for external document synchronization functionality.
/// Tests the ExternalDocument model properties related to syncing with external sources.
/// </summary>
public sealed class ExternalDocumentSyncTests
{
    #region LastSyncedAt Tests

    [Fact]
    public void ExternalDocument_LastSyncedAt_DefaultsToNull()
    {
        // Arrange & Act
        var doc = new ExternalDocument();

        // Assert
        doc.LastSyncedAt.ShouldBeNull();
    }

    [Fact]
    public void ExternalDocument_LastSyncedAt_CanBeSet()
    {
        // Arrange
        var syncTime = DateTimeOffset.UtcNow;
        var doc = new ExternalDocument
        {
            LastSyncedAt = syncTime
        };

        // Assert
        doc.LastSyncedAt.ShouldBe(syncTime);
    }

    [Fact]
    public void ExternalDocument_LastSyncedAt_CanBeSetToNull()
    {
        // Arrange
        var doc = new ExternalDocument
        {
            LastSyncedAt = DateTimeOffset.UtcNow
        };

        // Act
        doc.LastSyncedAt = null;

        // Assert
        doc.LastSyncedAt.ShouldBeNull();
    }

    [Fact]
    public void ExternalDocument_Builder_SetsLastSyncedAt()
    {
        // Arrange
        var syncTime = DateTimeOffset.UtcNow.AddDays(-1);

        // Act
        var doc = TestExternalDocumentBuilder.Create()
            .WithLastSyncedAt(syncTime)
            .Build();

        // Assert
        doc.LastSyncedAt.ShouldBe(syncTime);
    }

    #endregion

    #region SourceUrl Tests

    [Fact]
    public void ExternalDocument_SourceUrl_DefaultsToNull()
    {
        // Arrange & Act
        var doc = new ExternalDocument();

        // Assert
        doc.SourceUrl.ShouldBeNull();
    }

    [Fact]
    public void ExternalDocument_SourceUrl_CanBeSet()
    {
        // Arrange
        var url = "https://example.com/docs/api-reference.md";
        var doc = new ExternalDocument
        {
            SourceUrl = url
        };

        // Assert
        doc.SourceUrl.ShouldBe(url);
    }

    [Fact]
    public void ExternalDocument_Builder_SetsSourceUrl()
    {
        // Arrange
        var url = "https://api.example.com/docs/guide.md";

        // Act
        var doc = TestExternalDocumentBuilder.Create()
            .WithSourceUrl(url)
            .Build();

        // Assert
        doc.SourceUrl.ShouldBe(url);
    }

    [Fact]
    public void ExternalDocument_Builder_AllowsNullSourceUrl()
    {
        // Act
        var doc = TestExternalDocumentBuilder.Create()
            .WithSourceUrl(null)
            .Build();

        // Assert
        doc.SourceUrl.ShouldBeNull();
    }

    #endregion

    #region ContentHash Tests

    [Fact]
    public void ExternalDocument_ContentHash_DefaultsToEmptyString()
    {
        // Arrange & Act
        var doc = new ExternalDocument();

        // Assert
        doc.ContentHash.ShouldBe(string.Empty);
    }

    [Fact]
    public void ExternalDocument_ContentHash_CanBeSet()
    {
        // Arrange
        var hash = "abc123def456789";
        var doc = new ExternalDocument
        {
            ContentHash = hash
        };

        // Assert
        doc.ContentHash.ShouldBe(hash);
    }

    [Fact]
    public void ExternalDocument_Builder_SetsContentHash()
    {
        // Arrange
        var hash = "sha256hash123";

        // Act
        var doc = TestExternalDocumentBuilder.Create()
            .WithContentHash(hash)
            .Build();

        // Assert
        doc.ContentHash.ShouldBe(hash);
    }

    [Fact]
    public void ExternalDocument_ContentHashChange_IndicatesNeedForResync()
    {
        // Arrange
        var originalHash = "original-hash";
        var newHash = "new-hash";

        var existingDoc = TestExternalDocumentBuilder.Create()
            .WithContentHash(originalHash)
            .Build();

        // Simulate fetching updated content
        var fetchedHash = newHash;

        // Act & Assert - Hash change indicates content was updated
        (existingDoc.ContentHash != fetchedHash).ShouldBeTrue();
    }

    #endregion

    #region NamespacePrefix Tests

    [Fact]
    public void ExternalDocument_NamespacePrefix_DefaultsToExternal()
    {
        // Arrange & Act
        var doc = new ExternalDocument();

        // Assert
        doc.NamespacePrefix.ShouldBe("external");
    }

    [Fact]
    public void ExternalDocument_NamespacePrefix_CanBeSet()
    {
        // Arrange
        var prefix = "api-docs";
        var doc = new ExternalDocument
        {
            NamespacePrefix = prefix
        };

        // Assert
        doc.NamespacePrefix.ShouldBe(prefix);
    }

    [Fact]
    public void ExternalDocument_Builder_SetsNamespacePrefix()
    {
        // Arrange
        var prefix = "framework-docs";

        // Act
        var doc = TestExternalDocumentBuilder.Create()
            .WithNamespacePrefix(prefix)
            .Build();

        // Assert
        doc.NamespacePrefix.ShouldBe(prefix);
    }

    [Fact]
    public void ExternalDocument_NamespacePrefix_EnablesSourceFiltering()
    {
        // Arrange
        var docs = new[]
        {
            TestExternalDocumentBuilder.Create()
                .WithNamespacePrefix("api-docs")
                .WithTitle("API Reference")
                .Build(),
            TestExternalDocumentBuilder.Create()
                .WithNamespacePrefix("framework-docs")
                .WithTitle("Framework Guide")
                .Build(),
            TestExternalDocumentBuilder.Create()
                .WithNamespacePrefix("api-docs")
                .WithTitle("API Authentication")
                .Build()
        };

        // Act
        var apiDocs = docs.Where(d => d.NamespacePrefix == "api-docs").ToList();
        var frameworkDocs = docs.Where(d => d.NamespacePrefix == "framework-docs").ToList();

        // Assert
        apiDocs.Count.ShouldBe(2);
        frameworkDocs.Count.ShouldBe(1);
    }

    #endregion

    #region CharCount Tests

    [Fact]
    public void ExternalDocument_CharCount_DefaultsToZero()
    {
        // Arrange & Act
        var doc = new ExternalDocument();

        // Assert
        doc.CharCount.ShouldBe(0);
    }

    [Fact]
    public void ExternalDocument_CharCount_CanBeSet()
    {
        // Arrange
        var count = 1500;
        var doc = new ExternalDocument
        {
            CharCount = count
        };

        // Assert
        doc.CharCount.ShouldBe(count);
    }

    [Fact]
    public void ExternalDocument_Builder_SetsCharCount()
    {
        // Arrange
        var count = 2500;

        // Act
        var doc = TestExternalDocumentBuilder.Create()
            .WithCharCount(count)
            .Build();

        // Assert
        doc.CharCount.ShouldBe(count);
    }

    [Fact]
    public void ExternalDocument_Builder_WithContent_UpdatesCharCount()
    {
        // Arrange
        var content = "This is test content with some text.";

        // Act
        var doc = TestExternalDocumentBuilder.Create()
            .WithContent(content)
            .Build();

        // Assert - Builder should auto-calculate char count from content
        doc.CharCount.ShouldBe(content.Length);
    }

    #endregion

    #region Sync Scenario Tests

    [Fact]
    public void ExternalDocument_InitialSync_SetsAllSyncProperties()
    {
        // Arrange
        var syncTime = DateTimeOffset.UtcNow;
        var sourceUrl = "https://docs.example.com/guide.md";
        var contentHash = "initial-hash-value";

        // Act
        var doc = TestExternalDocumentBuilder.Create()
            .WithSourceUrl(sourceUrl)
            .WithLastSyncedAt(syncTime)
            .WithContentHash(contentHash)
            .WithNamespacePrefix("external-guide")
            .Build();

        // Assert
        doc.SourceUrl.ShouldBe(sourceUrl);
        doc.LastSyncedAt.ShouldBe(syncTime);
        doc.ContentHash.ShouldBe(contentHash);
        doc.NamespacePrefix.ShouldBe("external-guide");
    }

    [Fact]
    public void ExternalDocument_IncrementalSync_UpdatesSyncTimestamp()
    {
        // Arrange
        var originalSyncTime = DateTimeOffset.UtcNow.AddDays(-1);
        var doc = TestExternalDocumentBuilder.Create()
            .WithLastSyncedAt(originalSyncTime)
            .Build();

        // Act - Simulate re-sync
        var newSyncTime = DateTimeOffset.UtcNow;
        doc.LastSyncedAt = newSyncTime;

        // Assert
        doc.LastSyncedAt.ShouldBe(newSyncTime);
        (doc.LastSyncedAt!.Value > originalSyncTime).ShouldBeTrue();
    }

    [Fact]
    public void ExternalDocument_StalenessCheck_DetectsOutdatedContent()
    {
        // Arrange
        var staleThreshold = TimeSpan.FromDays(7);
        var oldSyncTime = DateTimeOffset.UtcNow.AddDays(-10);

        var doc = TestExternalDocumentBuilder.Create()
            .WithLastSyncedAt(oldSyncTime)
            .Build();

        // Act
        var isStale = doc.LastSyncedAt.HasValue &&
                      DateTimeOffset.UtcNow - doc.LastSyncedAt.Value > staleThreshold;

        // Assert
        isStale.ShouldBeTrue();
    }

    [Fact]
    public void ExternalDocument_StalenessCheck_DetectsFreshContent()
    {
        // Arrange
        var staleThreshold = TimeSpan.FromDays(7);
        var recentSyncTime = DateTimeOffset.UtcNow.AddDays(-1);

        var doc = TestExternalDocumentBuilder.Create()
            .WithLastSyncedAt(recentSyncTime)
            .Build();

        // Act
        var isStale = doc.LastSyncedAt.HasValue &&
                      DateTimeOffset.UtcNow - doc.LastSyncedAt.Value > staleThreshold;

        // Assert
        isStale.ShouldBeFalse();
    }

    [Fact]
    public void ExternalDocument_BuildMany_CreatesManyWithIncrementalSyncTimes()
    {
        // Arrange & Act
        var docs = TestExternalDocumentBuilder.Create()
            .WithLastSyncedAt(DateTimeOffset.UtcNow)
            .BuildMany(5);

        // Assert - Each document should have an incrementally different sync time
        var syncTimes = docs.Select(d => d.LastSyncedAt).ToList();
        for (int i = 1; i < syncTimes.Count; i++)
        {
            syncTimes[i].ShouldNotBe(syncTimes[i - 1]);
        }
    }

    #endregion
}
