using CompoundDocs.McpServer.Skills;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;

namespace CompoundDocs.Tests.Skills;

/// <summary>
/// Tests for SkillLoader to verify all 17 skills load correctly.
/// </summary>
public sealed class SkillLoaderTests : IDisposable
{
    private readonly SkillLoader _skillLoader;
    private readonly string _skillsDirectory;

    /// <summary>
    /// All 17 expected skill names from the manifest.
    /// </summary>
    private static readonly string[] ExpectedSkillNames =
    [
        "/cdocs:capture-problem",
        "/cdocs:capture-insight",
        "/cdocs:capture-codebase",
        "/cdocs:capture-tool",
        "/cdocs:capture-style",
        "/cdocs:query",
        "/cdocs:search",
        "/cdocs:recall",
        "/cdocs:related",
        "/cdocs:activate",
        "/cdocs:deactivate",
        "/cdocs:status",
        "/cdocs:promote",
        "/cdocs:demote",
        "/cdocs:delete",
        "/cdocs:reindex",
        "/cdocs:help"
    ];

    public SkillLoaderTests()
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
        var assemblyLocation = typeof(SkillLoaderTests).Assembly.Location;
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

    [Fact]
    public async Task LoadSkillsAsync_LoadsAll17Skills()
    {
        // Act
        var loadedCount = await _skillLoader.LoadSkillsAsync(_skillsDirectory);

        // Assert
        loadedCount.ShouldBe(17, "Should load exactly 17 skills");
    }

    [Fact]
    public async Task LoadSkillsAsync_InitializesLoader()
    {
        // Act
        await _skillLoader.LoadSkillsAsync(_skillsDirectory);

        // Assert
        _skillLoader.IsInitialized.ShouldBeTrue();
    }

    [Fact]
    public async Task GetAllSkills_ReturnsAll17Skills()
    {
        // Arrange
        await _skillLoader.LoadSkillsAsync(_skillsDirectory);

        // Act
        var skills = _skillLoader.GetAllSkills();

        // Assert
        skills.Count.ShouldBe(17);
    }

    [Theory]
    [MemberData(nameof(GetAllSkillNames))]
    public async Task GetSkill_ReturnsSkillForEachExpectedName(string skillName)
    {
        // Arrange
        await _skillLoader.LoadSkillsAsync(_skillsDirectory);

        // Act
        var skill = _skillLoader.GetSkill(skillName);

        // Assert
        skill.ShouldNotBeNull($"Skill '{skillName}' should exist");
        skill.Name.ShouldBe(skillName);
    }

    [Theory]
    [InlineData("capture-problem")]
    [InlineData("query")]
    [InlineData("help")]
    [InlineData("activate")]
    public async Task GetSkill_FindsSkillWithoutPrefix(string shortName)
    {
        // Arrange
        await _skillLoader.LoadSkillsAsync(_skillsDirectory);

        // Act
        var skill = _skillLoader.GetSkill(shortName);

        // Assert
        skill.ShouldNotBeNull($"Skill '{shortName}' should be found without prefix");
        skill.Name.ShouldBe($"/cdocs:{shortName}");
    }

    [Fact]
    public async Task LoadSkillsAsync_AllSkillsHaveValidNames()
    {
        // Arrange
        await _skillLoader.LoadSkillsAsync(_skillsDirectory);

        // Act
        var skills = _skillLoader.GetAllSkills();

        // Assert
        foreach (var skill in skills)
        {
            skill.Name.ShouldStartWith("/cdocs:");
            skill.Name.Length.ShouldBeGreaterThan(7);
        }
    }

    [Fact]
    public async Task LoadSkillsAsync_AllSkillsHaveDescriptions()
    {
        // Arrange
        await _skillLoader.LoadSkillsAsync(_skillsDirectory);

        // Act
        var skills = _skillLoader.GetAllSkills();

        // Assert
        foreach (var skill in skills)
        {
            skill.Description.ShouldNotBeNullOrWhiteSpace(
                $"Skill '{skill.Name}' should have a description");
        }
    }

    [Fact]
    public async Task LoadSkillsAsync_AllSkillsHaveTriggers()
    {
        // Arrange
        await _skillLoader.LoadSkillsAsync(_skillsDirectory);

        // Act
        var skills = _skillLoader.GetAllSkills();

        // Assert
        foreach (var skill in skills)
        {
            skill.Triggers.ShouldNotBeEmpty($"Skill '{skill.Name}' should have at least one trigger");
        }
    }

    [Fact]
    public async Task LoadSkillsAsync_AllSkillsHaveVersions()
    {
        // Arrange
        await _skillLoader.LoadSkillsAsync(_skillsDirectory);

        // Act
        var skills = _skillLoader.GetAllSkills();

        // Assert
        foreach (var skill in skills)
        {
            skill.Version.ShouldNotBeNullOrWhiteSpace($"Skill '{skill.Name}' should have a version");
            // Version should match semantic versioning pattern
            var isValidSemver = System.Text.RegularExpressions.Regex.IsMatch(
                skill.Version, @"^\d+\.\d+\.\d+$");
            isValidSemver.ShouldBeTrue(
                $"Skill '{skill.Name}' version '{skill.Version}' should be semver format");
        }
    }

