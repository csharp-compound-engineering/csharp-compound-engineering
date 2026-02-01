using CompoundDocs.McpServer.Data;
using CompoundDocs.McpServer.Data.Entities;
using CompoundDocs.McpServer.Data.Repositories;
using CompoundDocs.McpServer.Session;
using Microsoft.Extensions.Logging;

namespace CompoundDocs.IntegrationTests.Data;

/// <summary>
/// Integration tests for RepoPathRepository using PostgreSQL container.
/// Tests are skipped when Docker is not available.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class RepoPathRepositoryTests : IAsyncLifetime
{
    private readonly PostgresFixture _fixture;
    private TenantDbContext? _context;
    private RepoPathRepository? _sut;

    public RepoPathRepositoryTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync()
    {
        if (!_fixture.IsAvailable)
        {
            return Task.CompletedTask;
        }

        _context = _fixture.CreateDbContext();
        _sut = new RepoPathRepository(_context, _fixture.CreateLogger<RepoPathRepository>());
        return Task.CompletedTask;
    }

    private void SkipIfDockerUnavailable()
    {
        Skip.If(!_fixture.IsAvailable, "Docker is not available. Skipping integration test.");
    }

    public async Task DisposeAsync()
    {
        if (_context == null)
        {
            return;
        }

        // Clean up test data
        _context.RepoPaths.RemoveRange(_context.RepoPaths);
        await _context.SaveChangesAsync();
        await _context.DisposeAsync();
    }

    #region GetByPathHashAsync Tests

    [SkippableFact]
    public async Task GetByPathHashAsync_WithExistingPath_ReturnsRepoPath()
    {
        SkipIfDockerUnavailable();

        // Arrange
        var pathHash = TenantKeyProvider.ComputePathHash("/test/project/path1");
        var repoPath = new RepoPath
        {
            Id = Guid.NewGuid(),
            ProjectName = "test-project",
            AbsolutePath = "/test/project/path1",
            PathHash = pathHash,
            CreatedAt = DateTimeOffset.UtcNow,
            LastAccessedAt = DateTimeOffset.UtcNow
        };
        _context!.RepoPaths.Add(repoPath);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut!.GetByPathHashAsync(pathHash);

        // Assert
        result.ShouldNotBeNull();
        result.PathHash.ShouldBe(pathHash);
        result.ProjectName.ShouldBe("test-project");
    }

    [SkippableFact]
    public async Task GetByPathHashAsync_WithNonExistentPath_ReturnsNull()
    {
        SkipIfDockerUnavailable();

        // Arrange
        var pathHash = "nonexistent12345";

        // Act
        var result = await _sut!.GetByPathHashAsync(pathHash);

        // Assert
        result.ShouldBeNull();
    }

    [SkippableFact]
    public async Task GetByPathHashAsync_IncludesBranches()
    {
        SkipIfDockerUnavailable();

        // Arrange
        var pathHash = TenantKeyProvider.ComputePathHash("/test/project/with-branches");
        var repoPath = new RepoPath
        {
            Id = Guid.NewGuid(),
            ProjectName = "project-with-branches",
            AbsolutePath = "/test/project/with-branches",
            PathHash = pathHash,
            CreatedAt = DateTimeOffset.UtcNow,
            LastAccessedAt = DateTimeOffset.UtcNow,
            Branches = new List<Branch>
            {
                new Branch
                {
                    Id = Guid.NewGuid(),
                    BranchName = "main",
                    CreatedAt = DateTimeOffset.UtcNow,
                    LastAccessedAt = DateTimeOffset.UtcNow
                }
            }
        };
        _context!.RepoPaths.Add(repoPath);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut!.GetByPathHashAsync(pathHash);

        // Assert
        result.ShouldNotBeNull();
        result.Branches.ShouldNotBeNull();
        result.Branches.Count.ShouldBe(1);
        result.Branches.First().BranchName.ShouldBe("main");
    }

    #endregion

    #region GetOrCreateAsync Tests

    [SkippableFact]
    public async Task GetOrCreateAsync_WithNewPath_CreatesNewRepoPath()
    {
        SkipIfDockerUnavailable();

        // Arrange
        var absolutePath = "/new/project/path";
        var projectName = "new-project";
        var pathHash = TenantKeyProvider.ComputePathHash(absolutePath);

        // Act
        var result = await _sut!.GetOrCreateAsync(absolutePath, projectName, pathHash);

        // Assert
        result.ShouldNotBeNull();
        result.AbsolutePath.ShouldBe(absolutePath);
        result.ProjectName.ShouldBe(projectName);
        result.PathHash.ShouldBe(pathHash);

        // Verify in database
        var fromDb = await _sut.GetByPathHashAsync(pathHash);
        fromDb.ShouldNotBeNull();
    }

    [SkippableFact]
    public async Task GetOrCreateAsync_WithExistingPath_ReturnsExistingAndUpdatesTimestamp()
    {
        SkipIfDockerUnavailable();

        // Arrange
        var absolutePath = "/existing/project/path";
        var projectName = "existing-project";
        var pathHash = TenantKeyProvider.ComputePathHash(absolutePath);

        var originalTimestamp = DateTimeOffset.UtcNow.AddDays(-1);
        var existingRepo = new RepoPath
        {
            Id = Guid.NewGuid(),
            ProjectName = projectName,
            AbsolutePath = absolutePath,
            PathHash = pathHash,
            CreatedAt = originalTimestamp,
            LastAccessedAt = originalTimestamp
        };
        _context!.RepoPaths.Add(existingRepo);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut!.GetOrCreateAsync(absolutePath, projectName, pathHash);

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe(existingRepo.Id);
        result.LastAccessedAt.ShouldBeGreaterThan(originalTimestamp);
    }

    [SkippableFact]
    public async Task GetOrCreateAsync_WithEmptyPath_ThrowsArgumentException()
    {
        SkipIfDockerUnavailable();

        // Act & Assert
        await Should.ThrowAsync<ArgumentException>(async () =>
            await _sut!.GetOrCreateAsync("", "project", "hash"));
    }

    #endregion

    #region UpdateLastAccessedAsync Tests

    [SkippableFact]
    public async Task UpdateLastAccessedAsync_WithExistingPath_UpdatesTimestamp()
    {
        SkipIfDockerUnavailable();

        // Arrange
        var pathHash = TenantKeyProvider.ComputePathHash("/update/access/test");
        var originalTimestamp = DateTimeOffset.UtcNow.AddHours(-1);

        var repoPath = new RepoPath
        {
            Id = Guid.NewGuid(),
            ProjectName = "update-test",
            AbsolutePath = "/update/access/test",
            PathHash = pathHash,
            CreatedAt = originalTimestamp,
            LastAccessedAt = originalTimestamp
        };
        _context!.RepoPaths.Add(repoPath);
        await _context.SaveChangesAsync();

        // Act
        var success = await _sut!.UpdateLastAccessedAsync(pathHash);

        // Assert
        success.ShouldBeTrue();

        // Verify updated timestamp
        _context.ChangeTracker.Clear();
        var updated = await _sut.GetByPathHashAsync(pathHash);
        updated.ShouldNotBeNull();
        updated.LastAccessedAt.ShouldBeGreaterThan(originalTimestamp);
    }

    [SkippableFact]
    public async Task UpdateLastAccessedAsync_WithNonExistentPath_ReturnsFalse()
    {
        SkipIfDockerUnavailable();

        // Arrange
        var pathHash = "nonexistent123456";

        // Act
        var success = await _sut!.UpdateLastAccessedAsync(pathHash);

        // Assert
        success.ShouldBeFalse();
    }

    #endregion

    #region GetAllAsync Tests

    [SkippableFact]
    public async Task GetAllAsync_ReturnsAllPaths()
    {
        SkipIfDockerUnavailable();

        // Arrange
        var repo1 = new RepoPath
        {
            Id = Guid.NewGuid(),
            ProjectName = "project-one",
            AbsolutePath = "/project/one",
            PathHash = TenantKeyProvider.ComputePathHash("/project/one"),
            CreatedAt = DateTimeOffset.UtcNow,
            LastAccessedAt = DateTimeOffset.UtcNow.AddMinutes(-10)
        };
        var repo2 = new RepoPath
        {
            Id = Guid.NewGuid(),
            ProjectName = "project-two",
            AbsolutePath = "/project/two",
            PathHash = TenantKeyProvider.ComputePathHash("/project/two"),
            CreatedAt = DateTimeOffset.UtcNow,
            LastAccessedAt = DateTimeOffset.UtcNow
        };
        _context!.RepoPaths.AddRange(repo1, repo2);
        await _context.SaveChangesAsync();

        // Act
        var results = await _sut!.GetAllAsync();

        // Assert
        results.Count.ShouldBeGreaterThanOrEqualTo(2);

        // Should be ordered by LastAccessedAt descending
        results[0].LastAccessedAt.ShouldBeGreaterThanOrEqualTo(results[1].LastAccessedAt);
    }

    [SkippableFact]
    public async Task GetAllAsync_WithProjectNameFilter_FiltersResults()
    {
        SkipIfDockerUnavailable();

        // Arrange
        var repo1 = new RepoPath
        {
            Id = Guid.NewGuid(),
            ProjectName = "filter-target",
            AbsolutePath = "/filter/target",
            PathHash = TenantKeyProvider.ComputePathHash("/filter/target"),
            CreatedAt = DateTimeOffset.UtcNow,
            LastAccessedAt = DateTimeOffset.UtcNow
        };
        var repo2 = new RepoPath
        {
            Id = Guid.NewGuid(),
            ProjectName = "other-project",
            AbsolutePath = "/other/project",
            PathHash = TenantKeyProvider.ComputePathHash("/other/project"),
            CreatedAt = DateTimeOffset.UtcNow,
            LastAccessedAt = DateTimeOffset.UtcNow
        };
        _context!.RepoPaths.AddRange(repo1, repo2);
        await _context.SaveChangesAsync();

        // Act
        var results = await _sut!.GetAllAsync(projectName: "filter-target");

        // Assert
        results.Count.ShouldBe(1);
        results[0].ProjectName.ShouldBe("filter-target");
    }

    #endregion

    #region DeleteAsync Tests

    [SkippableFact]
    public async Task DeleteAsync_WithExistingPath_DeletesAndReturnsTrue()
    {
        SkipIfDockerUnavailable();

        // Arrange
        var pathHash = TenantKeyProvider.ComputePathHash("/delete/test/path");
        var repoPath = new RepoPath
        {
            Id = Guid.NewGuid(),
            ProjectName = "delete-test",
            AbsolutePath = "/delete/test/path",
            PathHash = pathHash,
            CreatedAt = DateTimeOffset.UtcNow,
            LastAccessedAt = DateTimeOffset.UtcNow
        };
        _context!.RepoPaths.Add(repoPath);
        await _context.SaveChangesAsync();

        // Act
        var success = await _sut!.DeleteAsync(pathHash);

        // Assert
        success.ShouldBeTrue();

        // Verify deleted
        var deleted = await _sut.GetByPathHashAsync(pathHash);
        deleted.ShouldBeNull();
    }

    [SkippableFact]
    public async Task DeleteAsync_WithNonExistentPath_ReturnsFalse()
    {
        SkipIfDockerUnavailable();

        // Arrange
        var pathHash = "nonexistent123456";

        // Act
        var success = await _sut!.DeleteAsync(pathHash);

        // Assert
        success.ShouldBeFalse();
    }

    #endregion

    #region GetStalePathsAsync Tests

    [SkippableFact]
    public async Task GetStalePathsAsync_ReturnsPathsOlderThanThreshold()
    {
        SkipIfDockerUnavailable();

        // Arrange
        var staleTime = DateTimeOffset.UtcNow.AddDays(-30);
        var recentTime = DateTimeOffset.UtcNow;

        var staleRepo = new RepoPath
        {
            Id = Guid.NewGuid(),
            ProjectName = "stale-project",
            AbsolutePath = "/stale/project",
            PathHash = TenantKeyProvider.ComputePathHash("/stale/project"),
            CreatedAt = staleTime,
            LastAccessedAt = staleTime
        };
        var recentRepo = new RepoPath
        {
            Id = Guid.NewGuid(),
            ProjectName = "recent-project",
            AbsolutePath = "/recent/project",
            PathHash = TenantKeyProvider.ComputePathHash("/recent/project"),
            CreatedAt = recentTime,
            LastAccessedAt = recentTime
        };
        _context!.RepoPaths.AddRange(staleRepo, recentRepo);
        await _context.SaveChangesAsync();

        // Act
        var results = await _sut!.GetStalePathsAsync(DateTimeOffset.UtcNow.AddDays(-7));

        // Assert
        results.ShouldContain(r => r.ProjectName == "stale-project");
        results.ShouldNotContain(r => r.ProjectName == "recent-project");
    }

    #endregion
}
