# GitHub Pages Plugin Marketplace Research

A comprehensive research report on using GitHub Pages to host a Claude plugin marketplace (JSON index file) with GitHub Actions for CI/CD to publish plugins to the marketplace.

---

## Table of Contents

1. [GitHub Pages Overview](#1-github-pages-overview)
2. [Serving JSON Files via GitHub Pages](#2-serving-json-files-via-github-pages)
3. [Plugin Marketplace Index Structure](#3-plugin-marketplace-index-structure)
4. [GitHub Actions Overview](#4-github-actions-overview)
5. [CI/CD for Plugin Publishing](#5-cicd-for-plugin-publishing)
6. [Single Repository Architecture](#6-single-repository-architecture)
7. [Multi-Repository Architecture](#7-multi-repository-architecture)
8. [Workflow: Publishing a Plugin](#8-workflow-publishing-a-plugin)
9. [GitHub Actions for Index Management](#9-github-actions-for-index-management)
10. [Authentication and Permissions](#10-authentication-and-permissions)
11. [Complete Examples](#11-complete-examples)
12. [Claude Code Plugin Specifics](#12-claude-code-plugin-specifics)
13. [Best Practices](#13-best-practices)
14. [Alternative Approaches](#14-alternative-approaches)
15. [Recommendations](#15-recommendations)
16. [Sources](#16-sources)

---

## 1. GitHub Pages Overview

### What is GitHub Pages?

GitHub Pages is a free static site hosting service provided by GitHub that allows you to host websites directly from a GitHub repository. While primarily designed for web pages, it excels at serving any static content including JSON files, making it ideal for hosting a plugin marketplace index.

### Key Features

| Feature | Description |
|---------|-------------|
| **Free Hosting** | No cost for public repositories |
| **Automatic HTTPS** | SSL certificates provided automatically |
| **Custom Domains** | Support for custom domain names |
| **CDN Distribution** | Content served via GitHub's CDN |
| **Git-based Updates** | Content updates via git push |

### Domain Options

1. **Default github.io Domain**
   - Format: `https://<username>.github.io/<repository>/`
   - Example: `https://myorg.github.io/plugin-marketplace/`

2. **Custom Domain**
   - Requires DNS configuration (CNAME or A records)
   - Example: `https://plugins.mydomain.com/`

### Deployment Sources

GitHub Pages supports three publishing source configurations:

| Source | Description | Best For |
|--------|-------------|----------|
| **Main Branch (root)** | Serves from repository root of main/master | Simple sites where all content is the site |
| **Main Branch (/docs)** | Serves from /docs folder | Projects with separate documentation |
| **gh-pages Branch** | Dedicated branch for site content | Build artifacts, keeping source separate |
| **GitHub Actions** | Custom workflow deployment | Complex build processes |

### Limitations

- **Static only**: No server-side processing
- **Build limits**: 10 builds per hour
- **Size limits**: 1GB repository, 100MB file size
- **Bandwidth**: 100GB/month soft limit
- **No custom headers**: Cannot configure CORS or other HTTP headers

---

## 2. Serving JSON Files via GitHub Pages

### Hosting JSON as Static Content

GitHub Pages can serve JSON files as static content, effectively creating a read-only JSON API. This is perfect for a plugin marketplace index.

### URL Structure

JSON files are accessible at predictable URLs:

```
https://<username>.github.io/<repository>/<path>/<file>.json
```

Examples:
- `https://myorg.github.io/marketplace/index.json`
- `https://myorg.github.io/marketplace/plugins/v1/catalog.json`

### CORS Configuration

**Important**: GitHub Pages serves all public content with `Access-Control-Allow-Origin: *` by default. This means:

- Any website can fetch your JSON files via AJAX
- No authentication or access control is possible
- This is ideal for public plugin marketplaces
- You cannot restrict which origins can access the content

#### CORS Workarounds (If Needed)

If you need custom CORS headers, consider:

1. **Cloudflare Workers/Pages**: Proxy requests and inject custom headers
2. **Netlify**: Use `_headers` file for custom header configuration
3. **Vercel**: Configure headers in vercel.json

For a Claude Code plugin marketplace, the default CORS behavior (Allow-Origin: *) is typically sufficient since Claude Code clients will be making requests from various origins.

### Content-Type Headers

GitHub Pages automatically sets appropriate Content-Type headers based on file extensions:

- `.json` files are served as `application/json`
- No manual configuration required

### Caching Behavior

GitHub Pages applies caching headers:

- Default cache: 10 minutes for most content
- Cache invalidation happens automatically on push
- CDN propagation may take a few minutes
- Use query string versioning for cache busting: `index.json?v=1.2.3`

### Example Setup

```
repository/
├── docs/                    # GitHub Pages source
│   ├── index.html          # Optional landing page
│   ├── marketplace.json    # Plugin index
│   └── plugins/
│       ├── plugin-a.json   # Individual plugin metadata
│       └── plugin-b.json
└── src/                     # Source code (not served)
```

---

## 3. Plugin Marketplace Index Structure

### Claude Code Marketplace Schema (Official)

Based on Anthropic's official plugin marketplace structure, here is the recommended schema:

#### marketplace.json Structure

```json
{
  "$schema": "https://anthropic.com/claude-code/marketplace.schema.json",
  "name": "my-plugin-marketplace",
  "version": "1.0.0",
  "description": "A curated collection of Claude Code plugins",
  "owner": {
    "name": "Organization Name",
    "email": "contact@example.com"
  },
  "plugins": [
    {
      "name": "plugin-name",
      "description": "Brief description of what the plugin does",
      "version": "1.0.0",
      "author": {
        "name": "Author Name",
        "email": "author@example.com"
      },
      "source": "./plugins/plugin-name",
      "category": "development",
      "tags": ["tag1", "tag2"],
      "repository": "https://github.com/owner/repo"
    }
  ]
}
```

#### Plugin Entry Fields

| Field | Required | Description |
|-------|----------|-------------|
| `name` | Yes | Unique identifier for the plugin |
| `source` | Yes | Path or source object for the plugin |
| `description` | Recommended | What the plugin does |
| `version` | Recommended | Semantic version (e.g., "1.0.0") |
| `author` | Recommended | Author information object |
| `category` | Optional | Classification (development, productivity, etc.) |
| `tags` | Optional | Array of searchable tags |
| `repository` | Optional | Source code repository URL |

#### Source Options

Local path:
```json
"source": "./plugins/my-plugin"
```

GitHub source:
```json
"source": {
  "type": "github",
  "repo": "owner/repo-name"
}
```

### Extended Marketplace Schema

For a more feature-rich marketplace, consider extending the schema:

```json
{
  "$schema": "https://anthropic.com/claude-code/marketplace.schema.json",
  "name": "enterprise-plugin-marketplace",
  "version": "2.0.0",
  "description": "Enterprise Claude Code plugin marketplace",
  "owner": {
    "name": "Enterprise Team",
    "email": "team@enterprise.com"
  },
  "metadata": {
    "lastUpdated": "2026-01-22T00:00:00Z",
    "totalPlugins": 25,
    "categories": ["development", "productivity", "testing", "documentation"]
  },
  "plugins": [
    {
      "name": "advanced-formatter",
      "displayName": "Advanced Code Formatter",
      "description": "Multi-language code formatting with style presets",
      "version": "2.1.0",
      "minClaudeCodeVersion": "1.0.0",
      "author": {
        "name": "Developer Name",
        "email": "dev@example.com",
        "url": "https://github.com/developer"
      },
      "source": "./plugins/advanced-formatter",
      "category": "development",
      "tags": ["formatting", "style", "linting"],
      "repository": "https://github.com/org/advanced-formatter",
      "license": "MIT",
      "downloads": 1500,
      "rating": 4.8,
      "verified": true,
      "featured": false,
      "changelog": "https://github.com/org/advanced-formatter/blob/main/CHANGELOG.md",
      "documentation": "https://org.github.io/advanced-formatter/docs"
    }
  ]
}
```

### Plugin Directory Structure

Each plugin should follow this structure:

```
plugin-name/
├── .claude-plugin/          # Required: Metadata directory
│   └── plugin.json          # Required: Plugin manifest
├── commands/                # Optional: Command definitions
├── agents/                  # Optional: Agent definitions
├── skills/                  # Optional: Agent Skills
├── hooks/                   # Optional: Hook configurations
├── .mcp.json               # Optional: MCP server definitions
├── scripts/                # Optional: Hook and utility scripts
├── LICENSE                 # Optional
├── CHANGELOG.md            # Optional
└── README.md               # Recommended
```

### Alternative: Simple Registry Format

For simpler needs, a key-value mapping works well:

```json
{
  "@myorg/plugin-github": "github:myorg/plugin-github",
  "@myorg/plugin-slack": "github:myorg/plugin-slack",
  "@myorg/plugin-jira": "github:myorg/plugin-jira"
}
```

This format maps package names to repository locations, with detailed metadata fetched from individual repositories.

---

## 4. GitHub Actions Overview

### What are GitHub Actions?

GitHub Actions is a CI/CD platform that automates build, test, and deployment pipelines directly within GitHub. Workflows are defined in YAML files stored in `.github/workflows/`.

### Key Components

| Component | Description |
|-----------|-------------|
| **Workflow** | Automated process defined in YAML |
| **Event** | Trigger that starts a workflow |
| **Job** | Set of steps that execute on the same runner |
| **Step** | Individual task (shell command or action) |
| **Action** | Reusable unit of code |
| **Runner** | Server that runs workflows |

### Common Workflow Triggers

```yaml
on:
  push:
    branches: [main, master]
    paths:
      - 'plugins/**'

  pull_request:
    branches: [main]

  release:
    types: [published, created]

  workflow_dispatch:  # Manual trigger
    inputs:
      plugin_name:
        description: 'Plugin to publish'
        required: true

  repository_dispatch:  # External trigger
    types: [plugin-update]

  schedule:
    - cron: '0 0 * * *'  # Daily at midnight
```

### Secrets and Environment Variables

```yaml
env:
  NODE_ENV: production

jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - name: Use secret
        env:
          API_KEY: ${{ secrets.API_KEY }}
        run: echo "Using API key..."
```

### GITHUB_TOKEN

Every workflow run automatically receives a `GITHUB_TOKEN` with repository-scoped permissions:

```yaml
jobs:
  build:
    runs-on: ubuntu-latest
    permissions:
      contents: write    # Push commits
      pull-requests: write
    steps:
      - uses: actions/checkout@v4
      - name: Push changes
        run: git push
        env:
          GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}
```

---

## 5. CI/CD for Plugin Publishing

### Key Actions for Deployment

#### Official GitHub Actions

1. **actions/upload-pages-artifact**
   - Packages and uploads artifact for GitHub Pages
   - Max 10GB, no symbolic links

2. **actions/deploy-pages**
   - Deploys uploaded artifact to GitHub Pages
   - Works with `actions/upload-pages-artifact`

#### Popular Third-Party Actions

1. **peaceiris/actions-gh-pages** (Recommended)
   - Simple deployment of static files
   - Works with any static site generator
   - Flexible authentication options

2. **JamesIves/github-pages-deploy-action**
   - Push to any branch
   - Automatic cleanup of old files

3. **stefanzweifel/git-auto-commit-action**
   - Detects file changes
   - Commits and pushes automatically
   - Good for updating JSON files

### Plugin Build and Validation Workflow

```yaml
name: Build and Validate Plugin

on:
  push:
    paths:
      - 'plugins/**'
  pull_request:
    paths:
      - 'plugins/**'

jobs:
  validate:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Setup Node.js
        uses: actions/setup-node@v4
        with:
          node-version: '20'

      - name: Install dependencies
        run: npm ci

      - name: Lint
        run: npm run lint

      - name: Test
        run: npm test

      - name: Validate plugin.json schema
        run: |
          npx ajv validate -s schemas/plugin-schema.json -d plugins/*/plugin.json

      - name: Build
        run: npm run build

      - name: Upload artifact
        uses: actions/upload-artifact@v4
        with:
          name: plugin-build
          path: dist/
```

### Release Creation Workflow

```yaml
name: Create Release

on:
  push:
    tags:
      - 'v*'

jobs:
  release:
    runs-on: ubuntu-latest
    permissions:
      contents: write

    steps:
      - uses: actions/checkout@v4

      - name: Setup Node.js
        uses: actions/setup-node@v4
        with:
          node-version: '20'

      - name: Install and build
        run: |
          npm ci
          npm run build

      - name: Create release archive
        run: |
          cd dist
          zip -r ../plugin-${{ github.ref_name }}.zip .

      - name: Create GitHub Release
        uses: softprops/action-gh-release@v1
        with:
          files: plugin-${{ github.ref_name }}.zip
          generate_release_notes: true
```

### Version Bumping Actions

1. **phips28/autobump**
   - Automated npm version bumping
   - Based on commit messages

2. **pocket-apps/action-update-version**
   - Updates version fields in JSON/YAML
   - Supports package.json, app.yaml

3. **semantic-release**
   - Full automation with Conventional Commits
   - Generates changelogs
   - Creates GitHub releases

---

## 6. Single Repository Architecture

### Can GitHub Pages and Plugin Source Coexist?

**Yes!** This is the recommended approach for a plugin marketplace. Here are two viable structures:

### Option A: Using /docs Folder (Recommended)

```
marketplace-repo/
├── .github/
│   └── workflows/
│       ├── validate-plugin.yml
│       ├── publish-plugin.yml
│       └── update-index.yml
├── docs/                          # GitHub Pages source
│   ├── index.html                # Marketplace landing page
│   ├── marketplace.json          # Plugin index (auto-updated)
│   ├── .nojekyll                 # Bypass Jekyll processing
│   └── assets/
│       └── style.css
├── plugins/                       # Plugin source code
│   ├── plugin-github/
│   │   ├── .claude-plugin/
│   │   │   └── plugin.json
│   │   ├── commands/
│   │   └── src/
│   └── plugin-slack/
│       └── ...
├── scripts/
│   └── update-index.js           # Script to update marketplace.json
├── schemas/
│   └── plugin-schema.json        # Validation schema
├── package.json
└── README.md
```

**Configuration**: Settings > Pages > Source: "Deploy from branch" > Branch: main, Folder: /docs

### Option B: Using gh-pages Branch

```
marketplace-repo/
├── .github/
│   └── workflows/
│       ├── validate-plugin.yml
│       ├── publish-plugin.yml
│       └── deploy-pages.yml      # Builds and pushes to gh-pages
├── plugins/
│   ├── plugin-github/
│   └── plugin-slack/
├── site/                          # Source for GitHub Pages
│   ├── index.html
│   └── marketplace.json
├── scripts/
│   └── update-index.js
└── package.json

# gh-pages branch (auto-generated):
├── index.html
├── marketplace.json
├── .nojekyll
└── assets/
```

**Configuration**: Settings > Pages > Source: "Deploy from branch" > Branch: gh-pages

### Option C: GitHub Actions Deployment (Modern)

Uses `actions/deploy-pages` workflow without requiring a specific branch structure:

```yaml
# .github/workflows/deploy-pages.yml
name: Deploy to GitHub Pages

on:
  push:
    branches: [main]
    paths:
      - 'docs/**'
  workflow_dispatch:

permissions:
  contents: read
  pages: write
  id-token: write

jobs:
  deploy:
    runs-on: ubuntu-latest
    environment:
      name: github-pages
      url: ${{ steps.deployment.outputs.page_url }}
    steps:
      - uses: actions/checkout@v4

      - name: Setup Pages
        uses: actions/configure-pages@v4

      - name: Upload artifact
        uses: actions/upload-pages-artifact@v3
        with:
          path: 'docs'

      - name: Deploy to GitHub Pages
        id: deployment
        uses: actions/deploy-pages@v4
```

**Configuration**: Settings > Pages > Source: "GitHub Actions"

### Comparison

| Aspect | /docs Folder | gh-pages Branch | GitHub Actions |
|--------|--------------|-----------------|----------------|
| **Simplicity** | Simplest | Moderate | Most flexible |
| **Separation** | Less separation | Clear separation | Full control |
| **Build Step** | Direct push updates | Requires workflow | Requires workflow |
| **History** | All changes in main | Clean main history | Clean main history |
| **Best For** | Simple static content | Generated content | Complex builds |

---

## 7. Multi-Repository Architecture

### When Multi-Repo is Necessary

- Plugins developed by different teams/organizations
- Plugins require separate access controls
- Independent release cycles needed
- Plugin size exceeds repository limits

### Architecture

```
Organization/
├── plugin-marketplace/           # Central registry
│   ├── docs/
│   │   └── marketplace.json     # Index of all plugins
│   └── .github/workflows/
│       └── update-index.yml     # Listens for dispatch events
├── plugin-github/               # Individual plugin repos
│   └── .github/workflows/
│       └── publish.yml          # Triggers marketplace update
└── plugin-slack/
    └── .github/workflows/
        └── publish.yml
```

### Cross-Repository Communication

Use `repository_dispatch` for cross-repo workflow triggers:

**Plugin Repository (sender):**

```yaml
name: Publish Plugin

on:
  release:
    types: [published]

jobs:
  notify-marketplace:
    runs-on: ubuntu-latest
    steps:
      - name: Trigger marketplace update
        uses: peter-evans/repository-dispatch@v2
        with:
          token: ${{ secrets.MARKETPLACE_PAT }}  # PAT with repo scope
          repository: myorg/plugin-marketplace
          event-type: plugin-update
          client-payload: |
            {
              "plugin": "${{ github.repository }}",
              "version": "${{ github.event.release.tag_name }}",
              "download_url": "${{ github.event.release.assets[0].browser_download_url }}"
            }
```

**Marketplace Repository (receiver):**

```yaml
name: Update Marketplace Index

on:
  repository_dispatch:
    types: [plugin-update]

jobs:
  update-index:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Update marketplace.json
        run: |
          node scripts/update-index.js \
            --plugin "${{ github.event.client_payload.plugin }}" \
            --version "${{ github.event.client_payload.version }}" \
            --download-url "${{ github.event.client_payload.download_url }}"

      - name: Commit and push
        uses: stefanzweifel/git-auto-commit-action@v5
        with:
          commit_message: "Update ${{ github.event.client_payload.plugin }} to ${{ github.event.client_payload.version }}"
```

---

## 8. Workflow: Publishing a Plugin

### Complete Publishing Flow

```
Developer                    GitHub Actions                 GitHub Pages
    │                              │                              │
    │  Push code/Create release    │                              │
    │─────────────────────────────>│                              │
    │                              │                              │
    │                              │  1. Validate plugin          │
    │                              │  2. Run tests                │
    │                              │  3. Build plugin             │
    │                              │                              │
    │                              │  4. Create release           │
    │                              │  5. Upload assets            │
    │                              │                              │
    │                              │  6. Update marketplace.json  │
    │                              │  7. Commit changes           │
    │                              │                              │
    │                              │─────────────────────────────>│
    │                              │  8. Deploy to GitHub Pages   │
    │                              │                              │
    │                              │  (Auto-deploys on push)      │
    │                              │                              │
    │  Marketplace updated!        │                              │
    │<─────────────────────────────│                              │
```

### Unified Workflow Example

```yaml
name: Publish Plugin

on:
  push:
    tags:
      - 'v*'

jobs:
  validate:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-node@v4
        with:
          node-version: '20'
      - run: npm ci
      - run: npm test
      - run: npm run validate

  build:
    needs: validate
    runs-on: ubuntu-latest
    outputs:
      version: ${{ steps.version.outputs.version }}
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-node@v4
        with:
          node-version: '20'
      - run: npm ci
      - run: npm run build

      - id: version
        run: echo "version=${GITHUB_REF#refs/tags/v}" >> $GITHUB_OUTPUT

      - uses: actions/upload-artifact@v4
        with:
          name: plugin-build
          path: dist/

  release:
    needs: build
    runs-on: ubuntu-latest
    permissions:
      contents: write
    steps:
      - uses: actions/download-artifact@v4
        with:
          name: plugin-build
          path: dist/

      - name: Create archive
        run: cd dist && zip -r ../plugin.zip .

      - uses: softprops/action-gh-release@v1
        with:
          files: plugin.zip

  update-marketplace:
    needs: [build, release]
    runs-on: ubuntu-latest
    permissions:
      contents: write
    steps:
      - uses: actions/checkout@v4
        with:
          ref: main  # Checkout main branch, not the tag

      - name: Update marketplace.json
        run: |
          node scripts/update-index.js \
            --version "${{ needs.build.outputs.version }}"

      - uses: stefanzweifel/git-auto-commit-action@v5
        with:
          commit_message: "chore: update marketplace index to v${{ needs.build.outputs.version }} [skip ci]"
          file_pattern: docs/marketplace.json
```

---

## 9. GitHub Actions for Index Management

### Reading and Updating JSON Files

#### Using jq (Shell-based)

```yaml
- name: Update marketplace.json with jq
  run: |
    # Read existing index
    PLUGINS=$(cat docs/marketplace.json)

    # Update plugin version
    UPDATED=$(echo "$PLUGINS" | jq --arg version "${{ github.ref_name }}" \
      '.plugins |= map(if .id == "my-plugin" then .version = $version else . end)')

    # Update lastUpdated timestamp
    FINAL=$(echo "$UPDATED" | jq --arg ts "$(date -u +%Y-%m-%dT%H:%M:%SZ)" \
      '.lastUpdated = $ts')

    # Write back
    echo "$FINAL" | jq '.' > docs/marketplace.json
```

#### Using Node.js Script

```javascript
// scripts/update-index.js
const fs = require('fs');
const path = require('path');

const indexPath = path.join(__dirname, '../docs/marketplace.json');
const index = JSON.parse(fs.readFileSync(indexPath, 'utf8'));

const pluginId = process.env.PLUGIN_ID;
const version = process.env.VERSION;
const downloadUrl = process.env.DOWNLOAD_URL;

// Find and update plugin
const plugin = index.plugins.find(p => p.id === pluginId);
if (plugin) {
  plugin.version = version;
  plugin.downloadUrl = downloadUrl;
  plugin.updatedAt = new Date().toISOString();
} else {
  // Add new plugin
  index.plugins.push({
    id: pluginId,
    version,
    downloadUrl,
    publishedAt: new Date().toISOString(),
    updatedAt: new Date().toISOString()
  });
}

index.lastUpdated = new Date().toISOString();

fs.writeFileSync(indexPath, JSON.stringify(index, null, 2));
console.log(`Updated ${pluginId} to version ${version}`);
```

### Committing Changes from Actions

```yaml
- name: Commit updated index
  run: |
    git config --local user.email "41898282+github-actions[bot]@users.noreply.github.com"
    git config --local user.name "github-actions[bot]"
    git add docs/marketplace.json
    git commit -m "chore: update marketplace index [skip ci]"
    git push
```

Or use a dedicated action:

```yaml
- uses: stefanzweifel/git-auto-commit-action@v5
  with:
    commit_message: "chore: update marketplace index"
    file_pattern: docs/marketplace.json
    commit_user_name: github-actions[bot]
    commit_user_email: 41898282+github-actions[bot]@users.noreply.github.com
```

### Avoiding Infinite Workflow Loops

**Critical**: Commits made by workflows can trigger other workflows, causing infinite loops.

#### Method 1: Use GITHUB_TOKEN (Recommended)

Commits made with `GITHUB_TOKEN` do NOT trigger subsequent workflows:

```yaml
- uses: actions/checkout@v4
  # Uses GITHUB_TOKEN by default, which prevents loop
```

#### Method 2: Skip CI in Commit Message

```yaml
- name: Commit with skip ci
  run: |
    git commit -m "chore: update index [skip ci]"
```

#### Method 3: Path Filtering

```yaml
on:
  push:
    paths-ignore:
      - 'docs/marketplace.json'  # Don't trigger on index updates
```

---

## 10. Authentication and Permissions

### GITHUB_TOKEN Permissions

The `GITHUB_TOKEN` is automatically available in workflows with scoped permissions:

```yaml
permissions:
  contents: read       # Read repository content
  contents: write      # Push commits, create releases
  pull-requests: write # Comment on PRs
  issues: write        # Create/update issues
  pages: write         # Deploy to GitHub Pages
```

Repository settings: Settings > Actions > General > Workflow permissions

### When GITHUB_TOKEN is Sufficient

- Pushing to the same repository
- Creating releases in the same repository
- Deploying GitHub Pages in the same repository

### When PAT is Required

- Cross-repository operations
- Triggering workflows in other repositories
- Pushing to protected branches (sometimes)
- Modifying workflow files (.github/workflows/)

### Creating and Using PATs

1. Create PAT: Settings > Developer settings > Personal access tokens
2. Select scopes: `repo` (full access) or `public_repo` (public only)
3. Store as secret: Repository > Settings > Secrets > Actions

```yaml
- name: Trigger other repo
  uses: peter-evans/repository-dispatch@v2
  with:
    token: ${{ secrets.CROSS_REPO_PAT }}  # PAT, not GITHUB_TOKEN
    repository: other-org/other-repo
    event-type: update-request
```

---

## 11. Complete Examples

### Repository Structure (Single Repo)

```
claude-plugin-marketplace/
├── .github/
│   ├── workflows/
│   │   ├── ci.yml                    # Validate PRs
│   │   ├── publish-plugin.yml        # On release, update index
│   │   └── deploy-pages.yml          # Deploy to GitHub Pages
│   └── CODEOWNERS
├── .claude-plugin/
│   └── marketplace.json              # Claude Code marketplace manifest
├── docs/                              # GitHub Pages source
│   ├── index.html                    # Landing page
│   ├── marketplace.json              # Plugin index (auto-updated)
│   ├── .nojekyll                     # Bypass Jekyll
│   ├── api/
│   │   └── v1/
│   │       ├── plugins.json          # Full plugin list
│   │       └── plugins/
│   │           ├── plugin-a.json     # Individual plugin details
│   │           └── plugin-b.json
│   └── assets/
│       └── icons/
├── plugins/                           # Plugin source
│   ├── mcp-github/
│   │   ├── .claude-plugin/
│   │   │   └── plugin.json
│   │   ├── .mcp.json
│   │   ├── commands/
│   │   ├── agents/
│   │   └── package.json
│   └── mcp-slack/
│       └── ...
├── scripts/
│   ├── generate-marketplace.js       # Auto-generate marketplace.json
│   ├── update-index.js               # Update marketplace.json
│   ├── validate-plugin.js            # Validate plugin structure
│   └── bump-version.js               # Version management
├── schemas/
│   ├── plugin.schema.json            # Plugin manifest schema
│   └── marketplace.schema.json       # Marketplace index schema
├── templates/
│   └── plugin-template/              # Template for new plugins
├── package.json
├── README.md
└── CONTRIBUTING.md
```

### marketplace.json Complete Example

```json
{
  "$schema": "https://anthropic.com/claude-code/marketplace.schema.json",
  "name": "company-claude-plugins",
  "version": "1.2.0",
  "description": "Official plugin marketplace for Company development tools",
  "owner": {
    "name": "Company Engineering",
    "email": "engineering@company.com"
  },
  "metadata": {
    "lastUpdated": "2026-01-22T10:30:00Z",
    "apiVersion": "1.0",
    "totalPlugins": 3
  },
  "plugins": [
    {
      "name": "code-formatter",
      "displayName": "Code Formatter Pro",
      "description": "Multi-language code formatting with company style presets",
      "version": "2.0.1",
      "author": {
        "name": "Dev Team",
        "email": "devteam@company.com"
      },
      "source": "./plugins/code-formatter",
      "category": "development",
      "tags": ["formatting", "style", "lint"],
      "repository": "https://github.com/company/code-formatter",
      "license": "MIT",
      "verified": true
    },
    {
      "name": "test-runner",
      "displayName": "Smart Test Runner",
      "description": "Intelligent test execution with coverage reporting",
      "version": "1.5.0",
      "author": {
        "name": "QA Team",
        "email": "qa@company.com"
      },
      "source": "./plugins/test-runner",
      "category": "testing",
      "tags": ["testing", "coverage", "ci"],
      "repository": "https://github.com/company/test-runner",
      "license": "MIT",
      "verified": true
    },
    {
      "name": "doc-generator",
      "displayName": "Documentation Generator",
      "description": "Auto-generate API documentation from code",
      "version": "1.0.0",
      "author": {
        "name": "Docs Team",
        "email": "docs@company.com"
      },
      "source": {
        "type": "github",
        "repo": "company/doc-generator"
      },
      "category": "documentation",
      "tags": ["docs", "api", "markdown"],
      "repository": "https://github.com/company/doc-generator",
      "license": "Apache-2.0",
      "verified": false
    }
  ]
}
```

### CI Validation Workflow

```yaml
# .github/workflows/ci.yml
name: CI - Validate Plugins

on:
  push:
    branches: [main]
    paths:
      - 'plugins/**'
      - '.claude-plugin/**'
  pull_request:
    branches: [main]
    paths:
      - 'plugins/**'
      - '.claude-plugin/**'

jobs:
  validate:
    runs-on: ubuntu-latest
    steps:
      - name: Checkout repository
        uses: actions/checkout@v4

      - name: Setup Node.js
        uses: actions/setup-node@v4
        with:
          node-version: '20'

      - name: Install dependencies
        run: npm ci

      - name: Validate plugin structure
        run: npm run validate:plugins

      - name: Validate marketplace.json
        run: npm run validate:marketplace

      - name: Check for duplicate plugins
        run: npm run check:duplicates

      - name: Lint plugin files
        run: npm run lint

  test:
    runs-on: ubuntu-latest
    needs: validate
    steps:
      - name: Checkout repository
        uses: actions/checkout@v4

      - name: Setup Node.js
        uses: actions/setup-node@v4
        with:
          node-version: '20'

      - name: Install dependencies
        run: npm ci

      - name: Run tests
        run: npm test
```

### Publish Plugin Workflow

```yaml
# .github/workflows/publish-plugin.yml
name: Publish Plugin

on:
  release:
    types: [published]

permissions:
  contents: write
  pages: write
  id-token: write

jobs:
  update-marketplace:
    runs-on: ubuntu-latest
    steps:
      - name: Checkout repository
        uses: actions/checkout@v4
        with:
          fetch-depth: 0

      - name: Setup Node.js
        uses: actions/setup-node@v4
        with:
          node-version: '20'

      - name: Install dependencies
        run: npm ci

      - name: Extract version from tag
        id: version
        run: echo "VERSION=${GITHUB_REF#refs/tags/v}" >> $GITHUB_OUTPUT

      - name: Update marketplace.json
        run: |
          node scripts/generate-marketplace.js
          node scripts/update-version.js ${{ steps.version.outputs.VERSION }}

      - name: Commit marketplace update
        run: |
          git config --global user.name "github-actions[bot]"
          git config --global user.email "41898282+github-actions[bot]@users.noreply.github.com"
          git add docs/marketplace.json docs/api/
          if ! git diff --staged --quiet; then
            git commit -m "chore: update marketplace to v${{ steps.version.outputs.VERSION }} [skip ci]"
            git push
          fi

  deploy-pages:
    runs-on: ubuntu-latest
    needs: update-marketplace
    environment:
      name: github-pages
      url: ${{ steps.deployment.outputs.page_url }}
    steps:
      - name: Checkout repository
        uses: actions/checkout@v4
        with:
          ref: main  # Get the latest commit with updated marketplace

      - name: Setup Pages
        uses: actions/configure-pages@v4

      - name: Upload artifact
        uses: actions/upload-pages-artifact@v3
        with:
          path: 'docs'

      - name: Deploy to GitHub Pages
        id: deployment
        uses: actions/deploy-pages@v4
```

### Alternative: Deploy with peaceiris/actions-gh-pages

```yaml
# .github/workflows/deploy-pages-alt.yml
name: Deploy to GitHub Pages

on:
  push:
    branches: [main]
    paths:
      - 'docs/**'
      - '.claude-plugin/**'
  workflow_dispatch:

permissions:
  contents: write

jobs:
  deploy:
    runs-on: ubuntu-latest
    concurrency:
      group: ${{ github.workflow }}-${{ github.ref }}
    steps:
      - name: Checkout repository
        uses: actions/checkout@v4

      - name: Setup Node.js
        uses: actions/setup-node@v4
        with:
          node-version: '20'

      - name: Install dependencies
        run: npm ci

      - name: Generate marketplace index
        run: npm run generate:marketplace

      - name: Deploy to GitHub Pages
        uses: peaceiris/actions-gh-pages@v4
        if: github.ref == 'refs/heads/main'
        with:
          github_token: ${{ secrets.GITHUB_TOKEN }}
          publish_dir: ./docs
          cname: plugins.company.com  # Optional: custom domain
```

### Generate Marketplace Script

```javascript
// scripts/generate-marketplace.js
const fs = require('fs');
const path = require('path');

const PLUGINS_DIR = path.join(__dirname, '..', 'plugins');
const OUTPUT_DIR = path.join(__dirname, '..', 'docs');
const MARKETPLACE_FILE = path.join(OUTPUT_DIR, 'marketplace.json');

function readPluginManifest(pluginDir) {
  const manifestPath = path.join(pluginDir, '.claude-plugin', 'plugin.json');
  if (!fs.existsSync(manifestPath)) {
    console.warn(`No manifest found for ${pluginDir}`);
    return null;
  }
  return JSON.parse(fs.readFileSync(manifestPath, 'utf-8'));
}

function generateMarketplace() {
  const plugins = [];

  const pluginDirs = fs.readdirSync(PLUGINS_DIR, { withFileTypes: true })
    .filter(dirent => dirent.isDirectory())
    .map(dirent => dirent.name);

  for (const pluginName of pluginDirs) {
    const pluginDir = path.join(PLUGINS_DIR, pluginName);
    const manifest = readPluginManifest(pluginDir);

    if (manifest) {
      plugins.push({
        name: manifest.name || pluginName,
        description: manifest.description || '',
        version: manifest.version || '0.0.0',
        author: manifest.author || {},
        source: `./plugins/${pluginName}`,
        category: manifest.category || 'uncategorized',
        tags: manifest.tags || [],
        repository: manifest.repository || null,
        license: manifest.license || 'MIT'
      });
    }
  }

  const marketplace = {
    "$schema": "https://anthropic.com/claude-code/marketplace.schema.json",
    name: process.env.MARKETPLACE_NAME || 'plugin-marketplace',
    version: process.env.MARKETPLACE_VERSION || '1.0.0',
    description: 'Auto-generated plugin marketplace',
    owner: {
      name: process.env.OWNER_NAME || 'Marketplace Owner',
      email: process.env.OWNER_EMAIL || 'owner@example.com'
    },
    metadata: {
      lastUpdated: new Date().toISOString(),
      totalPlugins: plugins.length
    },
    plugins
  };

  // Ensure output directory exists
  if (!fs.existsSync(OUTPUT_DIR)) {
    fs.mkdirSync(OUTPUT_DIR, { recursive: true });
  }

  // Write main marketplace file
  fs.writeFileSync(MARKETPLACE_FILE, JSON.stringify(marketplace, null, 2));

  // Write API-style individual plugin files
  const apiDir = path.join(OUTPUT_DIR, 'api', 'v1', 'plugins');
  fs.mkdirSync(apiDir, { recursive: true });

  for (const plugin of plugins) {
    const pluginFile = path.join(apiDir, `${plugin.name}.json`);
    fs.writeFileSync(pluginFile, JSON.stringify(plugin, null, 2));
  }

  // Write full plugins list
  fs.writeFileSync(
    path.join(OUTPUT_DIR, 'api', 'v1', 'plugins.json'),
    JSON.stringify({ plugins }, null, 2)
  );

  console.log(`Generated marketplace with ${plugins.length} plugins`);
}

generateMarketplace();
```

### package.json Scripts

```json
{
  "name": "claude-plugin-marketplace",
  "version": "1.0.0",
  "description": "Claude Code plugin marketplace",
  "scripts": {
    "generate:marketplace": "node scripts/generate-marketplace.js",
    "validate:plugins": "node scripts/validate-plugins.js",
    "validate:marketplace": "node scripts/validate-marketplace.js",
    "check:duplicates": "node scripts/check-duplicates.js",
    "lint": "eslint plugins/**/*.js",
    "test": "jest",
    "build": "npm run generate:marketplace",
    "prepublish": "npm run validate:plugins && npm run build"
  },
  "devDependencies": {
    "ajv": "^8.12.0",
    "eslint": "^8.56.0",
    "jest": "^29.7.0"
  }
}
```

### Landing Page (docs/index.html)

```html
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Claude Code Plugin Marketplace</title>
    <style>
        body {
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
            max-width: 800px;
            margin: 0 auto;
            padding: 2rem;
            background: #0f0f0f;
            color: #e0e0e0;
        }
        h1 { color: #fff; }
        .plugin {
            background: #1a1a1a;
            padding: 1rem;
            margin: 1rem 0;
            border-radius: 8px;
            border: 1px solid #333;
        }
        .plugin h3 { margin-top: 0; color: #6cb6ff; }
        .version { color: #8b949e; font-size: 0.9em; }
        code {
            background: #2d2d2d;
            padding: 0.5rem;
            display: block;
            border-radius: 4px;
            overflow-x: auto;
        }
    </style>
</head>
<body>
    <h1>Claude Code Plugin Marketplace</h1>
    <p>Add this marketplace to Claude Code:</p>
    <code>/plugin marketplace add https://username.github.io/plugin-marketplace</code>

    <h2>Available Plugins</h2>
    <div id="plugins"></div>

    <script>
        fetch('./marketplace.json')
            .then(r => r.json())
            .then(data => {
                const container = document.getElementById('plugins');
                data.plugins.forEach(plugin => {
                    container.innerHTML += `
                        <div class="plugin">
                            <h3>${plugin.displayName || plugin.name}</h3>
                            <span class="version">v${plugin.version}</span>
                            <p>${plugin.description}</p>
                        </div>
                    `;
                });
            })
            .catch(err => console.error('Failed to load plugins:', err));
    </script>
</body>
</html>
```

---

## 12. Claude Code Plugin Specifics

### Plugin Directory Structure

Claude Code plugins follow a standardized structure:

```
plugin-name/
├── .claude-plugin/
│   └── plugin.json          # Required: Plugin manifest
├── commands/                # Slash commands (.md files)
│   └── my-command.md
├── agents/                  # Subagent definitions (.md files)
│   └── my-agent.md
├── skills/                  # Agent skills (subdirectories)
│   └── skill-name/
│       └── SKILL.md        # Required for each skill
├── hooks/
│   └── hooks.json          # Event handler configuration
├── .mcp.json               # MCP server definitions
└── scripts/                # Helper scripts
```

### Plugin Manifest (plugin.json)

```json
{
  "name": "mcp-github",
  "displayName": "GitHub MCP Server",
  "version": "2.1.0",
  "description": "Full GitHub integration for Claude Code",
  "author": "Anthropic",
  "license": "MIT",
  "repository": "https://github.com/anthropics/claude-plugins-official",
  "keywords": ["github", "mcp", "git"],
  "engines": {
    "claudeCode": ">=1.0.0"
  },
  "main": "src/index.ts",
  "mcpServers": {
    "github": {
      "command": "node",
      "args": ["${CLAUDE_PLUGIN_ROOT}/dist/server.js"],
      "env": {
        "GITHUB_TOKEN": "${GITHUB_TOKEN}"
      }
    }
  }
}
```

### MCP Server Configuration (.mcp.json)

```json
{
  "github-mcp": {
    "command": "node",
    "args": ["${CLAUDE_PLUGIN_ROOT}/servers/github-server.js"],
    "env": {
      "GITHUB_TOKEN": "${GITHUB_TOKEN}"
    }
  }
}
```

### Environment Variables

Use `${CLAUDE_PLUGIN_ROOT}` for all intra-plugin path references:

```json
{
  "command": "node",
  "args": ["${CLAUDE_PLUGIN_ROOT}/dist/server.js"]
}
```

Never use hardcoded absolute paths or relative paths from working directory.

---

## 13. Best Practices

### Semantic Versioning

Follow [SemVer](https://semver.org/) for all plugins:

- **MAJOR**: Breaking changes
- **MINOR**: New features, backward compatible
- **PATCH**: Bug fixes, backward compatible

Use conventional commits for automated versioning:

```
feat: add new command for issue creation
fix: resolve authentication error
feat!: change API response format (BREAKING)
```

### Plugin Validation

Validate before publishing:

```javascript
// scripts/validate-plugin.js
const Ajv = require('ajv');
const fs = require('fs');

const schema = require('../schemas/plugin.schema.json');
const pluginPath = process.argv[2];
const plugin = require(`../${pluginPath}/.claude-plugin/plugin.json`);

const ajv = new Ajv();
const validate = ajv.compile(schema);

if (!validate(plugin)) {
  console.error('Validation failed:', validate.errors);
  process.exit(1);
}

// Check required files exist
const requiredFiles = ['.claude-plugin/plugin.json'];
for (const file of requiredFiles) {
  if (!fs.existsSync(`${pluginPath}/${file}`)) {
    console.error(`Missing required file: ${file}`);
    process.exit(1);
  }
}

console.log('Plugin validation passed!');
```

### Security Considerations

1. **Never commit secrets** to the repository
2. **Validate all inputs** in workflows
3. **Use minimal permissions** for tokens
4. **Pin action versions** to SHA: `uses: actions/checkout@a12a3943b4bdde767164f792f33f40b04645d846`
5. **Review third-party actions** before use

### .nojekyll File

Always include an empty `.nojekyll` file in your GitHub Pages source to bypass Jekyll processing:

```
docs/.nojekyll  # Empty file to disable Jekyll processing
```

---

## 14. Alternative Approaches

### GitHub Releases as Distribution

Use GitHub Releases instead of a JSON index:

**Pros:**
- Built-in versioning and changelog
- Download counts
- Asset management
- No custom infrastructure

**Cons:**
- No centralized index
- Harder to search/filter
- No custom metadata

### npm/NuGet for Plugin Distribution

Publish plugins to package registries:

**npm Example:**
```json
{
  "name": "@myorg/claude-plugin-github",
  "version": "2.1.0",
  "claudePlugin": true,
  "files": ["dist/", ".claude-plugin/"]
}
```

**Pros:**
- Established ecosystem
- Dependency management
- Version resolution
- CDN distribution

**Cons:**
- Requires registry account
- More complex publishing
- May have naming restrictions

### Comparison Matrix

| Approach | Complexity | Cost | Features | Maintenance |
|----------|------------|------|----------|-------------|
| GitHub Pages + JSON | Low | Free | Basic | Low |
| GitHub Releases | Low | Free | Basic | Low |
| npm Registry | Medium | Free/Paid | Good | Low |
| Custom Registry | High | Medium-High | Full | High |

**Recommendation**: Start with GitHub Pages + JSON for simplicity and zero cost. Migrate to custom solution only if needed.

---

## 15. Recommendations

### For This Project

Based on the research, here are specific recommendations for building a Claude Code plugin marketplace:

#### Architecture: Single Repository with /docs Folder

```
claude-plugin-marketplace/
├── .github/workflows/      # CI/CD
├── .claude-plugin/         # Marketplace manifest for Claude Code
│   └── marketplace.json
├── docs/                   # GitHub Pages (marketplace.json, landing page)
├── plugins/               # Plugin source code
├── scripts/               # Automation scripts
└── schemas/               # JSON schemas
```

#### Key Implementation Steps

1. **Configure GitHub Pages**
   - Source: Deploy from branch
   - Branch: main, Folder: /docs

2. **Create marketplace.json**
   - Use the Claude Code marketplace schema
   - Include categories and search-friendly metadata
   - Add `.nojekyll` file

3. **Set Up Workflows**
   - PR validation workflow
   - Release/publish workflow
   - Index update workflow (with [skip ci])

4. **Plugin Structure**
   - Follow Claude Code plugin conventions
   - Include plugin.json manifest
   - Document installation and configuration

5. **Documentation**
   - Create index.html landing page
   - Document contribution process
   - Provide plugin development guide

#### URLs

Once deployed:
- Landing page: `https://username.github.io/marketplace/`
- Plugin index: `https://username.github.io/marketplace/marketplace.json`
- Schema: `https://username.github.io/marketplace/schema/plugin-v1.json`

### Quick Start Checklist

- [ ] Create repository with recommended structure
- [ ] Add `.claude-plugin/marketplace.json` with schema
- [ ] Create `/docs` folder with `marketplace.json` and optional landing page
- [ ] Add `.nojekyll` file to `/docs`
- [ ] Create GitHub Actions workflows for CI and deployment
- [ ] Configure GitHub Pages (Settings > Pages > Source: main, /docs)
- [ ] Add first plugin to `/plugins` directory
- [ ] Test marketplace addition: `/plugin marketplace add https://username.github.io/repo`

---

## 16. Sources

### GitHub Pages Documentation
- [About GitHub Pages - GitHub Docs](https://docs.github.com/en/pages/getting-started-with-github-pages/about-github-pages)
- [Configuring a publishing source - GitHub Docs](https://docs.github.com/en/pages/getting-started-with-github-pages/configuring-a-publishing-source-for-your-github-pages-site)
- [Using custom workflows with GitHub Pages - GitHub Docs](https://docs.github.com/en/pages/getting-started-with-github-pages/using-custom-workflows-with-github-pages)

### GitHub Pages CORS
- [Configure GitHub Pages CORS headers - GitHub Community](https://github.com/orgs/community/discussions/157852)
- [Github pages and CORS - GitHub Community](https://github.com/orgs/community/discussions/22399)
- [Hosting a JSON API on GitHub Pages - Victor Scholz](https://victorscholz.medium.com/hosting-a-json-api-on-github-pages-47b402f72603)

### GitHub Actions
- [actions/deploy-pages](https://github.com/actions/deploy-pages)
- [actions/upload-pages-artifact](https://github.com/actions/upload-pages-artifact)
- [peaceiris/actions-gh-pages](https://github.com/peaceiris/actions-gh-pages)
- [JamesIves/github-pages-deploy-action](https://github.com/JamesIves/github-pages-deploy-action)
- [stefanzweifel/git-auto-commit-action](https://github.com/stefanzweifel/git-auto-commit-action)
- [Automated Version Bump](https://github.com/marketplace/actions/automated-version-bump)

### Claude Code Plugin Documentation
- [Claude Code Plugin Marketplace Docs](https://code.claude.com/docs/en/plugin-marketplaces)
- [Anthropic Claude Code marketplace.json](https://github.com/anthropics/claude-code/blob/main/.claude-plugin/marketplace.json)
- [Plugin Schema Documentation](https://github.com/ananddtyagi/cc-marketplace/blob/main/PLUGIN_SCHEMA.md)
- [Claude Plugin Template](https://github.com/ivan-magda/claude-code-plugin-template)
- [kivilaid/plugin-marketplace](https://github.com/kivilaid/plugin-marketplace)

### Version Management
- [semantic-release](https://dev.to/kouts/automated-versioning-and-package-publishing-using-github-actions-and-semantic-release-1kce)
- [Update Files Version Field](https://github.com/marketplace/actions/update-files-version-field)

### Repository Structure
- [GitHub Repository Structure Best Practices](https://medium.com/code-factory-berlin/github-repository-structure-best-practices-248e6effc405)
- [Folder Structure Conventions](https://github.com/kriasoft/Folder-Structure-Conventions)

---

*Research completed: January 22, 2026*
