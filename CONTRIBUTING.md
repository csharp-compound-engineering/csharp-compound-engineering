# Contributing to CSharp Compound Docs

Thank you for your interest in contributing to CSharp Compound Docs! This document provides guidelines and instructions for contributing.

## Table of Contents

- [Code of Conduct](#code-of-conduct)
- [Getting Started](#getting-started)
- [Development Environment Setup](#development-environment-setup)
- [Project Structure](#project-structure)
- [Coding Standards](#coding-standards)
- [Testing Requirements](#testing-requirements)
- [Pull Request Process](#pull-request-process)
- [Commit Message Format](#commit-message-format)
- [Issue Guidelines](#issue-guidelines)
- [Documentation](#documentation)

---

## Code of Conduct

This project has adopted the [Contributor Covenant Code of Conduct](CODE_OF_CONDUCT.md). By participating, you are expected to uphold this code. Please report unacceptable behavior by opening a GitHub Issue with the `conduct` label.

---

## Getting Started

### Prerequisites

Before contributing, ensure you have:

- .NET 9.0 SDK or later
- Git
- A code editor (Visual Studio, VS Code, or Rider recommended)
- PowerShell 7+ (only needed for the reference IaC scripts in `opentofu/k8s/scripts/` and `opentofu/serverless/scripts/`)
- AWS credentials (optional — only needed to run against real AWS services)
- Docker (optional — only needed for building the production container image)

### Quick Start

1. Fork the repository
2. Clone your fork:
   ```bash
   git clone https://github.com/YOUR-USERNAME/csharp-compound-engineering.git  # your fork
   cd csharp-compound-engineering
   ```
3. Add upstream remote:
   ```bash
   git remote add upstream https://github.com/csharp-compound-engineering/csharp-compound-engineering.git
   ```
4. Create a feature branch:
   ```bash
   git checkout -b feature/your-feature-name
   ```

---

## Development Environment Setup

### 1. Install Dependencies

```bash
# Restore NuGet packages
dotnet restore

# Verify build
dotnet build
```

### 2. Configure AWS (Optional)

Unit tests and mock-based integration/E2E tests run without AWS credentials. To run tests against real AWS services, configure credentials:

```bash
aws sso login   # or: aws configure
```

### 3. Run Tests

```bash
# Run all tests
dotnet test

# Run only unit tests
dotnet test tests/CompoundDocs.Tests.Unit

# Run only integration tests
dotnet test tests/CompoundDocs.Tests.Integration

# Run a specific test
dotnet test --filter "FullyQualifiedName~MyTestMethod"
```

### 4. Run the MCP Server Locally

```bash
dotnet run --project src/CompoundDocs.McpServer/CompoundDocs.McpServer.csproj
```

---

## Project Structure

```
csharp-compound-engineering/
├── src/                          # Source code
│   ├── CompoundDocs.McpServer/   # MCP server app (HTTP transport)
│   ├── CompoundDocs.Common/      # Shared models, config loading, logging
│   ├── CompoundDocs.GraphRag/    # RAG pipeline orchestration
│   ├── CompoundDocs.Vector/      # AWS OpenSearch Serverless KNN search
│   ├── CompoundDocs.Graph/       # Amazon Neptune (openCypher)
│   ├── CompoundDocs.Bedrock/     # Embedding + LLM services
│   ├── CompoundDocs.GitSync/     # Git repo monitoring
│   └── CompoundDocs.Worker/      # Background document processing
├── tests/                        # Test projects
│   ├── CompoundDocs.Tests.Unit/
│   ├── CompoundDocs.Tests.Integration/
│   └── CompoundDocs.Tests.E2E/
├── charts/compound-docs/         # Helm chart for Kubernetes deployment (production)
├── opentofu/                     # Reference IaC for AWS infrastructure (dev/testing)
│   └── scripts/                  # PowerShell orchestration scripts
├── scripts/                      # Bash scripts (coverage, release)
├── docs/                         # Documentation site (Nextra)
└── .claude-plugin/               # Claude Code plugin manifests
```

---

## Coding Standards

### General Guidelines

- Follow the [.editorconfig](.editorconfig) rules
- Use meaningful variable and method names
- Keep methods small and focused (single responsibility)
- Prefer composition over inheritance
- Write self-documenting code; add comments only when necessary

### C# Specific

```csharp
// Use file-scoped namespaces
namespace CompoundDocs.McpServer.Services;

// Use primary constructors where appropriate (C# 12+)
public class DocumentService(IDocumentRepository repository, ILogger<DocumentService> logger)
{
    // Use expression-bodied members for simple operations
    public int Count => _documents.Count;

    // Use async/await consistently
    public async Task<Document?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        // Use pattern matching
        return await repository.GetAsync(id, ct) switch
        {
            Document doc => doc,
            null => null
        };
    }

    // Use nullable reference types
    public string? GetTitle(Document? doc) => doc?.Title;
}
```

### Naming Conventions

| Element | Convention | Example |
|---------|------------|---------|
| Namespace | PascalCase | `CompoundDocs.McpServer` |
| Class | PascalCase | `DocumentService` |
| Interface | IPascalCase | `IDocumentRepository` |
| Method | PascalCase | `GetDocumentAsync` |
| Property | PascalCase | `DocumentPath` |
| Field (private) | _camelCase | `_repository` |
| Parameter | camelCase | `documentId` |
| Local variable | camelCase | `result` |
| Constant | PascalCase | `MaxRetryCount` |

### Code Organization

```csharp
public class MyService
{
    // 1. Constants
    private const int MaxRetries = 3;

    // 2. Static fields
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    // 3. Instance fields
    private readonly ILogger _logger;
    private readonly IRepository _repository;

    // 4. Constructors
    public MyService(ILogger logger, IRepository repository)
    {
        _logger = logger;
        _repository = repository;
    }

    // 5. Properties
    public bool IsEnabled { get; set; }

    // 6. Public methods
    public async Task DoWorkAsync() { }

    // 7. Protected methods
    protected virtual void OnEvent() { }

    // 8. Private methods
    private void ValidateInput() { }
}
```

---

## Testing Requirements

### Coverage Requirements

- **100% line coverage** required
- **100% branch coverage** required
- **100% method coverage** required

Coverage is enforced via Coverlet. Builds will fail if coverage drops below threshold.

### Test Categories

```csharp
// Unit tests - fast, isolated
[Fact]
[Trait("Category", "Unit")]
public void MyMethod_WithValidInput_ReturnsExpected() { }

// Integration tests - with real infrastructure
[Fact]
[Trait("Category", "Integration")]
public async Task MyMethod_WithDatabase_PersistsData() { }

// E2E tests - full workflow
[Fact(Timeout = 120000)]
[Trait("Category", "E2E")]
public async Task FullWorkflow_IndexAndQuery_ReturnsResults() { }
```

### Test Independence

All tests must be completely independent:

```csharp
// CORRECT: Each test creates its own mocks
[Fact]
public void Test_Scenario()
{
    var mock = new Mock<IService>();
    var sut = new MyClass(mock.Object);
    // ...
}

// INCORRECT: Shared mocks (DO NOT DO THIS)
private Mock<IService> _sharedMock; // BAD!
```

### Running Tests

```bash
# Run all tests
dotnet test

# Run with coverage
dotnet test /p:CollectCoverage=true

# Run specific category
dotnet test --filter "Category=Unit"

# Run specific test
dotnet test --filter "FullyQualifiedName~MyTestMethod"
```

---

## Pull Request Process

### Before Submitting

1. **Sync with upstream**:
   ```bash
   git fetch upstream
   git rebase upstream/master
   ```

2. **Run all tests**:
   ```bash
   dotnet test
   ```

3. **Check for warnings**:
   ```bash
   dotnet build --warnaserror
   ```

4. **Format code**:
   ```bash
   dotnet format
   ```

### PR Requirements

- [ ] Tests pass locally
- [ ] Code coverage maintained at 100%
- [ ] No compiler warnings
- [ ] Documentation updated (if applicable)
- [ ] CHANGELOG.md updated (for user-facing changes)
- [ ] Commit messages follow convention

### PR Template

When creating a PR, include:

```markdown
## Summary
Brief description of changes

## Type of Change
- [ ] Bug fix
- [ ] New feature
- [ ] Breaking change
- [ ] Documentation update

## Testing
How were changes tested?

## Checklist
- [ ] Tests added/updated
- [ ] Documentation updated
- [ ] CHANGELOG.md updated
```

### Review Process

1. Create PR against `master` branch
2. Automated checks must pass (CI, coverage)
3. At least one maintainer approval required
4. Address review feedback
5. Maintainer merges when approved

---

## Commit Message Format

This project uses [Conventional Commits](https://www.conventionalcommits.org/).

### Format

```
<type>(<scope>): <description>

[optional body]

[optional footer(s)]
```

### Types

| Type | Description |
|------|-------------|
| `feat` | New feature |
| `fix` | Bug fix |
| `docs` | Documentation only |
| `style` | Code style (formatting, semicolons) |
| `refactor` | Code change that neither fixes nor adds |
| `perf` | Performance improvement |
| `test` | Adding or updating tests |
| `build` | Build system or dependencies |
| `ci` | CI configuration |
| `chore` | Other changes (tooling, etc.) |

### Scopes

| Scope | Description |
|-------|-------------|
| `mcp` | MCP server |
| `tools` | MCP tools |
| `skills` | Claude Code skills |
| `db` | Database/migrations |
| `docker` | Docker configuration |
| `docs` | Documentation |
| `tests` | Test infrastructure |

### Examples

```bash
# Feature
feat(tools): add support for custom doc-types in rag_query

# Bug fix
fix(mcp): handle null embedding gracefully in search

# Breaking change
feat(db)!: change tenant isolation to compound key

BREAKING CHANGE: Existing databases require migration.

# Documentation
docs: add troubleshooting guide for Ollama issues

# Multiple scopes
feat(tools,skills): implement promotion level support
```

---

## Issue Guidelines

### Bug Reports

Include:

1. **Environment**: OS, .NET version, Docker version
2. **Steps to reproduce**: Numbered list
3. **Expected behavior**: What should happen
4. **Actual behavior**: What actually happens
5. **Logs/errors**: Relevant error messages
6. **Screenshots**: If applicable

### Feature Requests

Include:

1. **Problem statement**: What problem does this solve?
2. **Proposed solution**: How should it work?
3. **Alternatives considered**: Other approaches
4. **Additional context**: Examples, mockups

### Issue Labels

| Label | Description |
|-------|-------------|
| `bug` | Something isn't working |
| `enhancement` | New feature request |
| `documentation` | Documentation improvements |
| `good first issue` | Good for newcomers |
| `help wanted` | Extra attention needed |
| `question` | Further information requested |

---

## Documentation

### When to Update Docs

- New features require documentation
- API changes require API reference updates
- Bug fixes may need troubleshooting updates
- Configuration changes need configuration docs

### Documentation Locations

| Type | Location |
|------|----------|
| API Reference | `docs/content/api-reference.mdx` |
| Architecture | `docs/content/architecture.mdx` |
| Configuration | `docs/content/configuration.mdx` |
| Installation | `docs/content/installation.mdx` |
| Troubleshooting | `docs/content/troubleshooting.mdx` |

### Documentation Style

- Use clear, concise language
- Include code examples
- Use tables for structured data
- Add diagrams for complex concepts
- Keep lines under 100 characters

---

## Getting Help

- **Questions**: Open a GitHub Discussion
- **Bugs**: Open a GitHub Issue
- **Security**: See [SECURITY.md](SECURITY.md)

Thank you for contributing!
