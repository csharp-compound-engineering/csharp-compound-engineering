using CompoundDocs.McpServer.Models;
using Microsoft.Extensions.VectorData;

namespace CompoundDocs.Tests.Storage;

/// <summary>
/// Unit tests for the ExternalDocument model.
/// Verifies property initialization, Semantic Kernel attributes, and helper methods.
/// </summary>
public sealed class ExternalDocumentTests
{
    [Fact]
    public void Constructor_GeneratesUniqueId()
    {
        // Arrange & Act
        var doc1 = new ExternalDocument();
        var doc2 = new ExternalDocument();

        // Assert
        doc1.Id.ShouldNotBe(doc2.Id);
        Guid.TryParse(doc1.Id, out _).ShouldBeTrue();
    }

    [Fact]
    public void Constructor_InitializesEmptyStrings()
    {
        // Arrange & Act
        var doc = new ExternalDocument();

        // Assert
        doc.TenantKey.ShouldBe(string.Empty);
        doc.Title.ShouldBe(string.Empty);
        doc.Content.ShouldBe(string.Empty);
        doc.RelativePath.ShouldBe(string.Empty);
        doc.ContentHash.ShouldBe(string.Empty);
    }

    [Fact]
    public void Constructor_InitializesDefaultNamespacePrefix()
    {
        // Arrange & Act
        var doc = new ExternalDocument();

        // Assert
        doc.NamespacePrefix.ShouldBe("external");
    }

    [Fact]
    public void Constructor_InitializesNullablePropertiesAsNull()
    {
        // Arrange & Act
        var doc = new ExternalDocument();

        // Assert
        doc.SourceUrl.ShouldBeNull();
        doc.LastSyncedAt.ShouldBeNull();
        doc.Vector.ShouldBeNull();
    }

    [Fact]
    public void Constructor_InitializesCharCountToZero()
    {
        // Arrange & Act
        var doc = new ExternalDocument();

        // Assert
        doc.CharCount.ShouldBe(0);
    }

    [Fact]
    public void Model_DoesNotHavePromotionLevel()
    {
        // Arrange
        var properties = typeof(ExternalDocument).GetProperties();

        // Assert - External documents should not have promotion level
        properties.ShouldNotContain(p => p.Name == "PromotionLevel");
    }

    [Fact]
    public void Model_DoesNotHaveDocType()
    {
        // Arrange
        var properties = typeof(ExternalDocument).GetProperties();

        // Assert - External documents are generic reference material
        properties.ShouldNotContain(p => p.Name == "DocType");
    }

    [Fact]
    public void Model_HasSourceUrlProperty()
    {
        // Arrange
        var property = typeof(ExternalDocument).GetProperty("SourceUrl");

        // Assert
        property.ShouldNotBeNull();
        property.PropertyType.ShouldBe(typeof(string));
    }

    [Fact]
    public void Model_HasLastSyncedAtProperty()
    {
        // Arrange
        var property = typeof(ExternalDocument).GetProperty("LastSyncedAt");

        // Assert
        property.ShouldNotBeNull();
        property.PropertyType.ShouldBe(typeof(DateTimeOffset?));
    }

    [Fact]
    public void Model_HasNamespacePrefixProperty()
    {
        // Arrange
        var property = typeof(ExternalDocument).GetProperty("NamespacePrefix");

        // Assert
        property.ShouldNotBeNull();
        property.PropertyType.ShouldBe(typeof(string));
    }

    [Fact]
    public void VectorStoreKeyAttribute_AppliedToId()
    {
        // Arrange
        var idProperty = typeof(ExternalDocument).GetProperty("Id");

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
        var vectorProperty = typeof(ExternalDocument).GetProperty("Vector");

        // Act
        var attribute = vectorProperty?.GetCustomAttributes(typeof(VectorStoreVectorAttribute), false)
            .Cast<VectorStoreVectorAttribute>()
            .FirstOrDefault();

        // Assert
        attribute.ShouldNotBeNull();
        attribute.Dimensions.ShouldBe(1024);
    }

    [Fact]
    public void TenantKey_IsFilterable()
    {
        // Arrange
        var property = typeof(ExternalDocument).GetProperty("TenantKey");

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
        var property = typeof(ExternalDocument).GetProperty("NamespacePrefix");

        // Act
        var attribute = property?.GetCustomAttributes(typeof(VectorStoreDataAttribute), false)
            .Cast<VectorStoreDataAttribute>()
            .FirstOrDefault();

        // Assert
        attribute.ShouldNotBeNull();
        attribute.IsIndexed.ShouldBeTrue();
    }

    [Fact]
    public void CreateTenantKey_FormatsCorrectly()
    {
        // Arrange
        var projectName = "my-project";
        var branchName = "feature-branch";
        var pathHash = "abc123";

        // Act
        var tenantKey = ExternalDocument.CreateTenantKey(projectName, branchName, pathHash);

        // Assert
        tenantKey.ShouldBe("my-project:feature-branch:abc123");
    }

    [Fact]
    public void ParseTenantKey_ParsesCorrectly()
    {
        // Arrange
        var tenantKey = "my-project:feature-branch:abc123";

        // Act
        var (projectName, branchName, pathHash) = ExternalDocument.ParseTenantKey(tenantKey);

        // Assert
        projectName.ShouldBe("my-project");
        branchName.ShouldBe("feature-branch");
        pathHash.ShouldBe("abc123");
    }

    [Fact]
    public void ParseTenantKey_HandlesExtraColons()
    {
        // Arrange - Extra colons after the second one are preserved in pathHash
        // This is consistent with Split(':', 3) behavior
        var tenantKey = "my-project:feature:branch:abc123";

        // Act
        var (projectName, branchName, pathHash) = ExternalDocument.ParseTenantKey(tenantKey);

        // Assert - Split limit of 3 puts everything after second colon in pathHash
        projectName.ShouldBe("my-project");
        branchName.ShouldBe("feature");
        pathHash.ShouldBe("branch:abc123");
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("only:two")]
    [InlineData("")]
    public void ParseTenantKey_ThrowsForInvalidFormat(string invalidTenantKey)
    {
        // Act & Assert
        Should.Throw<ArgumentException>(() => ExternalDocument.ParseTenantKey(invalidTenantKey));
    }

    [Fact]
    public void AllProperties_HaveVectorStoreAttributes()
    {
        // Arrange
        var properties = typeof(ExternalDocument).GetProperties();

        // Assert - All properties should have either VectorStoreKey, VectorStoreData, or VectorStoreVector
        foreach (var property in properties)
        {
            var hasKey = property.GetCustomAttributes(typeof(VectorStoreKeyAttribute), false).Any();
            var hasData = property.GetCustomAttributes(typeof(VectorStoreDataAttribute), false).Any();
            var hasVector = property.GetCustomAttributes(typeof(VectorStoreVectorAttribute), false).Any();

            (hasKey || hasData || hasVector).ShouldBeTrue(
                $"Property '{property.Name}' should have a VectorStore attribute");
        }
    }
}
