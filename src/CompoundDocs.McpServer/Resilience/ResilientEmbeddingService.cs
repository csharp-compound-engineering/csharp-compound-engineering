using CompoundDocs.McpServer.SemanticKernel;
using Microsoft.Extensions.Logging;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace CompoundDocs.McpServer.Resilience;

/// <summary>
/// Resilient wrapper around IEmbeddingService that provides:
/// - Retry with exponential backoff
/// - Circuit breaker for Ollama failures
/// - Graceful degradation with cached embeddings
/// - Meaningful error messages
/// </summary>
public sealed class ResilientEmbeddingService : IEmbeddingService
{
    private readonly IEmbeddingService _innerService;
    private readonly IResiliencePolicies _resiliencePolicies;
    private readonly IEmbeddingCache _embeddingCache;
    private readonly ILogger<ResilientEmbeddingService> _logger;

    // Track Ollama availability state
    private bool _ollamaAvailable = true;
    private DateTime _lastAvailabilityCheck = DateTime.MinValue;
    private readonly TimeSpan _availabilityCheckInterval = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Creates a new instance of ResilientEmbeddingService.
    /// </summary>
    /// <param name="innerService">The underlying embedding service.</param>
    /// <param name="resiliencePolicies">Resilience policies.</param>
    /// <param name="embeddingCache">Embedding cache for fallback.</param>
    /// <param name="logger">Logger instance.</param>
    public ResilientEmbeddingService(
        IEmbeddingService innerService,
        IResiliencePolicies resiliencePolicies,
        IEmbeddingCache embeddingCache,
        ILogger<ResilientEmbeddingService> logger)
    {
        _innerService = innerService ?? throw new ArgumentNullException(nameof(innerService));
        _resiliencePolicies = resiliencePolicies ?? throw new ArgumentNullException(nameof(resiliencePolicies));
        _embeddingCache = embeddingCache ?? throw new ArgumentNullException(nameof(embeddingCache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public int Dimensions => _innerService.Dimensions;

    /// <summary>
    /// Gets whether Ollama is currently available.
    /// </summary>
    public bool IsOllamaAvailable => _ollamaAvailable;

    /// <inheritdoc />
    public async Task<ReadOnlyMemory<float>> GenerateEmbeddingAsync(
        string content,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);

        // Try cache first if Ollama might be unavailable
        if (!_ollamaAvailable && _embeddingCache.TryGet(content, out var cachedEmbedding))
        {
            _logger.LogInformation(
                "Using cached embedding (Ollama unavailable). Content length: {ContentLength}",
                content.Length);
            return cachedEmbedding;
        }

        try
        {
            var embedding = await _resiliencePolicies.ExecuteWithOllamaResilienceAsync(
                async ct => await _innerService.GenerateEmbeddingAsync(content, ct),
                cancellationToken);

            // Cache the successful result
            _embeddingCache.Set(content, embedding);

            // Mark Ollama as available
            UpdateOllamaAvailability(true);

            return embedding;
        }
        catch (BrokenCircuitException ex)
        {
            _logger.LogWarning(
                "Circuit breaker is open for Ollama. Attempting fallback. Exception: {Message}",
                ex.Message);

            UpdateOllamaAvailability(false);
            return HandleOllamaUnavailable(content, "Circuit breaker is open");
        }
        catch (TimeoutRejectedException ex)
        {
            _logger.LogWarning(
                "Ollama request timed out. Attempting fallback. Exception: {Message}",
                ex.Message);

            return HandleOllamaUnavailable(content, "Request timed out");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(
                "HTTP error communicating with Ollama. Attempting fallback. Status: {StatusCode}, Message: {Message}",
                ex.StatusCode,
                ex.Message);

            UpdateOllamaAvailability(false);
            return HandleOllamaUnavailable(content, $"Connection error: {ex.Message}");
        }
        catch (Exception ex) when (IsTransientOllamaError(ex))
        {
            _logger.LogWarning(
                "Transient Ollama error. Attempting fallback. Exception: {ExceptionType}: {Message}",
                ex.GetType().Name,
                ex.Message);

            return HandleOllamaUnavailable(content, ex.Message);
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

        // Check cache for all items first
        var results = new ReadOnlyMemory<float>[contents.Count];
        var uncachedIndices = new List<int>();
        var uncachedContents = new List<string>();

        for (var i = 0; i < contents.Count; i++)
        {
            if (_embeddingCache.TryGet(contents[i], out var cached))
            {
                results[i] = cached;
            }
            else
            {
                uncachedIndices.Add(i);
                uncachedContents.Add(contents[i]);
            }
        }

        // If all cached, return early
        if (uncachedIndices.Count == 0)
        {
            _logger.LogDebug("All {Count} embeddings served from cache", contents.Count);
            return results;
        }

        try
        {
            // Generate embeddings for uncached items
            var newEmbeddings = await _resiliencePolicies.ExecuteWithOllamaResilienceAsync(
                async ct => await _innerService.GenerateEmbeddingsAsync(uncachedContents, ct),
                cancellationToken);

            // Store results and update cache
            for (var i = 0; i < uncachedIndices.Count; i++)
            {
                var originalIndex = uncachedIndices[i];
                results[originalIndex] = newEmbeddings[i];
                _embeddingCache.Set(contents[originalIndex], newEmbeddings[i]);
            }

            UpdateOllamaAvailability(true);

            _logger.LogDebug(
                "Generated {NewCount} embeddings, {CachedCount} from cache",
                uncachedIndices.Count,
                contents.Count - uncachedIndices.Count);

            return results;
        }
        catch (BrokenCircuitException)
        {
            UpdateOllamaAvailability(false);
            return HandleBatchOllamaUnavailable(contents, results, uncachedIndices, "Circuit breaker is open");
        }
        catch (TimeoutRejectedException)
        {
            return HandleBatchOllamaUnavailable(contents, results, uncachedIndices, "Request timed out");
        }
        catch (HttpRequestException ex)
        {
            UpdateOllamaAvailability(false);
            return HandleBatchOllamaUnavailable(contents, results, uncachedIndices, $"Connection error: {ex.Message}");
        }
        catch (Exception ex) when (IsTransientOllamaError(ex))
        {
            return HandleBatchOllamaUnavailable(contents, results, uncachedIndices, ex.Message);
        }
    }

    private ReadOnlyMemory<float> HandleOllamaUnavailable(string content, string reason)
    {
        // Try to get from cache as fallback
        if (_embeddingCache.TryGet(content, out var cached))
        {
            _logger.LogInformation(
                "Ollama unavailable ({Reason}). Using cached embedding for content of length {ContentLength}",
                reason,
                content.Length);
            return cached;
        }

        // No fallback available - throw meaningful error
        throw new OllamaUnavailableException(
            $"Ollama embedding service is unavailable ({reason}) and no cached embedding exists for this content. " +
            "Please ensure Ollama is running and try again.",
            reason);
    }

    private IReadOnlyList<ReadOnlyMemory<float>> HandleBatchOllamaUnavailable(
        IReadOnlyList<string> contents,
        ReadOnlyMemory<float>[] results,
        List<int> uncachedIndices,
        string reason)
    {
        // For batch requests, we have some results already cached
        // For uncached items, try to find any cached version or throw
        var missingIndices = new List<int>();

        foreach (var index in uncachedIndices)
        {
            if (_embeddingCache.TryGet(contents[index], out var cached))
            {
                results[index] = cached;
            }
            else
            {
                missingIndices.Add(index);
            }
        }

        if (missingIndices.Count > 0)
        {
            throw new OllamaUnavailableException(
                $"Ollama embedding service is unavailable ({reason}) and {missingIndices.Count} of {contents.Count} " +
                "embeddings could not be retrieved from cache. Please ensure Ollama is running and try again.",
                reason);
        }

        _logger.LogInformation(
            "Ollama unavailable ({Reason}). All {Count} embeddings served from cache",
            reason,
            contents.Count);

        return results;
    }

    private void UpdateOllamaAvailability(bool available)
    {
        if (_ollamaAvailable != available || DateTime.UtcNow - _lastAvailabilityCheck > _availabilityCheckInterval)
        {
            var previousState = _ollamaAvailable;
            _ollamaAvailable = available;
            _lastAvailabilityCheck = DateTime.UtcNow;

            if (previousState != available)
            {
                if (available)
                {
                    _logger.LogInformation("Ollama service is now available");
                }
                else
                {
                    _logger.LogWarning("Ollama service is now unavailable");
                }
            }
        }
    }

    private static bool IsTransientOllamaError(Exception ex)
    {
        var message = ex.Message.ToLowerInvariant();
        return message.Contains("connection")
            || message.Contains("timeout")
            || message.Contains("unavailable")
            || message.Contains("refused")
            || message.Contains("network")
            || ex is TaskCanceledException { CancellationToken.IsCancellationRequested: false };
    }
}

/// <summary>
/// Exception thrown when Ollama is unavailable and no fallback is possible.
/// </summary>
public sealed class OllamaUnavailableException : Exception
{
    /// <summary>
    /// Gets the reason for unavailability.
    /// </summary>
    public string Reason { get; }

    /// <summary>
    /// Creates a new OllamaUnavailableException.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="reason">The reason for unavailability.</param>
    public OllamaUnavailableException(string message, string reason)
        : base(message)
    {
        Reason = reason;
    }

    /// <summary>
    /// Creates a new OllamaUnavailableException with inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="reason">The reason for unavailability.</param>
    /// <param name="innerException">The inner exception.</param>
    public OllamaUnavailableException(string message, string reason, Exception innerException)
        : base(message, innerException)
    {
        Reason = reason;
    }
}
