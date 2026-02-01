# Observability Structure Summary

This file contains the summary for the observability specification.

---

## spec/observability.md

### What This File Covers

The observability specification (`spec/observability.md`) defines the **logging architecture** for the CSharp Compound Docs MCP server. It is explicitly scoped to **logging only for MVP**, with metrics, health endpoints, tracing, and alerting deferred to post-MVP phases.

### Key Topics

1. **stdio Transport Constraint**: All logging must go to stderr because stdout is reserved for MCP protocol messages when using stdio transport.

2. **Logging Configuration**: Configuring `ILogger<T>` with console providers, log level filtering, and `appsettings.json` setup.

3. **Structured Logging Patterns**: Using semantic message templates with named parameters, standard log fields (ProjectName, BranchName, DocumentPath, ToolName, ElapsedMs, CorrelationId), and logging scopes for contextual information.

4. **Correlation ID Pattern**: Each MCP tool invocation generates a unique correlation ID to trace related log entries across a request flow.

5. **Sensitive Data Handling**: Guidelines on what should never be logged (document content, credentials, embedding vectors) versus what is safe to log (file paths, metadata, timing, error messages).

6. **Diagnostic Scenarios**: Troubleshooting guides for common issues like slow RAG queries, missing documents in search, and indexing failures.

7. **Service-Specific Logging**: Logging patterns for File Watcher Service, Embedding Service, Document Repository, and MCP Tool Execution.

8. **Future Considerations**: OpenTelemetry metrics, health check endpoints, distributed tracing, and Grafana dashboards for post-MVP.

### Structural Relationships

| Relationship | Document |
|--------------|----------|
| **Parent** | [SPEC.md](../SPEC.md) - Main plugin specification entry point |
| **Siblings** | [doc-types.md](doc-types.md), [mcp-server.md](mcp-server.md), [infrastructure.md](infrastructure.md), [skills.md](skills.md), [agents.md](agents.md), [marketplace.md](marketplace.md), [configuration.md](configuration.md), [testing.md](testing.md), [research-index.md](research-index.md) |
| **Children** | None |

### Research Document Dependencies

The observability spec references several research documents for background information:
- `research/dotnet-generic-host-mcp-research.md` - ILogger configuration for MCP
- `research/hosted-services-background-tasks.md` - BackgroundService logging patterns
- `research/semantic-kernel-ollama-rag-research.md` - Embedding service error handling
- `research/mcp-csharp-sdk-research.md` - Tool execution logging patterns
- `research/aspire-development-orchestrator.md` - Post-MVP observability dashboard
