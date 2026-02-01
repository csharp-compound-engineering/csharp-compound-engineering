# Phase 146: MCP Protocol Compliance Testing

> **Status**: NOT_STARTED
> **Effort Estimate**: 4-6 hours
> **Category**: Testing Framework
> **Prerequisites**: Phase 120 (E2E Test Implementation)

---

## Spec References

This phase implements MCP protocol compliance testing defined in:

- **spec/mcp-server.md** - [Transport](../spec/mcp-server.md#transport) and [Error Response Format](../spec/mcp-server.md#error-response-format)
- **research/mcp-csharp-sdk-research.md** - [Transport Layer](../research/mcp-csharp-sdk-research.md#8-transport-layer), [Error Handling](../research/mcp-csharp-sdk-research.md#9-error-handling)

---

## Objectives

1. Implement MCP protocol message format validation tests
2. Create tool response format compliance tests for all 9 MCP tools
3. Create error response format compliance tests for standard error codes
4. Implement stdio transport compliance tests (line-delimited JSON-RPC)
5. Create protocol version negotiation tests
6. Validate JSON-RPC 2.0 request/response structure compliance

---

## Acceptance Criteria

### Protocol Message Validation

- [ ] Test class `McpMessageValidationTests` created in `tests/CompoundDocs.E2ETests/Protocol/`
- [ ] Validates JSON-RPC 2.0 message structure:
  - [ ] `jsonrpc` field equals `"2.0"`
  - [ ] `id` field present on requests (number or string)
  - [ ] `method` field present on requests
  - [ ] `params` field is object or array when present
- [ ] Validates MCP-specific message types:
  - [ ] `initialize` request/response structure
  - [ ] `tools/list` request/response structure
  - [ ] `tools/call` request/response structure
  - [ ] `notifications/initialized` structure

### Tool Response Format Compliance

- [ ] Test class `ToolResponseComplianceTests` created in `tests/CompoundDocs.E2ETests/Protocol/`
- [ ] Each tool response validates:
  - [ ] Response contains `content` array
  - [ ] Content items have valid `type` field (`text` or `image`)
  - [ ] Text content items have non-null `text` field
  - [ ] Response includes `isError` field when applicable
- [ ] Tests cover all 9 MCP tools:
  - [ ] `rag_query` response format
  - [ ] `semantic_search` response format
  - [ ] `index_document` response format
  - [ ] `list_doc_types` response format
  - [ ] `search_external_docs` response format
  - [ ] `rag_query_external` response format
  - [ ] `delete_documents` response format
  - [ ] `update_promotion_level` response format
  - [ ] `activate_project` response format

### Error Response Format Compliance

- [ ] Test class `ErrorResponseComplianceTests` created in `tests/CompoundDocs.E2ETests/Protocol/`
- [ ] Validates standard JSON-RPC error codes:
  - [ ] `-32700` (Parse error) - Invalid JSON
  - [ ] `-32600` (Invalid Request) - Malformed request structure
  - [ ] `-32601` (Method not found) - Unknown method
  - [ ] `-32602` (Invalid params) - Invalid method parameters
  - [ ] `-32603` (Internal error) - Server error
- [ ] Validates application error response format:
  - [ ] `error` field is `true`
  - [ ] `code` field contains error code string
  - [ ] `message` field contains human-readable description
  - [ ] `details` field is object (may be empty)
- [ ] Tests cover domain-specific error codes:
  - [ ] `DOCUMENT_NOT_FOUND`
  - [ ] `EMBEDDING_FAILED`
  - [ ] `INVALID_DOC_TYPE`
  - [ ] `PROJECT_NOT_ACTIVE`
  - [ ] `TENANT_MISMATCH`

### Stdio Transport Compliance

- [ ] Test class `StdioTransportComplianceTests` created in `tests/CompoundDocs.E2ETests/Protocol/`
- [ ] Validates line-delimited JSON-RPC over stdio:
  - [ ] Each message is a single line (no embedded newlines in JSON)
  - [ ] Messages terminated with newline (`\n`)
  - [ ] Server reads from stdin, writes to stdout
  - [ ] Logging output goes to stderr (not stdout)
- [ ] Validates message framing:
  - [ ] Multiple sequential requests handled correctly
  - [ ] Concurrent request handling (if supported)
  - [ ] Large message handling (>64KB)
- [ ] Validates stream lifecycle:
  - [ ] Server handles stdin EOF gracefully
  - [ ] Server exits cleanly on shutdown

### Protocol Version Handling

- [ ] Test class `ProtocolVersionTests` created in `tests/CompoundDocs.E2ETests/Protocol/`
- [ ] Validates initialization handshake:
  - [ ] Server responds to `initialize` request
  - [ ] Response includes `protocolVersion` field
  - [ ] Response includes `capabilities` object
  - [ ] Response includes `serverInfo` with `name` and `version`
- [ ] Validates version negotiation:
  - [ ] Server accepts supported protocol versions
  - [ ] Server rejects unsupported versions gracefully
  - [ ] Protocol version format is `YYYY-MM-DD` (e.g., `"2025-06-18"`)
- [ ] Validates capability advertisement:
  - [ ] `tools` capability present
  - [ ] Tool definitions match `tools/list` response

---

## Implementation Notes

### Test Project Location

All compliance tests should be in the E2E test project since they require the full MCP server running:

```
tests/CompoundDocs.E2ETests/
└── Protocol/
    ├── McpMessageValidationTests.cs
    ├── ToolResponseComplianceTests.cs
    ├── ErrorResponseComplianceTests.cs
    ├── StdioTransportComplianceTests.cs
    └── ProtocolVersionTests.cs
```

### Raw Transport Testing

For low-level transport compliance tests, use raw `Process` with stdin/stdout rather than the MCP client SDK to validate actual wire format:

```csharp
public class StdioTransportComplianceTests : IClassFixture<AspireIntegrationFixture>
{
    private readonly AspireIntegrationFixture _fixture;

    public StdioTransportComplianceTests(AspireIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    [Trait("Category", "E2E")]
    [Trait("Feature", "Protocol")]
    public async Task Server_WhenReceivingInitialize_RespondsWithValidJsonRpc()
    {
        // Arrange - direct process communication
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"run --project {_fixture.ServerProjectPath}",
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();

        // Act - send raw JSON-RPC request
        var request = JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            id = 1,
            method = "initialize",
            @params = new
            {
                protocolVersion = "2025-06-18",
                capabilities = new { },
                clientInfo = new { name = "test-client", version = "1.0.0" }
            }
        });

        await process.StandardInput.WriteLineAsync(request);
        await process.StandardInput.FlushAsync();

        var responseLine = await process.StandardOutput.ReadLineAsync();

        // Assert - validate JSON-RPC structure
        responseLine.ShouldNotBeNullOrEmpty();

        var response = JsonDocument.Parse(responseLine);
        response.RootElement.GetProperty("jsonrpc").GetString().ShouldBe("2.0");
        response.RootElement.GetProperty("id").GetInt32().ShouldBe(1);
        response.RootElement.TryGetProperty("result", out _).ShouldBeTrue();
        response.RootElement.TryGetProperty("error", out _).ShouldBeFalse();
    }

    [Fact]
    [Trait("Category", "E2E")]
    [Trait("Feature", "Protocol")]
    public async Task Server_WhenLogging_WritesToStderr()
    {
        // Validate that all log output goes to stderr, not stdout
        // stdout should contain only JSON-RPC messages
    }
}
```

### JSON-RPC Message Structure Validation

```csharp
public class McpMessageValidationTests : IClassFixture<AspireIntegrationFixture>
{
    private readonly AspireIntegrationFixture _fixture;

    public McpMessageValidationTests(AspireIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    [Trait("Category", "E2E")]
    [Trait("Feature", "Protocol")]
    public async Task ToolsCall_Response_ContainsRequiredFields()
    {
        // Arrange
        var mcpClient = _fixture.McpClient!;

        // Act
        var result = await mcpClient.CallToolAsync(
            "list_doc_types",
            new Dictionary<string, object?>());

        // Assert - validate MCP response structure
        result.ShouldNotBeNull();
        result.Content.ShouldNotBeNull();

        // Each content item must have valid type
        foreach (var content in result.Content)
        {
            content.ShouldBeAssignableTo<ContentBlock>();
            if (content is TextContentBlock textBlock)
            {
                textBlock.Text.ShouldNotBeNull();
            }
        }
    }

    [Fact]
    [Trait("Category", "E2E")]
    [Trait("Feature", "Protocol")]
    public async Task ToolsCall_WithInvalidParams_ReturnsError()
    {
        // Arrange
        var mcpClient = _fixture.McpClient!;

        // Act
        var result = await mcpClient.CallToolAsync(
            "semantic_search",
            new Dictionary<string, object?>
            {
                // Missing required 'query' parameter
                ["max_results"] = 5
            });

        // Assert - should return error in content
        result.IsError.ShouldBeTrue();

        var errorContent = result.Content.OfType<TextContentBlock>().First().Text;
        var error = JsonSerializer.Deserialize<ErrorResponse>(errorContent);

        error!.Error.ShouldBeTrue();
        error.Code.ShouldNotBeNullOrEmpty();
        error.Message.ShouldNotBeNullOrEmpty();
    }
}
```

### Error Response Format Validation

```csharp
public class ErrorResponseComplianceTests : IClassFixture<AspireIntegrationFixture>
{
    [Fact]
    [Trait("Category", "E2E")]
    [Trait("Feature", "Protocol")]
    public async Task ApplicationError_ContainsRequiredFields()
    {
        // Arrange
        var mcpClient = _fixture.McpClient!;

        // Act - trigger a domain error
        var result = await mcpClient.CallToolAsync(
            "rag_query",
            new Dictionary<string, object?>
            {
                ["query"] = "test",
                ["project_name"] = "nonexistent_project"
            });

        // Assert - validate error response format per spec
        result.IsError.ShouldBeTrue();

        var errorJson = result.Content.OfType<TextContentBlock>().First().Text;
        var error = JsonDocument.Parse(errorJson);

        // Required fields per spec/mcp-server.md#error-response-format
        error.RootElement.GetProperty("error").GetBoolean().ShouldBeTrue();
        error.RootElement.TryGetProperty("code", out var code).ShouldBeTrue();
        code.GetString().ShouldNotBeNullOrEmpty();
        error.RootElement.TryGetProperty("message", out var message).ShouldBeTrue();
        message.GetString().ShouldNotBeNullOrEmpty();
        error.RootElement.TryGetProperty("details", out _).ShouldBeTrue();
    }

    [Theory]
    [InlineData("-32700", "Invalid JSON should return parse error")]
    [InlineData("-32600", "Malformed request should return invalid request")]
    [InlineData("-32601", "Unknown method should return method not found")]
    [InlineData("-32602", "Invalid params should return invalid params")]
    [Trait("Category", "E2E")]
    [Trait("Feature", "Protocol")]
    public async Task JsonRpcErrors_FollowSpecification(string expectedCode, string description)
    {
        // Test each JSON-RPC error code
    }
}
```

### Protocol Version Tests

```csharp
public class ProtocolVersionTests : IClassFixture<AspireIntegrationFixture>
{
    [Fact]
    [Trait("Category", "E2E")]
    [Trait("Feature", "Protocol")]
    public async Task Initialize_ResponseIncludesProtocolVersion()
    {
        // The initialize response must include protocol version
        // per MCP specification
    }

    [Fact]
    [Trait("Category", "E2E")]
    [Trait("Feature", "Protocol")]
    public async Task Initialize_ProtocolVersionFormat_IsYYYYMMDD()
    {
        // Protocol version must be in YYYY-MM-DD format
        // e.g., "2025-06-18"
    }

    [Fact]
    [Trait("Category", "E2E")]
    [Trait("Feature", "Protocol")]
    public async Task Initialize_CapabilitiesIncludeTools()
    {
        // Server must advertise tools capability
    }

    [Fact]
    [Trait("Category", "E2E")]
    [Trait("Feature", "Protocol")]
    public async Task Initialize_ServerInfoIncludesNameAndVersion()
    {
        // Server info must include name and version
    }
}
```

### Error Response Record

```csharp
/// <summary>
/// Application error response format per spec/mcp-server.md.
/// </summary>
internal record ErrorResponse
{
    [JsonPropertyName("error")]
    public bool Error { get; init; }

    [JsonPropertyName("code")]
    public string Code { get; init; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;

    [JsonPropertyName("details")]
    public JsonElement? Details { get; init; }
}
```

### Test Data Constants

```csharp
internal static class ProtocolTestConstants
{
    // JSON-RPC error codes per specification
    public const int ParseError = -32700;
    public const int InvalidRequest = -32600;
    public const int MethodNotFound = -32601;
    public const int InvalidParams = -32602;
    public const int InternalError = -32603;

    // Application error codes per spec/mcp-server/tools.md
    public const string DocumentNotFound = "DOCUMENT_NOT_FOUND";
    public const string EmbeddingFailed = "EMBEDDING_FAILED";
    public const string InvalidDocType = "INVALID_DOC_TYPE";
    public const string ProjectNotActive = "PROJECT_NOT_ACTIVE";
    public const string TenantMismatch = "TENANT_MISMATCH";

    // Protocol version format regex
    public const string ProtocolVersionPattern = @"^\d{4}-\d{2}-\d{2}$";
}
```

---

## Dependencies

### Depends On
- **Phase 120**: E2E Test Implementation (E2E test infrastructure must be in place)
- **Phase 115**: Aspire Fixture (test fixture for MCP server)
- **Phase 110**: xUnit Test Framework Configuration

### Blocks
- None (validation/quality phase)

---

## Verification Steps

After completing this phase, verify:

1. **All protocol compliance tests pass**:
   ```bash
   dotnet test tests/CompoundDocs.E2ETests/ --filter "Feature=Protocol"
   ```

2. **Test coverage for protocol validation**:
   ```bash
   dotnet test tests/CompoundDocs.E2ETests/ /p:CollectCoverage=true --filter "Feature=Protocol"
   ```

3. **Validate JSON-RPC error code coverage**:
   - Ensure tests exist for all 5 standard JSON-RPC error codes
   - Ensure tests exist for all application-specific error codes

4. **Transport compliance verification**:
   - Run stdio transport tests to validate wire format
   - Verify logging goes to stderr, not stdout

5. **Protocol version tests**:
   - Verify version format validation
   - Verify capability advertisement

---

## Key Technical Decisions

### Raw Transport Testing vs SDK Testing

| Approach | Use Case |
|----------|----------|
| Raw Process stdin/stdout | Wire format validation, transport compliance |
| MCP Client SDK | Logical response validation, tool behavior |

**Rationale**: Using raw process I/O for transport tests ensures we validate the actual wire protocol, not just the SDK's interpretation of it.

### Test Categorization

| Category | Trait | Purpose |
|----------|-------|---------|
| E2E | `Category=E2E` | Full system tests |
| Protocol | `Feature=Protocol` | Protocol compliance subset |

**Usage**: Run protocol compliance tests specifically with `--filter "Feature=Protocol"`.

### Error Code Validation Strategy

| Error Type | Validation Approach |
|------------|---------------------|
| JSON-RPC codes | Numeric code in error response |
| Application codes | String code in error content JSON |

**Rationale**: JSON-RPC errors use standard numeric codes in the response `error.code` field. Application errors return success with error content containing our custom error format.

---

## Notes

- Protocol compliance tests may take longer than typical E2E tests due to process lifecycle overhead
- Consider adding a dedicated `xunit.runner.json` configuration if protocol tests need different timeout settings
- The MCP SDK may evolve; these tests help catch breaking changes in protocol handling
- For thorough validation, consider testing against the official MCP test suite if one becomes available
- Raw transport tests should clean up child processes in all cases (use `try/finally` or `IAsyncDisposable`)
