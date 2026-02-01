# Phase 127: Plugin Manifest Schema

> **Status**: NOT_STARTED
> **Effort Estimate**: 3-4 hours
> **Category**: Marketplace & Deployment
> **Prerequisites**: Phase 126

---

## Spec References

This phase implements the plugin manifest schema defined in:

- **spec/marketplace.md** - [Plugin Manifest](../spec/marketplace.md#plugin-manifest) (lines 46-115)
- **spec/marketplace.md** - [MCP Configuration](../spec/marketplace.md#mcp-configuration) (lines 213-234)
- **spec/marketplace.md** - [External MCP Server Prerequisites](../spec/marketplace.md#external-mcp-server-prerequisites) (lines 238-407)
- **research/claude-plugin-marketplace-ecosystem-research.md** - Plugin manifest schema patterns

---

## Objectives

1. Create the `plugin.json` (manifest.json) file with complete metadata
2. Define all required metadata fields (name, version, description, author)
3. Declare the complete skills list for the plugin
4. Configure MCP server reference for stdio transport
5. Document runtime dependencies (dotnet-10.0, docker, powershell-7)
6. Configure installation instructions for git-clone source type
7. Create JSON schema validation for manifest structure

---

## Acceptance Criteria

- [ ] `manifest.json` file exists at `marketplace/plugins/csharp-compounding-docs/manifest.json`
- [ ] Manifest includes `$schema` reference to Claude plugin schema
- [ ] Core metadata fields populated:
  - [ ] `name`: "csharp-compounding-docs"
  - [ ] `display_name`: "CSharp Compound Docs"
  - [ ] `version`: "1.0.0" (following semver)
  - [ ] `description`: Clear description of plugin purpose
  - [ ] `author`: Name and URL configured
  - [ ] `repository`: GitHub repository URL
  - [ ] `license`: "MIT"
  - [ ] `keywords`: Relevant search terms
- [ ] `claude_code_version` compatibility requirement specified (">=1.0.0")
- [ ] Components section complete:
  - [ ] `skills` array lists all 17 plugin skills
  - [ ] `mcp_servers` array references the plugin's MCP server
- [ ] Dependencies section complete:
  - [ ] Runtime dependencies: dotnet-10.0, docker, powershell-7
- [ ] Install section complete:
  - [ ] `type`: "git-clone"
  - [ ] `url`: Repository clone URL
  - [ ] `path`: Installation path
- [ ] Manifest validates against Claude plugin schema
- [ ] `.mcp.json` file exists at plugin root for MCP server registration

---

## Implementation Notes

### Manifest File Location

Create the manifest at the marketplace plugins directory:

```
marketplace/
└── plugins/
    └── csharp-compounding-docs/
        └── manifest.json
```

### Complete manifest.json

```json
{
  "$schema": "https://claude.ai/plugin-schema/v1",
  "name": "csharp-compounding-docs",
  "display_name": "CSharp Compound Docs",
  "version": "1.0.0",
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
    "dotnet",
    "documentation",
    "institutional-knowledge",
    "compound-engineering"
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

### Metadata Fields Explained

| Field | Purpose | Example |
|-------|---------|---------|
| `$schema` | JSON schema validation reference | `https://claude.ai/plugin-schema/v1` |
| `name` | Unique plugin identifier (kebab-case) | `csharp-compounding-docs` |
| `display_name` | Human-readable name for UI | `CSharp Compound Docs` |
| `version` | Semantic version (MAJOR.MINOR.PATCH) | `1.0.0` |
| `description` | Plugin purpose and capabilities | RAG-powered semantic search... |
| `author.name` | Author's name or organization | Your Name |
| `author.url` | Author's website or profile | GitHub profile URL |
| `repository` | Source code repository URL | GitHub repository URL |
| `license` | SPDX license identifier | `MIT` |
| `keywords` | Search/discovery terms | Array of relevant terms |
| `claude_code_version` | Minimum compatible Claude Code version | `>=1.0.0` |

### Skills List Declaration

The `components.skills` array declares all skills provided by the plugin:

**Capture Skills** (document type-specific):
- `cdocs:problem` - Capture problem documentation
- `cdocs:insight` - Capture insight documentation
- `cdocs:codebase` - Capture codebase documentation
- `cdocs:tool` - Capture tool documentation
- `cdocs:style` - Capture style documentation

**Query Skills**:
- `cdocs:query` - RAG-based contextual query
- `cdocs:search` - Semantic vector search
- `cdocs:search-external` - Search external knowledge base
- `cdocs:query-external` - RAG query against external docs

**Management Skills**:
- `cdocs:activate` - Activate project context
- `cdocs:delete` - Delete documents
- `cdocs:promote` - Promote document priority
- `cdocs:create-type` - Create custom document types

**Utility Skills**:
- `cdocs:research` - Multi-source research workflow
- `cdocs:capture-select` - Interactive document type selection
- `cdocs:todo` - TODO documentation capture
- `cdocs:worktree` - Git worktree integration

### MCP Server Configuration Reference

The `components.mcp_servers` array references MCP servers bundled with the plugin:

```json
{
  "name": "csharp-compounding-docs",
  "transport": "stdio",
  "executable": "scripts/launch-mcp-server.ps1"
}
```

| Field | Purpose |
|-------|---------|
| `name` | Server identifier for registration |
| `transport` | Communication protocol (`stdio`, `sse`, `http`) |
| `executable` | Path to launch script (relative to plugin root) |

### Plugin Root .mcp.json

Create `.mcp.json` at the plugin root for MCP server auto-registration:

```json
{
  "mcpServers": {
    "csharp-compounding-docs": {
      "command": "pwsh",
      "args": [
        "-File",
        "${CLAUDE_PLUGIN_ROOT}/scripts/launch-mcp-server.ps1"
      ]
    }
  }
}
```

**Note**: `${CLAUDE_PLUGIN_ROOT}` is resolved by Claude Code to the plugin's installation directory.

### Runtime Dependencies

The `dependencies.runtime` array specifies required system dependencies:

| Dependency | Version | Purpose |
|------------|---------|---------|
| `dotnet-10.0` | .NET 10 SDK/Runtime | MCP server execution |
| `docker` | Latest | PostgreSQL/pgvector containers |
| `powershell-7` | PowerShell 7+ | Launch scripts, automation |

### Installation Instructions

The `install` section defines how Claude Code installs the plugin:

```json
{
  "type": "git-clone",
  "url": "https://github.com/username/csharp-compound-engineering.git",
  "path": "plugins/csharp-compounding-docs"
}
```

**Installation Types**:
- `git-clone` - Clone from git repository
- `npm` - Install from npm registry
- `download` - Download release archive

**Path Resolution**: The `path` is relative to Claude Code's plugin directory (`~/.claude/plugins/` for user scope, `.claude/plugins/` for project scope).

### Directory Structure

After this phase, the marketplace structure includes:

```
marketplace/
├── plugins/
│   └── csharp-compounding-docs/
│       └── manifest.json          # Plugin manifest (this phase)
├── api/
│   └── plugins.json               # Plugin registry (Phase 128)
└── index.html                     # Marketplace landing (Phase 129)

# At plugin root
.mcp.json                          # MCP server registration
```

### Version Management

The manifest version follows semantic versioning:

- **MAJOR**: Breaking changes to skills or MCP tools
- **MINOR**: New features, new document types
- **PATCH**: Bug fixes, documentation updates

Version update locations when releasing:
1. `manifest.json` - `version` field
2. `src/CompoundDocs.McpServer/CompoundDocs.McpServer.csproj` - `Version` property
3. `CHANGELOG.md` - Release notes

### Schema Validation

Validate the manifest against the Claude plugin schema:

```bash
# Using ajv-cli (Node.js)
npx ajv validate -s https://claude.ai/plugin-schema/v1.json -d manifest.json

# Using Python jsonschema
python -c "
import json
import jsonschema
import urllib.request

schema = json.loads(urllib.request.urlopen('https://claude.ai/plugin-schema/v1.json').read())
manifest = json.load(open('manifest.json'))
jsonschema.validate(manifest, schema)
print('Manifest is valid')
"
```

---

## Dependencies

### Depends On
- **Phase 126**: Marketplace Directory Structure (provides `marketplace/plugins/` directory)

### Blocks
- **Phase 128**: Plugin Registry API (`api/plugins.json`)
- **Phase 129**: Marketplace Landing Page (reads manifest for plugin details)
- **Phase 130**: Release Packaging Script (packages manifest with plugin)

---

## Verification Steps

After completing this phase, verify:

1. **Manifest file exists**:
   ```bash
   cat marketplace/plugins/csharp-compounding-docs/manifest.json
   ```

2. **JSON is valid**:
   ```bash
   python -m json.tool marketplace/plugins/csharp-compounding-docs/manifest.json
   ```

3. **Required fields present**:
   ```bash
   jq '.name, .version, .description, .components.skills | length' \
     marketplace/plugins/csharp-compounding-docs/manifest.json
   ```

4. **Skills count matches expected (17 skills)**:
   ```bash
   jq '.components.skills | length' marketplace/plugins/csharp-compounding-docs/manifest.json
   # Expected output: 17
   ```

5. **MCP server configured**:
   ```bash
   jq '.components.mcp_servers[0]' marketplace/plugins/csharp-compounding-docs/manifest.json
   ```

6. **Plugin root .mcp.json exists**:
   ```bash
   cat .mcp.json
   ```

7. **Dependencies specified**:
   ```bash
   jq '.dependencies.runtime' marketplace/plugins/csharp-compounding-docs/manifest.json
   ```

---

## Notes

- The manifest schema URL (`https://claude.ai/plugin-schema/v1`) is a placeholder; use the actual schema URL when Claude Code publishes the official schema
- Author information should be updated with actual maintainer details before publishing
- Repository URL should be updated to the actual GitHub repository
- The `keywords` array should be expanded based on marketplace search optimization
- MCP server registration via `.mcp.json` uses the `${CLAUDE_PLUGIN_ROOT}` environment variable which Claude Code resolves at runtime
- Runtime dependencies are informational; Claude Code does not automatically install them
- Future enhancement: Add `dependencies.plugins` for plugin-to-plugin dependencies when Claude Code supports it (Issue #9444)
