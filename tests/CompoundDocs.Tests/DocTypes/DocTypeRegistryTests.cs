using CompoundDocs.McpServer.DocTypes;
using CompoundDocs.McpServer.Models;
using Microsoft.Extensions.Logging;

namespace CompoundDocs.Tests.DocTypes;

/// <summary>
/// Unit tests for DocTypeRegistry.
/// </summary>
public sealed class DocTypeRegistryTests : TestBase
{
    private readonly Mock<ILogger<DocTypeRegistry>> _mockRegistryLogger;
    private readonly Mock<ILogger<DocTypeValidator>> _mockValidatorLogger;

    public DocTypeRegistryTests()
    {
        _mockRegistryLogger = CreateLooseMock<ILogger<DocTypeRegistry>>();
        _mockValidatorLogger = CreateLooseMock<ILogger<DocTypeValidator>>();
    }

    private DocTypeRegistry CreateRegistry()
    {
        var validator = new DocTypeValidator(_mockValidatorLogger.Object);
        return new DocTypeRegistry(_mockRegistryLogger.Object, validator);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange
        var validator = new DocTypeValidator(_mockValidatorLogger.Object);

        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            new DocTypeRegistry(null!, validator));
    }

    [Fact]
    public void Constructor_WithNullValidator_ThrowsArgumentNullException()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            new DocTypeRegistry(_mockRegistryLogger.Object, null!));
    }

    [Fact]
    public void Constructor_RegistersBuiltInDocTypes()
    {
        // Act
        var registry = CreateRegistry();

        // Assert - Should have all built-in types
        registry.IsRegistered(DocumentTypes.Problem).ShouldBeTrue();
        registry.IsRegistered(DocumentTypes.Insight).ShouldBeTrue();
        registry.IsRegistered(DocumentTypes.Codebase).ShouldBeTrue();
        registry.IsRegistered(DocumentTypes.Tool).ShouldBeTrue();
        registry.IsRegistered(DocumentTypes.Style).ShouldBeTrue();
        registry.IsRegistered(DocumentTypes.Spec).ShouldBeTrue();
        registry.IsRegistered(DocumentTypes.Adr).ShouldBeTrue();
        registry.IsRegistered(DocumentTypes.Research).ShouldBeTrue();
        registry.IsRegistered(DocumentTypes.Doc).ShouldBeTrue();
    }

    #endregion

    #region GetDocType Tests

    [Fact]
    public void GetDocType_WithRegisteredType_ReturnsDefinition()
    {
        // Arrange
        var registry = CreateRegistry();

        // Act
        var docType = registry.GetDocType(DocumentTypes.Problem);

        // Assert
        docType.ShouldNotBeNull();
        docType.Id.ShouldBe(DocumentTypes.Problem);
        docType.Name.ShouldBe("Problem Statement");
    }

    [Fact]
    public void GetDocType_WithUnregisteredType_ReturnsNull()
    {
        // Arrange
        var registry = CreateRegistry();

        // Act
        var docType = registry.GetDocType("unknown-type");

        // Assert
        docType.ShouldBeNull();
    }

    [Fact]
    public void GetDocType_IsCaseInsensitive()
    {
        // Arrange
        var registry = CreateRegistry();

        // Act
        var docType1 = registry.GetDocType("problem");
        var docType2 = registry.GetDocType("PROBLEM");
        var docType3 = registry.GetDocType("Problem");

        // Assert
        docType1.ShouldNotBeNull();
        docType2.ShouldNotBeNull();
        docType3.ShouldNotBeNull();
        docType1.Id.ShouldBe(docType2.Id);
        docType2.Id.ShouldBe(docType3.Id);
    }

    [Fact]
    public void GetDocType_WithNullId_ThrowsArgumentException()
    {
        // Arrange
        var registry = CreateRegistry();

        // Act & Assert
        Should.Throw<ArgumentException>(() =>
            registry.GetDocType(null!));
    }

    [Fact]
    public void GetDocType_WithEmptyId_ThrowsArgumentException()
    {
        // Arrange
        var registry = CreateRegistry();

        // Act & Assert
        Should.Throw<ArgumentException>(() =>
            registry.GetDocType(""));
    }

    [Fact]
    public void GetDocType_WithWhitespaceId_ThrowsArgumentException()
    {
        // Arrange
        var registry = CreateRegistry();

        // Act & Assert
        Should.Throw<ArgumentException>(() =>
            registry.GetDocType("   "));
    }

    #endregion

    #region GetSchema Tests

    [Fact]
    public void GetSchema_WithRegisteredTypeAndSchema_ReturnsSchema()
    {
        // Arrange
        var registry = CreateRegistry();
        var schema = """{"type": "object"}""";
        var definition = DocTypeDefinition.CreateBuiltIn(
            id: "test-type",
            name: "Test Type",
            description: "Test description",
            schema: schema,
            triggerPhrases: ["test"],
            requiredFields: ["title"]
        );
        registry.RegisterDocType(definition, schema);

        // Act
        var result = registry.GetSchema("test-type");

        // Assert
        result.ShouldBe(schema);
    }

    [Fact]
    public void GetSchema_WithRegisteredTypeNoSchema_ReturnsNull()
    {
        // Arrange
        var registry = CreateRegistry();

        // Act - Spec type doesn't have a specific schema
        var result = registry.GetSchema(DocumentTypes.Spec);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void GetSchema_WithUnregisteredType_ReturnsNull()
    {
        // Arrange
        var registry = CreateRegistry();

        // Act
        var result = registry.GetSchema("unknown-type");

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void GetSchema_WithNullId_ThrowsArgumentException()
    {
        // Arrange
        var registry = CreateRegistry();

        // Act & Assert
        Should.Throw<ArgumentException>(() =>
            registry.GetSchema(null!));
    }

    #endregion

    #region GetAllDocTypes Tests

    [Fact]
    public void GetAllDocTypes_ReturnsAllRegisteredTypes()
    {
        // Arrange
        var registry = CreateRegistry();

        // Act
        var allTypes = registry.GetAllDocTypes();

        // Assert
        allTypes.ShouldNotBeEmpty();
        allTypes.Count.ShouldBeGreaterThanOrEqualTo(9); // At least 9 built-in types
    }

    [Fact]
    public void GetAllDocTypes_IncludesBuiltInTypes()
    {
        // Arrange
        var registry = CreateRegistry();

        // Act
        var allTypes = registry.GetAllDocTypes();

        // Assert
        allTypes.ShouldContain(t => t.Id == DocumentTypes.Problem);
        allTypes.ShouldContain(t => t.Id == DocumentTypes.Insight);
        allTypes.ShouldContain(t => t.Id == DocumentTypes.Spec);
    }

    [Fact]
    public void GetAllDocTypes_IncludesCustomRegisteredTypes()
    {
        // Arrange
        var registry = CreateRegistry();
        var customType = DocTypeDefinition.CreateBuiltIn(
            id: "custom-type",
            name: "Custom Type",
            description: "A custom type",
            triggerPhrases: ["custom"],
            requiredFields: ["title"]
        );
        registry.RegisterDocType(customType);

        // Act
        var allTypes = registry.GetAllDocTypes();

        // Assert
        allTypes.ShouldContain(t => t.Id == "custom-type");
    }

    #endregion

    #region RegisterDocType Tests

    [Fact]
    public void RegisterDocType_WithValidDefinition_Succeeds()
    {
        // Arrange
        var registry = CreateRegistry();
        var definition = DocTypeDefinition.CreateBuiltIn(
            id: "new-type",
            name: "New Type",
            description: "A new type",
            triggerPhrases: ["new"],
            requiredFields: ["title"]
        );

        // Act
        registry.RegisterDocType(definition);

        // Assert
        registry.IsRegistered("new-type").ShouldBeTrue();
    }

    [Fact]
    public void RegisterDocType_WithSchema_StoresSchema()
    {
        // Arrange
        var registry = CreateRegistry();
        var schema = """{"type": "object"}""";
        var definition = DocTypeDefinition.CreateBuiltIn(
            id: "new-type",
            name: "New Type",
            description: "A new type",
            schema: schema,
            triggerPhrases: ["new"],
            requiredFields: ["title"]
        );

        // Act
        registry.RegisterDocType(definition, schema);

        // Assert
        registry.GetSchema("new-type").ShouldBe(schema);
    }

    [Fact]
    public void RegisterDocType_WithDuplicateId_ThrowsArgumentException()
    {
        // Arrange
        var registry = CreateRegistry();
        var definition1 = DocTypeDefinition.CreateBuiltIn(
            id: "unique-type",
            name: "Type 1",
            description: "First type",
            triggerPhrases: ["type1"],
            requiredFields: ["title"]
        );
        var definition2 = DocTypeDefinition.CreateBuiltIn(
            id: "unique-type",
            name: "Type 2",
            description: "Second type",
            triggerPhrases: ["type2"],
            requiredFields: ["title"]
        );

        registry.RegisterDocType(definition1);

        // Act & Assert
        var exception = Should.Throw<ArgumentException>(() =>
            registry.RegisterDocType(definition2));
        exception.Message.ShouldContain("already registered");
    }

    [Fact]
    public void RegisterDocType_WithNullDefinition_ThrowsArgumentNullException()
    {
        // Arrange
        var registry = CreateRegistry();

        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            registry.RegisterDocType(null!));
    }

    [Fact]
    public void RegisterDocType_WithNullSchema_DoesNotStoreSchema()
    {
        // Arrange
        var registry = CreateRegistry();
        var definition = DocTypeDefinition.CreateBuiltIn(
            id: "no-schema-type",
            name: "No Schema Type",
            description: "A type without schema",
            triggerPhrases: ["none"],
            requiredFields: ["title"]
        );

        // Act
        registry.RegisterDocType(definition, null);

        // Assert
        registry.GetSchema("no-schema-type").ShouldBeNull();
    }

    [Fact]
    public void RegisterDocType_WithEmptySchema_DoesNotStoreSchema()
    {
        // Arrange
        var registry = CreateRegistry();
        var definition = DocTypeDefinition.CreateBuiltIn(
            id: "empty-schema-type",
            name: "Empty Schema Type",
            description: "A type with empty schema",
            triggerPhrases: ["empty"],
            requiredFields: ["title"]
        );

        // Act
        registry.RegisterDocType(definition, "");

        // Assert
        registry.GetSchema("empty-schema-type").ShouldBeNull();
    }

    #endregion

    #region IsRegistered Tests

    [Fact]
    public void IsRegistered_WithRegisteredType_ReturnsTrue()
    {
        // Arrange
        var registry = CreateRegistry();

        // Act & Assert
        registry.IsRegistered(DocumentTypes.Problem).ShouldBeTrue();
    }

    [Fact]
    public void IsRegistered_WithUnregisteredType_ReturnsFalse()
    {
        // Arrange
        var registry = CreateRegistry();

        // Act & Assert
        registry.IsRegistered("nonexistent-type").ShouldBeFalse();
    }

    [Fact]
    public void IsRegistered_IsCaseInsensitive()
    {
        // Arrange
        var registry = CreateRegistry();

        // Act & Assert
        registry.IsRegistered("PROBLEM").ShouldBeTrue();
        registry.IsRegistered("problem").ShouldBeTrue();
        registry.IsRegistered("Problem").ShouldBeTrue();
    }

    [Fact]
    public void IsRegistered_WithNullId_ThrowsArgumentException()
    {
        // Arrange
        var registry = CreateRegistry();

        // Act & Assert
        Should.Throw<ArgumentException>(() =>
            registry.IsRegistered(null!));
    }

    [Fact]
    public void IsRegistered_WithEmptyId_ThrowsArgumentException()
    {
        // Arrange
        var registry = CreateRegistry();

        // Act & Assert
        Should.Throw<ArgumentException>(() =>
            registry.IsRegistered(""));
    }

    #endregion

    #region ValidateAsync Tests

    [Fact]
    public async Task ValidateAsync_WithUnknownDocType_ReturnsDocTypeNotFound()
    {
        // Arrange
        var registry = CreateRegistry();
        var frontmatter = new Dictionary<string, object?>
        {
            ["title"] = "Test"
        };

        // Act
        var result = await registry.ValidateAsync("unknown-type", frontmatter);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorType == ValidationErrorType.InvalidDocType);
    }

    [Fact]
    public async Task ValidateAsync_WithMissingRequiredField_ReturnsFailure()
    {
        // Arrange
        var registry = CreateRegistry();
        var frontmatter = new Dictionary<string, object?>
        {
            ["doc_type"] = "spec"
            // Missing "title" which is required for spec
        };

        // Act
        var result = await registry.ValidateAsync(DocumentTypes.Spec, frontmatter);

        // Assert
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyPath == "title");
    }

    [Fact]
    public async Task ValidateAsync_WithAllRequiredFields_ReturnsSuccess()
    {
        // Arrange
        var registry = CreateRegistry();
        var frontmatter = new Dictionary<string, object?>
        {
            ["doc_type"] = "spec",
            ["title"] = "Test Specification"
        };

        // Act
        var result = await registry.ValidateAsync(DocumentTypes.Spec, frontmatter);

        // Assert
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task ValidateAsync_WithNullDocTypeId_ThrowsArgumentException()
    {
        // Arrange
        var registry = CreateRegistry();
        var frontmatter = new Dictionary<string, object?>();

        // Act & Assert
        await Should.ThrowAsync<ArgumentException>(async () =>
            await registry.ValidateAsync(null!, frontmatter));
    }

    [Fact]
    public async Task ValidateAsync_WithNullFrontmatter_ThrowsArgumentNullException()
    {
        // Arrange
        var registry = CreateRegistry();

        // Act & Assert
        await Should.ThrowAsync<ArgumentNullException>(async () =>
            await registry.ValidateAsync(DocumentTypes.Spec, null!));
    }

    #endregion

    #region Built-In DocTypes Tests

    [Theory]
    [InlineData(DocumentTypes.Problem, "Problem Statement")]
    [InlineData(DocumentTypes.Insight, "Insight")]
    [InlineData(DocumentTypes.Codebase, "Codebase Documentation")]
    [InlineData(DocumentTypes.Tool, "Tool Documentation")]
    [InlineData(DocumentTypes.Style, "Style Guide")]
    [InlineData(DocumentTypes.Spec, "Specification")]
    [InlineData(DocumentTypes.Adr, "Architecture Decision Record")]
    [InlineData(DocumentTypes.Research, "Research")]
    [InlineData(DocumentTypes.Doc, "Documentation")]
    public void BuiltInDocType_HasCorrectName(string docTypeId, string expectedName)
    {
        // Arrange
        var registry = CreateRegistry();

        // Act
        var docType = registry.GetDocType(docTypeId);

        // Assert
        docType.ShouldNotBeNull();
        docType.Name.ShouldBe(expectedName);
    }

    [Theory]
    [InlineData(DocumentTypes.Problem)]
    [InlineData(DocumentTypes.Insight)]
    [InlineData(DocumentTypes.Codebase)]
    [InlineData(DocumentTypes.Tool)]
    [InlineData(DocumentTypes.Style)]
    [InlineData(DocumentTypes.Spec)]
    [InlineData(DocumentTypes.Adr)]
    [InlineData(DocumentTypes.Research)]
    [InlineData(DocumentTypes.Doc)]
    public void BuiltInDocType_IsMarkedAsBuiltIn(string docTypeId)
    {
        // Arrange
        var registry = CreateRegistry();

        // Act
        var docType = registry.GetDocType(docTypeId);

        // Assert
        docType.ShouldNotBeNull();
        docType.IsBuiltIn.ShouldBeTrue();
    }

    [Theory]
    [InlineData(DocumentTypes.Problem)]
    [InlineData(DocumentTypes.Insight)]
    [InlineData(DocumentTypes.Codebase)]
    [InlineData(DocumentTypes.Tool)]
    [InlineData(DocumentTypes.Style)]
    [InlineData(DocumentTypes.Spec)]
    [InlineData(DocumentTypes.Adr)]
    [InlineData(DocumentTypes.Research)]
    [InlineData(DocumentTypes.Doc)]
    public void BuiltInDocType_HasRequiredFields(string docTypeId)
    {
        // Arrange
        var registry = CreateRegistry();

        // Act
        var docType = registry.GetDocType(docTypeId);

        // Assert
        docType.ShouldNotBeNull();
        docType.RequiredFields.ShouldNotBeEmpty();
        docType.RequiredFields.ShouldContain("doc_type");
        docType.RequiredFields.ShouldContain("title");
    }

    [Theory]
    [InlineData(DocumentTypes.Problem)]
    [InlineData(DocumentTypes.Insight)]
    [InlineData(DocumentTypes.Codebase)]
    [InlineData(DocumentTypes.Tool)]
    [InlineData(DocumentTypes.Style)]
    [InlineData(DocumentTypes.Spec)]
    [InlineData(DocumentTypes.Adr)]
    [InlineData(DocumentTypes.Research)]
    [InlineData(DocumentTypes.Doc)]
    public void BuiltInDocType_HasTriggerPhrases(string docTypeId)
    {
        // Arrange
        var registry = CreateRegistry();

        // Act
        var docType = registry.GetDocType(docTypeId);

        // Assert
        docType.ShouldNotBeNull();
        docType.TriggerPhrases.ShouldNotBeEmpty();
    }

    #endregion
}
