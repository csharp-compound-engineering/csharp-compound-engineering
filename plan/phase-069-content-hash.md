# Phase 069: Content Hash Calculation

> **Status**: PLANNED
> **Category**: Document Processing
> **Estimated Effort**: S
> **Prerequisites**: Phase 068 (Markdown File Reading)

---

## Spec References

- [mcp-server/file-watcher.md - Content Hash Comparison](../spec/mcp-server/file-watcher.md#sync-on-activation-startup-reconciliation)
- [mcp-server/file-watcher.md - Reconciliation Algorithm](../spec/mcp-server/file-watcher.md#algorithm)
- [mcp-server/database-schema.md - Path Hash Generation](../spec/mcp-server/database-schema.md#path-hash-generation)
- [mcp-server/database-schema.md - ContentHash Field](../spec/mcp-server/database-schema.md#documents-schema-semantic-kernel-model)

---

## Objectives

1. Implement SHA256 content hash calculation for change detection
2. Enable skip-embedding optimization when content is unchanged
3. Implement path hash generation for compound key tenant isolation
4. Provide consistent hash computation across the application
5. Support both content hash (full file) and path hash (truncated for brevity)

---

## Acceptance Criteria

- [ ] `IContentHasher` interface defined with content hash and path hash methods
- [ ] `Sha256ContentHasher` implementation using `System.Security.Cryptography.SHA256`
- [ ] Content hash returns full 64-character lowercase hex string
- [ ] Path hash returns first 16 characters (64 bits) of SHA256 for brevity
- [ ] Path normalization handles both Windows and Unix path separators
- [ ] Hash calculation is deterministic and consistent across platforms
- [ ] Empty content produces valid hash (not null or exception)
- [ ] Service registered in DI container as singleton
- [ ] Unit tests verify hash consistency and change detection scenarios
- [ ] Integration with document processing pipeline for skip-embedding decisions

---

## Implementation Notes

### 1. Content Hasher Interface

Define the contract for hash computation:

```csharp
// src/CompoundDocs.Common/Services/IContentHasher.cs
namespace CompoundDocs.Common.Services;

/// <summary>
/// Provides content and path hash computation for change detection and tenant isolation.
/// </summary>
public interface IContentHasher
{
    /// <summary>
    /// Computes a SHA256 hash of the content for change detection.
    /// Returns full 64-character lowercase hex string.
    /// </summary>
    /// <param name="content">The content to hash.</param>
    /// <returns>64-character lowercase hex SHA256 hash.</returns>
    string ComputeContentHash(string content);

    /// <summary>
    /// Computes a SHA256 hash of the content bytes for change detection.
    /// Returns full 64-character lowercase hex string.
    /// </summary>
    /// <param name="contentBytes">The content bytes to hash.</param>
    /// <returns>64-character lowercase hex SHA256 hash.</returns>
    string ComputeContentHash(ReadOnlySpan<byte> contentBytes);

    /// <summary>
    /// Computes a truncated SHA256 hash of the absolute path for compound key isolation.
    /// Normalizes path separators before hashing.
    /// Returns first 16 characters (64 bits) for brevity.
    /// </summary>
    /// <param name="absolutePath">The absolute file system path.</param>
    /// <returns>16-character lowercase hex path hash.</returns>
    string ComputePathHash(string absolutePath);

    /// <summary>
    /// Determines if content has changed by comparing hashes.
    /// </summary>
    /// <param name="newContent">The new content to check.</param>
    /// <param name="existingHash">The existing hash to compare against.</param>
    /// <returns>True if content has changed (hashes differ); false if unchanged.</returns>
    bool HasContentChanged(string newContent, string existingHash);
}
```

### 2. SHA256 Content Hasher Implementation

```csharp
// src/CompoundDocs.Common/Services/Sha256ContentHasher.cs
using System.Security.Cryptography;
using System.Text;

namespace CompoundDocs.Common.Services;

/// <summary>
/// SHA256-based implementation of content and path hashing.
/// Thread-safe and suitable for singleton registration.
/// </summary>
public sealed class Sha256ContentHasher : IContentHasher
{
    /// <summary>
    /// Length of the truncated path hash (16 hex characters = 64 bits).
    /// </summary>
    public const int PathHashLength = 16;

    /// <summary>
    /// Length of the full content hash (64 hex characters = 256 bits).
    /// </summary>
    public const int ContentHashLength = 64;

    /// <inheritdoc />
    public string ComputeContentHash(string content)
    {
        ArgumentNullException.ThrowIfNull(content);

        var contentBytes = Encoding.UTF8.GetBytes(content);
        return ComputeContentHash(contentBytes);
    }

    /// <inheritdoc />
    public string ComputeContentHash(ReadOnlySpan<byte> contentBytes)
    {
        Span<byte> hashBytes = stackalloc byte[SHA256.HashSizeInBytes];
        SHA256.HashData(contentBytes, hashBytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    /// <inheritdoc />
    public string ComputePathHash(string absolutePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(absolutePath);

        // Normalize path separators (Windows to Unix) and trim trailing separator
        var normalizedPath = absolutePath
            .Replace('\\', '/')
            .TrimEnd('/');

        var pathBytes = Encoding.UTF8.GetBytes(normalizedPath);
        Span<byte> hashBytes = stackalloc byte[SHA256.HashSizeInBytes];
        SHA256.HashData(pathBytes, hashBytes);

        // Return first 16 characters (64 bits) for brevity
        return Convert.ToHexString(hashBytes)[..PathHashLength].ToLowerInvariant();
    }

    /// <inheritdoc />
    public bool HasContentChanged(string newContent, string existingHash)
    {
        ArgumentNullException.ThrowIfNull(newContent);
        ArgumentNullException.ThrowIfNull(existingHash);

        var newHash = ComputeContentHash(newContent);
        return !string.Equals(newHash, existingHash, StringComparison.OrdinalIgnoreCase);
    }
}
```

### 3. Hash Constants

Define constants for hash-related configuration:

```csharp
// src/CompoundDocs.Common/Constants/HashConstants.cs
namespace CompoundDocs.Common.Constants;

/// <summary>
/// Constants for content and path hash computation.
/// </summary>
public static class HashConstants
{
    /// <summary>
    /// Length of the truncated path hash in hex characters.
    /// 16 characters = 64 bits of entropy, sufficient for collision avoidance.
    /// </summary>
    public const int PathHashLength = 16;

    /// <summary>
    /// Length of the full SHA256 content hash in hex characters.
    /// </summary>
    public const int ContentHashLength = 64;

    /// <summary>
    /// The hash algorithm used for content and path hashing.
    /// </summary>
    public const string Algorithm = "SHA256";
}
```

### 4. Document Change Detector

Helper class for document-level change detection:

```csharp
// src/CompoundDocs.Common/Services/DocumentChangeDetector.cs
namespace CompoundDocs.Common.Services;

/// <summary>
/// Detects changes in documents by comparing content hashes.
/// Used to skip re-embedding for unchanged documents.
/// </summary>
public sealed class DocumentChangeDetector
{
    private readonly IContentHasher _contentHasher;

    public DocumentChangeDetector(IContentHasher contentHasher)
    {
        _contentHasher = contentHasher ?? throw new ArgumentNullException(nameof(contentHasher));
    }

    /// <summary>
    /// Determines if a document needs re-embedding based on content hash comparison.
    /// </summary>
    /// <param name="newContent">The current content from disk.</param>
    /// <param name="storedContentHash">The content hash stored in the database.</param>
    /// <returns>True if embedding is needed (content changed); false to skip.</returns>
    public bool NeedsEmbedding(string newContent, string? storedContentHash)
    {
        // Always embed if no stored hash exists (new document)
        if (string.IsNullOrEmpty(storedContentHash))
        {
            return true;
        }

        return _contentHasher.HasContentChanged(newContent, storedContentHash);
    }

    /// <summary>
    /// Computes the content hash and determines if embedding is needed in one operation.
    /// </summary>
    /// <param name="newContent">The current content from disk.</param>
    /// <param name="storedContentHash">The content hash stored in the database.</param>
    /// <returns>Tuple of (new hash, needs embedding).</returns>
    public (string NewHash, bool NeedsEmbedding) EvaluateContent(string newContent, string? storedContentHash)
    {
        var newHash = _contentHasher.ComputeContentHash(newContent);

        if (string.IsNullOrEmpty(storedContentHash))
        {
            return (newHash, true);
        }

        var needsEmbedding = !string.Equals(newHash, storedContentHash, StringComparison.OrdinalIgnoreCase);
        return (newHash, needsEmbedding);
    }
}
```

### 5. DI Registration

Register hash services as singletons:

```csharp
// Extension method for DI registration
// src/CompoundDocs.Common/Extensions/ServiceCollectionExtensions.cs
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddContentHashServices(this IServiceCollection services)
    {
        services.AddSingleton<IContentHasher, Sha256ContentHasher>();
        services.AddSingleton<DocumentChangeDetector>();
        return services;
    }
}
```

---

## Dependencies

### Depends On

- **Phase 068**: Markdown File Reading - Provides content bytes for hashing

### Blocks

- **Phase 070**: Document Indexing Pipeline - Uses hash for change detection
- **Phase 071+**: File Watcher Reconciliation - Compares hashes for sync decisions
- **Phase 072+**: Embedding Skip Optimization - Uses hash comparison to avoid redundant embedding calls

---

## Testing Verification

### Unit Tests

```csharp
// tests/CompoundDocs.Tests/Services/Sha256ContentHasherTests.cs
using CompoundDocs.Common.Services;

namespace CompoundDocs.Tests.Services;

public class Sha256ContentHasherTests
{
    private readonly Sha256ContentHasher _hasher = new();

    [Fact]
    public void ComputeContentHash_ReturnsConsistentHash_ForSameContent()
    {
        // Arrange
        const string content = "Hello, World!";

        // Act
        var hash1 = _hasher.ComputeContentHash(content);
        var hash2 = _hasher.ComputeContentHash(content);

        // Assert
        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void ComputeContentHash_ReturnsDifferentHash_ForDifferentContent()
    {
        // Arrange
        const string content1 = "Hello, World!";
        const string content2 = "Hello, World";  // Missing exclamation

        // Act
        var hash1 = _hasher.ComputeContentHash(content1);
        var hash2 = _hasher.ComputeContentHash(content2);

        // Assert
        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void ComputeContentHash_Returns64CharacterLowercaseHex()
    {
        // Arrange
        const string content = "Test content";

        // Act
        var hash = _hasher.ComputeContentHash(content);

        // Assert
        Assert.Equal(64, hash.Length);
        Assert.True(hash.All(c => char.IsAsciiHexDigitLower(c) || char.IsDigit(c)));
    }

    [Fact]
    public void ComputeContentHash_HandlesEmptyString()
    {
        // Arrange
        const string content = "";

        // Act
        var hash = _hasher.ComputeContentHash(content);

        // Assert
        Assert.NotNull(hash);
        Assert.Equal(64, hash.Length);
        // SHA256 of empty string is a known value
        Assert.Equal("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", hash);
    }

    [Fact]
    public void ComputeContentHash_HandlesUnicodeContent()
    {
        // Arrange
        const string content = "Hello \u4e16\u754c!";  // Hello World in Chinese

        // Act
        var hash = _hasher.ComputeContentHash(content);

        // Assert
        Assert.Equal(64, hash.Length);
    }

    [Fact]
    public void ComputeContentHash_ThrowsOnNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _hasher.ComputeContentHash((string)null!));
    }
}
```

### Path Hash Tests

```csharp
// tests/CompoundDocs.Tests/Services/PathHashTests.cs
using CompoundDocs.Common.Services;

namespace CompoundDocs.Tests.Services;

public class PathHashTests
{
    private readonly Sha256ContentHasher _hasher = new();

    [Fact]
    public void ComputePathHash_Returns16CharacterHash()
    {
        // Arrange
        const string path = "/Users/dev/projects/my-project";

        // Act
        var hash = _hasher.ComputePathHash(path);

        // Assert
        Assert.Equal(16, hash.Length);
    }

    [Fact]
    public void ComputePathHash_NormalizesWindowsPaths()
    {
        // Arrange
        const string windowsPath = @"C:\Users\dev\projects\my-project";
        const string unixPath = "C:/Users/dev/projects/my-project";

        // Act
        var windowsHash = _hasher.ComputePathHash(windowsPath);
        var unixHash = _hasher.ComputePathHash(unixPath);

        // Assert
        Assert.Equal(windowsHash, unixHash);
    }

    [Fact]
    public void ComputePathHash_TrimsTrailingSeparator()
    {
        // Arrange
        const string pathWithSeparator = "/Users/dev/projects/my-project/";
        const string pathWithoutSeparator = "/Users/dev/projects/my-project";

        // Act
        var hash1 = _hasher.ComputePathHash(pathWithSeparator);
        var hash2 = _hasher.ComputePathHash(pathWithoutSeparator);

        // Assert
        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void ComputePathHash_ReturnsLowercaseHex()
    {
        // Arrange
        const string path = "/Users/dev/projects/MY-PROJECT";

        // Act
        var hash = _hasher.ComputePathHash(path);

        // Assert
        Assert.True(hash.All(c => char.IsAsciiHexDigitLower(c) || char.IsDigit(c)));
    }

    [Fact]
    public void ComputePathHash_DifferentPaths_ProduceDifferentHashes()
    {
        // Arrange
        const string path1 = "/Users/dev/project-a";
        const string path2 = "/Users/dev/project-b";

        // Act
        var hash1 = _hasher.ComputePathHash(path1);
        var hash2 = _hasher.ComputePathHash(path2);

        // Assert
        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void ComputePathHash_ThrowsOnNullOrWhitespace()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _hasher.ComputePathHash(null!));
        Assert.Throws<ArgumentException>(() => _hasher.ComputePathHash(""));
        Assert.Throws<ArgumentException>(() => _hasher.ComputePathHash("   "));
    }
}
```

### Change Detection Tests

```csharp
// tests/CompoundDocs.Tests/Services/DocumentChangeDetectorTests.cs
using CompoundDocs.Common.Services;

namespace CompoundDocs.Tests.Services;

public class DocumentChangeDetectorTests
{
    private readonly DocumentChangeDetector _detector;
    private readonly IContentHasher _hasher;

    public DocumentChangeDetectorTests()
    {
        _hasher = new Sha256ContentHasher();
        _detector = new DocumentChangeDetector(_hasher);
    }

    [Fact]
    public void NeedsEmbedding_ReturnsTrue_WhenStoredHashIsNull()
    {
        // Arrange
        const string content = "New document content";

        // Act
        var result = _detector.NeedsEmbedding(content, null);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void NeedsEmbedding_ReturnsTrue_WhenStoredHashIsEmpty()
    {
        // Arrange
        const string content = "New document content";

        // Act
        var result = _detector.NeedsEmbedding(content, string.Empty);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void NeedsEmbedding_ReturnsFalse_WhenContentUnchanged()
    {
        // Arrange
        const string content = "Unchanged content";
        var storedHash = _hasher.ComputeContentHash(content);

        // Act
        var result = _detector.NeedsEmbedding(content, storedHash);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void NeedsEmbedding_ReturnsTrue_WhenContentChanged()
    {
        // Arrange
        const string originalContent = "Original content";
        const string newContent = "Modified content";
        var storedHash = _hasher.ComputeContentHash(originalContent);

        // Act
        var result = _detector.NeedsEmbedding(newContent, storedHash);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void EvaluateContent_ReturnsCorrectHash_AndNeedsEmbedding()
    {
        // Arrange
        const string content = "Test content";
        var expectedHash = _hasher.ComputeContentHash(content);

        // Act
        var (newHash, needsEmbedding) = _detector.EvaluateContent(content, null);

        // Assert
        Assert.Equal(expectedHash, newHash);
        Assert.True(needsEmbedding);
    }

    [Fact]
    public void EvaluateContent_ReturnsFalse_WhenHashMatches()
    {
        // Arrange
        const string content = "Same content";
        var storedHash = _hasher.ComputeContentHash(content);

        // Act
        var (newHash, needsEmbedding) = _detector.EvaluateContent(content, storedHash);

        // Assert
        Assert.Equal(storedHash, newHash);
        Assert.False(needsEmbedding);
    }
}
```

### Known Hash Vector Tests

```csharp
// tests/CompoundDocs.Tests/Services/HashVectorTests.cs
using CompoundDocs.Common.Services;

namespace CompoundDocs.Tests.Services;

/// <summary>
/// Tests against known SHA256 test vectors to ensure correct implementation.
/// </summary>
public class HashVectorTests
{
    private readonly Sha256ContentHasher _hasher = new();

    [Theory]
    [InlineData("", "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855")]
    [InlineData("abc", "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad")]
    [InlineData("hello", "2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824")]
    public void ComputeContentHash_MatchesKnownVectors(string input, string expectedHash)
    {
        // Act
        var hash = _hasher.ComputeContentHash(input);

        // Assert
        Assert.Equal(expectedHash, hash);
    }

    [Fact]
    public void ComputePathHash_SpecExample_MatchesExpected()
    {
        // This test verifies the example from database-schema.md
        // The spec shows: first 16 characters of SHA256(normalized_path)

        // Arrange
        const string path = "/Users/dev/projects/my-project";
        var fullHash = _hasher.ComputeContentHash(path);

        // Act
        var pathHash = _hasher.ComputePathHash(path);

        // Assert
        Assert.Equal(fullHash[..16], pathHash);
    }
}
```

---

## Files Created/Modified

| File | Action | Description |
|------|--------|-------------|
| `src/CompoundDocs.Common/Services/IContentHasher.cs` | Create | Content hash interface |
| `src/CompoundDocs.Common/Services/Sha256ContentHasher.cs` | Create | SHA256 implementation |
| `src/CompoundDocs.Common/Services/DocumentChangeDetector.cs` | Create | Change detection helper |
| `src/CompoundDocs.Common/Constants/HashConstants.cs` | Create | Hash-related constants |
| `src/CompoundDocs.Common/Extensions/ServiceCollectionExtensions.cs` | Modify | Add hash service registration |
| `tests/CompoundDocs.Tests/Services/Sha256ContentHasherTests.cs` | Create | Content hash unit tests |
| `tests/CompoundDocs.Tests/Services/PathHashTests.cs` | Create | Path hash unit tests |
| `tests/CompoundDocs.Tests/Services/DocumentChangeDetectorTests.cs` | Create | Change detector tests |
| `tests/CompoundDocs.Tests/Services/HashVectorTests.cs` | Create | Known vector verification |

---

## Usage Examples

### Computing Content Hash for Storage

```csharp
// During document indexing
var contentHasher = serviceProvider.GetRequiredService<IContentHasher>();
var content = await File.ReadAllTextAsync(filePath);
var contentHash = contentHasher.ComputeContentHash(content);

var document = new CompoundDocument
{
    ContentHash = contentHash,
    // ... other properties
};
```

### Skip Embedding for Unchanged Content

```csharp
// During file watcher reconciliation
var changeDetector = serviceProvider.GetRequiredService<DocumentChangeDetector>();
var existingDoc = await documentsCollection.GetAsync(documentId);
var newContent = await File.ReadAllTextAsync(filePath);

var (newHash, needsEmbedding) = changeDetector.EvaluateContent(newContent, existingDoc?.ContentHash);

if (needsEmbedding)
{
    // Generate embedding via Ollama - expensive operation
    var embedding = await embeddingService.GenerateEmbeddingAsync(newContent);
    existingDoc.Embedding = embedding;
}

existingDoc.ContentHash = newHash;
await documentsCollection.UpsertAsync(existingDoc);
```

### Computing Path Hash for Tenant Isolation

```csharp
// During project activation
var contentHasher = serviceProvider.GetRequiredService<IContentHasher>();
var absolutePath = Path.GetFullPath(projectRoot);
var pathHash = contentHasher.ComputePathHash(absolutePath);

var tenantContext = new TenantContext
{
    ProjectName = projectName,
    BranchName = currentBranch,
    PathHash = pathHash  // Enables git worktree support
};
```

---

## Algorithm Selection Rationale

### Why SHA256?

| Consideration | Decision |
|---------------|----------|
| Security | SHA256 is cryptographically secure; no collision attacks known |
| Performance | Fast for typical document sizes (<1MB); uses hardware acceleration on modern CPUs |
| Consistency | .NET built-in implementation guarantees cross-platform consistency |
| Spec alignment | Matches database-schema.md specification |

### Why Truncate Path Hash to 16 Characters?

| Aspect | Rationale |
|--------|-----------|
| Collision probability | 64 bits provides 2^64 possible values; collision probability negligible for <10M documents |
| Storage efficiency | 16 chars vs 64 chars saves database space in compound key |
| Readability | Shorter hashes are easier to debug and log |
| Spec compliance | Matches database-schema.md `path_hash VARCHAR(64)` with 16-char values |

---

## Risk Mitigation

| Risk | Mitigation |
|------|------------|
| Hash collision | SHA256 with 64+ bits is statistically negligible; monitor for duplicates in production |
| Encoding inconsistency | Always use UTF-8; explicit encoding in implementation |
| Path normalization edge cases | Comprehensive tests for Windows/Unix paths, trailing separators |
| Performance for large files | Use Span<byte> and stackalloc for efficient memory usage |
| Thread safety | SHA256.HashData is thread-safe; no instance state in hasher |
