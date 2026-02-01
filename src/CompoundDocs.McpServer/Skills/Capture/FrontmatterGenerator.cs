using System.Text;
using CompoundDocs.McpServer.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace CompoundDocs.McpServer.Skills.Capture;

/// <summary>
/// Generates YAML frontmatter for compound documents.
/// Handles standard fields and doc-type specific fields.
/// </summary>
public sealed class FrontmatterGenerator
{
    private readonly ISerializer _serializer;

    /// <summary>
    /// Creates a new instance of FrontmatterGenerator.
    /// </summary>
    public FrontmatterGenerator()
    {
        _serializer = new SerializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
            .Build();
    }

    /// <summary>
    /// Generates YAML frontmatter for a capture request.
    /// </summary>
    /// <param name="request">The capture request.</param>
    /// <returns>The YAML frontmatter string including delimiters.</returns>
    public string Generate(CaptureRequest request)
    {
        var frontmatter = new Dictionary<string, object?>
        {
            ["doc_type"] = request.DocType.ToLowerInvariant(),
            ["title"] = request.Title,
            ["created_at"] = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            ["last_modified"] = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
        };

        // Add promotion level if specified
        if (!string.IsNullOrWhiteSpace(request.PromotionLevel))
        {
            frontmatter["promotion_level"] = request.PromotionLevel.ToLowerInvariant();
        }
        else
        {
            frontmatter["promotion_level"] = PromotionLevels.Standard;
        }

        // Add tags if specified
        if (request.Tags?.Count > 0)
        {
            frontmatter["tags"] = request.Tags;
        }

        // Add doc-type specific fields
        AddDocTypeSpecificFields(frontmatter, request);

        // Add any additional metadata
        if (request.Metadata != null)
        {
            foreach (var (key, value) in request.Metadata)
            {
                // Don't override standard fields
                if (!frontmatter.ContainsKey(key))
                {
                    frontmatter[key] = value;
                }
            }
        }

        return FormatFrontmatter(frontmatter);
    }

    /// <summary>
    /// Generates frontmatter from a dictionary of values.
    /// </summary>
    /// <param name="values">The frontmatter values.</param>
    /// <returns>The YAML frontmatter string including delimiters.</returns>
    public string Generate(Dictionary<string, object?> values)
    {
        // Ensure required fields have defaults
        if (!values.ContainsKey("created_at"))
        {
            values["created_at"] = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
        }

        if (!values.ContainsKey("last_modified"))
        {
            values["last_modified"] = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
        }

        return FormatFrontmatter(values);
    }

    /// <summary>
    /// Gets the required frontmatter fields for a doc type.
    /// </summary>
    /// <param name="docType">The document type.</param>
    /// <returns>List of required field names.</returns>
    public IReadOnlyList<string> GetRequiredFields(string docType)
    {
        var standard = new List<string> { "doc_type", "title" };

        return docType.ToLowerInvariant() switch
        {
            DocumentTypes.Problem => [.. standard, "severity"],
            DocumentTypes.Insight => standard,
            DocumentTypes.Codebase => [.. standard, "module"],
            DocumentTypes.Tool => standard,
            DocumentTypes.Style => [.. standard, "language"],
            _ => standard
        };
    }

    /// <summary>
    /// Gets the optional frontmatter fields for a doc type.
    /// </summary>
    /// <param name="docType">The document type.</param>
    /// <returns>List of optional field names.</returns>
    public IReadOnlyList<string> GetOptionalFields(string docType)
    {
        var standard = new List<string>
        {
            "created_at",
            "last_modified",
            "tags",
            "promotion_level",
            "links"
        };

        return docType.ToLowerInvariant() switch
        {
            DocumentTypes.Problem => [.. standard, "status", "root_cause", "resolved_at"],
            DocumentTypes.Insight => [.. standard, "discovery_context", "confidence", "applications"],
            DocumentTypes.Codebase => [.. standard, "components", "patterns", "dependencies"],
            DocumentTypes.Tool => [.. standard, "version", "repository", "documentation_url"],
            DocumentTypes.Style => [.. standard, "scope", "enforcement"],
            _ => standard
        };
    }

