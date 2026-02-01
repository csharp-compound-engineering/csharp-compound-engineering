# Release Checklist

This document outlines the complete checklist for preparing, executing, and verifying releases of the CSharp Compound Docs MCP plugin.

## Table of Contents

- [Pre-Release Checklist](#pre-release-checklist)
- [Release Process](#release-process)
- [Post-Release Verification](#post-release-verification)
- [Rollback Procedures](#rollback-procedures)
- [Version Numbering](#version-numbering)

---

## Pre-Release Checklist

Complete all items before creating a release.

### Code Quality

- [ ] All unit tests pass
  ```bash
  dotnet test tests/CompoundDocs.Tests --configuration Release
  ```

- [ ] All integration tests pass
  ```bash
  dotnet test tests/CompoundDocs.IntegrationTests --configuration Release
  ```

- [ ] All E2E tests pass
  ```bash
  dotnet test tests/CompoundDocs.E2ETests --configuration Release
  ```

- [ ] Code coverage meets threshold (100% line, branch, method)
  ```bash
  dotnet test /p:CollectCoverage=true /p:Threshold=100
  ```

### Security

- [ ] No known security vulnerabilities
  ```bash
  dotnet list package --vulnerable
  ```

- [ ] No outdated packages with security patches
  ```bash
  dotnet list package --outdated
  ```

- [ ] Secrets scanning passed (no hardcoded credentials)

- [ ] Dependencies reviewed for license compliance

### Documentation

- [ ] CHANGELOG.md updated with version notes
  - Added new features documented
  - Changed behaviors documented
  - Deprecated items noted
  - Fixed bugs listed
  - Security fixes highlighted

- [ ] README.md reflects current functionality

- [ ] API documentation is accurate
  - All 9 MCP tools documented
  - Parameters and return types current
  - Examples tested

- [ ] Migration guide prepared (if breaking changes)

### Version Updates

- [ ] Version number updated in `marketplace/manifest.json`

- [ ] Version number updated in all `.csproj` files
  ```xml
  <Version>x.y.z</Version>
  <AssemblyVersion>x.y.z</AssemblyVersion>
  <FileVersion>x.y.z</FileVersion>
  ```

- [ ] Version number updated in `SPEC.md` header

- [ ] Docker image tags prepared

### Build Verification

- [ ] Solution builds without warnings
  ```bash
  dotnet build --configuration Release --warnaserror
  ```

- [ ] Docker images build successfully
  ```bash
  docker compose -f docker/docker-compose.yml build
  ```

- [ ] Published binaries work on all target platforms
  - Windows x64
  - macOS x64
  - macOS arm64
  - Linux x64

### Database

- [ ] Liquibase migrations tested on clean database
  ```bash
  docker compose up postgres -d
  # Wait for initialization
  docker compose logs postgres | grep "database system is ready"
  ```

- [ ] Liquibase migrations tested on existing database (upgrade path)

- [ ] Rollback scripts tested for new migrations

### Performance

- [ ] Performance benchmarks within acceptable bounds
  - Embedding generation: < 2s per document
  - Vector search: < 500ms for 10k documents
  - RAG query: < 5s total response time

- [ ] Memory usage within bounds
  - MCP server idle: < 100MB
  - Peak during indexing: < 500MB

---

## Release Process

### Step 1: Create Release Branch

```bash
git checkout master
git pull origin master
git checkout -b release/v{version}
```

### Step 2: Final Version Bump

Update all version references:

```bash
# Update version in manifest
sed -i 's/"version": ".*"/"version": "{version}"/' marketplace/manifest.json

# Update .csproj files
find src -name "*.csproj" -exec sed -i 's/<Version>.*<\/Version>/<Version>{version}<\/Version>/' {} \;
```

### Step 3: Generate Changelog Entry

Ensure CHANGELOG.md has an entry for this version:

```markdown
## [x.y.z] - YYYY-MM-DD

### Added
- Feature descriptions

### Changed
- Modification descriptions

### Fixed
- Bug fix descriptions

### Security
- Security-related changes
```

### Step 4: Create Pull Request

```bash
git add -A
git commit -m "chore: prepare release v{version}"
git push -u origin release/v{version}
gh pr create --title "Release v{version}" --body "Release preparation for version {version}"
```

### Step 5: Merge and Tag

After PR approval:

```bash
gh pr merge --squash
git checkout master
git pull origin master
git tag -a v{version} -m "Release v{version}"
git push origin v{version}
```

### Step 6: Create GitHub Release

```bash
gh release create v{version} \
  --title "v{version}" \
  --notes-file CHANGELOG.md \
  --latest
```

### Step 7: Publish Docker Images

```bash
docker compose -f docker/docker-compose.yml build
docker tag csharp-compounding-docs-postgres:latest ghcr.io/{org}/csharp-compounding-docs-postgres:v{version}
docker push ghcr.io/{org}/csharp-compounding-docs-postgres:v{version}
```

### Step 8: Update Marketplace

- [ ] Update marketplace listing
- [ ] Upload new screenshots if UI changed
- [ ] Update feature descriptions

---

## Post-Release Verification

### Immediate Verification (within 1 hour)

- [ ] GitHub release page accessible
- [ ] Release assets downloadable
- [ ] Docker images pullable
  ```bash
  docker pull ghcr.io/{org}/csharp-compounding-docs-postgres:v{version}
  ```

- [ ] Fresh installation works
  1. Clone repository
  2. Run `docker compose up`
  3. Configure Claude Code MCP
  4. Verify tools are accessible

### Extended Verification (within 24 hours)

- [ ] All MCP tools respond correctly
  - `activate_project` - activates project context
  - `rag_query` - returns synthesized answers
  - `semantic_search` - returns ranked documents
  - `index_document` - indexes new documents
  - `list_doc_types` - lists available types
  - `search_external_docs` - searches external docs
  - `rag_query_external` - RAG on external docs
  - `delete_documents` - removes documents
  - `update_promotion_level` - updates visibility

- [ ] File watcher detects changes within 500ms

- [ ] Database migrations apply cleanly

- [ ] No error logs in first 24 hours of operation

### Community Verification

- [ ] Update documentation site
- [ ] Announce in release channels
- [ ] Monitor issue tracker for reports

---

## Rollback Procedures

### Scenario 1: Critical Bug in MCP Server

**Symptoms**: MCP server crashes, tools return errors, Claude Code integration broken

**Immediate Actions**:

1. Communicate issue status
   ```bash
   gh issue create --title "CRITICAL: v{version} rollback in progress" --body "..."
   ```

2. Revert to previous release
   ```bash
   # Users should update their Claude Code MCP config to use previous version
   # Or use specific version tag in Docker
   docker pull ghcr.io/{org}/csharp-compounding-docs:v{previous-version}
   ```

3. Mark release as pre-release
   ```bash
   gh release edit v{version} --prerelease
   ```

### Scenario 2: Database Migration Failure

**Symptoms**: PostgreSQL errors, data corruption, missing tables

**Immediate Actions**:

1. Stop all MCP server instances

2. Execute Liquibase rollback
   ```bash
   docker compose exec postgres liquibase \
     --changeLogFile=/liquibase/changelog/changelog.xml \
     rollbackCount 1
   ```

3. Verify database state
   ```sql
   SELECT * FROM databasechangelog ORDER BY dateexecuted DESC LIMIT 5;
   ```

4. Restore from backup if necessary
   ```bash
   pg_restore -h localhost -p 5433 -U compounding -d compounding_docs backup.dump
   ```

### Scenario 3: Performance Regression

**Symptoms**: Slow queries, high memory usage, timeouts

**Immediate Actions**:

1. Identify regression source via profiling

2. If database-related, check query plans
   ```sql
   EXPLAIN ANALYZE SELECT ... -- problematic query
   ```

3. Apply hotfix or rollback

4. Document regression and root cause

### Scenario 4: Security Vulnerability

**Symptoms**: CVE reported, vulnerability scan failure

**Immediate Actions**:

1. Assess severity and impact

2. For CRITICAL/HIGH:
   - Immediately yank release
     ```bash
     gh release delete v{version} --yes
     git push --delete origin v{version}
     ```
   - Notify users via security advisory

3. For MEDIUM/LOW:
   - Prepare patch release
   - Document in SECURITY.md

---

## Version Numbering

This project follows [Semantic Versioning 2.0.0](https://semver.org/).

### Version Format

```
MAJOR.MINOR.PATCH[-PRERELEASE][+BUILD]
```

### Version Components

| Component | When to Increment |
|-----------|-------------------|
| MAJOR | Breaking changes to MCP tool API, configuration format, or database schema |
| MINOR | New features, new MCP tools, backward-compatible enhancements |
| PATCH | Bug fixes, performance improvements, documentation updates |

### Pre-Release Tags

| Tag | Purpose |
|-----|---------|
| `-alpha.N` | Early development, unstable API |
| `-beta.N` | Feature complete, API stabilizing |
| `-rc.N` | Release candidate, final testing |
| `-preview` | Preview release for marketplace |

### Examples

```
0.1.0-preview    # Initial marketplace preview
0.1.1            # Bug fix release
0.2.0            # New features added
1.0.0            # First stable release
1.0.1            # Patch for stable release
2.0.0            # Breaking changes
```

---

## Release Schedule

| Release Type | Frequency | Notes |
|--------------|-----------|-------|
| Major | As needed | Significant changes, well-announced |
| Minor | Monthly | Feature releases |
| Patch | As needed | Bug fixes, can be immediate for critical issues |
| Security | Immediate | Security patches released ASAP |

---

## Contacts

| Role | Responsibility |
|------|----------------|
| Release Manager | Coordinates release process |
| QA Lead | Verifies pre-release checklist |
| Security Lead | Reviews security items |
| Documentation Lead | Ensures docs are current |
