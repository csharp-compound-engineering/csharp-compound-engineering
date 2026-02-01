# Plan Creation Process

> This document defines the orchestration process for creating the implementation plan for the CSharp Compound Docs Plugin.

---

## Overview

The implementation plan is created through a distributed agent orchestration pattern where:

- **Main Context**: Maintains `PLAN.md` and coordinates sub-agents
- **Phase Sub-Agents**: Create individual phase files under `plan/`
- **Deep Research Agents**: Answer questions that block phase creation

---

## Related Documents

- **[SPEC-STRUCTURE.md](./SPEC-STRUCTURE.md)**: Index to spec file summaries and structural relationships. Each major spec area (doc-types, mcp-server, skills, etc.) has a detailed summary in `structure/*.md`. Use this to quickly understand what each spec file covers before spawning phase agents.

---

## Directory Structure

```
csharp-compound-engineering/
├── PLAN.md                          # Main plan entry point (main context owns)
├── PLAN-CREATION-PROCESS.md         # This file
├── SPEC-STRUCTURE.md                # Spec file summaries and relationships
├── plan/                            # Phase files (sub-agents create)
│   ├── phase-001-*.md
│   ├── phase-002-*.md
│   ├── ...
│   └── phase-150-*.md (minimum)
└── .plan-temp/                      # Temporary files (auto-cleaned)
    ├── context-{uuid}.md            # Sub-agent context saves
    └── answer-{uuid}.md             # Research agent answers
```

---

## Agent Responsibilities

### Main Context

**Owns**: `PLAN.md`

**Responsibilities**:
1. Spawn sub-agents for each phase
2. Collect brief phase summaries (1-2 sentences max)
3. Update `PLAN.md` with phase entries and backreferences
4. Handle question escalation workflow
5. Track overall progress

**Does NOT**:
- Read full phase file contents
- Process detailed research findings
- Maintain phase implementation details in context

### Phase Sub-Agents (general-purpose)

**Owns**: Individual `plan/phase-{NNN}-*.md` files

