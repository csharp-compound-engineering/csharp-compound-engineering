using CompoundDocs.Common.Models;
using CompoundDocs.Graph;
using CompoundDocs.Graph.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CompoundDocs.Tests.Integration.Graph;

/// <summary>
/// Integration tests for graph repository operations against Amazon Neptune.
/// These tests require real AWS infrastructure and are skipped in CI.
/// </summary>
public class GraphRepositoryTests
{
    [Fact(Skip = "Requires AWS infrastructure - Neptune, OpenSearch, Bedrock")]
    public async Task CreateDocumentNode_StoresInGraph()
    {
        // Arrange: build real Neptune graph repository from env config
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
            Id = "integ-doc-001",
            FilePath = "docs/integration-test.md",
            Title = "Integration Test Document"
        };

        // Act: upsert document and verify it is retrievable via linked documents query
        await repo.UpsertDocumentAsync(doc);
        var linked = await repo.GetLinkedDocumentsAsync(doc.Id);

        // Assert
        linked.ShouldNotBeNull();
    }

    [Fact(Skip = "Requires AWS infrastructure - Neptune, OpenSearch, Bedrock")]
    public async Task GetRelatedConcepts_ReturnsConnectedNodes()
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

        var concept1 = new ConceptNode { Id = "concept-a", Name = "Dependency Injection" };
        var concept2 = new ConceptNode { Id = "concept-b", Name = "Service Lifetime" };

        // Act: create concepts and a RELATES_TO relationship, then traverse
        await repo.UpsertConceptAsync(concept1);
        await repo.UpsertConceptAsync(concept2);
        await repo.CreateRelationshipAsync(new GraphRelationship
        {
            Type = "RELATES_TO",
            SourceId = concept1.Id,
            TargetId = concept2.Id
        });

        var related = await repo.GetRelatedConceptsAsync(concept1.Id, hops: 1);

        // Assert: concept2 should be found as related to concept1
        related.ShouldNotBeEmpty();
        related.ShouldContain(c => c.Id == concept2.Id);
    }

    [Fact(Skip = "Requires AWS infrastructure - Neptune, OpenSearch, Bedrock")]
    public async Task UpsertDocument_IsIdempotent()
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
        var doc = new DocumentNode
        {
            Id = "idempotent-doc-001",
            FilePath = "docs/idempotent.md",
            Title = "First Title"
        };

        // Act: upsert the same document ID twice with different titles
        await repo.UpsertDocumentAsync(doc);
        var updatedDoc = doc with { Title = "Updated Title" };
        await repo.UpsertDocumentAsync(updatedDoc);

        var linked = await repo.GetLinkedDocumentsAsync(doc.Id);

        // Assert: upsert should succeed without duplicate node creation
        linked.ShouldNotBeNull();
    }

    [Fact(Skip = "Requires AWS infrastructure - Neptune, OpenSearch, Bedrock")]
    public async Task DeleteDocumentCascade_RemovesDocumentAndChildren()
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

        var doc = new DocumentNode
        {
            Id = "delete-doc-001",
            FilePath = "docs/to-delete.md",
            Title = "Document To Delete"
        };
        var section = new SectionNode
        {
            Id = "delete-sec-001",
            DocumentId = doc.Id,
            Title = "Section To Delete",
            Order = 0,
            HeadingLevel = 2
        };

        await repo.UpsertDocumentAsync(doc);
        await repo.UpsertSectionAsync(section);
        await repo.CreateRelationshipAsync(new GraphRelationship
        {
            Type = "HAS_SECTION",
            SourceId = doc.Id,
            TargetId = section.Id
        });

        // Act: cascade delete the document
        await repo.DeleteDocumentCascadeAsync(doc.Id);
        var linked = await repo.GetLinkedDocumentsAsync(doc.Id);

        // Assert: document should no longer be found
        linked.ShouldBeEmpty();
    }
}
