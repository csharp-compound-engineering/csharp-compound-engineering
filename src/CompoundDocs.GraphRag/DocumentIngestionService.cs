using System.Text.RegularExpressions;
using CompoundDocs.Bedrock;
using CompoundDocs.Common.Models;
using CompoundDocs.Common.Parsing;
using CompoundDocs.Graph;
using CompoundDocs.Vector;
using Microsoft.Extensions.Logging;

namespace CompoundDocs.GraphRag;

internal sealed partial class DocumentIngestionService : IDocumentIngestionService
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Information,
        Message = "Ingesting document {DocumentId} ({ContentLength} chars)")]
    private partial void LogIngesting(string documentId, int contentLength);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning,
        Message = "No chunks produced for document {DocumentId}")]
    private partial void LogNoChunks(string documentId);

    [LoggerMessage(EventId = 3, Level = LogLevel.Warning,
        Message = "Embedding failed for chunk {ChunkId}: {ErrorMessage}")]
    private partial void LogEmbeddingFailed(string chunkId, string errorMessage);

    [LoggerMessage(EventId = 4, Level = LogLevel.Warning,
        Message = "Entity extraction failed for chunk {ChunkId}: {ErrorMessage}")]
    private partial void LogEntityExtractionFailed(string chunkId, string errorMessage);

    [LoggerMessage(EventId = 5, Level = LogLevel.Warning,
        Message = "Vector indexing failed for chunk {ChunkId}: {ErrorMessage}")]
    private partial void LogVectorIndexFailed(string chunkId, string errorMessage);

    [LoggerMessage(EventId = 6, Level = LogLevel.Information,
        Message = "Ingestion complete for {DocumentId}: {ChunkCount} chunks, {ConceptCount} concepts")]
    private partial void LogIngestionComplete(string documentId, int chunkCount, int conceptCount);

    [LoggerMessage(EventId = 7, Level = LogLevel.Information,
        Message = "Deleting document {DocumentId}")]
    private partial void LogDeleting(string documentId);

    [LoggerMessage(EventId = 8, Level = LogLevel.Information,
        Message = "Deleted document {DocumentId}")]
    private partial void LogDeleted(string documentId);

    [LoggerMessage(EventId = 9, Level = LogLevel.Warning,
        Message = "Code example upsert failed for chunk {ChunkId}: {ErrorMessage}")]
    private partial void LogCodeExampleFailed(string chunkId, string errorMessage);

    [GeneratedRegex(@"[^a-z0-9\-]")]
    private static partial Regex NonAlphanumericHyphenRegex();

    [GeneratedRegex(@"-{2,}")]
    private static partial Regex MultipleHyphensRegex();

    private readonly IGraphRepository _graphRepository;
    private readonly IVectorStore _vectorStore;
    private readonly IBedrockEmbeddingService _embeddingService;
    private readonly IEntityExtractor _entityExtractor;
    private readonly MarkdownParser _markdownParser;
    private readonly FrontmatterParser _frontmatterParser;
    private readonly ILogger<DocumentIngestionService> _logger;

    public DocumentIngestionService(
        IGraphRepository graphRepository,
        IVectorStore vectorStore,
        IBedrockEmbeddingService embeddingService,
        IEntityExtractor entityExtractor,
        MarkdownParser markdownParser,
        FrontmatterParser frontmatterParser,
        ILogger<DocumentIngestionService> logger)
    {
        _graphRepository = graphRepository;
        _vectorStore = vectorStore;
        _embeddingService = embeddingService;
        _entityExtractor = entityExtractor;
        _markdownParser = markdownParser;
        _frontmatterParser = frontmatterParser;
        _logger = logger;
    }

    public async Task IngestDocumentAsync(string content, DocumentIngestionMetadata metadata, CancellationToken ct = default)
    {
        LogIngesting(metadata.DocumentId, content.Length);

        // 1. Parse frontmatter -> extract body
        var frontmatterResult = _frontmatterParser.Parse(content);
        var body = frontmatterResult.Body;

        // 2. Parse markdown body -> extract headers + internal links
        var document = _markdownParser.Parse(body);
        var headers = _markdownParser.ExtractHeaders(document);
        var links = _markdownParser.ExtractLinks(document);

        // 3. Chunk body at header boundaries
        var chunks = _markdownParser.ChunkByHeaders(body);
        if (chunks.Count == 0)
        {
            LogNoChunks(metadata.DocumentId);
            return;
        }

        // 4. Build sections: filter H2 headers, create SectionNode per H2
        var h2Headers = headers.Where(h => h.Level == 2).ToList();
        var sections = new List<SectionNode>();

        // Check if there is content before the first header (intro section)
        var firstHeaderLine = headers.Count > 0 ? headers[0].Line : int.MaxValue;
        var hasIntroContent = body.Split('\n').Take(firstHeaderLine).Any(l => !string.IsNullOrWhiteSpace(l));

        if (hasIntroContent && (h2Headers.Count == 0 || h2Headers[0].Line > 0))
        {
            sections.Add(new SectionNode
            {
                Id = $"{metadata.DocumentId}:introduction",
                DocumentId = metadata.DocumentId,
                Title = "Introduction",
                Order = 0,
                HeadingLevel = 2
            });
        }

        for (var i = 0; i < h2Headers.Count; i++)
        {
            var h = h2Headers[i];
            sections.Add(new SectionNode
            {
                Id = $"{metadata.DocumentId}:{NormalizeSectionId(h.Text)}",
                DocumentId = metadata.DocumentId,
                Title = h.Text,
                Order = sections.Count,
                HeadingLevel = h.Level
            });
        }

        // 5. Upsert DocumentNode
        await _graphRepository.UpsertDocumentAsync(new DocumentNode
        {
            Id = metadata.DocumentId,
            FilePath = metadata.FilePath,
            Title = metadata.Title,
            DocType = metadata.DocType,
            PromotionLevel = metadata.PromotionLevel,
            CommitHash = metadata.CommitHash
        }, ct);

        // 6. Upsert SectionNodes (also creates HAS_SECTION edges)
        foreach (var section in sections)
        {
            await _graphRepository.UpsertSectionAsync(section, ct);
        }

        // 7. Process each chunk
        var totalConceptCount = 0;
        foreach (var chunk in chunks)
        {
            var parentSection = FindParentSection(chunk.StartLine, h2Headers, sections);
            var chunkId = $"{metadata.DocumentId}:chunk-{chunk.Index}";

            var chunkNode = new ChunkNode
            {
                Id = chunkId,
                SectionId = parentSection.Id,
                DocumentId = metadata.DocumentId,
                Content = chunk.Content,
                Order = chunk.Index,
                TokenCount = EstimateTokenCount(chunk.Content)
            };

            await _graphRepository.UpsertChunkAsync(chunkNode, ct);

            // Generate embedding -> index in vector store
            try
            {
                var embedding = await _embeddingService.GenerateEmbeddingAsync(chunk.Content, ct);

                var vectorMetadata = new Dictionary<string, string>
                {
                    ["document_id"] = metadata.DocumentId,
                    ["section_id"] = parentSection.Id,
                    ["chunk_id"] = chunkId,
                    ["file_path"] = metadata.FilePath,
                    ["repository"] = metadata.Repository,
                    ["header_path"] = chunk.HeaderPath
                };

                try
                {
                    await _vectorStore.IndexAsync(chunkId, embedding, vectorMetadata, ct);
                }
                catch (Exception ex)
                {
                    LogVectorIndexFailed(chunkId, ex.Message);
                }
            }
            catch (Exception ex)
            {
                LogEmbeddingFailed(chunkId, ex.Message);
            }

            // Extract entities -> create ConceptNodes + MENTIONS relationships
            try
            {
                var entities = await _entityExtractor.ExtractEntitiesAsync(chunk.Content, ct);
                foreach (var entity in entities)
                {
                    var conceptId = NormalizeConceptId(entity.Name);
                    await _graphRepository.UpsertConceptAsync(new ConceptNode
                    {
                        Id = conceptId,
                        Name = entity.Name,
                        Description = entity.Description,
                        Category = entity.Type,
                        Aliases = entity.Aliases
                    }, ct);

                    await _graphRepository.CreateRelationshipAsync(new GraphRelationship
                    {
                        Type = "MENTIONS",
                        SourceId = chunkId,
                        TargetId = conceptId
                    }, ct);

                    totalConceptCount++;
                }
            }
            catch (Exception ex)
            {
                LogEntityExtractionFailed(chunkId, ex.Message);
            }

            // Extract code blocks -> create CodeExampleNodes + HAS_CODE_EXAMPLE relationships
            try
            {
                var chunkDocument = _markdownParser.Parse(chunk.Content);
                var codeBlocks = _markdownParser.ExtractCodeBlocks(chunkDocument);
                for (var cbi = 0; cbi < codeBlocks.Count; cbi++)
                {
                    var cb = codeBlocks[cbi];
                    var codeExampleId = $"{chunkId}:code-{cbi}";
                    await _graphRepository.UpsertCodeExampleAsync(new CodeExampleNode
                    {
                        Id = codeExampleId,
                        ChunkId = chunkId,
                        Language = cb.Language,
                        Code = cb.Code
                    }, chunkId, ct);
                }
            }
            catch (Exception ex)
            {
                LogCodeExampleFailed(chunkId, ex.Message);
            }
        }

        // 8. Process internal links -> LINKS_TO relationships
        foreach (var link in links)
        {
            var resolvedPath = ResolveRelativeLink(metadata.FilePath, link.Url);
            if (resolvedPath is null)
            {
                continue;
            }

            var targetDocId = $"{metadata.Repository.ToLowerInvariant()}:{resolvedPath}";
            await _graphRepository.CreateRelationshipAsync(new GraphRelationship
            {
                Type = "LINKS_TO",
                SourceId = metadata.DocumentId,
                TargetId = targetDocId
            }, ct);
        }

        LogIngestionComplete(metadata.DocumentId, chunks.Count, totalConceptCount);
    }

    public async Task DeleteDocumentAsync(string documentId, CancellationToken ct = default)
    {
        LogDeleting(documentId);

        await _vectorStore.DeleteByDocumentIdAsync(documentId, ct);
        await _graphRepository.DeleteDocumentCascadeAsync(documentId, ct);

        LogDeleted(documentId);
    }

    internal static string NormalizeConceptId(string name)
    {
        var normalized = name.Trim().ToLowerInvariant();
        normalized = normalized.Replace(' ', '-');
        normalized = NonAlphanumericHyphenRegex().Replace(normalized, "");
        normalized = MultipleHyphensRegex().Replace(normalized, "-");
        normalized = normalized.Trim('-');
        return $"concept:{normalized}";
    }

    internal static string? ResolveRelativeLink(string sourceFilePath, string linkUrl)
    {
        if (string.IsNullOrEmpty(linkUrl))
        {
            return null;
        }

        // Strip fragment
        var fragmentIndex = linkUrl.IndexOf('#');
        var path = fragmentIndex >= 0 ? linkUrl[..fragmentIndex] : linkUrl;

        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        // Resolve relative to source file's directory
        // GetDirectoryName returns "" for root-level files, non-null for relative paths
        var rawDir = Path.GetDirectoryName(sourceFilePath);
        var sourceDir = rawDir is not null ? rawDir.Replace('\\', '/') : "";
        var combined = sourceDir.Length == 0 ? path : $"{sourceDir}/{path}";

        // Normalize path segments (resolve ../ and ./)
        var segments = combined.Replace('\\', '/').Split('/');
        var stack = new Stack<string>();
        foreach (var segment in segments)
        {
            if (segment == ".." && stack.Count > 0)
            {
                stack.Pop();
            }
            else if (segment != "." && segment != "")
            {
                stack.Push(segment);
            }
        }

        var resolved = string.Join('/', stack.Reverse());
        return resolved.ToLowerInvariant();
    }

    internal static int EstimateTokenCount(string content) => content.Length / 4;

    private static SectionNode FindParentSection(
        int chunkStartLine,
        List<HeaderInfo> h2Headers,
        List<SectionNode> sections)
    {
        // Intro section at index 0 shifts H2-to-section index mapping by 1
        var hasIntro = sections[0].Id.EndsWith(":introduction");

        // Find the last H2 header at or before the chunk start line
        for (var i = h2Headers.Count - 1; i >= 0; i--)
        {
            if (h2Headers[i].Line <= chunkStartLine)
            {
                return sections[hasIntro ? i + 1 : i];
            }
        }

        // Falls before any H2 -> intro section or first section
        return sections[0];
    }

    private static string NormalizeSectionId(string headerText)
    {
        var normalized = headerText.Trim().ToLowerInvariant();
        normalized = normalized.Replace(' ', '-');
        normalized = NonAlphanumericHyphenRegex().Replace(normalized, "");
        normalized = MultipleHyphensRegex().Replace(normalized, "-");
        return normalized.Trim('-');
    }
}
