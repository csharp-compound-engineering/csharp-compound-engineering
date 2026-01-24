# Compound Engineering Paradigm Research Report

**Source Repository:** `/Users/michaelmccord/Projects/compound-engineering-plugin/`
**Research Date:** 2026-01-22
**Report Location:** `/Users/michaelmccord/Projects/csharp-compound-engineering/research/compound-engineering-paradigm-research.md`

---

## Table of Contents

1. [Paradigm Overview](#1-paradigm-overview)
2. [Repository Structure](#2-repository-structure)
3. [CLAUDE.md Analysis](#3-claudemd-analysis)
4. [Documentation (docs/)](#4-documentation-docs)
5. [Plans (plans/)](#5-plans-plans)
6. [Plugins (plugins/)](#6-plugins-plugins)
7. [Architecture and Design](#7-architecture-and-design)
8. [Implementation Details](#8-implementation-details)
9. [Integration Patterns](#9-integration-patterns)
10. [Best Practices](#10-best-practices)
11. [Examples and Usage](#11-examples-and-usage)
12. [Complete Summary](#12-complete-summary)

---

## 1. Paradigm Overview

### What is Compound Engineering?

**Compound Engineering** is a development methodology and Claude Code plugin system that inverts traditional software development patterns. The core philosophy is:

> **"Each unit of engineering work should make subsequent units of work easier—not harder."**

Traditional development accumulates technical debt, where every feature adds complexity and the codebase becomes harder to work with over time. Compound engineering flips this by ensuring that:

- Solved problems teach the system
- Knowledge accumulates and is reusable
- Reviews catch more issues over time
- Patterns get documented automatically
- Quality stays high so future changes are easy

### Core Philosophy

The philosophy emphasizes an **80/20 split**:
- **80% in planning and review**
- **20% in execution**

This contrasts with traditional development where developers jump into coding immediately. The compound approach front-loads thinking to reduce rework and compound learnings.

### The Four Pillars

The paradigm is built on four interconnected pillars:

| Pillar | Purpose | Key Insight |
|--------|---------|-------------|
| **Plan** | Stop starting over from scratch | Leverage institutional memory via research agents |
| **Delegate** | Work with experts who never forget | AI agents enforce conventions consistently |
| **Assess** | Get multiple expert opinions quickly | Parallel multi-agent code reviews |
| **Compound** | Never solve the same bug twice | Document solutions as searchable knowledge |

### Goals and Objectives

1. **Accumulate Knowledge** - Build a searchable knowledge base of solutions
2. **Consistent Quality** - Enforce conventions through specialized AI agents
3. **Parallel Expertise** - Run multiple expert reviews simultaneously
4. **Pattern Recognition** - Identify and document recurring patterns
5. **Reduced Context Switching** - Keep knowledge in the system, not in developers' heads

### Target Use Cases

- Rails/Ruby development teams
- Python and TypeScript projects
- Teams wanting to systematize code reviews
- Organizations building institutional knowledge
- Projects requiring consistent architectural decisions
- Teams adopting AI-assisted development workflows

---

## 2. Repository Structure

### Complete Directory Tree

```
compound-engineering-plugin/
├── .claude-plugin/
│   └── marketplace.json          # Marketplace catalog
├── .git/
├── .github/
├── .gitignore
├── CLAUDE.md                     # Repository instructions (11.4KB)
├── LICENSE                       # MIT License
├── README.md                     # Quick start guide
├── docs/                         # Documentation site (GitHub Pages)
│   ├── index.html                # Landing page (52KB)
│   ├── css/
│   │   ├── style.css
│   │   └── docs.css
│   ├── js/
│   │   └── main.js
│   ├── pages/
│   │   ├── getting-started.html
│   │   ├── agents.html
│   │   ├── commands.html
│   │   ├── skills.html
│   │   ├── mcp-servers.html
│   │   └── changelog.html
│   └── solutions/                # Documented solutions
├── plans/                        # Planning documents
│   ├── grow-your-own-garden-plugin-architecture.md
│   └── landing-page-launchkit-refresh.md
└── plugins/
    └── compound-engineering/     # Main plugin
        ├── .claude-plugin/
        │   └── plugin.json       # Plugin metadata
        ├── README.md             # Plugin documentation
        ├── CHANGELOG.md          # Version history
        ├── agents/               # 27 specialized agents
        │   ├── design/           # 3 design agents
        │   ├── docs/             # 1 documentation agent
        │   ├── research/         # 4 research agents
        │   ├── review/           # 14 review agents
        │   └── workflow/         # 5 workflow agents
        ├── commands/             # 20+ slash commands
        │   ├── workflows/        # Core workflow commands
        │   │   ├── plan.md
        │   │   ├── work.md
        │   │   ├── review.md
        │   │   └── compound.md
        │   └── [utility commands]
        ├── skills/               # 14 skills
        │   ├── agent-native-architecture/
        │   ├── andrew-kane-gem-writer/
        │   ├── compound-docs/
        │   ├── create-agent-skills/
        │   ├── every-style-editor/
        │   ├── file-todos/
        │   ├── gemini-imagegen/
        │   ├── rclone/
        │   ├── skill-creator/
        │   └── [others]
        └── mcp-servers/          # MCP server configurations
```

### Purpose of Each Directory

| Directory | Purpose |
|-----------|---------|
| `.claude-plugin/` | Plugin marketplace metadata and catalog |
| `docs/` | Static documentation site (GitHub Pages ready) |
| `docs/pages/` | Reference pages for agents, commands, skills |
| `docs/solutions/` | Documented problem solutions (compound knowledge) |
| `plans/` | Future roadmap and design documents |
| `plugins/compound-engineering/` | The main plugin distribution |
| `plugins/.../agents/` | Specialized AI agents organized by category |
| `plugins/.../commands/` | Slash commands for workflows |
| `plugins/.../skills/` | Domain expertise and reference materials |
| `plugins/.../mcp-servers/` | Model Context Protocol server configs |

---

## 3. CLAUDE.md Analysis

### Overview

The CLAUDE.md file (11.4KB) serves as the primary instruction file for Claude Code when working in this repository. It follows the compound engineering philosophy of documenting learnings.

### Key Sections

#### 1. Repository Structure Documentation

Provides a complete map of the repository with purpose annotations:

```
every-marketplace/
├── .claude-plugin/
│   └── marketplace.json          # Marketplace catalog
├── docs/                         # Documentation site
└── plugins/
    └── compound-engineering/     # The actual plugin
        ├── agents/               # 24 specialized AI agents
        ├── commands/             # 13 slash commands
        ├── skills/               # 11 skills
        └── mcp-servers/          # 2 MCP servers
```

#### 2. Philosophy Statement

Explicitly states the compound engineering process:
1. **Plan** - Understand the change needed and its impact
2. **Delegate** - Use AI tools to help with implementation
3. **Assess** - Verify changes work as expected
4. **Codify** - Update CLAUDE.md with learnings

#### 3. Working Instructions

**Adding a New Plugin:**
- Create plugin directory structure
- Add required metadata files
- Update marketplace.json
- Test locally before committing

**Updating the Compound Engineering Plugin:**
Provides a comprehensive checklist:
- Count all components accurately (bash commands provided)
- Update ALL description strings with correct counts
- Update version numbers in multiple locations
- Update documentation
- Rebuild documentation site with `/release-docs`
- Validate JSON files

#### 4. JSON Structure Specifications

Documents the exact schema for:
- `marketplace.json` - Marketplace catalog format
- `plugin.json` - Plugin metadata format

Explicitly warns against adding non-spec fields.

#### 5. Documentation Site Management

- Location: `/docs` (GitHub Pages ready)
- Built with plain HTML/CSS/JS (LaunchKit template)
- No build step required
- `/release-docs` command updates all pages

#### 6. Testing Instructions

- Local installation commands
- Agent and command testing
- JSON validation commands

#### 7. Common Tasks Checklists

Step-by-step guides for:
- Adding a new agent
- Adding a new command
- Adding a new skill (with file format specification)
- Updating tags/keywords

#### 8. Commit Conventions

Specific patterns:
- `Add [component name]`
- `Remove [component name]`
- `Update [file] to [what changed]`
- `Fix [issue]`
- `Simplify [component] to [improvement]`

#### 9. Key Learnings Section

Documents real lessons learned:

**2024-11-22: Component Counts**
> "Always count actual files before updating descriptions. The counts appear in multiple places and must all match."

**2024-10-09: Marketplace.json Simplification**
> "Stick to the official spec. Custom fields may confuse users or break compatibility."

---

## 4. Documentation (docs/)

### Landing Page (index.html)

The 52KB landing page serves as the primary marketing and documentation entry point.

#### Key Messaging

**Hero:** "Your Code Reviews Just Got 12 Expert Opinions. In 30 Seconds."

**Philosophy Quote:**
> "Most engineering work is amnesia. You solve a problem on Tuesday, forget the solution by Friday, and re-solve it next quarter. Compounding engineering is different: each solved problem teaches the system."

#### The Four Pillars Explained

**Plan - Stop starting over from scratch:**
Three research agents work in parallel reading docs, analyzing repo history, and finding community patterns to build plans on institutional memory.

**Delegate - Work with experts who never forget:**
27 specialized agents that never get tired, never skip reviews, never forget conventions. They enforce standards consistently.

**Assess - Get twelve opinions without twelve meetings:**
Parallel multi-agent review: security audit, performance analysis, architecture review, data integrity check, and more.

**Compound - Never solve the same bug twice:**
Capture solutions as searchable documentation with YAML frontmatter immediately after fixing problems.

#### Component Statistics

| Component | Count |
|-----------|-------|
| Specialized Agents | 27 |
| Slash Commands | 20+ |
| Intelligent Skills | 14 |
| MCP Servers | 1 (Context7) |

#### Installation (Three Steps)

```bash
# 1. Add the Marketplace
claude /plugin marketplace add https://github.com/kieranklaassen/every-marketplace

# 2. Install the Plugin
claude /plugin install compound-engineering

# 3. Start Using
/review PR#123
claude agent security-sentinel
skill: gemini-imagegen
```

### Reference Pages

Located in `docs/pages/`:

| Page | Content |
|------|---------|
| `getting-started.html` | Installation and quick start guide |
| `agents.html` | All 27 agents with descriptions and usage |
| `commands.html` | All 20+ commands reference |
| `skills.html` | All 14 skills documentation |
| `mcp-servers.html` | MCP server configuration |
| `changelog.html` | Version history |

---

## 5. Plans (plans/)

### grow-your-own-garden-plugin-architecture.md

**Vision:** "Everyone grows their own garden, but we're all using the same process."

#### Current Problem Identified
- Monolithic plugin: 24+ agents, users use ~30%
- No personalization (same agents for Rails dev and Python dev)
- Static collection that doesn't adapt

#### Proposed Solution: The Seed Architecture

**The Seed (Core Plugin):**
- 4 commands: `/plan`, `/work`, `/review`, `/compound`
- 5 universal review agents
- 4 research agents
- 3 core skills
- 2 MCP servers

**The Growth Loop:**
After each `/compound`, suggest relevant agents based on tech stack:

```
✅ Learning documented

💡 It looks like you're using Rails.
   Would you like to add the "DHH Rails Reviewer"?

   [y] Yes  [n] No  [x] Never ask
```

**Three Sources of New Agents:**
1. **Predefined** - "You're using Rails, add DHH reviewer?"
2. **Dynamic** - "You're using actor model, create an expert?"
3. **Custom** - "Want to create an agent for this pattern?"

**Agent Storage Hierarchy:**
```
.claude/agents/       → Project-specific (highest priority)
~/.claude/agents/     → User's garden
plugin/agents/        → From installed plugins
```

#### Implementation Phases

1. Split the plugin (core vs specialized)
2. Agent discovery from multiple locations
3. Growth via `/compound` suggestions
4. Management commands (`/agents list`, `/agents add`, `/agents disable`)

### landing-page-launchkit-refresh.md

A detailed plan for improving the documentation landing page, including:
- Section-by-section review checklist
- Pragmatic writing style guidelines
- Social proof section proposal
- Anti-patterns to fix (passive voice, abstract claims)

---

## 6. Plugins (plugins/)

### Plugin Metadata (plugin.json)

```json
{
  "name": "compound-engineering",
  "version": "2.26.5",
  "description": "AI-powered development tools. 27 agents, 21 commands, 14 skills, 1 MCP server",
  "author": {
    "name": "Kieran Klaassen",
    "email": "kieran@every.to"
  },
  "license": "MIT",
  "keywords": [
    "ai-powered", "compound-engineering", "workflow-automation",
    "code-review", "rails", "ruby", "python", "typescript",
    "knowledge-management", "image-generation", "browser-automation"
  ],
  "mcpServers": {
    "context7": {
      "type": "http",
      "url": "https://mcp.context7.com/mcp"
    }
  }
}
```

### Agents (27 Total)

#### Review Agents (14)

| Agent | Description |
|-------|-------------|
| `agent-native-reviewer` | Verify features are agent-native (action + context parity) |
| `architecture-strategist` | Analyze architectural decisions and compliance |
| `code-simplicity-reviewer` | Final pass for simplicity and minimalism |
| `data-integrity-guardian` | Database migrations and data integrity |
| `data-migration-expert` | Validate ID mappings, check for swapped values |
| `deployment-verification-agent` | Create Go/No-Go deployment checklists |
| `dhh-rails-reviewer` | Rails review from DHH's perspective |
| `kieran-rails-reviewer` | Rails code review with strict conventions |
| `kieran-python-reviewer` | Python code review with strict conventions |
| `kieran-typescript-reviewer` | TypeScript code review |
| `pattern-recognition-specialist` | Analyze code for patterns and anti-patterns |
| `performance-oracle` | Performance analysis and optimization |
| `security-sentinel` | Security audits and vulnerability assessments |
| `julik-frontend-races-reviewer` | JavaScript/Stimulus race condition review |

#### Research Agents (4)

| Agent | Description |
|-------|-------------|
| `best-practices-researcher` | Gather external best practices and examples |
| `framework-docs-researcher` | Research framework documentation |
| `git-history-analyzer` | Analyze git history and code evolution |
| `repo-research-analyst` | Research repository structure and conventions |

#### Design Agents (3)

| Agent | Description |
|-------|-------------|
| `design-implementation-reviewer` | Verify UI matches Figma designs |
| `design-iterator` | Iteratively refine UI through design iterations |
| `figma-design-sync` | Synchronize implementations with Figma designs |

#### Workflow Agents (5)

| Agent | Description |
|-------|-------------|
| `bug-reproduction-validator` | Reproduce and validate bug reports |
| `every-style-editor` | Edit content to Every's style guide |
| `lint` | Run linting and code quality checks |
| `pr-comment-resolver` | Address PR comments and implement fixes |
| `spec-flow-analyzer` | Analyze user flows and specification gaps |

#### Docs Agent (1)

| Agent | Description |
|-------|-------------|
| `ankane-readme-writer` | Create READMEs following Ankane-style template |

### Agent File Format

Agents are defined as markdown files with YAML frontmatter:

```markdown
---
name: agent-name
description: "Detailed description with examples..."
model: inherit
---

[Agent instructions and behavior definition]
```

**Key Agent Characteristics:**
- `model: inherit` - Uses the user's configured model
- Rich description with XML-style examples
- Detailed behavioral instructions
- Specific code patterns and anti-patterns
- Reporting protocols

### Commands (20+)

#### Core Workflow Commands (workflows: prefix)

| Command | Purpose |
|---------|---------|
| `/workflows:plan` | Create implementation plans with research |
| `/workflows:work` | Execute plans efficiently with quality gates |
| `/workflows:review` | Multi-agent parallel code reviews |
| `/workflows:compound` | Document solved problems |

#### Utility Commands

| Command | Purpose |
|---------|---------|
| `/deepen-plan` | Enhance plans with parallel research agents |
| `/changelog` | Create engaging changelogs |
| `/create-agent-skill` | Create or edit Claude Code skills |
| `/generate_command` | Generate new slash commands |
| `/heal-skill` | Fix skill documentation issues |
| `/plan_review` | Multi-agent plan review in parallel |
| `/report-bug` | Report bugs in the plugin |
| `/reproduce-bug` | Reproduce bugs using logs |
| `/triage` | Triage and prioritize issues |
| `/resolve_parallel` | Resolve TODO comments in parallel |
| `/resolve_pr_parallel` | Resolve PR comments in parallel |
| `/resolve_todo_parallel` | Resolve file-based todos in parallel |
| `/test-browser` | Run browser tests on affected pages |
| `/xcode-test` | Build and test iOS apps |
| `/feature-video` | Record feature walkthroughs |
| `/agent-native-audit` | Agent-native architecture review |
| `/lfg` | Full autonomous engineering workflow |
| `/release-docs` | Update documentation site |
| `/deploy-docs` | Prepare docs for deployment |

### Skills (14)

#### Architecture & Design

| Skill | Description |
|-------|-------------|
| `agent-native-architecture` | Build AI agents using prompt-native architecture |

#### Development Tools

| Skill | Description |
|-------|-------------|
| `andrew-kane-gem-writer` | Write Ruby gems following Andrew Kane's patterns |
| `compound-docs` | Capture solved problems as categorized documentation |
| `create-agent-skills` | Expert guidance for creating Claude Code skills |
| `dhh-rails-style` | Write Ruby/Rails code in DHH's 37signals style |
| `dspy-ruby` | Build type-safe LLM applications with DSPy.rb |
| `frontend-design` | Create production-grade frontend interfaces |
| `skill-creator` | Guide for creating effective Claude Code skills |

#### Content & Workflow

| Skill | Description |
|-------|-------------|
| `every-style-editor` | Review copy for Every's style guide |
| `file-todos` | File-based todo tracking system |
| `git-worktree` | Manage Git worktrees for parallel development |

#### File Transfer

| Skill | Description |
|-------|-------------|
| `rclone` | Upload files to S3, Cloudflare R2, Backblaze B2 |

#### Browser Automation

| Skill | Description |
|-------|-------------|
| `agent-browser` | CLI-based browser automation using Vercel's agent-browser |

#### Image Generation

| Skill | Description |
|-------|-------------|
| `gemini-imagegen` | Generate and edit images using Google's Gemini API |

### Skill File Format

Skills have a specific structure:

```
skills/skill-name/
├── SKILL.md           # Main skill definition with YAML frontmatter
├── references/        # Supporting reference documents
│   ├── patterns.md
│   ├── examples.md
│   └── resources.md
├── assets/            # Templates and assets
│   └── template.md
└── scripts/           # Supporting scripts (optional)
```

**SKILL.md Format:**
```markdown
---
name: skill-name
description: Brief description in third person
allowed-tools:
  - Read
  - Write
  - Bash
preconditions:
  - Required condition 1
  - Required condition 2
---

# Skill Title

[Detailed skill documentation with intake options, routing, and examples]
```

### MCP Servers (1)

**Context7:**
- Provides framework documentation lookup
- HTTP-based MCP server at `https://mcp.context7.com/mcp`
- Tools: `resolve-library-id`, `get-library-docs`
- Supports 100+ frameworks (Rails, React, Next.js, Vue, Django, etc.)

---

## 7. Architecture and Design

### Overall System Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                     Claude Code Interface                        │
├─────────────────────────────────────────────────────────────────┤
│                                                                   │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐           │
│  │   Commands   │  │    Agents    │  │    Skills    │           │
│  │              │  │              │  │              │           │
│  │ /plan        │  │ security-    │  │ compound-    │           │
│  │ /work        │  │ sentinel     │  │ docs         │           │
│  │ /review      │  │              │  │              │           │
│  │ /compound    │  │ kieran-      │  │ agent-native │           │
│  │              │  │ rails-rev    │  │ -arch        │           │
│  └──────┬───────┘  └──────┬───────┘  └──────┬───────┘           │
│         │                 │                 │                    │
│         └────────────┬────┴────────────────┘                    │
│                      │                                           │
│              ┌───────▼───────┐                                  │
│              │   MCP Tools   │                                  │
│              │   Context7    │                                  │
│              └───────────────┘                                  │
│                                                                   │
├─────────────────────────────────────────────────────────────────┤
│                     Knowledge Layer                              │
│                                                                   │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │                    docs/solutions/                        │   │
│  │                                                           │   │
│  │  performance-issues/  security-issues/  build-errors/     │   │
│  │  database-issues/     runtime-errors/   test-failures/    │   │
│  └──────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────┘
```

### Component Relationships

**Commands orchestrate Agents:**
- `/workflows:plan` spawns: `repo-research-analyst`, `best-practices-researcher`, `framework-docs-researcher`, `spec-flow-analyzer`
- `/workflows:review` spawns up to 15 review agents in parallel
- `/workflows:work` may invoke: `code-simplicity-reviewer`, `kieran-rails-reviewer`, design agents

**Commands invoke Skills:**
- `/workflows:compound` routes to `compound-docs` skill
- `/workflows:work` may use `git-worktree`, `file-todos` skills
- Agents can load skills for domain expertise

**Skills provide Knowledge:**
- Reference documents in `references/` subdirectory
- Templates in `assets/` subdirectory
- Executable scripts in `scripts/` subdirectory

### Design Patterns Used

#### 1. Parallel Sub-Agent Pattern

Commands spawn multiple agents simultaneously:

```
Task kieran-rails-reviewer(PR content)
Task dhh-rails-reviewer(PR content)
Task security-sentinel(PR content)
Task performance-oracle(PR content)
[... more agents in parallel]
```

#### 2. Skill-Based Knowledge Injection

Skills provide curated, domain-specific knowledge that agents can load:

```markdown
# best-practices-researcher agent

### Phase 1: Check Available Skills FIRST
1. Discover Available Skills via Glob
2. Identify Relevant Skills (Rails → dhh-rails-style)
3. Extract Patterns from Skills
4. Assess Coverage before going online
```

#### 3. File-Based State Management

The `file-todos` skill uses files as the state interface:

```
todos/
├── 001-pending-p1-security-vulnerability.md
├── 002-pending-p2-performance-optimization.md
└── 003-complete-p3-code-cleanup.md
```

File naming encodes state: `{id}-{status}-{priority}-{description}.md`

#### 4. YAML Frontmatter for Metadata

All components use YAML frontmatter for structured metadata:

```yaml
---
name: component-name
description: "Description with examples"
model: inherit
allowed-tools: [Read, Write, Bash]
preconditions:
  - Condition 1
---
```

#### 5. Decision Gates and User Interaction

Commands present options and wait for user input:

```markdown
What's next?
1. Continue workflow (recommended)
2. Run `/deepen-plan`
3. Run `/plan_review`
4. Start `/workflows:work`
5. Create Issue
6. Simplify
```

#### 6. Progressive Enhancement

The "Grow Your Own Garden" pattern:
- Start with core (seed) functionality
- Suggest additions based on usage patterns
- Allow customization at project, user, and plugin levels

---

## 8. Implementation Details

### Command Implementation (/workflows:plan)

**Structure:**
```markdown
---
name: workflows:plan
description: Transform feature descriptions into well-structured project plans
argument-hint: "[feature description, bug report, or improvement idea]"
---
```

**Main Tasks:**

1. **Repository Research & Context Gathering** (Parallel Agents)
   - `Task repo-research-analyst(feature_description)`
   - `Task best-practices-researcher(feature_description)`
   - `Task framework-docs-researcher(feature_description)`

2. **Issue Planning & Structure**
   - Title & categorization (conventional format)
   - Stakeholder analysis
   - Content planning

3. **SpecFlow Analysis**
   - `Task spec-flow-analyzer(feature_description, research_findings)`

4. **Detail Level Selection**
   - MINIMAL: Simple bugs, small improvements
   - MORE: Most features, complex bugs
   - A LOT: Major features, architectural changes

5. **Issue Creation & Formatting**
   - Clear headings, code examples, task lists
   - Cross-referencing, AI-era considerations

6. **Post-Generation Options**
   - Open in editor
   - Run `/deepen-plan`
   - Run `/plan_review`
   - Start `/workflows:work`
   - Create Issue (GitHub/Linear)

### Agent Implementation (security-sentinel)

**Frontmatter:**
```yaml
---
name: security-sentinel
description: "Use this agent when you need to perform security audits..."
model: inherit
---
```

**Core Protocol:**

1. Input Validation Analysis
2. SQL Injection Risk Assessment
3. XSS Vulnerability Detection
4. Authentication & Authorization Audit
5. Sensitive Data Exposure Scan
6. OWASP Top 10 Compliance Check

**Reporting Protocol:**
- Executive Summary with severity ratings
- Detailed Findings with code locations
- Risk Matrix (Critical/High/Medium/Low)
- Remediation Roadmap

### Skill Implementation (compound-docs)

**7-Step Process:**

1. **Detect Confirmation** - Auto-invoke on "that worked", "it's fixed", etc.
2. **Gather Context** - Extract from conversation (BLOCKING if missing info)
3. **Check Existing Docs** - Search for similar issues
4. **Generate Filename** - `[symptom]-[module]-[YYYYMMDD].md`
5. **Validate YAML Schema** - BLOCKING validation gate
6. **Create Documentation** - Write to `docs/solutions/[category]/`
7. **Cross-Reference** - Link related issues, detect critical patterns

**Categories (Auto-detected):**
- build-errors/
- test-failures/
- runtime-errors/
- performance-issues/
- database-issues/
- security-issues/
- ui-bugs/
- integration-issues/
- logic-errors/

### Agent-Native Architecture Skill

**Core Principles:**

1. **Parity** - Whatever the user can do through UI, agent achieves through tools
2. **Granularity** - Prefer atomic primitives; features are prompt-defined outcomes
3. **Composability** - New features via new prompts, not new code
4. **Emergent Capability** - Agent accomplishes unanticipated tasks
5. **Improvement Over Time** - Gets better through context and prompt refinement

**Architecture Checklist:**
- Every UI action has corresponding agent capability
- Tools are primitives (not workflows)
- CRUD completeness for every entity
- Dynamic context injection
- Shared workspace between agent and user
- Explicit completion signals (not heuristic detection)

**Anti-Patterns:**
- Agent as router (routing not acting)
- Build app, then add agent
- Request/response thinking (missing the loop)
- Defensive tool design (over-constrained inputs)
- Context starvation (agent doesn't know resources)
- Orphan UI actions (no agent equivalent)
- Static tool mapping for dynamic APIs
- Incomplete CRUD

---

## 9. Integration Patterns

### Installing the Plugin

```bash
# Add the marketplace
claude /plugin marketplace add https://github.com/kieranklaassen/every-marketplace

# Install the plugin
claude /plugin install compound-engineering

# Verify installation
claude /plugin list
```

### Using Commands

```bash
# Create a plan
/workflows:plan "Add user authentication with OAuth"

# Execute work
/workflows:work plans/feat-user-authentication.md

# Run code review
/workflows:review PR#123
/workflows:review latest

# Document a fix
/workflows:compound "Fixed the N+1 query issue"
```

### Using Agents

```bash
# Direct agent invocation
claude agent security-sentinel
claude agent kieran-rails-reviewer
claude agent performance-oracle

# Agent with context
claude agent best-practices-researcher "JWT authentication patterns"
```

### Using Skills

```bash
# Invoke skill directly
skill: compound-docs
skill: agent-native-architecture
skill: dhh-rails-style

# Skills are also auto-loaded by commands/agents
```

### Extension Mechanisms

**Adding Custom Agents:**
```
.claude/agents/my-custom-reviewer.md
~/.claude/agents/my-global-agent.md
```

**Adding Custom Skills:**
```
.claude/skills/my-skill/
├── SKILL.md
└── references/
    └── patterns.md
```

**Adding Custom Commands:**
Create markdown file in appropriate commands directory with frontmatter.

### Customization Options

**Model Selection:**
```yaml
model: inherit      # Use user's configured model
model: haiku        # Force specific model (cost efficiency)
model: sonnet       # Force specific model
```

**Tool Restrictions:**
```yaml
allowed-tools:
  - Read
  - Write
  - Bash
  - Grep
```

**Preconditions:**
```yaml
preconditions:
  - Problem has been solved
  - Solution has been verified
```

---

## 10. Best Practices

### From the Repository Documentation

#### Planning Best Practices

1. **Front-load Research** - Run parallel research agents before coding
2. **Use Appropriate Detail Level** - MINIMAL for simple, A LOT for major changes
3. **Include References** - Link to similar code, file paths, line numbers
4. **Track with Kebab-Case Files** - `plans/feat-user-auth.md`

#### Code Review Best Practices

1. **Run Full Multi-Agent Review** - Use all applicable agents in parallel
2. **Prioritize by Severity** - P1 (Critical) blocks merge, P2 (Important), P3 (Nice-to-have)
3. **Create Actionable Todos** - Store findings in `todos/` with file-todos skill
4. **Address P1s Before Merge** - Critical findings must be resolved

#### Documentation Best Practices

1. **Compound Immediately** - Run `/workflows:compound` right after fixing
2. **Include Exact Error Messages** - Copy-paste from output
3. **Document Failed Attempts** - Helps avoid wrong paths
4. **Explain Why, Not Just What** - Technical explanations
5. **Cross-Reference Related Issues** - Build knowledge graph

#### Agent-Native Best Practices

1. **Maintain Parity** - Every UI action has agent equivalent
2. **Use Atomic Tools** - Primitives over workflows
3. **Inject Dynamic Context** - Agent knows available resources
4. **Explicit Completion** - Use `complete_task` tool, not heuristics
5. **Shared Workspace** - Agent and user work in same space

### Anti-Patterns to Avoid

#### General Anti-Patterns

- **Skipping planning** - Jumping to code without research
- **Ignoring plan references** - Plans have links for a reason
- **Testing at the end** - Test continuously
- **80% done syndrome** - Finish features completely
- **Over-reviewing simple changes** - Save deep review for complex work

#### Code Anti-Patterns (from kieran-rails-reviewer)

- **Added complexity without justification** - Extract to new controllers/services
- **Separate turbo_stream.erb files for simple operations** - Use inline arrays
- **Vague naming** - 5-second rule: must understand in 5 seconds
- **Service extraction without signals** - Only when truly needed
- **Non-compact namespacing** - Use `class Module::ClassName` pattern

#### Agent-Native Anti-Patterns

- **Agent as router** - Using intelligence to route, not act
- **Context starvation** - Agent doesn't know what resources exist
- **Orphan UI actions** - UI capability without agent equivalent
- **Static tool mapping** - Building tool per API endpoint
- **Incomplete CRUD** - Create without update/delete
- **Sandbox isolation** - Agent in separate data space
- **Heuristic completion** - Detecting done without explicit signal

### Guidelines for Users

1. **Start with the workflow commands** - `/plan`, `/work`, `/review`, `/compound`
2. **Trust the agents** - They enforce conventions consistently
3. **Document solutions immediately** - While context is fresh
4. **Use skills for domain expertise** - They contain curated knowledge
5. **Customize gradually** - Add project/user agents as patterns emerge
6. **Keep knowledge in the system** - Not just in developers' heads

---

## 11. Examples and Usage

### Complete Workflow Example

```bash
# 1. PLAN - Create implementation plan
/workflows:plan "Add real-time notifications with ActionCable"

# Output: plans/feat-add-realtime-notifications.md
# → Ran research agents in parallel
# → Created structured plan with references
# → Offered to deepen plan or start work

# 2. WORK - Execute the plan
/workflows:work plans/feat-add-realtime-notifications.md

# → Set up feature branch or worktree
# → Created TodoWrite tasks from plan
# → Implemented following existing patterns
# → Ran tests continuously
# → Created PR with screenshots

# 3. REVIEW - Multi-agent code review
/workflows:review PR#456

# → Launched 12+ agents in parallel
# → Created todos in todos/ directory
# → Categorized findings by severity (P1/P2/P3)
# → Offered browser testing

# 4. COMPOUND - Document learnings
/workflows:compound "Fixed WebSocket connection race condition"

# → Auto-detected problem type
# → Created docs/solutions/runtime-errors/websocket-race-condition.md
# → Cross-referenced similar issues
# → Offered to add to Required Reading
```

### Agent Usage Examples

```bash
# Security review
claude agent security-sentinel "Review the new payment processing endpoints"

# Rails code review
claude agent kieran-rails-reviewer "Check the UserController changes"

# Research best practices
claude agent best-practices-researcher "JWT authentication in Rails APIs"

# Performance analysis
claude agent performance-oracle "Analyze the dashboard query performance"
```

### Skill Usage Examples

```bash
# Document a solution
skill: compound-docs
# → Walks through 7-step process
# → Creates YAML-fronted documentation

# Design agent-native architecture
skill: agent-native-architecture
# → Presents 13 options
# → Routes to relevant reference documents
# → Provides architecture checklist

# Write DHH-style Rails code
skill: dhh-rails-style
# → Provides patterns and conventions
# → Examples of controllers, models, frontend
```

### File-Based Todo Example

```markdown
# todos/001-pending-p1-sql-injection.md

---
status: pending
priority: p1
issue_id: "001"
tags: [security, code-review, sql-injection]
---

## Problem Statement
SQL injection vulnerability in search functionality...

## Findings
- Line 47 of search_controller.rb uses string interpolation
- User input not sanitized before query

## Proposed Solutions

### Option A: Parameterized Query
- Pros: Simple, standard approach
- Cons: None
- Effort: Small
- Risk: Low

## Acceptance Criteria
- [ ] All queries use parameterized inputs
- [ ] Security tests added
- [ ] Brakeman scan passes
```

### Getting Started Flow

1. **Install the plugin:**
   ```bash
   claude /plugin marketplace add https://github.com/kieranklaassen/every-marketplace
   claude /plugin install compound-engineering
   ```

2. **Try a simple review:**
   ```bash
   /workflows:review latest
   ```

3. **Create a plan:**
   ```bash
   /workflows:plan "Add dark mode toggle"
   ```

4. **Execute the plan:**
   ```bash
   /workflows:work plans/feat-add-dark-mode-toggle.md
   ```

5. **Document when done:**
   ```bash
   /workflows:compound
   ```

---

## 12. Complete Summary

### Key Takeaways

#### The Core Philosophy

**Compound Engineering** represents a paradigm shift from "accumulating technical debt" to "accumulating knowledge." Instead of each piece of work making the codebase harder to maintain, each solution makes future work easier.

The metaphor is financial: traditional development is spending (depleting), while compound engineering is investing (growing). Each documented solution, each refined pattern, each enforced convention compounds over time.

#### The Implementation

The paradigm is implemented as a Claude Code plugin with four main components:

1. **Workflow Commands** (`/plan`, `/work`, `/review`, `/compound`) - Orchestrate the entire development lifecycle
2. **Specialized Agents** (27 agents) - Provide consistent expert review and research
3. **Domain Skills** (14 skills) - Encode curated knowledge and best practices
4. **Knowledge Layer** (`docs/solutions/`) - Store and organize documented solutions

#### The Workflow Loop

```
Plan → Work → Review → Compound → Repeat
  ↑                                   ↓
  └───── Knowledge feeds back ────────┘
```

Each cycle builds on the last. Plans reference past solutions. Reviews learn from previous findings. Documentation grows richer with each problem solved.

#### The Architectural Principles

1. **Parallel Execution** - Multiple agents work simultaneously
2. **Knowledge Injection** - Skills provide domain expertise
3. **File-Based State** - Transparent, searchable, git-friendly
4. **Progressive Enhancement** - Start simple, grow as needed
5. **User Control** - Decision gates keep humans in the loop

### How This Paradigm Could Be Applied

#### For a C# Implementation

The compound-engineering paradigm translates well to .NET development:

**Agents to Create:**
- `csharp-reviewer` - C# conventions, SOLID principles
- `dotnet-security-sentinel` - .NET-specific security patterns
- `ef-core-guardian` - Entity Framework best practices
- `aspnet-architecture-strategist` - ASP.NET Core architecture

**Skills to Implement:**
- `clean-architecture` - Domain-driven design patterns
- `ef-migrations` - Database migration best practices
- `aspnet-minimal-api` - Modern API patterns
- `compound-docs` (port) - Solution documentation

**Workflow Commands:**
- Port the four core commands maintaining the same structure
- Adapt file patterns for .NET conventions
- Integrate with MSBuild, dotnet CLI

**Knowledge Structure:**
```
docs/solutions/
├── build-errors/
├── ef-migrations/
├── performance-issues/
├── security-issues/
└── patterns/
    └── dotnet-critical-patterns.md
```

#### Key Implementation Considerations

1. **Start with the Core Loop** - Plan, Work, Review, Compound
2. **Build Universal Agents First** - Security, performance, architecture
3. **Add Language-Specific Agents** - C# conventions, .NET patterns
4. **Create Domain Skills** - Curated knowledge for common patterns
5. **Establish Knowledge Structure** - Categorized solutions directory
6. **Enable the Compound Step** - Auto-capture of solutions

### Final Thoughts

The compound-engineering paradigm is more than a set of tools - it's a philosophy of development that treats knowledge as an asset. By systematically capturing solutions, enforcing conventions through AI agents, and building on past work, teams can achieve what the documentation calls "making today's work easier than yesterday's."

The implementation demonstrates sophisticated patterns: parallel agent execution, skill-based knowledge injection, file-based state management, and progressive enhancement. These patterns are transferable to any technology stack that works with Claude Code.

The key insight is that **AI agents don't forget**. Unlike human teams where knowledge walks out the door, these agents consistently enforce conventions, remember patterns, and build on documented solutions. This is the compound effect: each cycle makes the system smarter.

---

**End of Research Report**

*Generated from source repository analysis on 2026-01-22*
