using CompoundDocs.Common.Models;
using CompoundDocs.Graph;
using CompoundDocs.Graph.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CompoundDocs.Tests.Integration.Graph;

/// <summary>
/// Integration tests for multi-hop graph traversals against Amazon Neptune.
/// These tests require real AWS infrastructure and are skipped in CI.
/// </summary>
public class GraphTraversalTests
{
    [Fact(Skip = "Requires AWS infrastructure - Neptune, OpenSearch, Bedrock")]
    public async Task MultiHopTraversal_ReturnsRelatedDocuments()
    {
        // Arrange: build the full document -> section -> chunk -> concept chain
        var config = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddLogging();
        services.AddNeptuneGraph(config);
        await using var provider = services.BuildServiceProvider();

        var repo = provider.GetRequiredService<IGraphRepository>();

        var doc = new DocumentNode
        {
            Id = "trav-doc",
            FilePath = "docs/traversal.md",
            Title = "Traversal Test"
        };
        var section = new SectionNode
        {
            Id = "trav-sec",
            DocumentId = doc.Id,
            Title = "Section 1",
            Order = 0,
            HeadingLevel = 2
        };
        var chunk = new ChunkNode
        {
            Id = "trav-chunk",
            SectionId = section.Id,
            DocumentId = doc.Id,
            Content = "Test content about graph traversal patterns",
            Order = 0,
            TokenCount = 10
        };
        var concept = new ConceptNode
        {
            Id = "trav-concept",
            Name = "Graph Traversal"
        };

        // Act: create the full graph and establish relationships
        await repo.UpsertDocumentAsync(doc);
        await repo.UpsertSectionAsync(section);
        await repo.UpsertChunkAsync(chunk);
        await repo.UpsertConceptAsync(concept);
        await repo.CreateRelationshipAsync(new GraphRelationship
        {
            Type = "HAS_SECTION",
            SourceId = doc.Id,
            TargetId = section.Id
        });
        await repo.CreateRelationshipAsync(new GraphRelationship
        {
            Type = "HAS_CHUNK",
            SourceId = section.Id,
            TargetId = chunk.Id
        });
        await repo.CreateRelationshipAsync(new GraphRelationship
        {
            Type = "MENTIONS",
            SourceId = chunk.Id,
            TargetId = concept.Id
        });

        var chunks = await repo.GetChunksByConceptAsync(concept.Id);

        // Assert: traversing from concept back to chunks should find our chunk
        chunks.ShouldNotBeEmpty();
        chunks.ShouldContain(c => c.Id == chunk.Id);
    }

    [Fact(Skip = "Requires AWS infrastructure - Neptune, OpenSearch, Bedrock")]
    public async Task TwoHopConceptTraversal_FindsTransitiveRelations()
    {
        // Arrange: create a chain of concepts: A -> B -> C
        var config = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddLogging();
        services.AddNeptuneGraph(config);
        await using var provider = services.BuildServiceProvider();

        var repo = provider.GetRequiredService<IGraphRepository>();

        var conceptA = new ConceptNode { Id = "hop-a", Name = "SOLID Principles" };
        var conceptB = new ConceptNode { Id = "hop-b", Name = "Dependency Inversion" };
        var conceptC = new ConceptNode { Id = "hop-c", Name = "Inversion of Control" };

        await repo.UpsertConceptAsync(conceptA);
        await repo.UpsertConceptAsync(conceptB);
        await repo.UpsertConceptAsync(conceptC);
        await repo.CreateRelationshipAsync(new GraphRelationship
        {
            Type = "RELATES_TO",
            SourceId = conceptA.Id,
            TargetId = conceptB.Id
        });
        await repo.CreateRelationshipAsync(new GraphRelationship
        {
            Type = "RELATES_TO",
            SourceId = conceptB.Id,
            TargetId = conceptC.Id
        });

        // Act: traverse 2 hops from concept A
        var related = await repo.GetRelatedConceptsAsync(conceptA.Id, hops: 2);

        // Assert: both concept B (1 hop) and concept C (2 hops) should be reachable
        related.ShouldNotBeEmpty();
        related.ShouldContain(c => c.Id == conceptB.Id);
        related.ShouldContain(c => c.Id == conceptC.Id);
    }

    [Fact(Skip = "Requires AWS infrastructure - Neptune, OpenSearch, Bedrock")]
    public async Task RelationshipWithProperties_StoresCustomMetadata()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddLogging();
        services.AddNeptuneGraph(config);
        await using var provider = services.BuildServiceProvider();

        var repo = provider.GetRequiredService<IGraphRepository>();

        var concept1 = new ConceptNode { Id = "prop-concept-a", Name = "ASP.NET Core" };
        var concept2 = new ConceptNode { Id = "prop-concept-b", Name = "Middleware" };

        await repo.UpsertConceptAsync(concept1);
        await repo.UpsertConceptAsync(concept2);

        // Act: create relationship with custom properties
        await repo.CreateRelationshipAsync(new GraphRelationship
        {
            Type = "RELATES_TO",
            SourceId = concept1.Id,
            TargetId = concept2.Id,
            Properties = new Dictionary<string, object>
            {
                ["weight"] = 0.95,
                ["source"] = "entity-extraction"
            }
        });

        var related = await repo.GetRelatedConceptsAsync(concept1.Id, hops: 1);

        // Assert: relationship should be traversable
        related.ShouldNotBeEmpty();
        related.ShouldContain(c => c.Id == concept2.Id);
    }
}
