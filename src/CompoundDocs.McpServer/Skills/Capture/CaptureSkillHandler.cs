using System.Text;
using System.Text.RegularExpressions;
using CompoundDocs.McpServer.Models;
using CompoundDocs.McpServer.Services.DocumentProcessing;
using CompoundDocs.McpServer.Session;
using Microsoft.Extensions.Logging;

namespace CompoundDocs.McpServer.Skills.Capture;

/// <summary>
/// Implementation of capture skill handling for all 5 capture doc types.
/// Creates markdown files with proper frontmatter and triggers document indexing.
/// </summary>
public sealed partial class CaptureSkillHandler : ICaptureSkillHandler
{
    private const string DocsDirectoryName = ".csharp-compounding-docs";

    private readonly ISessionContext _sessionContext;
    private readonly IDocumentIndexer _documentIndexer;
    private readonly FrontmatterGenerator _frontmatterGenerator;
    private readonly ILogger<CaptureSkillHandler> _logger;

    /// <summary>
    /// The supported document types for capture operations.
    /// </summary>
    private static readonly IReadOnlyList<string> SupportedDocTypes =
    [
        DocumentTypes.Problem,
        DocumentTypes.Insight,
        DocumentTypes.Codebase,
        DocumentTypes.Tool,
        DocumentTypes.Style
    ];

    /// <summary>
    /// Creates a new instance of CaptureSkillHandler.
    /// </summary>
    public CaptureSkillHandler(
        ISessionContext sessionContext,
        IDocumentIndexer documentIndexer,
        ILogger<CaptureSkillHandler> logger)
    {
        _sessionContext = sessionContext ?? throw new ArgumentNullException(nameof(sessionContext));
        _documentIndexer = documentIndexer ?? throw new ArgumentNullException(nameof(documentIndexer));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _frontmatterGenerator = new FrontmatterGenerator();
    }

