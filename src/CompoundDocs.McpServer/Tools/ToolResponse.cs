using System.Text.Json.Serialization;

namespace CompoundDocs.McpServer.Tools;

/// <summary>
/// Standard response type for all MCP tools.
/// Provides consistent error handling and data formatting.
/// </summary>
/// <typeparam name="T">The type of data returned on success.</typeparam>
public sealed class ToolResponse<T>
{
    /// <summary>
    /// Whether the operation completed successfully.
    /// </summary>
    [JsonPropertyName("success")]
    public bool Success { get; init; }

    /// <summary>
    /// The response data when successful.
    /// </summary>
    [JsonPropertyName("data")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public T? Data { get; init; }

    /// <summary>
    /// Error message when the operation fails.
    /// </summary>
    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Error { get; init; }

    /// <summary>
    /// Structured error code for programmatic error handling.
    /// </summary>
    [JsonPropertyName("error_code")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ErrorCode { get; init; }

    /// <summary>
    /// Creates a successful response with data.
    /// </summary>
    /// <param name="data">The response data.</param>
    /// <returns>A successful tool response.</returns>
    public static ToolResponse<T> Ok(T data) => new()
    {
        Success = true,
        Data = data
    };

    /// <summary>
    /// Creates a failed response with error details.
    /// </summary>
    /// <param name="error">The error message.</param>
    /// <param name="errorCode">Optional error code.</param>
    /// <returns>A failed tool response.</returns>
    public static ToolResponse<T> Fail(string error, string? errorCode = null) => new()
    {
        Success = false,
        Error = error,
        ErrorCode = errorCode
    };

    /// <summary>
    /// Creates a failed response from a ToolError.
    /// </summary>
    /// <param name="toolError">The tool error.</param>
    /// <returns>A failed tool response.</returns>
    public static ToolResponse<T> Fail(ToolError toolError) => new()
    {
        Success = false,
        Error = toolError.Message,
        ErrorCode = toolError.Code
    };
}

/// <summary>
/// Non-generic tool response for operations without return data.
/// </summary>
public sealed class ToolResponse
{
    /// <summary>
    /// Whether the operation completed successfully.
    /// </summary>
    [JsonPropertyName("success")]
    public bool Success { get; init; }

    /// <summary>
    /// Optional message for successful operations.
    /// </summary>
    [JsonPropertyName("message")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Message { get; init; }

    /// <summary>
    /// Error message when the operation fails.
    /// </summary>
    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Error { get; init; }

    /// <summary>
    /// Structured error code for programmatic error handling.
    /// </summary>
    [JsonPropertyName("error_code")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ErrorCode { get; init; }

    /// <summary>
    /// Creates a successful response.
    /// </summary>
    /// <param name="message">Optional success message.</param>
    /// <returns>A successful tool response.</returns>
    public static ToolResponse Ok(string? message = null) => new()
    {
        Success = true,
        Message = message
    };

    /// <summary>
    /// Creates a failed response with error details.
    /// </summary>
    /// <param name="error">The error message.</param>
    /// <param name="errorCode">Optional error code.</param>
    /// <returns>A failed tool response.</returns>
    public static ToolResponse Fail(string error, string? errorCode = null) => new()
    {
        Success = false,
        Error = error,
        ErrorCode = errorCode
    };

    /// <summary>
    /// Creates a failed response from a ToolError.
    /// </summary>
    /// <param name="toolError">The tool error.</param>
    /// <returns>A failed tool response.</returns>
    public static ToolResponse Fail(ToolError toolError) => new()
    {
        Success = false,
        Error = toolError.Message,
        ErrorCode = toolError.Code
    };
}
