# Phase 031: Apple Silicon Detection and Handling

> **Status**: [PLANNED]
> **Category**: MCP Server Core
> **Estimated Effort**: S
> **Prerequisites**: Phase 029

---

## Spec References

- [spec/mcp-server/ollama-integration.md - Apple Silicon Note](../spec/mcp-server/ollama-integration.md#apple-silicon-note)
- [spec/infrastructure.md - Apple Silicon Note](../spec/infrastructure.md#apple-silicon-note)
- [spec/infrastructure.md - GPU Configuration Options](../spec/infrastructure.md#gpu-configuration-options)
- [research/ollama-docker-gpu-research.md](../research/ollama-docker-gpu-research.md)

---

## Objectives

1. Implement runtime detection of macOS ARM64 (Apple Silicon) architecture in the MCP server
2. Configure native Ollama expectation for Metal acceleration on Apple Silicon
3. Provide clear guidance for Docker vs native Ollama deployment
4. Generate appropriate error responses when Ollama is unavailable on Apple Silicon
5. Document configuration requirements for Apple Silicon users
6. Implement graceful fallback behavior when GPU/Metal acceleration is unavailable

---

## Acceptance Criteria

- [ ] MCP server detects macOS ARM64 architecture at runtime using .NET APIs
- [ ] Detection distinguishes between Apple Silicon (darwin-arm64) and Intel Mac (darwin-x64)
- [ ] Native Ollama at localhost:11434 is expected on Apple Silicon (not Docker at 11435)
- [ ] Clear error response returned when native Ollama not running on Apple Silicon
- [ ] Error includes platform identifier `darwin-arm64` and expected host
- [ ] Error message guides user to start Ollama natively before using the plugin
- [ ] Documentation covers Metal acceleration unavailability in Docker
- [ ] Fallback to CPU inference documented when Metal unavailable
- [ ] Platform detection is unit testable via interface abstraction
- [ ] Integration test verifies Apple Silicon detection behavior

---

## Implementation Notes

### 1. Platform Detection Service

Create an abstraction for platform detection to enable unit testing:

```csharp
/// <summary>
/// Platform detection for architecture-specific behavior.
/// </summary>
public interface IPlatformDetector
{
    /// <summary>
    /// Returns true if running on macOS with Apple Silicon (ARM64).
    /// </summary>
    bool IsAppleSilicon { get; }

    /// <summary>
    /// Returns the platform identifier (e.g., "darwin-arm64", "linux-x64").
    /// </summary>
    string PlatformIdentifier { get; }

    /// <summary>
    /// Returns true if running on macOS (any architecture).
    /// </summary>
    bool IsMacOS { get; }
}

/// <summary>
/// Production implementation using .NET runtime APIs.
/// </summary>
public class RuntimePlatformDetector : IPlatformDetector
{
    public bool IsAppleSilicon =>
        RuntimeInformation.IsOSPlatform(OSPlatform.OSX) &&
        RuntimeInformation.OSArchitecture == Architecture.Arm64;

    public bool IsMacOS =>
        RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

    public string PlatformIdentifier
    {
        get
        {
            var os = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "windows" :
                     RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "darwin" :
                     RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "linux" : "unknown";

            var arch = RuntimeInformation.OSArchitecture switch
            {
                Architecture.Arm64 => "arm64",
                Architecture.X64 => "x64",
                Architecture.X86 => "x86",
                Architecture.Arm => "arm",
                _ => "unknown"
            };

            return $"{os}-{arch}";
        }
    }
}
```

### 2. Apple Silicon Error Response

When the MCP server detects Apple Silicon and Ollama is not running:

```csharp
public record OllamaNotRunningError(
    string Platform,
    string ExpectedHost
);

/// <summary>
/// Creates an error response for missing Ollama on Apple Silicon.
/// </summary>
public static McpError CreateAppleSiliconOllamaError(string platform, string expectedHost)
{
    return new McpError
    {
        Code = "OLLAMA_NOT_RUNNING",
        Message = "Ollama server not detected. On Apple Silicon, Ollama must be running " +
                  "natively for Metal acceleration. Please start Ollama before using this tool.",
        Details = new OllamaNotRunningError(platform, expectedHost)
    };
}
```

**JSON Response Format**:

```json
{
  "error": true,
  "code": "OLLAMA_NOT_RUNNING",
  "message": "Ollama server not detected. On Apple Silicon, Ollama must be running natively for Metal acceleration. Please start Ollama before using this tool.",
  "details": {
    "platform": "darwin-arm64",
    "expected_host": "http://localhost:11434"
  }
}
```

### 3. Ollama Connection Strategy

The MCP server should use different connection strategies based on platform:

```csharp
public class OllamaConnectionStrategy
{
    private readonly IPlatformDetector _platform;
    private readonly IOptions<OllamaOptions> _options;

    public OllamaConnectionStrategy(
        IPlatformDetector platform,
        IOptions<OllamaOptions> options)
    {
        _platform = platform;
        _options = options;
    }

    /// <summary>
    /// Gets the Ollama endpoint based on platform and configuration.
    /// Apple Silicon expects native Ollama at default port 11434.
    /// Other platforms use the configured port (typically 11435 for Docker).
    /// </summary>
    public Uri GetOllamaEndpoint()
    {
        if (_platform.IsAppleSilicon)
        {
            // Native Ollama expected on Apple Silicon
            return new Uri("http://localhost:11434");
        }

        // Docker-based Ollama from launcher script configuration
        var host = _options.Value.Host ?? "localhost";
        var port = _options.Value.Port ?? 11435;
        return new Uri($"http://{host}:{port}");
    }

    /// <summary>
    /// Checks if Ollama is available at the expected endpoint.
    /// </summary>
    public async Task<bool> IsOllamaAvailableAsync(CancellationToken ct)
    {
        var endpoint = GetOllamaEndpoint();
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var response = await client.GetAsync($"{endpoint}api/tags", ct);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
```

### 4. Startup Validation

On MCP server startup, validate Ollama availability with platform-specific messaging:

```csharp
public class OllamaStartupValidator : IHostedService
{
    private readonly IPlatformDetector _platform;
    private readonly OllamaConnectionStrategy _connectionStrategy;
    private readonly ILogger<OllamaStartupValidator> _logger;

    public async Task StartAsync(CancellationToken ct)
    {
        var endpoint = _connectionStrategy.GetOllamaEndpoint();
        var available = await _connectionStrategy.IsOllamaAvailableAsync(ct);

        if (!available)
        {
            if (_platform.IsAppleSilicon)
            {
                _logger.LogWarning(
                    "Ollama not available at {Endpoint}. " +
                    "On Apple Silicon (darwin-arm64), Ollama must run natively " +
                    "to access Metal GPU acceleration. " +
                    "Docker cannot access Apple GPU hardware. " +
                    "Please start Ollama: https://ollama.ai/download",
                    endpoint);
            }
            else
            {
                _logger.LogWarning(
                    "Ollama not available at {Endpoint}. " +
                    "Embedding and RAG features will return errors until Ollama is running.",
                    endpoint);
            }
        }
        else
        {
            _logger.LogInformation(
                "Ollama available at {Endpoint} (platform: {Platform})",
                endpoint,
                _platform.PlatformIdentifier);
        }
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
```

### 5. Metal Acceleration Context

**Why Docker Cannot Use Metal on Apple Silicon**:

Docker Desktop on macOS runs containers in a Linux virtual machine. The Metal graphics API is macOS-specific and cannot be accessed from the Linux VM. This means:

| Environment | GPU Access | Performance |
|-------------|------------|-------------|
| Native Ollama on Apple Silicon | Metal acceleration | Fast (uses Neural Engine + GPU) |
| Docker Ollama on Apple Silicon | CPU only | 3-10x slower for inference |
| Docker Ollama on Linux + NVIDIA | CUDA acceleration | Fast |
| Docker Ollama on Linux + AMD | ROCm acceleration | Fast |

**Recommendation for Apple Silicon Users**:

1. Install Ollama natively from https://ollama.ai/download
2. Start Ollama before using the CSharp Compounding Docs plugin
3. The plugin will automatically detect native Ollama at localhost:11434

### 6. Configuration Documentation

Add to user documentation:

```markdown
## Apple Silicon Configuration

If you're running on a Mac with Apple Silicon (M1, M2, M3, M4):

### Why Native Ollama?

Apple Silicon Macs cannot use GPU acceleration in Docker containers because
Metal (Apple's GPU API) is not available inside Linux containers. Running
Ollama natively provides 3-10x better performance.

### Setup Steps

1. **Download Ollama**: https://ollama.ai/download
2. **Install and start Ollama** (it runs as a menu bar app)
3. **Verify Ollama is running**:
   ```bash
   curl http://localhost:11434/api/tags
   ```
4. The plugin will automatically detect native Ollama

### Troubleshooting

**Error: "Ollama server not detected"**
- Ensure Ollama is running (check menu bar)
- Verify with: `curl http://localhost:11434/api/tags`

**Slow inference despite native Ollama**
- Check model is loaded: `ollama list`
- Monitor GPU usage: Activity Monitor > GPU History
```

### 7. Fallback Behavior When GPU Unavailable

Even on Apple Silicon with native Ollama, Metal may be unavailable in some scenarios:

```csharp
public class GpuAvailabilityChecker
{
    /// <summary>
    /// Checks if GPU/Metal acceleration is actually being used.
    /// Note: This is informational only; Ollama handles fallback internally.
    /// </summary>
    public async Task<GpuStatus> CheckGpuStatusAsync(Uri ollamaEndpoint, CancellationToken ct)
    {
        try
        {
            using var client = new HttpClient();
            var response = await client.GetStringAsync($"{ollamaEndpoint}api/ps", ct);

            // Parse response to check if models are using GPU
            // Ollama's /api/ps shows processor info (cpu vs gpu/metal)
            // This is informational - the MCP server doesn't change behavior based on it

            return new GpuStatus { /* parsed info */ };
        }
        catch
        {
            return GpuStatus.Unknown;
        }
    }
}
```

**Fallback Documentation**:

When GPU is unavailable (rare on Apple Silicon with native Ollama):
- Ollama automatically falls back to CPU inference
- Performance will be slower but functionality is preserved
- No user action required unless startup validation warns about CPU mode

### 8. DI Registration

```csharp
public static class AppleSiliconExtensions
{
    public static IServiceCollection AddPlatformDetection(this IServiceCollection services)
    {
        services.AddSingleton<IPlatformDetector, RuntimePlatformDetector>();
        services.AddSingleton<OllamaConnectionStrategy>();
        services.AddHostedService<OllamaStartupValidator>();

        return services;
    }
}
```

---

## Dependencies

### Depends On

- **Phase 029**: MCP Server Host Setup - Basic server infrastructure must exist before adding platform detection

### Blocks

- **Phase 032+**: Ollama embedding integration - Needs correct endpoint resolution
- **Embedding service phases**: Must know which Ollama endpoint to use
- **RAG generation phases**: Must know which Ollama endpoint to use

---

## Testing Strategy

### Unit Tests

```csharp
public class PlatformDetectorTests
{
    [Fact]
    public void IsAppleSilicon_ReturnsExpectedValue()
    {
        var detector = new RuntimePlatformDetector();

        // This will return actual platform info
        // Use this test on different hardware to verify
        var result = detector.IsAppleSilicon;
        var identifier = detector.PlatformIdentifier;

        // Assert based on test environment
        if (identifier == "darwin-arm64")
        {
            Assert.True(result);
        }
    }
}

public class OllamaConnectionStrategyTests
{
    [Fact]
    public void GetOllamaEndpoint_AppleSilicon_ReturnsNativePort()
    {
        var mockPlatform = new Mock<IPlatformDetector>();
        mockPlatform.Setup(p => p.IsAppleSilicon).Returns(true);

        var options = Options.Create(new OllamaOptions { Port = 11435 });
        var strategy = new OllamaConnectionStrategy(mockPlatform.Object, options);

        var endpoint = strategy.GetOllamaEndpoint();

        Assert.Equal("http://localhost:11434/", endpoint.ToString());
    }

    [Fact]
    public void GetOllamaEndpoint_NotAppleSilicon_ReturnsConfiguredPort()
    {
        var mockPlatform = new Mock<IPlatformDetector>();
        mockPlatform.Setup(p => p.IsAppleSilicon).Returns(false);

        var options = Options.Create(new OllamaOptions { Host = "127.0.0.1", Port = 11435 });
        var strategy = new OllamaConnectionStrategy(mockPlatform.Object, options);

        var endpoint = strategy.GetOllamaEndpoint();

        Assert.Equal("http://127.0.0.1:11435/", endpoint.ToString());
    }
}
```

### Integration Tests

```csharp
[Trait("Category", "Integration")]
public class AppleSiliconIntegrationTests
{
    [SkippableFact]
    public async Task OllamaConnection_AppleSilicon_UsesNativeEndpoint()
    {
        var detector = new RuntimePlatformDetector();
        Skip.IfNot(detector.IsAppleSilicon, "Test requires Apple Silicon");

        // Test actual connection to native Ollama
        // This validates the full flow on Apple Silicon hardware
    }
}
```

---

## Files Created/Modified

| File | Action | Description |
|------|--------|-------------|
| `src/CompoundDocs.McpServer/Platform/IPlatformDetector.cs` | Create | Platform detection interface |
| `src/CompoundDocs.McpServer/Platform/RuntimePlatformDetector.cs` | Create | Production implementation |
| `src/CompoundDocs.McpServer/Ollama/OllamaConnectionStrategy.cs` | Create | Platform-aware endpoint resolution |
| `src/CompoundDocs.McpServer/Ollama/OllamaStartupValidator.cs` | Create | Startup health check with Apple Silicon messaging |
| `src/CompoundDocs.McpServer/Extensions/AppleSiliconExtensions.cs` | Create | DI registration helpers |
| `tests/CompoundDocs.McpServer.Tests/Platform/PlatformDetectorTests.cs` | Create | Unit tests for detection |
| `tests/CompoundDocs.McpServer.Tests/Ollama/OllamaConnectionStrategyTests.cs` | Create | Unit tests for strategy |
| `docs/apple-silicon-setup.md` | Create | User documentation for Apple Silicon |

---

## Risk Mitigation

| Risk | Mitigation |
|------|------------|
| User doesn't know to start native Ollama | Clear startup warning with download link |
| Docker Ollama accidentally started on Apple Silicon | Log warning about missing Metal acceleration |
| Native Ollama on non-standard port | Document how to modify configuration |
| Test coverage on Apple Silicon hardware | CI/CD needs macOS ARM64 runner or skip tests |
| Rosetta translation layer confusion | PlatformIdentifier clearly shows architecture |

---

## Verification Checklist

Before marking this phase complete:

1. [ ] `IPlatformDetector` interface created
2. [ ] `RuntimePlatformDetector` correctly detects Apple Silicon
3. [ ] `OllamaConnectionStrategy` returns correct endpoint for each platform
4. [ ] Startup validation logs appropriate warnings
5. [ ] Error response matches spec JSON format
6. [ ] Unit tests pass with mocked platform detector
7. [ ] Integration test works on Apple Silicon (if available)
8. [ ] User documentation complete
9. [ ] DI registration tested
