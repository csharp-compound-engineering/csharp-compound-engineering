using System.Diagnostics;
using CompoundDocs.McpServer.Data.Repositories;
using Microsoft.Extensions.Logging;

namespace CompoundDocs.McpServer.Session;

/// <summary>
/// Service for managing project activation workflow.
/// Handles validation, git branch detection, database registration, and file watcher coordination.
/// </summary>
public sealed class ProjectActivationService
{
    private readonly ISessionContext _sessionContext;
    private readonly IRepoPathRepository? _repoPathRepository;
    private readonly ILogger<ProjectActivationService> _logger;

    /// <summary>
    /// The default branch name to use when git detection fails.
    /// </summary>
    public const string DefaultBranchName = "main";

    /// <summary>
    /// The timeout for git commands in milliseconds.
    /// </summary>
    private const int GitCommandTimeoutMs = 5000;

    /// <summary>
    /// Creates a new instance of ProjectActivationService.
    /// </summary>
    /// <param name="sessionContext">The session context to update.</param>
    /// <param name="logger">The logger for diagnostic output.</param>
    /// <param name="repoPathRepository">Optional repository for repo paths.</param>
    public ProjectActivationService(
        ISessionContext sessionContext,
        ILogger<ProjectActivationService> logger,
        IRepoPathRepository? repoPathRepository = null)
    {
        _sessionContext = sessionContext ?? throw new ArgumentNullException(nameof(sessionContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _repoPathRepository = repoPathRepository;
    }

    /// <summary>
    /// Activates a project for the current session.
    /// </summary>
    /// <param name="projectPath">The absolute path to the project root.</param>
    /// <param name="branchOverride">Optional branch name override. If null, git detection is used.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The activation result containing tenant information.</returns>
    public async Task<ProjectActivationResult> ActivateProjectAsync(
        string projectPath,
        string? branchOverride = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(projectPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectPath, nameof(projectPath));

        _logger.LogInformation(
            "Activating project at path: {ProjectPath}",
            projectPath);

        // Validate project path exists
        if (!Directory.Exists(projectPath))
        {
            _logger.LogWarning(
                "Project path does not exist: {ProjectPath}",
                projectPath);

            return ProjectActivationResult.Failed(
                $"Project path does not exist: {projectPath}");
        }

        // Deactivate any currently active project
        if (_sessionContext.IsProjectActive)
        {
            _logger.LogInformation(
                "Deactivating current project before activating new one: {CurrentProject}",
                _sessionContext.ProjectName);

            _sessionContext.DeactivateProject();
        }

        // Detect or use provided branch name
        var branchName = branchOverride ?? await DetectGitBranchAsync(projectPath, cancellationToken);

        _logger.LogDebug(
            "Using branch name: {BranchName} (override: {IsOverride})",
            branchName,
            branchOverride != null);

        // Activate the project in session context
        _sessionContext.ActivateProject(projectPath, branchName);

        // Register repo path in database (will be implemented when database layer is available)
        await RegisterRepoPathAsync(projectPath, cancellationToken);

        _logger.LogInformation(
            "Project activated successfully: {ProjectName} on branch {BranchName}",
            _sessionContext.ProjectName,
            branchName);

        return ProjectActivationResult.Succeeded(
            _sessionContext.ProjectName!,
            branchName,
            _sessionContext.PathHash!,
            _sessionContext.TenantKey!);
    }

    /// <summary>
    /// Deactivates the current project.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task DeactivateProjectAsync(CancellationToken cancellationToken = default)
    {
        if (!_sessionContext.IsProjectActive)
        {
            _logger.LogDebug("No active project to deactivate");
            return Task.CompletedTask;
        }

        _logger.LogInformation(
            "Deactivating project: {ProjectName}",
            _sessionContext.ProjectName);

        _sessionContext.DeactivateProject();

        return Task.CompletedTask;
    }

    /// <summary>
    /// Detects the current git branch for the given path.
    /// Falls back to DefaultBranchName if detection fails.
    /// </summary>
    /// <param name="projectPath">The path to check for git branch.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The detected branch name, or DefaultBranchName if detection fails.</returns>
    public async Task<string> DetectGitBranchAsync(
        string projectPath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var branchName = await ExecuteGitCommandAsync(
                projectPath,
                "branch --show-current",
                cancellationToken);

            if (!string.IsNullOrWhiteSpace(branchName))
            {
                return branchName.Trim();
            }

            // Empty result from --show-current means detached HEAD or older git version
            // Try fallback method
            branchName = await ExecuteGitCommandAsync(
                projectPath,
                "symbolic-ref -q --short HEAD",
                cancellationToken);

            if (!string.IsNullOrWhiteSpace(branchName))
            {
                return branchName.Trim();
            }

            _logger.LogWarning(
                "Could not detect git branch for {ProjectPath}, using default: {DefaultBranch}",
                projectPath,
                DefaultBranchName);

            return DefaultBranchName;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Git branch detection failed for {ProjectPath}, using default: {DefaultBranch}",
                projectPath,
                DefaultBranchName);

            return DefaultBranchName;
        }
    }

