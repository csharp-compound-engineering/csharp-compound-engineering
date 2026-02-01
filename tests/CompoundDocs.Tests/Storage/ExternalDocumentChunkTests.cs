using CompoundDocs.McpServer.Models;
using CompoundDocs.Tests.Utilities;
using Microsoft.Extensions.VectorData;

namespace CompoundDocs.Tests.Storage;

/// <summary>
/// Unit tests for the ExternalDocumentChunk model.
/// Verifies property initialization, Semantic Kernel attributes, and factory methods.
/// </summary>
public sealed class ExternalDocumentChunkTests
{
    [Fact]
    public void Constructor_GeneratesUniqueId()
    {
        // Arrange & Act
        var chunk1 = new ExternalDocumentChunk();
        var chunk2 = new ExternalDocumentChunk();

        // Assert
        chunk1.Id.ShouldNotBe(chunk2.Id);
        Guid.TryParse(chunk1.Id, out _).ShouldBeTrue();
    }

    [Fact]
    public void Constructor_InitializesEmptyStrings()
    {
        // Arrange & Act
        var chunk = new ExternalDocumentChunk();

        // Assert
        chunk.ExternalDocumentId.ShouldBe(string.Empty);
        chunk.TenantKey.ShouldBe(string.Empty);
        chunk.HeaderPath.ShouldBe(string.Empty);
        chunk.Content.ShouldBe(string.Empty);
    }

    [Fact]
    public void Constructor_InitializesDefaultNamespacePrefix()
    {
        // Arrange & Act
        var chunk = new ExternalDocumentChunk();

        // Assert
        chunk.NamespacePrefix.ShouldBe("external");
    }

    [Fact]
    public void Constructor_InitializesLineNumbersToZero()
    {
        // Arrange & Act
        var chunk = new ExternalDocumentChunk();

        // Assert
        chunk.StartLine.ShouldBe(0);
        chunk.EndLine.ShouldBe(0);
    }

    [Fact]
    public void Constructor_InitializesVectorAsNull()
    {
        // Arrange & Act
        var chunk = new ExternalDocumentChunk();

        // Assert
        chunk.Vector.ShouldBeNull();
    }

    [Fact]
    public void Model_DoesNotHavePromotionLevel()
    {
        // Arrange
        var properties = typeof(ExternalDocumentChunk).GetProperties();

        // Assert - External document chunks should not have promotion level
        properties.ShouldNotContain(p => p.Name == "PromotionLevel");
    }

    [Fact]
    public void Model_HasExternalDocumentIdNotDocumentId()
    {
        // Arrange
        var properties = typeof(ExternalDocumentChunk).GetProperties();

        // Assert
        properties.ShouldContain(p => p.Name == "ExternalDocumentId");
        properties.ShouldNotContain(p => p.Name == "DocumentId");
    }

    [Fact]
    public void VectorStoreKeyAttribute_AppliedToId()
    {
        // Arrange
        var idProperty = typeof(ExternalDocumentChunk).GetProperty("Id");

        // Act
        var attribute = idProperty?.GetCustomAttributes(typeof(VectorStoreKeyAttribute), false)
            .Cast<VectorStoreKeyAttribute>()
            .FirstOrDefault();

        // Assert
        attribute.ShouldNotBeNull();
    }

    [Fact]
    public void VectorStoreVectorAttribute_ConfiguredCorrectly()
    {
        // Arrange
        var vectorProperty = typeof(ExternalDocumentChunk).GetProperty("Vector");

        // Act
        var attribute = vectorProperty?.GetCustomAttributes(typeof(VectorStoreVectorAttribute), false)
            .Cast<VectorStoreVectorAttribute>()
            .FirstOrDefault();

        // Assert
        attribute.ShouldNotBeNull();
        attribute.Dimensions.ShouldBe(1024);
    }

