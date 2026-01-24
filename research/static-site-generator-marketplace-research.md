# Static Site Generator Research: Plugin Marketplace

**Date:** January 2026
**Purpose:** Evaluate static site generators for hosting a plugin marketplace on GitHub Pages

## Executive Summary

This research evaluates four approaches for building a plugin marketplace hosted on GitHub Pages:

1. **Plain HTML/CSS/JS** - Maximum control, minimum abstraction
2. **Jekyll** - GitHub Pages native support
3. **Hugo** - Fastest build times
4. **Nextra** - Modern React/Next.js ecosystem

**Recommendation:** Nextra is the recommended choice for this plugin marketplace due to its modern development experience, MDX support for interactive components, built-in search capabilities, and strong alignment with React/Next.js ecosystem practices.

---

## Marketplace Requirements

The plugin marketplace needs to:

- List available plugins with metadata (name, version, description, author)
- Display installation instructions
- Render README content from plugin repositories
- Provide search/filtering capabilities
- Be hosted on GitHub Pages (static deployment)

---

## Option 1: Plain HTML/CSS/JS

### Overview

A hand-crafted solution using vanilla web technologies without any build framework.

### Pros

| Benefit | Description |
|---------|-------------|
| **Zero Build Step** | Files served directly, no compilation needed |
| **Full Control** | Complete ownership of every aspect of the site |
| **Simplest Deployment** | Just push HTML files to GitHub Pages |
| **No Dependencies** | No package managers, no version conflicts |
| **Fast Initial Load** | No framework overhead |
| **Easy to Understand** | Anyone can read and modify the code |

### Cons

| Limitation | Description |
|------------|-------------|
| **No Templating** | Repeated HTML across pages, maintenance burden |
| **Manual Updates** | Every change requires editing raw files |
| **No Built-in Search** | Must implement from scratch or use third-party |
| **No Markdown Support** | README rendering requires external library |
| **Scalability Issues** | Managing 50+ plugins becomes unwieldy |
| **No Hot Reload** | Manual browser refresh during development |

### GitHub Pages Deployment

- Trivial - just commit HTML/CSS/JS files
- No build configuration needed
- Works out of the box with default settings

### Plugin Metadata Approach

```html
<!-- plugins.json loaded via fetch() -->
<script>
fetch('plugins.json')
  .then(res => res.json())
  .then(plugins => renderPluginList(plugins));
</script>
```

### README Rendering

Would require:
- Client-side Markdown library (e.g., marked.js, showdown)
- CORS considerations for fetching external READMEs
- GitHub API rate limiting (60 requests/hour unauthenticated)

### Development Experience

- **Learning Curve:** Low (if familiar with web basics)
- **Tooling:** Minimal (any text editor works)
- **Iteration Speed:** Slow (no live reload without additional setup)
- **Debugging:** Browser DevTools only

### Best For

- Very small plugin catalogs (< 10 plugins)
- Teams wanting absolute simplicity
- Prototyping before adopting a framework

---

## Option 2: Jekyll

### Overview

Jekyll is a Ruby-based static site generator with native GitHub Pages integration. It was created by Tom Preston-Werner (GitHub co-founder) and has deep GitHub Pages integration.

### Pros

| Benefit | Description |
|---------|-------------|
| **Native GitHub Pages Support** | Builds automatically on push |
| **Data Files** | YAML/JSON files in `_data/` directory for plugin catalogs |
| **Liquid Templating** | Powerful templating with includes and layouts |
| **Mature Ecosystem** | Large collection of themes and plugins |
| **Markdown Support** | Built-in Markdown to HTML conversion |
| **Collections** | First-class support for managing groups of content |

### Cons

