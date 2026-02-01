# Phase 096: /cdocs:capture-select Meta Skill

> **Status**: NOT_STARTED
> **Effort Estimate**: 6-8 hours
> **Category**: Skills System
> **Prerequisites**: Phase 084 (Skill Infrastructure)

---

## Spec References

This phase implements the `/cdocs:capture-select` meta skill defined in:

- **spec/skills/meta-skills.md** - [/cdocs:capture-select](../spec/skills/meta-skills.md#cdocscapture-select) - Multi-trigger conflict resolution
- **spec/skills.md** - [Architectural Difference from Original Plugin](../spec/skills.md#architectural-difference-from-original-plugin) - Distributed capture pattern rationale
- **spec/skills/skill-patterns.md** - [SKILL.md Template](../spec/skills/skill-patterns.md#skillmd-template) - Skill file structure

---

## Objectives

1. Create SKILL.md file for the `/cdocs:capture-select` meta skill
2. Implement multi-trigger detection logic in skill instructions
3. Design selection dialog presentation with matched triggers
4. Support multi-select capability (0, 1, or multiple selections)
5. Implement triggered skill invocation after user selection
6. Handle cancellation (Skip All) gracefully

---

## Acceptance Criteria

### SKILL.md File Structure

- [ ] Skill file created at `${CLAUDE_PLUGIN_ROOT}/skills/cdocs-capture-select/SKILL.md`
- [ ] YAML frontmatter with required fields:
  - [ ] `name: cdocs:capture-select`
  - [ ] `description: Present unified selection interface when multiple capture skills trigger simultaneously`
  - [ ] `allowed-tools` includes Read, Write for skill invocation
  - [ ] `preconditions` includes project activation
  - [ ] `auto-invoke` with `trigger: multi-trigger-detection`

### Multi-Trigger Detection

- [ ] Skill auto-invokes when 2+ capture skills trigger on same conversation
- [ ] Skill does NOT invoke when 0 or 1 skill triggers (normal flow)
- [ ] Detection is dynamic - no hardcoded list of conflicting skills
- [ ] Queries all registered capture skills to check trigger status
- [ ] Each skill reports matched trigger phrases without executing capture

### Selection Dialog Presentation

- [ ] Dialog displays all triggered doc-types with checkboxes
- [ ] Each option shows:
  - [ ] Doc-type name (e.g., "Problem")
  - [ ] Doc-type description (e.g., "Bug fix documentation")
  - [ ] Matched trigger phrases (e.g., `Triggers matched: "fixed", "bug"`)
- [ ] Actions available: `[Capture Selected]` and `[Skip All]`
- [ ] Dialog format matches spec example

### Multi-Select Capability

- [ ] User can select zero options (equivalent to Skip All)
- [ ] User can select exactly one option
- [ ] User can select multiple options
- [ ] Selection state tracked per dialog session
- [ ] Clear visual indication of selected items

### Triggered Skill Invocation

- [ ] For each selected doc-type, invoke corresponding capture skill
- [ ] Skills invoked sequentially (not parallel) to avoid conflicts
- [ ] Each skill captures independently (not merged)
- [ ] Context from conversation passed to each invoked skill
- [ ] Success/failure status reported for each capture

### Cancellation Handling

- [ ] "Skip All" button cancels without capturing
- [ ] Selecting zero items then "Capture Selected" also skips
- [ ] User informed that no documentation was captured
- [ ] Conversation continues normally after skip
- [ ] No orphaned state left from partial operation

### Manual Invocation Support

- [ ] Skill can be invoked manually via `/cdocs:capture-select`
- [ ] Manual invocation triggers same multi-trigger check
- [ ] If no multiple triggers detected, informs user
- [ ] Useful for forcing re-evaluation of trigger state

---

## Implementation Notes

### SKILL.md Content

```yaml
---
name: cdocs:capture-select
description: Present unified selection interface when 2+ capture skills trigger simultaneously. Allows user to select which doc-types to capture when conversation matches multiple skill triggers.
allowed-tools:
  - Read    # Access conversation context and skill metadata
  - Write   # Not typically needed but may write temp state
preconditions:
  - Project activated via /cdocs:activate
auto-invoke:
  trigger: multi-trigger-detection
  threshold: 2  # Minimum number of triggered skills to activate
---

# Capture Selection Meta Skill

## Purpose

This skill handles conflict resolution when multiple capture skills (e.g., `/cdocs:problem` and `/cdocs:tool`) trigger on the same conversation content. Instead of auto-capturing to both or arbitrarily choosing one, it presents a selection interface.

## Intake

This skill receives:
- List of triggered capture skills
- For each triggered skill:
  - Skill name (e.g., `cdocs:problem`)
  - Doc-type description
  - Matched trigger phrases from conversation

## Process

### Step 1: Verify Multi-Trigger State

Confirm that 2+ capture skills have triggered:

```
Triggered skills detected: {count}
- {skill1}: {matched phrases}
- {skill2}: {matched phrases}
```

If only 0-1 skills triggered, inform user and exit:
```
Multi-trigger resolution not needed. {0|1} skill(s) triggered.
```

### Step 2: Present Selection Dialog

Display the selection interface:

```
Multiple doc types detected for this content:

[ ] Problem - Bug fix documentation
    Triggers matched: "fixed", "bug"

[ ] Tool - Library/package knowledge
    Triggers matched: "NuGet", "package"

[Capture Selected] [Skip All]
```

**BLOCKING**: Wait for user selection before proceeding.

### Step 3: Process Selection

Based on user action:

**If "Skip All" or zero items selected:**
```
No documentation captured. Continuing conversation.
```

**If 1+ items selected:**
For each selected doc-type in order:
1. Invoke the corresponding capture skill (`/cdocs:{type}`)
2. Wait for capture to complete
3. Report status

```
Invoking capture for: Problem
... [problem skill execution] ...
Capture complete: ./csharp-compounding-docs/problems/nuget-auth-bug-20250124.md

Invoking capture for: Tool
... [tool skill execution] ...
Capture complete: ./csharp-compounding-docs/tools/nuget-credential-provider-20250124.md
```

### Step 4: Summary

Report final status:

```
Capture Selection Complete

Captured: 2 documents
- ./csharp-compounding-docs/problems/nuget-auth-bug-20250124.md
- ./csharp-compounding-docs/tools/nuget-credential-provider-20250124.md

Skipped: 0 doc-types
```

## Example Scenarios

### Scenario 1: Problem + Tool Overlap

Conversation contains: "I fixed a bug in the NuGet package authentication"

Triggers:
- `/cdocs:problem`: "fixed", "bug"
- `/cdocs:tool`: "NuGet", "package"

User selects both -> Two separate documents created.

### Scenario 2: Codebase + Style Overlap

Conversation contains: "We decided to always use the repository pattern"

Triggers:
- `/cdocs:codebase`: "decided to", "pattern"
- `/cdocs:style`: "always", "pattern"

User selects codebase only -> One document created.

### Scenario 3: Skip All

User encounters multi-trigger dialog but realizes the content isn't worth capturing.

User clicks "Skip All" -> No documents created, conversation continues.

## Edge Cases

### No Context Available

If conversation context cannot be retrieved:
```
Unable to retrieve conversation context for trigger analysis.
Please retry or invoke specific skills manually:
- /cdocs:problem
- /cdocs:tool
```

### Skill Invocation Failure

If a selected skill fails to capture:
```
Capture failed for: Problem
Error: Schema validation failed - missing required field 'root_cause'

Continuing with remaining selections...
```

### Custom Doc-Types

Custom doc-types created via `/cdocs:create-type` participate in multi-trigger detection identically to built-in types. The dialog displays them alongside built-in types.
```

### Skill Directory Structure

```
${CLAUDE_PLUGIN_ROOT}/skills/cdocs-capture-select/
├── SKILL.md              # Main skill definition (above)
└── references/
    └── conflict-examples.md  # Example multi-trigger scenarios
```

### Multi-Trigger Detection Logic

The detection mechanism works as follows:

1. **Trigger Collection Phase**:
   - When conversation content is analyzed, each capture skill evaluates its trigger phrases
   - Skills report `triggered: true/false` and `matched_phrases: [...]`
   - This evaluation does NOT execute the capture - just checks triggers

2. **Conflict Detection**:
   - If `triggered_count >= 2`, the meta-skill activates
   - If `triggered_count < 2`, normal flow continues

3. **Skill Query Pattern**:
   ```pseudocode
   function checkAllTriggers(conversation_context):
       triggered_skills = []
       for skill in getAllCaptureSkills():
           result = skill.evaluateTriggers(conversation_context)
           if result.triggered:
               triggered_skills.append({
                   name: skill.name,
                   description: skill.description,
                   matched_phrases: result.matched_phrases
               })
       return triggered_skills
   ```

### Selection State Model

```typescript
interface CaptureSelectionState {
  triggered_skills: Array<{
    skill_name: string;
    doc_type: string;
    description: string;
    matched_phrases: string[];
    selected: boolean;  // User toggle state
  }>;
  action: 'pending' | 'capture' | 'skip';
  capture_results: Array<{
    doc_type: string;
    success: boolean;
    file_path?: string;
    error?: string;
  }>;
}
```

### Sequential Invocation Rationale

Skills are invoked sequentially rather than in parallel because:
1. Each skill may prompt for additional context
2. Parallel invocation could create file write conflicts
3. User can see progress and potentially abort early
4. Simpler error handling and recovery

---

## Dependencies

### Depends On
- Phase 084: Skill Infrastructure (skill loading and invocation)
- Phase 085-089: Built-in Capture Skills (skills to invoke)
- Phase 074: list_doc_types MCP Tool (enumerate doc-types)
- Phase 010: Project Configuration (config loading)

### Blocks
- Phase 097+: Skills that depend on multi-trigger resolution

---

## Verification Steps

After completing this phase, verify:

1. **Skill Discovery**: `/cdocs:capture-select` appears in available skills
2. **Single Trigger**: Does NOT activate when only 1 skill triggers
3. **Multi-Trigger**: Activates when 2+ skills trigger on same conversation
4. **Selection Display**: Dialog shows all triggered skills with matched phrases
5. **Multi-Select**: User can select 0, 1, or multiple options
6. **Capture Execution**: Selected skills invoked in sequence
7. **Skip All**: No captures when skip selected
8. **Manual Invocation**: Works when user explicitly invokes

### Manual Verification

```bash
# Test multi-trigger scenario
# Conversation: "I fixed a bug in the NuGet package authentication"

# Expected: Selection dialog with Problem and Tool options
# Select both -> Two files created
# Select one -> One file created
# Skip All -> No files created
```

### Test Scenarios

```markdown
## Test 1: Basic Multi-Trigger

Conversation: "I fixed the authentication bug in our JWT library"

Expected triggers:
- problem: "fixed", "bug"
- tool: "library"

Expected: Selection dialog appears with both options.

## Test 2: No Multi-Trigger

Conversation: "The error was a null reference exception"

Expected triggers:
- problem: "error", "exception"

Expected: Problem skill invokes directly, no selection dialog.

## Test 3: Custom Type Participation

Setup: Custom doc-type "api-contract" exists with triggers ["API", "endpoint"]

Conversation: "I fixed the bug in our API endpoint"

Expected triggers:
- problem: "fixed", "bug"
- api-contract: "API", "endpoint"

Expected: Selection dialog includes both built-in and custom types.

## Test 4: Skip All

Conversation: "I fixed the NuGet package issue"

Expected triggers:
- problem: "fixed", "issue"
- tool: "NuGet", "package"

Action: User clicks "Skip All"

Expected: No files created, message confirms skip.

## Test 5: Partial Selection

Same as Test 4, but user selects only "problem"

Expected: Only problem doc created, tool skipped.

## Test 6: Manual Invocation

User invokes `/cdocs:capture-select` directly when no multi-trigger state exists.

Expected: Message indicating no multi-trigger detected.
```

---

## Files to Create

### New Files

| File | Purpose |
|------|---------|
| `skills/cdocs-capture-select/SKILL.md` | Main skill definition |
| `skills/cdocs-capture-select/references/conflict-examples.md` | Example scenarios |

---

## Notes

- This skill is both user-invocable and auto-invoked (hybrid pattern)
- The skill operates on the skill system itself (meta-skill)
- Detection is dynamic - no hardcoded skill list
- Custom doc-types participate identically to built-in types
- Sequential invocation ensures clean state between captures
- Cancellation leaves no orphaned state
- Each capture operates independently (no merged documents)
