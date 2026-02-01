# Phase 129: Nextra Marketplace Landing Page

> **Category**: Marketplace & Deployment
> **Prerequisites**: Phase 126 (GitHub Pages Infrastructure)
> **Estimated Effort**: 8-12 hours
> **Status**: Pending

---

## Objective

Create a modern, interactive marketplace landing page using Nextra (Next.js-based static site generator) to showcase the CSharp Compound Docs plugin. The landing page will provide feature highlights, installation instructions, documentation links, and be deployed as a static site to GitHub Pages.

---

## Success Criteria

- [ ] Nextra project initialized with docs theme in `marketplace/` directory
- [ ] `next.config.mjs` configured for GitHub Pages static export
- [ ] Landing page with plugin hero section and feature showcase
- [ ] Installation instructions with copy-to-clipboard functionality
- [ ] Documentation links to plugin README and skill reference
- [ ] Dark mode support matching Claude Code aesthetic
- [ ] Mobile-responsive design using Tailwind CSS
- [ ] GitHub Actions workflow for automated deployment
- [ ] `.nojekyll` file included in build output
- [ ] Pagefind search integration configured

---

## Specification References

| Document | Section | Relevance |
|----------|---------|-----------|
| [spec/marketplace.md](../spec/marketplace.md) | Marketplace Landing Page | Features, design considerations, technology decision |
| [spec/marketplace.md](../spec/marketplace.md) | Marketplace Architecture | Directory structure and hosting details |
| [research/static-site-generator-marketplace-research.md](../research/static-site-generator-marketplace-research.md) | Option 4: Nextra | Complete Nextra setup guide and rationale |
| [research/static-site-generator-marketplace-research.md](../research/static-site-generator-marketplace-research.md) | GitHub Pages Deployment | Configuration and workflow requirements |

---

## Tasks

### Task 129.1: Initialize Nextra Project

Create the Nextra project structure in the `marketplace/` directory.

**Commands**:
```bash
cd marketplace
npx create-next-app@latest . --typescript --tailwind --eslint --app=false --src-dir=false
npm install nextra nextra-theme-docs
```

**Directory Structure After Initialization**:
```
marketplace/
├── pages/
│   ├── _app.tsx
│   ├── _document.tsx
│   └── index.mdx
├── components/
├── data/
├── public/
├── styles/
│   └── globals.css
├── theme.config.tsx
├── next.config.mjs
├── package.json
├── tsconfig.json
└── tailwind.config.js
```

---

### Task 129.2: Configure Next.js for Static Export

Create `marketplace/next.config.mjs`:

```javascript
import nextra from 'nextra';

const withNextra = nextra({
  theme: 'nextra-theme-docs',
  themeConfig: './theme.config.tsx',
  staticImage: true,
  defaultShowCopyCode: true,
});

export default withNextra({
  output: 'export',
  images: { unoptimized: true },
  basePath: '/csharp-compound-engineering',
  assetPrefix: '/csharp-compound-engineering/',
  trailingSlash: true,
  reactStrictMode: true,
});
```

**Key Configuration Notes**:
- `output: 'export'` - Required for static site generation
- `images: { unoptimized: true }` - Required for static export (no image optimization API)
- `basePath` - Matches GitHub Pages repository subpath
- `trailingSlash: true` - Ensures consistent URL behavior

---

### Task 129.3: Create Theme Configuration

Create `marketplace/theme.config.tsx`:

```tsx
import React from 'react';
import { DocsThemeConfig } from 'nextra-theme-docs';

const config: DocsThemeConfig = {
  logo: (
    <span style={{ fontWeight: 700 }}>
      CSharp Compound Docs
    </span>
  ),
  project: {
    link: 'https://github.com/username/csharp-compound-engineering',
  },
  docsRepositoryBase: 'https://github.com/username/csharp-compound-engineering/tree/main/marketplace',
  useNextSeoProps() {
    return {
      titleTemplate: '%s - CSharp Compound Docs Plugin',
    };
  },
  head: (
    <>
      <meta name="viewport" content="width=device-width, initial-scale=1.0" />
      <meta name="description" content="RAG-powered knowledge management for C#/.NET projects" />
      <meta property="og:title" content="CSharp Compound Docs Plugin" />
      <meta property="og:description" content="Capture and retrieve institutional knowledge with semantic search" />
    </>
  ),
  banner: {
    key: 'v1-release',
    text: (
      <a href="/csharp-compound-engineering/getting-started">
        CSharp Compound Docs v1.0 is released! Read the getting started guide →
      </a>
    ),
  },
  footer: {
    text: (
      <span>
        MIT {new Date().getFullYear()} © CSharp Compound Docs
      </span>
    ),
  },
  primaryHue: 210,
  darkMode: true,
  nextThemes: {
    defaultTheme: 'dark',
  },
};

export default config;
```

