using CompoundDocs.McpServer.Agents;
using CompoundDocs.Tests.Infrastructure;
using Microsoft.Extensions.Logging;

namespace CompoundDocs.Tests.Agents;

/// <summary>
/// Unit tests for AgentLoader.
/// </summary>
public sealed class AgentLoaderTests : AsyncTestBase
{
    private readonly Mock<ILogger<AgentLoader>> _mockLogger;
    private readonly AgentLoader _loader;
    private string? _tempDirectory;

    public AgentLoaderTests()
    {
        _mockLogger = CreateLooseMock<ILogger<AgentLoader>>();
        _loader = new AgentLoader(_mockLogger.Object);
    }

    public override async Task DisposeAsync()
    {
        _loader.Dispose();

        // Clean up temp directory if it exists
        if (_tempDirectory != null && Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }

        await base.DisposeAsync();
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        var ex = Assert.Throws<ArgumentNullException>(() => new AgentLoader(null!));
        Assert.Equal("logger", ex.ParamName);
    }

    [Fact]
    public void InitialState_IsNotInitialized()
    {
        // Assert
        Assert.False(_loader.IsInitialized);
        Assert.Equal(0, _loader.AgentCount);
    }

    [Fact]
    public async Task LoadAgentsAsync_WithNonExistentDirectory_ReturnsZero()
    {
        // Arrange
        var nonExistentDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        // Act
        var count = await _loader.LoadAgentsAsync(nonExistentDir);

        // Assert
        Assert.Equal(0, count);
        Assert.False(_loader.IsInitialized);
    }

    [Fact]
    public async Task LoadAgentsAsync_WithEmptyDirectory_ReturnsZero()
    {
        // Arrange
        _tempDirectory = CreateTempDirectory();

        // Act
        var count = await _loader.LoadAgentsAsync(_tempDirectory);

        // Assert
        Assert.Equal(0, count);
        Assert.True(_loader.IsInitialized);
    }

    [Fact]
    public async Task LoadAgentsAsync_WithValidAgentFile_LoadsAgent()
    {
        // Arrange
        _tempDirectory = CreateTempDirectory();
        var agentYaml = @"
name: test-agent
description: A test agent for unit testing
version: ""1.0.0""
purpose: |
  This is a test agent used for validating the agent loader functionality.
mcp_tools:
  - name: test_tool
    description: A test tool
    provider: cdocs
    required: true
skills:
  - name: test_skill
    description: A test skill
";
        File.WriteAllText(Path.Combine(_tempDirectory, "test-agent.yaml"), agentYaml);

        // Act
        var count = await _loader.LoadAgentsAsync(_tempDirectory);

        // Assert
        Assert.Equal(1, count);
        Assert.True(_loader.IsInitialized);
        Assert.Equal(1, _loader.AgentCount);

        var agent = _loader.GetAgent("test-agent");
        Assert.NotNull(agent);
        Assert.Equal("test-agent", agent.Name);
        Assert.Equal("A test agent for unit testing", agent.Description);
        Assert.Equal("1.0.0", agent.Version);
        Assert.Contains("test agent", agent.Purpose.ToLower());
        Assert.Single(agent.McpTools);
        Assert.Single(agent.Skills);
    }

    [Fact]
    public async Task LoadAgentsAsync_WithMultipleAgentFiles_LoadsAllAgents()
    {
        // Arrange
        _tempDirectory = CreateTempDirectory();

        var agent1 = @"
name: agent-one
description: First test agent
version: ""1.0.0""
purpose: Testing agent loader with multiple files
mcp_tools:
  - name: tool1
    description: Tool 1
skills:
  - name: skill1
    description: Skill 1
";

        var agent2 = @"
name: agent-two
description: Second test agent
version: ""2.0.0""
purpose: Another test agent for validation
mcp_tools:
  - name: tool2
    description: Tool 2
skills:
  - name: skill2
    description: Skill 2
";

        File.WriteAllText(Path.Combine(_tempDirectory, "agent-one.yaml"), agent1);
        File.WriteAllText(Path.Combine(_tempDirectory, "agent-two.yml"), agent2);

        // Act
        var count = await _loader.LoadAgentsAsync(_tempDirectory);

        // Assert
        Assert.Equal(2, count);
        Assert.Equal(2, _loader.AgentCount);

        var agentOne = _loader.GetAgent("agent-one");
        Assert.NotNull(agentOne);
        Assert.Equal("First test agent", agentOne.Description);

        var agentTwo = _loader.GetAgent("agent-two");
        Assert.NotNull(agentTwo);
        Assert.Equal("Second test agent", agentTwo.Description);
    }

