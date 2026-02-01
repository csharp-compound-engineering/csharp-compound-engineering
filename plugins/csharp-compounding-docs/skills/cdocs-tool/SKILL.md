---
name: cdocs:tool
description: Captures tool and library knowledge including gotchas, workarounds, and configuration
allowed-tools:
  - Read
  - Write
  - Bash
preconditions:
  - Project activated via /cdocs:activate
  - Tool/library gotcha or knowledge has been discovered
auto-invoke:
  trigger: conversation-pattern
  patterns:
    - "gotcha"
    - "watch out for"
    - "careful with"
    - "heads up"
    - "workaround"
    - "dependency"
    - "library"
    - "package"
    - "NuGet"
---

# Tool Documentation Skill

## Intake

This skill captures knowledge about tools, libraries, frameworks, and their usage. It expects the following context from the conversation:

- **Tool/library name**: What tool or library is this about?
- **Version**: Which version exhibits this behavior?
- **Knowledge type**: Gotcha, configuration, integration, performance, or workaround?
- **Description**: What is the key knowledge to capture?
- **Context**: When or how does this occur?

## Process

### Step 1: Gather Context

Extract from conversation history:
- **Tool name**: Which tool/library/framework?
- **Version**: Specific version where behavior observed (important for version-specific issues)
- **Knowledge type**: Is this a gotcha, configuration tip, integration note, performance issue, or workaround?
- **Description**: What is the key insight?
- **Setup requirements**: Any prerequisites or dependencies?
- **Code examples**: Sample code demonstrating usage or workaround
- **Official docs gap**: Is this information missing from official documentation?

Use Sequential Thinking MCP to:
- Determine if issue is version-specific or general
- Identify root cause of gotcha or limitation
- Evaluate if workaround is safe long-term
- Check if official documentation covers this

**BLOCKING**: If tool name or key knowledge is unclear, ask the user to clarify and WAIT for response.

### Step 2: Validate Schema

Load `schema.yaml` for the tool doc-type.
Validate required fields:
- `doc_type` = "tool"
- `title` (1-200 chars)
- `tool_name` (1-100 chars)

Validate optional fields:
- `version`: must match semver pattern (e.g., "1.2.3" or "1.2.3-beta")
- `promotion_level`: must be one of ["standard", "promoted", "pinned"]

**BLOCK if validation fails** - show specific schema violations to the user.

### Step 3: Write Documentation

1. Generate filename: `{sanitized-title}-{YYYYMMDD}.md`
   - Sanitize title: lowercase, replace spaces with hyphens, remove special chars
   - Example: `entity-framework-lazy-loading-gotcha-20250125.md`

2. Create directory if needed:
   ```bash
   mkdir -p ./csharp-compounding-docs/tools/
   ```

3. Write file with YAML frontmatter + markdown body:
   ```markdown
   ---
   doc_type: tool
   title: "Entity Framework lazy loading gotcha with async"
   tool_name: "Entity Framework Core"
   version: "8.0.1"
   tags: ["entity-framework", "async", "gotcha"]
   date: 2025-01-25
   ---

   # Entity Framework lazy loading gotcha with async

   ## Overview

   [Brief description of the tool knowledge]

   ## The Issue/Gotcha

   [Detailed description of the problem, limitation, or gotcha]

   ## Why This Happens

   [Root cause or technical explanation]

   ## Solution/Workaround

   [How to handle this correctly]

   ## Code Example

   ```csharp
   // Example code
   ```

   ## Version Information

   - Observed in: [version]
   - Still present in: [version]
   - Fixed in: [version] (if applicable)

   ## Additional Notes

   [Any other relevant information]
   ```

4. Use Sequential Thinking MCP when:
   - Determining if issue is version-specific or configuration-related
   - Analyzing root cause of gotcha
   - Evaluating safety/permanence of workaround
   - Checking gaps in official documentation

### Step 4: Post-Capture Options

After successfully writing the document:

```
✓ Tool documentation captured

File created: ./csharp-compounding-docs/tools/{filename}.md

What's next?
1. Continue workflow
2. Link related docs (use /cdocs:related)
3. View documentation
4. Capture another tool gotcha
```

