# Sequential Thinking MCP Package Verification

**Date**: 2025-01-23
**Status**: VERIFIED - Package name in spec is INCORRECT

## Findings

### 1. Is `@anthropics/sequential-thinking-mcp` correct?

**NO** - This package name does not exist.

### 2. Correct Package Name

The correct npm package name is:

```
@modelcontextprotocol/server-sequential-thinking
```

### 3. Availability

- **Published on npm**: Yes
- **Author**: Anthropic, PBC
- **License**: MIT
- **Downloads**: 70,000+ weekly
- **Source**: [GitHub - modelcontextprotocol/servers](https://github.com/modelcontextprotocol/servers/tree/main/src/sequentialthinking)

## Correct Configuration

```json
{
  "mcpServers": {
    "sequential-thinking": {
      "command": "npx",
      "args": ["-y", "@modelcontextprotocol/server-sequential-thinking"]
    }
  }
}
```

## Action Required

Update the spec to use `@modelcontextprotocol/server-sequential-thinking` instead of `@anthropics/sequential-thinking-mcp`.

## Sources

- [npm: @modelcontextprotocol/server-sequential-thinking](https://www.npmjs.com/package/@modelcontextprotocol/server-sequential-thinking)
- [GitHub: Sequential Thinking MCP Server](https://github.com/modelcontextprotocol/servers/tree/main/src/sequentialthinking)
- [PulseMCP: Sequential Thinking Server](https://www.pulsemcp.com/servers/anthropic-sequential-thinking)
