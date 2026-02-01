using System.Diagnostics;
using System.Text;
using System.Text.Json.Serialization;

namespace CompoundDocs.E2ETests.Fixtures;

/// <summary>
/// Test fixture that manages the lifecycle of an MCP server process for E2E testing.
/// Uses stdio transport for communication with the MCP server.
/// </summary>
public class McpServerFixture : IAsyncLifetime
{
    private Process? _serverProcess;
    private StreamWriter? _stdin;
    private StreamReader? _stdout;
    private StreamReader? _stderr;
    private int _messageId;
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly Dictionary<int, TaskCompletionSource<JsonElement>> _pendingRequests = new();
    private CancellationTokenSource? _readCts;
    private Task? _readTask;

    /// <summary>
    /// Gets whether the server process is running.
    /// </summary>
    public bool IsRunning => _serverProcess is { HasExited: false };

    /// <summary>
    /// Gets the path to the MCP server executable.
    /// </summary>
    public string ServerPath { get; private set; } = string.Empty;

    /// <summary>
    /// Timeout for MCP requests (default 30 seconds).
    /// </summary>
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Initializes and starts the MCP server process.
    /// </summary>
    public async Task InitializeAsync()
    {
        ServerPath = FindServerExecutable();
        var serverDir = Path.GetDirectoryName(ServerPath)!;

        // Build the project first to ensure we have the latest binaries
        var buildResult = await RunDotnetBuildAsync(serverDir);
        if (!buildResult)
        {
            throw new InvalidOperationException("Failed to build MCP server project");
        }

        // Find the built executable/DLL
        var dllPath = FindBuiltDll(serverDir);

        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"\"{dllPath}\"",
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = serverDir,
            Environment =
            {
                ["DOTNET_ENVIRONMENT"] = "Testing",
                ["COMPOUNDDOCS_LOG_LEVEL"] = "Debug"
            }
        };

        _serverProcess = new Process { StartInfo = startInfo };

        if (!_serverProcess.Start())
        {
            throw new InvalidOperationException("Failed to start MCP server process");
        }

        _stdin = _serverProcess.StandardInput;
        _stdout = _serverProcess.StandardOutput;
        _stderr = _serverProcess.StandardError;

        // Start background task to read stderr (for diagnostics) and stdout (for responses)
        _readCts = new CancellationTokenSource();
        _readTask = ReadResponsesAsync(_readCts.Token);
        _ = ReadStderrAsync(_readCts.Token);

        // Give the server a moment to fully initialize the transport
        await Task.Delay(1000);

