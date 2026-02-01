# Phase 020: Sensitive Data Handling in Logging

> **Status**: NOT_STARTED
> **Effort Estimate**: 3-5 hours
> **Category**: Infrastructure Setup
> **Prerequisites**: Phase 018 (Structured Logging Configuration)

---

## Spec References

This phase implements sensitive data protection for logging defined in:

- **spec/observability.md** - [Sensitive Data Handling](../spec/observability.md#sensitive-data-handling) (lines 175-204)
- **spec/observability.md** - [Structured Logging](../spec/observability.md#structured-logging) (lines 102-172)

---

## Objectives

1. Establish clear data classification rules for logging (never log vs. safe to log)
2. Implement logging sanitization helper utilities
3. Create defensive coding patterns to prevent accidental sensitive data leakage
4. Define code review guidelines for logging statements
5. Implement unit tests that verify no sensitive data appears in logs

---

## Acceptance Criteria

### Data Classification Implementation

- [ ] `LoggingSensitivity.cs` enum/constants defining data categories:
  - [ ] `NeverLog` category items documented and enforced
  - [ ] `SafeToLog` category items documented
- [ ] XML documentation on all logging-related methods explaining sensitivity rules

### Logging Sanitization Helpers

- [ ] `ILogSanitizer` interface in `CompoundDocs.Common`
- [ ] `LogSanitizer` implementation with:
  - [ ] `SanitizePath(string absolutePath)` - converts to relative path, removes user-specific segments
  - [ ] `SanitizeException(Exception ex)` - removes sensitive data from exception details
  - [ ] `TruncateContent(string content, int maxLength)` - safe preview without full content
  - [ ] `RedactCredentials(string text)` - masks connection strings, API keys, tokens
- [ ] Extension methods for `ILogger` that enforce sanitization

### Sensitive Data Categories

#### NEVER Log (enforced by code review and tests)

- [ ] Document content (markdown body text)
- [ ] User credentials, tokens, or API keys
- [ ] Database connection strings with credentials
- [ ] Embedding vectors (float arrays)
- [ ] Query result content (only counts/metadata)
- [ ] Full SQL queries containing user data
- [ ] Environment variable values containing secrets

#### Safe to Log (documented in code)

- [ ] File paths (relative to repository root)
- [ ] Document metadata (title, type, character count, hash)
- [ ] Operation timing (elapsed milliseconds)
- [ ] Error messages (sanitized, without sensitive context)
- [ ] Document IDs and content hashes
- [ ] Query parameters (search terms, filters)
- [ ] Counts and aggregates (result counts, batch sizes)

### Code Review Guidelines

- [ ] `LOGGING-GUIDELINES.md` document in `docs/` directory containing:
  - [ ] Data classification reference table
  - [ ] Code examples of correct vs. incorrect logging
  - [ ] PR review checklist for logging statements
  - [ ] How to use sanitization helpers

### Unit Tests for Sensitive Data Leakage

- [ ] `LogSanitizerTests.cs` covering:
  - [ ] Path sanitization removes absolute paths
  - [ ] Credential redaction masks connection strings
  - [ ] Content truncation limits exposure
  - [ ] Exception sanitization removes stack details with sensitive data
- [ ] `LoggingSecurityTests.cs` covering:
  - [ ] Mock logger captures log output
  - [ ] Test that document indexing doesn't log content
  - [ ] Test that embedding generation doesn't log vectors
  - [ ] Test that error handling doesn't expose credentials
  - [ ] Test that query operations log only metadata

---

## Implementation Notes

### LogSanitizer Interface and Implementation

```csharp
// src/CompoundDocs.Common/Logging/ILogSanitizer.cs
namespace CompoundDocs.Common.Logging;

/// <summary>
/// Provides methods to sanitize data before logging to prevent sensitive data exposure.
/// </summary>
public interface ILogSanitizer
{
    /// <summary>
    /// Converts an absolute file path to a safe relative path for logging.
    /// </summary>
    /// <param name="absolutePath">The absolute file path.</param>
    /// <param name="basePath">The base path to make relative to.</param>
    /// <returns>A relative path safe for logging.</returns>
    string SanitizePath(string absolutePath, string basePath);

    /// <summary>
    /// Sanitizes an exception for logging by removing potentially sensitive data.
    /// </summary>
    /// <param name="exception">The exception to sanitize.</param>
    /// <returns>A sanitized exception message.</returns>
    string SanitizeException(Exception exception);

    /// <summary>
    /// Creates a safe preview of content without exposing full text.
    /// </summary>
    /// <param name="content">The content to preview.</param>
    /// <param name="maxLength">Maximum preview length (default 50 chars).</param>
    /// <returns>A truncated preview with character count.</returns>
    string TruncateContent(string content, int maxLength = 50);

    /// <summary>
    /// Redacts credentials and sensitive patterns from text.
    /// </summary>
    /// <param name="text">Text potentially containing credentials.</param>
    /// <returns>Text with credentials redacted.</returns>
    string RedactCredentials(string text);
}
```

### LogSanitizer Implementation

```csharp
// src/CompoundDocs.Common/Logging/LogSanitizer.cs
namespace CompoundDocs.Common.Logging;

public class LogSanitizer : ILogSanitizer
{
    private static readonly Regex ConnectionStringPattern = new(
        @"(Password|Pwd|Secret|Key|Token)=([^;]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ApiKeyPattern = new(
        @"(api[_-]?key|bearer|authorization)[=:\s]+['""]?([a-zA-Z0-9\-_]{20,})['""]?",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public string SanitizePath(string absolutePath, string basePath)
    {
        if (string.IsNullOrEmpty(absolutePath))
            return "[empty path]";

        if (string.IsNullOrEmpty(basePath))
            return Path.GetFileName(absolutePath);

        var relativePath = Path.GetRelativePath(basePath, absolutePath);

        // Ensure we don't expose parent directory traversal
        if (relativePath.StartsWith(".."))
            return Path.GetFileName(absolutePath);

        return relativePath;
    }

    public string SanitizeException(Exception exception)
    {
        if (exception is null)
            return "[null exception]";

        var message = exception.Message;

        // Redact any credentials that might appear in exception messages
        message = RedactCredentials(message);

        // Return type and sanitized message, not full stack trace
        return $"{exception.GetType().Name}: {message}";
    }

    public string TruncateContent(string content, int maxLength = 50)
    {
        if (string.IsNullOrEmpty(content))
            return "[empty]";

        var charCount = content.Length;

        if (charCount <= maxLength)
            return $"[{charCount} chars]";

        // Never include actual content, just metadata
        return $"[{charCount} chars]";
    }

    public string RedactCredentials(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        text = ConnectionStringPattern.Replace(text, "$1=***REDACTED***");
        text = ApiKeyPattern.Replace(text, "$1=***REDACTED***");

        return text;
    }
}
```

### Safe Logging Extension Methods

```csharp
// src/CompoundDocs.Common/Logging/LoggerExtensions.cs
namespace CompoundDocs.Common.Logging;

public static class LoggerExtensions
{
    private static readonly ILogSanitizer DefaultSanitizer = new LogSanitizer();

    /// <summary>
    /// Logs document operation with only safe metadata, never content.
    /// </summary>
    public static void LogDocumentOperation(
        this ILogger logger,
        LogLevel level,
        string operation,
        string documentPath,
        string basePath,
        int? charCount = null,
        long? elapsedMs = null)
    {
        var safePath = DefaultSanitizer.SanitizePath(documentPath, basePath);

        if (charCount.HasValue && elapsedMs.HasValue)
        {
            logger.Log(level,
                "{Operation} document: {DocumentPath} ({CharCount} chars) in {ElapsedMs}ms",
                operation, safePath, charCount.Value, elapsedMs.Value);
        }
        else if (charCount.HasValue)
        {
            logger.Log(level,
                "{Operation} document: {DocumentPath} ({CharCount} chars)",
                operation, safePath, charCount.Value);
        }
        else
        {
            logger.Log(level,
                "{Operation} document: {DocumentPath}",
                operation, safePath);
        }
    }

    /// <summary>
    /// Logs an error with sanitized exception details.
    /// </summary>
    public static void LogSanitizedError(
        this ILogger logger,
        Exception exception,
        string message,
        params object[] args)
    {
        var sanitizedException = DefaultSanitizer.SanitizeException(exception);
        logger.LogError(exception, message + " Error: {SanitizedError}",
            args.Concat(new[] { sanitizedException }).ToArray());
    }
}
```

### Sensitive Data Constants

```csharp
// src/CompoundDocs.Common/Logging/LoggingSensitivity.cs
namespace CompoundDocs.Common.Logging;

/// <summary>
/// Documents data sensitivity categories for logging decisions.
/// This is a reference document enforced by code review and unit tests.
/// </summary>
public static class LoggingSensitivity
{
    /// <summary>
    /// Data that must NEVER appear in logs under any circumstances.
    /// </summary>
    public static class NeverLog
    {
        public const string DocumentContent = "Markdown body text, document content";
        public const string Credentials = "Passwords, API keys, tokens, secrets";
        public const string ConnectionStrings = "Database connection strings with credentials";
        public const string EmbeddingVectors = "Float arrays from embedding generation";
        public const string QueryResultContent = "Full text content from search results";
        public const string SqlWithData = "SQL queries containing user data values";
        public const string EnvironmentSecrets = "Environment variable values with secrets";
    }

    /// <summary>
    /// Data that is safe to log and provides operational visibility.
    /// </summary>
    public static class SafeToLog
    {
        public const string RelativeFilePaths = "Paths relative to repository root";
        public const string DocumentMetadata = "Title, type, character count, content hash";
        public const string OperationTiming = "Elapsed milliseconds for operations";
        public const string SanitizedErrors = "Error messages without sensitive context";
        public const string DocumentIds = "Generated IDs and content hashes";
        public const string QueryParameters = "Search terms, filters, limits";
        public const string Counts = "Result counts, batch sizes, progress numbers";
    }
}
```

### Unit Test Pattern for Log Verification

```csharp
// tests/CompoundDocs.Tests/Logging/LoggingSecurityTests.cs
namespace CompoundDocs.Tests.Logging;

public class LoggingSecurityTests
{
    private readonly TestLogger<DocumentIndexer> _testLogger;
    private readonly DocumentIndexer _indexer;

    public LoggingSecurityTests()
    {
        _testLogger = new TestLogger<DocumentIndexer>();
        _indexer = new DocumentIndexer(_testLogger, /* other deps */);
    }

    [Fact]
    public async Task IndexDocument_DoesNotLogContent()
    {
        // Arrange
        var documentContent = "This is sensitive document content that should never appear in logs.";
        var document = new Document { Path = "test.md", Content = documentContent };

        // Act
        await _indexer.IndexAsync(document);

        // Assert - verify no log entry contains the content
        var allLogMessages = _testLogger.GetAllMessages();
        foreach (var message in allLogMessages)
        {
            Assert.DoesNotContain(documentContent, message);
            Assert.DoesNotContain("sensitive", message.ToLower());
        }
    }

    [Fact]
    public async Task IndexDocument_LogsOnlyMetadata()
    {
        // Arrange
        var document = new Document
        {
            Path = "/absolute/path/to/repo/docs/test.md",
            Content = "Content here"
        };

        // Act
        await _indexer.IndexAsync(document);

        // Assert - verify logs contain safe metadata
        var messages = _testLogger.GetAllMessages();
        Assert.Contains(messages, m => m.Contains("docs/test.md")); // Relative path
        Assert.Contains(messages, m => m.Contains("chars")); // Character count
        Assert.DoesNotContain(messages, m => m.Contains("/absolute/path")); // No absolute
    }

    [Fact]
    public void EmbeddingGeneration_DoesNotLogVectors()
    {
        // Arrange
        var embeddingLogger = new TestLogger<EmbeddingService>();
        var service = new EmbeddingService(embeddingLogger, /* other deps */);

        // Act
        var embedding = await service.GenerateEmbeddingAsync("test text");

        // Assert
        var messages = embeddingLogger.GetAllMessages();
        foreach (var message in messages)
        {
            // Embedding vectors are float arrays, should never appear
            Assert.DoesNotMatch(@"\[[\d\.\-,\s]+\]", message); // No float arrays
            Assert.DoesNotContain("0.123", message); // No vector values
        }
    }
}

/// <summary>
/// Test logger that captures all log messages for verification.
/// </summary>
public class TestLogger<T> : ILogger<T>
{
    private readonly List<string> _messages = new();

    public IReadOnlyList<string> GetAllMessages() => _messages.AsReadOnly();

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
        Exception? exception, Func<TState, Exception?, string> formatter)
    {
        _messages.Add(formatter(state, exception));
    }

    public bool IsEnabled(LogLevel logLevel) => true;
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        => NullScope.Instance;

    private class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();
        public void Dispose() { }
    }
}
```

### Code Review Checklist

Create `docs/LOGGING-GUIDELINES.md` with the following structure:

```markdown
# Logging Guidelines

## Quick Reference: What NOT to Log

| Category | Examples | Why |
|----------|----------|-----|
| Document Content | Markdown text, code snippets | Privacy, data exposure |
| Credentials | Passwords, API keys, tokens | Security breach risk |
| Connection Strings | Full DB connection strings | Credential exposure |
| Embedding Vectors | Float arrays from AI | Data leakage, noise |
| SQL with Data | `WHERE name = 'John'` | Data exposure |

## Quick Reference: Safe to Log

| Category | Examples | Purpose |
|----------|----------|---------|
| Relative Paths | `docs/api.md` | Debugging, tracing |
| Metadata | `{CharCount: 1234}` | Performance monitoring |
| Timing | `{ElapsedMs: 45}` | Performance analysis |
| Counts | `{ResultCount: 10}` | Operational metrics |
| IDs/Hashes | `{DocId: "abc123"}` | Correlation |

## PR Review Checklist

- [ ] No `LogInformation/Debug/Error` calls include content variables
- [ ] File paths use `SanitizePath()` before logging
- [ ] Exceptions use `SanitizeException()` or `LogSanitizedError()`
- [ ] Connection strings and config values are not logged
- [ ] Search queries log only the query text, not results
- [ ] Embedding operations log timing and counts only
```

---

## Dependencies

### Depends On

- **Phase 018**: Structured Logging Configuration (provides `ILogger<T>` setup, log level configuration)

### Blocks

- **Phase 021+**: Any phase implementing business logic with logging (ensures logging is secure from the start)

---

## Verification Steps

After completing this phase, verify:

1. **Unit tests pass**: All `LoggingSecurityTests` pass
2. **Sanitizer works**: `LogSanitizerTests` cover all edge cases
3. **Extension methods compile**: `LoggerExtensions` integrate with `ILogger<T>`
4. **Guidelines documented**: `LOGGING-GUIDELINES.md` exists and is comprehensive
5. **No regressions**: Existing logging statements are updated to use sanitization helpers

### Manual Verification

```bash
# Run logging security tests
dotnet test --filter "FullyQualifiedName~LoggingSecurityTests"

# Run sanitizer tests
dotnet test --filter "FullyQualifiedName~LogSanitizerTests"

# Verify no sensitive patterns in existing logs
grep -rn "LogInformation.*Content" src/ --include="*.cs"  # Should find none
grep -rn "LogDebug.*embedding" src/ --include="*.cs"      # Should find none with vectors
```

---

## Security Considerations

1. **Defense in depth**: Even with sanitization helpers, code review remains critical
2. **Test coverage**: Unit tests catch regressions but cannot catch all scenarios
3. **Production monitoring**: Consider log aggregation tools that can detect sensitive data patterns
4. **Rotation policy**: If sensitive data is ever logged, have a log rotation/purge policy

---

## Notes

- The `ILogSanitizer` should be registered as a singleton in DI for performance
- Consider making `LogSanitizer` thread-safe if needed for concurrent logging scenarios
- The test logger pattern can be reused across all service tests
- Regex patterns for credential detection should be reviewed periodically for new patterns
