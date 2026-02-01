namespace CompoundDocs.McpServer.Tools;

/// <summary>
/// Represents a structured tool error with code and message.
/// </summary>
/// <param name="Code">The error code for programmatic handling.</param>
/// <param name="Message">The human-readable error message.</param>
public sealed record ToolError(string Code, string Message);

/// <summary>
/// Standard error codes and messages for MCP tools.
/// Provides consistent error handling across all tools.
/// </summary>
public static class ToolErrors
{
    #region Project/Session Errors

    /// <summary>
    /// Error when no project is currently active.
    /// </summary>
    public static readonly ToolError NoActiveProject = new(
        "NO_ACTIVE_PROJECT",
        "No project is currently active. Use 'activate_project' tool first.");

    /// <summary>
    /// Error when the project path does not exist.
    /// </summary>
    public static ToolError ProjectPathNotFound(string path) => new(
        "PROJECT_PATH_NOT_FOUND",
        $"Project path does not exist: {path}");

    /// <summary>
    /// Error when project activation fails.
    /// </summary>
    public static ToolError ProjectActivationFailed(string reason) => new(
        "PROJECT_ACTIVATION_FAILED",
        $"Failed to activate project: {reason}");

    #endregion

    #region Document Errors

    /// <summary>
    /// Error when a document is not found.
    /// </summary>
    public static ToolError DocumentNotFound(string filePath) => new(
        "DOCUMENT_NOT_FOUND",
        $"Document not found: {filePath}");

    /// <summary>
    /// Error when document indexing fails.
    /// </summary>
    public static ToolError IndexingFailed(string filePath, string reason) => new(
        "INDEXING_FAILED",
        $"Failed to index document '{filePath}': {reason}");

    /// <summary>
    /// Error when document deletion fails.
    /// </summary>
    public static ToolError DeletionFailed(string filePath, string reason) => new(
        "DELETION_FAILED",
        $"Failed to delete document '{filePath}': {reason}");

    /// <summary>
    /// Error when the file path is invalid.
    /// </summary>
    public static ToolError InvalidFilePath(string path) => new(
        "INVALID_FILE_PATH",
        $"Invalid file path: {path}");

    /// <summary>
    /// Error when the file does not exist.
    /// </summary>
    public static ToolError FileNotFound(string path) => new(
        "FILE_NOT_FOUND",
        $"File not found: {path}");

    /// <summary>
    /// Error when the file cannot be read.
    /// </summary>
    public static ToolError FileReadError(string path, string reason) => new(
        "FILE_READ_ERROR",
        $"Cannot read file '{path}': {reason}");

    #endregion

    #region Search/Query Errors

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

    #endregion

    #region Validation Errors

    /// <summary>
    /// Error when a required parameter is missing.
    /// </summary>
    public static ToolError MissingParameter(string parameterName) => new(
        "MISSING_PARAMETER",
        $"Required parameter is missing: {parameterName}");

    /// <summary>
    /// Error when a parameter value is invalid.
    /// </summary>
    public static ToolError InvalidParameter(string parameterName, string reason) => new(
        "INVALID_PARAMETER",
        $"Invalid value for parameter '{parameterName}': {reason}");

    /// <summary>
    /// Error when the promotion level is invalid.
    /// </summary>
    public static ToolError InvalidPromotionLevel(string level) => new(
        "INVALID_PROMOTION_LEVEL",
        $"Invalid promotion level: '{level}'. Valid values are: standard, important, critical.");

    /// <summary>
    /// Error when the document type is invalid.
    /// </summary>
    public static ToolError InvalidDocType(string docType) => new(
        "INVALID_DOC_TYPE",
        $"Invalid document type: '{docType}'. Use 'list_doc_types' to see valid types.");

    #endregion

    #region External Docs Errors

    /// <summary>
    /// Error when external source is not configured.
    /// </summary>
    public static ToolError ExternalSourceNotConfigured(string source) => new(
        "EXTERNAL_SOURCE_NOT_CONFIGURED",
        $"External documentation source not configured: {source}");

    /// <summary>
    /// Error when external search fails.
    /// </summary>
    public static ToolError ExternalSearchFailed(string source, string reason) => new(
        "EXTERNAL_SEARCH_FAILED",
        $"Failed to search external source '{source}': {reason}");

    #endregion

    #region General Errors

    /// <summary>
    /// Error when an unexpected exception occurs.
    /// </summary>
    public static ToolError UnexpectedError(string reason) => new(
        "UNEXPECTED_ERROR",
        $"An unexpected error occurred: {reason}");

    /// <summary>
    /// Error when operation is cancelled.
    /// </summary>
    public static readonly ToolError OperationCancelled = new(
        "OPERATION_CANCELLED",
        "The operation was cancelled.");

    /// <summary>
    /// Error when service is unavailable.
    /// </summary>
    public static ToolError ServiceUnavailable(string serviceName) => new(
        "SERVICE_UNAVAILABLE",
        $"Service is unavailable: {serviceName}");

    #endregion

    #region Resilience Errors

    /// <summary>
    /// Error when rate limit is exceeded.
    /// </summary>
    public static ToolError RateLimitExceeded(string toolName, TimeSpan retryAfter) => new(
        "RATE_LIMIT_EXCEEDED",
        $"Rate limit exceeded for tool '{toolName}'. Please retry after {retryAfter.TotalSeconds:F1} seconds.");

    /// <summary>
    /// Error when circuit breaker is open.
    /// </summary>
    public static ToolError CircuitBreakerOpen(string serviceName) => new(
        "CIRCUIT_BREAKER_OPEN",
        $"Service '{serviceName}' is temporarily unavailable due to repeated failures. Please try again later.");

    /// <summary>
    /// Error when Ollama embedding service is unavailable.
    /// </summary>
    public static ToolError OllamaUnavailable(string reason) => new(
        "OLLAMA_UNAVAILABLE",
        $"Ollama embedding service is unavailable: {reason}. Ensure Ollama is running and accessible.");

    /// <summary>
    /// Error when operation times out.
    /// </summary>
    public static ToolError OperationTimeout(string operation, int timeoutSeconds) => new(
        "OPERATION_TIMEOUT",
        $"Operation '{operation}' timed out after {timeoutSeconds} seconds.");

    /// <summary>
    /// Error when database is unavailable.
    /// </summary>
    public static ToolError DatabaseUnavailable(string reason) => new(
        "DATABASE_UNAVAILABLE",
        $"Database is unavailable: {reason}. Please check the database connection.");

    #endregion
}