        // Wait for server to be ready by sending initialize request
        await InitializeServerAsync();
    }

    private static async Task<bool> RunDotnetBuildAsync(string projectDir)
    {
        using var buildProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = "build --no-restore -c Debug -v:q",
                WorkingDirectory = projectDir,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };

        buildProcess.Start();
        await buildProcess.WaitForExitAsync();
        return buildProcess.ExitCode == 0;
    }

    private static string FindBuiltDll(string projectDir)
    {
        // Look for Debug build first, then Release
        var configurations = new[] { "Debug", "Release" };
        var frameworks = new[] { "net9.0", "net8.0" };

        foreach (var config in configurations)
        {
            foreach (var framework in frameworks)
            {
                var dllPath = Path.Combine(projectDir, "bin", config, framework, "CompoundDocs.McpServer.dll");
                if (File.Exists(dllPath))
                {
                    return dllPath;
                }
            }
        }

        throw new FileNotFoundException("Could not find built MCP server DLL");
    }

    private async Task ReadStderrAsync(CancellationToken cancellationToken)
    {
        if (_stderr is null) return;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await _stderr.ReadLineAsync(cancellationToken);
                if (line is null) break;
                // Stderr is just for logging, we don't process it
                System.Diagnostics.Debug.WriteLine($"[MCP stderr] {line}");
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown
        }
    }

    /// <summary>
    /// Stops the MCP server process and cleans up resources.
    /// </summary>
    public async Task DisposeAsync()
    {
        // Cancel the read task
        _readCts?.Cancel();

        if (_readTask is not null)
        {
            try
            {
                await _readTask.WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch (OperationCanceledException)
            {
                // Expected
            }
            catch (TimeoutException)
            {
                // Read task didn't complete in time
            }
        }

        // Close streams
        _stdin?.Dispose();
        _stdout?.Dispose();
        _stderr?.Dispose();

        // Stop the process
        if (_serverProcess is { HasExited: false })
        {
            try
            {
                _serverProcess.Kill(entireProcessTree: true);
                await _serverProcess.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch
            {
                // Best effort cleanup
            }
        }

        _serverProcess?.Dispose();
        _readCts?.Dispose();
        _sendLock.Dispose();
    }

    /// <summary>
    /// Sends a JSON-RPC request to the MCP server and waits for the response.
    /// </summary>
    /// <typeparam name="TResult">The expected result type.</typeparam>
    /// <param name="method">The MCP method name.</param>
    /// <param name="parameters">Optional method parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The response result.</returns>
    public async Task<TResult> SendRequestAsync<TResult>(
        string method,
        object? parameters = null,
        CancellationToken cancellationToken = default)
    {
        var response = await SendRequestInternalAsync(method, parameters, cancellationToken);

        if (response.TryGetProperty("error", out var error))
        {
            var errorCode = error.GetProperty("code").GetInt32();
            var errorMessage = error.GetProperty("message").GetString();
            throw new McpServerException(errorCode, errorMessage ?? "Unknown error");
        }

        var result = response.GetProperty("result");
        return JsonSerializer.Deserialize<TResult>(result.GetRawText())
            ?? throw new InvalidOperationException("Failed to deserialize response");
    }

    /// <summary>
    /// Sends a notification (no response expected) to the MCP server.
    /// </summary>
    /// <param name="method">The MCP method name.</param>
    /// <param name="parameters">Optional method parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task SendNotificationAsync(
        string method,
        object? parameters = null,
        CancellationToken cancellationToken = default)
    {
        var notification = new
        {
            jsonrpc = "2.0",
            method,
            @params = parameters
        };

        await SendMessageAsync(notification, cancellationToken);
    }

    /// <summary>
    /// Calls an MCP tool and returns the result.
    /// </summary>
    /// <param name="toolName">The name of the tool to call.</param>
    /// <param name="arguments">Tool arguments.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The tool call result.</returns>
    public async Task<McpToolResult> CallToolAsync(
        string toolName,
        Dictionary<string, object>? arguments = null,
        CancellationToken cancellationToken = default)
    {
        var parameters = new
        {
            name = toolName,
            arguments = arguments ?? new Dictionary<string, object>()
        };

        return await SendRequestAsync<McpToolResult>("tools/call", parameters, cancellationToken);
    }

    private async Task InitializeServerAsync()
    {
        var initParams = new
        {
            protocolVersion = "2024-11-05",
            capabilities = new
            {
                tools = new { }
            },
            clientInfo = new
            {
                name = "CompoundDocs.E2ETests",
                version = "1.0.0"
            }
        };

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await SendRequestAsync<JsonElement>("initialize", initParams, cts.Token);

        // Send initialized notification
        await SendNotificationAsync("notifications/initialized", cancellationToken: cts.Token);
    }

    private async Task<JsonElement> SendRequestInternalAsync(
        string method,
        object? parameters,
        CancellationToken cancellationToken)
    {
        var id = Interlocked.Increment(ref _messageId);
        var tcs = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);

        lock (_pendingRequests)
        {
            _pendingRequests[id] = tcs;
        }

        try
        {
            var request = new
            {
                jsonrpc = "2.0",
                id,
                method,
                @params = parameters
            };

            await SendMessageAsync(request, cancellationToken);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(RequestTimeout);

            return await tcs.Task.WaitAsync(timeoutCts.Token);
        }
        finally
        {
            lock (_pendingRequests)
            {
                _pendingRequests.Remove(id);
            }
        }
    }

    private async Task SendMessageAsync(object message, CancellationToken cancellationToken)
    {
        if (_stdin is null)
        {
            throw new InvalidOperationException("Server not initialized");
        }

        // MCP uses newline-delimited JSON (NDJSON) - one JSON message per line
        var json = JsonSerializer.Serialize(message);

        await _sendLock.WaitAsync(cancellationToken);
        try
        {
            await _stdin.WriteLineAsync(json);
            await _stdin.FlushAsync(cancellationToken);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    private async Task ReadResponsesAsync(CancellationToken cancellationToken)
    {
        if (_stdout is null) return;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                // MCP uses newline-delimited JSON - each line is a complete JSON message
                var line = await _stdout.ReadLineAsync(cancellationToken);
                if (line is null) break;

                // Skip empty lines
                if (string.IsNullOrWhiteSpace(line)) continue;

                ProcessMessage(line);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown
        }
    }

    private void ProcessMessage(string content)
    {
        try
        {
            var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;

            if (root.TryGetProperty("id", out var idElement))
            {
                var id = idElement.GetInt32();

                TaskCompletionSource<JsonElement>? tcs;
                lock (_pendingRequests)
                {
                    _pendingRequests.TryGetValue(id, out tcs);
                }

                tcs?.TrySetResult(root);
            }
        }
        catch (JsonException)
        {
            // Invalid JSON, ignore
        }
    }

    private static string FindServerExecutable()
    {
        // Look for the MCP server project relative to the test assembly
        var currentDir = AppContext.BaseDirectory;

        // Navigate up to find the solution root
        var dir = new DirectoryInfo(currentDir);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "csharp-compounding-docs.sln")))
        {
            dir = dir.Parent;
        }

        if (dir is null)
        {
            throw new FileNotFoundException("Could not find solution root");
        }

        var serverProject = Path.Combine(dir.FullName, "src", "CompoundDocs.McpServer", "CompoundDocs.McpServer.csproj");

        if (!File.Exists(serverProject))
        {
            throw new FileNotFoundException($"MCP server project not found at: {serverProject}");
        }

        return serverProject;
    }
}