    [Fact]
    public void ExternalDocumentId_IsFilterable()
    {
        // Arrange
        var property = typeof(ExternalDocumentChunk).GetProperty("ExternalDocumentId");

        // Act
        var attribute = property?.GetCustomAttributes(typeof(VectorStoreDataAttribute), false)
            .Cast<VectorStoreDataAttribute>()
            .FirstOrDefault();

        // Assert
        attribute.ShouldNotBeNull();
        attribute.IsIndexed.ShouldBeTrue();
    }

    [Fact]
    public void TenantKey_IsFilterable()
    {
        // Arrange
        var property = typeof(ExternalDocumentChunk).GetProperty("TenantKey");

        // Act
        var attribute = property?.GetCustomAttributes(typeof(VectorStoreDataAttribute), false)
            .Cast<VectorStoreDataAttribute>()
            .FirstOrDefault();

        // Assert
        attribute.ShouldNotBeNull();
        attribute.IsIndexed.ShouldBeTrue();
    }

    [Fact]
    public void NamespacePrefix_IsFilterable()
    {
        // Arrange
        var property = typeof(ExternalDocumentChunk).GetProperty("NamespacePrefix");

        // Act
        var attribute = property?.GetCustomAttributes(typeof(VectorStoreDataAttribute), false)
            .Cast<VectorStoreDataAttribute>()
            .FirstOrDefault();

        // Assert
        attribute.ShouldNotBeNull();
        attribute.IsIndexed.ShouldBeTrue();
    }

    [Fact]
    public void LineCount_CalculatesCorrectly()
    {
        // Arrange
        var chunk = new ExternalDocumentChunk
        {
            StartLine = 10,
            EndLine = 25
        };

        // Act & Assert
        chunk.LineCount.ShouldBe(16); // 25 - 10 + 1 = 16 lines
    }

    [Fact]
    public void CreateFromParent_InheritsParentProperties()
    {
        // Arrange
        var parent = TestExternalDocumentBuilder.Create()
            .WithTenantKey("test-project", "main", "hash123")
            .WithNamespacePrefix("api-docs")
            .Build();

        // Act
        var chunk = ExternalDocumentChunk.CreateFromParent(
            parent,
            headerPath: "## API Reference > ### Authentication",
            content: "Use Bearer tokens for authentication.",
            startLine: 10,
            endLine: 25);

        // Assert
        chunk.ExternalDocumentId.ShouldBe(parent.Id);
        chunk.TenantKey.ShouldBe(parent.TenantKey);
        chunk.NamespacePrefix.ShouldBe(parent.NamespacePrefix);
        chunk.HeaderPath.ShouldBe("## API Reference > ### Authentication");
        chunk.Content.ShouldBe("Use Bearer tokens for authentication.");
        chunk.StartLine.ShouldBe(10);
        chunk.EndLine.ShouldBe(25);
    }

    [Fact]
    public void CreateFromParent_GeneratesNewId()
    {
        // Arrange
        var parent = TestExternalDocumentBuilder.Create().Build();

        // Act
        var chunk1 = ExternalDocumentChunk.CreateFromParent(parent, "", "", 1, 10);
        var chunk2 = ExternalDocumentChunk.CreateFromParent(parent, "", "", 11, 20);

        // Assert
        chunk1.Id.ShouldNotBe(chunk2.Id);
        chunk1.Id.ShouldNotBe(parent.Id);
    }

    [Fact]
    public void AllProperties_HaveVectorStoreAttributes()
    {
        // Arrange
        var properties = typeof(ExternalDocumentChunk).GetProperties();

        // Assert - All properties except LineCount should have VectorStore attributes
        foreach (var property in properties)
        {
            // LineCount is a calculated property, not stored
            if (property.Name == "LineCount") continue;

            var hasKey = property.GetCustomAttributes(typeof(VectorStoreKeyAttribute), false).Any();
            var hasData = property.GetCustomAttributes(typeof(VectorStoreDataAttribute), false).Any();
            var hasVector = property.GetCustomAttributes(typeof(VectorStoreVectorAttribute), false).Any();

            (hasKey || hasData || hasVector).ShouldBeTrue(
                $"Property '{property.Name}' should have a VectorStore attribute");
        }
    }
}
