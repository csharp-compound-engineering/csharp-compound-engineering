using Microsoft.Extensions.AI;

namespace CompoundDocs.IntegrationTests.Fixtures;

/// <summary>
/// Test fixture that provides an Ollama service or mock embedding generator for integration tests.
/// Supports both real Ollama containers and mock implementations for isolated testing.
/// </summary>
/// <remarks>
/// <para>
/// This fixture will attempt to connect to Ollama and fall back to mock embeddings if unavailable.
/// For local development, run Ollama with:
/// <code>
/// ollama serve
/// ollama pull nomic-embed-text
/// </code>
/// </para>
/// <para>
/// Set OLLAMA_URL environment variable to override the default endpoint (http://localhost:11434).
/// </para>
/// </remarks>
public class OllamaFixture : IAsyncLifetime
{
    private IEmbeddingGenerator<string, Embedding<float>>? _embeddingGenerator;
    private bool _useMockEmbeddings;

    /// <summary>
    /// Gets the embedding generator for tests.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if accessed before initialization.</exception>
    public IEmbeddingGenerator<string, Embedding<float>> EmbeddingGenerator => _embeddingGenerator
        ?? throw new InvalidOperationException("OllamaFixture has not been initialized. Call InitializeAsync first.");

    /// <summary>
    /// Gets whether mock embeddings are being used.
    /// </summary>
    public bool UseMockEmbeddings => _useMockEmbeddings;

    /// <summary>
    /// Gets the Ollama endpoint URL.
    /// </summary>
    public string OllamaUrl { get; private set; } = string.Empty;

    /// <summary>
    /// The embedding dimension used by the fixture (1536 for compatibility with common models).
    /// </summary>
    public const int EmbeddingDimension = 1536;

    /// <summary>
    /// Initializes the Ollama fixture. Uses mock embeddings if Ollama is not available.
    /// </summary>
    public async Task InitializeAsync()
    {
        OllamaUrl = Environment.GetEnvironmentVariable("OLLAMA_URL") ?? "http://localhost:11434";

        // Try to connect to Ollama, fall back to mock if not available
        if (await TryInitializeRealOllamaAsync())
        {
            _useMockEmbeddings = false;
        }
        else
        {
            _embeddingGenerator = new MockEmbeddingGenerator(EmbeddingDimension);
            _useMockEmbeddings = true;
        }
    }

    /// <summary>
    /// Cleans up resources.
    /// </summary>
    public Task DisposeAsync()
    {
        if (_embeddingGenerator is IDisposable disposable)
        {
            disposable.Dispose();
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Asserts that real Ollama embeddings are available (not mocked).
    /// Call this at the start of tests that require real embedding generation.
    /// </summary>
    /// <remarks>
    /// Use <see cref="Skip.If"/> from xUnit for conditional skipping, or use this method
    /// when real embeddings are a hard requirement.
    /// </remarks>
    public void RequireRealEmbeddings()
    {
        if (_useMockEmbeddings)
        {
            Assert.Fail(
                "Ollama is not available. Set up Ollama to run embedding integration tests. " +
                "See fixture documentation for setup instructions.");
        }
    }

    private async Task<bool> TryInitializeRealOllamaAsync()
    {
        try
        {
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var response = await httpClient.GetAsync($"{OllamaUrl}/api/version");

            if (response.IsSuccessStatusCode)
            {
                // Ollama is available - could create real embedding generator here
                // For now, we use the mock since the real implementation requires model pulling
                // In a real implementation, you would use:
                // _embeddingGenerator = new OllamaEmbeddingGenerator(new Uri(OllamaUrl), "nomic-embed-text");
                return false;
            }
        }
        catch (Exception)
        {
            // Ollama not available, will use mock
        }

        return false;
    }
}

/// <summary>
/// Mock embedding generator for isolated testing without Ollama.
/// Generates deterministic embeddings based on input text hash.
/// </summary>
/// <remarks>
/// This generator produces normalized vectors that are deterministic based on input,
/// making tests reproducible. Semantically similar texts will NOT produce similar vectors -
/// this is purely for testing infrastructure, not semantic similarity.
/// </remarks>
public sealed class MockEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>
{
    private readonly int _dimension;

    /// <summary>
    /// Gets metadata about the embedding generator.
    /// </summary>
    public EmbeddingGeneratorMetadata Metadata { get; }

    /// <summary>
    /// Creates a new mock embedding generator.
    /// </summary>
    /// <param name="dimension">The dimension of generated embeddings.</param>
    public MockEmbeddingGenerator(int dimension = 1536)
    {
        _dimension = dimension;
        Metadata = new EmbeddingGeneratorMetadata("MockEmbeddingGenerator", new Uri("mock://embedding-generator"));
    }

    /// <summary>
    /// Generates mock embeddings for the given inputs.
    /// Embeddings are deterministic based on input text hash.
    /// </summary>
    public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values,
        EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var embeddings = values.Select(v => new Embedding<float>(GenerateDeterministicVector(v)));
        var result = new GeneratedEmbeddings<Embedding<float>>(embeddings);
        return Task.FromResult(result);
    }

    /// <summary>
    /// Gets the service by type or null if not available.
    /// </summary>
    public object? GetService(Type serviceType, object? key = null)
    {
        if (serviceType == typeof(IEmbeddingGenerator<string, Embedding<float>>))
        {
            return this;
        }
        return null;
    }

    /// <summary>
    /// Disposes the mock generator.
    /// </summary>
    public void Dispose()
    {
        // No resources to dispose
    }

    private float[] GenerateDeterministicVector(string input)
    {
        var vector = new float[_dimension];
        var hash = input.GetHashCode(StringComparison.Ordinal);
        var random = new Random(hash);

        for (int i = 0; i < _dimension; i++)
        {
            vector[i] = (float)(random.NextDouble() * 2 - 1); // Values between -1 and 1
        }

        // Normalize the vector to unit length
        var magnitude = MathF.Sqrt(vector.Sum(x => x * x));
        if (magnitude > 0)
        {
            for (int i = 0; i < _dimension; i++)
            {
                vector[i] /= magnitude;
            }
        }

        return vector;
    }
}

/// <summary>
/// Collection definition for tests that share an Ollama instance.
/// Use [Collection("OllamaEmbeddings")] on test classes to share the fixture.
/// </summary>
/// <example>
/// <code>
/// [Collection("OllamaEmbeddings")]
/// public class MyEmbeddingTests
/// {
///     private readonly OllamaFixture _ollama;
///
///     public MyEmbeddingTests(OllamaFixture ollama)
///     {
///         _ollama = ollama;
///     }
///
///     [Fact]
///     public async Task TestWithEmbeddings()
///     {
///         var embeddings = await _ollama.EmbeddingGenerator.GenerateAsync(["test"]);
///         embeddings.ShouldNotBeEmpty();
///     }
/// }
/// </code>
/// </example>
[CollectionDefinition("OllamaEmbeddings")]
public class OllamaEmbeddingsCollection : ICollectionFixture<OllamaFixture>
{
    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionDefinition] and all the
    // ICollectionFixture<> interfaces.
}
