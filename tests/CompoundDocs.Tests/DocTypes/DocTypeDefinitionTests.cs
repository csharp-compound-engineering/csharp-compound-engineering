using CompoundDocs.McpServer.DocTypes;
using CompoundDocs.McpServer.Models;

namespace CompoundDocs.Tests.DocTypes;

/// <summary>
/// Unit tests for DocTypeDefinition.
/// </summary>
public sealed class DocTypeDefinitionTests
{
    #region CreateBuiltIn Tests

    [Fact]
    public void CreateBuiltIn_WithRequiredParameters_CreatesValidDefinition()
    {
        // Act
        var definition = DocTypeDefinition.CreateBuiltIn(
            id: "test-type",
            name: "Test Type",
            description: "A test document type");

        // Assert
        definition.ShouldNotBeNull();
        definition.Id.ShouldBe("test-type");
        definition.Name.ShouldBe("Test Type");
        definition.Description.ShouldBe("A test document type");
    }

    [Fact]
    public void CreateBuiltIn_SetsIsBuiltInToTrue()
    {
        // Act
        var definition = DocTypeDefinition.CreateBuiltIn(
            id: "test-type",
            name: "Test Type",
            description: "A test document type");

        // Assert
        definition.IsBuiltIn.ShouldBeTrue();
    }

    [Fact]
    public void CreateBuiltIn_WithSchema_IncludesSchema()
    {
        // Arrange
        var schema = """{"type": "object"}""";

        // Act
        var definition = DocTypeDefinition.CreateBuiltIn(
            id: "test-type",
            name: "Test Type",
            description: "A test document type",
            schema: schema);

        // Assert
        definition.Schema.ShouldBe(schema);
    }

    [Fact]
    public void CreateBuiltIn_WithoutSchema_SchemaIsNull()
    {
        // Act
        var definition = DocTypeDefinition.CreateBuiltIn(
            id: "test-type",
            name: "Test Type",
            description: "A test document type");

        // Assert
        definition.Schema.ShouldBeNull();
    }

    [Fact]
    public void CreateBuiltIn_WithTriggerPhrases_IncludesTriggerPhrases()
    {
        // Arrange
        var triggers = new List<string> { "trigger1", "trigger2" };

        // Act
        var definition = DocTypeDefinition.CreateBuiltIn(
            id: "test-type",
            name: "Test Type",
            description: "A test document type",
            triggerPhrases: triggers);

        // Assert
        definition.TriggerPhrases.ShouldBe(triggers);
    }

    [Fact]
    public void CreateBuiltIn_WithoutTriggerPhrases_DefaultsToEmptyList()
    {
        // Act
        var definition = DocTypeDefinition.CreateBuiltIn(
            id: "test-type",
            name: "Test Type",
            description: "A test document type");

        // Assert
        definition.TriggerPhrases.ShouldBeEmpty();
    }

    [Fact]
    public void CreateBuiltIn_WithRequiredFields_IncludesRequiredFields()
    {
        // Arrange
        var requiredFields = new List<string> { "title", "doc_type" };

        // Act
        var definition = DocTypeDefinition.CreateBuiltIn(
            id: "test-type",
            name: "Test Type",
            description: "A test document type",
            requiredFields: requiredFields);

        // Assert
        definition.RequiredFields.ShouldBe(requiredFields);
    }

    [Fact]
    public void CreateBuiltIn_WithoutRequiredFields_DefaultsToEmptyList()
    {
        // Act
        var definition = DocTypeDefinition.CreateBuiltIn(
            id: "test-type",
            name: "Test Type",
            description: "A test document type");

        // Assert
        definition.RequiredFields.ShouldBeEmpty();
    }

    [Fact]
    public void CreateBuiltIn_WithOptionalFields_IncludesOptionalFields()
    {
        // Arrange
        var optionalFields = new List<string> { "tags", "links" };

        // Act
        var definition = DocTypeDefinition.CreateBuiltIn(
            id: "test-type",
            name: "Test Type",
            description: "A test document type",
            optionalFields: optionalFields);

        // Assert
        definition.OptionalFields.ShouldBe(optionalFields);
    }

    [Fact]
    public void CreateBuiltIn_WithoutOptionalFields_DefaultsToEmptyList()
    {
        // Act
        var definition = DocTypeDefinition.CreateBuiltIn(
            id: "test-type",
            name: "Test Type",
            description: "A test document type");

        // Assert
        definition.OptionalFields.ShouldBeEmpty();
    }

    [Fact]
    public void CreateBuiltIn_DefaultPromotionLevel_IsStandard()
    {
        // Act
        var definition = DocTypeDefinition.CreateBuiltIn(
            id: "test-type",
            name: "Test Type",
            description: "A test document type");

        // Assert
        definition.PromotionLevel.ShouldBe(PromotionLevels.Standard);
    }

    #endregion

    #region CreateCustom Tests

