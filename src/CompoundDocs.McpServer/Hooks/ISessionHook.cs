namespace CompoundDocs.McpServer.Hooks;

/// <summary>
/// Interface for session lifecycle hooks.
/// Hooks are executed at various points during MCP session operations.
/// </summary>
public interface ISessionHook
{
    /// <summary>
    /// The name of the hook for identification and logging.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// The execution order of the hook (lower values execute first).
    /// Default is 0.
    /// </summary>
    int Order { get; }

    /// <summary>
    /// Whether this hook is enabled.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Called when an MCP session starts.
    /// Can perform prerequisite checks and initialization.
    /// </summary>
    /// <param name="context">The session hook context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The hook result indicating whether to continue or cancel.</returns>
    Task<SessionHookResult> OnSessionStartAsync(
        SessionHookContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Called when an MCP session ends.
    /// Can perform cleanup and resource disposal.
    /// </summary>
    /// <param name="context">The session hook context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The hook result.</returns>
    Task<SessionHookResult> OnSessionEndAsync(
        SessionHookContext context,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Context information passed to session hooks.
/// </summary>
public sealed class SessionHookContext
{
    /// <summary>
    /// The tenant key for the session.
    /// </summary>
    public string? TenantKey { get; init; }

    /// <summary>
    /// The session identifier.
    /// </summary>
    public string? SessionId { get; init; }

    /// <summary>
    /// Additional metadata that can be passed between hooks.
    /// </summary>
    public IDictionary<string, object?> Metadata { get; } = new Dictionary<string, object?>();
}

/// <summary>
/// Result of a session hook execution.
/// </summary>
public sealed class SessionHookResult
{
    /// <summary>
    /// Whether the operation should continue.
    /// </summary>
    public bool ShouldContinue { get; init; } = true;

    /// <summary>
    /// Whether the hook execution was successful.
    /// </summary>
    public bool IsSuccess { get; init; } = true;

    /// <summary>
    /// Optional error message if the hook failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Optional warnings from the hook.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];

    /// <summary>
    /// Creates a successful continue result.
    /// </summary>
    public static SessionHookResult Continue(IReadOnlyList<string>? warnings = null)
    {
        return new SessionHookResult
        {
            ShouldContinue = true,
            IsSuccess = true,
            Warnings = warnings ?? []
        };
    }

    /// <summary>
    /// Creates a result that cancels the operation.
    /// </summary>
    public static SessionHookResult Cancel(string reason)
    {
        return new SessionHookResult
        {
            ShouldContinue = false,
            IsSuccess = true,
            ErrorMessage = reason
        };
    }

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    public static SessionHookResult Failure(string error)
    {
        return new SessionHookResult
        {
            ShouldContinue = false,
            IsSuccess = false,
            ErrorMessage = error
        };
    }

    /// <summary>
    /// Creates a skipped result (hook didn't need to run).
    /// </summary>
    public static SessionHookResult Skip()
    {
        return new SessionHookResult
        {
            ShouldContinue = true,
            IsSuccess = true
        };
    }
}
