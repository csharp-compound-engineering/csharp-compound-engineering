# Phase 124: Coverage Reporting in CI

> **Status**: NOT_STARTED
> **Effort Estimate**: 3-4 hours
> **Category**: Testing Framework
> **Prerequisites**: Phase 121 (Code Coverage Configuration), Phase 114 (ReportGenerator Coverage Visualization)

---

## Spec References

This phase implements CI/CD coverage reporting defined in:

- **spec/testing/ci-cd-pipeline.md** - [Coverage Reporting](../spec/testing/ci-cd-pipeline.md#coverage-reporting) section
- **research/reportgenerator-coverage-visualization.md** - GitHub Actions integration patterns

---

## Objectives

1. Integrate ReportGenerator into the GitHub Actions CI workflow
2. Generate HTML coverage reports from Cobertura XML output
3. Add coverage summary to GitHub Actions job summary
4. Upload coverage reports as workflow artifacts
5. Configure GitHub Pages publishing for release coverage reports
6. Generate coverage badges for README embedding
7. Establish per-version and "latest" URL structures

---

## Acceptance Criteria

- [ ] ReportGenerator GitHub Action (`danielpalme/ReportGenerator-GitHub-Action@5`) is configured in test workflow
- [ ] Coverage report generated with report types: `Html`, `Badges`, `MarkdownSummaryGithub`
- [ ] Coverage summary appended to `$GITHUB_STEP_SUMMARY` for visibility in Actions UI
- [ ] Coverage report uploaded as artifact named `coverage-report`
- [ ] Release workflow deploys coverage to GitHub Pages with version directory structure
- [ ] Latest symlink/copy updated on each release
- [ ] Coverage badges generated and accessible via GitHub Pages URLs
- [ ] URL structure follows pattern:
  - Latest: `https://{org}.github.io/{repo}/coverage/latest/`
  - Per-version: `https://{org}.github.io/{repo}/coverage/v{version}/`

---

## Implementation Notes

### ReportGenerator in Test Workflow

Add coverage report generation step to `.github/workflows/test.yml` after the unit tests job:

```yaml
  coverage-report:
    runs-on: ubuntu-latest
    needs: unit-tests
    steps:
      - uses: actions/checkout@v4

      - name: Download Unit Coverage
        uses: actions/download-artifact@v4
        with:
          name: unit-coverage
          path: ./coverage/unit/

      - name: Generate Coverage Report
        uses: danielpalme/ReportGenerator-GitHub-Action@5
        with:
          reports: '**/coverage.cobertura.xml'
          targetdir: 'coveragereport'
          reporttypes: 'Html;Badges;MarkdownSummaryGithub'
          title: 'CompoundDocs Coverage Report'
          tag: '${{ github.run_number }}'

      - name: Add Coverage to Job Summary
        run: cat coveragereport/SummaryGithub.md >> $GITHUB_STEP_SUMMARY

      - name: Upload Coverage Artifact
        uses: actions/upload-artifact@v4
        with:
          name: coverage-report
          path: coveragereport
          retention-days: 30
```

### ReportGenerator Action Configuration

| Parameter | Value | Purpose |
|-----------|-------|---------|
| `reports` | `**/coverage.cobertura.xml` | Glob pattern to find Cobertura XML files |
| `targetdir` | `coveragereport` | Output directory for generated reports |
| `reporttypes` | `Html;Badges;MarkdownSummaryGithub` | Report formats to generate |
| `title` | `CompoundDocs Coverage Report` | Report title shown in HTML |
| `tag` | `${{ github.run_number }}` | Build identifier for report versioning |

### Report Types Generated

| Type | Output | Purpose |
|------|--------|---------|
| `Html` | `index.html` + supporting files | Full navigable coverage report |
| `Badges` | `badge_*.svg` | SVG badges for README embedding |
| `MarkdownSummaryGithub` | `SummaryGithub.md` | GitHub-formatted summary for job summary |

### GitHub Pages Publishing (Release Workflow)

Add coverage publishing to `.github/workflows/release.yml` after tests pass:

```yaml
  publish-coverage:
    runs-on: ubuntu-latest
    needs: [release]
    if: needs.release.outputs.new_release_published == 'true'
    permissions:
      pages: write
      id-token: write
    steps:
      - uses: actions/checkout@v4
        with:
          ref: gh-pages
          fetch-depth: 0

      - name: Download Coverage Report
        uses: actions/download-artifact@v4
        with:
          name: coverage-report
          path: coveragereport

      - name: Deploy Coverage to GitHub Pages
        run: |
          VERSION=${{ needs.release.outputs.new_release_version }}

          # Create version-specific directory
          mkdir -p coverage/$VERSION
          cp -r coveragereport/* coverage/$VERSION/

          # Update latest directory (copy, not symlink for GitHub Pages)
          rm -rf coverage/latest
          mkdir -p coverage/latest
          cp -r coveragereport/* coverage/latest/

          # Configure git
          git config user.name "github-actions[bot]"
          git config user.email "github-actions[bot]@users.noreply.github.com"

          # Commit and push
          git add coverage/
          git commit -m "docs: update coverage for v$VERSION [skip ci]" || echo "No changes to commit"
          git push

      - name: Output Coverage URLs
        run: |
          echo "## Coverage Report Published" >> $GITHUB_STEP_SUMMARY
          echo "" >> $GITHUB_STEP_SUMMARY
          echo "- **Latest**: https://${{ github.repository_owner }}.github.io/${{ github.event.repository.name }}/coverage/latest/" >> $GITHUB_STEP_SUMMARY
          echo "- **v${{ needs.release.outputs.new_release_version }}**: https://${{ github.repository_owner }}.github.io/${{ github.event.repository.name }}/coverage/v${{ needs.release.outputs.new_release_version }}/" >> $GITHUB_STEP_SUMMARY
```

### GitHub Pages Setup Requirements

Before coverage publishing works, GitHub Pages must be configured:

1. **Enable GitHub Pages**: Repository Settings > Pages > Source: "Deploy from a branch"
2. **Create gh-pages branch**: `git checkout --orphan gh-pages && git commit --allow-empty -m "Initial gh-pages" && git push origin gh-pages`
3. **Configure branch protection**: Allow `github-actions[bot]` to push to `gh-pages`

### Coverage Badge Integration

After first release, badges are available at predictable URLs:

```markdown
<!-- README.md badge examples -->
![Line Coverage](https://myorg.github.io/csharp-compounding-docs/coverage/latest/badge_linecoverage.svg)
![Branch Coverage](https://myorg.github.io/csharp-compounding-docs/coverage/latest/badge_branchcoverage.svg)
![Method Coverage](https://myorg.github.io/csharp-compounding-docs/coverage/latest/badge_methodcoverage.svg)
```

### URL Structure

The coverage reports follow a versioned URL structure:

```
https://{org}.github.io/{repo}/
├── coverage/
│   ├── latest/           # Always points to most recent release
│   │   ├── index.html
│   │   ├── badge_linecoverage.svg
│   │   ├── badge_branchcoverage.svg
│   │   └── ...
│   ├── v1.0.0/           # Per-release archives
│   │   ├── index.html
│   │   └── ...
│   ├── v1.1.0/
│   │   └── ...
│   └── v2.0.0/
│       └── ...
```

### Artifact Retention

| Artifact | Retention | Rationale |
|----------|-----------|-----------|
| `coverage-report` | 30 days | Downloadable for PR review |
| GitHub Pages | Permanent | Per-version archives never deleted |

### Combined Multi-Project Coverage

If merging coverage from multiple test projects (unit + integration):

```yaml
- name: Download All Coverage Artifacts
  uses: actions/download-artifact@v4
  with:
    pattern: '*-coverage'
    merge-multiple: true
    path: ./coverage/

- name: Generate Combined Coverage Report
  uses: danielpalme/ReportGenerator-GitHub-Action@5
  with:
    reports: './coverage/**/coverage.cobertura.xml'
    targetdir: 'coveragereport'
    reporttypes: 'Html;Badges;MarkdownSummaryGithub;Cobertura'
    title: 'CompoundDocs Combined Coverage Report'
```

The additional `Cobertura` report type outputs a merged `Cobertura.xml` for tools that need consolidated data.

---

## Dependencies

### Depends On
- **Phase 121**: Code Coverage Configuration (Coverlet produces Cobertura XML in CI)
- **Phase 114**: ReportGenerator Coverage Visualization (local tool setup and familiarity)

### Blocks
- README badge integration (requires published badges)
- Documentation site coverage links (requires GitHub Pages setup)

---

## Verification Steps

After completing this phase, verify:

1. **Test workflow generates coverage**:
   - Push a commit or open a PR
   - Verify `coverage-report` job runs successfully
   - Check job summary shows coverage statistics

2. **Artifact uploaded**:
   - In Actions run, verify `coverage-report` artifact is available
   - Download and verify HTML report opens correctly

3. **Release publishes to GitHub Pages**:
   - Create a test release (or merge to main with releasable commit)
   - Verify `gh-pages` branch updated with version directory
   - Access `https://{org}.github.io/{repo}/coverage/latest/` and verify report loads

4. **Badge URLs accessible**:
   - Verify badge SVGs load at expected URLs
   - Test embedding in README renders correctly

5. **Version history preserved**:
   - After multiple releases, verify each version's coverage is accessible
   - Confirm `latest/` always shows most recent version

---

## Troubleshooting

### Common Issues

| Issue | Cause | Solution |
|-------|-------|----------|
| "No coverage files found" | Incorrect glob pattern | Verify artifact download path matches glob |
| GitHub Pages 404 | Branch not configured | Enable Pages for `gh-pages` branch |
| Permission denied pushing to gh-pages | Actions permissions | Grant `pages: write` permission |
| Badges not rendering | Cache/CDN delay | Wait 5-10 minutes or append `?v=1` cache buster |
| Report shows 0% coverage | XML not merged correctly | Check `merge-multiple: true` on artifact download |

### Debug Commands

```bash
# List coverage files found
find . -name "coverage.cobertura.xml" -type f

# Verify ReportGenerator can parse files
dotnet tool run reportgenerator \
  -reports:"**/coverage.cobertura.xml" \
  -targetdir:"coveragereport" \
  -reporttypes:"TextSummary" \
  -verbosity:Verbose
```

---

## Notes

- ReportGenerator GitHub Action v5 supports .NET 8/10 coverage files natively
- The `MarkdownSummaryGithub` format is specifically designed for GitHub's job summary rendering
- Coverage history tracking (`-historydir`) is not used in CI to avoid storage bloat; trends can be derived from per-version archives
- Badge SVGs are self-contained and don't require external services like shields.io
- The `[skip ci]` in the coverage commit message prevents infinite workflow loops
- Consider adding coverage threshold enforcement in ReportGenerator as a backup to Coverlet's `<Threshold>` setting
