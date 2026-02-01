# Phase 022: MCP stdio Transport Configuration

> **Status**: NOT_STARTED
> **Effort Estimate**: 2-3 hours
> **Category**: MCP Server Core
> **Prerequisites**: Phase 021 (MCP Server Host Setup)

---

## Spec References

This phase implements stdio transport configuration as defined in:

- **spec/mcp-server.md** - [Transport](../spec/mcp-server.md#transport) (lines 22-28)
- **spec/observability.md** - [Critical Constraint: stdio Transport](../spec/observability.md#critical-constraint-stdio-transport) (lines 27-38)
- **research/mcp-csharp-sdk-research.md** - [Transport Layer](../research/mcp-csharp-sdk-research.md#8-transport-layer) (lines 624-696)
- **research/dotnet-generic-host-mcp-research.md** - [Logging to stderr for stdio Transport](../research/dotnet-generic-host-mcp-research.md#critical-logging-to-stderr-for-stdio-transport) (lines 624-635)

---

## Objectives

1. Configure stdio transport for MCP protocol communication
2. Ensure stdout is reserved exclusively for MCP JSON-RPC messages
3. Redirect ALL logging to stderr (critical requirement)
4. Implement proper transport initialization and graceful shutdown
5. Configure console behavior for stdio mode compatibility

---

## Acceptance Criteria

### Transport Configuration

- [ ] MCP server uses `WithStdioServerTransport()` for transport registration
- [ ] Transport correctly uses stdin for receiving MCP requests
- [ ] Transport correctly uses stdout for sending MCP responses
- [ ] No application output (other than MCP protocol) goes to stdout

### Logging to stderr

- [ ] Console logger configured with `LogToStandardErrorThreshold = LogLevel.Trace`
- [ ] All log levels (Trace through Critical) output to stderr
- [ ] No log messages appear on stdout
- [ ] Logging configuration is set early in host startup

### Console Configuration

- [ ] Console input/output encoding set to UTF-8 for JSON compatibility
- [ ] Console buffering appropriate for stdio protocol
- [ ] Ctrl+C handler properly triggers graceful shutdown

### Transport Lifecycle

- [ ] Transport initializes correctly on host startup
- [ ] Transport handles graceful shutdown on SIGTERM/Ctrl+C
- [ ] Pending requests complete or timeout during shutdown
- [ ] Transport resources are properly disposed

---

## Implementation Notes

### stdio Transport Registration

The MCP C# SDK provides the `WithStdioServerTransport()` extension method:

```csharp
// Program.cs
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

var builder = Host.CreateApplicationBuilder(args);

// Transport registration
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()  // Uses stdin/stdout for MCP protocol
    .WithToolsFromAssembly();

await builder.Build().RunAsync();
```

### Critical: Logging Configuration

**All logging MUST go to stderr because stdout is reserved for MCP protocol messages.**

This is the most critical configuration for stdio transport:

```csharp
var builder = Host.CreateApplicationBuilder(args);

// CRITICAL: Route ALL logs to stderr BEFORE any other configuration
builder.Logging.ClearProviders();
builder.Logging.AddConsole(options =>
{
    // LogLevel.Trace ensures ALL log levels go to stderr
    options.LogToStandardErrorThreshold = LogLevel.Trace;
});

// Set minimum level and filters
builder.Logging.SetMinimumLevel(LogLevel.Information);
builder.Logging.AddFilter("Microsoft", LogLevel.Warning);
builder.Logging.AddFilter("System", LogLevel.Warning);
builder.Logging.AddFilter("CSharpCompoundDocs", LogLevel.Debug);
```

### LogToStandardErrorThreshold Values

| Value | Behavior |
|-------|----------|
| `LogLevel.None` (default) | All logs go to stdout (BREAKS MCP!) |
| `LogLevel.Trace` | ALL logs go to stderr (REQUIRED) |
| `LogLevel.Error` | Error/Critical to stderr, others to stdout |

**Always use `LogLevel.Trace` for MCP stdio servers.**

### Console Configuration for stdio

Ensure proper console encoding and behavior:

```csharp
// Set early in Program.cs, before host builder
Console.InputEncoding = System.Text.Encoding.UTF8;
Console.OutputEncoding = System.Text.Encoding.UTF8;
```

### Complete stdio Transport Setup

```csharp
// Program.cs
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using System.Text;

// Configure console encoding for JSON protocol
Console.InputEncoding = Encoding.UTF8;
Console.OutputEncoding = Encoding.UTF8;

var builder = Host.CreateApplicationBuilder(args);

// ===== CRITICAL: LOGGING TO STDERR =====
// Must be configured FIRST - stdout is reserved for MCP protocol
builder.Logging.ClearProviders();
builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Trace;
});

// Add debug output for development
if (builder.Environment.IsDevelopment())
{
    builder.Logging.AddDebug();
}

// Log level configuration
builder.Logging.SetMinimumLevel(LogLevel.Information);
builder.Logging.AddFilter("Microsoft", LogLevel.Warning);
builder.Logging.AddFilter("System", LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.Hosting.Lifetime", LogLevel.Information);
builder.Logging.AddFilter("CSharpCompoundDocs", LogLevel.Debug);

// ===== MCP SERVER WITH STDIO TRANSPORT =====
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

// ===== GRACEFUL SHUTDOWN TIMEOUT =====
builder.Services.Configure<HostOptions>(options =>
{
    options.ShutdownTimeout = TimeSpan.FromSeconds(30);
});

var host = builder.Build();

// Log startup (goes to stderr)
var logger = host.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("CSharp Compound Docs MCP Server starting...");

try
{
    await host.RunAsync();
}
finally
{
    logger.LogInformation("CSharp Compound Docs MCP Server stopped");
}
```

### appsettings.json Configuration

Logging can also be configured via appsettings.json:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "System": "Warning",
      "Microsoft.Hosting.Lifetime": "Information",
      "CSharpCompoundDocs": "Debug"
    },
    "Console": {
      "LogToStandardErrorThreshold": "Trace",
      "FormatterName": "simple",
      "FormatterOptions": {
        "SingleLine": true,
        "TimestampFormat": "HH:mm:ss.fff ",
        "UseUtcTimestamp": false
      }
    }
  }
}
```

### Transport Shutdown Handling

The Generic Host handles graceful shutdown automatically when:
- Ctrl+C is pressed
- SIGTERM is received
- `IHostApplicationLifetime.StopApplication()` is called

Configure appropriate shutdown timeout:

```csharp
builder.Services.Configure<HostOptions>(options =>
{
    // Allow 30 seconds for pending operations to complete
    options.ShutdownTimeout = TimeSpan.FromSeconds(30);
});
```

### Verification: No stdout Pollution

To verify that nothing is written to stdout except MCP messages:

1. Run the server and redirect stdout to a file
2. Check that only valid JSON-RPC messages appear
3. All log messages should appear in stderr

```bash
# Test stdout isolation
dotnet run > stdout.txt 2> stderr.txt

# stdout.txt should only contain MCP JSON-RPC messages
# stderr.txt should contain all log output
```

---

## Dependencies

### Depends On

- Phase 021: MCP Server Host Setup (provides the host builder and basic project structure)

### Blocks

- Phase 023: MCP Server Info Configuration (requires transport to be configured)
- All tool implementation phases (tools need the transport to operate)
- Phase 018: Logging (logging configuration is integral to stdio transport)

---

## Verification Steps

After completing this phase, verify:

### 1. Transport Initialization

```bash
# Server starts without errors
cd src/CompoundDocs.McpServer
dotnet run
# Should see startup log in stderr, server should be waiting for input
```

### 2. stdout Isolation Test

```bash
# Verify no log pollution on stdout
dotnet run > stdout.txt 2> stderr.txt &
sleep 2
kill $!

# Check files
cat stdout.txt  # Should be empty or only MCP initialization message
cat stderr.txt  # Should contain all startup logs
```

### 3. MCP Protocol Response

```bash
# Send a simple MCP initialize request
echo '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"test","version":"1.0"}}}' | dotnet run 2>/dev/null

# Should receive valid JSON-RPC response on stdout
```

### 4. Graceful Shutdown

```bash
# Start server in background
dotnet run &
SERVER_PID=$!

# Send SIGTERM
kill $SERVER_PID

# Server should shut down gracefully within timeout
wait $SERVER_PID
echo "Exit code: $?"
```

### 5. Log Level Verification

Verify logs appear at correct levels in stderr:

```csharp
// Temporary test in Program.cs
logger.LogTrace("Trace message - should appear in Development");
logger.LogDebug("Debug message - should appear");
logger.LogInformation("Info message - should appear");
logger.LogWarning("Warning message - should appear");
logger.LogError("Error message - should appear");
logger.LogCritical("Critical message - should appear");
```

---

## Common Issues and Solutions

### Issue: Log messages appear on stdout

**Cause**: `LogToStandardErrorThreshold` not set or set too high.

**Solution**: Ensure logging is configured BEFORE other services:

```csharp
// FIRST thing after creating builder
builder.Logging.ClearProviders();
builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);
```

### Issue: MCP client cannot communicate with server

**Cause**: stdout is polluted with non-JSON content.

**Solution**:
1. Verify `LogToStandardErrorThreshold` is set to `LogLevel.Trace`
2. Check for any `Console.WriteLine()` calls in application code
3. Ensure third-party libraries aren't writing to stdout

### Issue: Server crashes on shutdown

**Cause**: Insufficient shutdown timeout for pending operations.

**Solution**: Increase `ShutdownTimeout`:

```csharp
builder.Services.Configure<HostOptions>(options =>
{
    options.ShutdownTimeout = TimeSpan.FromSeconds(60);
});
```

### Issue: Encoding errors in MCP messages

**Cause**: Console encoding not set to UTF-8.

**Solution**: Set encoding at start of Program.cs:

```csharp
Console.InputEncoding = Encoding.UTF8;
Console.OutputEncoding = Encoding.UTF8;
```

---

## Testing Considerations

### Unit Tests

stdio transport configuration is primarily integration-level, but verify:

- Logging configuration produces expected providers
- Host options have correct shutdown timeout
- Console encoding is UTF-8

### Integration Tests

- Start server process, send MCP messages, verify responses
- Verify stdout contains only valid JSON-RPC
- Verify stderr contains expected log messages
- Test graceful shutdown with pending requests

---

## Notes

- The `ModelContextProtocol` package handles the low-level stdio communication
- `WithStdioServerTransport()` registers all necessary services automatically
- The transport uses `Console.In` and `Console.Out` streams internally
- For debugging, stderr output is essential since stdout cannot be used
- Consider adding a file logger for production environments (in addition to console/stderr)

---

## References

- [MCP C# SDK - Stdio Transport](https://github.com/modelcontextprotocol/csharp-sdk)
- [.NET Console Logging - LogToStandardErrorThreshold](https://learn.microsoft.com/en-us/dotnet/core/extensions/logging)
- [.NET Generic Host - Shutdown](https://learn.microsoft.com/en-us/dotnet/core/extensions/generic-host#host-shutdown)
