using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using CompoundDocs.McpServer.Data.Repositories;
using CompoundDocs.McpServer.Filters;
using CompoundDocs.McpServer.Models;
using CompoundDocs.McpServer.SemanticKernel;
using CompoundDocs.McpServer.Session;
using CompoundDocs.McpServer.Tools;
using Microsoft.Extensions.Logging;

namespace CompoundDocs.McpServer.Skills.Query;

/// <summary>
/// Implementation of query skill handlers for RAG query, semantic search,
/// contextual recall, and related document discovery.
/// </summary>
public sealed class QuerySkillHandler : IQuerySkillHandler
{
    private readonly IDocumentRepository _documentRepository;
    private readonly IEmbeddingService _embeddingService;
    private readonly ISessionContext _sessionContext;
    private readonly ILogger<QuerySkillHandler> _logger;

    // Simple in-memory conversation context storage (would be replaced with proper session store in production)
    private static readonly ConcurrentDictionary<string, ConversationContext> _conversations = new();

    /// <summary>
    /// Creates a new instance of QuerySkillHandler.
    /// </summary>
    public QuerySkillHandler(
        IDocumentRepository documentRepository,
        IEmbeddingService embeddingService,
        ISessionContext sessionContext,
        ILogger<QuerySkillHandler> logger)
    {
        _documentRepository = documentRepository ?? throw new ArgumentNullException(nameof(documentRepository));
        _embeddingService = embeddingService ?? throw new ArgumentNullException(nameof(embeddingService));
        _sessionContext = sessionContext ?? throw new ArgumentNullException(nameof(sessionContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    #region HandleQueryAsync

    /// <inheritdoc />
    public async Task<ToolResponse<QueryResult>> HandleQueryAsync(
        QueryRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_sessionContext.IsProjectActive)
        {
            return ToolResponse<QueryResult>.Fail(ToolErrors.NoActiveProject);
        }

        if (string.IsNullOrWhiteSpace(request.Query))
        {
            return ToolResponse<QueryResult>.Fail(ToolErrors.EmptyQuery);
        }

        // Validate and parse parameters
        var (docTypeList, docTypeError) = ValidateDocTypes(request.DocTypes);
        if (docTypeError != null)
        {
            return ToolResponse<QueryResult>.Fail(docTypeError);
        }

        var (minPromotion, promotionError) = ValidatePromotionLevel(request.PromotionLevel);
        if (promotionError != null)
        {
            return ToolResponse<QueryResult>.Fail(promotionError);
        }

        var limit = Math.Clamp(request.Limit, 1, 20);

        _logger.LogInformation(
            "Query: '{Query}', limit={Limit}, docTypes={DocTypes}, promotionLevel={PromotionLevel}",
            request.Query,
            limit,
            request.DocTypes ?? "all",
            request.PromotionLevel ?? "all");

        try
        {
            // Generate query embedding
            var embedding = await _embeddingService.GenerateEmbeddingAsync(request.Query, cancellationToken);

            // Search for relevant chunks
            var chunkResults = await _documentRepository.SearchChunksAsync(
                embedding,
                _sessionContext.TenantKey!,
                limit: limit * 2, // Get more chunks initially for diversity
                minRelevance: 0.0f,
                cancellationToken: cancellationToken);

            if (chunkResults.Count == 0)
            {
                return ToolResponse<QueryResult>.Ok(new QueryResult
                {
                    Query = request.Query,
                    Answer = "No relevant documentation found for your query. Try rephrasing or using different terms.",
                    Sources = [],
                    Chunks = request.IncludeChunks ? [] : null,
                    ConfidenceScore = 0.0f
                });
            }

            // Deduplicate by document and select top chunks
            var documentChunks = chunkResults
                .GroupBy(c => c.Chunk.DocumentId)
                .Take(limit)
                .ToList();

            // Get parent documents for source attribution
            var sources = new List<QuerySource>();
            var chunks = new List<QueryChunk>();

            foreach (var docGroup in documentChunks)
            {
                var topChunk = docGroup
                    .OrderByDescending(c => ApplyPromotionBoost(c.RelevanceScore, c.Chunk.PromotionLevel))
                    .First();

                var document = await _documentRepository.GetByIdAsync(topChunk.Chunk.DocumentId, cancellationToken);

                if (document != null)
                {
                    // Apply filters
                    if (docTypeList != null && !docTypeList.Contains(document.DocType, StringComparer.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (minPromotion.HasValue && ParsePromotionLevel(document.PromotionLevel) < minPromotion.Value)
                    {
                        continue;
                    }

                    sources.Add(new QuerySource
                    {
                        FilePath = document.FilePath,
                        Title = document.Title,
                        DocType = document.DocType,
                        PromotionLevel = document.PromotionLevel,
                        RelevanceScore = topChunk.RelevanceScore
                    });
                }

                if (request.IncludeChunks)
                {
                    foreach (var chunkResult in docGroup.Take(3)) // Max 3 chunks per document
                    {
                        chunks.Add(new QueryChunk
                        {
                            DocumentId = chunkResult.Chunk.DocumentId,
                            HeaderPath = chunkResult.Chunk.HeaderPath,
                            Content = chunkResult.Chunk.Content,
                            RelevanceScore = chunkResult.RelevanceScore,
                            StartLine = chunkResult.Chunk.StartLine,
                            EndLine = chunkResult.Chunk.EndLine
                        });
                    }
                }
            }

            // Build context for RAG synthesis
            var context = BuildContext(chunkResults.Take(limit * 2).ToList());

            // Synthesize answer
            var answer = SynthesizeAnswer(request.Query, context, sources);

            // Calculate confidence based on relevance scores
            var avgRelevance = sources.Count > 0 ? sources.Average(s => s.RelevanceScore) : 0f;
            var confidenceScore = Math.Min(1.0f, avgRelevance * 1.2f);

            _logger.LogInformation(
                "Query completed: {SourceCount} sources, confidence={Confidence:F2}",
                sources.Count,
                confidenceScore);

            return ToolResponse<QueryResult>.Ok(new QueryResult
            {
                Query = request.Query,
                Answer = answer,
                Sources = sources,
                Chunks = request.IncludeChunks ? chunks : null,
                ConfidenceScore = confidenceScore
            });
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Query cancelled");
            return ToolResponse<QueryResult>.Fail(ToolErrors.OperationCancelled);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during query");
            return ToolResponse<QueryResult>.Fail(ToolErrors.RagSynthesisFailed(ex.Message));
        }
    }

    #endregion

    #region HandleSearchAsync

    /// <inheritdoc />
    public async Task<ToolResponse<SearchQueryResult>> HandleSearchAsync(
        SearchRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_sessionContext.IsProjectActive)
        {
            return ToolResponse<SearchQueryResult>.Fail(ToolErrors.NoActiveProject);
        }

        if (string.IsNullOrWhiteSpace(request.Query))
        {
            return ToolResponse<SearchQueryResult>.Fail(ToolErrors.EmptyQuery);
        }

        // Validate and parse parameters
        var (docTypeList, docTypeError) = ValidateDocTypes(request.DocTypes);
        if (docTypeError != null)
        {
            return ToolResponse<SearchQueryResult>.Fail(docTypeError);
        }

        var (minPromotion, promotionError) = ValidatePromotionLevel(request.PromotionLevel);
        if (promotionError != null)
        {
            return ToolResponse<SearchQueryResult>.Fail(promotionError);
        }

        var limit = Math.Clamp(request.Limit, 1, 100);
        var minRelevance = Math.Clamp(request.MinRelevance, 0.0f, 1.0f);

        _logger.LogInformation(
            "Search: query='{Query}', limit={Limit}, minRelevance={MinRelevance}",
            request.Query,
            limit,
            minRelevance);

        try
        {
            // Generate query embedding
            var embedding = await _embeddingService.GenerateEmbeddingAsync(request.Query, cancellationToken);

            // Search documents
            var searchResults = await _documentRepository.SearchAsync(
                embedding,
                _sessionContext.TenantKey!,
                limit: limit,
                minRelevance: minRelevance,
                docType: docTypeList?.FirstOrDefault(),
                cancellationToken: cancellationToken);

            // Apply promotion level boost and filter
            var documents = searchResults
                .Select(r => new SearchDocument
                {
                    FilePath = r.Document.FilePath,
                    Title = r.Document.Title,
                    DocType = r.Document.DocType,
                    PromotionLevel = r.Document.PromotionLevel,
                    RelevanceScore = ApplyPromotionBoost(r.RelevanceScore, r.Document.PromotionLevel),
                    ContentSnippet = GetContentSnippet(r.Document.Content, 200)
                })
                .OrderByDescending(r => r.RelevanceScore)
                .ToList();

            // Filter by promotion level if specified
            if (minPromotion.HasValue)
            {
                documents = documents
                    .Where(d => ParsePromotionLevel(d.PromotionLevel) >= minPromotion.Value)
                    .ToList();
            }

            // Filter by doc types if multiple were specified
            if (docTypeList != null && docTypeList.Count > 1)
            {
                documents = documents
                    .Where(d => docTypeList.Contains(d.DocType, StringComparer.OrdinalIgnoreCase))
                    .ToList();
            }

            _logger.LogInformation(
                "Search completed: {ResultCount} results for query '{Query}'",
                documents.Count,
                request.Query);

            return ToolResponse<SearchQueryResult>.Ok(new SearchQueryResult
            {
                Query = request.Query,
                TotalResults = documents.Count,
                Documents = documents
            });
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Search cancelled");
            return ToolResponse<SearchQueryResult>.Fail(ToolErrors.OperationCancelled);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during search");
            return ToolResponse<SearchQueryResult>.Fail(ToolErrors.SearchFailed(ex.Message));
        }
    }

    #endregion

    #region HandleRecallAsync

    /// <inheritdoc />
    public async Task<ToolResponse<RecallResult>> HandleRecallAsync(
        RecallRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_sessionContext.IsProjectActive)
        {
            return ToolResponse<RecallResult>.Fail(ToolErrors.NoActiveProject);
        }

        if (string.IsNullOrWhiteSpace(request.Query))
        {
            return ToolResponse<RecallResult>.Fail(ToolErrors.EmptyQuery);
        }

        // Validate parameters
        var (docTypeList, docTypeError) = ValidateDocTypes(request.DocTypes);
        if (docTypeError != null)
        {
            return ToolResponse<RecallResult>.Fail(docTypeError);
        }

        var (minPromotion, promotionError) = ValidatePromotionLevel(request.PromotionLevel);
        if (promotionError != null)
        {
            return ToolResponse<RecallResult>.Fail(promotionError);
        }

        var limit = Math.Clamp(request.Limit, 1, 20);

        // Get or create conversation context
        var sessionId = request.SessionId ?? $"{_sessionContext.TenantKey}:{Guid.NewGuid():N}";
        var context = GetOrCreateConversation(sessionId, request.ContextMode);
        var isFollowUp = context.TurnNumber > 0;

        _logger.LogInformation(
            "Recall: query='{Query}', sessionId={SessionId}, turn={Turn}, isFollowUp={IsFollowUp}",
            request.Query,
            sessionId,
            context.TurnNumber + 1,
            isFollowUp);

        try
        {
            // Build expanded query with context if this is a follow-up
            var expandedQuery = isFollowUp && request.IncludeHistory
                ? ExpandQueryWithContext(request.Query, context)
                : request.Query;

            // Generate query embedding
            var embedding = await _embeddingService.GenerateEmbeddingAsync(expandedQuery, cancellationToken);

            // Search for relevant chunks
            var chunkResults = await _documentRepository.SearchChunksAsync(
                embedding,
                _sessionContext.TenantKey!,
                limit: limit * 2,
                minRelevance: 0.0f,
                cancellationToken: cancellationToken);

            if (chunkResults.Count == 0)
            {
                return ToolResponse<RecallResult>.Ok(new RecallResult
                {
                    Query = request.Query,
                    Answer = "No relevant documentation found. Try rephrasing your question.",
                    Sources = [],
                    Context = new RecallContext
                    {
                        TurnNumber = context.TurnNumber + 1,
                        IsFollowUp = isFollowUp,
                        PreviousDocuments = context.ReferencedDocuments.ToList(),
                        SessionId = sessionId
                    },
                    SuggestedFollowUps = null,
                    ConfidenceScore = 0.0f
                });
            }

            // Process results similar to query
            var documentChunks = chunkResults
                .GroupBy(c => c.Chunk.DocumentId)
                .Take(limit)
                .ToList();

            var sources = new List<QuerySource>();

            foreach (var docGroup in documentChunks)
            {
                var topChunk = docGroup
                    .OrderByDescending(c => ApplyPromotionBoost(c.RelevanceScore, c.Chunk.PromotionLevel))
                    .First();

                var document = await _documentRepository.GetByIdAsync(topChunk.Chunk.DocumentId, cancellationToken);

                if (document != null)
                {
                    if (docTypeList != null && !docTypeList.Contains(document.DocType, StringComparer.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (minPromotion.HasValue && ParsePromotionLevel(document.PromotionLevel) < minPromotion.Value)
                    {
                        continue;
                    }

                    sources.Add(new QuerySource
                    {
                        FilePath = document.FilePath,
                        Title = document.Title,
                        DocType = document.DocType,
                        PromotionLevel = document.PromotionLevel,
                        RelevanceScore = topChunk.RelevanceScore
                    });
                }
            }

            // Build context and synthesize answer
            var ragContext = BuildContext(chunkResults.Take(limit * 2).ToList());
            var answer = SynthesizeAnswer(request.Query, ragContext, sources);

            // Calculate confidence
            var avgRelevance = sources.Count > 0 ? sources.Average(s => s.RelevanceScore) : 0f;
            var confidenceScore = Math.Min(1.0f, avgRelevance * 1.2f);

            // Generate suggested follow-ups
            var suggestedFollowUps = GenerateSuggestedFollowUps(request.Query, sources);

            // Update conversation context
            context.TurnNumber++;
            context.PreviousQueries.Add(request.Query);
            context.PreviousResponses.Add(answer);
            foreach (var source in sources)
            {
                if (!context.ReferencedDocuments.Contains(source.FilePath))
                {
                    context.ReferencedDocuments.Add(source.FilePath);
                }
            }

            _logger.LogInformation(
                "Recall completed: {SourceCount} sources, turn={Turn}, confidence={Confidence:F2}",
                sources.Count,
                context.TurnNumber,
                confidenceScore);

            return ToolResponse<RecallResult>.Ok(new RecallResult
            {
                Query = request.Query,
                Answer = answer,
                Sources = sources,
                Context = new RecallContext
                {
                    TurnNumber = context.TurnNumber,
                    IsFollowUp = isFollowUp,
                    PreviousDocuments = context.ReferencedDocuments.ToList(),
                    SessionId = sessionId
                },
                SuggestedFollowUps = suggestedFollowUps,
                ConfidenceScore = confidenceScore
            });
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Recall cancelled");
            return ToolResponse<RecallResult>.Fail(ToolErrors.OperationCancelled);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during recall");
            return ToolResponse<RecallResult>.Fail(ToolErrors.RagSynthesisFailed(ex.Message));
        }
    }

    #endregion

    #region HandleRelatedAsync

    /// <inheritdoc />
    public async Task<ToolResponse<RelatedDocumentsResult>> HandleRelatedAsync(
        RelatedRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_sessionContext.IsProjectActive)
        {
            return ToolResponse<RelatedDocumentsResult>.Fail(ToolErrors.NoActiveProject);
        }

        if (string.IsNullOrWhiteSpace(request.FilePath) && string.IsNullOrWhiteSpace(request.Query))
        {
            return ToolResponse<RelatedDocumentsResult>.Fail(
                ToolErrors.MissingParameter("Either file_path or query must be provided"));
        }

        var (docTypeList, docTypeError) = ValidateDocTypes(request.DocTypes);
        if (docTypeError != null)
        {
            return ToolResponse<RelatedDocumentsResult>.Fail(docTypeError);
        }

        var depth = Math.Clamp(request.Depth, 1, 3);
        var limit = Math.Clamp(request.Limit, 1, 50);

        _logger.LogInformation(
            "Related: filePath={FilePath}, query={Query}, depth={Depth}, limit={Limit}",
            request.FilePath ?? "null",
            request.Query ?? "null",
            depth,
            limit);

        try
        {
            // Resolve source document
            CompoundDocument? sourceDocument = null;

            if (!string.IsNullOrWhiteSpace(request.FilePath))
            {
                sourceDocument = await _documentRepository.GetByTenantKeyAsync(
                    _sessionContext.TenantKey!,
                    request.FilePath,
                    cancellationToken);

                if (sourceDocument == null)
                {
                    return ToolResponse<RelatedDocumentsResult>.Fail(
                        ToolErrors.DocumentNotFound(request.FilePath));
                }
            }
            else if (!string.IsNullOrWhiteSpace(request.Query))
            {
                // Search for a starting document
                var embedding = await _embeddingService.GenerateEmbeddingAsync(request.Query, cancellationToken);
                var searchResults = await _documentRepository.SearchAsync(
                    embedding,
                    _sessionContext.TenantKey!,
                    limit: 1,
                    minRelevance: 0.0f,
                    cancellationToken: cancellationToken);

                if (searchResults.Count == 0)
                {
                    return ToolResponse<RelatedDocumentsResult>.Fail(
                        ToolErrors.DocumentNotFound($"No document found matching query: {request.Query}"));
                }

                sourceDocument = searchResults[0].Document;
            }

            // Extract links from source document
            var directLinks = ParseDocumentLinks(sourceDocument!.Links);

            // Build related documents list
            var relatedDocuments = new List<RelatedDocument>();
            var visited = new HashSet<string> { sourceDocument.FilePath };

            // Track link statistics
            var directLinkCount = 0;
            var incomingLinkCount = 0;
            var transitiveLinkCount = 0;
            var semanticMatchCount = 0;

            // Process direct outgoing links
            if (request.LinkTypes == LinkType.All || request.LinkTypes == LinkType.Outgoing || request.LinkTypes == LinkType.Bidirectional)
            {
                foreach (var linkedPath in directLinks)
                {
                    if (visited.Contains(linkedPath)) continue;

                    var linkedDoc = await _documentRepository.GetByTenantKeyAsync(
                        _sessionContext.TenantKey!,
                        linkedPath,
                        cancellationToken);

                    if (linkedDoc != null)
                    {
                        visited.Add(linkedPath);

                        // Check if this is bidirectional
                        var reverseLinks = ParseDocumentLinks(linkedDoc.Links);
                        var isBidirectional = reverseLinks.Contains(sourceDocument.FilePath);

                        if (request.LinkTypes == LinkType.Bidirectional && !isBidirectional)
                        {
                            continue;
                        }

                        relatedDocuments.Add(new RelatedDocument
                        {
                            FilePath = linkedDoc.FilePath,
                            Title = linkedDoc.Title,
                            DocType = linkedDoc.DocType,
                            Relationship = new DocumentRelationship
                            {
                                Type = isBidirectional ? RelationshipType.Bidirectional : RelationshipType.DirectLink,
                                Distance = 1,
                                ViaDocument = null
                            },
                            RelevanceScore = isBidirectional ? 1.0f : 0.9f
                        });
                        directLinkCount++;
                    }
                }
            }

            // Process incoming links (find documents that link to source)
            if (request.LinkTypes == LinkType.All || request.LinkTypes == LinkType.Incoming)
            {
                var allDocs = await _documentRepository.GetAllForTenantAsync(
                    _sessionContext.TenantKey!,
                    cancellationToken: cancellationToken);

                foreach (var doc in allDocs)
                {
                    if (visited.Contains(doc.FilePath)) continue;

                    var docLinks = ParseDocumentLinks(doc.Links);
                    if (docLinks.Contains(sourceDocument.FilePath))
                    {
                        visited.Add(doc.FilePath);
                        relatedDocuments.Add(new RelatedDocument
                        {
                            FilePath = doc.FilePath,
                            Title = doc.Title,
                            DocType = doc.DocType,
                            Relationship = new DocumentRelationship
                            {
                                Type = RelationshipType.IncomingLink,
                                Distance = 1,
                                ViaDocument = null
                            },
                            RelevanceScore = 0.85f
                        });
                        incomingLinkCount++;
                    }
                }
            }

            // Process transitive links for depth > 1
            if (depth > 1 && (request.LinkTypes == LinkType.All || request.LinkTypes == LinkType.Outgoing))
            {
                var currentLevel = relatedDocuments
                    .Where(d => d.Relationship.Distance == 1 && d.Relationship.Type == RelationshipType.DirectLink)
                    .Select(d => d.FilePath)
                    .ToList();

                for (int currentDepth = 2; currentDepth <= depth; currentDepth++)
                {
                    var nextLevel = new List<string>();

                    foreach (var docPath in currentLevel)
                    {
                        var doc = await _documentRepository.GetByTenantKeyAsync(
                            _sessionContext.TenantKey!,
                            docPath,
                            cancellationToken);

                        if (doc == null) continue;

                        var transitiveLinks = ParseDocumentLinks(doc.Links);
                        foreach (var linkedPath in transitiveLinks)
                        {
                            if (visited.Contains(linkedPath)) continue;

                            var linkedDoc = await _documentRepository.GetByTenantKeyAsync(
                                _sessionContext.TenantKey!,
                                linkedPath,
                                cancellationToken);

                            if (linkedDoc != null)
                            {
                                visited.Add(linkedPath);
                                nextLevel.Add(linkedPath);

                                relatedDocuments.Add(new RelatedDocument
                                {
                                    FilePath = linkedDoc.FilePath,
                                    Title = linkedDoc.Title,
                                    DocType = linkedDoc.DocType,
                                    Relationship = new DocumentRelationship
                                    {
                                        Type = RelationshipType.TransitiveLink,
                                        Distance = currentDepth,
                                        ViaDocument = docPath
                                    },
                                    RelevanceScore = (float)Math.Pow(0.7, currentDepth - 1)
                                });
                                transitiveLinkCount++;
                            }
                        }
                    }

                    currentLevel = nextLevel;
                }
            }

            // Add semantic matches
            if (request.IncludeSemantic)
            {
                var embedding = sourceDocument.Vector ?? await _embeddingService.GenerateEmbeddingAsync(
                    sourceDocument.Content,
                    cancellationToken);

                var semanticResults = await _documentRepository.SearchAsync(
                    embedding,
                    _sessionContext.TenantKey!,
                    limit: limit,
                    minRelevance: 0.5f,
                    cancellationToken: cancellationToken);

                foreach (var result in semanticResults)
                {
                    if (visited.Contains(result.Document.FilePath)) continue;

                    visited.Add(result.Document.FilePath);
                    relatedDocuments.Add(new RelatedDocument
                    {
                        FilePath = result.Document.FilePath,
                        Title = result.Document.Title,
                        DocType = result.Document.DocType,
                        Relationship = new DocumentRelationship
                        {
                            Type = RelationshipType.Semantic,
                            Distance = 0,
                            ViaDocument = null
                        },
                        RelevanceScore = result.RelevanceScore * 0.8f
                    });
                    semanticMatchCount++;
                }
            }

            // Apply doc type filter
            if (docTypeList != null)
            {
                relatedDocuments = relatedDocuments
                    .Where(d => docTypeList.Contains(d.DocType, StringComparer.OrdinalIgnoreCase))
                    .ToList();
            }

            // Sort by relevance and limit
            relatedDocuments = relatedDocuments
                .OrderByDescending(d => d.RelevanceScore)
                .Take(limit)
                .ToList();

            _logger.LogInformation(
                "Related completed: {Count} documents found (direct={Direct}, incoming={Incoming}, transitive={Transitive}, semantic={Semantic})",
                relatedDocuments.Count,
                directLinkCount,
                incomingLinkCount,
                transitiveLinkCount,
                semanticMatchCount);

            return ToolResponse<RelatedDocumentsResult>.Ok(new RelatedDocumentsResult
            {
                SourceDocument = new SourceDocumentInfo
                {
                    FilePath = sourceDocument.FilePath,
                    Title = sourceDocument.Title,
                    DocType = sourceDocument.DocType
                },
                RelatedDocuments = relatedDocuments,
                LinkSummary = new LinkSummary
                {
                    TotalRelated = relatedDocuments.Count,
                    DirectLinks = directLinkCount,
                    IncomingLinks = incomingLinkCount,
                    TransitiveLinks = transitiveLinkCount,
                    SemanticMatches = semanticMatchCount
                }
            });
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Related search cancelled");
            return ToolResponse<RelatedDocumentsResult>.Fail(ToolErrors.OperationCancelled);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during related document search");
            return ToolResponse<RelatedDocumentsResult>.Fail(ToolErrors.SearchFailed(ex.Message));
        }
    }

    #endregion

    #region Helper Methods

    private static (List<string>? docTypes, ToolError? error) ValidateDocTypes(string? docTypes)
    {
        if (string.IsNullOrWhiteSpace(docTypes))
        {
            return (null, null);
        }

        var docTypeList = docTypes
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        foreach (var dt in docTypeList)
        {
            if (!DocumentTypes.IsValid(dt))
            {
                return (null, ToolErrors.InvalidDocType(dt));
            }
        }

        return (docTypeList, null);
    }

    private static (PromotionLevel? level, ToolError? error) ValidatePromotionLevel(string? promotionLevel)
    {
        if (string.IsNullOrWhiteSpace(promotionLevel))
        {
            return (null, null);
        }

        if (!Enum.TryParse<PromotionLevel>(promotionLevel, ignoreCase: true, out var parsed))
        {
            return (null, ToolErrors.InvalidPromotionLevel(promotionLevel));
        }

        return (parsed, null);
    }

    private static float ApplyPromotionBoost(float score, string promotionLevel)
    {
        return score * PromotionLevels.GetBoostFactor(promotionLevel);
    }

    private static PromotionLevel ParsePromotionLevel(string level)
    {
        return level.ToLowerInvariant() switch
        {
            "standard" => PromotionLevel.Standard,
            "important" or "promoted" => PromotionLevel.Important,
            "critical" or "pinned" => PromotionLevel.Critical,
            _ => PromotionLevel.Standard
        };
    }

    private static string GetContentSnippet(string content, int maxLength)
    {
        if (string.IsNullOrEmpty(content))
        {
            return string.Empty;
        }

        if (content.Length <= maxLength)
        {
            return content;
        }

        return content[..maxLength] + "...";
    }

    private static string BuildContext(IReadOnlyList<ChunkSearchResult> chunks)
    {
        var sb = new StringBuilder();

        foreach (var chunk in chunks.OrderByDescending(c => c.RelevanceScore))
        {
            sb.AppendLine($"--- [{chunk.Chunk.HeaderPath}] ---");
            sb.AppendLine(chunk.Chunk.Content);
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string SynthesizeAnswer(string query, string context, List<QuerySource> sources)
    {
        // Note: In a full implementation, this would use Semantic Kernel's chat completion
        // to synthesize an answer from the context. For now, we provide the context with
        // source attribution.

        var sb = new StringBuilder();
        sb.AppendLine("Based on the project documentation, here is the relevant information:");
        sb.AppendLine();
        sb.AppendLine(context);
        sb.AppendLine();
        sb.AppendLine("Sources:");
        foreach (var source in sources)
        {
            sb.AppendLine($"- [{source.Title}]({source.FilePath}) (relevance: {source.RelevanceScore:F2})");
        }

        return sb.ToString();
    }

    private static List<string> ParseDocumentLinks(string? linksJson)
    {
        if (string.IsNullOrWhiteSpace(linksJson))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(linksJson) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private ConversationContext GetOrCreateConversation(string sessionId, ContextMode mode)
    {
        if (mode == ContextMode.New)
        {
            var newContext = new ConversationContext();
            _conversations[sessionId] = newContext;
            return newContext;
        }

        return _conversations.GetOrAdd(sessionId, _ => new ConversationContext());
    }

    private static string ExpandQueryWithContext(string query, ConversationContext context)
    {
        if (context.PreviousQueries.Count == 0)
        {
            return query;
        }

        // Simple context expansion - in production would use more sophisticated NLP
        var lastQuery = context.PreviousQueries.Last();
        var referencedDocs = string.Join(", ", context.ReferencedDocuments.TakeLast(3));

        return $"{query} (in context of: {lastQuery}; related documents: {referencedDocs})";
    }

    private static List<string>? GenerateSuggestedFollowUps(string query, List<QuerySource> sources)
    {
        // Simple follow-up generation based on source documents
        if (sources.Count == 0)
        {
            return null;
        }

        var suggestions = new List<string>();

        // Suggest exploring specific sources
        if (sources.Count > 1)
        {
            suggestions.Add($"Can you tell me more about {sources[0].Title}?");
        }

        // Suggest related topics based on doc types
        var docTypes = sources.Select(s => s.DocType).Distinct().ToList();
        if (docTypes.Contains("spec"))
        {
            suggestions.Add("What are the implementation details for this specification?");
        }
        if (docTypes.Contains("adr"))
        {
            suggestions.Add("What were the alternatives considered in this decision?");
        }

        return suggestions.Count > 0 ? suggestions : null;
    }

    #endregion

    #region Nested Types

    /// <summary>
    /// Tracks conversation context for multi-turn recall.
    /// </summary>
    private sealed class ConversationContext
    {
        public int TurnNumber { get; set; }
        public List<string> PreviousQueries { get; } = [];
        public List<string> PreviousResponses { get; } = [];
        public List<string> ReferencedDocuments { get; } = [];
    }

    #endregion
}
