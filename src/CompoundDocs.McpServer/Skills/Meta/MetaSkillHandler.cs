using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CompoundDocs.McpServer.Data.Repositories;
using CompoundDocs.McpServer.Models;
using CompoundDocs.McpServer.Session;
using CompoundDocs.McpServer.Tools;
using Microsoft.Extensions.Logging;

namespace CompoundDocs.McpServer.Skills.Meta;

/// <summary>
/// Handles meta skills for status, activate, deactivate, and help operations.
/// </summary>
public sealed class MetaSkillHandler : IMetaSkillHandler
{
    private readonly ISessionContext _sessionContext;
    private readonly IDocumentRepository _documentRepository;
    private readonly SkillLoader _skillLoader;
    private readonly ProjectActivationService _activationService;
    private readonly ILogger<MetaSkillHandler> _logger;

    /// <summary>
    /// Creates a new instance of MetaSkillHandler.
    /// </summary>
    public MetaSkillHandler(
        ISessionContext sessionContext,
        IDocumentRepository documentRepository,
        SkillLoader skillLoader,
        ProjectActivationService activationService,
        ILogger<MetaSkillHandler> logger)
    {
        _sessionContext = sessionContext ?? throw new ArgumentNullException(nameof(sessionContext));
        _documentRepository = documentRepository ?? throw new ArgumentNullException(nameof(documentRepository));
        _skillLoader = skillLoader ?? throw new ArgumentNullException(nameof(skillLoader));
        _activationService = activationService ?? throw new ArgumentNullException(nameof(activationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    #region HandleStatusAsync

    /// <inheritdoc />
    public async Task<ToolResponse<StatusResult>> HandleStatusAsync(
        StatusRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Handling status request: IncludeDetails={IncludeDetails}, IncludeRecent={IncludeRecent}",
            request.IncludeDetails, request.IncludeRecent);

        try
        {
            var result = new StatusResult
            {
                IsProjectActive = _sessionContext.IsProjectActive,
                ProjectName = _sessionContext.ProjectName,
                BranchName = _sessionContext.ActiveBranch,
                TenantKey = _sessionContext.TenantKey,
                ProjectPath = _sessionContext.ActiveProjectPath,
                SkillsCount = _skillLoader.SkillCount,
                ServerVersion = GetServerVersion()
            };

            // Get detailed statistics if requested and project is active
            if (request.IncludeDetails && _sessionContext.IsProjectActive)
            {
                var allDocuments = await _documentRepository.GetAllForTenantAsync(
                    _sessionContext.TenantKey!,
                    cancellationToken: cancellationToken);

                result.TotalDocuments = allDocuments.Count;

                // Count by document type
                result.DocTypeCounts = allDocuments
                    .GroupBy(d => d.DocType)
                    .Select(g => new TypeCount { Type = g.Key, Count = g.Count() })
                    .OrderByDescending(tc => tc.Count)
                    .ToList();

                // Count by promotion level
                result.PromotionLevelCounts = allDocuments
                    .GroupBy(d => d.PromotionLevel)
                    .Select(g => new LevelCount { Level = g.Key, Count = g.Count() })
                    .OrderByDescending(lc => lc.Count)
                    .ToList();

                // Calculate total chunks (approximate)
                var totalChunks = 0;
                foreach (var doc in allDocuments.Take(50)) // Limit to avoid performance issues
                {
                    var chunks = await _documentRepository.GetChunksAsync(doc.Id, cancellationToken);
                    totalChunks += chunks.Count;
                }
                result.TotalChunks = totalChunks;
            }

            // Get recent documents if requested
            if (request.IncludeRecent && _sessionContext.IsProjectActive)
            {
                var allDocuments = await _documentRepository.GetAllForTenantAsync(
                    _sessionContext.TenantKey!,
                    cancellationToken: cancellationToken);

                result.RecentDocuments = allDocuments
                    .OrderByDescending(d => d.LastModified)
                    .Take(Math.Clamp(request.RecentLimit, 1, 20))
                    .Select(d => new RecentDocument
                    {
                        FilePath = d.FilePath,
                        Title = d.Title,
                        DocType = d.DocType,
                        LastModified = d.LastModified
                    })
                    .ToList();
            }

            _logger.LogInformation("Status retrieved: ProjectActive={IsActive}, Documents={Count}",
                result.IsProjectActive, result.TotalDocuments);

            return ToolResponse<StatusResult>.Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during status retrieval");
            return ToolResponse<StatusResult>.Fail(ToolErrors.UnexpectedError(ex.Message));
        }
    }

    #endregion

    #region HandleActivateAsync

    /// <inheritdoc />
    public async Task<ToolResponse<ActivateResult>> HandleActivateAsync(
        ActivateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.ProjectPath))
        {
            return ToolResponse<ActivateResult>.Fail(ToolErrors.MissingParameter("project_path"));
        }

        _logger.LogInformation("Activating project: {ProjectPath}", request.ProjectPath);

        try
        {
            var activationResult = await _activationService.ActivateProjectAsync(
                request.ProjectPath,
                request.Branch,
                cancellationToken);

            if (!activationResult.IsSuccess)
            {
                _logger.LogWarning("Project activation failed: {Error}", activationResult.ErrorMessage);
                return ToolResponse<ActivateResult>.Fail(
                    ToolErrors.ProjectActivationFailed(activationResult.ErrorMessage ?? "Unknown error"));
            }

            _logger.LogInformation("Project activated: {ProjectName} on branch {Branch}",
                activationResult.ProjectName, activationResult.BranchName);

            return ToolResponse<ActivateResult>.Ok(new ActivateResult
            {
                Success = true,
                ProjectName = activationResult.ProjectName!,
                BranchName = activationResult.BranchName!,
                TenantKey = activationResult.TenantKey!,
                Message = $"Project '{activationResult.ProjectName}' activated on branch '{activationResult.BranchName}'"
            });
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Project activation cancelled");
            return ToolResponse<ActivateResult>.Fail(ToolErrors.OperationCancelled);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during project activation");
            return ToolResponse<ActivateResult>.Fail(ToolErrors.UnexpectedError(ex.Message));
        }
    }

