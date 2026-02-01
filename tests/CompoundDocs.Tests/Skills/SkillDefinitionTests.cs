using CompoundDocs.McpServer.Skills;
using Shouldly;

namespace CompoundDocs.Tests.Skills;

/// <summary>
/// Tests for SkillDefinition model validation and behavior.
/// </summary>
public sealed class SkillDefinitionTests
{
    [Fact]
    public void ShortName_WithCdocsPrefix_ReturnsNameWithoutPrefix()
    {
        // Arrange
        var skill = new SkillDefinition { Name = "/cdocs:capture-problem" };

        // Act
        var shortName = skill.ShortName;

        // Assert
        shortName.ShouldBe("capture-problem");
    }

    [Fact]
    public void ShortName_WithoutPrefix_ReturnsOriginalName()
    {
        // Arrange
        var skill = new SkillDefinition { Name = "capture-problem" };

        // Act
        var shortName = skill.ShortName;

        // Assert
        shortName.ShouldBe("capture-problem");
    }

    [Fact]
    public void ShortName_WithEmptyName_ReturnsEmpty()
    {
        // Arrange
        var skill = new SkillDefinition { Name = string.Empty };

        // Act
        var shortName = skill.ShortName;

        // Assert
        shortName.ShouldBe(string.Empty);
    }

    [Fact]
    public void ShortName_CaseInsensitivePrefix()
    {
        // Arrange
        var skill = new SkillDefinition { Name = "/CDOCS:capture-problem" };

        // Act
        var shortName = skill.ShortName;

        // Assert
        shortName.ShouldBe("capture-problem");
    }

    [Fact]
    public void DefaultVersion_IsOnePointZeroZero()
    {
        // Arrange & Act
        var skill = new SkillDefinition();

        // Assert
        skill.Version.ShouldBe("1.0.0");
    }

    [Fact]
    public void DefaultTriggers_IsEmpty()
    {
        // Arrange & Act
        var skill = new SkillDefinition();

        // Assert
        skill.Triggers.ShouldNotBeNull();
        skill.Triggers.ShouldBeEmpty();
    }

    [Fact]
    public void DefaultParameters_IsEmpty()
    {
        // Arrange & Act
        var skill = new SkillDefinition();

        // Assert
        skill.Parameters.ShouldNotBeNull();
        skill.Parameters.ShouldBeEmpty();
    }

    [Fact]
    public void DefaultToolCalls_IsEmpty()
    {
        // Arrange & Act
        var skill = new SkillDefinition();

        // Assert
        skill.ToolCalls.ShouldNotBeNull();
        skill.ToolCalls.ShouldBeEmpty();
    }

    [Fact]
    public void DefaultOutput_IsNull()
    {
        // Arrange & Act
        var skill = new SkillDefinition();

        // Assert
        skill.Output.ShouldBeNull();
    }

    [Fact]
    public void DefaultMetadata_IsNull()
    {
        // Arrange & Act
        var skill = new SkillDefinition();

        // Assert
        skill.Metadata.ShouldBeNull();
    }

    [Fact]
    public void SkillParameter_DefaultRequired_IsFalse()
    {
        // Arrange & Act
        var param = new SkillParameter();

        // Assert
        param.Required.ShouldBeFalse();
    }

    [Fact]
    public void SkillParameter_DefaultType_IsString()
    {
        // Arrange & Act
        var param = new SkillParameter();

        // Assert
        param.Type.ShouldBe("string");
    }

    [Fact]
    public void ParameterValidation_AllPropertiesNullByDefault()
    {
        // Arrange & Act
        var validation = new ParameterValidation();

        // Assert
        validation.Pattern.ShouldBeNull();
        validation.MinLength.ShouldBeNull();
        validation.MaxLength.ShouldBeNull();
        validation.Min.ShouldBeNull();
        validation.Max.ShouldBeNull();
        validation.AllowedValues.ShouldBeNull();
    }

    [Fact]
    public void SkillToolCall_DefaultArguments_IsEmpty()
    {
        // Arrange & Act
        var toolCall = new SkillToolCall();

        // Assert
        toolCall.Arguments.ShouldNotBeNull();
        toolCall.Arguments.ShouldBeEmpty();
    }

    [Fact]
    public void SkillToolCall_DefaultCondition_IsNull()
    {
        // Arrange & Act
        var toolCall = new SkillToolCall();

        // Assert
        toolCall.Condition.ShouldBeNull();
    }

    [Fact]
    public void SkillToolCall_DefaultResultVariable_IsNull()
    {
        // Arrange & Act
        var toolCall = new SkillToolCall();

        // Assert
        toolCall.ResultVariable.ShouldBeNull();
    }

    [Fact]
    public void SkillOutput_DefaultFormat_IsJson()
    {
        // Arrange & Act
        var output = new SkillOutput();

        // Assert
        output.Format.ShouldBe("json");
    }

