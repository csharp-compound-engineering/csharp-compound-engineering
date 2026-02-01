using Microsoft.Extensions.Logging;

namespace CompoundDocs.McpServer.Hooks;

/// <summary>
/// Executes document lifecycle hooks in order.
/// </summary>
public sealed class DocumentHookExecutor
{
    private readonly ILogger<DocumentHookExecutor> _logger;
    private readonly List<IDocumentHook> _hooks = [];
    private readonly object _hooksLock = new();

    /// <summary>
    /// Creates a new instance of DocumentHookExecutor.
    /// </summary>
    public DocumentHookExecutor(
        ILogger<DocumentHookExecutor> logger,
        IEnumerable<IDocumentHook>? hooks = null)
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
    public void RegisterHook(IDocumentHook hook)
    {
        ArgumentNullException.ThrowIfNull(hook);

        lock (_hooksLock)
        {
            _hooks.Add(hook);
            _hooks.Sort((a, b) => a.Order.CompareTo(b.Order));
        }

        _logger.LogInformation(
            "Registered document hook '{HookName}' with order {Order}",
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
                _logger.LogInformation("Unregistered document hook '{HookName}'", hookName);
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Gets all registered hooks.
    /// </summary>
    public IReadOnlyList<IDocumentHook> GetHooks()
    {
        lock (_hooksLock)
        {
            return _hooks.ToList();
        }
    }

    /// <summary>
    /// Executes before-index hooks.
    /// </summary>
    /// <param name="context">The hook context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The aggregated result of all hooks.</returns>
    public async Task<HookExecutionResult> ExecuteBeforeIndexAsync(
        DocumentHookContext context,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteHooksAsync(
            DocumentEventType.BeforeIndex,
            context,
            (hook, ctx, ct) => hook.OnBeforeIndexAsync(ctx, ct),
            cancellationToken);
    }

    /// <summary>
    /// Executes after-index hooks.
    /// </summary>
    /// <param name="context">The hook context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The aggregated result of all hooks.</returns>
    public async Task<HookExecutionResult> ExecuteAfterIndexAsync(
        DocumentHookContext context,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteHooksAsync(
            DocumentEventType.AfterIndex,
            context,
            (hook, ctx, ct) => hook.OnAfterIndexAsync(ctx, ct),
            cancellationToken);
    }

    /// <summary>
    /// Executes before-delete hooks.
    /// </summary>
    /// <param name="context">The hook context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The aggregated result of all hooks.</returns>
    public async Task<HookExecutionResult> ExecuteBeforeDeleteAsync(
        DocumentHookContext context,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteHooksAsync(
            DocumentEventType.BeforeDelete,
            context,
            (hook, ctx, ct) => hook.OnBeforeDeleteAsync(ctx, ct),
            cancellationToken);
    }

    /// <summary>
    /// Executes after-delete hooks.
    /// </summary>
    /// <param name="context">The hook context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The aggregated result of all hooks.</returns>
    public async Task<HookExecutionResult> ExecuteAfterDeleteAsync(
        DocumentHookContext context,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteHooksAsync(
            DocumentEventType.AfterDelete,
            context,
            (hook, ctx, ct) => hook.OnAfterDeleteAsync(ctx, ct),
            cancellationToken);
    }

    private async Task<HookExecutionResult> ExecuteHooksAsync(
        DocumentEventType eventType,
        DocumentHookContext context,
        Func<IDocumentHook, DocumentHookContext, CancellationToken, Task<HookResult>> executor,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var result = new HookExecutionResult { EventType = eventType };
        var hooks = GetEnabledHooks();

        if (hooks.Count == 0)
        {
            _logger.LogDebug("No hooks registered for event {EventType}", eventType);
            return result;
        }

        _logger.LogDebug(
            "Executing {Count} hooks for event {EventType} on document '{FilePath}'",
            hooks.Count, eventType, context.FilePath);

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

    private List<IDocumentHook> GetEnabledHooks()
    {
        lock (_hooksLock)
        {
            return _hooks.Where(h => h.IsEnabled).ToList();
        }
    }
}

/// <summary>
/// Result of executing all hooks for an event.
/// </summary>
public sealed class HookExecutionResult
{
    /// <summary>
    /// The type of event that was executed.
    /// </summary>
    public DocumentEventType EventType { get; init; }

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
    public Dictionary<string, HookResult> HookResults { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Whether the operation should proceed.
    /// </summary>
    public bool ShouldProceed => !WasCancelled && !HasErrors;
}
