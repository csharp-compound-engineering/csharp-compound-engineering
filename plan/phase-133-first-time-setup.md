# Phase 133: First-Time Project Setup

> **Status**: NOT_STARTED
> **Effort Estimate**: 4-6 hours
> **Category**: Configuration System
> **Prerequisites**: Phase 010 (Project Configuration System), Phase 094 (/cdocs:activate Meta Skill)

---

## Spec References

This phase implements the first-time project initialization workflow defined in:

- **spec/configuration.md** - [Initialization > First-Time Project Setup](../spec/configuration.md#first-time-project-setup) - Core initialization workflow
- **spec/configuration.md** - [Project Directory Structure](../spec/configuration.md#project-directory-structure) - Folder structure requirements
- **Phase 010** - Project configuration models and services (provides `IProjectInitializationService`)
- **Phase 094** - /cdocs:activate skill (triggers initialization when config is missing)

---

## Objectives

1. Implement the interactive first-time project setup workflow
2. Create `.csharp-compounding-docs/` hidden directory with `config.json`
3. Create `csharp-compounding-docs/` visible directory with built-in doc-type folders
4. Generate default configuration with project name derived from folder
5. Provide user prompts for optional configuration customization
6. Integrate with `/cdocs:activate` skill for seamless onboarding

---

## Acceptance Criteria

### Directory Creation

- [ ] Hidden config directory `.csharp-compounding-docs/` created at project root
- [ ] Visible docs directory `csharp-compounding-docs/` created at project root
- [ ] Built-in folders created under `csharp-compounding-docs/`:
  - [ ] `problems/`
  - [ ] `insights/`
  - [ ] `codebase/`
  - [ ] `tools/`
  - [ ] `styles/`
  - [ ] `schemas/`
- [ ] All directories created with proper permissions (755 on Unix)
- [ ] Creation is idempotent (re-running doesn't error on existing directories)

### Configuration Generation

- [ ] `config.json` created in `.csharp-compounding-docs/`
- [ ] Project name derived from folder name following pattern `^[a-z][a-z0-9-]*$`
- [ ] Folder name transformations applied:
  - [ ] Converted to lowercase
  - [ ] Underscores replaced with hyphens
  - [ ] Spaces replaced with hyphens
  - [ ] Invalid characters removed
  - [ ] `project-` prefix added if name starts with non-letter
- [ ] Default settings applied:
  - [ ] `retrieval.min_relevance_score: 0.7`
  - [ ] `retrieval.max_results: 3`
  - [ ] `retrieval.max_linked_docs: 5`
  - [ ] `semantic_search.min_relevance_score: 0.5`
  - [ ] `semantic_search.default_limit: 10`
  - [ ] `link_resolution.max_depth: 2`
  - [ ] `custom_doc_types: []`
- [ ] JSON formatted with snake_case properties and indentation

### User Prompts (Optional Configuration)

- [ ] User prompted to confirm or modify derived project name
- [ ] User prompted to configure external docs path (optional)
- [ ] User prompted about .gitignore suggestion
- [ ] All prompts have sensible defaults for quick acceptance
- [ ] Non-interactive mode available (accepts all defaults)

### Integration with /cdocs:activate

- [ ] Initialization triggered automatically when `/cdocs:activate` runs on uninitialized project
- [ ] Clear messaging distinguishes initialization from activation
- [ ] After initialization, activation proceeds automatically
- [ ] User sees both initialization and activation status

### Git Integration

- [ ] .gitignore suggestion displayed if .gitignore exists but doesn't include config folder
- [ ] Suggestion is informational only (does not modify .gitignore automatically)
- [ ] Works correctly in non-Git directories (Git is optional)

### Error Handling

- [ ] Clear error if directory creation fails (permissions)
- [ ] Clear error if config.json write fails
- [ ] Rollback partial initialization on failure (optional, log warning if not possible)
- [ ] Cannot initialize inside existing `csharp-compounding-docs/` folder

---

## Implementation Notes

### Initialization Flow Diagram

```
/cdocs:activate called
        |
        v
Config exists? ----Yes----> Proceed with activation
        |
        No
        |
        v
Prompt: "No config found. Initialize compounding docs for this project?"
        |
        v
User confirms
        |
        v
Derive project name from folder
        |
        v
Prompt: "Project name: '{name}' - Accept? (Y/n/custom)"
        |
        v
Create .csharp-compounding-docs/
        |
        v
Generate config.json with defaults
        |
        v
Create csharp-compounding-docs/
        |
        v
Create built-in folders (problems/, insights/, etc.)
        |
        v
Suggest .gitignore addition
        |
        v
Display initialization success
        |
        v
Proceed with activation
```

### Skill Extension for /cdocs:activate

The `/cdocs:activate` skill (Phase 094) must be extended to handle the uninitialized case:

```markdown
## Extended Activation Steps

### 0. Check for Existing Configuration

Before Git detection, check if config exists:

```bash
# Check if config file exists
test -f .csharp-compounding-docs/config.json && echo "exists" || echo "missing"
```

If config is missing, proceed to initialization workflow.

### Initialization Workflow (if config missing)

#### 0.1 Confirm Initialization

Ask the user:
```
No compounding docs configuration found in this project.

Would you like to initialize CSharp Compounding Docs?
This will create:
- .csharp-compounding-docs/config.json (configuration)
- csharp-compounding-docs/ (documentation folder)

Initialize? (Y/n)
```

#### 0.2 Derive and Confirm Project Name

Get the current directory name:
```bash
basename "$(pwd)"
```

Transform to valid project name (lowercase, hyphens only, must start with letter).

Present to user:
```
Derived project name: "my-awesome-project"

Accept this name? (Y/n/custom)
- Press Enter or Y to accept
- Press N to enter a custom name
```

If custom:
```
Enter project name (lowercase, alphanumeric with hyphens, must start with letter):
```

Validate against pattern `^[a-z][a-z0-9-]*$`.

#### 0.3 Create Configuration Directory

```bash
mkdir -p .csharp-compounding-docs
```

#### 0.4 Generate Configuration File

Create `.csharp-compounding-docs/config.json`:

```json
{
  "project_name": "{derived_or_custom_name}",
  "retrieval": {
    "min_relevance_score": 0.7,
    "max_results": 3,
    "max_linked_docs": 5
  },
  "semantic_search": {
    "min_relevance_score": 0.5,
    "default_limit": 10
  },
  "link_resolution": {
    "max_depth": 2
  },
  "custom_doc_types": []
}
```

Use the Write tool to create the file with proper JSON formatting.

#### 0.5 Create Documentation Directory Structure

```bash
mkdir -p csharp-compounding-docs/problems
mkdir -p csharp-compounding-docs/insights
mkdir -p csharp-compounding-docs/codebase
mkdir -p csharp-compounding-docs/tools
mkdir -p csharp-compounding-docs/styles
mkdir -p csharp-compounding-docs/schemas
```

#### 0.6 Check and Suggest .gitignore

```bash
# Check if .gitignore exists and doesn't already have the config folder
if [ -f .gitignore ]; then
  grep -q ".csharp-compounding-docs" .gitignore || echo "suggest"
fi
```

If suggestion needed:
```
Consider adding to your .gitignore:

# Compounding docs config (optional - can be shared or private)
# .csharp-compounding-docs/

Note: The config folder contains project-specific settings.
- Include it in Git to share settings across team
- Exclude it if settings should be machine-specific
```

#### 0.7 Report Initialization Success

```
CSharp Compounding Docs initialized!

Created:
- .csharp-compounding-docs/config.json
- csharp-compounding-docs/problems/
- csharp-compounding-docs/insights/
- csharp-compounding-docs/codebase/
- csharp-compounding-docs/tools/
- csharp-compounding-docs/styles/
- csharp-compounding-docs/schemas/

Project name: {project_name}

Proceeding with activation...
```

Then continue with normal activation steps (Git detection, MCP tool call, etc.).
```

### Project Name Derivation Algorithm

```csharp
public static string DeriveProjectName(string folderPath)
{
    var folderName = new DirectoryInfo(folderPath).Name;

    // Step 1: Convert to lowercase
    var name = folderName.ToLowerInvariant();

    // Step 2: Replace underscores and spaces with hyphens
    name = name.Replace('_', '-').Replace(' ', '-');

    // Step 3: Remove invalid characters (keep only a-z, 0-9, -)
    name = new string(name.Where(c => char.IsLetterOrDigit(c) || c == '-').ToArray());

    // Step 4: Collapse multiple hyphens
    while (name.Contains("--"))
    {
        name = name.Replace("--", "-");
    }

    // Step 5: Trim leading/trailing hyphens
    name = name.Trim('-');

    // Step 6: Ensure starts with letter
    if (string.IsNullOrEmpty(name) || !char.IsLetter(name[0]))
    {
        name = "project-" + name;
    }

    // Step 7: Ensure not empty
    if (string.IsNullOrEmpty(name))
    {
        name = "project";
    }

    return name;
}
```

### Example Transformations

| Folder Name | Derived Project Name |
|-------------|---------------------|
| `MyAwesomeApp` | `myawesomeapp` |
| `my_project` | `my-project` |
| `My Project Name` | `my-project-name` |
| `123-numbers-first` | `project-123-numbers-first` |
| `!!!invalid!!!` | `project` |
| `CSharp.Compound.Engineering` | `csharpcompoundengineering` |
| `test__double` | `test-double` |

### Default Config.json Template

```json
{
  "project_name": "${PROJECT_NAME}",
  "retrieval": {
    "min_relevance_score": 0.7,
    "max_results": 3,
    "max_linked_docs": 5
  },
  "semantic_search": {
    "min_relevance_score": 0.5,
    "default_limit": 10
  },
  "link_resolution": {
    "max_depth": 2
  },
  "custom_doc_types": []
}
```

### Directory Structure After Initialization

```
my-project/
├── .csharp-compounding-docs/           # Hidden - configuration
│   └── config.json                     # Project settings
├── csharp-compounding-docs/            # Visible - documentation
│   ├── problems/                       # Problem/solution docs
│   ├── insights/                       # Product insights
│   ├── codebase/                       # Architecture decisions
│   ├── tools/                          # Library/tool knowledge
│   ├── styles/                         # Coding conventions
│   └── schemas/                        # Custom doc-type schemas
└── ... (existing project files)
```

### Non-Interactive Mode

For automation scenarios, support a `--yes` or `--non-interactive` flag:

```
/cdocs:activate --yes
```

In non-interactive mode:
- Accept derived project name automatically
- Skip external docs configuration
- Skip .gitignore prompt
- Proceed directly with initialization and activation

---

## Error Handling

| Error Condition | User Message | Action |
|-----------------|--------------|--------|
| Permission denied creating directory | "Error: Cannot create directory. Check permissions for {path}" | Abort initialization |
| Permission denied writing config | "Error: Cannot write config.json. Check permissions for {path}" | Clean up created directories if possible |
| Invalid custom project name | "Invalid project name. Must match pattern: ^[a-z][a-z0-9-]*$" | Re-prompt for name |
| Already initialized | "Project already initialized. Running activation..." | Skip to activation |
| Inside csharp-compounding-docs folder | "Error: Cannot initialize from within documentation folder. Navigate to project root." | Abort |
| Disk full | "Error: Insufficient disk space to create directories" | Abort initialization |

### Cleanup on Failure

If initialization fails partway through:

1. Log warning about partial state
2. Remove `.csharp-compounding-docs/` if empty
3. Do NOT remove `csharp-compounding-docs/` if it contains any files (could be user data)
4. Advise user to manually clean up if needed

---

## Dependencies

### Depends On

- **Phase 010**: Project Configuration System (`IProjectInitializationService`, `ProjectConfig` models)
- **Phase 094**: /cdocs:activate skill (integration point for initialization trigger)
- **Phase 081**: Skills System Base Infrastructure (skill execution context)
- **Phase 009**: Plugin Directory Structure (skill file locations)

### Blocks

- **Phase 095+**: All capture skills require initialized project
- **Phase 096+**: All query skills require initialized project
- Custom doc-type creation requires initialized project

---

## Verification Steps

### Manual Verification

```bash
# 1. Navigate to a project without compounding docs
cd /path/to/new-project
ls -la .csharp-compounding-docs/  # Should not exist

# 2. Invoke activate skill (should trigger initialization)
# In Claude Code session:
/cdocs:activate

# 3. Follow prompts to initialize

# 4. Verify directories created
ls -la .csharp-compounding-docs/
# Expected: config.json

ls -la csharp-compounding-docs/
# Expected: problems/ insights/ codebase/ tools/ styles/ schemas/

# 5. Verify config.json content
cat .csharp-compounding-docs/config.json
# Expected: Valid JSON with project_name and defaults

# 6. Re-run activate (should skip initialization)
/cdocs:activate
# Expected: "Project already initialized. Running activation..."

# 7. Test custom project name
cd /tmp/test-init
mkdir "My Test Project"
cd "My Test Project"
/cdocs:activate
# When prompted, enter custom name: "custom-name"
cat .csharp-compounding-docs/config.json | grep project_name
# Expected: "custom-name"
```

### Integration Test Scenarios

| Scenario | Expected Behavior |
|----------|-------------------|
| Fresh project, accept defaults | Initialization completes, activation follows |
| Fresh project, custom name | Custom name validated and used |
| Fresh project, invalid custom name | Re-prompted until valid |
| Already initialized project | Skips initialization, proceeds to activation |
| Non-interactive mode | All defaults accepted automatically |
| Permission denied | Clear error, no partial state |
| Folder with spaces | Derived name handles spaces correctly |
| Folder starting with number | `project-` prefix added |

---

## Files to Create/Modify

### Modified Files

| File | Changes |
|------|---------|
| `plugins/csharp-compounding-docs/skills/cdocs-activate/SKILL.md` | Add initialization workflow before activation steps |

### Implementation in Core Library

The actual initialization logic exists in Phase 010's `IProjectInitializationService`. This phase focuses on:

1. The skill-level workflow that triggers initialization
2. User interaction patterns (prompts, confirmations)
3. Integration between initialization and activation

---

## Notes

- Initialization is designed to be discoverable - users typing `/cdocs:activate` on a new project get guided through setup
- The two-folder design (hidden config, visible docs) separates machine-specific settings from version-controlled documentation
- Project name derivation is conservative - it will always produce a valid name even from unusual folder names
- The .gitignore suggestion is advisory only - teams may want to share or not share config
- Non-interactive mode enables CI/CD and automation scenarios
- Initialization is idempotent for directories (mkdir -p) but will not overwrite existing config.json
- The workflow prioritizes quick onboarding while allowing customization for power users
