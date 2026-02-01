using CompoundDocs.McpServer.Skills;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;

namespace CompoundDocs.Tests.Skills;

/// <summary>
/// Tests for skill trigger phrase matching functionality.
/// </summary>
public sealed class SkillTriggerMatchingTests : IDisposable
{
    private readonly SkillLoader _skillLoader;
    private readonly string _skillsDirectory;

    public SkillTriggerMatchingTests()
    {
        var logger = new Mock<ILogger<SkillLoader>>();
        _skillLoader = new SkillLoader(logger.Object);

        // Find the skills directory relative to the test execution directory
        var currentDir = Directory.GetCurrentDirectory();
        var projectRoot = FindProjectRoot(currentDir);
        _skillsDirectory = Path.Combine(projectRoot, "skills");
    }

    private static string FindProjectRoot(string startDir)
    {
        // Start from the current directory and go up looking for the skills directory
        var dir = new DirectoryInfo(startDir);
        while (dir != null)
        {
            var skillsPath = Path.Combine(dir.FullName, "skills");
            if (Directory.Exists(skillsPath) &&
                Directory.GetFiles(skillsPath, "*.yaml").Length > 0)
            {
                return dir.FullName;
            }

            // Also check for .sln file as indicator of project root
            if (dir.GetFiles("*.sln").Length > 0)
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        // Fallback: try using the assembly location
        var assemblyLocation = typeof(SkillTriggerMatchingTests).Assembly.Location;
        if (!string.IsNullOrEmpty(assemblyLocation))
        {
            var assemblyDir = Path.GetDirectoryName(assemblyLocation);
            dir = new DirectoryInfo(assemblyDir!);
            while (dir != null)
            {
                var skillsPath = Path.Combine(dir.FullName, "skills");
                if (Directory.Exists(skillsPath) &&
                    Directory.GetFiles(skillsPath, "*.yaml").Length > 0)
                {
                    return dir.FullName;
                }
                dir = dir.Parent;
            }
        }

        throw new InvalidOperationException(
            $"Could not find project root from {startDir}. " +
            "Ensure the skills directory exists at the project root.");
    }

    public void Dispose()
    {
        _skillLoader.Dispose();
    }

    [Theory]
    [InlineData("capture problem")]
    [InlineData("document problem")]
    [InlineData("log problem")]
    [InlineData("record issue")]
    [InlineData("capture bug")]
    public async Task FindByTrigger_CaptureProblemTriggers_ReturnsCaptureProblemSkill(string trigger)
    {
        // Arrange
        await _skillLoader.LoadSkillsAsync(_skillsDirectory);

        // Act
        var skills = _skillLoader.FindByTrigger(trigger);

        // Assert
        skills.ShouldNotBeEmpty($"Should find skill for trigger '{trigger}'");
        skills.ShouldContain(s => s.Name == "/cdocs:capture-problem",
            $"Should find capture-problem skill for trigger '{trigger}'");
    }

    [Theory]
    [InlineData("capture insight")]
    [InlineData("document insight")]
    [InlineData("TIL")]
    [InlineData("today I learned")]
    public async Task FindByTrigger_CaptureInsightTriggers_ReturnsCaptureInsightSkill(string trigger)
    {
        // Arrange
        await _skillLoader.LoadSkillsAsync(_skillsDirectory);

        // Act
        var skills = _skillLoader.FindByTrigger(trigger);

        // Assert
        skills.ShouldNotBeEmpty($"Should find skill for trigger '{trigger}'");
        skills.ShouldContain(s => s.Name == "/cdocs:capture-insight",
            $"Should find capture-insight skill for trigger '{trigger}'");
    }

    [Theory]
    [InlineData("query")]
    [InlineData("ask question")]
    [InlineData("what is")]
    [InlineData("how do I")]
    public async Task FindByTrigger_QueryTriggers_ReturnsQuerySkill(string trigger)
    {
        // Arrange
        await _skillLoader.LoadSkillsAsync(_skillsDirectory);

        // Act
        var skills = _skillLoader.FindByTrigger(trigger);

        // Assert
        skills.ShouldNotBeEmpty($"Should find skill for trigger '{trigger}'");
        skills.ShouldContain(s => s.Name == "/cdocs:query",
            $"Should find query skill for trigger '{trigger}'");
    }

    [Theory]
    [InlineData("search")]
    [InlineData("find documents")]
    [InlineData("semantic search")]
    public async Task FindByTrigger_SearchTriggers_ReturnsSearchSkill(string trigger)
    {
        // Arrange
        await _skillLoader.LoadSkillsAsync(_skillsDirectory);

        // Act
        var skills = _skillLoader.FindByTrigger(trigger);

        // Assert
        skills.ShouldNotBeEmpty($"Should find skill for trigger '{trigger}'");
        skills.ShouldContain(s => s.Name == "/cdocs:search",
            $"Should find search skill for trigger '{trigger}'");
    }

    [Theory]
    [InlineData("activate project")]
    [InlineData("switch project")]
    [InlineData("open project")]
    public async Task FindByTrigger_ActivateTriggers_ReturnsActivateSkill(string trigger)
    {
        // Arrange
        await _skillLoader.LoadSkillsAsync(_skillsDirectory);

        // Act
        var skills = _skillLoader.FindByTrigger(trigger);

        // Assert
        skills.ShouldNotBeEmpty($"Should find skill for trigger '{trigger}'");
        skills.ShouldContain(s => s.Name == "/cdocs:activate",
            $"Should find activate skill for trigger '{trigger}'");
    }

    [Theory]
    [InlineData("help")]
    [InlineData("list skills")]
    [InlineData("available commands")]
    public async Task FindByTrigger_HelpTriggers_ReturnsHelpSkill(string trigger)
    {
        // Arrange
        await _skillLoader.LoadSkillsAsync(_skillsDirectory);

        // Act
        var skills = _skillLoader.FindByTrigger(trigger);

        // Assert
        skills.ShouldNotBeEmpty($"Should find skill for trigger '{trigger}'");
        skills.ShouldContain(s => s.Name == "/cdocs:help",
            $"Should find help skill for trigger '{trigger}'");
    }

    [Theory]
    [InlineData("status")]
    [InlineData("show status")]
    [InlineData("current project")]
    public async Task FindByTrigger_StatusTriggers_ReturnsStatusSkill(string trigger)
    {
        // Arrange
        await _skillLoader.LoadSkillsAsync(_skillsDirectory);

        // Act
        var skills = _skillLoader.FindByTrigger(trigger);

        // Assert
        skills.ShouldNotBeEmpty($"Should find skill for trigger '{trigger}'");
        skills.ShouldContain(s => s.Name == "/cdocs:status",
            $"Should find status skill for trigger '{trigger}'");
    }

    [Fact]
    public async Task FindByTrigger_CaseInsensitive_FindsSkill()
    {
        // Arrange
        await _skillLoader.LoadSkillsAsync(_skillsDirectory);

        // Act
        var skills = _skillLoader.FindByTrigger("CAPTURE PROBLEM");

        // Assert
        skills.ShouldNotBeEmpty();
        skills.ShouldContain(s => s.Name == "/cdocs:capture-problem");
    }

    [Fact]
    public async Task FindByTrigger_PartialMatch_FindsSkill()
    {
        // Arrange
        await _skillLoader.LoadSkillsAsync(_skillsDirectory);

        // Act
        var skills = _skillLoader.FindByTrigger("capture");

        // Assert
        skills.ShouldNotBeEmpty("Partial match should find skills");
    }

    [Fact]
    public async Task FindByTrigger_WithWhitespace_FindsSkill()
    {
        // Arrange
        await _skillLoader.LoadSkillsAsync(_skillsDirectory);

        // Act
        var skills = _skillLoader.FindByTrigger("  capture problem  ");

        // Assert
        skills.ShouldNotBeEmpty();
        skills.ShouldContain(s => s.Name == "/cdocs:capture-problem");
    }

    [Fact]
    public async Task FindByTrigger_EmptyString_ReturnsEmpty()
    {
        // Arrange
        await _skillLoader.LoadSkillsAsync(_skillsDirectory);

        // Act
        var skills = _skillLoader.FindByTrigger(string.Empty);

        // Assert
        skills.ShouldBeEmpty();
    }

    [Fact]
    public async Task FindByTrigger_NullString_ReturnsEmpty()
    {
        // Arrange
        await _skillLoader.LoadSkillsAsync(_skillsDirectory);

        // Act
        var skills = _skillLoader.FindByTrigger(null!);

        // Assert
        skills.ShouldBeEmpty();
    }

    [Fact]
    public async Task FindByTrigger_NonMatchingPhrase_ReturnsEmpty()
    {
        // Arrange
        await _skillLoader.LoadSkillsAsync(_skillsDirectory);

        // Act
        var skills = _skillLoader.FindByTrigger("xyzzy invalid command");

        // Assert
        skills.ShouldBeEmpty();
    }

    [Fact]
    public async Task FindByTrigger_AmbiguousTrigger_MayReturnMultipleSkills()
    {
        // Arrange
        await _skillLoader.LoadSkillsAsync(_skillsDirectory);

        // Act - "document" appears in multiple skill triggers
        var skills = _skillLoader.FindByTrigger("document");

        // Assert
        skills.ShouldNotBeEmpty("Ambiguous triggers may match multiple skills");
    }

    [Theory]
    [InlineData("recall")]
    [InlineData("remember")]
    [InlineData("follow up")]
    public async Task FindByTrigger_RecallTriggers_ReturnsRecallSkill(string trigger)
    {
        // Arrange
        await _skillLoader.LoadSkillsAsync(_skillsDirectory);

        // Act
        var skills = _skillLoader.FindByTrigger(trigger);

        // Assert
        skills.ShouldNotBeEmpty($"Should find skill for trigger '{trigger}'");
        skills.ShouldContain(s => s.Name == "/cdocs:recall",
            $"Should find recall skill for trigger '{trigger}'");
    }

    [Theory]
    [InlineData("related")]
    [InlineData("find related")]
    [InlineData("connections")]
    public async Task FindByTrigger_RelatedTriggers_ReturnsRelatedSkill(string trigger)
    {
        // Arrange
        await _skillLoader.LoadSkillsAsync(_skillsDirectory);

        // Act
        var skills = _skillLoader.FindByTrigger(trigger);

        // Assert
        skills.ShouldNotBeEmpty($"Should find skill for trigger '{trigger}'");
        skills.ShouldContain(s => s.Name == "/cdocs:related",
            $"Should find related skill for trigger '{trigger}'");
    }

    [Theory]
    [InlineData("promote document")]
    [InlineData("boost document")]
    [InlineData("make important")]
    public async Task FindByTrigger_PromoteTriggers_ReturnsPromoteSkill(string trigger)
    {
        // Arrange
        await _skillLoader.LoadSkillsAsync(_skillsDirectory);

        // Act
        var skills = _skillLoader.FindByTrigger(trigger);

        // Assert
        skills.ShouldNotBeEmpty($"Should find skill for trigger '{trigger}'");
        skills.ShouldContain(s => s.Name == "/cdocs:promote",
            $"Should find promote skill for trigger '{trigger}'");
    }

    [Theory]
    [InlineData("demote document")]
    [InlineData("unpin document")]
    [InlineData("make standard")]
    public async Task FindByTrigger_DemoteTriggers_ReturnsDemoteSkill(string trigger)
    {
        // Arrange
        await _skillLoader.LoadSkillsAsync(_skillsDirectory);

        // Act
        var skills = _skillLoader.FindByTrigger(trigger);

        // Assert
        skills.ShouldNotBeEmpty($"Should find skill for trigger '{trigger}'");
        skills.ShouldContain(s => s.Name == "/cdocs:demote",
            $"Should find demote skill for trigger '{trigger}'");
    }

    [Theory]
    [InlineData("delete document")]
    [InlineData("remove document")]
    [InlineData("unindex document")]
    public async Task FindByTrigger_DeleteTriggers_ReturnsDeleteSkill(string trigger)
    {
        // Arrange
        await _skillLoader.LoadSkillsAsync(_skillsDirectory);

        // Act
        var skills = _skillLoader.FindByTrigger(trigger);

        // Assert
        skills.ShouldNotBeEmpty($"Should find skill for trigger '{trigger}'");
        skills.ShouldContain(s => s.Name == "/cdocs:delete",
            $"Should find delete skill for trigger '{trigger}'");
    }

    [Theory]
    [InlineData("reindex")]
    [InlineData("refresh index")]
    [InlineData("rebuild index")]
    public async Task FindByTrigger_ReindexTriggers_ReturnsReindexSkill(string trigger)
    {
        // Arrange
        await _skillLoader.LoadSkillsAsync(_skillsDirectory);

        // Act
        var skills = _skillLoader.FindByTrigger(trigger);

        // Assert
        skills.ShouldNotBeEmpty($"Should find skill for trigger '{trigger}'");
        skills.ShouldContain(s => s.Name == "/cdocs:reindex",
            $"Should find reindex skill for trigger '{trigger}'");
    }

    [Theory]
    [InlineData("deactivate")]
    [InlineData("close project")]
    [InlineData("reset session")]
    public async Task FindByTrigger_DeactivateTriggers_ReturnsDeactivateSkill(string trigger)
    {
        // Arrange
        await _skillLoader.LoadSkillsAsync(_skillsDirectory);

        // Act
        var skills = _skillLoader.FindByTrigger(trigger);

        // Assert
        skills.ShouldNotBeEmpty($"Should find skill for trigger '{trigger}'");
        skills.ShouldContain(s => s.Name == "/cdocs:deactivate",
            $"Should find deactivate skill for trigger '{trigger}'");
    }

    [Fact]
    public async Task AllSkills_HaveAtLeastOneTrigger()
    {
        // Arrange
        await _skillLoader.LoadSkillsAsync(_skillsDirectory);

        // Act
        var skills = _skillLoader.GetAllSkills();

        // Assert
        foreach (var skill in skills)
        {
            skill.Triggers.ShouldNotBeEmpty($"Skill '{skill.Name}' should have at least one trigger");
            skill.Triggers.All(t => !string.IsNullOrWhiteSpace(t))
                .ShouldBeTrue($"Skill '{skill.Name}' should not have empty triggers");
        }
    }

    [Fact]
    public async Task AllTriggers_AreAtLeastThreeCharacters()
    {
        // Arrange
        await _skillLoader.LoadSkillsAsync(_skillsDirectory);

        // Act
        var skills = _skillLoader.GetAllSkills();

        // Assert
        foreach (var skill in skills)
        {
            foreach (var trigger in skill.Triggers)
            {
                trigger.Length.ShouldBeGreaterThanOrEqualTo(3,
                    $"Skill '{skill.Name}' trigger '{trigger}' should be at least 3 characters");
            }
        }
    }
}
