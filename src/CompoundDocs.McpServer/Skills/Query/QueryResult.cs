using System.Text.Json.Serialization;

namespace CompoundDocs.McpServer.Skills.Query;

/// <summary>
/// Represents a source document used in query results.
/// </summary>
public sealed class QuerySource
{
    /// <summary>
    /// The file path of the source document.
    /// </summary>
    [JsonPropertyName("file_path")]
    public required string FilePath { get; init; }

    /// <summary>
    /// The document title.
    /// </summary>
    [JsonPropertyName("title")]
    public required string Title { get; init; }

    /// <summary>
    /// The document type.
    /// </summary>
    [JsonPropertyName("doc_type")]
    public required string DocType { get; init; }

    /// <summary>
    /// The promotion level.
    /// </summary>
    [JsonPropertyName("promotion_level")]
    public string? PromotionLevel { get; init; }

    /// <summary>
    /// The relevance score (0.0 to 1.0).
    /// </summary>
    [JsonPropertyName("relevance_score")]
    public required float RelevanceScore { get; init; }
}

/// <summary>
/// Represents a content chunk in query results.
/// </summary>
public sealed class QueryChunk
{
    /// <summary>
    /// The parent document ID.
    /// </summary>
    [JsonPropertyName("document_id")]
    public required string DocumentId { get; init; }

    /// <summary>
    /// The header path within the document.
    /// </summary>
    [JsonPropertyName("header_path")]
    public required string HeaderPath { get; init; }

    /// <summary>
    /// The chunk content.
    /// </summary>
    [JsonPropertyName("content")]
    public required string Content { get; init; }

    /// <summary>
    /// The relevance score.
    /// </summary>
    [JsonPropertyName("relevance_score")]
    public required float RelevanceScore { get; init; }

    /// <summary>
    /// Starting line number in the source document.
    /// </summary>
    [JsonPropertyName("start_line")]
    public required int StartLine { get; init; }

    /// <summary>
    /// Ending line number in the source document.
    /// </summary>
    [JsonPropertyName("end_line")]
    public required int EndLine { get; init; }
}

/// <summary>
/// Result model for RAG query operations.
/// </summary>
public sealed class QueryResult
{
    /// <summary>
    /// The original query.
    /// </summary>
    [JsonPropertyName("query")]
    public required string Query { get; init; }

    /// <summary>
    /// The synthesized answer from documentation.
    /// </summary>
    [JsonPropertyName("answer")]
    public required string Answer { get; init; }

    /// <summary>
    /// Source documents used for the answer.
    /// </summary>
    [JsonPropertyName("sources")]
    public required List<QuerySource> Sources { get; init; }

    /// <summary>
    /// Individual content chunks (if requested).
    /// </summary>
    [JsonPropertyName("chunks")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<QueryChunk>? Chunks { get; init; }

    /// <summary>
    /// Confidence score for the answer (0.0 to 1.0).
    /// </summary>
    [JsonPropertyName("confidence_score")]
    public required float ConfidenceScore { get; init; }
}

/// <summary>
/// Represents a document match in search results.
/// </summary>
public sealed class SearchDocument
{
    /// <summary>
    /// Relative path to the document.
    /// </summary>
    [JsonPropertyName("file_path")]
    public required string FilePath { get; init; }

    /// <summary>
    /// Document title.
    /// </summary>
    [JsonPropertyName("title")]
    public required string Title { get; init; }

    /// <summary>
    /// Document type classification.
    /// </summary>
    [JsonPropertyName("doc_type")]
    public required string DocType { get; init; }

    /// <summary>
    /// Document promotion level.
    /// </summary>
    [JsonPropertyName("promotion_level")]
    public required string PromotionLevel { get; init; }

    /// <summary>
    /// Semantic similarity score (0.0 to 1.0).
    /// </summary>
    [JsonPropertyName("relevance_score")]
    public required float RelevanceScore { get; init; }

    /// <summary>
    /// Preview of document content.
    /// </summary>
    [JsonPropertyName("content_snippet")]
    public string? ContentSnippet { get; init; }
}

/// <summary>
/// Result model for semantic search operations.
/// </summary>
public sealed class SearchQueryResult
{
    /// <summary>
    /// The original search query.
    /// </summary>
    [JsonPropertyName("query")]
    public required string Query { get; init; }

    /// <summary>
    /// Number of documents found.
    /// </summary>
    [JsonPropertyName("total_results")]
    public required int TotalResults { get; init; }

    /// <summary>
    /// Ranked list of matching documents.
    /// </summary>
    [JsonPropertyName("documents")]
    public required List<SearchDocument> Documents { get; init; }
}

/// <summary>
/// Conversation context metadata for recall operations.
/// </summary>
public sealed class RecallContext
{
    /// <summary>
    /// Current turn in the conversation.
    /// </summary>
    [JsonPropertyName("turn_number")]
    public required int TurnNumber { get; init; }

    /// <summary>
    /// Whether this was detected as a follow-up.
    /// </summary>
    [JsonPropertyName("is_follow_up")]
    public required bool IsFollowUp { get; init; }

    /// <summary>
    /// Documents referenced in previous turns.
    /// </summary>
    [JsonPropertyName("previous_documents")]
    public List<string>? PreviousDocuments { get; init; }

