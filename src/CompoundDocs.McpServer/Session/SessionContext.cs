using System.Threading;

namespace CompoundDocs.McpServer.Session;

/// <summary>
/// Implementation of ISessionContext for managing session state.
/// This is a scoped service that maintains the active project context per request.
/// Thread-safe for concurrent access within a session.
/// </summary>
public sealed class SessionContext : ISessionContext, IDisposable
{
    private readonly ReaderWriterLockSlim _lock = new(LockRecursionPolicy.NoRecursion);
    private string? _activeProjectPath;
    private string? _activeBranch;
    private string? _tenantKey;
    private string? _projectName;
    private string? _pathHash;
    private bool _disposed;

    /// <inheritdoc />
    public string? ActiveProjectPath
    {
        get
        {
            ThrowIfDisposed();
            _lock.EnterReadLock();
            try
            {
                return _activeProjectPath;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }
    }

    /// <inheritdoc />
    public string? ActiveBranch
    {
        get
        {
            ThrowIfDisposed();
            _lock.EnterReadLock();
            try
            {
                return _activeBranch;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }
    }

    /// <inheritdoc />
    public string? TenantKey
    {
        get
        {
            ThrowIfDisposed();
            _lock.EnterReadLock();
            try
            {
                return _tenantKey;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }
    }

    /// <inheritdoc />
    public bool IsProjectActive
    {
        get
        {
            ThrowIfDisposed();
            _lock.EnterReadLock();
            try
            {
                return _activeProjectPath != null;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }
    }

    /// <inheritdoc />
    public string? ProjectName
    {
        get
        {
            ThrowIfDisposed();
            _lock.EnterReadLock();
            try
            {
                return _projectName;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }
    }

    /// <inheritdoc />
    public string? PathHash
    {
        get
        {
            ThrowIfDisposed();
            _lock.EnterReadLock();
            try
            {
                return _pathHash;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }
    }

    /// <inheritdoc />
    public void ActivateProject(string projectPath, string branchName)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(projectPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectPath, nameof(projectPath));
        ArgumentNullException.ThrowIfNull(branchName);
        ArgumentException.ThrowIfNullOrWhiteSpace(branchName, nameof(branchName));

        _lock.EnterWriteLock();
        try
        {
            _activeProjectPath = projectPath;
            _activeBranch = branchName;
            _projectName = TenantKeyProvider.ExtractProjectName(projectPath);
            _pathHash = TenantKeyProvider.ComputePathHash(projectPath);
            _tenantKey = TenantKeyProvider.GenerateTenantKey(_projectName, branchName, _pathHash);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <inheritdoc />
    public void DeactivateProject()
    {
        ThrowIfDisposed();
        _lock.EnterWriteLock();
        try
        {
            _activeProjectPath = null;
            _activeBranch = null;
            _tenantKey = null;
            _projectName = null;
            _pathHash = null;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <inheritdoc />
    public string GetConnectionString(string baseConnectionString)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(baseConnectionString);

        _lock.EnterReadLock();
        try
        {
            if (_activeProjectPath == null)
            {
                throw new InvalidOperationException(
                    "Cannot get connection string: no project is currently active. " +
                    "Call ActivateProject first.");
            }

            // The base connection string is returned as-is since tenant isolation
            // is handled via query filtering, not separate databases.
            // The tenant key is used in vector search filters, not connection strings.
            return baseConnectionString;
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Gets a snapshot of the current session state for logging or debugging.
    /// </summary>
    /// <returns>A string representation of the current state.</returns>
    public override string ToString()
    {
        _lock.EnterReadLock();
        try
        {
            if (_activeProjectPath == null)
            {
                return "SessionContext[Inactive]";
            }

            return $"SessionContext[{_projectName}:{_activeBranch}:{_pathHash?[..8]}]";
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Disposes the session context and releases the reader-writer lock.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        _lock.Dispose();
        _disposed = true;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
