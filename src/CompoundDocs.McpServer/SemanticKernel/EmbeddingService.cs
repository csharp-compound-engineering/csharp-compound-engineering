using CompoundDocs.McpServer.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CompoundDocs.McpServer.SemanticKernel;

/// <summary>
/// Production implementation of IEmbeddingService using Microsoft.Extensions.AI.
/// Wraps IEmbeddingGenerator for testability.
/// </summary>
public sealed class EmbeddingService : IEmbeddingService
{
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;
    private readonly ILogger<EmbeddingService> _logger;
    private readonly int _dimensions;

    /// <summary>
    /// Creates a new instance of the EmbeddingService.
    /// </summary>
    /// <param name="embeddingGenerator">The embedding generator.</param>
    /// <param name="options">The MCP server options containing Ollama configuration.</param>
    /// <param name="logger">Logger instance.</param>
    public EmbeddingService(
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
        IOptions<CompoundDocsServerOptions> options,
        ILogger<EmbeddingService> logger)
    {
        _embeddingGenerator = embeddingGenerator ?? throw new ArgumentNullException(nameof(embeddingGenerator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _dimensions = OllamaConnectionOptions.EmbeddingDimensions;
    }

    /// <inheritdoc />
    public int Dimensions => _dimensions;

    /// <inheritdoc />
    public async Task<ReadOnlyMemory<float>> GenerateEmbeddingAsync(
        string content,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);

        var startTime = DateTimeOffset.UtcNow;
        _logger.LogDebug("Generating embedding for content of length {ContentLength}; Model={Model}",
            content.Length, "mxbai-embed-large");

        try
        {
            var result = await _embeddingGenerator.GenerateAsync(
                content,
                cancellationToken: cancellationToken);

            var embedding = result.Vector;
            ValidateEmbeddingDimensions(embedding.Length);

            var duration = DateTimeOffset.UtcNow - startTime;
            _logger.LogDebug("Successfully generated embedding with {Dimensions} dimensions; Duration={DurationMs}ms; ContentLength={ContentLength}",
                embedding.Length, (long)duration.TotalMilliseconds, content.Length);

            return embedding;
        }
        catch (Exception ex) when (ex is not ArgumentException && ex is not InvalidOperationException)
        {
            var duration = DateTimeOffset.UtcNow - startTime;
            _logger.LogError(ex, "Failed to generate embedding for content of length {ContentLength}; Duration={DurationMs}ms; ExceptionType={ExceptionType}",
                content.Length, (long)duration.TotalMilliseconds, ex.GetType().Name);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ReadOnlyMemory<float>>> GenerateEmbeddingsAsync(
        IReadOnlyList<string> contents,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contents);

        if (contents.Count == 0)
        {
            return Array.Empty<ReadOnlyMemory<float>>();
        }

        var startTime = DateTimeOffset.UtcNow;
        var totalLength = contents.Sum(c => c.Length);
        _logger.LogDebug("Generating batch embeddings for {Count} items; TotalContentLength={TotalLength}; Model={Model}",
            contents.Count, totalLength, "mxbai-embed-large");

        try
        {
            var results = await _embeddingGenerator.GenerateAsync(
                contents,
                cancellationToken: cancellationToken);

            var embeddings = new List<ReadOnlyMemory<float>>(results.Count);

            foreach (var result in results)
            {
                var embedding = result.Vector;
                ValidateEmbeddingDimensions(embedding.Length);
                embeddings.Add(embedding);
            }

            var duration = DateTimeOffset.UtcNow - startTime;
            _logger.LogDebug("Successfully generated {Count} embeddings; Duration={DurationMs}ms; AvgDurationPerItem={AvgMs}ms",
                embeddings.Count, (long)duration.TotalMilliseconds, (long)(duration.TotalMilliseconds / contents.Count));

            return embeddings.AsReadOnly();
        }
        catch (Exception ex) when (ex is not ArgumentException && ex is not InvalidOperationException)
        {
            var duration = DateTimeOffset.UtcNow - startTime;
            _logger.LogError(ex, "Failed to generate batch embeddings for {Count} items; Duration={DurationMs}ms; TotalContentLength={TotalLength}; ExceptionType={ExceptionType}",
                contents.Count, (long)duration.TotalMilliseconds, totalLength, ex.GetType().Name);
            throw;
        }
    }

    private void ValidateEmbeddingDimensions(int actualDimensions)
    {
        if (actualDimensions != _dimensions)
        {
            _logger.LogWarning(
                "Embedding dimension mismatch. Expected {Expected}, got {Actual}. " +
                "Ensure mxbai-embed-large model is being used.",
                _dimensions,
                actualDimensions);

            throw new InvalidOperationException(
                $"Embedding dimension mismatch: expected {_dimensions}, got {actualDimensions}. " +
                $"Ensure mxbai-embed-large model is being used.");
        }
    }
}