Wait for user selection.

## Schema Reference

See `schema.yaml` in this directory for the complete tool document schema.

Required fields:
- `doc_type`: "tool"
- `title`: string (1-200 chars)
- `tool_name`: string (1-100 chars) - name of the tool

Optional fields:
- `version`: string matching semver pattern (e.g., "1.2.3" or "1.2.3-beta")
- `setup_requirements`: array of strings (max 500 chars each)
- `promotion_level`: enum ["standard", "promoted", "pinned"] (default: "standard")
- `links`: array of URIs
- `tags`: array of strings (max 50 chars each)
- `usage`: string (max 2000 chars) - brief usage instructions

## Examples

### Example 1: Library Gotcha

```markdown
---
doc_type: tool
title: "MassTransit consumer exceptions trigger infinite retries by default"
tool_name: "MassTransit"
version: "8.1.0"
tags: ["masstransit", "messaging", "error-handling", "gotcha"]
date: 2025-01-20
links:
  - "https://masstransit.io/documentation/configuration/exceptions"
---

# MassTransit consumer exceptions trigger infinite retries by default

## Overview

MassTransit consumers that throw exceptions will retry indefinitely by default, potentially causing message processing to stall and queues to back up.

## The Issue/Gotcha

When a consumer throws an exception during message processing, MassTransit's default behavior is to retry the message with exponential backoff - **forever**. There is no default maximum retry count.

This caught us when a consumer threw a `JsonException` due to an incompatible message format change. The message was retried continuously for hours until we noticed queue depth alerts.

## Why This Happens

MassTransit philosophy is to be resilient to transient failures (network issues, database timeouts, etc.) which may resolve themselves. The framework assumes you want to keep retrying until successful.

However, this doesn't work well for:
- Deserialization errors (malformed messages)
- Business validation failures
- Incompatible schema versions

These errors won't resolve with retries, so the message gets stuck.

## Solution/Workaround

### Option 1: Configure Retry Policy (Recommended)

Set explicit retry limits in consumer configuration:

```csharp
cfg.ReceiveEndpoint("document-queue", e =>
{
    e.UseMessageRetry(r =>
    {
        r.Incremental(5, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2));
    });

    e.ConfigureConsumer<DocumentConsumer>(context);
});
```

This retries 5 times with increasing delays, then moves to error queue.

### Option 2: Fault Handling

Configure fault handling to move failed messages:

```csharp
cfg.ReceiveEndpoint("document-queue", e =>
{
    e.UseMessageRetry(r => r.Immediate(3));

    e.ConfigureFault(f =>
    {
        f.MoveFaultedMessagesToErrorQueue = true;
    });

    e.ConfigureConsumer<DocumentConsumer>(context);
});
```

### Option 3: Selective Retry

Handle different exception types differently:

```csharp
cfg.ReceiveEndpoint("document-queue", e =>
{
    e.UseMessageRetry(r =>
    {
        r.Incremental(5, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2));

        // Don't retry these - fail immediately
        r.Ignore<JsonException>();
        r.Ignore<ValidationException>();
    });

    e.ConfigureConsumer<DocumentConsumer>(context);
});
```

## Code Example

Complete working example:

```csharp
public class MassTransitConfiguration
{
    public static void ConfigureEndpoint(IRabbitMqBusFactoryConfigurator cfg)
    {
        cfg.ReceiveEndpoint("document-processing", e =>
        {
            // Retry policy: 5 attempts with exponential backoff
            e.UseMessageRetry(r =>
            {
                r.Incremental(
                    retryLimit: 5,
                    initialInterval: TimeSpan.FromSeconds(1),
                    intervalIncrement: TimeSpan.FromSeconds(2)
                );

                // Skip retry for exceptions that won't resolve
                r.Ignore<JsonException>();
                r.Ignore<ArgumentException>();
                r.Ignore<ValidationException>();
            });

            // Configure what happens after all retries exhausted
            e.ConfigureFault(f =>
            {
                f.MoveFaultedMessagesToErrorQueue = true;
            });

            // Register consumer
            e.ConfigureConsumer<DocumentProcessingConsumer>(context);
        });
    }
}
```

## Version Information

- Observed in: 8.1.0
- Still present in: 8.2.0 (latest at time of writing)
- This is intended behavior, not a bug

## Additional Notes

**Best Practice**: Always configure explicit retry policies for production consumers. The defaults are meant for development/testing.

**Monitoring**: Set up alerts on:
- Queue depth (indicates messages not processing)
- Error queue depth (indicates permanent failures)
- Consumer processing time (indicates retry loops)

**Related Documentation**: Official docs mention this briefly but don't emphasize how critical it is to configure retry policies. Easy to miss during initial setup.
```

