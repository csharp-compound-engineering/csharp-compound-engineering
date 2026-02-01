# Phase 028: Semantic Kernel Integration Setup

> **Status**: [PLANNED]
> **Category**: MCP Server Core
> **Estimated Effort**: L
> **Prerequisites**: Phase 021 (MCP Server Project Structure)

---

## Spec References

- [mcp-server.md - Semantic Kernel Integration](../spec/mcp-server.md#semantic-kernel-integration)
- [mcp-server.md - Service Interfaces](../spec/mcp-server.md#service-interfaces)
- [mcp-server.md - Testable Service Wrappers](../spec/mcp-server.md#testable-service-wrappers)
- [structure/mcp-server.md](../structure/mcp-server.md)
- [research/microsoft-semantic-kernel-research.md](../research/microsoft-semantic-kernel-research.md)
- [research/semantic-kernel-ollama-rag-research.md](../research/semantic-kernel-ollama-rag-research.md)

---

## Objectives

1. Add Microsoft.SemanticKernel NuGet package references to CompoundDocs.McpServer project
2. Configure Kernel builder with Ollama embedding and chat completion services
3. Integrate SK services with .NET dependency injection container
4. Create testable wrapper interfaces for SK services (IEmbeddingService, IChatService)
5. Implement production wrappers that delegate to SK services
6. Configure SK options via IOptions pattern from configuration
7. Handle experimental pragma warnings (SKEXP0070) appropriately

---

## Acceptance Criteria

- [ ] NuGet packages installed in CompoundDocs.McpServer project:
  - [ ] `Microsoft.SemanticKernel` (stable release)
  - [ ] `Microsoft.SemanticKernel.Connectors.Ollama` (prerelease/alpha)
  - [ ] `Microsoft.SemanticKernel.Connectors.PgVector` (prerelease)
  - [ ] `Microsoft.Extensions.VectorData.Abstractions` (preview)
- [ ] `Directory.Packages.props` updated with centralized package versions
- [ ] `SemanticKernelOptions` class created with configuration properties
- [ ] `IEmbeddingService` interface defined matching spec
- [ ] `IChatService` interface defined for RAG synthesis
- [ ] `SemanticKernelEmbeddingService` implementation wraps `ITextEmbeddingGenerationService`
- [ ] `SemanticKernelChatService` implementation wraps `IChatCompletionService`
- [ ] `IServiceCollection` extension method for SK registration
- [ ] Pragma warning suppression for experimental features (SKEXP0070)
- [ ] Unit tests for service wrappers using mocks
- [ ] Configuration binding from `appsettings.json` for Ollama endpoint and models

---

## Implementation Notes

### 1. NuGet Package References

Add to `Directory.Packages.props`:

```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>

  <ItemGroup>
    <!-- Semantic Kernel -->
    <PackageVersion Include="Microsoft.SemanticKernel" Version="1.69.0" />
    <PackageVersion Include="Microsoft.SemanticKernel.Connectors.Ollama" Version="1.68.0-alpha" />
    <PackageVersion Include="Microsoft.SemanticKernel.Connectors.PgVector" Version="1.70.0-preview" />
    <PackageVersion Include="Microsoft.Extensions.VectorData.Abstractions" Version="9.0.0-preview.1.25161.3" />

    <!-- Additional dependencies -->
    <PackageVersion Include="Npgsql" Version="9.0.2" />
    <PackageVersion Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="9.0.2" />
  </ItemGroup>
</Project>
```

Add to `src/CompoundDocs.McpServer/CompoundDocs.McpServer.csproj`:

```xml
<ItemGroup>
  <PackageReference Include="Microsoft.SemanticKernel" />
  <PackageReference Include="Microsoft.SemanticKernel.Connectors.Ollama" />
  <PackageReference Include="Microsoft.SemanticKernel.Connectors.PgVector" />
  <PackageReference Include="Microsoft.Extensions.VectorData.Abstractions" />
  <PackageReference Include="Npgsql" />
</ItemGroup>
```

### 2. Pragma Warning Handling

Create `src/CompoundDocs.McpServer/GlobalSuppressions.cs`:

```csharp
// Semantic Kernel Ollama connector is experimental
// This suppresses the warning at the assembly level
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage(
    "SKEXP0070",
    "SKEXP0070:Ollama connector is experimental",
    Justification = "Required for Ollama integration per spec")]
```

Alternatively, use project-level suppression in `.csproj`:

```xml
<PropertyGroup>
  <NoWarn>$(NoWarn);SKEXP0070</NoWarn>
</PropertyGroup>
```

### 3. SemanticKernelOptions Configuration Class

Create `src/CompoundDocs.McpServer/Configuration/SemanticKernelOptions.cs`:

```csharp
namespace CompoundDocs.McpServer.Configuration;

/// <summary>
/// Configuration options for Semantic Kernel services.
/// </summary>
public sealed class SemanticKernelOptions
{
    /// <summary>
    /// Configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "SemanticKernel";

    /// <summary>
    /// Ollama service endpoint (e.g., "http://localhost:11435").
    /// </summary>
    public required string OllamaEndpoint { get; init; }

    /// <summary>
    /// Embedding model name. Fixed to "mxbai-embed-large" per spec.
    /// </summary>
    public string EmbeddingModel { get; init; } = "mxbai-embed-large";

    /// <summary>
    /// Embedding dimensions. Must match model output (1024 for mxbai-embed-large).
    /// </summary>
    public int EmbeddingDimensions { get; init; } = 1024;

    /// <summary>
    /// Chat completion model for RAG synthesis.
    /// </summary>
    public string ChatModel { get; init; } = "mistral";

    /// <summary>
    /// PostgreSQL connection string for vector store.
    /// </summary>
    public required string PostgresConnectionString { get; init; }

    /// <summary>
    /// Timeout for embedding operations in seconds.
    /// </summary>
    public int EmbeddingTimeoutSeconds { get; init; } = 300;

    /// <summary>
    /// Timeout for chat completion operations in seconds.
    /// </summary>
    public int ChatTimeoutSeconds { get; init; } = 120;
}
```

### 4. IEmbeddingService Interface

Create `src/CompoundDocs.McpServer/Services/Abstractions/IEmbeddingService.cs`:

```csharp
namespace CompoundDocs.McpServer.Services.Abstractions;

/// <summary>
/// Abstraction over embedding generation for testability.
/// Wraps Semantic Kernel's ITextEmbeddingGenerationService.
/// </summary>
public interface IEmbeddingService
{
    /// <summary>
    /// Generate embedding vector for the given content.
    /// </summary>
    /// <param name="content">Text content to embed.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>1024-dimension embedding vector.</returns>
    Task<ReadOnlyMemory<float>> GenerateEmbeddingAsync(
        string content,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate embeddings for multiple texts in a batch.
    /// </summary>
    /// <param name="contents">Text contents to embed.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of embedding vectors in same order as input.</returns>
    Task<IReadOnlyList<ReadOnlyMemory<float>>> GenerateEmbeddingsAsync(
        IReadOnlyList<string> contents,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Expected embedding dimensions (1024 for mxbai-embed-large).
    /// </summary>
    int Dimensions { get; }
}
```

### 5. IChatService Interface

Create `src/CompoundDocs.McpServer/Services/Abstractions/IChatService.cs`:

```csharp
using Microsoft.SemanticKernel.ChatCompletion;

namespace CompoundDocs.McpServer.Services.Abstractions;

/// <summary>
/// Abstraction over chat completion for testability.
/// Used for RAG response generation.
/// </summary>
public interface IChatService
{
    /// <summary>
    /// Generate a chat response given conversation history.
    /// </summary>
    /// <param name="chatHistory">Conversation history including system prompt and context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Generated response content.</returns>
    Task<string> GetCompletionAsync(
        ChatHistory chatHistory,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate a streaming chat response.
    /// </summary>
    /// <param name="chatHistory">Conversation history.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Async enumerable of response chunks.</returns>
    IAsyncEnumerable<string> GetStreamingCompletionAsync(
        ChatHistory chatHistory,
        CancellationToken cancellationToken = default);
}
```

### 6. SemanticKernelEmbeddingService Implementation

Create `src/CompoundDocs.McpServer/Services/SemanticKernelEmbeddingService.cs`:

```csharp
#pragma warning disable SKEXP0070

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel.Embeddings;
using CompoundDocs.McpServer.Configuration;
using CompoundDocs.McpServer.Services.Abstractions;

namespace CompoundDocs.McpServer.Services;

/// <summary>
/// Production implementation of IEmbeddingService using Semantic Kernel.
/// </summary>
public sealed class SemanticKernelEmbeddingService : IEmbeddingService
{
    private readonly ITextEmbeddingGenerationService _skEmbeddingService;
    private readonly ILogger<SemanticKernelEmbeddingService> _logger;
    private readonly int _dimensions;

    public SemanticKernelEmbeddingService(
        ITextEmbeddingGenerationService skEmbeddingService,
        IOptions<SemanticKernelOptions> options,
        ILogger<SemanticKernelEmbeddingService> logger)
    {
        _skEmbeddingService = skEmbeddingService ?? throw new ArgumentNullException(nameof(skEmbeddingService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _dimensions = options.Value.EmbeddingDimensions;
    }

    public int Dimensions => _dimensions;

    public async Task<ReadOnlyMemory<float>> GenerateEmbeddingAsync(
        string content,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);

        _logger.LogDebug("Generating embedding for content of length {ContentLength}", content.Length);

        var embedding = await _skEmbeddingService.GenerateEmbeddingAsync(
            content,
            kernel: null,
            cancellationToken);

        if (embedding.Length != _dimensions)
        {
            _logger.LogWarning(
                "Embedding dimension mismatch. Expected {Expected}, got {Actual}",
                _dimensions,
                embedding.Length);
        }

        return embedding;
    }

    public async Task<IReadOnlyList<ReadOnlyMemory<float>>> GenerateEmbeddingsAsync(
        IReadOnlyList<string> contents,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contents);

        if (contents.Count == 0)
        {
            return Array.Empty<ReadOnlyMemory<float>>();
        }

        _logger.LogDebug("Generating batch embeddings for {Count} items", contents.Count);

        var embeddings = await _skEmbeddingService.GenerateEmbeddingsAsync(
            contents.ToList(),
            kernel: null,
            cancellationToken);

        return embeddings.ToList().AsReadOnly();
    }
}
```

### 7. SemanticKernelChatService Implementation

Create `src/CompoundDocs.McpServer/Services/SemanticKernelChatService.cs`:

```csharp
#pragma warning disable SKEXP0070

using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.ChatCompletion;
using CompoundDocs.McpServer.Services.Abstractions;

namespace CompoundDocs.McpServer.Services;

/// <summary>
/// Production implementation of IChatService using Semantic Kernel.
/// </summary>
public sealed class SemanticKernelChatService : IChatService
{
    private readonly IChatCompletionService _skChatService;
    private readonly ILogger<SemanticKernelChatService> _logger;

    public SemanticKernelChatService(
        IChatCompletionService skChatService,
        ILogger<SemanticKernelChatService> logger)
    {
        _skChatService = skChatService ?? throw new ArgumentNullException(nameof(skChatService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<string> GetCompletionAsync(
        ChatHistory chatHistory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(chatHistory);

        _logger.LogDebug(
            "Requesting chat completion with {MessageCount} messages",
            chatHistory.Count);

        var response = await _skChatService.GetChatMessageContentAsync(
            chatHistory,
            executionSettings: null,
            kernel: null,
            cancellationToken);

        return response.Content ?? string.Empty;
    }

    public async IAsyncEnumerable<string> GetStreamingCompletionAsync(
        ChatHistory chatHistory,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(chatHistory);

        _logger.LogDebug(
            "Requesting streaming chat completion with {MessageCount} messages",
            chatHistory.Count);

        await foreach (var chunk in _skChatService.GetStreamingChatMessageContentsAsync(
            chatHistory,
            executionSettings: null,
            kernel: null,
            cancellationToken))
        {
            if (!string.IsNullOrEmpty(chunk.Content))
            {
                yield return chunk.Content;
            }
        }
    }
}
```

### 8. Dependency Injection Extension Method

Create `src/CompoundDocs.McpServer/Extensions/SemanticKernelServiceCollectionExtensions.cs`:

```csharp
#pragma warning disable SKEXP0070

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Npgsql;
using CompoundDocs.McpServer.Configuration;
using CompoundDocs.McpServer.Services;
using CompoundDocs.McpServer.Services.Abstractions;

namespace CompoundDocs.McpServer.Extensions;

/// <summary>
/// Extension methods for registering Semantic Kernel services.
/// </summary>
public static class SemanticKernelServiceCollectionExtensions
{
    /// <summary>
    /// Adds Semantic Kernel services to the dependency injection container.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <param name="configuration">Application configuration.</param>
    /// <returns>Service collection for chaining.</returns>
    public static IServiceCollection AddSemanticKernelServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Bind configuration
        services.Configure<SemanticKernelOptions>(
            configuration.GetSection(SemanticKernelOptions.SectionName));

        var options = configuration
            .GetSection(SemanticKernelOptions.SectionName)
            .Get<SemanticKernelOptions>()
            ?? throw new InvalidOperationException(
                $"Missing {SemanticKernelOptions.SectionName} configuration section");

        // Register NpgsqlDataSource for vector store
        services.AddSingleton<NpgsqlDataSource>(sp =>
        {
            var dataSourceBuilder = new NpgsqlDataSourceBuilder(options.PostgresConnectionString);
            dataSourceBuilder.UseVector();
            return dataSourceBuilder.Build();
        });

        // Build and register Kernel
        services.AddSingleton<Kernel>(sp =>
        {
            var kernelBuilder = Kernel.CreateBuilder();

            // Add Ollama embedding generation
            kernelBuilder.AddOllamaTextEmbeddingGeneration(
                modelId: options.EmbeddingModel,
                endpoint: new Uri(options.OllamaEndpoint),
                serviceId: "ollama-embeddings");

            // Add Ollama chat completion
            kernelBuilder.AddOllamaChatCompletion(
                modelId: options.ChatModel,
                endpoint: new Uri(options.OllamaEndpoint),
                serviceId: "ollama-chat");

            return kernelBuilder.Build();
        });

        // Register SK services extracted from kernel
        services.AddSingleton(sp =>
        {
            var kernel = sp.GetRequiredService<Kernel>();
            return kernel.GetRequiredService<Microsoft.SemanticKernel.Embeddings.ITextEmbeddingGenerationService>();
        });

        services.AddSingleton(sp =>
        {
            var kernel = sp.GetRequiredService<Kernel>();
            return kernel.GetRequiredService<Microsoft.SemanticKernel.ChatCompletion.IChatCompletionService>();
        });

        // Register wrapper services
        services.AddSingleton<IEmbeddingService, SemanticKernelEmbeddingService>();
        services.AddSingleton<IChatService, SemanticKernelChatService>();

        return services;
    }
}
```

### 9. Configuration in appsettings.json

Add to `src/CompoundDocs.McpServer/appsettings.json`:

```json
{
  "SemanticKernel": {
    "OllamaEndpoint": "http://localhost:11435",
    "EmbeddingModel": "mxbai-embed-large",
    "EmbeddingDimensions": 1024,
    "ChatModel": "mistral",
    "PostgresConnectionString": "Host=localhost;Port=5433;Database=compoundingdocs;Username=compoundingdocs;Password=compoundingdocs",
    "EmbeddingTimeoutSeconds": 300,
    "ChatTimeoutSeconds": 120
  }
}
```

### 10. Unit Test Structure

Create `tests/CompoundDocs.Tests/Services/SemanticKernelEmbeddingServiceTests.cs`:

```csharp
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel.Embeddings;
using Moq;
using CompoundDocs.McpServer.Configuration;
using CompoundDocs.McpServer.Services;

namespace CompoundDocs.Tests.Services;

public class SemanticKernelEmbeddingServiceTests
{
    private readonly Mock<ITextEmbeddingGenerationService> _mockSkService;
    private readonly Mock<ILogger<SemanticKernelEmbeddingService>> _mockLogger;
    private readonly IOptions<SemanticKernelOptions> _options;
    private readonly SemanticKernelEmbeddingService _sut;

    public SemanticKernelEmbeddingServiceTests()
    {
        _mockSkService = new Mock<ITextEmbeddingGenerationService>();
        _mockLogger = new Mock<ILogger<SemanticKernelEmbeddingService>>();
        _options = Options.Create(new SemanticKernelOptions
        {
            OllamaEndpoint = "http://localhost:11435",
            PostgresConnectionString = "Host=localhost",
            EmbeddingDimensions = 1024
        });

        _sut = new SemanticKernelEmbeddingService(
            _mockSkService.Object,
            _options,
            _mockLogger.Object);
    }

    [Fact]
    public async Task GenerateEmbeddingAsync_ReturnsEmbedding_WhenContentValid()
    {
        // Arrange
        var content = "Test content";
        var expectedEmbedding = new ReadOnlyMemory<float>(new float[1024]);

        _mockSkService
            .Setup(x => x.GenerateEmbeddingAsync(content, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedEmbedding);

        // Act
        var result = await _sut.GenerateEmbeddingAsync(content);

        // Assert
        Assert.Equal(expectedEmbedding.Length, result.Length);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GenerateEmbeddingAsync_ThrowsArgumentException_WhenContentInvalid(string? content)
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _sut.GenerateEmbeddingAsync(content!));
    }

    [Fact]
    public void Dimensions_Returns1024()
    {
        Assert.Equal(1024, _sut.Dimensions);
    }
}
```

---

## Dependencies

### Depends On

- **Phase 021**: MCP Server Project Structure - Project must exist before adding packages

### Blocks

- **Phase 029**: PostgreSQL Vector Store Integration - Requires SK configuration
- **Phase 030**: Ollama Integration Implementation - Requires embedding/chat services
- **Phase 031**: RAG Pipeline Implementation - Requires all SK services
- **Phase 032+**: MCP Tool Implementations - Require RAG pipeline

---

## Testing Verification

After implementation, verify with:

```bash
# 1. Build succeeds with new packages
dotnet build src/CompoundDocs.McpServer/

# 2. Run unit tests
dotnet test tests/CompoundDocs.Tests/ --filter "FullyQualifiedName~SemanticKernel"

# 3. Verify no SKEXP0070 warnings in build output
dotnet build src/CompoundDocs.McpServer/ 2>&1 | grep -i "SKEXP"

# 4. Check package references
dotnet list src/CompoundDocs.McpServer/ package | grep -i "SemanticKernel"
```

---

## Files Created/Modified

| File | Action | Description |
|------|--------|-------------|
| `Directory.Packages.props` | Modify | Add SK package versions |
| `src/CompoundDocs.McpServer/CompoundDocs.McpServer.csproj` | Modify | Add SK package references |
| `src/CompoundDocs.McpServer/GlobalSuppressions.cs` | Create | Pragma warning suppressions |
| `src/CompoundDocs.McpServer/Configuration/SemanticKernelOptions.cs` | Create | SK configuration class |
| `src/CompoundDocs.McpServer/Services/Abstractions/IEmbeddingService.cs` | Create | Embedding service interface |
| `src/CompoundDocs.McpServer/Services/Abstractions/IChatService.cs` | Create | Chat service interface |
| `src/CompoundDocs.McpServer/Services/SemanticKernelEmbeddingService.cs` | Create | Embedding service implementation |
| `src/CompoundDocs.McpServer/Services/SemanticKernelChatService.cs` | Create | Chat service implementation |
| `src/CompoundDocs.McpServer/Extensions/SemanticKernelServiceCollectionExtensions.cs` | Create | DI registration extension |
| `src/CompoundDocs.McpServer/appsettings.json` | Modify | Add SK configuration section |
| `tests/CompoundDocs.Tests/Services/SemanticKernelEmbeddingServiceTests.cs` | Create | Unit tests |
| `tests/CompoundDocs.Tests/Services/SemanticKernelChatServiceTests.cs` | Create | Unit tests |

---

## Risk Mitigation

| Risk | Mitigation |
|------|------------|
| SK package version incompatibility | Pin specific versions in Directory.Packages.props |
| Ollama connector API changes (alpha) | Isolate behind IEmbeddingService/IChatService interfaces |
| Experimental warning noise | Suppress at assembly level with justification |
| Configuration binding failures | Validate configuration at startup with clear error messages |
| NpgsqlDataSource lifecycle | Register as singleton, dispose on shutdown |
| Test isolation from SK | Use mocks for ITextEmbeddingGenerationService/IChatCompletionService |

---

## Notes

- The Ollama connector is experimental (alpha), so breaking changes are possible in future SK releases
- The wrapper interfaces (IEmbeddingService, IChatService) provide insulation from SK API changes
- PgVector connector is in preview; namespace is still `Microsoft.SemanticKernel.Connectors.Postgres` despite package rename
- Kernel is registered as singleton because it's thread-safe and expensive to create
- NpgsqlDataSource must call `UseVector()` before building to enable pgvector support