| Limitation | Description |
|------------|-------------|
| **Version Lock** | GitHub Pages stuck on Jekyll 3.x (4.0+ not supported) |
| **Plugin Restrictions** | Only [whitelisted plugins](https://pages.github.com/versions/) work with auto-build |
| **Ruby Dependency** | Requires Ruby environment setup |
| **Slower Builds** | Noticeably slower than Hugo for large sites |
| **Limited Interactivity** | Adding dynamic features requires JavaScript |
| **Dated Development Experience** | Less modern than React-based solutions |

### GitHub Pages Deployment

Two approaches:

**1. Native Build (Limited)**
```yaml
# _config.yml
plugins:
  - jekyll-feed
  - jekyll-seo-tag
  # Only whitelisted plugins work
```
- Push source files, GitHub builds automatically
- Limited to supported Jekyll version and plugins

**2. GitHub Actions (Full Control)**
```yaml
# .github/workflows/jekyll.yml
name: Build Jekyll
on:
  push:
    branches: [main]
jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: ruby/setup-ruby@v1
        with:
          ruby-version: '3.2'
          bundler-cache: true
      - run: bundle exec jekyll build
      - uses: actions/upload-pages-artifact@v3
```

### Plugin Catalog Data Structure

```yaml
# _data/plugins.yml
- name: MyPlugin
  version: 1.0.0
  description: A useful plugin
  repo: https://github.com/user/myplugin
  install_command: dotnet add package MyPlugin
```

```liquid
<!-- plugins.html -->
{% for plugin in site.data.plugins %}
<div class="plugin-card">
  <h3>{{ plugin.name }}</h3>
  <p>{{ plugin.description }}</p>
  <code>{{ plugin.install_command }}</code>
</div>
{% endfor %}
```

### README Rendering Limitations

- Must pre-fetch READMEs at build time (custom plugin required)
- jekyll-datapage_gen can generate pages from data files
- External README fetching not built-in

### Development Experience

- **Learning Curve:** Medium (Ruby, Liquid templating)
- **Tooling:** `bundle exec jekyll serve --livereload`
- **Iteration Speed:** Moderate (live reload available)
- **Debugging:** Ruby error messages can be cryptic

### Best For

- Simple plugin catalogs prioritizing zero-config deployment
- Teams already familiar with Ruby/Jekyll
- Projects not needing interactive UI components

---

## Option 3: Hugo

### Overview

Hugo is a Go-based static site generator known for exceptional build speed. It compiles as a single binary with no dependencies.

### Pros

| Benefit | Description |
|---------|-------------|
| **Blazing Fast Builds** | 1000+ pages in under 2 seconds |
| **Single Binary** | No runtime dependencies, easy installation |
| **Powerful Templating** | Go templates with extensive functions |
| **Data Files** | JSON, YAML, TOML support in `data/` directory |
| **Multilingual** | First-class i18n support |
| **Shortcodes** | Reusable content snippets |
| **Hugo Modules** | Git-based dependency management |

### Cons

| Limitation | Description |
|------------|-------------|
| **No Native GitHub Pages** | Requires GitHub Actions workflow |
| **Go Template Syntax** | Learning curve for the templating language |
| **Limited Plugin System** | Extensions via modules, not traditional plugins |
| **Less Interactive** | No built-in React/component support |
| **Smaller Theme Ecosystem** | Compared to Jekyll |

### GitHub Pages Deployment

Requires GitHub Actions:

```yaml
# .github/workflows/hugo.yml
name: Deploy Hugo
on:
  push:
    branches: [main]
jobs:
  deploy:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
        with:
          submodules: true
      - name: Setup Hugo
        uses: peaceiris/actions-hugo@v2
        with:
          hugo-version: 'latest'
      - name: Build
        run: hugo --minify
      - name: Deploy
        uses: peaceiris/actions-gh-pages@v3
        with:
          github_token: ${{ secrets.GITHUB_TOKEN }}
          publish_dir: ./public
```

### Plugin Catalog Data Structure

```yaml
# data/plugins.yaml
plugins:
  - name: MyPlugin
    version: 1.0.0
    description: A useful plugin
    category: utilities
    repo: https://github.com/user/myplugin
```

```html
<!-- layouts/plugins/list.html -->
{{ range .Site.Data.plugins.plugins }}
<div class="plugin-card">
  <h3>{{ .name }}</h3>
  <p>{{ .description }}</p>
  <span class="badge">{{ .category }}</span>
</div>
{{ end }}
```

### README Rendering

- `getRemote` function can fetch external content at build time
- Must handle caching and rate limiting
- Example:

```go
{{ $readme := resources.GetRemote "https://raw.githubusercontent.com/user/repo/main/README.md" }}
{{ $readme.Content | markdownify }}
```

### Development Experience

- **Learning Curve:** Medium-High (Go templates)
- **Tooling:** `hugo server --watch`
- **Iteration Speed:** Very fast (instant rebuilds)
- **Debugging:** Good error messages

### Best For

- Large plugin catalogs (100+ plugins)
- Teams prioritizing build performance
- Multilingual marketplace requirements

---

## Option 4: Nextra (Recommended)

### Overview

Nextra is a Next.js-based static site generator designed for documentation sites. It leverages MDX (Markdown + JSX) for rich content authoring with React components.

### Pros

| Benefit | Description |
|---------|-------------|
| **MDX Support** | Embed React components in Markdown |
| **Modern DX** | Hot reload, TypeScript, modern tooling |
| **Built-in Search** | Pagefind integration for full-text search |
| **React Ecosystem** | Access to npm packages and component libraries |
| **Theming** | Documentation and Blog themes available |
| **Interactive Components** | Build custom plugin cards, filters, etc. |
| **SEO Optimized** | Built-in meta tags and OpenGraph support |
| **Tailwind CSS** | Style customization with utility classes |

### Cons

| Limitation | Description |
|------------|-------------|
| **Static Export Required** | Must configure `output: 'export'` for GitHub Pages |
| **Smaller Community** | Less mature than Docusaurus ecosystem |
| **Node.js Dependency** | Requires Node.js environment |
| **Build Time** | Slower than Hugo for very large sites |
| **Pagefind Issues** | Some reported issues with static export search |

### GitHub Pages Deployment

**Configuration (`next.config.mjs`):**

```javascript
import nextra from 'nextra';

const withNextra = nextra({
  theme: 'nextra-theme-docs',
  themeConfig: './theme.config.tsx',
});

export default withNextra({
  output: 'export',
  images: { unoptimized: true },
  basePath: '/repository-name', // For GitHub Pages subpath
  assetPrefix: '/repository-name/',
  trailingSlash: true,
});
```

**GitHub Actions Workflow:**

```yaml
# .github/workflows/deploy.yml
name: Deploy Nextra
on:
  push:
    branches: [main]
jobs:
  deploy:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-node@v4
        with:
          node-version: '20'
          cache: 'npm'
      - run: npm ci
      - run: npm run build
      - name: Add .nojekyll
        run: touch out/.nojekyll
      - uses: actions/upload-pages-artifact@v3
        with:
          path: out
      - uses: actions/deploy-pages@v4
```

**Important:** Add `.nojekyll` file to prevent GitHub Pages from ignoring `_next` directory.

### Plugin Marketplace Architecture

**Plugin Data (`data/plugins.ts`):**

```typescript
export interface Plugin {
  id: string;
  name: string;
  version: string;
  description: string;
  author: string;
  repo: string;
  category: string;
  tags: string[];
  installCommand: string;
}

export const plugins: Plugin[] = [
  {
    id: 'my-plugin',
    name: 'MyPlugin',
    version: '1.0.0',
    description: 'A useful plugin for the system',
    author: 'developer',
    repo: 'https://github.com/user/myplugin',
    category: 'utilities',
    tags: ['utility', 'helper'],
    installCommand: 'dotnet add package MyPlugin',
  },
  // ...more plugins
];
```

**Plugin Card Component (`components/PluginCard.tsx`):**

```tsx
import { Plugin } from '../data/plugins';

export function PluginCard({ plugin }: { plugin: Plugin }) {
  return (
    <div className="border rounded-lg p-4 hover:shadow-lg transition">
      <h3 className="text-xl font-semibold">{plugin.name}</h3>
      <p className="text-gray-600">{plugin.description}</p>
      <div className="mt-2 flex gap-2">
        {plugin.tags.map(tag => (
          <span key={tag} className="badge">{tag}</span>
        ))}
      </div>
      <pre className="mt-4 bg-gray-100 p-2 rounded">
        {plugin.installCommand}
      </pre>
    </div>
  );
}
```

**Plugin List Page (`pages/plugins.mdx`):**

```mdx
import { plugins } from '../data/plugins';
import { PluginCard } from '../components/PluginCard';
import { PluginFilter } from '../components/PluginFilter';

# Plugin Marketplace

<PluginFilter plugins={plugins}>
  {filteredPlugins => (
    <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
      {filteredPlugins.map(plugin => (
        <PluginCard key={plugin.id} plugin={plugin} />
      ))}
    </div>
  )}
</PluginFilter>
```

### README Rendering Approaches

**Option A: Build-time Fetching (Recommended)**

```typescript
// lib/fetchReadme.ts
export async function fetchReadme(repo: string): Promise<string> {
  const [owner, name] = repo.replace('https://github.com/', '').split('/');
  const url = `https://raw.githubusercontent.com/${owner}/${name}/main/README.md`;
  const res = await fetch(url);
  return res.text();
}
```

```typescript
// pages/plugins/[id].tsx
import { GetStaticProps, GetStaticPaths } from 'next';
import { plugins } from '../../data/plugins';
import { fetchReadme } from '../../lib/fetchReadme';
import { MDXRemote } from 'next-mdx-remote';
import { serialize } from 'next-mdx-remote/serialize';