    /// <inheritdoc />
    public async Task<CaptureResult> HandleCaptureAsync(
        CaptureRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug(
                "Handling capture request: DocType={DocType}, Title={Title}",
                request.DocType, request.Title);

            // Validate session context
            if (!_sessionContext.IsProjectActive)
            {
                return CaptureResult.Failed("No project is active. Use activate_project first.");
            }

            var projectPath = _sessionContext.ActiveProjectPath!;
            var tenantKey = _sessionContext.TenantKey!;

            // Validate doc type
            var docType = request.DocType.ToLowerInvariant();
            if (!SupportedDocTypes.Contains(docType))
            {
                return CaptureResult.Failed(
                    $"Unsupported doc type: {request.DocType}. " +
                    $"Supported types: {string.Join(", ", SupportedDocTypes)}");
            }

            // Validate content
            var contentValidation = await ValidateContentAsync(docType, request.Content, cancellationToken);
            var warnings = new List<string>(contentValidation.Warnings);

            if (!contentValidation.IsValid && request.UseTemplate)
            {
                // Return errors if content validation fails and template usage is expected
                return CaptureResult.Failed(contentValidation.Errors, warnings);
            }

            // Generate file path if not provided
            var relativePath = GenerateFilePath(request, docType);
            var absolutePath = Path.Combine(projectPath, relativePath);

            // Check if file exists
            if (File.Exists(absolutePath) && !request.Overwrite)
            {
                return CaptureResult.Failed(
                    $"File already exists at {relativePath}. Set overwrite=true to replace.");
            }

            // Prepare the full document content
            var documentContent = PrepareDocumentContent(request, docType);

            // Create the file atomically (write to temp, then move)
            var createResult = await CreateFileAtomicallyAsync(
                absolutePath, documentContent, cancellationToken);

            if (!createResult.Success)
            {
                return CaptureResult.Failed(createResult.Error!);
            }

            _logger.LogInformation(
                "Created document file: {FilePath}",
                relativePath);

            // Index the document
            var indexResult = await _documentIndexer.IndexDocumentAsync(
                relativePath, documentContent, tenantKey, cancellationToken);

            if (!indexResult.IsSuccess)
            {
                warnings.Add($"Document created but indexing failed: {indexResult.Error}");
                return CaptureResult.Succeeded(
                    relativePath,
                    docType,
                    request.Title,
                    documentId: null,
                    indexed: false,
                    warnings: warnings);
            }

            // Add index warnings
            if (indexResult.Warnings.Count > 0)
            {
                warnings.AddRange(indexResult.Warnings);
            }

            _logger.LogInformation(
                "Indexed document {FilePath}: {ChunkCount} chunks",
                relativePath, indexResult.ChunkCount);

            return CaptureResult.Succeeded(
                relativePath,
                docType,
                request.Title,
                documentId: indexResult.Document?.Id,
                indexed: true,
                chunkCount: indexResult.ChunkCount,
                warnings: warnings.Count > 0 ? warnings : null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to handle capture request");
            return CaptureResult.Failed($"Unexpected error: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public Task<ContentValidationResult> ValidateContentAsync(
        string docType,
        string content,
        CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        // Validate doc type
        if (!SupportedDocTypes.Contains(docType.ToLowerInvariant()))
        {
            errors.Add($"Unsupported doc type: {docType}");
            return Task.FromResult(ContentValidationResult.Failure(errors));
        }

        // If content is empty, that's allowed (template will be used)
        if (string.IsNullOrWhiteSpace(content))
        {
            return Task.FromResult(ContentValidationResult.Success());
        }

        // Validate section structure
        var templateValidation = ContentTemplates.ValidateSections(docType, content);

        if (!templateValidation.IsValid)
        {
            foreach (var section in templateValidation.MissingSections)
            {
                errors.Add($"Missing required section: {section}");
            }
        }

        warnings.AddRange(templateValidation.Warnings);

        // Check for potential issues
        if (content.Length > 50000)
        {
            warnings.Add("Content is very long (>50000 chars). Consider splitting into multiple documents.");
        }

        // Check for TL;DR section content
        if (content.Contains("## TL;DR", StringComparison.OrdinalIgnoreCase))
        {
            var tldrMatch = TldrSectionRegex().Match(content);
            if (tldrMatch.Success)
            {
                var tldrContent = tldrMatch.Groups[1].Value.Trim();
                if (tldrContent.StartsWith("<!--") || string.IsNullOrWhiteSpace(tldrContent))
                {
                    warnings.Add("TL;DR section appears to contain only placeholder text.");
                }
            }
        }

        if (errors.Count > 0)
        {
            return Task.FromResult(ContentValidationResult.Failure(errors, warnings));
        }

        return Task.FromResult(ContentValidationResult.Success(warnings));
    }

    /// <inheritdoc />
    public string? GetTemplate(string docType)
    {
        return ContentTemplates.GetTemplate(docType);
    }

    /// <inheritdoc />
    public IReadOnlyList<string> GetSupportedDocTypes()
    {
        return SupportedDocTypes;
    }

    /// <summary>
    /// Generates a file path for the document.
    /// </summary>
    private string GenerateFilePath(CaptureRequest request, string docType)
    {
        if (!string.IsNullOrWhiteSpace(request.FilePath))
        {
            // Ensure the path is within the docs directory
            var normalizedPath = request.FilePath.TrimStart('/').TrimStart('\\');
            if (!normalizedPath.StartsWith(DocsDirectoryName, StringComparison.OrdinalIgnoreCase))
            {
                normalizedPath = Path.Combine(DocsDirectoryName, normalizedPath);
            }

            // Ensure .md extension
            if (!normalizedPath.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            {
                normalizedPath += ".md";
            }

            return normalizedPath.Replace('\\', '/');
        }

        // Generate path from doc type and title
        var sanitizedTitle = SanitizeFileName(request.Title);
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd");
        var fileName = $"{timestamp}-{sanitizedTitle}.md";

        // Organize by doc type subdirectory
        var subDirectory = GetSubdirectoryForDocType(docType);
        return Path.Combine(DocsDirectoryName, subDirectory, fileName).Replace('\\', '/');
    }

    /// <summary>
    /// Gets the subdirectory for a document type.
    /// </summary>
    private static string GetSubdirectoryForDocType(string docType)
    {
        return docType.ToLowerInvariant() switch
        {
            DocumentTypes.Problem => "problems",
            DocumentTypes.Insight => "insights",
            DocumentTypes.Codebase => "codebase",
            DocumentTypes.Tool => "tools",
            DocumentTypes.Style => "styles",
            _ => "misc"
        };
    }

    /// <summary>
    /// Sanitizes a string for use as a file name.
    /// </summary>
    private static string SanitizeFileName(string title)
    {
        // Convert to lowercase and replace spaces with hyphens
        var sanitized = title.ToLowerInvariant()
            .Replace(' ', '-')
            .Replace('_', '-');

        // Remove invalid characters
        sanitized = InvalidCharsRegex().Replace(sanitized, "");

        // Collapse multiple hyphens
        sanitized = MultipleHyphensRegex().Replace(sanitized, "-");

        // Trim hyphens from start and end
        sanitized = sanitized.Trim('-');

        // Limit length
        if (sanitized.Length > 50)
        {
            sanitized = sanitized[..50].TrimEnd('-');
        }

        return sanitized;
    }

    /// <summary>
    /// Prepares the full document content including frontmatter.
    /// </summary>
    private string PrepareDocumentContent(CaptureRequest request, string docType)
    {
        var sb = new StringBuilder();

        // Generate frontmatter
        var frontmatter = _frontmatterGenerator.Generate(request);
        sb.Append(frontmatter);
        sb.AppendLine();

        // Add title as H1
        sb.AppendLine($"# {request.Title}");
        sb.AppendLine();

        // Add content
        if (!string.IsNullOrWhiteSpace(request.Content))
        {
            sb.Append(request.Content.Trim());
        }
        else if (request.UseTemplate)
        {
            // Use template if content is empty and template is requested
            var template = ContentTemplates.GetTemplate(docType);
            if (template != null)
            {
                sb.Append(template.Trim());
            }
        }

        sb.AppendLine();

        return sb.ToString();
    }

    /// <summary>
    /// Creates a file atomically by writing to a temp file and then moving.
    /// </summary>
    private async Task<FileCreateResult> CreateFileAtomicallyAsync(
        string targetPath,
        string content,
        CancellationToken cancellationToken)
    {
        try
        {
            // Ensure directory exists
            var directory = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                _logger.LogDebug("Created directory: {Directory}", directory);
            }

            // Write to temp file first
            var tempPath = targetPath + ".tmp";

            await File.WriteAllTextAsync(tempPath, content, Encoding.UTF8, cancellationToken);

            // Move temp file to target (atomic on most file systems)
            File.Move(tempPath, targetPath, overwrite: true);

            return new FileCreateResult { Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create file: {Path}", targetPath);
            return new FileCreateResult
            {
                Success = false,
                Error = $"Failed to create file: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Regex for matching TL;DR section content.
    /// </summary>
    [GeneratedRegex(@"## TL;DR\s*\n(.*?)(?=\n##|\z)", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex TldrSectionRegex();

    /// <summary>
    /// Regex for matching invalid filename characters.
    /// </summary>
    [GeneratedRegex(@"[^a-z0-9\-]")]
    private static partial Regex InvalidCharsRegex();

    /// <summary>
    /// Regex for collapsing multiple hyphens.
    /// </summary>
    [GeneratedRegex(@"-+")]
    private static partial Regex MultipleHyphensRegex();

    /// <summary>
    /// Result of file creation operation.
    /// </summary>
    private sealed class FileCreateResult
    {
        public bool Success { get; init; }
        public string? Error { get; init; }
    }
}
