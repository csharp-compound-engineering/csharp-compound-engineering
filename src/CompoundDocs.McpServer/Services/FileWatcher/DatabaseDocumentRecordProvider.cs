using CompoundDocs.McpServer.Data.Repositories;
using CompoundDocs.McpServer.Session;
using Microsoft.Extensions.Logging;

namespace CompoundDocs.McpServer.Services.FileWatcher;

/// <summary>
/// Production implementation of IDocumentRecordProvider that retrieves document records from the database.
/// Uses IDocumentRepository for actual data access.
/// </summary>
public sealed class DatabaseDocumentRecordProvider : IDocumentRecordProvider
{
    private readonly IDocumentRepository _documentRepository;
    private readonly ISessionContext _sessionContext;
    private readonly ILogger<DatabaseDocumentRecordProvider> _logger;

    /// <summary>
    /// Creates a new instance of DatabaseDocumentRecordProvider.
    /// </summary>
    /// <param name="documentRepository">The document repository for database access.</param>
    /// <param name="sessionContext">The session context for tenant information.</param>
    /// <param name="logger">The logger instance.</param>
    public DatabaseDocumentRecordProvider(
        IDocumentRepository documentRepository,
        ISessionContext sessionContext,
        ILogger<DatabaseDocumentRecordProvider> logger)
    {
        _documentRepository = documentRepository ?? throw new ArgumentNullException(nameof(documentRepository));
        _sessionContext = sessionContext ?? throw new ArgumentNullException(nameof(sessionContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<Dictionary<string, DateTimeOffset>> GetDocumentRecordsAsync(
        string projectPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(projectPath);

        var tenantKey = _sessionContext.TenantKey;
        if (string.IsNullOrEmpty(tenantKey))
        {
            _logger.LogWarning("No active project, returning empty document records");
            return new Dictionary<string, DateTimeOffset>(StringComparer.OrdinalIgnoreCase);
        }

        _logger.LogDebug("Getting document records for tenant: {TenantKey}", tenantKey);

        try
        {
            var documents = await _documentRepository.GetAllForTenantAsync(tenantKey, cancellationToken: cancellationToken);

            var result = new Dictionary<string, DateTimeOffset>(StringComparer.OrdinalIgnoreCase);
            foreach (var doc in documents)
            {
                if (!string.IsNullOrEmpty(doc.FilePath))
                {
                    // Convert relative path to absolute path
                    var absolutePath = Path.IsPathRooted(doc.FilePath)
                        ? doc.FilePath
                        : Path.GetFullPath(Path.Combine(projectPath, doc.FilePath));

                    result[absolutePath] = doc.LastModified;
                }
            }

            _logger.LogDebug("Retrieved {Count} document records for tenant: {TenantKey}", result.Count, tenantKey);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving document records for tenant: {TenantKey}", tenantKey);
            return new Dictionary<string, DateTimeOffset>(StringComparer.OrdinalIgnoreCase);
        }
    }
}