export const getStaticPaths: GetStaticPaths = async () => ({
  paths: plugins.map(p => ({ params: { id: p.id } })),
  fallback: false,
});

export const getStaticProps: GetStaticProps = async ({ params }) => {
  const plugin = plugins.find(p => p.id === params?.id);
  const readmeContent = await fetchReadme(plugin.repo);
  const mdxSource = await serialize(readmeContent);
  return { props: { plugin, mdxSource } };
};
```

**Option B: Client-side Fetching**

```tsx
import { useEffect, useState } from 'react';
import ReactMarkdown from 'react-markdown';

function PluginReadme({ repo }: { repo: string }) {
  const [readme, setReadme] = useState<string>('');

  useEffect(() => {
    fetchReadme(repo).then(setReadme);
  }, [repo]);

  return <ReactMarkdown>{readme}</ReactMarkdown>;
}
```

### Development Experience

- **Learning Curve:** Medium (React/Next.js knowledge helpful)
- **Tooling:** `npm run dev` with hot module replacement
- **Iteration Speed:** Fast (instant preview of changes)
- **Debugging:** React DevTools, excellent error messages
- **TypeScript:** First-class support

### Best For

- Interactive plugin marketplaces with filtering/search
- Teams familiar with React/Next.js
- Projects needing custom UI components
- Modern development workflow requirements

---

## Comparison Matrix

| Criteria | Plain HTML | Jekyll | Hugo | Nextra |
|----------|------------|--------|------|--------|
| **Build Speed** | N/A | Slow | Fastest | Fast |
| **GitHub Pages Native** | Yes | Yes | No | No |
| **Learning Curve** | Low | Medium | Medium-High | Medium |
| **Templating Power** | None | Liquid | Go Templates | MDX/React |
| **Interactive Components** | Manual | Limited | Limited | Excellent |
| **Plugin Data Management** | JSON fetch | YAML data files | YAML/JSON data | TypeScript |
| **Built-in Search** | No | Plugin | Themes | Pagefind |
| **README Rendering** | Client-side | Build plugin | getRemote | Build/Client |
| **Modern DX** | Poor | Fair | Good | Excellent |
| **Scalability** | Poor | Good | Excellent | Good |
| **Community Size** | N/A | Large | Medium | Growing |

---

## Recommendation: Nextra

For this plugin marketplace project, **Nextra is the recommended choice** based on the following analysis:

### Why Nextra Wins

1. **Interactive Marketplace Features**
   - React components enable sophisticated plugin cards with hover effects
   - Easy to build filter/search UI with state management
   - Can add future features like ratings, favorites, comparison views

2. **MDX Flexibility**
   - Write content in Markdown while embedding interactive components
   - Plugin pages can mix documentation with live examples
   - Easy to maintain by non-developers (Markdown) and developers (React)

3. **Modern Development Experience**
   - Hot module replacement for instant feedback
   - TypeScript for type-safe plugin data
   - Familiar React patterns for component development
   - npm ecosystem for additional functionality

4. **README Rendering**
   - Build-time fetching via `getStaticProps`
   - MDX rendering maintains consistent styling
   - Can process and enhance README content (syntax highlighting, etc.)

5. **Search Capabilities**
   - Pagefind provides full-text search out of the box
   - Can be extended with custom search logic
   - No external service dependencies

6. **Future-Proof Architecture**
   - React/Next.js skills are widely applicable
   - Active development and growing community
   - Easy to migrate to full Next.js app if needed

### GitHub Pages Deployment Strategy

1. Configure static export in `next.config.mjs`
2. Set up GitHub Actions workflow for automated deployment
3. Add `.nojekyll` file to preserve `_next` directory
4. Configure `basePath` for repository subpath

### Implementation Roadmap

```
Phase 1: Setup
- Initialize Nextra project with docs theme
- Configure GitHub Pages deployment workflow
- Create plugin data schema (TypeScript)

