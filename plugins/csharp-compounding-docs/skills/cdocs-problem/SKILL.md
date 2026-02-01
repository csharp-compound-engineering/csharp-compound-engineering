---
name: cdocs:problem
description: Captures solved problems with symptoms, root cause, and solution
allowed-tools:
  - Read
  - Write
  - Bash
preconditions:
  - Project activated via /cdocs:activate
  - Problem has been solved or is in progress
auto-invoke:
  trigger: conversation-pattern
  patterns:
    - "fixed"
    - "it's fixed"
    - "bug"
    - "the issue was"
    - "problem solved"
    - "resolved"
    - "exception"
    - "error"
    - "crash"
    - "failing"
---

# Problem Documentation Skill

## Intake

This skill captures documentation for solved or in-progress problems, bugs, and issues. It expects the following context from the conversation:

- **Problem description**: What was the issue?
- **Symptoms**: Observable error messages, unexpected behavior, or failures
- **Root cause**: The underlying cause of the problem (if identified)
- **Solution**: How the problem was resolved (if solved)
- **Context**: Files, modules, or versions involved

## Process

### Step 1: Gather Context

Extract from conversation history:
- **Problem summary**: What went wrong?
- **Symptoms**: Error messages, stack traces, unexpected behavior
- **Root cause**: Why did this happen? (use Sequential Thinking MCP for complex multi-factor analysis)
- **Solution**: What fixed it?
- **Severity**: How critical was this issue?
- **Related files**: Which code was affected?

**BLOCKING**: If critical context is missing (problem description or symptoms), ask the user and WAIT for response.

### Step 2: Validate Schema

Load `schema.yaml` for the problem doc-type.
Validate required fields:
- `doc_type` = "problem"
- `title` (1-200 chars)
- `tags` (at least 1 tag)

Validate optional fields:
- `severity`: must be one of ["low", "medium", "high", "critical"]
- `solution_status`: must be one of ["open", "investigating", "in_progress", "resolved", "wont_fix"]

**BLOCK if validation fails** - show specific schema violations to the user.

### Step 3: Write Documentation

1. Generate filename: `{sanitized-title}-{YYYYMMDD}.md`
   - Sanitize title: lowercase, replace spaces with hyphens, remove special chars
   - Example: `database-connection-timeout-20250125.md`

2. Create directory if needed:
   ```bash
   mkdir -p ./csharp-compounding-docs/problems/
   ```

3. Write file with YAML frontmatter + markdown body:
   ```markdown
   ---
   doc_type: problem
   title: "Database connection timeout in production"
   tags: ["database", "production", "timeout"]
   severity: high
   solution_status: resolved
   date: 2025-01-25
   ---

   # Database connection timeout in production

   ## Problem Description

   [Detailed description of the problem]

   ## Symptoms

   - Error message: "Timeout expired. The timeout period elapsed..."
   - Occurred during peak traffic hours
   - Connection pool exhausted

   ## Root Cause

   [Why this happened - identified cause]

   ## Solution

   [How it was fixed]

   ## Related Files

   - `src/Data/DbContext.cs`
   - `appsettings.Production.json`

   ## Lessons Learned

   [What we learned from this]
   ```

4. Use Sequential Thinking MCP when:
   - Multiple contributing factors to root cause
   - Complex interaction between components
   - Need to analyze trade-offs in solution approach

### Step 4: Post-Capture Options

After successfully writing the document:

```
✓ Problem documentation captured

File created: ./csharp-compounding-docs/problems/{filename}.md

What's next?
1. Continue workflow
2. Link related docs (use /cdocs:related)
3. View documentation
4. Capture another problem
```

Wait for user selection.

## Schema Reference

See `schema.yaml` in this directory for the complete problem document schema.

Required fields:
- `doc_type`: "problem"
- `title`: string (1-200 chars)
- `tags`: array of strings (min 1, max 50 chars each)

Optional fields:
- `root_cause`: string (max 2000 chars)
- `solution_status`: enum ["open", "investigating", "in_progress", "resolved", "wont_fix"]
- `severity`: enum ["low", "medium", "high", "critical"]
- `promotion_level`: enum ["standard", "promoted", "pinned"] (default: "standard")
- `links`: array of URIs
- `date`: date in YYYY-MM-DD format

## Examples

### Example 1: Resolved Bug

```markdown
---
doc_type: problem
title: "NullReferenceException in user authentication"
tags: ["authentication", "bug", "security"]
severity: high
solution_status: resolved
root_cause: "Missing null check before accessing user.Claims property"
date: 2025-01-20
---

# NullReferenceException in user authentication

## Problem Description

Users were unable to log in and received a 500 error during authentication.

## Symptoms

- Exception: `System.NullReferenceException: Object reference not set to an instance of an object`
- Stack trace pointed to `AuthenticationService.ValidateUser()`
- Only occurred for users without custom claims

## Root Cause

The authentication service assumed all users would have a Claims collection, but users created through the legacy import process had a null Claims property.

## Solution

Added null check and initialization:
```csharp
if (user.Claims == null)
{
    user.Claims = new List<Claim>();
}
```

## Related Files

- `src/Authentication/AuthenticationService.cs`
- `src/Models/User.cs`

## Lessons Learned

Always validate assumptions about object state, especially when dealing with data from multiple sources or legacy systems.
```

### Example 2: In-Progress Issue

```markdown
---
doc_type: problem
title: "Memory leak in background job processor"
tags: ["performance", "memory", "background-jobs"]
severity: critical
solution_status: investigating
date: 2025-01-25
---

# Memory leak in background job processor

## Problem Description

Server memory usage grows continuously when background job processor is running, eventually causing OutOfMemoryException.

## Symptoms

- Memory usage increases by ~100MB per hour
- GC does not reclaim memory
- Server crashes after 8-10 hours of operation
- Memory profiler shows increasing number of Job objects retained

## Root Cause

Still investigating. Current hypothesis: Event handlers not being unsubscribed, causing job instances to remain in memory.

## Attempted Solutions

- Tried explicit GC.Collect() calls - no effect
- Reviewed disposal patterns - all IDisposable properly implemented
- Currently analyzing memory dump with dotMemory

## Related Files

- `src/Jobs/BackgroundJobProcessor.cs`
- `src/Jobs/JobManager.cs`
```