    [Fact]
    public async Task LoadSkillsAsync_CaptureSkillsHaveOutputTemplates()
    {
        // Arrange
        await _skillLoader.LoadSkillsAsync(_skillsDirectory);
        var captureSkillNames = new[]
        {
            "/cdocs:capture-problem",
            "/cdocs:capture-insight",
            "/cdocs:capture-codebase",
            "/cdocs:capture-tool",
            "/cdocs:capture-style"
        };

        // Act & Assert
        foreach (var skillName in captureSkillNames)
        {
            var skill = _skillLoader.GetSkill(skillName);
            skill.ShouldNotBeNull();
            skill.Output.ShouldNotBeNull($"Capture skill '{skillName}' should have output configuration");
            skill.Output.Template.ShouldNotBeNullOrWhiteSpace(
                $"Capture skill '{skillName}' should have an output template");
            skill.Output.Format.ShouldBe("markdown",
                $"Capture skill '{skillName}' should output markdown");
        }
    }

    [Fact]
    public async Task LoadSkillsAsync_QuerySkillsHaveToolCalls()
    {
        // Arrange
        await _skillLoader.LoadSkillsAsync(_skillsDirectory);
        var querySkillNames = new[]
        {
            "/cdocs:query",
            "/cdocs:search",
            "/cdocs:recall",
            "/cdocs:related"
        };

        // Act & Assert
        foreach (var skillName in querySkillNames)
        {
            var skill = _skillLoader.GetSkill(skillName);
            skill.ShouldNotBeNull();
            skill.ToolCalls.ShouldNotBeEmpty($"Query skill '{skillName}' should have tool calls");
        }
    }

    [Fact]
    public async Task LoadSkillsAsync_MetaSkillsHaveCorrectCategory()
    {
        // Arrange
        await _skillLoader.LoadSkillsAsync(_skillsDirectory);
        var metaSkillNames = new[]
        {
            "/cdocs:activate",
            "/cdocs:deactivate",
            "/cdocs:status",
            "/cdocs:help"
        };

        // Act & Assert
        foreach (var skillName in metaSkillNames)
        {
            var skill = _skillLoader.GetSkill(skillName);
            skill.ShouldNotBeNull();
            skill.Metadata.ShouldNotBeNull($"Meta skill '{skillName}' should have metadata");
            skill.Metadata.Category.ShouldBe("meta",
                $"Meta skill '{skillName}' should have category 'meta'");
        }
    }

    [Fact]
    public async Task LoadSkillsAsync_UtilitySkillsHaveCorrectCategory()
    {
        // Arrange
        await _skillLoader.LoadSkillsAsync(_skillsDirectory);
        var utilitySkillNames = new[]
        {
            "/cdocs:promote",
            "/cdocs:demote",
            "/cdocs:delete",
            "/cdocs:reindex"
        };

        // Act & Assert
        foreach (var skillName in utilitySkillNames)
        {
            var skill = _skillLoader.GetSkill(skillName);
            skill.ShouldNotBeNull();
            skill.Metadata.ShouldNotBeNull($"Utility skill '{skillName}' should have metadata");
            skill.Metadata.Category.ShouldBe("utility",
                $"Utility skill '{skillName}' should have category 'utility'");
        }
    }

    [Fact]
    public async Task LoadSkillsAsync_AllSkillNamesAreUnique()
    {
        // Arrange
        await _skillLoader.LoadSkillsAsync(_skillsDirectory);

        // Act
        var skills = _skillLoader.GetAllSkills();
        var names = skills.Select(s => s.Name).ToList();
        var uniqueNames = names.Distinct().ToList();

        // Assert
        names.Count.ShouldBe(uniqueNames.Count, "All skill names should be unique");
    }

    [Fact]
    public async Task LoadSkillsAsync_ShortNamesAreCorrect()
    {
        // Arrange
        await _skillLoader.LoadSkillsAsync(_skillsDirectory);

        // Act
        var skills = _skillLoader.GetAllSkills();

        // Assert
        foreach (var skill in skills)
        {
            var expectedShortName = skill.Name[7..]; // Remove "/cdocs:"
            skill.ShortName.ShouldBe(expectedShortName,
                $"Skill '{skill.Name}' should have correct short name");
        }
    }

    [Fact]
    public async Task ReloadAsync_ReloadsAllSkills()
    {
        // Arrange
        await _skillLoader.LoadSkillsAsync(_skillsDirectory);
        var initialCount = _skillLoader.SkillCount;

        // Act
        var reloadedCount = await _skillLoader.ReloadAsync(_skillsDirectory);

        // Assert
        reloadedCount.ShouldBe(initialCount);
        _skillLoader.IsInitialized.ShouldBeTrue();
    }

    [Fact]
    public async Task SkillCount_ReturnsCorrectCount()
    {
        // Arrange
        await _skillLoader.LoadSkillsAsync(_skillsDirectory);

        // Act
        var count = _skillLoader.SkillCount;

        // Assert
        count.ShouldBe(17);
    }

    [Fact]
    public async Task LoadSkillsAsync_WithNonExistentDirectory_ReturnsZero()
    {
        // Act
        var count = await _skillLoader.LoadSkillsAsync("/non/existent/path");

        // Assert
        count.ShouldBe(0);
    }

    [Fact]
    public void GetSkill_BeforeInitialization_ReturnsNull()
    {
        // Act
        var skill = _skillLoader.GetSkill("/cdocs:query");

        // Assert
        skill.ShouldBeNull();
    }

    [Fact]
    public void GetAllSkills_BeforeInitialization_ReturnsEmpty()
    {
        // Act
        var skills = _skillLoader.GetAllSkills();

        // Assert
        skills.ShouldBeEmpty();
    }

    public static IEnumerable<object[]> GetAllSkillNames()
    {
        return ExpectedSkillNames.Select(name => new object[] { name });
    }
}