    #endregion

    #region HandleDeactivateAsync

    /// <inheritdoc />
    public Task<ToolResponse<DeactivateResult>> HandleDeactivateAsync(
        DeactivateRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Handling deactivate request: Confirm={Confirm}", request.Confirm);

        try
        {
            var wasActive = _sessionContext.IsProjectActive;
            var previousProject = _sessionContext.ProjectName;
            var previousBranch = _sessionContext.ActiveBranch;

            if (wasActive)
            {
                _sessionContext.DeactivateProject();
                _logger.LogInformation("Project deactivated: {ProjectName}", previousProject);
            }
            else
            {
                _logger.LogDebug("No project was active to deactivate");
            }

            return Task.FromResult(ToolResponse<DeactivateResult>.Ok(new DeactivateResult
            {
                WasActive = wasActive,
                PreviousProjectName = previousProject,
                PreviousBranchName = previousBranch,
                Message = wasActive
                    ? $"Project '{previousProject}' has been deactivated"
                    : "No project was active"
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during project deactivation");
            return Task.FromResult(ToolResponse<DeactivateResult>.Fail(ToolErrors.UnexpectedError(ex.Message)));
        }
    }

    #endregion

    #region HandleHelpAsync

    /// <inheritdoc />
    public Task<ToolResponse<HelpResult>> HandleHelpAsync(
        HelpRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Handling help request: Category={Category}, SkillName={SkillName}",
            request.Category, request.SkillName);

        try
        {
            // Get specific skill details if requested
            if (!string.IsNullOrWhiteSpace(request.SkillName))
            {
                var skill = _skillLoader.GetSkill(request.SkillName);
                if (skill == null)
                {
                    return Task.FromResult(ToolResponse<HelpResult>.Fail(
                        ToolErrors.InvalidParameter("skill_name", $"Skill '{request.SkillName}' not found")));
                }

                return Task.FromResult(ToolResponse<HelpResult>.Ok(new HelpResult
                {
                    SkillDetails = MapSkillToDetails(skill)
                }));
            }

            // Get all skills grouped by category
            var allSkills = _skillLoader.GetAllSkills();
            var skillsByCategory = new Dictionary<string, List<SkillSummary>>();

            foreach (var skill in allSkills)
            {
                var category = skill.Metadata?.Category ?? "other";
                if (!string.IsNullOrWhiteSpace(request.Category) &&
                    !category.Equals(request.Category, StringComparison.OrdinalIgnoreCase) &&
                    !request.Category.Equals("all", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!skillsByCategory.TryGetValue(category, out var categorySkills))
                {
                    categorySkills = [];
                    skillsByCategory[category] = categorySkills;
                }

                categorySkills.Add(new SkillSummary
                {
                    Name = skill.Name,
                    ShortName = skill.ShortName,
                    Description = skill.Description
                });
            }

            return Task.FromResult(ToolResponse<HelpResult>.Ok(new HelpResult
            {
                SkillsByCategory = skillsByCategory,
                TotalSkills = allSkills.Count
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during help retrieval");
            return Task.FromResult(ToolResponse<HelpResult>.Fail(ToolErrors.UnexpectedError(ex.Message)));
        }
    }

    #endregion

    #region Helper Methods

    private static string GetServerVersion()
    {
        var assembly = typeof(MetaSkillHandler).Assembly;
        var version = assembly.GetName().Version;
        return version?.ToString() ?? "1.0.0";
    }

    private static SkillDetails MapSkillToDetails(SkillDefinition skill)
    {
        return new SkillDetails
        {
            Name = skill.Name,
            ShortName = skill.ShortName,
            Description = skill.Description,
            Version = skill.Version,
            Triggers = skill.Triggers,
            Parameters = skill.Parameters.Select(p => new ParameterInfo
            {
                Name = p.Name,
                Type = p.Type,
                Description = p.Description,
                Required = p.Required,
                Default = p.Default?.ToString()
            }).ToList(),
            Category = skill.Metadata?.Category,
            Tags = skill.Metadata?.Tags
        };
    }

    #endregion
}

#region Request/Result Types

/// <summary>
/// Request for status skill.
/// </summary>
public sealed class StatusRequest
{
    /// <summary>
    /// Whether to include detailed statistics.
    /// </summary>
    public bool IncludeDetails { get; init; }

    /// <summary>
    /// Whether to include recently indexed documents.
    /// </summary>
    public bool IncludeRecent { get; init; }

    /// <summary>
    /// Number of recent documents to include.
    /// </summary>
    public int RecentLimit { get; init; } = 5;
}

/// <summary>
/// Result of status skill.
/// </summary>
public sealed class StatusResult
{
    [JsonPropertyName("is_project_active")]
    public bool IsProjectActive { get; init; }

    [JsonPropertyName("project_name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ProjectName { get; init; }

    [JsonPropertyName("branch_name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? BranchName { get; init; }

    [JsonPropertyName("tenant_key")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TenantKey { get; init; }

    [JsonPropertyName("project_path")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ProjectPath { get; init; }

    [JsonPropertyName("total_documents")]
    public int TotalDocuments { get; set; }

    [JsonPropertyName("total_chunks")]
    public int TotalChunks { get; set; }

    [JsonPropertyName("doc_type_counts")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<TypeCount>? DocTypeCounts { get; set; }

    [JsonPropertyName("promotion_level_counts")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<LevelCount>? PromotionLevelCounts { get; set; }

    [JsonPropertyName("recent_documents")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<RecentDocument>? RecentDocuments { get; set; }

    [JsonPropertyName("skills_count")]
    public int SkillsCount { get; init; }

    [JsonPropertyName("server_version")]
    public string ServerVersion { get; init; } = "1.0.0";
}

/// <summary>
/// Document type count.
/// </summary>
public sealed class TypeCount
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    [JsonPropertyName("count")]
    public int Count { get; init; }
}

/// <summary>
/// Promotion level count.
/// </summary>
public sealed class LevelCount
{
    [JsonPropertyName("level")]
    public string Level { get; init; } = string.Empty;

    [JsonPropertyName("count")]
    public int Count { get; init; }
}

/// <summary>
/// Recently indexed document information.
/// </summary>
public sealed class RecentDocument
{
    [JsonPropertyName("file_path")]
    public string FilePath { get; init; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;

    [JsonPropertyName("doc_type")]
    public string DocType { get; init; } = string.Empty;

    [JsonPropertyName("last_modified")]
    public DateTimeOffset LastModified { get; init; }
}

/// <summary>
/// Request for activate skill.
/// </summary>
public sealed class ActivateRequest
{
    /// <summary>
    /// The absolute path to the project root.
    /// </summary>
    public string ProjectPath { get; init; } = string.Empty;

    /// <summary>
    /// Optional branch name override.
    /// </summary>
    public string? Branch { get; init; }
}

/// <summary>
/// Result of activate skill.
/// </summary>
public sealed class ActivateResult
{
    [JsonPropertyName("success")]
    public bool Success { get; init; }

    [JsonPropertyName("project_name")]
    public string ProjectName { get; init; } = string.Empty;

    [JsonPropertyName("branch_name")]
    public string BranchName { get; init; } = string.Empty;

    [JsonPropertyName("tenant_key")]
    public string TenantKey { get; init; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;
}

/// <summary>
/// Request for deactivate skill.
/// </summary>
public sealed class DeactivateRequest
{
    /// <summary>
    /// Confirm deactivation.
    /// </summary>
    public bool Confirm { get; init; } = true;
}

/// <summary>
/// Result of deactivate skill.
/// </summary>
public sealed class DeactivateResult
{
    [JsonPropertyName("was_active")]
    public bool WasActive { get; init; }

    [JsonPropertyName("previous_project_name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PreviousProjectName { get; init; }

    [JsonPropertyName("previous_branch_name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PreviousBranchName { get; init; }

    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;
}

/// <summary>
/// Request for help skill.
/// </summary>
public sealed class HelpRequest
{
    /// <summary>
    /// Filter by category (capture, query, meta, utility).
    /// </summary>
    public string? Category { get; init; }

    /// <summary>
    /// Get detailed help for a specific skill.
    /// </summary>
    public string? SkillName { get; init; }
}

/// <summary>
/// Result of help skill.
/// </summary>
public sealed class HelpResult
{
    [JsonPropertyName("skill_details")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SkillDetails? SkillDetails { get; init; }

    [JsonPropertyName("skills_by_category")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, List<SkillSummary>>? SkillsByCategory { get; init; }

    [JsonPropertyName("total_skills")]
    public int TotalSkills { get; init; }
}

/// <summary>
/// Detailed skill information.
/// </summary>
public sealed class SkillDetails
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("short_name")]
    public string ShortName { get; init; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; init; } = "1.0.0";

    [JsonPropertyName("triggers")]
    public List<string> Triggers { get; init; } = [];

    [JsonPropertyName("parameters")]
    public List<ParameterInfo> Parameters { get; init; } = [];

    [JsonPropertyName("category")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Category { get; init; }

    [JsonPropertyName("tags")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Tags { get; init; }
}

/// <summary>
/// Parameter information for help display.
/// </summary>
public sealed class ParameterInfo
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; init; } = "string";

    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;

    [JsonPropertyName("required")]
    public bool Required { get; init; }

    [JsonPropertyName("default")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Default { get; init; }
}

/// <summary>
/// Brief skill summary for listing.
/// </summary>
public sealed class SkillSummary
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("short_name")]
    public string ShortName { get; init; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;
}

#endregion
