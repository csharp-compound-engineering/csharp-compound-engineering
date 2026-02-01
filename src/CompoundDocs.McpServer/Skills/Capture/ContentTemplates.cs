using CompoundDocs.McpServer.Models;

namespace CompoundDocs.McpServer.Skills.Capture;

/// <summary>
/// Provides markdown templates for each document type.
/// Templates define the expected structure and sections for captured documents.
/// </summary>
public static class ContentTemplates
{
    /// <summary>
    /// Gets the template for the specified document type.
    /// </summary>
    /// <param name="docType">The document type.</param>
    /// <returns>The markdown template, or null if not found.</returns>
    public static string? GetTemplate(string docType)
    {
        return docType.ToLowerInvariant() switch
        {
            DocumentTypes.Problem => ProblemTemplate,
            DocumentTypes.Insight => InsightTemplate,
            DocumentTypes.Codebase => CodebaseTemplate,
            DocumentTypes.Tool => ToolTemplate,
            DocumentTypes.Style => StyleTemplate,
            _ => null
        };
    }

    /// <summary>
    /// Gets the required sections for a document type.
    /// </summary>
    /// <param name="docType">The document type.</param>
    /// <returns>List of required section headings.</returns>
    public static IReadOnlyList<string> GetRequiredSections(string docType)
    {
        return docType.ToLowerInvariant() switch
        {
            DocumentTypes.Problem => ProblemRequiredSections,
            DocumentTypes.Insight => InsightRequiredSections,
            DocumentTypes.Codebase => CodebaseRequiredSections,
            DocumentTypes.Tool => ToolRequiredSections,
            DocumentTypes.Style => StyleRequiredSections,
            _ => []
        };
    }

    /// <summary>
    /// Gets the optional sections for a document type.
    /// </summary>
    /// <param name="docType">The document type.</param>
    /// <returns>List of optional section headings.</returns>
    public static IReadOnlyList<string> GetOptionalSections(string docType)
    {
        return docType.ToLowerInvariant() switch
        {
            DocumentTypes.Problem => ProblemOptionalSections,
            DocumentTypes.Insight => InsightOptionalSections,
            DocumentTypes.Codebase => CodebaseOptionalSections,
            DocumentTypes.Tool => ToolOptionalSections,
            DocumentTypes.Style => StyleOptionalSections,
            _ => []
        };
    }

    /// <summary>
    /// Validates that content contains the required sections for a document type.
    /// </summary>
    /// <param name="docType">The document type.</param>
    /// <param name="content">The markdown content to validate.</param>
    /// <returns>Validation result with missing sections.</returns>
    public static TemplateValidationResult ValidateSections(string docType, string content)
    {
        var requiredSections = GetRequiredSections(docType);
        var missingSections = new List<string>();
        var warnings = new List<string>();

        foreach (var section in requiredSections)
        {
            // Check for section header (## Section Name)
            var pattern = $"## {section}";
            if (!content.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                missingSections.Add(section);
            }
        }

        // Check for optional sections and add warnings if commonly expected ones are missing
        var optionalSections = GetOptionalSections(docType);
        foreach (var section in optionalSections)
        {
            var pattern = $"## {section}";
            if (!content.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                // Only warn about particularly important optional sections
                if (IsImportantOptionalSection(docType, section))
                {
                    warnings.Add($"Consider adding section: {section}");
                }
            }
        }

        return new TemplateValidationResult
        {
            IsValid = missingSections.Count == 0,
            MissingSections = missingSections,
            Warnings = warnings
        };
    }

    private static bool IsImportantOptionalSection(string docType, string section)
    {
        return (docType, section) switch
        {
            (DocumentTypes.Problem, "Prevention") => true,
            (DocumentTypes.Insight, "Limitations") => true,
            (DocumentTypes.Tool, "Troubleshooting") => true,
            _ => false
        };
    }

    #region Problem Template

    private static readonly IReadOnlyList<string> ProblemRequiredSections =
    [
        "TL;DR",
        "Context",
        "What Went Wrong",
        "Root Cause",
        "Solution"
    ];

    private static readonly IReadOnlyList<string> ProblemOptionalSections =
    [
        "Prevention",
        "Related Issues",
        "Timeline",
        "Impact"
    ];