---

### Task 129.4: Create Plugin Data Model

Create `marketplace/data/plugin.ts`:

```typescript
export interface PluginFeature {
  title: string;
  description: string;
  icon: string;
}

export interface PluginSkill {
  name: string;
  command: string;
  description: string;
  category: 'capture' | 'query' | 'manage' | 'workflow';
}

export interface PluginInfo {
  name: string;
  displayName: string;
  version: string;
  description: string;
  author: {
    name: string;
    url: string;
  };
  repository: string;
  license: string;
  keywords: string[];
  features: PluginFeature[];
  skills: PluginSkill[];
  dependencies: {
    runtime: string[];
    mcpServers: string[];
  };
  installCommands: {
    interactive: string;
    cli: string;
    projectScope: string;
  };
}

export const pluginInfo: PluginInfo = {
  name: 'csharp-compounding-docs',
  displayName: 'CSharp Compound Docs',
  version: '1.0.0',
  description: 'Capture and retrieve institutional knowledge with RAG-powered semantic search for C#/.NET projects',
  author: {
    name: 'Your Name',
    url: 'https://github.com/username',
  },
  repository: 'https://github.com/username/csharp-compound-engineering',
  license: 'MIT',
  keywords: [
    'knowledge-management',
    'rag',
    'semantic-search',
    'csharp',
    'dotnet',
    'claude-code',
    'mcp',
  ],
  features: [
    {
      title: 'RAG-Powered Search',
      description: 'Semantic vector search using local embeddings with pgvector for fast, accurate knowledge retrieval.',
      icon: '🔍',
    },
    {
      title: 'Distributed Capture',
      description: 'Automatically detect and capture knowledge patterns during development conversations.',
      icon: '📝',
    },
    {
      title: 'Multi-Tenant Isolation',
      description: 'Per-project knowledge bases with complete data isolation using PostgreSQL schemas.',
      icon: '🏢',
    },
    {
      title: 'External Knowledge',
      description: 'Index external documentation, Stack Overflow answers, and third-party resources.',
      icon: '🌐',
    },
    {
      title: 'Link Graph',
      description: 'Bi-directional document linking with automatic related content discovery.',
      icon: '🔗',
    },
    {
      title: '5 Built-in Doc Types',
      description: 'Problem/Solution, Insight, Codebase, Tool, and Style knowledge types out of the box.',
      icon: '📚',
    },
  ],
  skills: [
    { name: 'cdocs:activate', command: '/cdocs:activate', description: 'Initialize knowledge base for current project', category: 'workflow' },
    { name: 'cdocs:problem', command: '/cdocs:problem', description: 'Capture solved problem with root cause and solution', category: 'capture' },
    { name: 'cdocs:insight', command: '/cdocs:insight', description: 'Capture product or domain insight', category: 'capture' },
    { name: 'cdocs:codebase', command: '/cdocs:codebase', description: 'Document architecture decision or code pattern', category: 'capture' },
    { name: 'cdocs:tool', command: '/cdocs:tool', description: 'Record library gotcha or tool configuration', category: 'capture' },
    { name: 'cdocs:style', command: '/cdocs:style', description: 'Document coding convention or preference', category: 'capture' },
    { name: 'cdocs:query', command: '/cdocs:query', description: 'RAG query against project knowledge base', category: 'query' },
    { name: 'cdocs:search', command: '/cdocs:search', description: 'Semantic search across all documents', category: 'query' },
    { name: 'cdocs:search-external', command: '/cdocs:search-external', description: 'Search indexed external documentation', category: 'query' },
    { name: 'cdocs:delete', command: '/cdocs:delete', description: 'Remove document from knowledge base', category: 'manage' },
    { name: 'cdocs:promote', command: '/cdocs:promote', description: 'Boost document relevance score', category: 'manage' },
    { name: 'cdocs:create-type', command: '/cdocs:create-type', description: 'Define custom doc-type schema', category: 'workflow' },
  ],
  dependencies: {
    runtime: ['dotnet-10.0', 'docker', 'powershell-7'],
    mcpServers: ['Context7', 'Microsoft Learn', 'Sequential Thinking'],
  },
  installCommands: {
    interactive: '/plugin install csharp-compounding-docs@csharp-compound-marketplace',
    cli: 'claude plugin install csharp-compounding-docs@csharp-compound-marketplace',
    projectScope: 'claude plugin install csharp-compounding-docs@csharp-compound-marketplace --scope project',
  },
};
```

