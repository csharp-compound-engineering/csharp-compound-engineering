# Phase 086: /cdocs:insight Capture Skill

> **Status**: NOT_STARTED
> **Effort Estimate**: 4-6 hours
> **Category**: Skills System
> **Prerequisites**: Phase 081 (Skills Infrastructure), Phase 082 (Skill Auto-Invocation), Phase 083 (Capture Skill Base)

---

## Spec References

This phase implements the `/cdocs:insight` capture skill as defined in:

- **spec/skills/capture-skills.md** - [/cdocs:insight Specification](../spec/skills/capture-skills.md#cdocsinsight) - Trigger phrases, behavior, MCP integration
- **spec/doc-types/built-in-types.md** - [Product/Project Insights Schema](../spec/doc-types/built-in-types.md#2-productproject-insights-insight) - Complete schema for insight doc-type
- **research/claude-code-skills-research.md** - Skill structure, auto-invoke mechanisms, YAML frontmatter
- **research/building-claude-code-skills-research.md** - Skill development patterns and best practices

---

## Objectives

1. Create the `/cdocs:insight` skill SKILL.md file with proper YAML frontmatter
2. Define trigger phrases for product/project insight detection
3. Implement the insight doc-type schema.yaml file
4. Configure output directory as `./csharp-compounding-docs/insights/`
5. Support insight type classification (business_logic, user_behavior, domain_knowledge, etc.)
6. Integrate Sequential Thinking MCP for connecting insights to broader context
7. Implement significance assessment for captured insights

---

## Acceptance Criteria

### SKILL.md Content
- [ ] SKILL.md file created at `.claude/skills/cdocs-insight/SKILL.md`
- [ ] YAML frontmatter includes:
  - [ ] `name: cdocs:insight`
  - [ ] `description` - Clear description for Claude's semantic matching
  - [ ] `auto_invoke: true` - Enable automatic triggering
  - [ ] `trigger_phrases` - Array of insight indicator phrases
- [ ] Skill instructions cover:
  - [ ] When to capture vs. when not to capture
  - [ ] How to extract insight from conversation context
  - [ ] Classification guidance for insight_type
  - [ ] Confidence assessment criteria
  - [ ] Impact area identification

### Trigger Phrases Configuration
- [ ] Primary trigger phrases from spec:
  - [ ] "users want"
  - [ ] "users prefer"
  - [ ] "interesting that"
  - [ ] "makes sense because"
  - [ ] "the reason is"
  - [ ] "apparently"
  - [ ] "learned that"
  - [ ] "realized"
- [ ] Classification hints for context matching:
  - [ ] "business context"
  - [ ] "user behavior"
  - [ ] "product"
  - [ ] "feature"
  - [ ] "customer"
  - [ ] "domain"
  - [ ] "market"
  - [ ] "requirement"
  - [ ] "stakeholder"

### Schema Definition (schema.yaml)
- [ ] Schema file created at `.claude/skills/cdocs-insight/schema.yaml`
- [ ] Schema name: `insight`
- [ ] Schema description: "Product, business, or domain learnings"
- [ ] Required fields defined:
  - [ ] `insight_type` (enum): `business_logic`, `user_behavior`, `domain_knowledge`, `feature_interaction`, `market_observation`
  - [ ] `impact_area` (string): Which part of product this affects
- [ ] Optional fields defined:
  - [ ] `confidence` (enum): `verified`, `hypothesis`, `observation`
  - [ ] `source` (string): How this insight was discovered
- [ ] Common required fields inherited (doc_type, title, date, summary, significance)

### Output Directory Configuration
- [ ] Default output path: `./csharp-compounding-docs/insights/`
- [ ] Filename pattern: `{sanitized-title}-{YYYYMMDD}.md`
- [ ] Directory auto-creation if not exists
- [ ] Path configurable via project config override

### MCP Integration
- [ ] Sequential Thinking integration for:
  - [ ] Connecting insights to broader product/business context
  - [ ] Identifying related features or business areas
  - [ ] Assessing potential impact and implications
- [ ] Skill assumes MCP servers are available (SessionStart verified)
- [ ] No defensive checks for MCP availability in skill

### Decision Menu Integration
- [ ] Confirmation prompt after insight extraction
- [ ] Menu options:
  - [ ] Confirm and save
  - [ ] Edit before saving
  - [ ] Discard
  - [ ] Change doc-type (if misclassified)

---

## Implementation Notes

### SKILL.md Structure

Create the skill file at `.claude/skills/cdocs-insight/SKILL.md`:

```markdown
---
name: cdocs:insight
description: |
  Capture product, business, or domain insights when the user discusses learnings about
  user behavior, business logic, market observations, or domain knowledge. Auto-invokes
  when conversation contains insight indicators like "users want", "learned that",
  "interesting that", or discovery language about product/business understanding.
auto_invoke: true
trigger_phrases:
  - "users want"
  - "users prefer"
  - "interesting that"
  - "makes sense because"
  - "the reason is"
  - "apparently"
  - "learned that"
  - "realized"
  - "turns out"
  - "discovered that"
  - "now I understand"
classification_hints:
  - "business context"
  - "user behavior"
  - "product"
  - "feature"
  - "customer"
  - "domain"
  - "market"
  - "requirement"
  - "stakeholder"
---

# /cdocs:insight - Product/Project Insight Capture

## Purpose

Capture significant learnings about the product being built, including business logic
understanding, user behavior patterns, domain knowledge, feature interactions, and
market observations.

## When to Capture

### DO Capture
- User behavior patterns discovered through feedback or analytics
- Business logic that was non-obvious or required clarification
- Domain knowledge that took time to understand
- Feature interactions that weren't initially apparent
- Market observations that inform product decisions
- Stakeholder requirements with important context

### DO NOT Capture
- Trivial observations easily found in documentation
- Temporary assumptions awaiting validation
- Personal opinions not backed by evidence
- Information that will be obsolete within weeks

## Extraction Process

1. **Identify the Insight**
   - What was learned or discovered?
   - What makes this non-obvious or valuable?

2. **Classify the Insight Type**
   - `business_logic` - Rules, constraints, or workflows in the business domain
   - `user_behavior` - How users actually interact vs. expected behavior
   - `domain_knowledge` - Industry or domain-specific understanding
   - `feature_interaction` - How features affect or depend on each other
   - `market_observation` - Competitive or market-related learnings

3. **Assess Confidence Level**
   - `verified` - Confirmed through data, testing, or authoritative source
   - `hypothesis` - Strong evidence but not fully validated
   - `observation` - Initial observation requiring further validation

4. **Identify Impact Area**
   - Which product area, feature, or module does this affect?
   - Consider both direct and indirect impacts

5. **Determine Significance**
   - `architectural` - Affects system design decisions
   - `behavioral` - Affects how features should work
   - `performance` - Affects performance requirements
   - `correctness` - Affects what is considered correct behavior
   - `convention` - Affects team conventions or practices
   - `integration` - Affects integrations or external dependencies

## MCP Integration

Use Sequential Thinking to:
- Connect the insight to broader product/business context
- Identify related features or business areas that might be affected
- Assess potential downstream implications
- Determine if this insight supersedes or modifies existing documentation

## Output Format

Generate document at: `./csharp-compounding-docs/insights/{sanitized-title}-{YYYYMMDD}.md`

```yaml
---
doc_type: insight
title: "{Descriptive title}"
date: {YYYY-MM-DD}
summary: "{One-line summary for search results}"
significance: {architectural|behavioral|performance|correctness|convention|integration}
insight_type: {business_logic|user_behavior|domain_knowledge|feature_interaction|market_observation}
impact_area: "{Affected product area}"
confidence: {verified|hypothesis|observation}
source: "{How this was discovered}"
tags:
  - {relevant-tag}
---

# {Title}

## Insight

{Detailed description of what was learned}

## Context

{Background on how this insight emerged}

## Implications

{What this means for the product/project}

## Related Areas

{Other features or areas this might affect}
```

## Confirmation

After extraction, present a decision menu:
1. **Save** - Write the insight document
2. **Edit** - Modify before saving
3. **Discard** - Do not capture
4. **Reclassify** - Change doc-type if this should be a different category
```

### Schema File (schema.yaml)

Create at `.claude/skills/cdocs-insight/schema.yaml`:

```yaml
# insight.schema.yaml
# Schema for product/project insight documents
$schema: "https://json-schema.org/draft/2020-12/schema"
title: "Insight Document Schema"
description: "Product, business, or domain learnings"
type: object

required:
  - doc_type
  - title
  - date
  - summary
  - significance
  - insight_type
  - impact_area

properties:
  doc_type:
    type: string
    const: "insight"
    description: "Document type identifier"

  title:
    type: string
    minLength: 1
    description: "Descriptive title for the insight"

  date:
    type: string
    pattern: '^\d{4}-\d{2}-\d{2}$'
    description: "Date captured in YYYY-MM-DD format"

  summary:
    type: string
    minLength: 1
    maxLength: 200
    description: "One-line summary for search results"

  significance:
    type: string
    enum:
      - architectural
      - behavioral
      - performance
      - correctness
      - convention
      - integration
    description: "How significant is this insight"

  insight_type:
    type: string
    enum:
      - business_logic
      - user_behavior
      - domain_knowledge
      - feature_interaction
      - market_observation
    description: "Category of insight"

  impact_area:
    type: string
    minLength: 1
    description: "Which part of product this affects"

  confidence:
    type: string
    enum:
      - verified
      - hypothesis
      - observation
    default: observation
    description: "Confidence level in this insight"

  source:
    type: string
    description: "How this insight was discovered"

  tags:
    type: array
    items:
      type: string
    description: "Searchable tags"

  related_docs:
    type: array
    items:
      type: string
    description: "Paths to related documents"

  supersedes:
    type: string
    description: "Path to document this supersedes"

  promotion_level:
    type: string
    enum:
      - standard
      - important
      - critical
    default: standard
    description: "Boost level for RAG retrieval"

# Trigger configuration for auto-invoke
trigger_phrases:
  - "users want"
  - "users prefer"
  - "interesting that"
  - "makes sense because"
  - "the reason is"
  - "apparently"
  - "learned that"
  - "realized"

classification_hints:
  - "business context"
  - "user behavior"
  - "product"
  - "feature"
  - "customer"
  - "domain"
  - "market"
  - "requirement"
  - "stakeholder"
```

### Directory Structure

After implementation:

```
.claude/
└── skills/
    └── cdocs-insight/
        ├── SKILL.md           # Skill definition and instructions
        └── schema.yaml        # Document schema for validation

csharp-compounding-docs/
└── insights/                  # Output directory (created on first use)
    └── .gitkeep
```

### Insight Type Classification Guide

Provide clear guidance for classification:

| Type | Description | Example |
|------|-------------|---------|
| `business_logic` | Rules, constraints, workflows | "Invoices must be approved before payment if > $10K" |
| `user_behavior` | How users actually behave | "Users prefer keyboard shortcuts over menu navigation" |
| `domain_knowledge` | Industry-specific understanding | "HIPAA requires audit logs for all PHI access" |
| `feature_interaction` | Feature dependencies | "Enabling dark mode affects chart color schemes" |
| `market_observation` | Competitive/market learnings | "Competitors all support OAuth 2.0, users expect it" |

### Significance Assessment

Map insight types to typical significance levels:

| Insight Type | Typical Significance |
|--------------|---------------------|
| `business_logic` | `behavioral`, `correctness` |
| `user_behavior` | `behavioral`, `convention` |
| `domain_knowledge` | `correctness`, `integration` |
| `feature_interaction` | `architectural`, `behavioral` |
| `market_observation` | `architectural`, `integration` |

---

## File Structure

After implementation, the following files should exist:

```
.claude/skills/cdocs-insight/
├── SKILL.md                    # Skill definition with YAML frontmatter
└── schema.yaml                 # JSON Schema for insight doc-type

src/CompoundDocs.Skills/
├── InsightCaptureSkill/
│   ├── InsightCaptureHandler.cs
│   ├── InsightExtractor.cs
│   └── InsightClassifier.cs
└── Models/
    └── InsightDocument.cs

tests/CompoundDocs.Tests/
└── Skills/
    └── InsightCaptureSkillTests/
        ├── TriggerDetectionTests.cs
        ├── InsightExtractionTests.cs
        ├── InsightClassificationTests.cs
        └── SchemaValidationTests.cs
```

---

## Dependencies

### Depends On
- **Phase 081**: Skills Infrastructure - Base skill framework and registration
- **Phase 082**: Skill Auto-Invocation - Trigger phrase detection mechanism
- **Phase 083**: Capture Skill Base - Common capture skill patterns and utilities
- **Phase 060**: Frontmatter Schema Validation - Schema validation for insight documents
- **Phase 059**: Frontmatter Parsing - YAML frontmatter extraction

### Blocks
- Multi-trigger conflict resolution (when insight triggers alongside other capture skills)
- Insight-specific RAG queries
- Insight promotion workflows

---

## Verification Steps

After completing this phase, verify:

1. **Skill File Structure**
   - SKILL.md exists at `.claude/skills/cdocs-insight/SKILL.md`
   - schema.yaml exists at `.claude/skills/cdocs-insight/schema.yaml`
   - YAML frontmatter is valid and parseable
   - All trigger phrases are present

2. **Trigger Detection**
   - Phrase "users want feature X" triggers skill
   - Phrase "I learned that" triggers skill
   - Phrase "interesting that users prefer" triggers skill
   - Non-insight phrases do not trigger (e.g., "fix the bug")

3. **Schema Validation**
   - Valid insight document passes validation
   - Missing `insight_type` fails validation
   - Invalid `confidence` value fails validation
   - Invalid `insight_type` value fails validation
   - Missing `impact_area` fails validation

4. **Document Generation**
   - Generated documents saved to `./csharp-compounding-docs/insights/`
   - Filename follows pattern `{title}-{date}.md`
   - Generated frontmatter includes all required fields
   - Document body follows expected structure

5. **MCP Integration**
   - Sequential Thinking called for context analysis
   - Insight connected to broader product context
   - Impact assessment generated

---

## Testing Notes

### Test Scenarios

```csharp
// TriggerDetectionTests.cs
[Theory]
[InlineData("I learned that users prefer dark mode", true)]
[InlineData("Users want better search functionality", true)]
[InlineData("Interesting that the cache invalidates on weekends", true)]
[InlineData("Fix the null reference exception", false)]
[InlineData("Deploy to production", false)]
public void DetectsTriggerPhrases(string conversation, bool shouldTrigger)

// InsightClassificationTests.cs
[Theory]
[InlineData("Users click the back button instead of save", "user_behavior")]
[InlineData("Invoices require manager approval over $5000", "business_logic")]
[InlineData("GDPR requires data deletion within 30 days", "domain_knowledge")]
[InlineData("Dark mode changes all chart colors", "feature_interaction")]
[InlineData("All competitors support SSO", "market_observation")]
public void ClassifiesInsightTypes(string insight, string expectedType)

// SchemaValidationTests.cs
[Fact] public async Task ValidInsightDocument_PassesValidation()
[Fact] public async Task MissingInsightType_FailsValidation()
[Fact] public async Task InvalidConfidence_FailsValidation()
[Fact] public async Task MissingImpactArea_FailsValidation()
```

### Test Data

```yaml
# valid-insight.yaml
doc_type: insight
title: Users prefer keyboard navigation
date: 2025-01-24
summary: Power users strongly prefer keyboard shortcuts over mouse navigation
significance: behavioral
insight_type: user_behavior
impact_area: Navigation system
confidence: verified
source: User interviews and analytics data
tags:
  - ux
  - accessibility
  - navigation
```

---

## Notes

- **Confidence Levels**: Default to `observation` for newly captured insights; upgrade to `hypothesis` or `verified` as evidence accumulates
- **Impact Area**: Should be specific enough to be useful (e.g., "checkout flow" not "the product")
- **Source Documentation**: Encourage capturing how the insight was discovered for future reference
- **Cross-referencing**: Use `related_docs` to link insights to relevant problem, codebase, or style documents
- **Promotion**: High-confidence insights affecting multiple areas may warrant promotion to `important` or `critical`
