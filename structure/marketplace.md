# Marketplace Structure Summary

This file contains the summary for the marketplace specification.

---

## spec/marketplace.md

### What This File Covers

The Plugin Marketplace Specification defines how the csharp-compounding-docs plugin will be published and distributed via a custom GitHub Pages-hosted marketplace. Key topics include:

- **Marketplace Architecture**: GitHub Pages hosting with a defined directory structure for plugins, manifests, versioned releases, and API endpoints
- **Plugin Manifest Schema**: Complete JSON manifest format including metadata, skills list, MCP server configuration, runtime dependencies, and installation instructions
- **Plugin Registry**: The `api/plugins.json` format that lists all available plugins with download URLs and metadata
- **Marketplace Landing Page**: Features and design decisions, with Nextra (Next.js-based static site generator) selected for implementation
- **Installation Flow**: Native Claude Code plugin installation commands (`claude plugin install/update`) - git clone is explicitly not supported
- **MCP Configuration**: Plugin-level `.mcp.json` and the `${CLAUDE_PLUGIN_ROOT}` environment variable for path resolution
- **External MCP Server Prerequisites**: Check-and-warn architecture via SessionStart hook - the plugin checks for required MCP servers (Context7, Microsoft Learn, Sequential Thinking) but never installs them
- **Release Process**: Semantic versioning strategy, release steps, and GitHub Actions automation for CI/CD
- **Future Enhancements**: Analytics, plugin discovery features, and multi-plugin marketplace support

### Structural Relationships

- **Parent**: [SPEC.md](../SPEC.md) - The root specification document that provides the executive summary and links to all sub-topics
- **Children**: None - This is a leaf specification file
- **Siblings** (other top-level spec files):
  - spec/doc-types.md - Doc-types architecture
  - spec/mcp-server.md - MCP server implementation
  - spec/infrastructure.md - Docker infrastructure
  - spec/skills.md - Skills and commands
  - spec/agents.md - Research agents
  - spec/configuration.md - Configuration schema
  - spec/testing.md - Testing strategy
  - spec/observability.md - Observability/monitoring
  - spec/research-index.md - Research document index