---

### Task 129.5: Create Landing Page

Create `marketplace/pages/index.mdx`:

```mdx
---
title: CSharp Compound Docs Plugin
---

import { Callout, Cards, Card, Steps, Tabs } from 'nextra/components';
import { HeroSection } from '../components/HeroSection';
import { FeatureGrid } from '../components/FeatureGrid';
import { SkillsTable } from '../components/SkillsTable';
import { InstallationBlock } from '../components/InstallationBlock';
import { pluginInfo } from '../data/plugin';

<HeroSection plugin={pluginInfo} />

## Features

<FeatureGrid features={pluginInfo.features} />

## Quick Start

<Steps>

### Install the Plugin

<InstallationBlock commands={pluginInfo.installCommands} />

### Configure MCP Dependencies

Ensure the following MCP servers are configured in your `~/.claude/settings.json`:

```json filename="~/.claude/settings.json"
{
  "mcpServers": {
    "context7": {
      "type": "http",
      "url": "https://mcp.context7.com/mcp"
    },
    "microsoft-learn": {
      "type": "sse",
      "url": "https://learn.microsoft.com/api/mcp"
    },
    "sequential-thinking": {
      "command": "npx",
      "args": ["-y", "@modelcontextprotocol/server-sequential-thinking"]
    }
  }
}
```

### Activate for Your Project

```bash
# In Claude Code, run:
/cdocs:activate
```

### Start Capturing Knowledge

Use any of the capture skills during your development workflow:

- `/cdocs:problem` - When you fix a bug
- `/cdocs:insight` - When you learn something about the domain
- `/cdocs:codebase` - When you make an architecture decision
- `/cdocs:tool` - When you discover a library gotcha
- `/cdocs:style` - When you establish a coding convention

</Steps>

## Available Skills

<SkillsTable skills={pluginInfo.skills} />

## Requirements

<Cards>
  <Card title=".NET 10.0" href="https://dotnet.microsoft.com/download/dotnet/10.0">
    Runtime for MCP server
  </Card>
  <Card title="Docker" href="https://www.docker.com/get-started">
    PostgreSQL + pgvector infrastructure
  </Card>
  <Card title="PowerShell 7" href="https://github.com/PowerShell/PowerShell">
    Cross-platform launcher scripts
  </Card>
</Cards>

<Callout type="info">
  The plugin automatically provisions Docker containers for PostgreSQL (with pgvector) and Ollama (for local embeddings) on first activation.
</Callout>

## Documentation

- [Full README](/csharp-compound-engineering/docs/readme) - Complete plugin documentation
- [Skill Reference](/csharp-compound-engineering/docs/skills) - Detailed skill documentation
- [Configuration Guide](/csharp-compound-engineering/docs/configuration) - Setup and customization
- [Custom Doc Types](/csharp-compound-engineering/docs/custom-types) - Creating custom schemas

## License

This project is licensed under the MIT License. See the [LICENSE](https://github.com/username/csharp-compound-engineering/blob/main/LICENSE) file for details.
```

---

### Task 129.6: Create Hero Section Component

Create `marketplace/components/HeroSection.tsx`:

```tsx
import React from 'react';
import { PluginInfo } from '../data/plugin';

interface HeroSectionProps {
  plugin: PluginInfo;
}

export function HeroSection({ plugin }: HeroSectionProps) {
  return (
    <div className="mx-auto max-w-4xl py-16 text-center">
      <h1 className="text-5xl font-bold tracking-tight">
        {plugin.displayName}
      </h1>
      <p className="mt-6 text-xl text-gray-600 dark:text-gray-400">
        {plugin.description}
      </p>
      <div className="mt-8 flex flex-wrap justify-center gap-3">
        {plugin.keywords.slice(0, 6).map((keyword) => (
          <span
            key={keyword}
            className="rounded-full bg-blue-100 px-4 py-1 text-sm font-medium text-blue-800 dark:bg-blue-900 dark:text-blue-200"
          >
            {keyword}
          </span>
        ))}
      </div>
      <div className="mt-8">
        <span className="inline-flex items-center rounded-md bg-green-100 px-3 py-1 text-sm font-medium text-green-800 dark:bg-green-900 dark:text-green-200">
          v{plugin.version}
        </span>
        <span className="ml-4 text-gray-500 dark:text-gray-400">
          {plugin.license} License
        </span>
      </div>
    </div>
  );
}
```

