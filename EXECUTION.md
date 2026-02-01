# Execution Instructions: Spec Structure Generation

## Objective

Generate `SPEC-STRUCTURE.md` containing summaries of all spec files and their structural relationships, using background agents to avoid polluting main context with summary content.

## Critical Constraint

**Background agents return their final output text through `TaskOutput`.** If an agent writes a summary in its response, that summary comes back to main context. To prevent this:

- Do not read the entire task output. Instead only read the last line, which should contain the path to the summary.
- Instruct the sub-agents to write the summary path in the last line or, if they are step 4 agents, their last line should simply be `done`.

## Execution Steps

### Step 1: Setup

1. Read `SPEC-LISTING.md` to get the list of spec files
2. Create `SPEC-STRUCTURE.md` with header content and placeholder for summaries
3. Ensure `summaries/` directory exists

### Step 2: Generate Summaries (Parallel Background Agents)

For each file listed in `SPEC-LISTING.md`, spawn a background agent with this prompt template:

```
Read the file `spec/{filepath}`. Create a concise summary containing:
1. What the file covers
2. How it structurally relates to other spec files (parent, children, siblings)

Save this summary to `summaries/{filename}.md`.

CRITICAL: the last line of your output shoudl contain the path to the summary and nothing else.
```

Also create one for the root `SPEC.md` file.

### Step 3: Wait for Completion

Use `TaskOutput` with `block=true` for each agent. The returned value should be only a file path.

### Step 4: Append Summaries to SPEC-STRUCTURE.md (Sequential Background Agents)

For each summary file path returned, spawn a background agent with this prompt:

```
Read the summary file at `{summary_path}`.
Append its contents to `SPEC-STRUCTURE.md` under the "## File Summaries" section.
Format as a subsection with the original spec filename as heading.

CRITICAL: The last line of your output must simply be `done`.
```

Run these sequentially to avoid write conflicts.

### Step 5: Cleanup

1. Delete the `summaries/` directory and its contents
2. Add a back-reference to `SPEC-STRUCTURE.md` in `PLAN-CREATION-PROCESS.md`

## Files Involved

### Input
- `SPEC-LISTING.md` - List of all spec files
- `SPEC.md` - Root specification
- `spec/**/*.md` - All spec files (29 total)

### Output
- `SPEC-STRUCTURE.md` - Final document with all summaries
- `PLAN-CREATION-PROCESS.md` - Updated with back-reference

### Temporary
- `summaries/*.md` - Intermediate summary files (deleted at end)
