using CompoundDocs.McpServer.Models;

namespace CompoundDocs.McpServer.Hooks;

/// <summary>
/// Interface for document lifecycle hooks.
/// Hooks are executed at various points during document operations.
/// </summary>
public interface IDocumentHook
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
    /// Called before a document is indexed.
    /// Can modify the document or cancel the indexing operation.
    /// </summary>
    /// <param name="context">The hook context containing document information.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The hook result indicating whether to continue or cancel.</returns>
    Task<HookResult> OnBeforeIndexAsync(
        DocumentHookContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Called after a document is successfully indexed.
    /// </summary>
    /// <param name="context">The hook context containing document information.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The hook result.</returns>
    Task<HookResult> OnAfterIndexAsync(
        DocumentHookContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Called before a document is deleted.
    /// Can cancel the deletion operation.
    /// </summary>
    /// <param name="context">The hook context containing document information.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The hook result indicating whether to continue or cancel.</returns>
    Task<HookResult> OnBeforeDeleteAsync(
        DocumentHookContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Called after a document is successfully deleted.
    /// </summary>
    /// <param name="context">The hook context containing document information.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The hook result.</returns>
    Task<HookResult> OnAfterDeleteAsync(
        DocumentHookContext context,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Context information passed to document hooks.
/// </summary>
public sealed class DocumentHookContext
{
    /// <summary>
    /// The document being processed.
    /// </summary>
    public CompoundDocument? Document { get; init; }

    /// <summary>
    /// The file path of the document.
    /// </summary>
    public required string FilePath { get; init; }

    /// <summary>
    /// The tenant key for the document.
    /// </summary>
    public required string TenantKey { get; init; }

    /// <summary>
    /// The document type identifier.
    /// </summary>
    public string? DocType { get; init; }

    /// <summary>
    /// The raw content of the document (for before hooks).
    /// </summary>
    public string? RawContent { get; init; }

    /// <summary>
    /// The parsed frontmatter (for before hooks).
    /// </summary>
    public IDictionary<string, object?>? Frontmatter { get; init; }

    /// <summary>
    /// Additional metadata that can be passed between hooks.
    /// </summary>
    public IDictionary<string, object?> Metadata { get; } = new Dictionary<string, object?>();
}

/// <summary>
/// Result of a hook execution.
/// </summary>
public sealed class HookResult
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
    public static HookResult Continue(IReadOnlyList<string>? warnings = null)
    {
        return new HookResult
        {
            ShouldContinue = true,
            IsSuccess = true,
            Warnings = warnings ?? []
        };
    }

    /// <summary>
    /// Creates a result that cancels the operation.
    /// </summary>
    public static HookResult Cancel(string reason)
    {
        return new HookResult
        {
            ShouldContinue = false,
            IsSuccess = true,
            ErrorMessage = reason
        };
    }

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    public static HookResult Failure(string error)
    {
        return new HookResult
        {
            ShouldContinue = false,
            IsSuccess = false,
            ErrorMessage = error
        };
    }

    /// <summary>
    /// Creates a skipped result (hook didn't need to run).
    /// </summary>
    public static HookResult Skip()
    {
        return new HookResult
        {
            ShouldContinue = true,
            IsSuccess = true
        };
    }
}

/// <summary>
/// The type of document event.
/// </summary>
public enum DocumentEventType
{
    /// <summary>
    /// Before a document is indexed.
    /// </summary>
    BeforeIndex,

    /// <summary>
    /// After a document is indexed.
    /// </summary>
    AfterIndex,

    /// <summary>
    /// Before a document is deleted.
    /// </summary>
    BeforeDelete,

    /// <summary>
    /// After a document is deleted.
    /// </summary>
    AfterDelete
}
