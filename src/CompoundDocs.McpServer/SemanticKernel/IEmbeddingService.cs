namespace CompoundDocs.McpServer.SemanticKernel;

/// <summary>
/// Abstraction over embedding generation for testability.
/// Wraps Semantic Kernel's ITextEmbeddingGenerationService.
/// </summary>
public interface IEmbeddingService
{
    /// <summary>
    /// Generate embedding vector for the given content.
    /// </summary>
    /// <param name="content">Text content to embed.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>1024-dimension embedding vector.</returns>
    Task<ReadOnlyMemory<float>> GenerateEmbeddingAsync(
        string content,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate embeddings for multiple texts in a batch.
    /// </summary>
    /// <param name="contents">Text contents to embed.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of embedding vectors in same order as input.</returns>
    Task<IReadOnlyList<ReadOnlyMemory<float>>> GenerateEmbeddingsAsync(
        IReadOnlyList<string> contents,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Expected embedding dimensions (1024 for mxbai-embed-large).
    /// </summary>
    int Dimensions { get; }
}