    /// <summary>
    /// Template for problem documentation.
    /// Captures issues, their root causes, and solutions.
    /// </summary>
    public const string ProblemTemplate = """
        ## TL;DR

        <!-- One-sentence summary of the problem and solution -->

        ## Context

        <!-- Background information: what were you trying to do? -->

        ## What Went Wrong

        <!-- Describe the symptoms and observable behavior -->

        ## Root Cause

        <!-- The underlying cause of the problem -->

        ## Solution

        <!-- How was this resolved? Include code snippets if applicable -->

        ## Prevention

        <!-- How can this be prevented in the future? -->
        """;

    #endregion

    #region Insight Template

    private static readonly IReadOnlyList<string> InsightRequiredSections =
    [
        "TL;DR",
        "Discovery Context",
        "Key Insight",
        "Evidence"
    ];

    private static readonly IReadOnlyList<string> InsightOptionalSections =
    [
        "Applications",
        "Limitations",
        "Related Insights",
        "Further Research"
    ];

    /// <summary>
    /// Template for insight documentation.
    /// Captures learnings and discoveries from project work.
    /// </summary>
    public const string InsightTemplate = """
        ## TL;DR

        <!-- One-sentence summary of the insight -->

        ## Discovery Context

        <!-- How was this insight discovered? What led to it? -->

        ## Key Insight

        <!-- The main takeaway or learning -->

        ## Evidence

        <!-- Supporting evidence, examples, or data -->

        ## Applications

        <!-- Where and how can this insight be applied? -->

        ## Limitations

        <!-- When does this insight NOT apply? Caveats? -->
        """;

    #endregion

    #region Codebase Template

    private static readonly IReadOnlyList<string> CodebaseRequiredSections =
    [
        "Purpose",
        "Key Components",
        "Design Decisions"
    ];

    private static readonly IReadOnlyList<string> CodebaseOptionalSections =
    [
        "Dependencies",
        "Usage Examples",
        "Configuration",
        "Testing Strategy",
        "Performance Considerations"
    ];

    /// <summary>
    /// Template for codebase documentation.
    /// Captures architectural and implementation details.
    /// </summary>
    public const string CodebaseTemplate = """
        ## Purpose

        <!-- What does this code/module do? Why does it exist? -->

        ## Key Components

        <!-- Main classes, functions, or modules -->

        ## Design Decisions

        <!-- Important architectural choices and their rationale -->

        ## Dependencies

        <!-- External and internal dependencies -->

        ## Usage Examples

        ```csharp
        // Example usage code
        ```
        """;

    #endregion

    #region Tool Template

    private static readonly IReadOnlyList<string> ToolRequiredSections =
    [
        "Purpose",
        "Setup",
        "Usage"
    ];

    private static readonly IReadOnlyList<string> ToolOptionalSections =
    [
        "Configuration",
        "Troubleshooting",
        "Best Practices",
        "Alternatives"
    ];

    /// <summary>
    /// Template for tool documentation.
    /// Captures how to set up and use development tools.
    /// </summary>
    public const string ToolTemplate = """
        ## Purpose

        <!-- What is this tool for? What problem does it solve? -->

        ## Setup

        <!-- Installation and configuration steps -->

        ```bash
        # Installation commands
        ```

        ## Usage

        <!-- How to use the tool -->

        ```bash
        # Example commands
        ```

        ## Configuration

        <!-- Configuration options and settings -->

        ## Troubleshooting

        <!-- Common issues and solutions -->
        """;

    #endregion

    #region Style Template

    private static readonly IReadOnlyList<string> StyleRequiredSections =
    [
        "Purpose",
        "Examples"
    ];

    private static readonly IReadOnlyList<string> StyleOptionalSections =
    [
        "Rationale",
        "Exceptions",
        "Enforcement",
        "Related Guidelines"
    ];

    /// <summary>
    /// Template for style guide documentation.
    /// Captures coding standards and conventions.
    /// </summary>
    public const string StyleTemplate = """
        ## Purpose

        <!-- What aspect of code style does this cover? Why is it important? -->

        ## Examples

        ### Do

        ```csharp
        // Good example
        ```

        ### Don't

        ```csharp
        // Bad example
        ```

        ## Rationale

        <!-- Why these conventions? -->

        ## Exceptions

        <!-- When is it acceptable to deviate from this guideline? -->
        """;

    #endregion
}

/// <summary>
/// Result of template section validation.
/// </summary>
public sealed class TemplateValidationResult
{
    /// <summary>
    /// Whether all required sections are present.
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// List of missing required sections.
    /// </summary>
    public IReadOnlyList<string> MissingSections { get; init; } = [];

    /// <summary>
    /// Validation warnings.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];
}