    /// <summary>
    /// Validates frontmatter values against doc type requirements.
    /// </summary>
    /// <param name="docType">The document type.</param>
    /// <param name="values">The frontmatter values to validate.</param>
    /// <returns>Validation result.</returns>
    public FrontmatterValidationResult Validate(string docType, Dictionary<string, object?> values)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        // Check required fields
        var requiredFields = GetRequiredFields(docType);
        foreach (var field in requiredFields)
        {
            if (!values.TryGetValue(field, out var value) || value == null ||
                (value is string str && string.IsNullOrWhiteSpace(str)))
            {
                errors.Add($"Missing required field: {field}");
            }
        }

        // Check doc_type matches
        if (values.TryGetValue("doc_type", out var docTypeValue))
        {
            var actualDocType = docTypeValue?.ToString()?.ToLowerInvariant();
            if (actualDocType != docType.ToLowerInvariant())
            {
                errors.Add($"doc_type mismatch: expected '{docType}', got '{actualDocType}'");
            }
        }

        // Validate promotion level if specified
        if (values.TryGetValue("promotion_level", out var promotionValue) &&
            promotionValue is string promotionLevel &&
            !PromotionLevels.IsValid(promotionLevel))
        {
            warnings.Add($"Invalid promotion_level '{promotionLevel}', defaulting to 'standard'");
        }

        return new FrontmatterValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors,
            Warnings = warnings
        };
    }

    private void AddDocTypeSpecificFields(Dictionary<string, object?> frontmatter, CaptureRequest request)
    {
        switch (request.DocType.ToLowerInvariant())
        {
            case DocumentTypes.Problem:
                frontmatter["severity"] = request.Metadata?.GetValueOrDefault("severity") ?? "medium";
                frontmatter["status"] = request.Metadata?.GetValueOrDefault("status") ?? "open";
                break;

            case DocumentTypes.Insight:
                if (request.Metadata?.TryGetValue("discovery_context", out var context) == true)
                {
                    frontmatter["discovery_context"] = context;
                }
                if (request.Metadata?.TryGetValue("confidence", out var confidence) == true)
                {
                    frontmatter["confidence"] = confidence;
                }
                break;

            case DocumentTypes.Codebase:
                if (request.Metadata?.TryGetValue("module", out var module) == true)
                {
                    frontmatter["module"] = module;
                }
                if (request.Metadata?.TryGetValue("components", out var components) == true)
                {
                    frontmatter["components"] = components;
                }
                break;

            case DocumentTypes.Tool:
                if (request.Metadata?.TryGetValue("version", out var version) == true)
                {
                    frontmatter["version"] = version;
                }
                if (request.Metadata?.TryGetValue("repository", out var repo) == true)
                {
                    frontmatter["repository"] = repo;
                }
                break;

            case DocumentTypes.Style:
                frontmatter["language"] = request.Metadata?.GetValueOrDefault("language") ?? "general";
                if (request.Metadata?.TryGetValue("scope", out var scope) == true)
                {
                    frontmatter["scope"] = scope;
                }
                break;
        }
    }

    private string FormatFrontmatter(Dictionary<string, object?> values)
    {
        var yaml = _serializer.Serialize(values);
        var sb = new StringBuilder();
        sb.AppendLine("---");
        sb.Append(yaml.TrimEnd());
        sb.AppendLine();
        sb.AppendLine("---");
        return sb.ToString();
    }
}

/// <summary>
/// Result of frontmatter validation.
/// </summary>
public sealed class FrontmatterValidationResult
{
    /// <summary>
    /// Whether the frontmatter is valid.
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// Validation errors.
    /// </summary>
    public IReadOnlyList<string> Errors { get; init; } = [];

    /// <summary>
    /// Validation warnings.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];
}
