namespace CompoundDocs.McpServer.Tools;

/// <summary>
/// Represents a structured tool error with code and message.
/// </summary>
/// <param name="Code">The error code for programmatic handling.</param>
/// <param name="Message">The human-readable error message.</param>
public sealed record ToolError(string Code, string Message);

/// <summary>
/// Standard error codes and messages for MCP tools.
/// </summary>
public static class ToolErrors
{
    /// <summary>
    /// Error when query is empty or invalid.
    /// </summary>
    public static readonly ToolError EmptyQuery = new(
        "EMPTY_QUERY",
        "Query cannot be empty.");

    /// <summary>
    /// Error when embedding generation fails.
    /// </summary>
    public static ToolError EmbeddingFailed(string reason) => new(
        "EMBEDDING_FAILED",
        $"Failed to generate embedding: {reason}");

    /// <summary>
    /// Error when search fails.
    /// </summary>
    public static ToolError SearchFailed(string reason) => new(
        "SEARCH_FAILED",
        $"Search operation failed: {reason}");

    /// <summary>
    /// Error when RAG synthesis fails.
    /// </summary>
    public static ToolError RagSynthesisFailed(string reason) => new(
        "RAG_SYNTHESIS_FAILED",
        $"RAG synthesis failed: {reason}");

    /// <summary>
    /// Error when operation is cancelled.
    /// </summary>
    public static readonly ToolError OperationCancelled = new(
        "OPERATION_CANCELLED",
        "The operation was cancelled.");

    /// <summary>
    /// Error when an unexpected exception occurs.
    /// </summary>
    public static ToolError UnexpectedError(string reason) => new(
        "UNEXPECTED_ERROR",
        $"An unexpected error occurred: {reason}");
}