    /// <summary>
    /// Session identifier for conversation tracking.
    /// </summary>
    [JsonPropertyName("session_id")]
    public string? SessionId { get; init; }
}

/// <summary>
/// Result model for contextual recall operations.
/// </summary>
public sealed class RecallResult
{
    /// <summary>
    /// The original query.
    /// </summary>
    [JsonPropertyName("query")]
    public required string Query { get; init; }

    /// <summary>
    /// Contextual answer from documentation.
    /// </summary>
    [JsonPropertyName("answer")]
    public required string Answer { get; init; }

    /// <summary>
    /// Source documents for this response.
    /// </summary>
    [JsonPropertyName("sources")]
    public required List<QuerySource> Sources { get; init; }

    /// <summary>
    /// Conversation context metadata.
    /// </summary>
    [JsonPropertyName("context")]
    public required RecallContext Context { get; init; }

    /// <summary>
    /// Suggested follow-up questions.
    /// </summary>
    [JsonPropertyName("suggested_follow_ups")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? SuggestedFollowUps { get; init; }

    /// <summary>
    /// Confidence score for the answer (0.0 to 1.0).
    /// </summary>
    [JsonPropertyName("confidence_score")]
    public required float ConfidenceScore { get; init; }
}

/// <summary>
/// Relationship type between documents.
/// </summary>
public enum RelationshipType
{
    /// <summary>
    /// Document explicitly links to another.
    /// </summary>
    DirectLink,

    /// <summary>
    /// Another document links to this one.
    /// </summary>
    IncomingLink,

    /// <summary>
    /// Documents link to each other.
    /// </summary>
    Bidirectional,

    /// <summary>
    /// Connected through intermediary documents.
    /// </summary>
    TransitiveLink,

    /// <summary>
    /// Semantically similar but not explicitly linked.
    /// </summary>
    Semantic
}

/// <summary>
/// Relationship metadata for a related document.
/// </summary>
public sealed class DocumentRelationship
{
    /// <summary>
    /// Type of relationship.
    /// </summary>
    [JsonPropertyName("type")]
    public required RelationshipType Type { get; init; }

    /// <summary>
    /// Link distance from source (0 for semantic).
    /// </summary>
    [JsonPropertyName("distance")]
    public required int Distance { get; init; }

    /// <summary>
    /// Intermediate document for transitive links.
    /// </summary>
    [JsonPropertyName("via_document")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ViaDocument { get; init; }
}

/// <summary>
/// Represents a related document in the result.
/// </summary>
public sealed class RelatedDocument
{
    /// <summary>
    /// Path to the related document.
    /// </summary>
    [JsonPropertyName("file_path")]
    public required string FilePath { get; init; }

    /// <summary>
    /// Document title.
    /// </summary>
    [JsonPropertyName("title")]
    public required string Title { get; init; }

    /// <summary>
    /// Document type.
    /// </summary>
    [JsonPropertyName("doc_type")]
    public required string DocType { get; init; }

    /// <summary>
    /// Relationship information.
    /// </summary>
    [JsonPropertyName("relationship")]
    public required DocumentRelationship Relationship { get; init; }

    /// <summary>
    /// Combined relevance score.
    /// </summary>
    [JsonPropertyName("relevance_score")]
    public required float RelevanceScore { get; init; }
}

/// <summary>
/// Summary of discovered relationships.
/// </summary>
public sealed class LinkSummary
{
    /// <summary>
    /// Total number of related documents.
    /// </summary>
    [JsonPropertyName("total_related")]
    public required int TotalRelated { get; init; }

    /// <summary>
    /// Number of direct outgoing links.
    /// </summary>
    [JsonPropertyName("direct_links")]
    public required int DirectLinks { get; init; }

    /// <summary>
    /// Number of incoming links.
    /// </summary>
    [JsonPropertyName("incoming_links")]
    public required int IncomingLinks { get; init; }

    /// <summary>
    /// Number of transitive links.
    /// </summary>
    [JsonPropertyName("transitive_links")]
    public required int TransitiveLinks { get; init; }

    /// <summary>
    /// Number of semantic matches.
    /// </summary>
    [JsonPropertyName("semantic_matches")]
    public required int SemanticMatches { get; init; }
}

/// <summary>
/// Information about the source document.
/// </summary>
public sealed class SourceDocumentInfo
{
    /// <summary>
    /// File path of the source document.
    /// </summary>
    [JsonPropertyName("file_path")]
    public required string FilePath { get; init; }

    /// <summary>
    /// Title of the source document.
    /// </summary>
    [JsonPropertyName("title")]
    public required string Title { get; init; }

    /// <summary>
    /// Document type.
    /// </summary>
    [JsonPropertyName("doc_type")]
    public required string DocType { get; init; }
}

/// <summary>
/// Result model for related document discovery operations.
/// </summary>
public sealed class RelatedDocumentsResult
{
    /// <summary>
    /// The document used as the starting point.
    /// </summary>
    [JsonPropertyName("source_document")]
    public required SourceDocumentInfo SourceDocument { get; init; }

    /// <summary>
    /// Documents related to the source.
    /// </summary>
    [JsonPropertyName("related_documents")]
    public required List<RelatedDocument> RelatedDocuments { get; init; }

    /// <summary>
    /// Summary of discovered relationships.
    /// </summary>
    [JsonPropertyName("link_summary")]
    public required LinkSummary LinkSummary { get; init; }
}