/// <summary>
/// Exception thrown when an MCP server returns an error response.
/// </summary>
public class McpServerException : Exception
{
    /// <summary>
    /// The JSON-RPC error code.
    /// </summary>
    public int ErrorCode { get; }

    /// <summary>
    /// Creates a new MCP server exception.
    /// </summary>
    /// <param name="errorCode">The error code.</param>
    /// <param name="message">The error message.</param>
    public McpServerException(int errorCode, string message)
        : base($"MCP Error {errorCode}: {message}")
    {
        ErrorCode = errorCode;
    }
}

/// <summary>
/// Represents the result of an MCP tool call.
/// </summary>
public class McpToolResult
{
    /// <summary>
    /// The content items returned by the tool.
    /// </summary>
    [JsonPropertyName("content")]
    public List<McpContent> Content { get; set; } = new();

    /// <summary>
    /// Whether the tool call was an error.
    /// </summary>
    [JsonPropertyName("isError")]
    public bool IsError { get; set; }
}

/// <summary>
/// Represents a content item in an MCP response.
/// </summary>
public class McpContent
{
    /// <summary>
    /// The content type (e.g., "text", "image").
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// The text content (if type is "text").
    /// </summary>
    [JsonPropertyName("text")]
    public string? Text { get; set; }
}

/// <summary>
/// Collection definition for tests that share an MCP server instance.
/// Use [Collection("McpServer")] on test classes to share the fixture.
/// </summary>
[CollectionDefinition("McpServer")]
public class McpServerCollection : ICollectionFixture<McpServerFixture>
{
    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionDefinition] and all the
    // ICollectionFixture<> interfaces.
}