---

### Task 129.7: Create Feature Grid Component

Create `marketplace/components/FeatureGrid.tsx`:

```tsx
import React from 'react';
import { PluginFeature } from '../data/plugin';

interface FeatureGridProps {
  features: PluginFeature[];
}

export function FeatureGrid({ features }: FeatureGridProps) {
  return (
    <div className="mt-8 grid gap-6 md:grid-cols-2 lg:grid-cols-3">
      {features.map((feature) => (
        <div
          key={feature.title}
          className="rounded-lg border border-gray-200 p-6 transition-shadow hover:shadow-lg dark:border-gray-800"
        >
          <div className="text-4xl">{feature.icon}</div>
          <h3 className="mt-4 text-lg font-semibold">{feature.title}</h3>
          <p className="mt-2 text-gray-600 dark:text-gray-400">
            {feature.description}
          </p>
        </div>
      ))}
    </div>
  );
}
```

---

### Task 129.8: Create Skills Table Component

Create `marketplace/components/SkillsTable.tsx`:

```tsx
import React, { useState } from 'react';
import { PluginSkill } from '../data/plugin';

interface SkillsTableProps {
  skills: PluginSkill[];
}

const categoryColors: Record<string, string> = {
  capture: 'bg-purple-100 text-purple-800 dark:bg-purple-900 dark:text-purple-200',
  query: 'bg-blue-100 text-blue-800 dark:bg-blue-900 dark:text-blue-200',
  manage: 'bg-orange-100 text-orange-800 dark:bg-orange-900 dark:text-orange-200',
  workflow: 'bg-green-100 text-green-800 dark:bg-green-900 dark:text-green-200',
};

export function SkillsTable({ skills }: SkillsTableProps) {
  const [filter, setFilter] = useState<string>('all');

  const categories = ['all', ...new Set(skills.map((s) => s.category))];
  const filteredSkills = filter === 'all'
    ? skills
    : skills.filter((s) => s.category === filter);

  return (
    <div className="mt-8">
      <div className="mb-4 flex flex-wrap gap-2">
        {categories.map((cat) => (
          <button
            key={cat}
            onClick={() => setFilter(cat)}
            className={`rounded-md px-3 py-1 text-sm font-medium transition-colors ${
              filter === cat
                ? 'bg-blue-600 text-white'
                : 'bg-gray-100 text-gray-700 hover:bg-gray-200 dark:bg-gray-800 dark:text-gray-300 dark:hover:bg-gray-700'
            }`}
          >
            {cat.charAt(0).toUpperCase() + cat.slice(1)}
          </button>
        ))}
      </div>
      <div className="overflow-x-auto">
        <table className="min-w-full divide-y divide-gray-200 dark:divide-gray-700">
          <thead>
            <tr>
              <th className="px-4 py-3 text-left text-sm font-semibold">Command</th>
              <th className="px-4 py-3 text-left text-sm font-semibold">Description</th>
              <th className="px-4 py-3 text-left text-sm font-semibold">Category</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-200 dark:divide-gray-700">
            {filteredSkills.map((skill) => (
              <tr key={skill.name}>
                <td className="whitespace-nowrap px-4 py-3">
                  <code className="rounded bg-gray-100 px-2 py-1 text-sm dark:bg-gray-800">
                    {skill.command}
                  </code>
                </td>
                <td className="px-4 py-3 text-sm text-gray-600 dark:text-gray-400">
                  {skill.description}
                </td>
                <td className="whitespace-nowrap px-4 py-3">
                  <span className={`rounded-full px-2 py-1 text-xs font-medium ${categoryColors[skill.category]}`}>
                    {skill.category}
                  </span>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
```

---

### Task 129.9: Create Installation Block Component

Create `marketplace/components/InstallationBlock.tsx`:

