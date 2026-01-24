# Claude Code Hooks for Skill Auto-Invocation Research

**Research Date:** January 22, 2026
**Research Focus:** Using Claude Code hooks to automatically invoke skills when entering a project

---

## Table of Contents

1. [Executive Summary](#1-executive-summary)
2. [Available Hook Types](#2-available-hook-types)
3. [Hook Configuration Format and Location](#3-hook-configuration-format-and-location)
4. [Project Entry Detection](#4-project-entry-detection)
5. [Can Hooks Invoke Skills Directly?](#5-can-hooks-invoke-skills-directly)
6. [Workarounds for Auto-Invoking Skills](#6-workarounds-for-auto-invoking-skills)
7. [File-Based Triggers](#7-file-based-triggers)
8. [Complete Configuration Examples](#8-complete-configuration-examples)
9. [Alternative Approaches](#9-alternative-approaches)
10. [Best Practices](#10-best-practices)
11. [Limitations and Known Issues](#11-limitations-and-known-issues)

---

## 1. Executive Summary

### Key Findings

1. **Hooks cannot directly invoke skills** - Hooks are deterministic shell commands that run at lifecycle points. They cannot call the `Skill()` tool directly.

2. **Hooks CAN inject context that triggers skills** - By outputting text or JSON with `additionalContext`, hooks can inject instructions that cause Claude to invoke skills.

3. **SessionStart is the primary hook for project entry** - This hook fires when Claude Code starts a new session, making it ideal for project initialization.

4. **UserPromptSubmit enables per-prompt skill suggestions** - This hook can analyze each prompt and inject skill activation instructions.

5. **CLAUDE.md is automatically loaded** - Claude Code already reads `CLAUDE.md` at session start, providing a built-in mechanism for project context.

### Recommended Approach

Use a combination of:
- **SessionStart hook** to inject initial project context and skill recommendations
- **UserPromptSubmit hook** to analyze prompts and suggest relevant skills
- **CLAUDE.md** for persistent project instructions that reference skills

---

## 2. Available Hook Types

### Complete Hook Lifecycle Events

| Hook Event | When It Fires | Primary Use Case |
|------------|---------------|------------------|
| **SessionStart** | Session begins or resumes | Load context, set environment variables |
| **UserPromptSubmit** | User submits a prompt (before processing) | Validate/modify prompts, inject context, suggest skills |
| **PreToolUse** | Before tool execution | Allow, deny, or modify tool calls |
| **PermissionRequest** | When permission dialog appears | Auto-allow or deny permissions |
| **PostToolUse** | After tool succeeds | Validate output, run formatters, provide feedback |
| **PostToolUseFailure** | After tool fails | Handle failures, retry logic |
| **SubagentStart** | When spawning a subagent | Monitor subagent initialization |
| **SubagentStop** | When subagent finishes | Evaluate/block subagent completion |
| **Stop** | Claude finishes responding | Block/continue Claude's work |
| **PreCompact** | Before context compaction | Prepare for compaction |
| **Setup** | During `--init`, `--init-only`, `--maintenance` | Install dependencies, run migrations |
| **SessionEnd** | Session terminates | Cleanup tasks, logging |
| **Notification** | When notifications are sent | Custom notification handling |

### Hook Events Relevant for Skill Invocation

| Hook | Skill Invocation Relevance |
|------|---------------------------|
| **SessionStart** | Best for initial skill loading - inject context on every session start |
| **UserPromptSubmit** | Best for dynamic skill activation - analyze each prompt and suggest skills |
| **Stop** | Can inject "remember to use X skill" reminders |
| **SubagentStart** | Can inject skill context for subagents |

---

## 3. Hook Configuration Format and Location

### Configuration File Locations

| Scope | Location | Purpose |
|-------|----------|---------|
| **Project-level** | `.claude/settings.json` | Shared with team via git |
| **Local project** | `.claude/settings.local.json` | Personal config, gitignored |
| **User-level** | `~/.claude/settings.json` | Applies to all projects |
| **Plugin hooks** | `<plugin>/hooks/hooks.json` | Plugin-provided hooks |
| **Skill-scoped** | YAML frontmatter in SKILL.md | Active only when skill runs |
| **Managed** | System paths (admin-deployed) | Organization-wide control |

### Settings File Hierarchy (Precedence)

1. Project-level (`.claude/settings.json`) - Highest priority
2. Local project (`.claude/settings.local.json`)
3. User-level (`~/.claude/settings.json`)
4. Managed settings

### Basic Hook Configuration Structure

```json
{
  "hooks": {
    "EventName": [
      {
        "matcher": "pattern",
        "hooks": [
          {
            "type": "command",
            "command": "your-command-here",
            "timeout": 30
          }
        ]
      }
    ]
  }
}
```

### Matchers

Matchers apply to tool-related hooks (`PreToolUse`, `PostToolUse`, `PermissionRequest`):

| Pattern | Description |
|---------|-------------|
| `Write` | Exact tool match |
| `Edit\|Write` | Multiple tools (regex) |
| `Notebook.*` | Regex pattern |
| `*` | Match all tools |
| `mcp__<server>__<tool>` | MCP tool matching |

For `SessionStart`, matchers can specify the source:
- `startup` - New session
- `resume` - Resumed session
- `clear` - After clearing context
- `compact` - After compaction

---

## 4. Project Entry Detection

### How Claude Code Detects Project Entry

1. **Working Directory**: Claude Code uses the current working directory when launched
2. **CLAUDE.md Detection**: Automatically reads `CLAUDE.md` from project root at session start
3. **.claude Directory**: Looks for `.claude/` directory for settings and skills
4. **SessionStart Hook**: Fires when entering any project session

### SessionStart Hook for Project Detection

```json
{
  "hooks": {
    "SessionStart": [
      {
        "matcher": "startup",
        "hooks": [
          {
            "type": "command",
            "command": "\"$CLAUDE_PROJECT_DIR\"/.claude/hooks/on-project-entry.sh",
            "timeout": 10
          }
        ]
      }
    ]
  }
}
```

### Project Detection Script Example

```bash
#!/bin/bash
# .claude/hooks/on-project-entry.sh

cd "$CLAUDE_PROJECT_DIR" || exit 1

# Detect project type and inject relevant context
if [ -f "package.json" ]; then
  echo "This is a Node.js project. Consider using the 'node-patterns' skill for best practices."
elif [ -f "*.csproj" ] || [ -f "*.sln" ]; then
  echo "This is a .NET project. Consider using the 'dotnet-patterns' skill for best practices."
elif [ -f "Cargo.toml" ]; then
  echo "This is a Rust project. Consider using the 'rust-patterns' skill for best practices."
fi

# Check for project-specific skill recommendations
if [ -f ".claude/skill-recommendations.txt" ]; then
  cat ".claude/skill-recommendations.txt"
fi

exit 0
```

### Environment Variables Available to Hooks

| Variable | Description |
|----------|-------------|
| `$CLAUDE_PROJECT_DIR` | Project root directory |
| `$CLAUDE_ENV_FILE` | File for persisting env vars (SessionStart only) |
| `$CLAUDE_PLUGIN_ROOT` | Plugin directory (for plugin hooks) |
| `$CLAUDE_CODE_REMOTE` | `true` if running in web environment |

---

## 5. Can Hooks Invoke Skills Directly?

### Short Answer: No, But...

Hooks **cannot** directly call the `Skill()` tool. However, hooks **can**:

1. **Inject context** that Claude uses to decide to invoke skills
2. **Output instructions** that cause Claude to use specific skills
3. **Suggest skills** through stdout content added to conversation context

### Why Direct Invocation Isn't Possible

- Hooks are **shell commands** that run outside Claude's context
- The `Skill()` tool is an **internal Claude Code tool** only callable by Claude
- Hooks execute **before** Claude processes the request

### What Hooks CAN Do

#### 1. Inject Plain Text Context (SessionStart/UserPromptSubmit)

```bash
#!/bin/bash
# stdout becomes conversation context
echo "IMPORTANT: Use the 'code-review' skill for any code review tasks in this project."
exit 0
```

#### 2. Inject Structured Context via JSON

```json
{
  "hookSpecificOutput": {
    "hookEventName": "SessionStart",
    "additionalContext": "This project uses the 'typescript-patterns' skill. Invoke it when writing TypeScript code."
  }
}
```

#### 3. Issue Skill Activation Instructions

```bash
#!/bin/bash
# Strong instruction pattern
echo "INSTRUCTION: For this project, you MUST use the following skills:"
echo "- /code-review for reviewing changes"
echo "- /test-patterns for writing tests"
echo "- /commit for creating commits"
exit 0
```

---

## 6. Workarounds for Auto-Invoking Skills

### Approach 1: SessionStart Context Injection

**Configuration** (`.claude/settings.json`):
```json
{
  "hooks": {
    "SessionStart": [
      {
        "hooks": [
          {
            "type": "command",
            "command": "\"$CLAUDE_PROJECT_DIR\"/.claude/hooks/inject-skills.sh",
            "timeout": 5
          }
        ]
      }
    ]
  }
}
```

**Script** (`.claude/hooks/inject-skills.sh`):
```bash
#!/bin/bash
cat << 'EOF'
## Project Skill Configuration

This project has the following skills configured. Use them automatically when appropriate:

1. **sc:load** - ALWAYS invoke this skill at the start of the session to load project context
2. **code-review** - Use when reviewing code changes
3. **test-patterns** - Use when writing or modifying tests
4. **commit** - Use when creating git commits

INSTRUCTION: Invoke /sc:load NOW to initialize the session properly.
EOF
exit 0
```

### Approach 2: UserPromptSubmit Skill Activation Engine

This approach analyzes each prompt and suggests relevant skills.

**Configuration** (`.claude/settings.json`):
```json
{
  "hooks": {
    "UserPromptSubmit": [
      {
        "hooks": [
          {
            "type": "command",
            "command": "\"$CLAUDE_PROJECT_DIR\"/.claude/hooks/skill-activator.sh",
            "timeout": 5
          }
        ]
      }
    ]
  }
}
```

**Script** (`.claude/hooks/skill-activator.sh`):
```bash
#!/bin/bash
# Read prompt from stdin
PROMPT=$(cat)
PROMPT_TEXT=$(echo "$PROMPT" | jq -r '.prompt // empty')

# Define skill triggers (keyword -> skill mapping)
declare -A SKILL_TRIGGERS=(
  ["test"]="test-patterns"
  ["commit"]="commit"
  ["review"]="code-review"
  ["deploy"]="deploy"
  ["document"]="documentation"
)

# Check for trigger keywords
SUGGESTED_SKILLS=""
for keyword in "${!SKILL_TRIGGERS[@]}"; do
  if echo "$PROMPT_TEXT" | grep -qi "$keyword"; then
    SUGGESTED_SKILLS="$SUGGESTED_SKILLS\n- /${SKILL_TRIGGERS[$keyword]}"
  fi
done

# Output skill suggestions if any found
if [ -n "$SUGGESTED_SKILLS" ]; then
  echo "SUGGESTION: Based on your request, consider using these skills:$SUGGESTED_SKILLS"
fi

exit 0
```

### Approach 3: Forced Evaluation Pattern

This approach uses stronger language to ensure Claude considers skills.

**Script** (`.claude/hooks/forced-skill-eval.sh`):
```bash
#!/bin/bash
PROMPT=$(cat)
PROMPT_TEXT=$(echo "$PROMPT" | jq -r '.prompt // empty')

cat << 'EOF'
## MANDATORY SKILL EVALUATION

Before proceeding with ANY task, you MUST:

1. **EVALUATE**: Check each available skill against the current request
2. **ACTIVATE**: If a skill matches, use Skill() tool IMMEDIATELY
3. **IMPLEMENT**: Only proceed with implementation AFTER skill activation

Available skills to evaluate:
- /sc:load - Session initialization (use at start)
- /code-review - Code review tasks
- /test-patterns - Testing tasks
- /commit - Git commit tasks

This evaluation is REQUIRED, not optional.
EOF

exit 0
```

### Approach 4: JSON-Based Skill Rules Engine

**Configuration** (`.claude/hooks/skill-rules.json`):
```json
{
  "skills": {
    "sc:load": {
      "triggers": {
        "keywords": ["start", "begin", "initialize", "load context"],
        "patterns": ["^.*session.*start.*$"],
        "firstPromptOnly": true
      },
      "priority": 1,
      "enforcement": "mandatory"
    },
    "test-patterns": {
      "triggers": {
        "keywords": ["test", "testing", "spec", "jest", "vitest"],
        "pathPatterns": ["**/*.test.ts", "**/*.spec.ts", "**/tests/**"]
      },
      "priority": 2,
      "enforcement": "suggest"
    },
    "code-review": {
      "triggers": {
        "keywords": ["review", "check", "audit", "examine"],
        "intentPatterns": ["look at.*code", "review.*changes"]
      },
      "priority": 2,
      "enforcement": "suggest"
    }
  }
}
```

**Node.js Evaluator** (`.claude/hooks/skill-evaluator.mjs`):
```javascript
#!/usr/bin/env node
import { readFileSync } from 'fs';
import { join, dirname } from 'path';
import { fileURLToPath } from 'url';

const __dirname = dirname(fileURLToPath(import.meta.url));
const projectDir = process.env.CLAUDE_PROJECT_DIR || process.cwd();

// Read prompt from stdin
let input = '';
process.stdin.setEncoding('utf8');
for await (const chunk of process.stdin) {
  input += chunk;
}

const { prompt, session_id } = JSON.parse(input);

// Load skill rules
const rulesPath = join(projectDir, '.claude/hooks/skill-rules.json');
const rules = JSON.parse(readFileSync(rulesPath, 'utf8'));

// Evaluate skills
const suggestions = [];
for (const [skillName, config] of Object.entries(rules.skills)) {
  const { triggers, enforcement } = config;
  let matched = false;

  // Check keywords
  if (triggers.keywords) {
    for (const keyword of triggers.keywords) {
      if (prompt.toLowerCase().includes(keyword.toLowerCase())) {
        matched = true;
        break;
      }
    }
  }

  // Check patterns
  if (!matched && triggers.patterns) {
    for (const pattern of triggers.patterns) {
      if (new RegExp(pattern, 'i').test(prompt)) {
        matched = true;
        break;
      }
    }
  }

  if (matched) {
    suggestions.push({ skill: skillName, enforcement });
  }
}

// Output suggestions
if (suggestions.length > 0) {
  const mandatory = suggestions.filter(s => s.enforcement === 'mandatory');
  const suggested = suggestions.filter(s => s.enforcement === 'suggest');

  if (mandatory.length > 0) {
    console.log(`INSTRUCTION: You MUST use these skills: ${mandatory.map(s => '/' + s.skill).join(', ')}`);
  }
  if (suggested.length > 0) {
    console.log(`SUGGESTION: Consider using: ${suggested.map(s => '/' + s.skill).join(', ')}`);
  }
}
```

---

## 7. File-Based Triggers

### Detecting Config File Presence

Hooks can check for specific files to determine project configuration:

```bash
#!/bin/bash
cd "$CLAUDE_PROJECT_DIR" || exit 1

# Check for skill configuration file
if [ -f ".claude/auto-skills.json" ]; then
  echo "Loading project skill configuration..."
  SKILLS=$(jq -r '.autoLoad[]' .claude/auto-skills.json 2>/dev/null)
  if [ -n "$SKILLS" ]; then
    echo "INSTRUCTION: Auto-load these skills for this project:"
    echo "$SKILLS" | while read skill; do
      echo "- /$skill"
    done
  fi
fi

# Check for CLAUDE.md with skill references
if [ -f "CLAUDE.md" ]; then
  # Extract skill recommendations from CLAUDE.md
  SKILL_SECTION=$(grep -A 20 "## Skills" CLAUDE.md 2>/dev/null)
  if [ -n "$SKILL_SECTION" ]; then
    echo "Note: Project CLAUDE.md contains skill recommendations."
  fi
fi

exit 0
```

### Auto-Skills Configuration File Format

**`.claude/auto-skills.json`**:
```json
{
  "autoLoad": [
    "sc:load"
  ],
  "suggestOnKeywords": {
    "test": "test-patterns",
    "commit": "commit",
    "review": "code-review"
  },
  "projectType": "typescript",
  "defaultSkills": ["typescript-patterns"]
}
```

---

## 8. Complete Configuration Examples

### Example 1: Full Project Setup with Skill Auto-Loading

**`.claude/settings.json`**:
```json
{
  "hooks": {
    "SessionStart": [
      {
        "matcher": "startup",
        "hooks": [
          {
            "type": "command",
            "command": "\"$CLAUDE_PROJECT_DIR\"/.claude/hooks/session-init.sh",
            "timeout": 10
          }
        ]
      }
    ],
    "UserPromptSubmit": [
      {
        "hooks": [
          {
            "type": "command",
            "command": "node \"$CLAUDE_PROJECT_DIR\"/.claude/hooks/skill-evaluator.mjs",
            "timeout": 5
          }
        ]
      }
    ]
  }
}
```

**`.claude/hooks/session-init.sh`**:
```bash
#!/bin/bash
set -e

cd "$CLAUDE_PROJECT_DIR" || exit 1

# Set environment variables
if [ -n "$CLAUDE_ENV_FILE" ]; then
  echo "export PROJECT_NAME=$(basename $CLAUDE_PROJECT_DIR)" >> "$CLAUDE_ENV_FILE"
fi

# Output session initialization context
cat << 'EOF'
## Session Initialized

This project is configured with automatic skill activation.

**Required First Action:** Invoke `/sc:load` to initialize the session with full project context.

**Available Project Skills:**
- `/sc:load` - Load project context (USE FIRST)
- `/code-review` - Review code changes
- `/test-patterns` - Testing best practices
- `/commit` - Git commit workflow
- `/deploy` - Deployment workflow

Claude will automatically suggest relevant skills based on your requests.
EOF

exit 0
```

### Example 2: Plugin with Hook-Based Skill Loading

**Plugin Structure:**
```
my-plugin/
├── .claude-plugin/
│   └── plugin.json
├── hooks/
│   └── hooks.json
├── skills/
│   └── my-skill/
│       └── SKILL.md
└── scripts/
    └── load-skill-context.sh
```

**`hooks/hooks.json`**:
```json
{
  "description": "Auto-loads plugin skills on session start",
  "hooks": {
    "SessionStart": [
      {
        "hooks": [
          {
            "type": "command",
            "command": "bash \"${CLAUDE_PLUGIN_ROOT}/scripts/load-skill-context.sh\""
          }
        ]
      }
    ]
  }
}
```

### Example 3: Skill with Embedded Hooks

**`.claude/skills/secure-ops/SKILL.md`**:
```yaml
---
name: secure-ops
description: Secure operations with automatic validation. Use for any production changes or sensitive operations.
disable-model-invocation: true
hooks:
  PreToolUse:
    - matcher: "Bash"
      hooks:
        - type: command
          command: "./scripts/security-check.sh"
  Stop:
    - matcher: "*"
      hooks:
        - type: prompt
          prompt: "Verify all security checks passed before completing"
---

# Secure Operations

When performing secure operations:
1. Validate all inputs
2. Check for sensitive data exposure
3. Verify permissions
4. Log all actions
```

---

## 9. Alternative Approaches

### Alternative 1: CLAUDE.md Skill Instructions

Instead of hooks, use `CLAUDE.md` to specify skill preferences:

**`CLAUDE.md`**:
```markdown
# Project Instructions

## Skill Usage

When working in this project, ALWAYS use the following skills:

1. **On session start**: Invoke `/sc:load` to load project context
2. **For code reviews**: Use `/code-review`
3. **For testing**: Use `/test-patterns`
4. **For commits**: Use `/commit`

These skills are REQUIRED for this project. Do not skip them.
```

### Alternative 2: Subagent with Pre-Loaded Skills

Define a subagent that auto-loads specific skills:

**`.claude/agents/project-assistant.md`**:
```yaml
---
name: project-assistant
description: Project assistant with pre-loaded skills
skills:
  - sc:load
  - code-review
  - test-patterns
permissionMode: default
---

You are a project assistant with specialized skills pre-loaded.
Always use your loaded skills when relevant to the user's request.
```

### Alternative 3: Mode Skill for Global Behavior

Create a "mode" skill that modifies Claude's behavior:

**`.claude/skills/auto-skill-mode/SKILL.md`**:
```yaml
---
name: auto-skill-mode
description: Enables automatic skill activation for all requests
mode: true
---

# Auto-Skill Mode

When this mode is active:
1. Before each task, evaluate which skills are relevant
2. Automatically invoke relevant skills
3. Use skill guidance in all responses

Active skills to consider:
- /code-review - For code analysis
- /test-patterns - For testing
- /commit - For git operations
- /deploy - For deployment
```

---

## 10. Best Practices

### Hook Performance

1. **Keep hooks fast** - SessionStart hooks run on every session, so keep them under 5 seconds
2. **Use timeouts** - Set appropriate timeouts to prevent hanging
3. **Exit cleanly** - Always use proper exit codes (0 for success, 2 for blocking errors)

### Skill Activation Patterns

1. **Use strong language** - Words like "INSTRUCTION", "MUST", "REQUIRED" improve compliance
2. **Be specific** - List exact skill names with `/` prefix
3. **Explain when** - Include context about when each skill applies
4. **Avoid overload** - Don't suggest too many skills at once

### Configuration Organization

1. **Separate concerns** - Use different hooks for different purposes
2. **Version control** - Keep `.claude/settings.json` in git for team sharing
3. **Local overrides** - Use `.claude/settings.local.json` for personal preferences
4. **Document hooks** - Include comments in scripts explaining their purpose

### Testing Hooks

```bash
# Test SessionStart hook manually
echo '{"session_id":"test","source":"startup","model":"claude-sonnet-4"}' | \
  bash .claude/hooks/session-init.sh

# Test UserPromptSubmit hook
echo '{"prompt":"write a test for the login function"}' | \
  bash .claude/hooks/skill-activator.sh

# Run Claude Code with debug output
claude --debug
```

---

## 11. Limitations and Known Issues

### Skills Don't Auto-Activate Reliably

According to multiple reports, Claude Code's autonomous skill activation based on description matching achieves only ~50% reliability. Using hooks with forced evaluation can improve this to ~80-84%.

### SessionStart Hook Timing

- SessionStart hooks may not run on resumed sessions in some versions
- The `resume` matcher may not work as expected in all scenarios
- Workaround: Use both `startup` and `resume` matchers

### Context Injection Limits

- Stdout from hooks is added to context, consuming tokens
- Keep injected context concise
- Large context injection can impact performance

### Exit Code Behavior

| Exit Code | Behavior |
|-----------|----------|
| 0 | Success - stdout processed |
| 2 | Blocking error - stderr fed to Claude |
| Other | Non-blocking error - stderr shown in verbose mode |

### Known Issues

1. **Skill activation via hooks is a workaround** - Not an officially supported feature
2. **Hook output visible in verbose mode** - Users see hook output with Ctrl+O
3. **JSON parsing sensitivity** - Malformed JSON in hook output can cause issues
4. **Environment variable scope** - `CLAUDE_ENV_FILE` only available in SessionStart

---

## Sources

### Official Documentation
- [Claude Code Hooks Reference](https://code.claude.com/docs/en/hooks)
- [Claude Code Hooks Guide](https://code.claude.com/docs/en/hooks-guide)
- [Claude Code Skills Documentation](https://code.claude.com/docs/en/skills)
- [Claude Code Settings](https://code.claude.com/docs/en/settings)

### GitHub Repositories
- [anthropics/claude-code](https://github.com/anthropics/claude-code)
- [ChrisWiles/claude-code-showcase](https://github.com/ChrisWiles/claude-code-showcase)
- [launchdarkly-labs/claude-code-session-start-hook](https://github.com/launchdarkly-labs/claude-code-session-start-hook)
- [disler/claude-code-hooks-mastery](https://github.com/disler/claude-code-hooks-mastery)
- [blader/Claudeception](https://github.com/blader/Claudeception)

### Blog Posts and Articles
- [Claude Code Skills Don't Auto-Activate (a workaround)](https://scottspence.com/posts/claude-code-skills-dont-auto-activate) - Scott Spence
- [How to Make Claude Code Skills Activate Reliably](https://scottspence.com/posts/how-to-make-claude-code-skills-activate-reliably) - Scott Spence
- [A Complete Guide to Hooks in Claude Code](https://www.eesel.ai/blog/hooks-in-claude-code) - Eesel.ai
- [Claude Code Hooks: A Practical Guide](https://www.datacamp.com/tutorial/claude-code-hooks) - DataCamp
- [Configure Claude Code Hooks to Automate Your Workflow](https://www.gend.co/blog/configure-claude-code-hooks-automation) - Gend.co
- [Claude Code power user customization: How to configure hooks](https://claude.com/blog/how-to-configure-hooks) - Anthropic
- [The Complete Guide to CLAUDE.md](https://www.builder.io/blog/claude-md-guide) - Builder.io

### Community Resources
- [Skills Auto-Activation via Hooks](https://paddo.dev/blog/claude-skills-hooks-solution/)
- [Ultimate Guide to Claude Code Setup](https://aibit.im/blog/post/ultimate-guide-to-claude-code-setup-hooks-skills-actions)
- [Claude Code Hooks Complete Guide](https://smartscope.blog/en/generative-ai/claude/claude-code-hooks-guide/)

---

## Summary

While Claude Code hooks cannot directly invoke skills, they provide powerful mechanisms for:

1. **Injecting context** at session start that instructs Claude to use specific skills
2. **Analyzing prompts** and suggesting relevant skills per-request
3. **Detecting project type** and recommending appropriate skills
4. **Creating forced evaluation patterns** that improve skill activation reliability

The recommended approach for auto-invoking skills when entering a project:

1. Use `SessionStart` hook with `startup` matcher to inject initial skill instructions
2. Use `UserPromptSubmit` hook to analyze each prompt and suggest skills
3. Include skill usage instructions in `CLAUDE.md` as a backup
4. Use strong, imperative language in hook output to maximize compliance
5. Test hooks thoroughly with debug mode enabled
