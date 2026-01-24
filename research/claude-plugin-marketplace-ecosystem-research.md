# Claude Plugin Marketplace Ecosystem Research

**Research Date:** January 22, 2026
**Research Focus:** Claude/MCP Plugin Marketplaces, Registries, and Distribution Patterns

---

## Executive Summary

This research documents the comprehensive ecosystem of Claude Code plugins and Model Context Protocol (MCP) server registries. The ecosystem consists of two primary components:

1. **Claude Code Plugin Marketplaces** - Anthropic-managed and community-driven repositories for Claude Code extensions
2. **MCP Server Registries** - Centralized and community directories for discovering MCP servers

Both systems have matured significantly, with official registries, standardized metadata schemas, and multiple distribution mechanisms.

---

## 1. Official Anthropic/Claude Marketplaces

### 1.1 Claude Code Plugin Directory (Official)

**Repository:** [anthropics/claude-plugins-official](https://github.com/anthropics/claude-plugins-official)

Anthropic maintains an official, managed directory of high-quality Claude Code plugins. This is the primary source for officially vetted plugins.

**Structure:**
```
claude-plugins-official/
├── /plugins                 # Internal plugins (Anthropic-maintained)
├── /external_plugins        # Third-party plugins (community & partners)
├── /.claude-plugin/
│   └── marketplace.json     # Central registry file
├── /.github/workflows       # CI/CD workflows
└── README.md
```

**Statistics (as of research date):**
- Stars: 4.6k
- Forks: 499
- Total Commits: 41
- Languages: Shell (56.9%), Python (43.1%)

**Official Plugin Categories:**
- Development (7 plugins)
- Productivity (4 plugins)
- Learning (2 plugins)
- Security (1 plugin)
- Migration (1 plugin)

**Installation:**
```bash
/plugin install {plugin-name}@claude-plugin-directory
```

### 1.2 Life Sciences Marketplace

**Repository:** [anthropics/life-sciences](https://github.com/anthropics/life-sciences)

A specialized marketplace for the Claude for Life Sciences initiative, providing MCP servers and skills for life sciences research and analysis tools.

### 1.3 Claude Code Built-in Marketplace

Claude Code ships with an integrated marketplace accessible via:
```bash
/plugin > Discover
```

The built-in marketplace automatically includes the official Anthropic plugin directory when Claude Code starts.

---

## 2. MCP Server Registries

### 2.1 Official MCP Registry

**URL:** [registry.modelcontextprotocol.io](https://registry.modelcontextprotocol.io/)
**Documentation:** [registry.modelcontextprotocol.io/docs](https://registry.modelcontextprotocol.io/docs)
**Repository:** [modelcontextprotocol/registry](https://github.com/modelcontextprotocol/registry)

The official MCP Registry is a community-driven registry service launched in September 2025. It serves as the authoritative repository and single source of truth for publicly-available MCP servers.

**Key Characteristics:**
- **Community-owned** by the MCP open-source community
- **Backed by trusted contributors** including Anthropic, GitHub, PulseMCP, and Microsoft
- **Unified discovery** - server creators publish once, all consumers reference the same canonical data
- **Metaregistry design** - hosts metadata about packages, not the package code or binaries

**API Status:** API freeze (v0.1) as of October 2025, ensuring stable endpoints for integrators.

**History:**
- February 2025: MCP creators David Soria Parra and Justin Spahr-Summers initiated the project
- Registry Maintainer Tadas Antanavicius (PulseMCP) led initial development
- Collaboration with Alex Hancock from Block

#### API Endpoints

**Base URL:** `https://registry.modelcontextprotocol.io`

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/v0/servers` | GET | Paginated list with filtering |
| `/v0/servers/{id}` | GET | Server details |

**Query Parameters for `/v0/servers`:**
- `limit` - Results per page (max 100)
- `offset` - Pagination offset
- `search` - Search query string

**Example Requests:**
```bash
# List 10 servers
curl "https://registry.modelcontextprotocol.io/v0/servers?limit=10"

# Search for filesystem servers
curl "https://registry.modelcontextprotocol.io/v0/servers?search=filesystem"
```

### 2.2 GitHub MCP Server Repository

**Repository:** [modelcontextprotocol/servers](https://github.com/modelcontextprotocol/servers)

The official GitHub repository for Model Context Protocol reference server implementations.

---

## 3. Community MCP Server Directories

### 3.1 Awesome MCP Servers Lists

Multiple community-maintained lists exist on GitHub:

| Repository | Description | Focus |
|------------|-------------|-------|
| [punkpeye/awesome-mcp-servers](https://github.com/punkpeye/awesome-mcp-servers) | Primary community list | General |
| [wong2/awesome-mcp-servers](https://github.com/wong2/awesome-mcp-servers) | Curated list | General |
| [appcypher/awesome-mcp-servers](https://github.com/appcypher/awesome-mcp-servers) | Production-ready focus | General |
| [TensorBlock/awesome-mcp-servers](https://github.com/TensorBlock/awesome-mcp-servers) | Comprehensive collection | General |
| [ever-works/awesome-mcp-servers](https://github.com/ever-works/awesome-mcp-servers) | Best solutions focus | General |
| [rohitg00/awesome-devops-mcp-servers](https://github.com/rohitg00/awesome-devops-mcp-servers) | DevOps-focused | DevOps |
| [jaw9c/awesome-remote-mcp-servers](https://github.com/jaw9c/awesome-remote-mcp-servers) | Remote/cloud servers | Remote |
| [MobinX/awesome-mcp-list](https://github.com/MobinX/awesome-mcp-list) | Concise list | General |

**Category Organization (Emoji-based):**
- Aggregators
- Art & Culture
- Architecture & Design
- Browser Automation
- Biology, Medicine and Bioinformatics
- Cloud Platforms
- Code Execution
- Coding Agents
- Command Line
- Communication
- Databases
- Data Platforms
- Developer Tools
- (20+ total categories)

**Badge System:**
- Programming languages: Python, TypeScript/JavaScript, Go, Rust, C#, Java
- Deployment scope: Cloud Service, Local Service, Embedded Systems
- Operating systems: macOS, Windows, Linux
- Official status marker for verified implementations

### 3.2 PulseMCP

**URL:** [pulsemcp.com/servers](https://www.pulsemcp.com/servers)

A daily-updated directory currently listing 7,900+ MCP servers. PulseMCP has an active member on the MCP Steering Committee and was instrumental in building the official MCP Registry.

**Features:**
- Daily updates
- REST API access for partners
- Comprehensive server information

### 3.3 Smithery

**URL:** [smithery.ai](https://smithery.ai/)

A platform for finding and shipping language model extensions compatible with MCP.

**Features:**
- Server verification system (`is:verified` filter)
- Security scanning (`scanPassed` boolean)
- Hosted/Remote and Local deployment options
- CLI tool for server management
- API with authentication

**API Base:** `https://registry.smithery.ai/servers`

**Query Parameters:**
- `q` - Search query
- `page` - Page number
- `pageSize` - Results per page

**Filters:**
- `is:verified` - Show only verified servers
- `owner:username` - Filter by owner

### 3.4 Glama

**URL:** [glama.ai/mcp/servers](https://glama.ai/mcp/servers)

Hosts the most comprehensive registry with daily updates and ChatGPT-like UI access.

**Features:**
- Daily updates
- Security, compatibility, and ease-of-use rankings
- Usage-based sorting (last 30 days)
- Multiple transport support
- API gateway access

### 3.5 Additional Directories

| Platform | URL | Servers Listed |
|----------|-----|----------------|
| mcp.so | [mcp.so](https://mcp.so/) | 17,387+ |
| MCP Market | [mcpmarket.com](https://mcpmarket.com/) | Curated collection |
| LobeHub | [lobehub.com/mcp](https://lobehub.com/mcp) | Multi-dimensional ratings |
| MCP Server Finder | [mcpserverfinder.com](https://www.mcpserverfinder.com/) | Comprehensive directory |
| Awesome MCP Servers | [mcpservers.org](https://mcpservers.org/) | Curated collection |

### 3.6 Cline MCP Marketplace

**Repository:** [cline/mcp-marketplace](https://github.com/cline/mcp-marketplace)

Official repository for submitting MCP servers to Cline's marketplace, featuring one-click installation.

---

## 4. Marketplace Architecture Patterns

### 4.1 Claude Code marketplace.json Schema

**Location:** `.claude-plugin/marketplace.json` in repository root

**Complete Schema:**

```json
{
  "name": "marketplace-name",
  "owner": {
    "name": "Maintainer Name",
    "email": "[email protected]"
  },
  "metadata": {
    "description": "Marketplace description",
    "version": "1.0.0",
    "pluginRoot": "./plugins"
  },
  "plugins": [
    {
      "name": "plugin-name",
      "source": "./path/to/plugin",
      "description": "Plugin description",
      "version": "1.0.0",
      "author": {
        "name": "Author Name",
        "email": "[email protected]"
      },
      "homepage": "https://docs.example.com",
      "repository": "https://github.com/owner/repo",
      "license": "MIT",
      "keywords": ["tag1", "tag2"],
      "category": "productivity",
      "tags": ["search", "tag"],
      "strict": true,
      "commands": "./commands/",
      "agents": ["./agents/agent.md"],
      "hooks": {},
      "mcpServers": {},
      "lspServers": {}
    }
  ]
}
```

**Required Fields:**
- `name` (marketplace): Kebab-case identifier
- `owner.name`: Maintainer name
- `plugins`: Array of plugin entries
- Plugin `name`: Kebab-case identifier
- Plugin `source`: Path or source object

**Source Resolution Patterns:**

1. **Relative Paths:**
```json
{ "source": "./plugins/my-plugin" }
```

2. **GitHub Repositories:**
```json
{
  "source": {
    "source": "github",
    "repo": "owner/plugin-repo",
    "ref": "v2.0.0",
    "sha": "40-character-commit-hash"
  }
}
```

3. **Git URLs:**
```json
{
  "source": {
    "source": "url",
    "url": "https://gitlab.com/team/plugin.git",
    "ref": "main",
    "sha": "commit-hash"
  }
}
```

**Reserved Marketplace Names:**
- `claude-code-marketplace`
- `claude-code-plugins`
- `claude-plugins-official`
- `anthropic-marketplace`
- `anthropic-plugins`
- `agent-skills`
- `life-sciences`

### 4.2 MCP Registry server.json Schema

**Schema URL:** `https://static.modelcontextprotocol.io/schemas/2025-07-09/server.schema.json`

**Complete Schema:**

```json
{
  "$schema": "https://static.modelcontextprotocol.io/schemas/2025-07-09/server.schema.json",
  "name": "io.github.username/server-name",
  "title": "Human Readable Title",
  "description": "Server description",
  "version": "1.0.0",
  "packages": [
    {
      "registry_type": "npm",
      "identifier": "@scope/package-name",
      "version": "1.0.0",
      "transport": {
        "type": "stdio"
      }
    }
  ],
  "remotes": [
    {
      "type": "streamable-http",
      "url": "https://example.com/mcp"
    }
  ]
}
```

**Package Registry Types:**

| Type | Example |
|------|---------|
| npm | `"registry_type": "npm", "identifier": "@scope/package"` |
| pypi | `"registry_type": "pypi", "identifier": "package-name"` |
| nuget | `"registry_type": "nuget", "identifier": "Package.Name"` |
| oci (Docker) | `"registry_type": "oci", "registry_base_url": "https://docker.io"` |
| oci (GHCR) | `"registry_type": "oci", "registry_base_url": "https://ghcr.io"` |

**Docker Hub Example:**
```json
{
  "packages": [
    {
      "registry_type": "oci",
      "registry_base_url": "https://docker.io",
      "identifier": "username/mcp-server",
      "version": "1.0.0"
    }
  ]
}
```

**GitHub Container Registry Example:**
```json
{
  "packages": [
    {
      "registry_type": "oci",
      "registry_base_url": "https://ghcr.io",
      "identifier": "username/mcp-server",
      "version": "1.0.0"
    }
  ]
}
```

**Metadata Extension:**
```json
{
  "_meta": {
    "status": "active",
    "publishedAt": "2025-01-22T00:00:00Z",
    "updatedAt": "2025-01-22T00:00:00Z",
    "isLatest": true,
    "io.modelcontextprotocol.registry/publisher-provided": {
      "custom": "metadata"
    }
  }
}
```

---

## 5. Plugin Metadata Standards

### 5.1 Claude Code Plugin Categories

Valid category values:
- `productivity`
- `security`
- `testing`
- `deployment`
- `documentation`
- `analysis`
- `integration`
- `ai`
- `devops`
- `debugging`
- `code-quality`
- `design`
- `example`
- `api-development`
- `database`
- `crypto`
- `performance`
- `ai-ml`
- `development`
- `learning`
- `migration`
- `other`

### 5.2 Required Plugin Fields

**For Claude Code Plugins:**
- `name` - Kebab-case identifier
- `source` - Path or source object
- `description` - Clear description
- `version` - Semver format (x.y.z)
- `category` - From valid category list
- `keywords` - At least 2 tags
- `author` - Object with name and email

### 5.3 MCP Tool Definition Schema

Each MCP tool is defined with:
```json
{
  "name": "tool_name",
  "description": "Human-readable description",
  "inputSchema": {
    "type": "object",
    "properties": {
      "parameter": {
        "type": "string",
        "description": "Parameter description"
      }
    },
    "required": ["parameter"]
  }
}
```

---

## 6. Versioning Conventions

### 6.1 Semantic Versioning

Both Claude Code plugins and MCP servers use semantic versioning (semver):
- Format: `MAJOR.MINOR.PATCH`
- Example: `1.2.3`

### 6.2 MCP Registry Versioning

- Unique version strings required for updates
- Semantic versions are parsed for proper ordering
- Non-semantic versions ordered by publication timestamp
- Version ranges (e.g., `^1.2.3`, `~1.2.3`, `>=1.2.3`, `1.x`) are rejected

### 6.3 Schema Versioning

MCP Registry uses dated schema versions:
- Format: `YYYY-MM-DD`
- URL: `https://static.modelcontextprotocol.io/schemas/{date}/server.schema.json`
- Enables backward-compatible evolution

---

## 7. Distribution Methods

### 7.1 npm (Node.js/JavaScript)

**Execution:** `npx` downloads and runs packages without global installation

```bash
npx -y @scope/mcp-server
```

**Configuration:**
```json
{
  "mcpServers": {
    "server-name": {
      "command": "npx",
      "args": ["-y", "@scope/mcp-server"]
    }
  }
}
```

### 7.2 PyPI (Python)

**Execution:** `uvx` (from uv package manager) or `pipx`

```bash
uvx mcp-server-name
# or
pipx run mcp-server-name
```

**Official MCP SDK:** `mcp` package (v1.25.0 latest)

### 7.3 NuGet (.NET)

MCP servers can be packaged and distributed via NuGet. The server.json references the NuGet package identifier.

### 7.4 Docker/OCI Containers

**Benefits:**
- Isolation and security
- Production-ready deployments
- Consistent environments

**Docker Hub:**
```bash
docker run --rm -i ghcr.io/username/mcp-server
```

**Configuration:**
```json
{
  "mcpServers": {
    "server-name": {
      "command": "docker",
      "args": ["run", "-i", "--rm", "image-name"]
    }
  }
}
```

### 7.5 GitHub Releases

Direct file downloads from GitHub releases for standalone executables or archives.

### 7.6 Direct Repository References

Both Claude Code plugins and MCP servers support direct Git repository references with optional version pinning.

---

## 8. Transport Mechanisms

### 8.1 STDIO (Standard Input/Output)

- Local integrations and command-line tools
- Inherently stateful connections
- Subprocess persists for connection lifetime

### 8.2 SSE (Server-Sent Events)

- Legacy method for remote server communication
- HTTP POST for client-to-server
- Server-to-client streaming
- Deprecated in favor of Streamable HTTP

### 8.3 Streamable HTTP (Recommended)

- Modern standard for remote MCP servers
- Single HTTP endpoint supporting POST and GET
- Recommended for production deployments
- Supports stateless mode for scalability

### 8.4 Protocol

All MCP communication uses JSON-RPC 2.0 wire protocol.

---

## 9. Submission Processes

### 9.1 Claude Code Official Directory

**Submission Form:** [forms.gle/tyiAZvch1kDADKoP9](https://forms.gle/tyiAZvch1kDADKoP9)

**Mandatory Requirements:**
1. Safety annotations on every tool
2. Server details and documentation links
3. Test credentials
4. Minimum 3 examples
5. Contact information
6. Comprehensive README.md documentation

**Common Rejection Reasons (90% of revision requests):**
- Missing/inaccurate safety annotations
- Incomplete documentation
- Missing required metadata

### 9.2 MCP Registry Publishing

**CLI Tool:** `mcp-publisher`

```bash
# Generate template
mcp-publisher init

# Authenticate
mcp-publisher auth

# Publish
mcp-publisher publish
```

**Namespace Requirements:**
- GitHub-based: `io.github.username/server-name`
- Domain-based: `me.example/server-name` (requires DNS/HTTP verification)

**Authentication Methods:**
1. GitHub OAuth
2. GitHub OIDC (for Actions)
3. DNS verification
4. HTTP verification

**Note:** Packages must be published to npm/PyPI/NuGet before publishing to MCP Registry.

### 9.3 Community Marketplace Creation

No submission process required:
1. Create GitHub repository
2. Add `.claude-plugin/marketplace.json`
3. Follow schema specifications
4. Community search systems auto-discover within 24 hours

---

## 10. Package Management Tools

### 10.1 mcp-get

**URL:** [mcp-get.com](https://mcp-get.com/)
**npm:** `@michaellatman/mcp-get`

**Features:**
- Discover, install, update, remove MCP packages
- Requires Node.js 18+

**Commands:**
```bash
npx @michaellatman/mcp-get install <package>
npx @michaellatman/mcp-get list
npx @michaellatman/mcp-get search <query>
```

### 10.2 @mcpm/cli

Alternative MCP package manager with:
- JSON configuration management
- Package discovery
- Community package support

### 10.3 Claude Code Plugin CLI

```bash
# Marketplace management
/plugin marketplace add <url-or-repo>
/plugin marketplace list
/plugin marketplace update <n>
/plugin marketplace remove <n>

# Plugin installation
/plugin install <plugin-name>@<marketplace>

# Validation
/plugin validate .
```

---

## 11. Security Considerations

### 11.1 Claude Code Plugins

- Anthropic does not control third-party plugin content
- Cannot verify plugins work as intended
- Users must trust plugins before installation
- Organizations can use `strictKnownMarketplaces` to restrict sources

### 11.2 MCP Servers

- Use environment variables for tokens
- Avoid untrusted data in fields
- Verify server security posture
- Local-first approach recommended

### 11.3 Authentication for Private Repos

| Provider | Environment Variables |
|----------|----------------------|
| GitHub | `GITHUB_TOKEN`, `GH_TOKEN` |
| GitLab | `GITLAB_TOKEN`, `GL_TOKEN` |
| Bitbucket | `BITBUCKET_TOKEN` |

---

## 12. Example Marketplace Configurations

### 12.1 Company Internal Marketplace

```json
{
  "name": "acme-corp-tools",
  "owner": {
    "name": "ACME Engineering",
    "email": "[email protected]"
  },
  "metadata": {
    "description": "Internal development tools for ACME Corp",
    "version": "2.0.0",
    "pluginRoot": "./plugins"
  },
  "plugins": [
    {
      "name": "code-standards",
      "source": "standards-checker",
      "description": "Enforces ACME coding standards",
      "version": "1.5.0",
      "author": { "name": "Platform Team" },
      "category": "code-quality",
      "keywords": ["linting", "standards", "quality"]
    },
    {
      "name": "deploy-helper",
      "source": {
        "source": "github",
        "repo": "acme-corp/deploy-plugin",
        "ref": "v3.0.0"
      },
      "description": "Deployment automation for ACME infrastructure",
      "version": "3.0.0",
      "category": "deployment",
      "keywords": ["deploy", "automation", "kubernetes"]
    }
  ]
}
```

### 12.2 MCP Server for Multiple Registries

```json
{
  "$schema": "https://static.modelcontextprotocol.io/schemas/2025-07-09/server.schema.json",
  "name": "io.github.myorg/multi-platform-server",
  "title": "Multi-Platform MCP Server",
  "description": "Available on npm, PyPI, and Docker",
  "version": "2.1.0",
  "packages": [
    {
      "registry_type": "npm",
      "identifier": "@myorg/mcp-server",
      "version": "2.1.0",
      "transport": { "type": "stdio" }
    },
    {
      "registry_type": "pypi",
      "identifier": "myorg-mcp-server",
      "version": "2.1.0",
      "transport": { "type": "stdio" }
    },
    {
      "registry_type": "oci",
      "registry_base_url": "https://ghcr.io",
      "identifier": "myorg/mcp-server",
      "version": "2.1.0",
      "transport": { "type": "stdio" }
    }
  ],
  "remotes": [
    {
      "type": "streamable-http",
      "url": "https://mcp.myorg.io/server"
    }
  ]
}
```

---

## 13. Key URLs and Resources

### Official Resources

| Resource | URL |
|----------|-----|
| Claude Code Docs | [code.claude.com/docs](https://code.claude.com/docs) |
| Plugin Marketplace Docs | [code.claude.com/docs/en/plugin-marketplaces](https://code.claude.com/docs/en/plugin-marketplaces) |
| Official Plugins | [github.com/anthropics/claude-plugins-official](https://github.com/anthropics/claude-plugins-official) |
| MCP Registry | [registry.modelcontextprotocol.io](https://registry.modelcontextprotocol.io/) |
| MCP Registry Docs | [registry.modelcontextprotocol.io/docs](https://registry.modelcontextprotocol.io/docs) |
| MCP Specification | [modelcontextprotocol.io](https://modelcontextprotocol.io/) |

### Community Directories

| Directory | URL | Description |
|-----------|-----|-------------|
| PulseMCP | [pulsemcp.com/servers](https://www.pulsemcp.com/servers) | 7,900+ servers, daily updates |
| Smithery | [smithery.ai](https://smithery.ai/) | Verified servers, CLI tool |
| Glama | [glama.ai/mcp/servers](https://glama.ai/mcp/servers) | Largest collection, usage rankings |
| mcp.so | [mcp.so](https://mcp.so/) | 17,387+ servers |
| MCP Market | [mcpmarket.com](https://mcpmarket.com/) | Curated collection |
| mcp-get | [mcp-get.com](https://mcp-get.com/) | Package manager |

### GitHub Resources

| Repository | Purpose |
|------------|---------|
| [modelcontextprotocol/registry](https://github.com/modelcontextprotocol/registry) | Official registry source |
| [modelcontextprotocol/servers](https://github.com/modelcontextprotocol/servers) | Reference implementations |
| [punkpeye/awesome-mcp-servers](https://github.com/punkpeye/awesome-mcp-servers) | Primary awesome list |
| [cline/mcp-marketplace](https://github.com/cline/mcp-marketplace) | Cline marketplace |

---

## 14. Conclusion

The Claude plugin and MCP server ecosystem has developed into a mature, multi-layered system with:

1. **Official channels** through Anthropic's plugin directory and the community-driven MCP Registry
2. **Standardized schemas** for both marketplace.json (Claude) and server.json (MCP)
3. **Multiple distribution options** including npm, PyPI, NuGet, Docker, and direct Git references
4. **Rich community directories** providing discovery across 17,000+ MCP servers
5. **Clear submission processes** with defined quality requirements
6. **Security considerations** built into the ecosystem design

For developers looking to distribute Claude Code plugins or MCP servers, the ecosystem provides flexible options from fully managed official directories to self-hosted community marketplaces.

---

## Sources

- [Anthropic Claude Code Plugin Documentation](https://code.claude.com/docs/en/plugin-marketplaces)
- [Official Claude Plugins Repository](https://github.com/anthropics/claude-plugins-official)
- [MCP Registry](https://registry.modelcontextprotocol.io/)
- [MCP Registry GitHub](https://github.com/modelcontextprotocol/registry)
- [Model Context Protocol Specification](https://modelcontextprotocol.io/)
- [PulseMCP](https://www.pulsemcp.com/)
- [Smithery](https://smithery.ai/)
- [Glama](https://glama.ai/mcp)
- [mcp-get](https://mcp-get.com/)
- [GitHub Blog - MCP Registry](https://github.blog/ai-and-ml/generative-ai/how-to-find-install-and-manage-mcp-servers-with-the-github-mcp-registry/)
- [Nordic APIs - MCP Registry API](https://nordicapis.com/getting-started-with-the-official-mcp-registry-api/)
- [punkpeye/awesome-mcp-servers](https://github.com/punkpeye/awesome-mcp-servers)
