using System.Text.Json;
using CompoundDocs.Common.Models;
using Microsoft.Extensions.Logging;

namespace CompoundDocs.Graph;

public sealed class NeptuneGraphRepository : IGraphRepository
{
    private readonly INeptuneClient _client;
    private readonly ILogger<NeptuneGraphRepository> _logger;

    public NeptuneGraphRepository(
        INeptuneClient client,
        ILogger<NeptuneGraphRepository> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task UpsertDocumentAsync(DocumentNode document, CancellationToken ct = default)
    {
        var query = """
            MERGE (d:Document {id: $id})
            SET d.filePath = $filePath,
                d.title = $title,
                d.docType = $docType,
                d.promotionLevel = $promotionLevel,
                d.lastUpdated = $lastUpdated,
                d.commitHash = $commitHash
            RETURN d
            """;

        var parameters = new Dictionary<string, object>
        {
            ["id"] = document.Id,
            ["filePath"] = document.FilePath,
            ["title"] = document.Title,
            ["docType"] = document.DocType ?? string.Empty,
            ["promotionLevel"] = document.PromotionLevel,
            ["lastUpdated"] = document.LastUpdated.ToString("O"),
            ["commitHash"] = document.CommitHash ?? string.Empty
        };

        await _client.ExecuteOpenCypherAsync(query, parameters, ct);
        _logger.LogDebug("Upserted document {DocumentId}", document.Id);
    }

    public async Task UpsertSectionAsync(SectionNode section, CancellationToken ct = default)
    {
        var query = """
            MERGE (s:Section {id: $id})
            SET s.documentId = $documentId,
                s.title = $title,
                s.order = $order,
                s.headingLevel = $headingLevel
            WITH s
            MATCH (d:Document {id: $documentId})
            MERGE (d)-[:HAS_SECTION]->(s)
            RETURN s
            """;

        var parameters = new Dictionary<string, object>
        {
            ["id"] = section.Id,
            ["documentId"] = section.DocumentId,
            ["title"] = section.Title,
            ["order"] = section.Order,
            ["headingLevel"] = section.HeadingLevel
        };

        await _client.ExecuteOpenCypherAsync(query, parameters, ct);
        _logger.LogDebug("Upserted section {SectionId}", section.Id);
    }

    public async Task UpsertChunkAsync(ChunkNode chunk, CancellationToken ct = default)
    {
        var query = """
            MERGE (c:Chunk {id: $id})
            SET c.sectionId = $sectionId,
                c.documentId = $documentId,
                c.content = $content,
                c.order = $order,
                c.tokenCount = $tokenCount
            WITH c
            MATCH (s:Section {id: $sectionId})
            MERGE (s)-[:HAS_CHUNK]->(c)
            RETURN c
            """;

        var parameters = new Dictionary<string, object>
        {
            ["id"] = chunk.Id,
            ["sectionId"] = chunk.SectionId,
            ["documentId"] = chunk.DocumentId,
            ["content"] = chunk.Content,
            ["order"] = chunk.Order,
            ["tokenCount"] = chunk.TokenCount
        };

        await _client.ExecuteOpenCypherAsync(query, parameters, ct);
        _logger.LogDebug("Upserted chunk {ChunkId}", chunk.Id);
    }

    public async Task UpsertConceptAsync(ConceptNode concept, CancellationToken ct = default)
    {
        var query = """
            MERGE (c:Concept {id: $id})
            SET c.name = $name,
                c.description = $description,
                c.category = $category,
                c.aliases = $aliases
            RETURN c
            """;

        var parameters = new Dictionary<string, object>
        {
            ["id"] = concept.Id,
            ["name"] = concept.Name,
            ["description"] = concept.Description ?? string.Empty,
            ["category"] = concept.Category ?? string.Empty,
            ["aliases"] = JsonSerializer.Serialize(concept.Aliases)
        };

        await _client.ExecuteOpenCypherAsync(query, parameters, ct);
        _logger.LogDebug("Upserted concept {ConceptId}", concept.Id);
    }

    public async Task CreateRelationshipAsync(GraphRelationship relationship, CancellationToken ct = default)
    {
        var query = $$"""
            MATCH (a {id: $sourceId})
            MATCH (b {id: $targetId})
            MERGE (a)-[r:{{relationship.Type}}]->(b)
            SET r += $properties
            RETURN r
            """;

        var parameters = new Dictionary<string, object>
        {
            ["sourceId"] = relationship.SourceId,
            ["targetId"] = relationship.TargetId,
            ["properties"] = JsonSerializer.Serialize(relationship.Properties)
        };

        await _client.ExecuteOpenCypherAsync(query, parameters, ct);
        _logger.LogDebug("Created relationship {Type} from {Source} to {Target}",
            relationship.Type, relationship.SourceId, relationship.TargetId);
    }

    public async Task DeleteDocumentCascadeAsync(string documentId, CancellationToken ct = default)
    {
        var query = """
            MATCH (d:Document {id: $documentId})
            OPTIONAL MATCH (d)-[:HAS_SECTION]->(s:Section)-[:HAS_CHUNK]->(c:Chunk)
            DETACH DELETE c, s, d
            """;

        var parameters = new Dictionary<string, object>
        {
            ["documentId"] = documentId
        };

        await _client.ExecuteOpenCypherAsync(query, parameters, ct);
        _logger.LogInformation("Cascade deleted document {DocumentId}", documentId);
    }

    public async Task<List<ConceptNode>> GetRelatedConceptsAsync(
        string conceptId,
        int hops = 2,
        CancellationToken ct = default)
    {
        var query = $$"""
            MATCH (c:Concept {id: $conceptId})-[:RELATES_TO*1..{{hops}}]-(related:Concept)
            RETURN DISTINCT related.id AS id, related.name AS name,
                   related.description AS description, related.category AS category,
                   related.aliases AS aliases
            """;

        var parameters = new Dictionary<string, object>
        {
            ["conceptId"] = conceptId
        };

        var result = await _client.ExecuteOpenCypherAsync(query, parameters, ct);
        return ParseConceptNodes(result);
    }

    public async Task<List<ChunkNode>> GetChunksByConceptAsync(
        string conceptId,
        CancellationToken ct = default)
    {
        var query = """
            MATCH (concept:Concept {id: $conceptId})<-[:MENTIONS]-(chunk:Chunk)
            RETURN chunk.id AS id, chunk.sectionId AS sectionId,
                   chunk.documentId AS documentId, chunk.content AS content,
                   chunk.order AS order, chunk.tokenCount AS tokenCount
            """;

        var parameters = new Dictionary<string, object>
        {
            ["conceptId"] = conceptId
        };

        var result = await _client.ExecuteOpenCypherAsync(query, parameters, ct);
        return ParseChunkNodes(result);
    }

    public async Task<List<DocumentNode>> GetLinkedDocumentsAsync(
        string documentId,
        CancellationToken ct = default)
    {
        var query = """
            MATCH (d:Document {id: $documentId})-[:LINKS_TO]->(linked:Document)
            RETURN linked.id AS id,
                   linked.filePath AS filePath, linked.title AS title,
                   linked.docType AS docType, linked.promotionLevel AS promotionLevel,
                   linked.lastUpdated AS lastUpdated, linked.commitHash AS commitHash
            """;

        var parameters = new Dictionary<string, object>
        {
            ["documentId"] = documentId
        };

        var result = await _client.ExecuteOpenCypherAsync(query, parameters, ct);
        return ParseDocumentNodes(result);
    }

    private static List<ConceptNode> ParseConceptNodes(JsonElement result)
    {
        var concepts = new List<ConceptNode>();
        if (result.ValueKind != JsonValueKind.Array) return concepts;

        foreach (var item in result.EnumerateArray())
        {
            concepts.Add(new ConceptNode
            {
                Id = item.GetProperty("id").GetString()!,
                Name = item.GetProperty("name").GetString()!,
                Description = item.TryGetProperty("description", out var desc) ? desc.GetString() : null,
                Category = item.TryGetProperty("category", out var cat) ? cat.GetString() : null
            });
        }

        return concepts;
    }

    private static List<ChunkNode> ParseChunkNodes(JsonElement result)
    {
        var chunks = new List<ChunkNode>();
        if (result.ValueKind != JsonValueKind.Array) return chunks;

        foreach (var item in result.EnumerateArray())
        {
            chunks.Add(new ChunkNode
            {
                Id = item.GetProperty("id").GetString()!,
                SectionId = item.GetProperty("sectionId").GetString()!,
                DocumentId = item.GetProperty("documentId").GetString()!,
                Content = item.GetProperty("content").GetString()!,
                Order = item.TryGetProperty("order", out var order) ? order.GetInt32() : 0,
                TokenCount = item.TryGetProperty("tokenCount", out var tc) ? tc.GetInt32() : 0
            });
        }

        return chunks;
    }

    private static List<DocumentNode> ParseDocumentNodes(JsonElement result)
    {
        var documents = new List<DocumentNode>();
        if (result.ValueKind != JsonValueKind.Array) return documents;

        foreach (var item in result.EnumerateArray())
        {
            documents.Add(new DocumentNode
            {
                Id = item.GetProperty("id").GetString()!,
                FilePath = item.GetProperty("filePath").GetString()!,
                Title = item.GetProperty("title").GetString()!,
                DocType = item.TryGetProperty("docType", out var dt) ? dt.GetString() : null,
                PromotionLevel = item.TryGetProperty("promotionLevel", out var pl) ? pl.GetString() ?? "draft" : "draft",
                CommitHash = item.TryGetProperty("commitHash", out var ch) ? ch.GetString() : null
            });
        }

        return documents;
    }
}
