# Phase 125: semantic-release Workflow

> **Status**: NOT_STARTED
> **Effort Estimate**: 3-4 hours
> **Category**: Testing Framework
> **Prerequisites**: Phase 121 (Test Workflow)

---

## Spec References

This phase implements the automated release workflow defined in:

- **spec/testing/ci-cd-pipeline.md** - [Release Workflow (semantic-release)](../spec/testing/ci-cd-pipeline.md#release-workflow-semantic-release)
- **research/github-actions-dotnet-cicd-research.md** - Release automation patterns and GitHub Actions best practices

---

## Objectives

1. Create `.github/workflows/release.yml` GitHub Actions workflow
2. Configure semantic-release for automated versioning based on Conventional Commits
3. Set up `.releaserc.json` configuration file with appropriate plugins
4. Create `package.json` with required Node.js dependencies
5. Configure automatic version updates for `Directory.Build.props` (.NET assembly version)
6. Configure automatic version updates for `.claude-plugin/plugin.json` (plugin manifest)
7. Configure automatic CHANGELOG.md generation from commit history

---

## Acceptance Criteria

- [ ] `.github/workflows/release.yml` created with:
  - [ ] Trigger on push to `main` branch only
  - [ ] Appropriate permissions (`contents: write`, `issues: write`, `pull-requests: write`)
  - [ ] Checkout action with `fetch-depth: 0` for full git history
  - [ ] Node.js setup step (LTS version)
  - [ ] .NET 10.0 SDK setup step
  - [ ] npm dependency installation (`npm clean-install`)
  - [ ] .NET build step (`dotnet build --configuration Release`)
  - [ ] .NET test step (`dotnet test --configuration Release --no-build`)
  - [ ] semantic-release execution (`npx semantic-release`)
- [ ] `.releaserc.json` created with plugins:
  - [ ] `@semantic-release/commit-analyzer` - Conventional Commits parsing
  - [ ] `@semantic-release/release-notes-generator` - Release notes from commits
  - [ ] `@semantic-release/changelog` - CHANGELOG.md generation
  - [ ] `semantic-release-dotnet` - Directory.Build.props version update
  - [ ] `@google/semantic-release-replace-plugin` - plugin.json version update
  - [ ] `@semantic-release/git` - Commit updated files back to repo
  - [ ] `@semantic-release/github` - Create GitHub Release
- [ ] `package.json` created at repository root with:
  - [ ] `private: true` to prevent accidental npm publishing
  - [ ] All semantic-release plugin dependencies as devDependencies
  - [ ] Version pinned to `0.0.0-development` (semantic-release manages actual version)
- [ ] Version bump behavior matches Conventional Commits:
  - [ ] `fix:` commits trigger PATCH version bump (0.0.X)
  - [ ] `feat:` commits trigger MINOR version bump (0.X.0)
  - [ ] `feat!:` or `BREAKING CHANGE` trigger MAJOR version bump (X.0.0)
  - [ ] `docs:`, `chore:`, `test:` commits do NOT trigger a release
- [ ] Release commit message format: `chore(release): ${nextRelease.version} [skip ci]`
- [ ] Git tag format: `v${version}` (e.g., `v1.2.3`)
- [ ] `CHANGELOG.md` auto-generated from commit history
- [ ] Workflow uses `GITHUB_TOKEN` (no custom secrets required)

---

## Implementation Notes

### GitHub Actions Workflow

Create `.github/workflows/release.yml`:

```yaml
name: Release

on:
  push:
    branches: [main]

permissions:
  contents: read

jobs:
  release:
    name: Release
    runs-on: ubuntu-latest
    permissions:
      contents: write
      issues: write
      pull-requests: write

    steps:
      - name: Checkout
        uses: actions/checkout@v4
        with:
          fetch-depth: 0
          persist-credentials: false

      - name: Setup Node.js
        uses: actions/setup-node@v4
        with:
          node-version: 'lts/*'

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Install dependencies
        run: npm clean-install

      - name: Build .NET project
        run: dotnet build --configuration Release

      - name: Run tests
        run: dotnet test --configuration Release --no-build

      - name: Release
        env:
          GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}
        run: npx semantic-release
```

**Key Configuration Notes**:
- `fetch-depth: 0` ensures full git history is available for semantic-release to analyze commits
- `persist-credentials: false` allows semantic-release to use its own authentication
- Job-level permissions override workflow-level permissions for security
- Tests must pass before release is created

### semantic-release Configuration

Create `.releaserc.json` at repository root:

```json
{
  "branches": ["main"],
  "tagFormat": "v${version}",
  "plugins": [
    "@semantic-release/commit-analyzer",
    "@semantic-release/release-notes-generator",
    [
      "@semantic-release/changelog",
      { "changelogFile": "CHANGELOG.md" }
    ],
    [
      "semantic-release-dotnet",
      { "paths": ["Directory.Build.props"] }
    ],
    [
      "@google/semantic-release-replace-plugin",
      {
        "replacements": [
          {
            "files": [".claude-plugin/plugin.json"],
            "from": "\"version\": \".*\"",
            "to": "\"version\": \"${nextRelease.version}\""
          }
        ]
      }
    ],
    [
      "@semantic-release/git",
      {
        "assets": [
          "CHANGELOG.md",
          "Directory.Build.props",
          ".claude-plugin/plugin.json"
        ],
        "message": "chore(release): ${nextRelease.version} [skip ci]"
      }
    ],
    "@semantic-release/github"
  ]
}
```

**Plugin Order Matters**: Plugins execute in order listed. The sequence is:
1. **commit-analyzer** - Determine version bump type from commits
2. **release-notes-generator** - Generate release notes content
3. **changelog** - Write CHANGELOG.md file
4. **semantic-release-dotnet** - Update .NET version in Directory.Build.props
5. **replace-plugin** - Update plugin.json version
6. **git** - Commit all changes and push
7. **github** - Create GitHub Release with notes

### Node.js Package Configuration

Create `package.json` at repository root:

```json
{
  "name": "csharp-compounding-docs",
  "version": "0.0.0-development",
  "private": true,
  "devDependencies": {
    "semantic-release": "^24.0.0",
    "@semantic-release/changelog": "^6.0.3",
    "@semantic-release/git": "^10.0.1",
    "@semantic-release/github": "^10.0.0",
    "@google/semantic-release-replace-plugin": "^1.2.7",
    "semantic-release-dotnet": "^3.0.0"
  }
}
```

**Note**: The `version` field is set to `0.0.0-development` as a placeholder. semantic-release does not use this version; it determines the version from git tags and commits.

### Directory.Build.props Version Property

Ensure `Directory.Build.props` has a version property that semantic-release-dotnet can update:

```xml
<Project>
  <PropertyGroup>
    <Version>0.0.0</Version>
    <!-- Other properties -->
  </PropertyGroup>
</Project>
```

The `semantic-release-dotnet` plugin will update the `<Version>` element to match the release version.

### plugin.json Version Field

Ensure `.claude-plugin/plugin.json` has a version field:

```json
{
  "name": "csharp-compounding-docs",
  "version": "0.0.0",
  ...
}
```

The `@google/semantic-release-replace-plugin` uses regex to update the version string.

### Conventional Commits Examples

The project follows [Conventional Commits](https://www.conventionalcommits.org/):

```bash
# Bug fix (PATCH release: 0.0.X -> 0.0.X+1)
git commit -m "fix(mcp): handle empty query gracefully"
git commit -m "fix: resolve null reference in chunking service"

# New feature (MINOR release: 0.X.0 -> 0.X+1.0)
git commit -m "feat(skills): add /cdocs:export skill"
git commit -m "feat: implement fuzzy search support"

# Breaking change (MAJOR release: X.0.0 -> X+1.0.0)
git commit -m "feat(api)!: change tool response schema

BREAKING CHANGE: All tool responses now include metadata field"

# Non-release commits (no version bump)
git commit -m "docs: update README installation instructions"
git commit -m "chore: update dependencies"
git commit -m "test: add unit tests for embedding service"
git commit -m "refactor: extract validation logic to helper"
```

### .gitignore Updates

Add Node.js artifacts to `.gitignore`:

```gitignore
# Node.js
node_modules/
package-lock.json
```

**Note**: `package-lock.json` is excluded since this project is not a Node.js application. The semantic-release dependencies are development tools only. If reproducible builds become important, this decision can be revisited.

### CHANGELOG.md Initial State

Create an initial `CHANGELOG.md` file (semantic-release will manage it):

```markdown
# Changelog

All notable changes to this project will be documented in this file.

This project adheres to [Semantic Versioning](https://semver.org/).

<!-- semantic-release will automatically update this file -->
```

---

## Dependencies

### Depends On
- **Phase 121**: Test Workflow (test.yml must exist and pass for releases)
- **Phase 009**: Plugin Directory (plugin.json must exist at .claude-plugin/plugin.json)
- **Phase 001**: Solution Structure (Directory.Build.props must exist)

### Blocks
- **Phase 126+**: Any phases requiring automated releases
- Coverage deployment to GitHub Pages (release-triggered coverage publishing)

---

## Verification Steps

After completing this phase, verify:

1. **Configuration files exist**:
   ```bash
   ls -la .github/workflows/release.yml
   ls -la .releaserc.json
   ls -la package.json
   ls -la CHANGELOG.md
   ```

2. **package.json is valid JSON**:
   ```bash
   node -e "require('./package.json')"
   ```

3. **.releaserc.json is valid JSON**:
   ```bash
   node -e "require('./.releaserc.json')"
   ```

4. **npm install succeeds**:
   ```bash
   npm install
   ```

5. **semantic-release dry run** (requires GitHub token locally):
   ```bash
   npx semantic-release --dry-run
   ```

6. **Version placeholders exist**:
   ```bash
   grep -E "<Version>" Directory.Build.props
   grep -E '"version":' .claude-plugin/plugin.json
   ```

7. **Workflow YAML is valid**:
   ```bash
   # GitHub CLI validation (if available)
   gh workflow view release.yml
   ```

---

## Key Technical Decisions

### Branch Configuration

| Setting | Value | Rationale |
|---------|-------|-----------|
| Release branch | `main` | Single main branch workflow; PRs merge to main |
| Tag format | `v${version}` | Industry standard (v1.0.0, v2.1.3) |
| Pre-release branches | None | Keeping release strategy simple initially |

### Plugin Selection

| Plugin | Purpose | Alternatives Considered |
|--------|---------|------------------------|
| `@semantic-release/commit-analyzer` | Parse Conventional Commits | Built-in, no alternatives |
| `@semantic-release/release-notes-generator` | Generate release notes | Built-in, no alternatives |
| `@semantic-release/changelog` | Update CHANGELOG.md | `conventional-changelog-cli` (manual) |
| `semantic-release-dotnet` | Update .NET version | `@google/semantic-release-replace-plugin` (regex) |
| `@google/semantic-release-replace-plugin` | Update arbitrary files | `semantic-release-exec` (scripts) |
| `@semantic-release/git` | Commit back to repo | Required for multi-file updates |
| `@semantic-release/github` | Create GitHub Release | Built-in, no alternatives |

### Authentication

| Method | Decision | Rationale |
|--------|----------|-----------|
| `GITHUB_TOKEN` | **Selected** | Automatic, no secret management |
| Personal Access Token | Rejected | Requires manual secret rotation |

**Note**: `GITHUB_TOKEN` has sufficient permissions for creating releases and pushing commits when workflow permissions are correctly configured.

### Commit Message for Releases

| Format | Decision | Rationale |
|--------|----------|-----------|
| `chore(release): ${version} [skip ci]` | **Selected** | Prevents infinite release loop, clear intent |

The `[skip ci]` suffix prevents the release commit from triggering another workflow run.

---

## Troubleshooting

### Common Issues

**Issue**: semantic-release fails with "No commits found"
**Solution**: Ensure `fetch-depth: 0` in checkout action for full git history

**Issue**: Release commit fails to push
**Solution**: Verify `persist-credentials: false` and job permissions include `contents: write`

**Issue**: Version not updated in Directory.Build.props
**Solution**: Verify `<Version>` element exists in PropertyGroup; ensure semantic-release-dotnet is in plugin list before @semantic-release/git

**Issue**: Duplicate releases created
**Solution**: Ensure `[skip ci]` is in the release commit message to prevent workflow re-trigger

**Issue**: CHANGELOG.md not updated
**Solution**: Ensure CHANGELOG.md exists (even empty) and is listed in @semantic-release/git assets

---

## Future Enhancements

1. **Branch protection rules**: Require PR reviews before merge to main
2. **Pre-release support**: Add `beta` or `next` branches for pre-releases
3. **NPM publishing**: If plugin becomes an npm package, add @semantic-release/npm
4. **Docker publishing**: Add ghcr.io container publishing on release
5. **Coverage publishing**: Add GitHub Pages coverage deployment triggered by releases

---

## Notes

- semantic-release is the industry standard for automated versioning in open source projects
- The `[skip ci]` convention is critical to prevent infinite workflow loops
- Consider adding commitlint to enforce Conventional Commits in PRs (future phase)
- The workflow runs on every push to main; releases only occur when commits warrant a version bump
- Non-release commits (docs, chore, test, refactor) will cause the workflow to succeed but skip release creation
