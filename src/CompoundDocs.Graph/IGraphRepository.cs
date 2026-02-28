using CompoundDocs.Common.Models;

namespace CompoundDocs.Graph;

public interface IGraphRepository
{
    Task UpsertDocumentAsync(DocumentNode document, CancellationToken ct = default);
    Task UpsertSectionAsync(SectionNode section, CancellationToken ct = default);
    Task UpsertChunkAsync(ChunkNode chunk, CancellationToken ct = default);
    Task UpsertConceptAsync(ConceptNode concept, CancellationToken ct = default);
    Task CreateRelationshipAsync(GraphRelationship relationship, CancellationToken ct = default);
    Task DeleteDocumentCascadeAsync(string documentId, CancellationToken ct = default);
    Task<List<ConceptNode>> FindConceptsByNameAsync(string name, CancellationToken ct = default);
    Task<List<ConceptNode>> GetRelatedConceptsAsync(string conceptId, int hops = 2, CancellationToken ct = default);
    Task<List<ChunkNode>> GetChunksByConceptAsync(string conceptId, CancellationToken ct = default);
    Task<List<DocumentNode>> GetLinkedDocumentsAsync(string documentId, CancellationToken ct = default);
    Task<List<ChunkNode>> GetChunksByIdsAsync(IReadOnlyList<string> chunkIds, CancellationToken ct = default);
    Task<List<ConceptNode>> GetConceptsByChunkIdsAsync(IReadOnlyList<string> chunkIds, CancellationToken ct = default);
    Task UpsertCodeExampleAsync(CodeExampleNode codeExample, string chunkId, CancellationToken ct = default);
    Task<string?> GetSyncStateAsync(string repoName, CancellationToken ct = default);
    Task SetSyncStateAsync(string repoName, string commitHash, CancellationToken ct = default);
}
