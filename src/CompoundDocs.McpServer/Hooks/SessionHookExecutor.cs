using Microsoft.Extensions.Logging;

namespace CompoundDocs.McpServer.Hooks;

/// <summary>
/// Executes session lifecycle hooks in order.
/// </summary>
public sealed class SessionHookExecutor
{
    private readonly ILogger<SessionHookExecutor> _logger;
    private readonly List<ISessionHook> _hooks = [];
    private readonly object _hooksLock = new();

    /// <summary>
    /// Creates a new instance of SessionHookExecutor.
    /// </summary>
    public SessionHookExecutor(
        ILogger<SessionHookExecutor> logger,
        IEnumerable<ISessionHook>? hooks = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (hooks != null)
        {
            foreach (var hook in hooks)
            {
                RegisterHook(hook);
            }
        }
    }

    /// <summary>
    /// Registers a hook with the executor.
    /// </summary>
    /// <param name="hook">The hook to register.</param>
    public void RegisterHook(ISessionHook hook)
    {
        ArgumentNullException.ThrowIfNull(hook);

        lock (_hooksLock)
        {
            _hooks.Add(hook);
            _hooks.Sort((a, b) => a.Order.CompareTo(b.Order));
        }

        _logger.LogInformation(
            "Registered session hook '{HookName}' with order {Order}",
            hook.Name, hook.Order);
    }

    /// <summary>
    /// Unregisters a hook from the executor.
    /// </summary>
    /// <param name="hookName">The name of the hook to unregister.</param>
    /// <returns>True if the hook was found and removed.</returns>
    public bool UnregisterHook(string hookName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hookName);

        lock (_hooksLock)
        {
            var hook = _hooks.FirstOrDefault(h =>
                string.Equals(h.Name, hookName, StringComparison.OrdinalIgnoreCase));

            if (hook != null)
            {
                _hooks.Remove(hook);
                _logger.LogInformation("Unregistered session hook '{HookName}'", hookName);
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Gets all registered hooks.
    /// </summary>
    public IReadOnlyList<ISessionHook> GetHooks()
    {
        lock (_hooksLock)
        {
            return _hooks.ToList();
        }
    }

    /// <summary>
    /// Executes session start hooks.
    /// </summary>
    /// <param name="context">The hook context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The aggregated result of all hooks.</returns>
    public async Task<SessionHookExecutionResult> ExecuteSessionStartAsync(
        SessionHookContext context,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteHooksAsync(
            "SessionStart",
            context,
            (hook, ctx, ct) => hook.OnSessionStartAsync(ctx, ct),
            cancellationToken);
    }

    /// <summary>
    /// Executes session end hooks.
    /// </summary>
    /// <param name="context">The hook context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The aggregated result of all hooks.</returns>
    public async Task<SessionHookExecutionResult> ExecuteSessionEndAsync(
        SessionHookContext context,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteHooksAsync(
            "SessionEnd",
            context,
            (hook, ctx, ct) => hook.OnSessionEndAsync(ctx, ct),
            cancellationToken);
    }

    private async Task<SessionHookExecutionResult> ExecuteHooksAsync(
        string eventType,
        SessionHookContext context,
        Func<ISessionHook, SessionHookContext, CancellationToken, Task<SessionHookResult>> executor,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var result = new SessionHookExecutionResult { EventType = eventType };
        var hooks = GetEnabledHooks();

        if (hooks.Count == 0)
        {
            _logger.LogDebug("No hooks registered for event {EventType}", eventType);
            return result;
        }

        _logger.LogDebug(
            "Executing {Count} hooks for event {EventType}",
            hooks.Count, eventType);

        foreach (var hook in hooks)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                result.WasCancelled = true;
                result.CancelledByHook = "System";
                result.CancelReason = "Operation cancelled by user.";
                break;
            }

            try
            {
                var hookResult = await executor(hook, context, cancellationToken);
                result.HookResults[hook.Name] = hookResult;

                if (hookResult.Warnings.Count > 0)
                {
                    result.Warnings.AddRange(hookResult.Warnings.Select(w => $"[{hook.Name}] {w}"));
                }

                if (!hookResult.IsSuccess)
                {
                    result.HasErrors = true;
                    result.Errors.Add($"[{hook.Name}] {hookResult.ErrorMessage}");
                    _logger.LogWarning(
                        "Hook '{HookName}' failed for event {EventType}: {Error}",
                        hook.Name, eventType, hookResult.ErrorMessage);
                }

                if (!hookResult.ShouldContinue)
                {
                    result.WasCancelled = true;
                    result.CancelledByHook = hook.Name;
                    result.CancelReason = hookResult.ErrorMessage ?? "Cancelled by hook.";
                    _logger.LogInformation(
                        "Hook '{HookName}' cancelled event {EventType}: {Reason}",
                        hook.Name, eventType, result.CancelReason);
                    break;
                }
            }
            catch (Exception ex)
            {
                result.HasErrors = true;
                result.Errors.Add($"[{hook.Name}] Exception: {ex.Message}");
                _logger.LogError(
                    ex,
                    "Hook '{HookName}' threw exception for event {EventType}",
                    hook.Name, eventType);

                // Continue with other hooks unless it's a critical error
                if (ex is OperationCanceledException)
                {
                    result.WasCancelled = true;
                    result.CancelledByHook = hook.Name;
                    result.CancelReason = "Operation cancelled.";
                    break;
                }
            }
        }

        _logger.LogDebug(
            "Completed {Count} hooks for event {EventType}. Cancelled: {Cancelled}, Errors: {HasErrors}",
            hooks.Count, eventType, result.WasCancelled, result.HasErrors);

        return result;
    }

    private List<ISessionHook> GetEnabledHooks()
    {
        lock (_hooksLock)
        {
            return _hooks.Where(h => h.IsEnabled).ToList();
        }
    }
}

/// <summary>
/// Result of executing all hooks for a session event.
/// </summary>
public sealed class SessionHookExecutionResult
{
    /// <summary>
    /// The type of event that was executed.
    /// </summary>
    public required string EventType { get; init; }

    /// <summary>
    /// Whether any hook cancelled the operation.
    /// </summary>
    public bool WasCancelled { get; set; }

    /// <summary>
    /// The name of the hook that cancelled the operation.
    /// </summary>
    public string? CancelledByHook { get; set; }

    /// <summary>
    /// The reason for cancellation.
    /// </summary>
    public string? CancelReason { get; set; }

    /// <summary>
    /// Whether any hook failed with an error.
    /// </summary>
    public bool HasErrors { get; set; }

    /// <summary>
    /// Error messages from hooks.
    /// </summary>
    public List<string> Errors { get; } = [];

    /// <summary>
    /// Warning messages from hooks.
    /// </summary>
    public List<string> Warnings { get; } = [];

    /// <summary>
    /// Individual results from each hook.
    /// </summary>
    public Dictionary<string, SessionHookResult> HookResults { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Whether the operation should proceed.
    /// </summary>
    public bool ShouldProceed => !WasCancelled && !HasErrors;
}