    [Fact]
    public async Task LoadAgentsAsync_WithFrontmatter_ParsesCorrectly()
    {
        // Arrange
        _tempDirectory = CreateTempDirectory();
        var agentYaml = @"---
name: frontmatter-agent
description: Agent with YAML frontmatter delimiters
version: ""1.0.0""
purpose: Testing frontmatter parsing functionality in the agent loader
mcp_tools:
  - name: fm_tool
    description: Frontmatter tool
skills:
  - name: fm_skill
    description: Frontmatter skill
---
";
        File.WriteAllText(Path.Combine(_tempDirectory, "frontmatter-agent.yaml"), agentYaml);

        // Act
        var count = await _loader.LoadAgentsAsync(_tempDirectory);

        // Assert
        Assert.Equal(1, count);
        var agent = _loader.GetAgent("frontmatter-agent");
        Assert.NotNull(agent);
        Assert.Equal("frontmatter-agent", agent.Name);
    }

    [Fact]
    public async Task LoadAgentsAsync_WithSchemaValidation_ValidatesAgainstSchema()
    {
        // Arrange
        _tempDirectory = CreateTempDirectory();

        // Create a valid schema file
        var schema = @"{
  ""$schema"": ""http://json-schema.org/draft-07/schema#"",
  ""type"": ""object"",
  ""required"": [""name"", ""description"", ""purpose"", ""mcp_tools"", ""skills""],
  ""properties"": {
    ""name"": { ""type"": ""string"" },
    ""description"": { ""type"": ""string"" },
    ""version"": { ""type"": ""string"" },
    ""purpose"": { ""type"": ""string"" },
    ""mcp_tools"": { ""type"": ""array"" },
    ""skills"": { ""type"": ""array"" }
  }
}";
        File.WriteAllText(Path.Combine(_tempDirectory, "agent-schema.json"), schema);

        var validAgent = @"
name: valid-agent
description: Valid agent for schema testing
version: ""1.0.0""
purpose: Testing schema validation in agent loader
mcp_tools:
  - name: tool
    description: A tool
skills:
  - name: skill
    description: A skill
";
        File.WriteAllText(Path.Combine(_tempDirectory, "valid-agent.yaml"), validAgent);

        // Act
        var count = await _loader.LoadAgentsAsync(_tempDirectory);

