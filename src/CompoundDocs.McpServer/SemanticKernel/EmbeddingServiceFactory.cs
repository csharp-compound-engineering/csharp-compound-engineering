using CompoundDocs.McpServer.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.Ollama;
using OllamaSharp;

namespace CompoundDocs.McpServer.SemanticKernel;

/// <summary>
/// Factory for creating IEmbeddingGenerator instances.
/// Configures the Ollama connector with mxbai-embed-large model.
/// </summary>
public sealed class EmbeddingServiceFactory
{
    private readonly IOptions<CompoundDocsServerOptions> _options;
    private readonly ILogger<EmbeddingServiceFactory> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    /// <summary>
    /// HTTP client name for Ollama connections.
    /// </summary>
    public const string OllamaHttpClientName = "ollama";

    /// <summary>
    /// Creates a new instance of the EmbeddingServiceFactory.
    /// </summary>
    /// <param name="options">MCP server options containing Ollama configuration.</param>
    /// <param name="httpClientFactory">HTTP client factory for creating Ollama clients.</param>
    /// <param name="logger">Logger instance.</param>
    public EmbeddingServiceFactory(
        IOptions<CompoundDocsServerOptions> options,
        IHttpClientFactory httpClientFactory,
        ILogger<EmbeddingServiceFactory> logger)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Creates an IEmbeddingGenerator configured for Ollama.
    /// Uses OllamaApiClient from OllamaSharp which implements IEmbeddingGenerator.
    /// </summary>
    /// <returns>A configured embedding generator.</returns>
    public IEmbeddingGenerator<string, Embedding<float>> CreateEmbeddingGenerator()
    {
        var ollamaOptions = _options.Value.Ollama;
        var endpoint = ollamaOptions.GetEndpoint();

        _logger.LogInformation(
            "Creating Ollama embedding generator with model {Model} at endpoint {Endpoint}",
            OllamaConnectionOptions.EmbeddingModel,
            endpoint);

        var httpClient = _httpClientFactory.CreateClient(OllamaHttpClientName);

        // Create the OllamaApiClient which implements IEmbeddingGenerator
        var apiClient = new OllamaApiClient(httpClient, OllamaConnectionOptions.EmbeddingModel);

        // OllamaApiClient directly implements IEmbeddingGenerator<string, Embedding<float>>
        return apiClient;
    }

    /// <summary>
    /// Creates a Semantic Kernel instance configured with Ollama services.
    /// </summary>
    /// <returns>A configured Kernel instance.</returns>
    public Kernel CreateKernel()
    {
        var ollamaOptions = _options.Value.Ollama;
        var endpoint = ollamaOptions.GetEndpoint();

        _logger.LogInformation(
            "Creating Semantic Kernel with Ollama at {Endpoint}",
            endpoint);

        var builder = Kernel.CreateBuilder();

        // Add Ollama embedding generator
        builder.AddOllamaEmbeddingGenerator(
            modelId: OllamaConnectionOptions.EmbeddingModel,
            endpoint: endpoint,
            serviceId: "ollama-embeddings");

        // Add Ollama chat completion for RAG synthesis
        builder.AddOllamaChatCompletion(
            modelId: ollamaOptions.GenerationModel,
            endpoint: endpoint,
            serviceId: "ollama-chat");

        return builder.Build();
    }
}

/// <summary>
/// Extension methods for registering embedding services.
/// </summary>
public static class EmbeddingServiceCollectionExtensions
{
    /// <summary>
    /// Adds Ollama embedding services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddOllamaEmbeddingServices(this IServiceCollection services)
    {
        // Configure HttpClient for Ollama with appropriate timeout
        services.AddHttpClient(EmbeddingServiceFactory.OllamaHttpClientName, (serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<CompoundDocsServerOptions>>().Value;
            client.BaseAddress = options.Ollama.GetEndpoint();
            client.Timeout = TimeSpan.FromMinutes(5); // Long timeout for large document embedding
        })
        .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(15),
            MaxConnectionsPerServer = 10,
            EnableMultipleHttp2Connections = true
        });

        // Register the factory
        services.AddSingleton<EmbeddingServiceFactory>();

        // Register IEmbeddingGenerator from factory
        services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(sp =>
        {
            var factory = sp.GetRequiredService<EmbeddingServiceFactory>();
            return factory.CreateEmbeddingGenerator();
        });

        // Register our wrapper service
        services.AddSingleton<IEmbeddingService, EmbeddingService>();

        return services;
    }

    /// <summary>
    /// Validates embedding service configuration at startup.
    /// Generates a test embedding to verify dimensions.
    /// </summary>
    /// <param name="embeddingService">The embedding service to validate.</param>
    /// <param name="logger">Logger for diagnostic output.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if validation succeeds.</returns>
    public static async Task<bool> ValidateEmbeddingServiceAsync(
        IEmbeddingService embeddingService,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Validating embedding service configuration...");

        try
        {
            // Generate a test embedding to verify dimensions
            var testEmbedding = await embeddingService.GenerateEmbeddingAsync(
                "Embedding service validation test",
                cancellationToken);

            if (testEmbedding.Length != OllamaConnectionOptions.EmbeddingDimensions)
            {
                logger.LogError(
                    "Embedding dimension mismatch: expected {Expected}, got {Actual}",
                    OllamaConnectionOptions.EmbeddingDimensions,
                    testEmbedding.Length);
                return false;
            }

            logger.LogInformation(
                "Embedding service validated: {Dimensions}-dimensional embeddings",
                testEmbedding.Length);

            return true;
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex,
                "Failed to connect to Ollama for embedding validation. " +
                "Ensure Ollama is running and mxbai-embed-large model is available.");
            return false;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Embedding service validation failed");
            return false;
        }
    }
}