    [Fact]
    public void CreateCustom_WithRequiredParameters_CreatesValidDefinition()
    {
        // Act
        var definition = DocTypeDefinition.CreateCustom(
            id: "custom-type",
            name: "Custom Type",
            description: "A custom document type");

        // Assert
        definition.ShouldNotBeNull();
        definition.Id.ShouldBe("custom-type");
        definition.Name.ShouldBe("Custom Type");
        definition.Description.ShouldBe("A custom document type");
    }

    [Fact]
    public void CreateCustom_SetsIsBuiltInToFalse()
    {
        // Act
        var definition = DocTypeDefinition.CreateCustom(
            id: "custom-type",
            name: "Custom Type",
            description: "A custom document type");

        // Assert
        definition.IsBuiltIn.ShouldBeFalse();
    }

    [Fact]
    public void CreateCustom_WithPromotionLevel_SetsPromotionLevel()
    {
        // Act
        var definition = DocTypeDefinition.CreateCustom(
            id: "custom-type",
            name: "Custom Type",
            description: "A custom document type",
            promotionLevel: PromotionLevels.Promoted);

        // Assert
        definition.PromotionLevel.ShouldBe(PromotionLevels.Promoted);
    }

    [Fact]
    public void CreateCustom_WithoutPromotionLevel_DefaultsToStandard()
    {
        // Act
        var definition = DocTypeDefinition.CreateCustom(
            id: "custom-type",
            name: "Custom Type",
            description: "A custom document type");

        // Assert
        definition.PromotionLevel.ShouldBe(PromotionLevels.Standard);
    }

    [Fact]
    public void CreateCustom_WithNullPromotionLevel_DefaultsToStandard()
    {
        // Act
        var definition = DocTypeDefinition.CreateCustom(
            id: "custom-type",
            name: "Custom Type",
            description: "A custom document type",
            promotionLevel: null);

        // Assert
        definition.PromotionLevel.ShouldBe(PromotionLevels.Standard);
    }

    [Fact]
    public void CreateCustom_WithSchema_IncludesSchema()
    {
        // Arrange
        var schema = """{"type": "object"}""";

        // Act
        var definition = DocTypeDefinition.CreateCustom(
            id: "custom-type",
            name: "Custom Type",
            description: "A custom document type",
            schema: schema);

        // Assert
        definition.Schema.ShouldBe(schema);
    }

    [Fact]
    public void CreateCustom_WithTriggerPhrases_IncludesTriggerPhrases()
    {
        // Arrange
        var triggers = new List<string> { "custom", "special" };

        // Act
        var definition = DocTypeDefinition.CreateCustom(
            id: "custom-type",
            name: "Custom Type",
            description: "A custom document type",
            triggerPhrases: triggers);

        // Assert
        definition.TriggerPhrases.ShouldBe(triggers);
    }

    [Fact]
    public void CreateCustom_WithRequiredFields_IncludesRequiredFields()
    {
        // Arrange
        var requiredFields = new List<string> { "title", "doc_type", "custom_field" };

        // Act
        var definition = DocTypeDefinition.CreateCustom(
            id: "custom-type",
            name: "Custom Type",
            description: "A custom document type",
            requiredFields: requiredFields);

        // Assert
        definition.RequiredFields.ShouldBe(requiredFields);
    }

    [Fact]
    public void CreateCustom_WithOptionalFields_IncludesOptionalFields()
    {
        // Arrange
        var optionalFields = new List<string> { "metadata", "extra" };

        // Act
        var definition = DocTypeDefinition.CreateCustom(
            id: "custom-type",
            name: "Custom Type",
            description: "A custom document type",
            optionalFields: optionalFields);

        // Assert
        definition.OptionalFields.ShouldBe(optionalFields);
    }

    #endregion

    #region Property Default Tests

    [Fact]
    public void DocTypeDefinition_TriggerPhrases_DefaultsToEmptyList()
    {
        // Arrange & Act
        var definition = new DocTypeDefinition
        {
            Id = "test",
            Name = "Test",
            Description = "Test"
        };

        // Assert
        definition.TriggerPhrases.ShouldBeEmpty();
    }

    [Fact]
    public void DocTypeDefinition_RequiredFields_DefaultsToEmptyList()
    {
        // Arrange & Act
        var definition = new DocTypeDefinition
        {
            Id = "test",
            Name = "Test",
            Description = "Test"
        };

        // Assert
        definition.RequiredFields.ShouldBeEmpty();
    }

    [Fact]
    public void DocTypeDefinition_OptionalFields_DefaultsToEmptyList()
    {
        // Arrange & Act
        var definition = new DocTypeDefinition
        {
            Id = "test",
            Name = "Test",
            Description = "Test"
        };

        // Assert
        definition.OptionalFields.ShouldBeEmpty();
    }

    [Fact]
    public void DocTypeDefinition_PromotionLevel_DefaultsToStandard()
    {
        // Arrange & Act
        var definition = new DocTypeDefinition
        {
            Id = "test",
            Name = "Test",
            Description = "Test"
        };

        // Assert
        definition.PromotionLevel.ShouldBe(PromotionLevels.Standard);
    }

    #endregion
}