**Responsibilities**:
1. Read relevant sections of SPEC.md and spec/*.md files
2. Create comprehensive phase documentation with:
   - Objectives
   - Prerequisites (dependencies on other phases)
   - Backreferences to spec sections
   - Acceptance criteria
   - Implementation notes
3. Follow the 500-line limit rule (decompose if needed)
4. Return ONLY a brief summary to main context

**On Blocking Questions**:
1. Save full context to `.plan-temp/context-{uuid}.md`
2. Return to main context:
   - The specific question
   - Context file path
3. STOP and wait for resumption

**On Resumption**:
1. Load context from provided context file
2. Load answer from provided answer file
3. Continue phase creation
4. Clean up both temp files
5. Validate cleanup succeeded
6. Return brief summary to main context

### Deep Research Agents

**Owns**: Answer files in `.plan-temp/answer-{uuid}.md`

**Responsibilities**:
1. Research the specific question
2. Use web search, Context7, and other tools as needed
3. Save comprehensive answer to answer file
4. Return ONLY the answer file path to main context

**Does NOT**:
- Return research content to main context
- Interact with phase files directly

---

## Workflow Diagrams

### Normal Phase Creation

```
Main Context                    Phase Sub-Agent
     │                                │
     │──spawn(phase N)───────────────▶│
     │                                │
     │                                │──read SPEC.md sections
     │                                │──create plan/phase-N-*.md
     │                                │
     │◀───"Phase N: Brief summary"────│
     │                                │
     │──update PLAN.md                │
     │                                ▼
```

### Question Escalation Workflow

```
Main Context          Phase Sub-Agent          Deep Research Agent
     │                      │                          │
     │──spawn(phase N)─────▶│                          │
     │                      │                          │
     │                      │──encounters question     │
     │                      │──save context to file    │
     │                      │                          │
     │◀──question + path────│                          │
     │                      │ (STOPPED)                │
     │                      │                          │
     │──spawn research─────────────────────────────────▶│
     │                                                  │
     │                                                  │──research
     │                                                  │──save answer
     │                                                  │
     │◀─────────────────answer file path────────────────│
     │                                                  │
     │──resume(context, answer)▶│                       │
     │                          │                       │
     │                          │──load context         │
     │                          │──load answer          │
     │                          │──continue creation    │
     │                          │──cleanup temp files   │
     │                          │──validate cleanup     │
     │                          │                       │
     │◀──"Phase N: summary"─────│                       │
     │                          │                       │
     │──update PLAN.md          ▼                       ▼
```

---

## Context File Format

When a sub-agent saves context, the file must include:

```markdown
# Sub-Agent Context Save

## Phase Information
- Phase Number: {NNN}
- Phase Name: {descriptive name}
- Target File: plan/phase-{NNN}-{slug}.md

## Progress State
- Current Step: {what was being done}
- Completed Items: {list of completed work}
- Pending Items: {list of remaining work}

## Blocking Question
{The specific question that blocked progress}

## Relevant Spec References
{List of spec sections consulted}

## Partial Work
{Any content already generated for the phase file}

## Resume Instructions
{Specific instructions for the resuming agent}
```

---

## Answer File Format

```markdown
# Research Answer

## Original Question
{The question that was asked}

## Answer Summary
{1-2 sentence summary}

## Detailed Answer
{Comprehensive research findings}

## Sources
{List of sources consulted}

## Recommendations
{Actionable recommendations for the phase}
```

---

## Phase File Structure

Each phase file follows this template:

```markdown
# Phase {NNN}: {Phase Name}

> **Status**: [PLANNED]
> **Estimated Effort**: {S/M/L/XL}
> **Prerequisites**: Phase {X}, Phase {Y}

---

## Spec References

- [SPEC.md Section](../SPEC.md#section-anchor)
- [spec/subtopic.md](../spec/subtopic.md#section-anchor)

---

## Objectives

1. {Objective 1}
2. {Objective 2}

---

## Acceptance Criteria

- [ ] {Criterion 1}
- [ ] {Criterion 2}

---

## Implementation Notes

{Detailed implementation guidance}

---

## Dependencies

### Depends On
- Phase {X}: {reason}

### Blocks
- Phase {Y}: {reason}

---

## Sub-Tasks

If this phase exceeds 500 lines, decompose into:
- `plan/phase-{NNN}/task-001-*.md`
- `plan/phase-{NNN}/task-002-*.md`
```

---

## PLAN.md Structure

```markdown
# Implementation Plan: CSharp Compound Docs Plugin

> **Total Phases**: {count}
> **Spec Version**: 0.1.0-draft

---

## Phase Index

| Phase | Name | Status | Dependencies | Spec Reference |
|-------|------|--------|--------------|----------------|
| 001 | {name} | [PLANNED] | - | [link](./plan/phase-001-*.md) |
| 002 | {name} | [PLANNED] | 001 | [link](./plan/phase-002-*.md) |
...

---

## Phase Groups

### Infrastructure Setup (Phases 001-020)
{brief description}

### MCP Server Core (Phases 021-050)
{brief description}

...
```

---

## Rules and Constraints

### Information Flow Rules

1. **Main context receives ONLY**:
   - Phase number and brief summary (1-2 sentences)
   - Question text and context file path (on blocking)
   - Answer file path (from research agent)

2. **Main context NEVER receives**:
   - Full phase file contents
   - Detailed research findings
   - Implementation specifics

3. **Temp files MUST be cleaned up**:
   - By the resuming sub-agent
   - Before returning summary to main context
   - With validation that cleanup succeeded

### Phase Numbering

- Three-digit zero-padded: `001`, `002`, ..., `150`
- Minimum 150 phases required
- Sequential, no gaps

### Backreference Format

All backreferences use relative markdown links:
```markdown
See [SPEC.md - Section Name](../SPEC.md#section-anchor)
See [spec/subtopic.md - Section](../spec/subtopic.md#section-anchor)
```

### 500-Line Rule

- Phase files must not exceed 500 lines
- If exceeded, decompose into `plan/phase-{NNN}/` subdirectory
- Create index file linking to sub-task files

---

## Error Handling

### Sub-Agent Failure

If a sub-agent fails without returning:
1. Main context notes the phase as [FAILED]
2. Re-spawn with fresh agent
3. If repeated failure, flag for manual intervention

### Cleanup Validation Failure

If temp file cleanup fails:
1. Sub-agent reports cleanup failure in summary
2. Main context logs warning
3. Manual cleanup may be required later

### Research Timeout

If research agent takes too long:
1. Main context can spawn alternative research approach
2. Or mark phase as [BLOCKED] and continue with other phases

---

## Execution Command

Main context initiates the process with:

```
"Begin phase creation starting with Phase 001"
```

Each phase spawn includes:
- Phase number
- Phase category (from spec analysis)
- Relevant spec section references
- Any known dependencies

---

## Progress Tracking

Main context maintains mental model of:
- Current phase being processed
- Phases completed
- Phases blocked/pending research
- Overall completion percentage

This is NOT stored in files - main context tracks in conversation.