```tsx
import React, { useState } from 'react';

interface InstallationBlockProps {
  commands: {
    interactive: string;
    cli: string;
    projectScope: string;
  };
}

export function InstallationBlock({ commands }: InstallationBlockProps) {
  const [copied, setCopied] = useState<string | null>(null);

  const copyToClipboard = async (text: string, key: string) => {
    await navigator.clipboard.writeText(text);
    setCopied(key);
    setTimeout(() => setCopied(null), 2000);
  };

  const installOptions = [
    { key: 'interactive', label: 'Interactive (Claude Code)', command: commands.interactive },
    { key: 'cli', label: 'CLI (User Scope)', command: commands.cli },
    { key: 'projectScope', label: 'CLI (Project Scope)', command: commands.projectScope },
  ];

  return (
    <div className="mt-4 space-y-3">
      {installOptions.map((option) => (
        <div
          key={option.key}
          className="group relative rounded-lg border border-gray-200 bg-gray-50 dark:border-gray-700 dark:bg-gray-900"
        >
          <div className="flex items-center justify-between px-4 py-2">
            <span className="text-xs font-medium text-gray-500 dark:text-gray-400">
              {option.label}
            </span>
            <button
              onClick={() => copyToClipboard(option.command, option.key)}
              className="rounded px-2 py-1 text-xs text-gray-500 hover:bg-gray-200 hover:text-gray-700 dark:hover:bg-gray-700 dark:hover:text-gray-300"
            >
              {copied === option.key ? 'Copied!' : 'Copy'}
            </button>
          </div>
          <pre className="overflow-x-auto px-4 pb-3">
            <code className="text-sm">{option.command}</code>
          </pre>
        </div>
      ))}
    </div>
  );
}
```

---

### Task 129.10: Create Component Index

Create `marketplace/components/index.ts`:

```typescript
export { HeroSection } from './HeroSection';
export { FeatureGrid } from './FeatureGrid';
export { SkillsTable } from './SkillsTable';
export { InstallationBlock } from './InstallationBlock';
```

---

### Task 129.11: Configure Tailwind CSS

Update `marketplace/tailwind.config.js`:

```javascript
/** @type {import('tailwindcss').Config} */
module.exports = {
  content: [
    './pages/**/*.{js,ts,jsx,tsx,mdx}',
    './components/**/*.{js,ts,jsx,tsx}',
    './theme.config.tsx',
  ],
  darkMode: 'class',
  theme: {
    extend: {},
  },
  plugins: [],
};
```

---

### Task 129.12: Create Global Styles

Update `marketplace/styles/globals.css`:

```css
@tailwind base;
@tailwind components;
@tailwind utilities;

:root {
  --nextra-primary-hue: 210;
}

/* Custom scrollbar for dark mode */
.dark ::-webkit-scrollbar {
  width: 8px;
  height: 8px;
}

.dark ::-webkit-scrollbar-track {
  background: #1a1a1a;
}

.dark ::-webkit-scrollbar-thumb {
  background: #333;
  border-radius: 4px;
}

.dark ::-webkit-scrollbar-thumb:hover {
  background: #444;
}

/* Code block enhancements */
pre code {
  @apply text-sm;
}

/* Table improvements */
table {
  @apply w-full;
}

th, td {
  @apply border-b border-gray-200 dark:border-gray-700;
}
```

---

### Task 129.13: Create GitHub Actions Deployment Workflow

Create `.github/workflows/deploy-marketplace.yml`:

```yaml
name: Deploy Marketplace

on:
  push:
    branches: [main]
    paths:
      - 'marketplace/**'
      - '.github/workflows/deploy-marketplace.yml'
  workflow_dispatch:

permissions:
  contents: read
  pages: write
  id-token: write

concurrency:
  group: 'pages'
  cancel-in-progress: true

jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - name: Checkout
        uses: actions/checkout@v4

      - name: Setup Node.js
        uses: actions/setup-node@v4
        with:
          node-version: '20'
          cache: 'npm'
          cache-dependency-path: marketplace/package-lock.json

      - name: Install dependencies
        run: npm ci
        working-directory: marketplace

      - name: Build with Next.js
        run: npm run build
        working-directory: marketplace

      - name: Add .nojekyll file
        run: touch out/.nojekyll
        working-directory: marketplace

      - name: Upload artifact
        uses: actions/upload-pages-artifact@v3
        with:
          path: marketplace/out

  deploy:
    environment:
      name: github-pages
      url: ${{ steps.deployment.outputs.page_url }}
    runs-on: ubuntu-latest
    needs: build
    steps:
      - name: Deploy to GitHub Pages
        id: deployment
        uses: actions/deploy-pages@v4
```

---

### Task 129.14: Update Package.json Scripts

Ensure `marketplace/package.json` includes:

```json
{
  "name": "csharp-compound-docs-marketplace",
  "version": "1.0.0",
  "private": true,
  "scripts": {
    "dev": "next dev",
    "build": "next build",
    "start": "next start",
    "lint": "next lint",
    "export": "next build"
  },
  "dependencies": {
    "next": "^14.0.0",
    "nextra": "^2.13.0",
    "nextra-theme-docs": "^2.13.0",
    "react": "^18.2.0",
    "react-dom": "^18.2.0"
  },
  "devDependencies": {
    "@types/node": "^20.0.0",
    "@types/react": "^18.2.0",
    "autoprefixer": "^10.4.0",
    "postcss": "^8.4.0",
    "tailwindcss": "^3.4.0",
    "typescript": "^5.0.0"
  }
}
```

---

### Task 129.15: Create Documentation Pages

Create `marketplace/pages/docs/_meta.json`:

```json
{
  "readme": "README",
  "skills": "Skill Reference",
  "configuration": "Configuration",
  "custom-types": "Custom Doc Types"
}
```

Create placeholder documentation pages:

**`marketplace/pages/docs/readme.mdx`**:
```mdx
---
title: README
---

# CSharp Compound Docs Plugin

{/* Content will be synced from main README.md */}

import { Callout } from 'nextra/components';

<Callout type="info">
  This documentation is synced from the main repository README.
  See the [GitHub repository](https://github.com/username/csharp-compound-engineering) for the latest version.
</Callout>
```

**`marketplace/pages/docs/skills.mdx`**:
```mdx
---
title: Skill Reference
---

# Skill Reference

Detailed documentation for all available skills.

{/* Skill documentation content */}
```

**`marketplace/pages/docs/configuration.mdx`**:
```mdx
---
title: Configuration Guide
---

# Configuration Guide

Learn how to configure the plugin for your project.

{/* Configuration documentation content */}
```

**`marketplace/pages/docs/custom-types.mdx`**:
```mdx
---
title: Custom Doc Types
---

# Creating Custom Doc Types

Learn how to define custom document type schemas.

{/* Custom types documentation content */}
```

---

## Verification Checklist

After completing all tasks, verify:

1. **Local Development**:
   ```bash
   cd marketplace
   npm install
   npm run dev
   # Visit http://localhost:3000/csharp-compound-engineering/
   ```

2. **Static Build**:
   ```bash
   npm run build
   ls -la out/
   # Verify .nojekyll exists
   # Verify _next/ directory exists
   ```

3. **Dark Mode**:
   - Toggle dark mode in the Nextra theme
   - Verify all components render correctly in both modes

4. **Mobile Responsiveness**:
   - Test at 320px, 768px, and 1024px widths
   - Verify navigation, feature grid, and skills table adapt properly

5. **Copy-to-Clipboard**:
   - Test copy buttons on installation commands
   - Verify "Copied!" feedback appears

6. **Search (Pagefind)**:
   - Build the site and verify search index is generated
   - Test search functionality on built site

7. **Navigation**:
   - Verify all documentation links work
   - Test external links (GitHub, .NET download, etc.)

8. **GitHub Actions**:
   - Push changes to trigger workflow
   - Verify deployment completes successfully
   - Access deployed site at `https://{username}.github.io/csharp-compound-engineering/`

---

## Dependencies

| Phase | Dependency Type | Description |
|-------|-----------------|-------------|
| Phase 126 | Hard | GitHub Pages infrastructure must be configured |
| Phase 130 | Provides | Marketplace integration will link to landing page |
| Phase 102 | Soft | Built-in schemas provide skill documentation content |

---

## Notes

- **Static Export**: Nextra with `output: 'export'` generates fully static HTML/CSS/JS that can be served from any static host. No Node.js server required in production.

- **basePath Configuration**: The `basePath` must match the GitHub repository name for GitHub Pages deployment to work correctly with subpath hosting.

- **.nojekyll File**: Critical for GitHub Pages deployment. Without this file, GitHub Pages will process files through Jekyll and ignore directories starting with underscore (like `_next/`).

- **Dark Mode Default**: The theme is configured to default to dark mode to match the Claude Code aesthetic. Users can toggle to light mode if preferred.

- **Image Optimization Disabled**: Next.js image optimization requires a server runtime and is incompatible with static export. All images use standard `<img>` tags via the `unoptimized: true` setting.

- **Pagefind Search**: Nextra v2.13+ includes Pagefind integration for full-text search. The search index is generated at build time and works entirely client-side.

- **Component Styling**: All components use Tailwind CSS utility classes with dark mode variants (`dark:*`) for consistent theming.

---

## Change Log

| Date | Changes |
|------|---------|
| 2025-01-24 | Initial phase creation |