    [Fact]
    public void SkillOutput_DefaultIncludeToolResults_IsTrue()
    {
        // Arrange & Act
        var output = new SkillOutput();

        // Assert
        output.IncludeToolResults.ShouldBeTrue();
    }

    [Fact]
    public void SkillOutput_DefaultTemplate_IsNull()
    {
        // Arrange & Act
        var output = new SkillOutput();

        // Assert
        output.Template.ShouldBeNull();
    }

    [Fact]
    public void SkillDefinition_CanSetAllProperties()
    {
        // Arrange & Act
        var skill = new SkillDefinition
        {
            Name = "/cdocs:test-skill",
            Description = "Test description",
            Version = "2.0.0",
            Triggers = ["trigger1", "trigger2"],
            Parameters =
            [
                new SkillParameter
                {
                    Name = "param1",
                    Type = "string",
                    Description = "Test param",
                    Required = true,
                    Default = "default value",
                    Validation = new ParameterValidation
                    {
                        MinLength = 1,
                        MaxLength = 100
                    }
                }
            ],
            ToolCalls =
            [
                new SkillToolCall
                {
                    Tool = "test_tool",
                    Arguments = new Dictionary<string, object?> { ["arg1"] = "value1" },
                    Condition = "param1",
                    ResultVariable = "result1"
                }
            ],
            Output = new SkillOutput
            {
                Format = "markdown",
                Template = "# {{title}}",
                IncludeToolResults = false
            },
            Metadata = new SkillMetadata
            {
                Author = "test-author",
                Category = "capture",
                Tags = ["tag1", "tag2"]
            }
        };

        // Assert
        skill.Name.ShouldBe("/cdocs:test-skill");
        skill.Description.ShouldBe("Test description");
        skill.Version.ShouldBe("2.0.0");
        skill.Triggers.Count.ShouldBe(2);
        skill.Parameters.Count.ShouldBe(1);
        skill.ToolCalls.Count.ShouldBe(1);
        skill.Output.ShouldNotBeNull();
        skill.Output.Format.ShouldBe("markdown");
        skill.Metadata.ShouldNotBeNull();
        skill.Metadata.Author.ShouldBe("test-author");
    }

    [Theory]
    [InlineData("/cdocs:capture-problem", "capture-problem")]
    [InlineData("/cdocs:query", "query")]
    [InlineData("/cdocs:capture-insight", "capture-insight")]
    [InlineData("/cdocs:activate", "activate")]
    [InlineData("/cdocs:help", "help")]
    public void ShortName_ExtractsCorrectName(string fullName, string expectedShortName)
    {
        // Arrange
        var skill = new SkillDefinition { Name = fullName };

        // Act
        var shortName = skill.ShortName;

        // Assert
        shortName.ShouldBe(expectedShortName);
    }

    [Fact]
    public void ParameterValidation_EnumValues_CanBeSet()
    {
        // Arrange & Act
        var validation = new ParameterValidation
        {
            AllowedValues = ["low", "medium", "high"]
        };

        // Assert
        validation.AllowedValues.ShouldNotBeNull();
        validation.AllowedValues.Count.ShouldBe(3);
        validation.AllowedValues.ShouldContain("low");
        validation.AllowedValues.ShouldContain("medium");
        validation.AllowedValues.ShouldContain("high");
    }

    [Fact]
    public void ParameterValidation_NumericConstraints_CanBeSet()
    {
        // Arrange & Act
        var validation = new ParameterValidation
        {
            Min = 1.0,
            Max = 100.0
        };

        // Assert
        validation.Min.ShouldBe(1.0);
        validation.Max.ShouldBe(100.0);
    }

    [Fact]
    public void ParameterValidation_StringConstraints_CanBeSet()
    {
        // Arrange & Act
        var validation = new ParameterValidation
        {
            MinLength = 5,
            MaxLength = 200,
            Pattern = @"^[a-z]+$"
        };

        // Assert
        validation.MinLength.ShouldBe(5);
        validation.MaxLength.ShouldBe(200);
        validation.Pattern.ShouldBe(@"^[a-z]+$");
    }

    [Fact]
    public void SkillMetadata_DefaultProperties_AreNull()
    {
        // Arrange & Act
        var metadata = new SkillMetadata();

        // Assert
        metadata.Author.ShouldBeNull();
        metadata.Category.ShouldBeNull();
        metadata.Tags.ShouldBeNull();
    }

    [Theory]
    [InlineData("capture")]
    [InlineData("query")]
    [InlineData("meta")]
    [InlineData("utility")]
    public void SkillMetadata_Category_AcceptsValidValues(string category)
    {
        // Arrange & Act
        var metadata = new SkillMetadata { Category = category };

        // Assert
        metadata.Category.ShouldBe(category);
    }
}
