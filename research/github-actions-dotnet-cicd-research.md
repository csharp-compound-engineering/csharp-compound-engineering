# GitHub Actions CI/CD for .NET Applications: Comprehensive Research Report

**Research Date**: January 22, 2026
**Project**: csharp-compound-engineering

---

## Table of Contents

1. [GitHub Actions for .NET](#1-github-actions-for-net)
2. [Multi-Platform Builds](#2-multi-platform-builds)
3. [GitHub Container Registry (ghcr.io)](#3-github-container-registry-ghcrio)
4. [Docker Build Actions](#4-docker-build-actions)
5. [GitHub Pages Deployment](#5-github-pages-deployment)
6. [Testing .NET Applications](#6-testing-net-applications)
7. [Integration Testing with Docker](#7-integration-testing-with-docker)
8. [E2E Testing with Testcontainers](#8-e2e-testing-with-testcontainers)
9. [Caching Strategies](#9-caching-strategies)
10. [Release Automation](#10-release-automation)
11. [Complete Workflow Examples](#11-complete-workflow-examples)

---

## 1. GitHub Actions for .NET

### Workflow File Structure

GitHub Actions workflows are defined in YAML files located in `.github/workflows/` directory. The basic structure includes:

```yaml
name: .NET CI

on:
  push:
    branches: [ main, master ]
  pull_request:
    branches: [ main, master ]
  release:
    types: [ published ]
  workflow_dispatch:  # Manual trigger

permissions:
  contents: read

jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'
      - name: Restore dependencies
        run: dotnet restore
      - name: Build
        run: dotnet build --no-restore
      - name: Test
        run: dotnet test --no-build --verbosity normal
```

### Trigger Types

| Trigger | Use Case |
|---------|----------|
| `push` | Continuous integration on code changes |
| `pull_request` | PR validation before merge |
| `release` | Deployment on new releases |
| `schedule` | Scheduled builds (cron syntax) |
| `workflow_dispatch` | Manual workflow execution |

### .NET SDK Setup Action

The `actions/setup-dotnet` action is the recommended way to configure .NET in workflows:

```yaml
- uses: actions/setup-dotnet@v4
  with:
    dotnet-version: '8.0.x'      # Latest 8.0.x patch
    # dotnet-version: '9.0.100'  # Exact version
    # dotnet-version: |          # Multiple versions
    #   6.0.x
    #   7.0.x
    #   8.0.x
    dotnet-quality: 'ga'         # Options: daily, signed, validated, preview, ga
    cache: true                  # Enable NuGet caching (requires lock file)
```

**Version Format Options**:
- `A.B.C` (e.g., `9.0.308`) - Exact version
- `A.B` or `A.B.x` (e.g., `8.0.x`) - Latest patch version
- `A` or `A.x` (e.g., `8.x`) - Latest minor and patch version

**Sources**:
- [actions/setup-dotnet Repository](https://github.com/actions/setup-dotnet)
- [Building and testing .NET - GitHub Docs](https://docs.github.com/actions/guides/building-and-testing-net)

---

## 2. Multi-Platform Builds

### Matrix Strategy

Matrix builds allow running jobs across multiple configurations in parallel:

```yaml
jobs:
  build:
    runs-on: ${{ matrix.os }}
    strategy:
      fail-fast: false  # Continue other jobs if one fails
      matrix:
        os: [ubuntu-latest, windows-latest, macos-latest]
        dotnet-version: ['7.0.x', '8.0.x', '9.0.x']
        include:
          - os: ubuntu-latest
            rid: linux-x64
          - os: windows-latest
            rid: win-x64
          - os: macos-latest
            rid: osx-x64
        exclude:
          - os: macos-latest
            dotnet-version: '7.0.x'
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: ${{ matrix.dotnet-version }}
      - run: dotnet build
      - run: dotnet publish -r ${{ matrix.rid }} --self-contained
```

### Runtime Identifiers (RIDs)

Common portable RIDs for cross-platform publishing:

| Platform | RID |
|----------|-----|
| Windows x64 | `win-x64` |
| Windows ARM64 | `win-arm64` |
| Linux x64 | `linux-x64` |
| Linux ARM64 | `linux-arm64` |
| Linux musl x64 (Alpine) | `linux-musl-x64` |
| Linux musl ARM64 | `linux-musl-arm64` |
| macOS x64 | `osx-x64` |
| macOS ARM64 (Apple Silicon) | `osx-arm64` |

### Cross-Platform Publishing

```yaml
- name: Publish for multiple platforms
  run: |
    dotnet publish -c Release -r linux-x64 --self-contained -o ./publish/linux-x64
    dotnet publish -c Release -r win-x64 --self-contained -o ./publish/win-x64
    dotnet publish -c Release -r osx-x64 --self-contained -o ./publish/osx-x64
    dotnet publish -c Release -r osx-arm64 --self-contained -o ./publish/osx-arm64
```

### Self-Contained Single-File Publishing

```yaml
- name: Publish single-file executable
  run: |
    dotnet publish -c Release \
      -r linux-x64 \
      --self-contained true \
      -p:PublishSingleFile=true \
      -p:PublishTrimmed=true \
      -p:PublishReadyToRun=true \
      -o ./publish
```

**Cost Considerations**:
- Linux runners: 1x billing rate
- Windows runners: 2x billing rate
- macOS runners: 10x billing rate

**Sources**:
- [.NET Runtime Identifier (RID) catalog](https://learn.microsoft.com/en-us/dotnet/core/rid-catalog)
- [Matrix Builds with GitHub Actions](https://www.blacksmith.sh/blog/matrix-builds-with-github-actions)

---

## 3. GitHub Container Registry (ghcr.io)

### Overview

GitHub Container Registry (GHCR) provides seamless integration with GitHub repositories for storing Docker images.

### Authentication

```yaml
- name: Login to GitHub Container Registry
  uses: docker/login-action@v3
  with:
    registry: ghcr.io
    username: ${{ github.actor }}
    password: ${{ secrets.GITHUB_TOKEN }}
```

### Required Permissions

```yaml
permissions:
  contents: read
  packages: write
```

### Complete GHCR Workflow

```yaml
name: Build and Push to GHCR

on:
  push:
    branches: [ main ]
    tags: [ 'v*' ]

permissions:
  contents: read
  packages: write

env:
  REGISTRY: ghcr.io
  IMAGE_NAME: ${{ github.repository }}

jobs:
  build-and-push:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Login to GHCR
        uses: docker/login-action@v3
        with:
          registry: ${{ env.REGISTRY }}
          username: ${{ github.actor }}
          password: ${{ secrets.GITHUB_TOKEN }}

      - name: Extract metadata
        id: meta
        uses: docker/metadata-action@v5
        with:
          images: ${{ env.REGISTRY }}/${{ env.IMAGE_NAME }}
          tags: |
            type=ref,event=branch
            type=ref,event=pr
            type=semver,pattern={{version}}
            type=semver,pattern={{major}}.{{minor}}
            type=sha

      - name: Build and push
        uses: docker/build-push-action@v6
        with:
          context: .
          push: true
          tags: ${{ steps.meta.outputs.tags }}
          labels: ${{ steps.meta.outputs.labels }}
```

### Image Tagging Strategies

```yaml
- uses: docker/metadata-action@v5
  with:
    images: ghcr.io/${{ github.repository }}
    tags: |
      # Branch name
      type=ref,event=branch
      # PR number
      type=ref,event=pr
      # Semantic versioning from tags
      type=semver,pattern={{version}}
      type=semver,pattern={{major}}.{{minor}}
      type=semver,pattern={{major}}
      # Git SHA (short)
      type=sha
      # Latest tag for default branch
      type=raw,value=latest,enable={{is_default_branch}}
```

**Sources**:
- [Working with the Container registry - GitHub Docs](https://docs.github.com/en/packages/working-with-a-github-packages-registry/working-with-the-container-registry)
- [docker/login-action](https://github.com/docker/login-action)

---

## 4. Docker Build Actions

### Key Actions

| Action | Purpose |
|--------|---------|
| `docker/setup-buildx-action` | Set up Docker Buildx for advanced builds |
| `docker/setup-qemu-action` | Enable multi-architecture emulation |
| `docker/build-push-action` | Build and push Docker images |
| `docker/login-action` | Authenticate to registries |
| `docker/metadata-action` | Generate image tags and labels |

### Basic Build and Push

```yaml
- name: Set up Docker Buildx
  uses: docker/setup-buildx-action@v3

- name: Build and push
  uses: docker/build-push-action@v6
  with:
    context: .
    push: true
    tags: user/app:latest
```

### Multi-Architecture Builds

```yaml
- name: Set up QEMU
  uses: docker/setup-qemu-action@v3

- name: Set up Docker Buildx
  uses: docker/setup-buildx-action@v3

- name: Build and push multi-platform
  uses: docker/build-push-action@v6
  with:
    context: .
    push: true
    platforms: linux/amd64,linux/arm64,linux/arm/v7
    tags: user/app:latest
```

### Layer Caching

**GitHub Actions Cache (Recommended)**:

```yaml
- name: Build and push
  uses: docker/build-push-action@v6
  with:
    context: .
    push: true
    tags: user/app:latest
    cache-from: type=gha
    cache-to: type=gha,mode=max
```

**Registry Cache**:

```yaml
- name: Build and push
  uses: docker/build-push-action@v6
  with:
    context: .
    push: true
    tags: user/app:latest
    cache-from: type=registry,ref=user/app:buildcache
    cache-to: type=registry,ref=user/app:buildcache,mode=max
```

**Cache Modes**:
- `mode=min` (default): Only cache final layer outputs
- `mode=max`: Cache all intermediate layers (higher cache hit rate)

### Advanced Build Options

```yaml
- uses: docker/build-push-action@v6
  with:
    context: .
    file: ./Dockerfile.production
    push: true
    tags: ${{ steps.meta.outputs.tags }}
    labels: ${{ steps.meta.outputs.labels }}
    build-args: |
      VERSION=${{ github.ref_name }}
      BUILD_DATE=${{ github.event.head_commit.timestamp }}
    secrets: |
      "github_token=${{ secrets.GITHUB_TOKEN }}"
    cache-from: type=gha
    cache-to: type=gha,mode=max
    platforms: linux/amd64,linux/arm64
```

**Sources**:
- [docker/build-push-action](https://github.com/docker/build-push-action)
- [Cache management with GitHub Actions - Docker Docs](https://docs.docker.com/build/ci/github-actions/cache/)

---

## 5. GitHub Pages Deployment

### Required Actions

1. `actions/configure-pages` - Enables GitHub Pages support
2. `actions/upload-pages-artifact` - Packages static files for deployment
3. `actions/deploy-pages` - Deploys the artifact to GitHub Pages

### Basic Deployment Workflow

```yaml
name: Deploy to GitHub Pages

on:
  push:
    branches: [ main ]
  workflow_dispatch:

permissions:
  contents: read
  pages: write
  id-token: write

concurrency:
  group: "pages"
  cancel-in-progress: false

jobs:
  deploy:
    environment:
      name: github-pages
      url: ${{ steps.deployment.outputs.page_url }}
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Setup Pages
        uses: actions/configure-pages@v4

      - name: Upload artifact
        uses: actions/upload-pages-artifact@v3
        with:
          path: './docs'  # Directory to deploy

      - name: Deploy to GitHub Pages
        id: deployment
        uses: actions/deploy-pages@v4
```

### Two-Job Pattern (Build + Deploy)

```yaml
jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'

      - name: Build documentation
        run: dotnet tool restore && dotnet docfx docs/docfx.json

      - name: Upload artifact
        uses: actions/upload-pages-artifact@v3
        with:
          path: docs/_site

  deploy:
    needs: build
    runs-on: ubuntu-latest
    environment:
      name: github-pages
      url: ${{ steps.deployment.outputs.page_url }}
    steps:
      - name: Deploy to GitHub Pages
        id: deployment
        uses: actions/deploy-pages@v4
```

### Setup Requirements

1. Navigate to repository Settings > Pages
2. Set "Build and deployment" source to "GitHub Actions"
3. The `github-pages` environment must exist before workflow runs

**Sources**:
- [actions/deploy-pages](https://github.com/actions/deploy-pages)
- [Using custom workflows with GitHub Pages](https://docs.github.com/en/pages/getting-started-with-github-pages/using-custom-workflows-with-github-pages)

---

## 6. Testing .NET Applications

### Basic Test Workflow

```yaml
- name: Test
  run: dotnet test --no-build --verbosity normal
```

### Test with Coverage (Coverlet)

```yaml
- name: Test with coverage
  run: |
    dotnet test --no-build \
      --collect:"XPlat Code Coverage" \
      --results-directory ./coverage

- name: Upload coverage report
  uses: actions/upload-artifact@v4
  with:
    name: coverage-report
    path: coverage/**/coverage.cobertura.xml
```

### Generate HTML Coverage Report

```yaml
- name: Install ReportGenerator
  run: dotnet tool install -g dotnet-reportgenerator-globaltool

- name: Test with coverage
  run: dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura

- name: Generate coverage report
  run: |
    reportgenerator \
      -reports:**/coverage.cobertura.xml \
      -targetdir:CoverageReport \
      -reporttypes:Html

- name: Upload coverage report
  uses: actions/upload-artifact@v4
  with:
    name: coverage-report
    path: CoverageReport
```

### Test Result Reporting

Using `dorny/test-reporter` for beautiful test reports:

```yaml
- name: Test
  run: dotnet test --logger "trx;LogFileName=test-results.trx"

- name: Test Report
  uses: dorny/test-reporter@v1
  if: success() || failure()
  with:
    name: .NET Tests
    path: '**/test-results.trx'
    reporter: dotnet-trx
```

### Code Coverage Summary

```yaml
- name: Code Coverage Summary
  uses: irongut/CodeCoverageSummary@v1.3.0
  with:
    filename: '**/coverage.cobertura.xml'
    badge: true
    format: markdown
    output: both

- name: Add Coverage PR Comment
  uses: marocchino/sticky-pull-request-comment@v2
  if: github.event_name == 'pull_request'
  with:
    recreate: true
    path: code-coverage-results.md
```

### Required Permissions

```yaml
permissions:
  contents: read
  actions: read
  checks: write  # For test reporter
```

**Sources**:
- [Beautiful .NET Test Reports Using GitHub Actions](https://seankilleen.com/2024/03/beautiful-net-test-reports-using-github-actions/)
- [.NET Test and Coverage Reports in GitHub Actions](https://www.damirscorner.com/blog/posts/20240719-DotNetTestAndCoverageReportsInGitHubActions.html)

---

## 7. Integration Testing with Docker

### GitHub Service Containers

Service containers are Docker containers that provide services (databases, caches, etc.) for your workflow:

```yaml
jobs:
  integration-tests:
    runs-on: ubuntu-latest

    services:
      postgres:
        image: postgres:16
        env:
          POSTGRES_USER: test
          POSTGRES_PASSWORD: test
          POSTGRES_DB: testdb
        ports:
          - 5432:5432
        options: >-
          --health-cmd pg_isready
          --health-interval 10s
          --health-timeout 5s
          --health-retries 5

      redis:
        image: redis:7
        ports:
          - 6379:6379
        options: >-
          --health-cmd "redis-cli ping"
          --health-interval 10s
          --health-timeout 5s
          --health-retries 5

    steps:
      - uses: actions/checkout@v4

      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'

      - name: Run integration tests
        run: dotnet test --filter "Category=Integration"
        env:
          ConnectionStrings__Database: "Host=localhost;Port=5432;Database=testdb;Username=test;Password=test"
          ConnectionStrings__Redis: "localhost:6379"
```

### PostgreSQL Service Container

```yaml
services:
  postgres:
    image: postgres:16-alpine
    env:
      POSTGRES_USER: postgres
      POSTGRES_PASSWORD: postgres
      POSTGRES_DB: testdb
    ports:
      - 5432:5432
    options: >-
      --health-cmd pg_isready
      --health-interval 10s
      --health-timeout 5s
      --health-retries 5
```

### Ollama Service Container

```yaml
services:
  ollama:
    image: ollama/ollama:latest
    ports:
      - 11434:11434
    options: >-
      --health-cmd "curl -f http://localhost:11434/api/tags || exit 1"
      --health-interval 30s
      --health-timeout 10s
      --health-retries 5
```

### Docker Compose in Actions

```yaml
- name: Start services
  run: docker compose -f docker-compose.test.yml up -d

- name: Wait for services
  run: |
    until docker compose -f docker-compose.test.yml ps | grep -q "healthy"; do
      echo "Waiting for services..."
      sleep 5
    done

- name: Run tests
  run: dotnet test --filter "Category=Integration"

- name: Stop services
  if: always()
  run: docker compose -f docker-compose.test.yml down -v
```

**Sources**:
- [Creating PostgreSQL service containers - GitHub Docs](https://docs.github.com/actions/guides/creating-postgresql-service-containers)
- [Docker Compose with Tests Action](https://github.com/marketplace/actions/docker-compose-with-tests-action)

---

## 8. E2E Testing with Testcontainers

### Overview

Testcontainers for .NET provides throwaway instances of Docker containers for testing, ensuring tests run with real dependencies.

### Basic Usage

```csharp
using Testcontainers.PostgreSql;

public class IntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
    }

    [Fact]
    public async Task DatabaseTest()
    {
        var connectionString = _postgres.GetConnectionString();
        // Run tests...
    }
}
```

### GitHub Actions Configuration

Testcontainers works out of the box on GitHub-hosted runners since Docker is pre-installed:

```yaml
jobs:
  e2e-tests:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'

      - name: Run E2E tests
        run: dotnet test --filter "Category=E2E"
```

### Using Testcontainers Cloud

For faster execution, offload container management to Testcontainers Cloud:

```yaml
- name: Setup Testcontainers Cloud
  uses: atomicjar/testcontainers-cloud-setup-action@v1
  with:
    token: ${{ secrets.TC_CLOUD_TOKEN }}

- name: Run E2E tests
  run: dotnet test --filter "Category=E2E"
```

### WebApplicationFactory with Testcontainers

```csharp
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder().Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.AddDbContext<AppDbContext>(options =>
                options.UseNpgsql(_postgres.GetConnectionString()));
        });
    }

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
    }
}
```

**Sources**:
- [Testcontainers for .NET CI/CD](https://dotnet.testcontainers.org/cicd/)
- [Running Testcontainers Tests Using GitHub Actions](https://www.docker.com/blog/running-testcontainers-tests-using-github-actions/)

---

## 9. Caching Strategies

### NuGet Package Caching

**Using setup-dotnet built-in caching** (requires lock file):

```yaml
- uses: actions/setup-dotnet@v4
  with:
    dotnet-version: '8.0.x'
    cache: true

- run: dotnet restore --locked-mode
```

To enable lock files, add to your `.csproj`:

```xml
<PropertyGroup>
  <RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>
  <DisableImplicitNuGetFallbackFolder>true</DisableImplicitNuGetFallbackFolder>
</PropertyGroup>
```

**Using actions/cache directly**:

```yaml
env:
  NUGET_PACKAGES: ${{ github.workspace }}/.nuget/packages

steps:
  - uses: actions/checkout@v4

  - uses: actions/cache@v4
    with:
      path: ${{ env.NUGET_PACKAGES }}
      key: ${{ runner.os }}-nuget-${{ hashFiles('**/*.csproj', '**/packages.lock.json') }}
      restore-keys: |
        ${{ runner.os }}-nuget-

  - run: dotnet restore
```

### Docker Layer Caching

**GitHub Actions Cache**:

```yaml
- uses: docker/build-push-action@v6
  with:
    context: .
    push: true
    tags: user/app:latest
    cache-from: type=gha
    cache-to: type=gha,mode=max
```

**Registry-based Cache**:

```yaml
- uses: docker/build-push-action@v6
  with:
    context: .
    push: true
    tags: ghcr.io/owner/app:latest
    cache-from: type=registry,ref=ghcr.io/owner/app:buildcache
    cache-to: type=registry,ref=ghcr.io/owner/app:buildcache,mode=max
```

### Cache Size Limits

- GitHub Actions cache limit: 10 GB per repository
- Caches not accessed in 7 days are automatically evicted
- Use `restore-keys` for partial cache matches

**Sources**:
- [Caching NuGet packages in GitHub Actions](https://www.damirscorner.com/blog/posts/20240726-CachingNuGetPackagesInGitHubActions.html)
- [GitHub Actions cache - Docker Docs](https://docs.docker.com/build/cache/backends/gha/)

---

## 10. Release Automation

### Creating Releases from Tags

```yaml
name: Release

on:
  push:
    tags:
      - 'v*'

permissions:
  contents: write

jobs:
  release:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Create Release
        uses: softprops/action-gh-release@v2
        with:
          generate_release_notes: true
          draft: false
          prerelease: ${{ contains(github.ref, '-') }}
```

### Building and Uploading Release Assets

```yaml
name: Release

on:
  push:
    tags:
      - 'v*'

permissions:
  contents: write

jobs:
  build:
    strategy:
      matrix:
        include:
          - os: ubuntu-latest
            rid: linux-x64
            artifact: myapp-linux-x64
          - os: windows-latest
            rid: win-x64
            artifact: myapp-win-x64.exe
          - os: macos-latest
            rid: osx-x64
            artifact: myapp-osx-x64

    runs-on: ${{ matrix.os }}
    steps:
      - uses: actions/checkout@v4

      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'

      - name: Publish
        run: |
          dotnet publish -c Release \
            -r ${{ matrix.rid }} \
            --self-contained \
            -p:PublishSingleFile=true \
            -o ./publish

      - name: Upload artifact
        uses: actions/upload-artifact@v4
        with:
          name: ${{ matrix.artifact }}
          path: ./publish

  release:
    needs: build
    runs-on: ubuntu-latest
    steps:
      - name: Download all artifacts
        uses: actions/download-artifact@v4
        with:
          path: ./artifacts

      - name: Create Release
        uses: softprops/action-gh-release@v2
        with:
          files: ./artifacts/**/*
          generate_release_notes: true
```

### Using svenstaro/upload-release-action

```yaml
- name: Upload Release Asset
  uses: svenstaro/upload-release-action@v2
  with:
    repo_token: ${{ secrets.GITHUB_TOKEN }}
    file: ./publish/myapp
    asset_name: myapp-${{ matrix.rid }}
    tag: ${{ github.ref }}
    overwrite: true
```

**Sources**:
- [svenstaro/upload-release-action](https://github.com/svenstaro/upload-release-action)
- [softprops/action-gh-release](https://github.com/softprops/action-gh-release)

---

## 11. Complete Workflow Examples

### Multi-Platform Build Workflow

```yaml
name: Build and Test

on:
  push:
    branches: [ main ]
  pull_request:
    branches: [ main ]

permissions:
  contents: read
  checks: write

env:
  DOTNET_SKIP_FIRST_TIME_EXPERIENCE: true
  DOTNET_NOLOGO: true
  NUGET_PACKAGES: ${{ github.workspace }}/.nuget/packages

jobs:
  build:
    runs-on: ${{ matrix.os }}
    strategy:
      fail-fast: false
      matrix:
        os: [ubuntu-latest, windows-latest, macos-latest]
        dotnet-version: ['8.0.x']
        include:
          - os: ubuntu-latest
            rid: linux-x64
          - os: windows-latest
            rid: win-x64
          - os: macos-latest
            rid: osx-arm64

    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: ${{ matrix.dotnet-version }}
          cache: true

      - name: Restore dependencies
        run: dotnet restore

      - name: Build
        run: dotnet build --no-restore --configuration Release

      - name: Test
        run: dotnet test --no-build --configuration Release --logger "trx;LogFileName=test-results.trx"

      - name: Test Report
        uses: dorny/test-reporter@v1
        if: success() || failure()
        with:
          name: Test Results (${{ matrix.os }})
          path: '**/test-results.trx'
          reporter: dotnet-trx

      - name: Publish
        run: |
          dotnet publish src/MyApp/MyApp.csproj \
            -c Release \
            -r ${{ matrix.rid }} \
            --self-contained \
            -p:PublishSingleFile=true \
            -o ./publish

      - name: Upload artifact
        uses: actions/upload-artifact@v4
        with:
          name: myapp-${{ matrix.rid }}
          path: ./publish
```

### Docker Build and Push Workflow

```yaml
name: Docker Build and Push

on:
  push:
    branches: [ main ]
    tags: [ 'v*' ]
  pull_request:
    branches: [ main ]

permissions:
  contents: read
  packages: write

env:
  REGISTRY: ghcr.io
  IMAGE_NAME: ${{ github.repository }}

jobs:
  build-and-push:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Set up QEMU
        uses: docker/setup-qemu-action@v3

      - name: Set up Docker Buildx
        uses: docker/setup-buildx-action@v3

      - name: Login to GHCR
        if: github.event_name != 'pull_request'
        uses: docker/login-action@v3
        with:
          registry: ${{ env.REGISTRY }}
          username: ${{ github.actor }}
          password: ${{ secrets.GITHUB_TOKEN }}

      - name: Extract metadata
        id: meta
        uses: docker/metadata-action@v5
        with:
          images: ${{ env.REGISTRY }}/${{ env.IMAGE_NAME }}
          tags: |
            type=ref,event=branch
            type=ref,event=pr
            type=semver,pattern={{version}}
            type=semver,pattern={{major}}.{{minor}}
            type=sha
            type=raw,value=latest,enable={{is_default_branch}}

      - name: Build and push
        uses: docker/build-push-action@v6
        with:
          context: .
          platforms: linux/amd64,linux/arm64
          push: ${{ github.event_name != 'pull_request' }}
          tags: ${{ steps.meta.outputs.tags }}
          labels: ${{ steps.meta.outputs.labels }}
          cache-from: type=gha
          cache-to: type=gha,mode=max
```

### Integration Test Workflow with Docker Compose

```yaml
name: Integration Tests

on:
  push:
    branches: [ main ]
  pull_request:
    branches: [ main ]

permissions:
  contents: read

env:
  DOTNET_SKIP_FIRST_TIME_EXPERIENCE: true
  DOTNET_NOLOGO: true

jobs:
  integration-tests:
    runs-on: ubuntu-latest

    services:
      postgres:
        image: postgres:16-alpine
        env:
          POSTGRES_USER: test
          POSTGRES_PASSWORD: test
          POSTGRES_DB: testdb
        ports:
          - 5432:5432
        options: >-
          --health-cmd pg_isready
          --health-interval 10s
          --health-timeout 5s
          --health-retries 5

      redis:
        image: redis:7-alpine
        ports:
          - 6379:6379
        options: >-
          --health-cmd "redis-cli ping"
          --health-interval 10s
          --health-timeout 5s
          --health-retries 5

    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'
          cache: true

      - name: Restore dependencies
        run: dotnet restore

      - name: Build
        run: dotnet build --no-restore

      - name: Run unit tests
        run: dotnet test --no-build --filter "Category!=Integration"

      - name: Run integration tests
        run: dotnet test --no-build --filter "Category=Integration"
        env:
          ConnectionStrings__Database: "Host=localhost;Port=5432;Database=testdb;Username=test;Password=test"
          ConnectionStrings__Redis: "localhost:6379"
```

### Complete CI/CD Pipeline

```yaml
name: CI/CD Pipeline

on:
  push:
    branches: [ main ]
    tags: [ 'v*' ]
  pull_request:
    branches: [ main ]

permissions:
  contents: write
  packages: write
  checks: write
  pages: write
  id-token: write

env:
  DOTNET_SKIP_FIRST_TIME_EXPERIENCE: true
  DOTNET_NOLOGO: true
  REGISTRY: ghcr.io
  IMAGE_NAME: ${{ github.repository }}

jobs:
  # Build and Test
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'
          cache: true

      - run: dotnet restore
      - run: dotnet build --no-restore -c Release
      - run: dotnet test --no-build -c Release --collect:"XPlat Code Coverage" --logger "trx"

      - name: Test Report
        uses: dorny/test-reporter@v1
        if: success() || failure()
        with:
          name: Test Results
          path: '**/TestResults/*.trx'
          reporter: dotnet-trx

      - name: Upload coverage
        uses: actions/upload-artifact@v4
        with:
          name: coverage
          path: '**/coverage.cobertura.xml'

  # Build Docker Image
  docker:
    needs: build
    runs-on: ubuntu-latest
    if: github.event_name != 'pull_request'
    steps:
      - uses: actions/checkout@v4

      - uses: docker/setup-qemu-action@v3
      - uses: docker/setup-buildx-action@v3

      - uses: docker/login-action@v3
        with:
          registry: ${{ env.REGISTRY }}
          username: ${{ github.actor }}
          password: ${{ secrets.GITHUB_TOKEN }}

      - uses: docker/metadata-action@v5
        id: meta
        with:
          images: ${{ env.REGISTRY }}/${{ env.IMAGE_NAME }}
          tags: |
            type=semver,pattern={{version}}
            type=sha
            type=raw,value=latest,enable={{is_default_branch}}

      - uses: docker/build-push-action@v6
        with:
          context: .
          platforms: linux/amd64,linux/arm64
          push: true
          tags: ${{ steps.meta.outputs.tags }}
          cache-from: type=gha
          cache-to: type=gha,mode=max

  # Build Release Artifacts
  release-artifacts:
    needs: build
    if: startsWith(github.ref, 'refs/tags/v')
    strategy:
      matrix:
        include:
          - os: ubuntu-latest
            rid: linux-x64
          - os: ubuntu-latest
            rid: linux-arm64
          - os: windows-latest
            rid: win-x64
          - os: macos-latest
            rid: osx-x64
          - os: macos-latest
            rid: osx-arm64
    runs-on: ${{ matrix.os }}
    steps:
      - uses: actions/checkout@v4

      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'

      - name: Publish
        run: |
          dotnet publish src/MyApp/MyApp.csproj \
            -c Release \
            -r ${{ matrix.rid }} \
            --self-contained \
            -p:PublishSingleFile=true \
            -p:PublishTrimmed=true \
            -o ./publish

      - uses: actions/upload-artifact@v4
        with:
          name: myapp-${{ matrix.rid }}
          path: ./publish

  # Create GitHub Release
  release:
    needs: release-artifacts
    if: startsWith(github.ref, 'refs/tags/v')
    runs-on: ubuntu-latest
    steps:
      - uses: actions/download-artifact@v4
        with:
          path: ./artifacts

      - name: Prepare release files
        run: |
          cd artifacts
          for dir in */; do
            name="${dir%/}"
            zip -r "../${name}.zip" "$dir"
          done

      - uses: softprops/action-gh-release@v2
        with:
          files: '*.zip'
          generate_release_notes: true
          draft: false

  # Deploy Documentation
  docs:
    needs: build
    if: github.ref == 'refs/heads/main'
    runs-on: ubuntu-latest
    environment:
      name: github-pages
      url: ${{ steps.deployment.outputs.page_url }}
    steps:
      - uses: actions/checkout@v4

      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'

      - name: Build docs
        run: |
          dotnet tool restore
          dotnet docfx docs/docfx.json

      - uses: actions/configure-pages@v4
      - uses: actions/upload-pages-artifact@v3
        with:
          path: docs/_site

      - uses: actions/deploy-pages@v4
        id: deployment
```

---

## Summary

This research covers the essential components for building a comprehensive CI/CD pipeline for .NET applications using GitHub Actions:

1. **SDK Setup**: Use `actions/setup-dotnet` with version flexibility and built-in caching
2. **Multi-Platform**: Matrix strategy for cross-platform builds with appropriate RIDs
3. **Container Registry**: GHCR integration with `docker/login-action` and `GITHUB_TOKEN`
4. **Docker Builds**: `docker/build-push-action` with Buildx for multi-arch support
5. **GitHub Pages**: Three-action pattern for static site deployment
6. **Testing**: Coverlet for coverage, `dorny/test-reporter` for beautiful reports
7. **Service Containers**: Native GitHub Actions feature for database/service dependencies
8. **Testcontainers**: Real dependencies in isolated containers for E2E testing
9. **Caching**: NuGet packages via lock files, Docker layers via GHA cache
10. **Releases**: Automated asset building and release creation from tags

---

## References

### Official Documentation
- [GitHub Actions Documentation](https://docs.github.com/en/actions)
- [Building and testing .NET - GitHub Docs](https://docs.github.com/actions/guides/building-and-testing-net)
- [Docker GitHub Actions Documentation](https://docs.docker.com/build/ci/github-actions/)
- [.NET Runtime Identifier (RID) catalog](https://learn.microsoft.com/en-us/dotnet/core/rid-catalog)

### Key Actions
- [actions/setup-dotnet](https://github.com/actions/setup-dotnet)
- [docker/build-push-action](https://github.com/docker/build-push-action)
- [actions/deploy-pages](https://github.com/actions/deploy-pages)
- [dorny/test-reporter](https://github.com/dorny/test-reporter)
- [softprops/action-gh-release](https://github.com/softprops/action-gh-release)

### Community Resources
- [Matrix Builds with GitHub Actions - Blacksmith](https://www.blacksmith.sh/blog/matrix-builds-with-github-actions)
- [Cache management with GitHub Actions - Docker Docs](https://docs.docker.com/build/ci/github-actions/cache/)
- [Testcontainers for .NET CI/CD](https://dotnet.testcontainers.org/cicd/)
- [Beautiful .NET Test Reports Using GitHub Actions](https://seankilleen.com/2024/03/beautiful-net-test-reports-using-github-actions/)
