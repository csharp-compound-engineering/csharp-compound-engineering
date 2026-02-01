using System.Security.Cryptography;
using System.Text;

namespace CompoundDocs.McpServer.Session;

/// <summary>
/// Static helper for tenant key operations.
/// Provides methods for computing path hashes and generating/parsing tenant keys.
/// </summary>
public static class TenantKeyProvider
{
    /// <summary>
    /// The separator used between components in the tenant key.
    /// </summary>
    public const char KeySeparator = ':';

    /// <summary>
    /// The number of characters to use from the SHA-256 hash.
    /// 16 characters = 64 bits of entropy, sufficient for uniqueness.
    /// </summary>
    public const int PathHashLength = 16;

    /// <summary>
    /// Computes a SHA-256 hash of the given path, returning the first 16 hex characters.
    /// The path is normalized before hashing to ensure consistency across platforms.
    /// </summary>
    /// <param name="absolutePath">The absolute path to hash.</param>
    /// <returns>The first 16 hex characters of the SHA-256 hash (lowercase).</returns>
    /// <exception cref="ArgumentNullException">Thrown when absolutePath is null.</exception>
    /// <exception cref="ArgumentException">Thrown when absolutePath is empty or whitespace.</exception>
    public static string ComputePathHash(string absolutePath)
    {
        ArgumentNullException.ThrowIfNull(absolutePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(absolutePath, nameof(absolutePath));

        // Normalize path separators and trim trailing slashes for consistency
        var normalizedPath = absolutePath
            .Replace('\\', '/')
            .TrimEnd('/');

        // Compute SHA-256 hash
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalizedPath));

        // Return first 16 characters of hex string (lowercase)
        return Convert.ToHexString(hashBytes)[..PathHashLength].ToLowerInvariant();
    }

    /// <summary>
    /// Generates a tenant key from the component parts.
    /// Format: project_name:branch_name:path_hash
    /// </summary>
    /// <param name="projectName">The project name.</param>
    /// <param name="branchName">The git branch name.</param>
    /// <param name="pathHash">The SHA-256 path hash.</param>
    /// <returns>The combined tenant key.</returns>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
    /// <exception cref="ArgumentException">Thrown when any parameter is empty or whitespace.</exception>
    public static string GenerateTenantKey(string projectName, string branchName, string pathHash)
    {
        ArgumentNullException.ThrowIfNull(projectName);
        ArgumentNullException.ThrowIfNull(branchName);
        ArgumentNullException.ThrowIfNull(pathHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectName, nameof(projectName));
        ArgumentException.ThrowIfNullOrWhiteSpace(branchName, nameof(branchName));
        ArgumentException.ThrowIfNullOrWhiteSpace(pathHash, nameof(pathHash));

        return $"{projectName}{KeySeparator}{branchName}{KeySeparator}{pathHash}";
    }

    /// <summary>
    /// Generates a tenant key from a project path and branch name.
    /// Computes the path hash automatically.
    /// </summary>
    /// <param name="projectPath">The absolute path to the project.</param>
    /// <param name="branchName">The git branch name.</param>
    /// <returns>The combined tenant key.</returns>
    public static string GenerateTenantKey(string projectPath, string branchName)
    {
        var projectName = ExtractProjectName(projectPath);
        var pathHash = ComputePathHash(projectPath);
        return GenerateTenantKey(projectName, branchName, pathHash);
    }

    /// <summary>
    /// Parses a tenant key into its component parts.
    /// </summary>
    /// <param name="tenantKey">The tenant key to parse.</param>
    /// <returns>A tuple containing (projectName, branchName, pathHash).</returns>
    /// <exception cref="ArgumentNullException">Thrown when tenantKey is null.</exception>
    /// <exception cref="ArgumentException">Thrown when tenantKey is empty or whitespace.</exception>
    /// <exception cref="FormatException">Thrown when tenantKey is not in the expected format.</exception>
    public static (string ProjectName, string BranchName, string PathHash) ParseTenantKey(string tenantKey)
    {
        ArgumentNullException.ThrowIfNull(tenantKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantKey, nameof(tenantKey));

        var parts = tenantKey.Split(KeySeparator);
        if (parts.Length != 3)
        {
            throw new FormatException(
                $"Invalid tenant key format. Expected 'project_name{KeySeparator}branch_name{KeySeparator}path_hash', got: {tenantKey}");
        }

        return (parts[0], parts[1], parts[2]);
    }

    /// <summary>
    /// Attempts to parse a tenant key into its component parts.
    /// </summary>
    /// <param name="tenantKey">The tenant key to parse.</param>
    /// <param name="result">The parsed result if successful.</param>
    /// <returns>True if parsing succeeded, false otherwise.</returns>
    public static bool TryParseTenantKey(
        string? tenantKey,
        out (string ProjectName, string BranchName, string PathHash) result)
    {
        result = default;

        if (string.IsNullOrWhiteSpace(tenantKey))
        {
            return false;
        }

        var parts = tenantKey.Split(KeySeparator);
        if (parts.Length != 3)
        {
            return false;
        }

        // Validate each part is not empty
        if (string.IsNullOrWhiteSpace(parts[0]) ||
            string.IsNullOrWhiteSpace(parts[1]) ||
            string.IsNullOrWhiteSpace(parts[2]))
        {
            return false;
        }

        result = (parts[0], parts[1], parts[2]);
        return true;
    }

    /// <summary>
    /// Extracts the project name from an absolute project path.
    /// Uses the last directory component of the path.
    /// </summary>
    /// <param name="projectPath">The absolute path to the project.</param>
    /// <returns>The extracted project name.</returns>
    /// <exception cref="ArgumentNullException">Thrown when projectPath is null.</exception>
    /// <exception cref="ArgumentException">Thrown when projectPath is empty or whitespace,
    /// or when the project name cannot be extracted.</exception>
    public static string ExtractProjectName(string projectPath)
    {
        ArgumentNullException.ThrowIfNull(projectPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectPath, nameof(projectPath));

        // Normalize and get the directory name
        var normalizedPath = projectPath
            .Replace('\\', '/')
            .TrimEnd('/');

        var lastSeparatorIndex = normalizedPath.LastIndexOf('/');
        var projectName = lastSeparatorIndex >= 0
            ? normalizedPath[(lastSeparatorIndex + 1)..]
            : normalizedPath;

        if (string.IsNullOrWhiteSpace(projectName))
        {
            throw new ArgumentException(
                $"Could not extract project name from path: {projectPath}",
                nameof(projectPath));
        }

        return projectName;
    }

    /// <summary>
    /// Validates that a tenant key has the correct format.
    /// </summary>
    /// <param name="tenantKey">The tenant key to validate.</param>
    /// <returns>True if the tenant key is valid, false otherwise.</returns>
    public static bool IsValidTenantKey(string? tenantKey)
    {
        return TryParseTenantKey(tenantKey, out _);
    }
}
