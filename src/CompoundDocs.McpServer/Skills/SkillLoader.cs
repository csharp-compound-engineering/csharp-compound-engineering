using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using NJsonSchema;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace CompoundDocs.McpServer.Skills;

/// <summary>
/// Loads and parses skill YAML files with schema validation and caching.
/// </summary>
public sealed class SkillLoader : IDisposable
{
    private readonly ILogger<SkillLoader> _logger;
    private readonly IDeserializer _yamlDeserializer;
    private readonly ConcurrentDictionary<string, SkillDefinition> _skillCache = new();
    private readonly ConcurrentDictionary<string, List<SkillDefinition>> _triggerIndex = new(StringComparer.OrdinalIgnoreCase);
    private JsonSchema? _schema;
    private bool _isInitialized;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    /// <summary>
    /// Default skills directory name.
    /// </summary>
    public const string DefaultSkillsDirectory = "skills";

    /// <summary>
    /// Default schema file name.
    /// </summary>
    public const string SchemaFileName = "skill-schema.json";

    /// <summary>
    /// Creates a new instance of SkillLoader.
    /// </summary>
    public SkillLoader(ILogger<SkillLoader> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _yamlDeserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
    }

    /// <summary>
    /// Loads all skills from the specified directory.
    /// </summary>
    /// <param name="skillsDirectory">Path to the skills directory.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of skills successfully loaded.</returns>
    public async Task<int> LoadSkillsAsync(string skillsDirectory, CancellationToken cancellationToken = default)
    {
        await _initLock.WaitAsync(cancellationToken);
        try
        {
            if (!Directory.Exists(skillsDirectory))
            {
                _logger.LogWarning("Skills directory not found: {Directory}", skillsDirectory);
                return 0;
            }

            // Load schema for validation
            await LoadSchemaAsync(skillsDirectory, cancellationToken);

            // Clear existing cache
            _skillCache.Clear();
            _triggerIndex.Clear();

            // Find all YAML skill files
            var skillFiles = Directory.GetFiles(skillsDirectory, "*.yaml", SearchOption.TopDirectoryOnly)
                .Concat(Directory.GetFiles(skillsDirectory, "*.yml", SearchOption.TopDirectoryOnly))
                .Where(f => !Path.GetFileName(f).Equals(SchemaFileName, StringComparison.OrdinalIgnoreCase));

            var loadedCount = 0;
            foreach (var filePath in skillFiles)
            {
                try
                {
                    var skill = await LoadSkillFileAsync(filePath, cancellationToken);
                    if (skill != null)
                    {
                        CacheSkill(skill);
                        loadedCount++;
                        _logger.LogDebug("Loaded skill: {SkillName} from {FilePath}", skill.Name, filePath);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to load skill from {FilePath}", filePath);
                }
            }

            _isInitialized = true;
            _logger.LogInformation("Loaded {Count} skills from {Directory}", loadedCount, skillsDirectory);
            return loadedCount;
        }
        finally
        {
            _initLock.Release();
        }
    }

    /// <summary>
    /// Loads a single skill file.
    /// </summary>
    private async Task<SkillDefinition?> LoadSkillFileAsync(string filePath, CancellationToken cancellationToken)
    {
        var content = await File.ReadAllTextAsync(filePath, cancellationToken);

        // Skip YAML frontmatter delimiter if present
        if (content.StartsWith("---"))
        {
            var endIndex = content.IndexOf("\n---", 3, StringComparison.Ordinal);
            if (endIndex > 0)
            {
                // Extract YAML content between delimiters
                content = content.Substring(4, endIndex - 4);
            }
        }

        // Validate against schema if available
        if (_schema != null)
        {
            var validationResult = await ValidateYamlAgainstSchemaAsync(content, filePath);
            if (!validationResult.IsValid)
            {
                _logger.LogWarning(
                    "Skill file {FilePath} failed schema validation: {Errors}",
                    filePath,
                    string.Join(", ", validationResult.Errors));
                // Continue loading despite validation errors to be lenient
            }
        }

        var skill = _yamlDeserializer.Deserialize<SkillDefinition>(content);
        if (string.IsNullOrEmpty(skill?.Name))
        {
            _logger.LogWarning("Skill file {FilePath} has no name defined", filePath);
            return null;
        }

        // Ensure name has proper prefix
        if (!skill.Name.StartsWith("/cdocs:", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "Skill {SkillName} in {FilePath} does not have /cdocs: prefix",
                skill.Name,
                filePath);
        }

        return skill;
    }

    /// <summary>
    /// Loads the JSON schema for validation.
    /// </summary>
    private async Task LoadSchemaAsync(string skillsDirectory, CancellationToken cancellationToken)
    {
        var schemaPath = Path.Combine(skillsDirectory, SchemaFileName);
        if (!File.Exists(schemaPath))
        {
            _logger.LogDebug("Schema file not found at {Path}, skipping validation", schemaPath);
            return;
        }

        try
        {
            var schemaContent = await File.ReadAllTextAsync(schemaPath, cancellationToken);
            _schema = await JsonSchema.FromJsonAsync(schemaContent, cancellationToken);
            _logger.LogDebug("Loaded skill schema from {Path}", schemaPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load skill schema from {Path}", schemaPath);
        }
    }

    /// <summary>
    /// Validates YAML content against the JSON schema.
    /// </summary>
    private async Task<SkillValidationResult> ValidateYamlAgainstSchemaAsync(string yamlContent, string filePath)
    {
        if (_schema == null)
        {
            return SkillValidationResult.Valid();
        }

        try
        {
            // Convert YAML to JSON for schema validation
            var deserializer = new DeserializerBuilder().Build();
            var yamlObject = deserializer.Deserialize<object>(yamlContent);
            var jsonContent = JsonSerializer.Serialize(yamlObject);

            var errors = _schema.Validate(jsonContent);
            if (errors.Count > 0)
            {
                return SkillValidationResult.Invalid(
                    errors.Select(e => $"{e.Path}: {e.Kind}"));
            }

            return SkillValidationResult.Valid();
        }
        catch (Exception ex)
        {
            return SkillValidationResult.Invalid($"Validation error: {ex.Message}");
        }
    }

    /// <summary>
    /// Caches a skill and builds trigger index.
    /// </summary>
    private void CacheSkill(SkillDefinition skill)
    {
        // Cache by name (both with and without prefix)
        _skillCache[skill.Name] = skill;
        _skillCache[skill.ShortName] = skill;

        // Build trigger index
        foreach (var trigger in skill.Triggers)
        {
            var normalizedTrigger = NormalizeTrigger(trigger);
            _triggerIndex.AddOrUpdate(
                normalizedTrigger,
                _ => [skill],
                (_, list) =>
                {
                    if (!list.Contains(skill))
                    {
                        list.Add(skill);
                    }
                    return list;
                });
        }
    }

    /// <summary>
    /// Gets a skill by name.
    /// </summary>
    /// <param name="name">Skill name (with or without /cdocs: prefix).</param>
    /// <returns>The skill definition, or null if not found.</returns>
    public SkillDefinition? GetSkill(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        // Try exact match first
        if (_skillCache.TryGetValue(name, out var skill))
        {
            return skill;
        }

        // Try with/without prefix
        var alternativeName = name.StartsWith("/cdocs:", StringComparison.OrdinalIgnoreCase)
            ? name[7..]
            : $"/cdocs:{name}";

        _skillCache.TryGetValue(alternativeName, out skill);
        return skill;
    }

    /// <summary>
    /// Gets all loaded skills.
    /// </summary>
    public IReadOnlyList<SkillDefinition> GetAllSkills()
    {
        // Return distinct skills (avoid duplicates from prefix/non-prefix keys)
        return _skillCache.Values
            .DistinctBy(s => s.Name)
            .OrderBy(s => s.Name)
            .ToList();
    }

    /// <summary>
    /// Finds skills matching a trigger phrase.
    /// </summary>
    /// <param name="phrase">The trigger phrase to match.</param>
    /// <returns>List of matching skills.</returns>
    public IReadOnlyList<SkillDefinition> FindByTrigger(string phrase)
    {
        if (string.IsNullOrEmpty(phrase))
        {
            return [];
        }

        var normalizedPhrase = NormalizeTrigger(phrase);

        // Exact match
        if (_triggerIndex.TryGetValue(normalizedPhrase, out var exactMatches))
        {
            return exactMatches;
        }

        // Partial match (phrase contains trigger or trigger contains phrase)
        var partialMatches = _triggerIndex
            .Where(kvp =>
                normalizedPhrase.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase) ||
                kvp.Key.Contains(normalizedPhrase, StringComparison.OrdinalIgnoreCase))
            .SelectMany(kvp => kvp.Value)
            .Distinct()
            .ToList();

        return partialMatches;
    }

    /// <summary>
    /// Normalizes a trigger phrase for matching.
    /// </summary>
    private static string NormalizeTrigger(string trigger)
    {
        return trigger.Trim().ToLowerInvariant();
    }

    /// <summary>
    /// Reloads all skills from the directory.
    /// </summary>
    public async Task<int> ReloadAsync(string skillsDirectory, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Reloading skills from {Directory}", skillsDirectory);
        return await LoadSkillsAsync(skillsDirectory, cancellationToken);
    }

    /// <summary>
    /// Whether the loader has been initialized.
    /// </summary>
    public bool IsInitialized => _isInitialized;

    /// <summary>
    /// Number of loaded skills.
    /// </summary>
    public int SkillCount => GetAllSkills().Count;

    /// <inheritdoc />
    public void Dispose()
    {
        _initLock.Dispose();
    }
}