### Example 2: Configuration Tip

```markdown
---
doc_type: tool
title: "Serilog file size rollover requires shared setting"
tool_name: "Serilog"
version: "3.1.1"
tags: ["serilog", "logging", "configuration"]
date: 2025-01-22
setup_requirements:
  - "Serilog.Sinks.File NuGet package"
---

# Serilog file size rollover requires shared setting

## Overview

When using Serilog's file sink with size-based rollover, you must set `shared: true` if multiple processes write to the same log file, otherwise rollover fails silently.

## The Issue/Gotcha

We had multiple worker processes writing to the same log file with size-based rollover:

```csharp
Log.Logger = new LoggerConfiguration()
    .WriteTo.File("logs/app.log",
        rollingInterval: RollingInterval.Infinite,
        rollOnFileSizeLimit: true,
        fileSizeLimitBytes: 10_000_000)
    .CreateLogger();
```

When the file reached 10MB, rollover would fail silently. Logs continued appending to the original file, which grew to 500MB+ before we noticed.

## Why This Happens

Serilog uses a file handle lock to coordinate rollover. When multiple processes access the same file, they each maintain their own file handle. The default settings don't coordinate between processes, so:

1. Process A checks file size, sees it's at limit
2. Process A attempts to rename file for rollover
3. Process B still has file open, rename fails
4. Process A continues writing to original file (no exception thrown)

## Solution/Workaround

Set `shared: true` when multiple processes write to the same file:

```csharp
Log.Logger = new LoggerConfiguration()
    .WriteTo.File("logs/app.log",
        rollingInterval: RollingInterval.Infinite,
        rollOnFileSizeLimit: true,
        fileSizeLimitBytes: 10_000_000,
        shared: true)  // ← This is critical
    .CreateLogger();
```

With `shared: true`, Serilog uses inter-process synchronization to coordinate access and rollover.

## Code Example

Complete configuration for multi-process logging:

```csharp
public static class LoggingConfiguration
{
    public static void ConfigureSerilog()
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithProcessId()
            .WriteTo.File(
                path: "logs/app-.log",
                rollingInterval: RollingInterval.Day,
                rollOnFileSizeLimit: true,
                fileSizeLimitBytes: 10_485_760, // 10 MB
                retainedFileCountLimit: 30,
                shared: true,  // Required for multiple processes
                flushToDiskInterval: TimeSpan.FromSeconds(1))
            .WriteTo.Console()
            .CreateLogger();
    }
}
```

## Version Information

- Observed in: 3.1.1
- Behavior unchanged since: 2.x (long-standing behavior)

## Additional Notes

**Performance**: The `shared: true` setting has a small performance overhead due to inter-process locking. For high-throughput scenarios, consider:
- Separate log files per process
- Centralized logging (e.g., Seq, Elasticsearch)
- Async sink wrapper

**Silent Failure**: This is by design - Serilog prioritizes not throwing exceptions during logging. Monitor actual log file sizes to detect rollover failures.

**Official Docs**: The `shared` parameter is documented but the multi-process requirement isn't prominently mentioned in size rollover examples.
```

### Example 3: Integration Workaround

