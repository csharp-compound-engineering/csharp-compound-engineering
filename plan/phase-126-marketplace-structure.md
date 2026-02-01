# Phase 126: Marketplace Directory Structure

> **Status**: NOT_STARTED
> **Effort Estimate**: 3-5 hours
> **Category**: Marketplace & Deployment
> **Prerequisites**: Phase 001 (Solution & Project Structure)

---

## Spec References

This phase implements the marketplace directory structure and hosting setup defined in:

- **spec/marketplace.md** - [Marketplace Architecture](../spec/marketplace.md#marketplace-architecture) (lines 14-42)
- **spec/marketplace.md** - [Plugin Registry](../spec/marketplace.md#plugin-registry) (lines 119-141)
- **structure/marketplace.md** - Marketplace structure summary

---

## Objectives

1. Configure GitHub Pages hosting for the marketplace
2. Create the marketplace directory structure for plugins, manifests, and API
3. Implement the `api/plugins.json` registry format
4. Set up versioned release directories for plugin distribution
5. Organize static assets for the marketplace landing page

---

## Acceptance Criteria

- [ ] GitHub Pages configuration exists in repository settings or `_config.yml`
- [ ] Marketplace directory structure matches spec:
  - [ ] `marketplace/index.html` placeholder exists
  - [ ] `marketplace/plugins/csharp-compounding-docs/` directory exists
  - [ ] `marketplace/plugins/csharp-compounding-docs/manifest.json` exists with full schema
  - [ ] `marketplace/plugins/csharp-compounding-docs/README.md` exists
  - [ ] `marketplace/plugins/csharp-compounding-docs/versions/` directory exists
  - [ ] `marketplace/api/plugins.json` registry file exists
  - [ ] `marketplace/assets/css/` directory exists
  - [ ] `marketplace/assets/images/` directory exists
- [ ] `api/plugins.json` follows the defined registry format with version, updated timestamp, and plugins array
- [ ] `manifest.json` contains all required fields per spec schema
- [ ] `.nojekyll` file exists in marketplace root (for GitHub Pages)
- [ ] CNAME file configured if using custom domain (optional)

---

## Implementation Notes

### GitHub Pages Hosting Setup

GitHub Pages will serve the marketplace from the `marketplace/` directory on the main branch.

**Repository Settings Configuration**:
1. Navigate to Settings > Pages
2. Source: Deploy from a branch
3. Branch: `main` (or `master`)
4. Folder: `/marketplace`

Alternatively, create `.github/workflows/pages.yml` for automated deployment:

```yaml
name: Deploy Marketplace to GitHub Pages

on:
  push:
    branches: [main, master]
    paths:
      - 'marketplace/**'
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
      - name: Checkout
        uses: actions/checkout@v4

      - name: Setup Pages
        uses: actions/configure-pages@v4

      - name: Upload artifact
        uses: actions/upload-pages-artifact@v3
        with:
          path: './marketplace'

      - name: Deploy to GitHub Pages
        id: deployment
        uses: actions/deploy-pages@v4
```

### Directory Structure

Create the following marketplace directory tree per spec/marketplace.md:

```
marketplace/
├── .nojekyll                    # Disable Jekyll processing
├── index.html                   # Marketplace landing page (placeholder)
├── plugins/
│   └── csharp-compounding-docs/
│       ├── manifest.json        # Plugin metadata
│       ├── README.md            # Plugin documentation
│       └── versions/
│           └── .gitkeep         # Placeholder for version directories
├── api/
│   └── plugins.json             # Plugin registry
└── assets/
    ├── css/
    │   └── .gitkeep
    └── images/
        └── .gitkeep
```

### Plugin Manifest (`manifest.json`)

Create the plugin manifest with the full schema defined in spec/marketplace.md:

```json
{
  "$schema": "https://claude.ai/plugin-schema/v1",
  "name": "csharp-compounding-docs",
  "display_name": "CSharp Compound Docs",
  "version": "0.0.0",
  "description": "Capture and retrieve institutional knowledge with RAG-powered semantic search for C#/.NET projects",
  "author": {
    "name": "Your Name",
    "url": "https://github.com/username"
  },
  "repository": "https://github.com/username/csharp-compound-engineering",
  "license": "MIT",
  "keywords": [
    "csharp-compounding-docs",
    "knowledge-management",
    "rag",
    "semantic-search",
    "csharp",
    "dotnet"
  ],
  "claude_code_version": ">=1.0.0",
  "components": {
    "skills": [
      "cdocs:activate",
      "cdocs:problem",
      "cdocs:insight",
      "cdocs:codebase",
      "cdocs:tool",
      "cdocs:style",
      "cdocs:query",
      "cdocs:search",
      "cdocs:search-external",
      "cdocs:query-external",
      "cdocs:delete",
      "cdocs:promote",
      "cdocs:research",
      "cdocs:create-type",
      "cdocs:capture-select",
      "cdocs:todo",
      "cdocs:worktree"
    ],
    "mcp_servers": [
      {
        "name": "csharp-compounding-docs",
        "transport": "stdio",
        "executable": "scripts/launch-mcp-server.ps1"
      }
    ]
  },
  "dependencies": {
    "runtime": [
      "dotnet-10.0",
      "docker",
      "powershell-7"
    ]
  },
  "install": {
    "type": "git-clone",
    "url": "https://github.com/username/csharp-compound-engineering.git",
    "path": "plugins/csharp-compounding-docs"
  }
}
```

### Plugin Registry (`api/plugins.json`)

Create the registry file that lists all available plugins:

```json
{
  "version": "1.0",
  "updated": "2025-01-24T00:00:00Z",
  "plugins": [
    {
      "id": "csharp-compounding-docs",
      "name": "C# Compound Engineering Docs",
      "version": "0.0.0",
      "description": "Capture and retrieve institutional knowledge with RAG-powered semantic search",
      "manifest_url": "/plugins/csharp-compounding-docs/manifest.json",
      "download_url": "/plugins/csharp-compounding-docs/versions/latest/plugin.zip",
      "stars": 0,
      "downloads": 0,
      "categories": [
        "knowledge-management",
        "documentation",
        "rag"
      ],
      "featured": true
    }
  ]
}
```

### Landing Page Placeholder (`index.html`)

Create a minimal placeholder that will be replaced with Nextra in a later phase:

```html
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <title>CSharp Compound Docs - Plugin Marketplace</title>
  <style>
    :root {
      --bg-color: #1a1a2e;
      --text-color: #eaeaea;
      --accent-color: #7c3aed;
    }
    body {
      font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
      background-color: var(--bg-color);
      color: var(--text-color);
      display: flex;
      justify-content: center;
      align-items: center;
      min-height: 100vh;
      margin: 0;
    }
    .container {
      text-align: center;
      padding: 2rem;
    }
    h1 {
      color: var(--accent-color);
    }
    a {
      color: var(--accent-color);
    }
  </style>
</head>
<body>
  <div class="container">
    <h1>CSharp Compound Docs</h1>
    <p>Plugin Marketplace - Coming Soon</p>
    <p>
      <a href="./api/plugins.json">View Plugin Registry</a> |
      <a href="./plugins/csharp-compounding-docs/manifest.json">View Plugin Manifest</a>
    </p>
  </div>
</body>
</html>
```

### Version Directory Structure

When releases are published, the versions directory will be populated:

```
versions/
├── 1.0.0/
│   └── plugin.zip
├── 1.0.1/
│   └── plugin.zip
└── latest -> 1.0.1    # Symlink to latest stable version
```

For now, create the directory with a `.gitkeep` and a README explaining the structure:

**`versions/README.md`**:
```markdown
# Plugin Versions

This directory contains versioned releases of the csharp-compounding-docs plugin.

## Structure

Each version has its own subdirectory:

```
versions/
├── 1.0.0/
│   └── plugin.zip
├── 1.0.1/
│   └── plugin.zip
└── latest -> 1.0.1
```

The `latest` symlink points to the most recent stable release.

## Creating a Release

Releases are created via the release workflow:

1. Tag the commit: `git tag v1.0.0`
2. Push the tag: `git push origin v1.0.0`
3. GitHub Actions packages and deploys the release

See `.github/workflows/release.yml` for details.
```

### Static Assets Organization

Create the assets directory structure for future marketplace styling:

```
assets/
├── css/
│   ├── main.css           # Main stylesheet (created later)
│   └── .gitkeep
└── images/
    ├── logo.svg           # Plugin logo (created later)
    ├── screenshot-*.png   # Plugin screenshots (created later)
    └── .gitkeep
```

### `.nojekyll` File

Create an empty `.nojekyll` file in the marketplace root to prevent GitHub Pages from processing files through Jekyll (which would ignore files starting with underscores):

```bash
touch marketplace/.nojekyll
```

---

## Dependencies

### Depends On
- **Phase 001**: Solution & Project Structure (marketplace/ directory created)

### Blocks
- Phase 127: Marketplace Landing Page (requires directory structure)
- Phase 128: GitHub Actions Release Workflow (requires manifest and registry)
- Phase 129: Plugin Packaging Script (requires versions directory)

---

## Verification Steps

After completing this phase, verify:

1. **Directory structure**: All marketplace directories exist as specified
   ```bash
   tree marketplace/
   ```

2. **JSON validity**: Manifest and registry files are valid JSON
   ```bash
   cat marketplace/plugins/csharp-compounding-docs/manifest.json | jq .
   cat marketplace/api/plugins.json | jq .
   ```

3. **Landing page renders**: Open `marketplace/index.html` in a browser

4. **Git tracking**: All new files are tracked
   ```bash
   git status marketplace/
   ```

5. **GitHub Pages ready**: `.nojekyll` file exists
   ```bash
   ls -la marketplace/.nojekyll
   ```

---

## Notes

- The marketplace URL will be `https://{username}.github.io/csharp-compound-engineering/`
- Version `0.0.0` is used as a placeholder until the first official release
- The landing page placeholder will be replaced with a Nextra-based site in a future phase
- CORS headers are automatically handled by GitHub Pages for JSON files
- The `latest` symlink in versions/ will be created during the first release, not in this phase
- Author information in manifest.json should be updated with actual values before first release