        // Assert
        Assert.Equal(1, count);
        var agent = _loader.GetAgent("valid-agent");
        Assert.NotNull(agent);
    }

    [Fact]
    public async Task LoadAgentsAsync_SkipsYamlLanguageServerComments()
    {
        // Arrange
        _tempDirectory = CreateTempDirectory();
        var agentYaml = @"# yaml-language-server: $schema=./agent-schema.json
name: comment-agent
description: Agent with yaml-language-server comment
version: ""1.0.0""
purpose: Testing that yaml-language-server comments are properly filtered out
mcp_tools:
  - name: tool
    description: Tool
skills:
  - name: skill
    description: Skill
";
        File.WriteAllText(Path.Combine(_tempDirectory, "comment-agent.yaml"), agentYaml);

        // Act
        var count = await _loader.LoadAgentsAsync(_tempDirectory);

        // Assert
        Assert.Equal(1, count);
        var agent = _loader.GetAgent("comment-agent");
        Assert.NotNull(agent);
    }

    [Fact]
    public void GetAgent_WithNullOrEmptyName_ReturnsNull()
    {
        // Act & Assert
        Assert.Null(_loader.GetAgent(null!));
        Assert.Null(_loader.GetAgent(string.Empty));
    }

    [Fact]
    public void GetAgent_WithNonExistentAgent_ReturnsNull()
    {
        // Act
        var agent = _loader.GetAgent("non-existent-agent");

        // Assert
        Assert.Null(agent);
    }

    [Fact]
    public async Task GetAgent_IsCaseInsensitive()
    {
        // Arrange
        _tempDirectory = CreateTempDirectory();
        var agentYaml = @"
name: case-test-agent
description: Testing case sensitivity
version: ""1.0.0""
purpose: Testing that agent lookups are case-insensitive
mcp_tools:
  - name: tool
    description: Tool
skills:
  - name: skill
    description: Skill
";
        File.WriteAllText(Path.Combine(_tempDirectory, "case-test.yaml"), agentYaml);
        await _loader.LoadAgentsAsync(_tempDirectory);

        // Act & Assert
        Assert.NotNull(_loader.GetAgent("case-test-agent"));
        Assert.NotNull(_loader.GetAgent("CASE-TEST-AGENT"));
        Assert.NotNull(_loader.GetAgent("Case-Test-Agent"));
    }

    [Fact]
    public async Task GetAllAgents_ReturnsAllLoadedAgents()
    {
        // Arrange
        _tempDirectory = CreateTempDirectory();

        for (int i = 1; i <= 3; i++)
        {
            var agentYaml = $@"
name: agent-{i}
description: Agent {i}
version: ""1.0.0""
purpose: Testing GetAllAgents with multiple agents
mcp_tools:
  - name: tool{i}
    description: Tool {i}
skills:
  - name: skill{i}
    description: Skill {i}
";
            File.WriteAllText(Path.Combine(_tempDirectory, $"agent-{i}.yaml"), agentYaml);
        }

        await _loader.LoadAgentsAsync(_tempDirectory);

        // Act
        var allAgents = _loader.GetAllAgents();

        // Assert
        Assert.Equal(3, allAgents.Count);
        Assert.Contains(allAgents, a => a.Name == "agent-1");
        Assert.Contains(allAgents, a => a.Name == "agent-2");
        Assert.Contains(allAgents, a => a.Name == "agent-3");
    }

    [Fact]
    public async Task GetAllAgents_ReturnsSortedByName()
    {
        // Arrange
        _tempDirectory = CreateTempDirectory();

        var names = new[] { "zebra-agent", "alpha-agent", "beta-agent" };
        foreach (var name in names)
        {
            var agentYaml = $@"
name: {name}
description: Agent {name}
version: ""1.0.0""
purpose: Testing sorted results
mcp_tools:
  - name: tool
    description: Tool
skills:
  - name: skill
    description: Skill
";
            File.WriteAllText(Path.Combine(_tempDirectory, $"{name}.yaml"), agentYaml);
        }

        await _loader.LoadAgentsAsync(_tempDirectory);

        // Act
        var allAgents = _loader.GetAllAgents();

        // Assert
        Assert.Equal(3, allAgents.Count);
        Assert.Equal("alpha-agent", allAgents[0].Name);
        Assert.Equal("beta-agent", allAgents[1].Name);
        Assert.Equal("zebra-agent", allAgents[2].Name);
    }

    [Fact]
    public async Task ReloadAsync_ClearsAndReloadsAgents()
    {
        // Arrange
        _tempDirectory = CreateTempDirectory();
        var agent1 = @"
name: original-agent
description: Original agent
version: ""1.0.0""
purpose: Testing reload functionality
mcp_tools:
  - name: tool
    description: Tool
skills:
  - name: skill
    description: Skill
";
        File.WriteAllText(Path.Combine(_tempDirectory, "original.yaml"), agent1);
        await _loader.LoadAgentsAsync(_tempDirectory);
        Assert.Equal(1, _loader.AgentCount);

        // Add a new agent file
        var agent2 = @"
name: new-agent
description: New agent added after initial load
version: ""1.0.0""
purpose: Testing reload with new agent
mcp_tools:
  - name: tool2
    description: Tool 2
skills:
  - name: skill2
    description: Skill 2
";
        File.WriteAllText(Path.Combine(_tempDirectory, "new.yaml"), agent2);

        // Act
        var count = await _loader.ReloadAsync(_tempDirectory);

        // Assert
        Assert.Equal(2, count);
        Assert.Equal(2, _loader.AgentCount);
        Assert.NotNull(_loader.GetAgent("original-agent"));
        Assert.NotNull(_loader.GetAgent("new-agent"));
    }

    [Fact]
    public async Task LoadAgentsAsync_WithCompleteAgentDefinition_LoadsAllProperties()
    {
        // Arrange
        _tempDirectory = CreateTempDirectory();
        var agentYaml = @"
name: complete-agent
description: A complete agent with all properties
version: ""2.1.0""
purpose: |
  This agent has all optional properties defined to test complete
  deserialization of the agent definition structure.
mcp_tools:
  - name: context7_resolve
    description: Resolve library IDs
    provider: context7
    required: true
    parameters:
      libraryName: ""{{library}}""
  - name: local_tool
    description: Local tool
    provider: cdocs
    required: false
skills:
  - name: research
    description: Research capabilities
    examples:
      - Example 1
      - Example 2
  - name: analysis
    description: Analysis capabilities
prompts:
  system: |
    You are a complete agent for testing.
  task_templates:
    template1: Template 1 content
    template2: Template 2 content
configuration:
  max_iterations: 20
  timeout_seconds: 300
  cache_results: false
metadata:
  author: test-author
  category: research
  tags:
    - testing
    - complete
";
        File.WriteAllText(Path.Combine(_tempDirectory, "complete.yaml"), agentYaml);

        // Act
        var count = await _loader.LoadAgentsAsync(_tempDirectory);

        // Assert
        Assert.Equal(1, count);
        var agent = _loader.GetAgent("complete-agent");
        Assert.NotNull(agent);

        // Verify basic properties
        Assert.Equal("complete-agent", agent.Name);
        Assert.Equal("A complete agent with all properties", agent.Description);
        Assert.Equal("2.1.0", agent.Version);

        // Verify MCP tools
        Assert.Equal(2, agent.McpTools.Count);
        var context7Tool = agent.McpTools[0];
        Assert.Equal("context7_resolve", context7Tool.Name);
        Assert.Equal("context7", context7Tool.Provider);
        Assert.True(context7Tool.Required);

        // Verify skills
        Assert.Equal(2, agent.Skills.Count);
        var researchSkill = agent.Skills[0];
        Assert.Equal("research", researchSkill.Name);
        Assert.Equal(2, researchSkill.Examples.Count);

        // Verify prompts
        Assert.NotNull(agent.Prompts);
        Assert.Contains("complete agent", agent.Prompts.System!.ToLower());
        Assert.Equal(2, agent.Prompts.TaskTemplates.Count);

        // Verify configuration
        Assert.NotNull(agent.Configuration);
        Assert.Equal(20, agent.Configuration.MaxIterations);
        Assert.Equal(300, agent.Configuration.TimeoutSeconds);
        Assert.False(agent.Configuration.CacheResults);

        // Verify metadata
        Assert.NotNull(agent.Metadata);
        Assert.Equal("test-author", agent.Metadata.Author);
        Assert.Equal("research", agent.Metadata.Category);
        Assert.Equal(2, agent.Metadata.Tags!.Count);
    }

    /// <summary>
    /// Creates a temporary directory for testing.
    /// </summary>
    private static string CreateTempDirectory()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), "AgentLoaderTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempPath);
        return tempPath;
    }
}