    /// <summary>
    /// Checks if the given path is inside a git repository.
    /// </summary>
    /// <param name="path">The path to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the path is in a git repository.</returns>
    public async Task<bool> IsGitRepositoryAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await ExecuteGitCommandAsync(
                path,
                "rev-parse --git-dir",
                cancellationToken);

            return !string.IsNullOrWhiteSpace(result);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets the git repository root for the given path.
    /// </summary>
    /// <param name="path">The path to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The repository root path, or null if not in a git repository.</returns>
    public async Task<string?> GetRepositoryRootAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await ExecuteGitCommandAsync(
                path,
                "rev-parse --show-toplevel",
                cancellationToken);

            return string.IsNullOrWhiteSpace(result) ? null : result.Trim();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Registers the repository path in the database.
    /// </summary>
    private async Task RegisterRepoPathAsync(string projectPath, CancellationToken cancellationToken)
    {
        if (_repoPathRepository == null)
        {
            _logger.LogDebug(
                "Repository not available, skipping registration for: {ProjectPath}",
                projectPath);
            return;
        }

        var projectName = _sessionContext.ProjectName;
        var pathHash = _sessionContext.PathHash;

        if (string.IsNullOrEmpty(projectName) || string.IsNullOrEmpty(pathHash))
        {
            _logger.LogWarning("Session context missing required values for registration");
            return;
        }

        try
        {
            // Register repo path
            await _repoPathRepository.GetOrCreateAsync(
                projectPath,
                projectName,
                pathHash,
                cancellationToken);

            _logger.LogDebug(
                "Registered repo path: {ProjectPath} with hash {PathHash}",
                projectPath,
                pathHash);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register repo path: {ProjectPath}", projectPath);
            // Don't fail activation if registration fails
        }
    }

    /// <summary>
    /// Executes a git command and returns the output.
    /// </summary>
    private async Task<string?> ExecuteGitCommandAsync(
        string workingDirectory,
        string arguments,
        CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };

        _logger.LogDebug(
            "Executing git command: git {Arguments} in {Directory}",
            arguments,
            workingDirectory);

        process.Start();

        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(GitCommandTimeoutMs);

        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                "Git command timed out after {Timeout}ms: git {Arguments}",
                GitCommandTimeoutMs,
                arguments);

            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch
            {
                // Best effort cleanup
            }

            return null;
        }

        var output = await outputTask;
        var error = await errorTask;

        if (process.ExitCode != 0)
        {
            _logger.LogDebug(
                "Git command returned non-zero exit code {ExitCode}: {Error}",
                process.ExitCode,
                error);

            return null;
        }

        return output;
    }
}

/// <summary>
/// Result of a project activation attempt.
/// </summary>
public sealed record ProjectActivationResult
{
    /// <summary>
    /// Gets whether the activation was successful.
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// Gets the project name if activation succeeded.
    /// </summary>
    public string? ProjectName { get; init; }

    /// <summary>
    /// Gets the branch name if activation succeeded.
    /// </summary>
    public string? BranchName { get; init; }

    /// <summary>
    /// Gets the path hash if activation succeeded.
    /// </summary>
    public string? PathHash { get; init; }

    /// <summary>
    /// Gets the full tenant key if activation succeeded.
    /// </summary>
    public string? TenantKey { get; init; }

    /// <summary>
    /// Gets the error message if activation failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Creates a successful activation result.
    /// </summary>
    public static ProjectActivationResult Succeeded(
        string projectName,
        string branchName,
        string pathHash,
        string tenantKey) => new()
        {
            IsSuccess = true,
            ProjectName = projectName,
            BranchName = branchName,
            PathHash = pathHash,
            TenantKey = tenantKey
        };

    /// <summary>
    /// Creates a failed activation result.
    /// </summary>
    public static ProjectActivationResult Failed(string errorMessage) => new()
    {
        IsSuccess = false,
        ErrorMessage = errorMessage
    };
}