Phase 2: Core Features
- Build PluginCard component
- Create plugin list page with grid layout
- Implement category filtering

Phase 3: Plugin Details
- Individual plugin pages with getStaticPaths
- README fetching and rendering
- Installation instructions display

Phase 4: Enhancement
- Add search functionality
- Implement tag-based filtering
- Add plugin comparison feature
```

---

## Sources

- [Nextra Official Documentation](https://nextra.site)
- [Nextra Static Exports Guide](https://nextra.site/docs/guide/static-exports)
- [Jekyll Data Files Documentation](https://jekyllrb.com/docs/step-by-step/06-data-files/)
- [GitHub Pages and Jekyll](https://docs.github.com/en/pages/setting-up-a-github-pages-site-with-jekyll/about-github-pages-and-jekyll)
- [Hugo Official Site](https://gohugo.io/)
- [Hugo vs Jekyll Comparison (CloudCannon)](https://cloudcannon.com/blog/jekyll-vs-hugo-choosing-the-right-tool-for-the-job/)
- [Benchmarking Hugo vs Jekyll vs GitHub Pages](https://michaelnordmeyer.com/benchmarking-hugo-vs-jekyll-vs-github-pages-in-2024)
- [Top Static Site Generators 2025 (CloudCannon)](https://cloudcannon.com/blog/the-top-five-static-site-generators-for-2025-and-when-to-use-them/)
- [Nextra vs Docusaurus Comparison](https://edujbarrios.com/blog/Nextra-vs-Docusaurus)
- [Next.js MDX Guide](https://nextjs.org/docs/pages/guides/mdx)
- [jekyll-datapage_gen Plugin](https://github.com/avillafiorita/jekyll-datapage_gen)
- [Deploy Next.js to GitHub Pages (FreeCodeCamp)](https://www.freecodecamp.org/news/how-to-deploy-next-js-app-to-github-pages/)
