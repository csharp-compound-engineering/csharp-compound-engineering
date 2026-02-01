# Testing Structure Summary

This file contains summaries for the testing specification and its children.

---

## spec/testing.md

### What This File Covers

The testing specification defines the complete testing strategy for the `csharp-compounding-docs` plugin, including:

- **Test Categories**: Unit tests (isolated, mocked), integration tests (real PostgreSQL/Ollama via Aspire), and E2E tests (full MCP protocol workflows)
- **Technology Stack**: xUnit 2.9.3, Moq, Shouldly, Coverlet, .NET Aspire, and ModelContextProtocol client
- **100% Code Coverage Enforcement**: Line, branch, and method coverage enforced via Coverlet with explicit exclusion patterns
- **Test Independence**: Strict requirement that all tests be completely independent with no shared state
- **MCP Testing Patterns**: Unit testing tool handlers with mocks, E2E testing via stdio MCP client
- **Naming Conventions**: Standardized class and method naming patterns
- **Coverage Visualization**: ReportGenerator for HTML reports and GitHub Pages publishing
- **Deferred Capabilities**: Snapshot testing (Verify), contract testing (Pact), and performance benchmarks (BenchmarkDotNet) excluded from MVP

### Structural Relationships

#### Parent
- [SPEC.md](../SPEC.md) - The root specification document that indexes all sub-topics

#### Children (Sub-Topics)
- [testing/test-independence.md](testing/test-independence.md) - Detailed patterns for test isolation
- [testing/ci-cd-pipeline.md](testing/ci-cd-pipeline.md) - GitHub Actions workflow configuration
- [testing/aspire-fixtures.md](testing/aspire-fixtures.md) - .NET Aspire test fixtures and container orchestration

#### Siblings (Same Level in spec/)
- doc-types.md - Document type architecture
- mcp-server.md - MCP server implementation
- infrastructure.md - Docker and infrastructure setup
- skills.md - Claude Code skills
- agents.md - Research agents
- marketplace.md - Plugin marketplace
- configuration.md - Configuration schemas
- observability.md - Logging and monitoring
- research-index.md - Research document index

---

## spec/testing/test-independence.md

### What This File Covers

This specification establishes the critical requirement that **all tests (unit, integration, E2E) must be completely independent** with no shared state between tests.

**Core Principles:**
- No shared mocks (each test creates its own mock instances)
- No test ordering dependencies (tests must pass regardless of execution order)
- No shared data (use unique identifiers like GUIDs for isolation)
- No static state (avoid static fields that persist between tests)

**Key Topics:**
- Anti-patterns to avoid (shared class-level mocks, shared SUT with state, test method dependencies)
- Correct patterns for unit tests (fresh mocks per test, helper methods that return new instances)
- Integration/E2E test isolation via data partitioning (unique collection names, unique file paths, IAsyncLifetime for setup/cleanup)
- Database isolation strategies (unique collection names, GUIDs, TRUNCATE, schema per test class)
- Enforcement via code review checklist and CI verification

**Benefits of Independence:** Parallel execution, debuggability, maintainability, CI reliability.

### Structural Relationships

- **Parent:** `spec/testing.md`
- **Siblings:**
  - `spec/testing/aspire-fixtures.md`
  - `spec/testing/ci-cd-pipeline.md`
- **Children:** None
- **References:**
  - `research/unit-testing-xunit-moq-shouldly.md` (xUnit, Moq, Shouldly patterns)
  - `research/aspire-testing-mcp-client.md` (Aspire integration testing patterns)

---

## spec/testing/ci-cd-pipeline.md

### What This File Covers

This specification defines the GitHub Actions CI/CD pipeline for automated testing of the `csharp-compounding-docs` plugin. Key areas include:

- **Pipeline Architecture**: Three-stage test execution (Unit Tests -> Integration Tests -> E2E Tests) with defined timeouts and dependencies
- **GitHub Actions Workflows**: Complete YAML configurations for `test.yml` and `release.yml` workflows targeting .NET 10.0
- **Docker Integration**: Pre-downloading Ollama models and PostgreSQL (pgvector) containers for integration/E2E tests
- **Model Download Optimization**: Strategies for caching large Ollama models within GitHub Actions' 10GB cache limit, including smaller CI-specific model alternatives
- **Coverage Reporting**: ReportGenerator integration for HTML reports from Coverlet's Cobertura output, with GitHub Pages publishing per release
- **Failure Handling**: Coverage threshold enforcement (100%), test timeout configuration, and xUnit failure reporting
- **Release Workflow (semantic-release)**: Fully automated versioning based on Conventional Commits, updating `Directory.Build.props`, `plugin.json`, and `CHANGELOG.md`

### Structural Relationships

- **Parent**: `spec/testing.md`
- **Siblings**:
  - `spec/testing/aspire-fixtures.md`
  - `spec/testing/test-independence.md`
- **Children**: None

---

## spec/testing/aspire-fixtures.md

### What This File Covers

This specification defines .NET Aspire fixture patterns for integration and E2E testing of the `csharp-compounding-docs` plugin. Key topics include:

- **Core Fixture (`AspireIntegrationFixture`)**: Manages Aspire application lifecycle using `DistributedApplicationTestingBuilder`, provides PostgreSQL connection strings, Ollama endpoints, and MCP client initialization via `StdioClientTransport`
- **xUnit Collection Fixtures**: Defines `ICollectionFixture` patterns to share a single Aspire fixture instance across test classes
- **Resource Waiting Patterns**: Documents `WaitForResourceHealthyAsync` vs `WaitForResourceAsync` with timeout recommendations (1 min for PostgreSQL, 1 min for Ollama)
- **Database Isolation Strategies**: Three approaches - unique collection names per test (recommended), TRUNCATE between tests, and schema-per-test-class
- **Lightweight Database Fixture**: Alternative fixture for tests needing only database access without full Aspire orchestration
- **Test Helpers**: Retry and wait-for-condition utilities for async operations
- **xUnit Configuration**: Runner settings disabling parallelization to prevent race conditions in shared-state tests

### Structural Relationships

- **Parent**: `spec/testing.md` - The main testing specification that defines the overall testing strategy, technology stack, coverage requirements, and test project structure
- **Siblings**:
  - `spec/testing/test-independence.md` - Patterns for ensuring complete test isolation
  - `spec/testing/ci-cd-pipeline.md` - GitHub Actions workflow configuration with staged test execution