```markdown
---
doc_type: tool
title: "Polly retry policy with HttpClientFactory requires specific registration"
tool_name: "Polly"
version: "8.2.0"
tags: ["polly", "httpclient", "resilience", "dependency-injection"]
date: 2025-01-25
setup_requirements:
  - "Microsoft.Extensions.Http.Polly NuGet package"
  - "Polly NuGet package"
usage: "Resilience and transient fault handling for HttpClient"
---

# Polly retry policy with HttpClientFactory requires specific registration

## Overview

When integrating Polly retry policies with HttpClientFactory in .NET, the policy must be registered using `AddPolicyHandler()` - using Polly directly in DI doesn't work as expected.

## The Issue/Gotcha

We tried to register a Polly retry policy and inject it into our typed HttpClient:

```csharp
// ❌ This doesn't work
services.AddSingleton<IAsyncPolicy<HttpResponseMessage>>(sp =>
    HttpPolicyExtensions
        .HandleTransientHttpError()
        .WaitAndRetryAsync(3, retryAttempt =>
            TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)))
);

services.AddHttpClient<IDocumentApiClient, DocumentApiClient>();
```

The retry policy was never applied. HTTP requests failed on first error without retries.

## Why This Happens

HttpClientFactory uses its own handler pipeline. Polly policies must be registered **on the HttpClient registration** using `AddPolicyHandler()`, not separately in DI.

The HttpClientFactory creates the handler pipeline during client construction. Policies registered separately via DI aren't part of this pipeline.

## Solution/Workaround

Register the policy directly on the HttpClient:

```csharp
services.AddHttpClient<IDocumentApiClient, DocumentApiClient>()
    .AddPolicyHandler(GetRetryPolicy());

static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
{
    return HttpPolicyExtensions
        .HandleTransientHttpError()
        .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
        .WaitAndRetryAsync(
            retryCount: 3,
            sleepDurationProvider: retryAttempt =>
                TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
            onRetry: (outcome, timespan, retryCount, context) =>
            {
                Log.Warning(
                    "Retry {RetryCount} after {Delay}s due to {StatusCode}",
                    retryCount, timespan.TotalSeconds, outcome.Result?.StatusCode);
            });
}
```

## Code Example

Complete working example with multiple policies:

```csharp
public static class HttpClientConfiguration
{
    public static void ConfigureDocumentApiClient(IServiceCollection services)
    {
        services.AddHttpClient<IDocumentApiClient, DocumentApiClient>(client =>
        {
            client.BaseAddress = new Uri("https://api.example.com");
            client.Timeout = TimeSpan.FromSeconds(30);
        })
        .AddPolicyHandler(GetRetryPolicy())
        .AddPolicyHandler(GetCircuitBreakerPolicy())
        .AddPolicyHandler(GetTimeoutPolicy());
    }

    private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt =>
                    TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    Log.Warning(
                        "Delaying for {Delay}s before retry {RetryCount}",
                        timespan.TotalSeconds, retryCount);
                });
    }

    private static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 5,
                durationOfBreak: TimeSpan.FromSeconds(30),
                onBreak: (outcome, duration) =>
                {
                    Log.Error("Circuit breaker opened for {Duration}s", duration.TotalSeconds);
                },
                onReset: () =>
                {
                    Log.Information("Circuit breaker reset");
                });
    }

    private static IAsyncPolicy<HttpResponseMessage> GetTimeoutPolicy()
    {
        return Policy.TimeoutAsync<HttpResponseMessage>(TimeSpan.FromSeconds(10));
    }
}
```

## Version Information

- Observed in: Polly 8.2.0 with Microsoft.Extensions.Http.Polly 8.0.0
- Required pattern since: Polly v7.x with HttpClientFactory integration

## Additional Notes

**Policy Order Matters**: Policies are executed in the order registered:
1. Timeout (innermost - wraps the actual request)
2. Retry (middle - retries if timeout or transient error)
3. Circuit Breaker (outermost - prevents retries when circuit open)

**Named vs Typed Clients**: This pattern works for both named and typed HttpClients.

**Testing**: When unit testing, you can override policies by registering a test HttpClient without policies, since each `AddHttpClient()` call replaces previous registrations for that type.
```
