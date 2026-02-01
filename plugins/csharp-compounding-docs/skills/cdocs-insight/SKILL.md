---
name: cdocs:insight
description: Captures product and project insights from development work
allowed-tools:
  - Read
  - Write
  - Bash
preconditions:
  - Project activated via /cdocs:activate
  - Insight has been discovered or realized
auto-invoke:
  trigger: conversation-pattern
  patterns:
    - "users want"
    - "users prefer"
    - "interesting that"
    - "makes sense because"
    - "the reason is"
    - "apparently"
    - "learned that"
    - "realized"
---

# Insight Documentation Skill

## Intake

This skill captures insights, discoveries, and valuable observations about the product, users, or project. It expects the following context from the conversation:

- **Insight**: The key realization or discovery
- **Discovery context**: How or when this insight was discovered
- **Applications**: Where or how this insight can be applied
- **Impact area**: What part of the product/project this affects

## Process

### Step 1: Gather Context

Extract from conversation history:
- **Core insight**: What was learned or realized?
- **Discovery context**: Under what circumstances was this discovered?
- **Supporting evidence**: Data, user feedback, or observations that support this
- **Applications**: Potential use cases or areas where this applies
- **Impact assessment**: Which areas of the product/project are affected?

Use Sequential Thinking MCP to:
- Connect the insight to broader product/business context
- Identify implications and downstream effects
- Evaluate the insight's significance

**BLOCKING**: If the core insight is unclear, ask the user to clarify and WAIT for response.

### Step 2: Validate Schema

Load `schema.yaml` for the insight doc-type.
Validate required fields:
- `doc_type` = "insight"
- `title` (1-200 chars)
- `tags` (at least 1 tag)

Validate optional fields:
- `promotion_level`: must be one of ["standard", "promoted", "pinned"]

**BLOCK if validation fails** - show specific schema violations to the user.

### Step 3: Write Documentation

1. Generate filename: `{sanitized-title}-{YYYYMMDD}.md`
   - Sanitize title: lowercase, replace spaces with hyphens, remove special chars
   - Example: `users-prefer-inline-validation-20250125.md`

2. Create directory if needed:
   ```bash
   mkdir -p ./csharp-compounding-docs/insights/
   ```

3. Write file with YAML frontmatter + markdown body:
   ```markdown
   ---
   doc_type: insight
   title: "Users prefer inline validation over modal dialogs"
   tags: ["ux", "validation", "user-feedback"]
   category: user_behavior
   date: 2025-01-25
   discovery_context: "Observed during user testing sessions"
   ---

   # Users prefer inline validation over modal dialogs

   ## Insight

   [The key realization or discovery]

   ## Discovery Context

   [How or when this was discovered]

   ## Applications

   - [Where this can be applied]
   - [Potential use case]

   ## Impact

   [What parts of the product/project this affects]

   ## Related Considerations

   [Additional thoughts or implications]
   ```

4. Use Sequential Thinking MCP when:
   - Connecting insight to broader context
   - Evaluating multiple implications
   - Assessing business or technical impact

### Step 4: Post-Capture Options

After successfully writing the document:

```
✓ Insight documentation captured

File created: ./csharp-compounding-docs/insights/{filename}.md

What's next?
1. Continue workflow
2. Link related docs (use /cdocs:related)
3. View documentation
4. Capture another insight
```

Wait for user selection.

## Schema Reference

See `schema.yaml` in this directory for the complete insight document schema.

Required fields:
- `doc_type`: "insight"
- `title`: string (1-200 chars)
- `tags`: array of strings (min 1, max 50 chars each)

Optional fields:
- `discovery_context`: string (max 2000 chars) - context in which insight was discovered
- `applications`: array of strings (max 500 chars each) - potential use cases
- `promotion_level`: enum ["standard", "promoted", "pinned"] (default: "standard")
- `links`: array of URIs
- `date`: date in YYYY-MM-DD format
- `category`: string (max 100 chars)

## Examples

### Example 1: User Behavior Insight

```markdown
---
doc_type: insight
title: "Users skip onboarding tutorials when time-pressured"
tags: ["onboarding", "ux", "user-behavior"]
category: user_behavior
date: 2025-01-20
discovery_context: "Analytics showed 78% skip rate during business hours vs 23% during evenings"
---

# Users skip onboarding tutorials when time-pressured

## Insight

Users are much more likely to skip onboarding tutorials during business hours when they're under time pressure to complete a task, versus leisurely exploring during evenings or weekends.

## Discovery Context

Our analytics revealed a strong correlation between time of day and onboarding completion:
- Business hours (9am-5pm): 78% skip the tutorial
- Evenings/weekends: 23% skip the tutorial
- Users who skip often contact support later with basic questions

## Applications

- Make tutorials optional but easily accessible later
- Provide context-sensitive help at point of need
- Design "quick start" path for time-pressured users
- Consider async onboarding (email series, progressive disclosure)

## Impact

Affects:
- Onboarding flow design
- Help documentation strategy
- Support ticket volume
- User success metrics

## Related Considerations

Consider offering:
1. "Quick Start" mode that covers essentials in < 2 minutes
2. "Learn as you go" tooltips instead of upfront tutorial
3. Email series for users who skip tutorial
```

### Example 2: Technical Discovery

```markdown
---
doc_type: insight
title: "Batch processing reduces API costs by 60%"
tags: ["performance", "api", "optimization", "cost"]
category: technical
date: 2025-01-25
discovery_context: "Performance testing revealed significant savings when batching API requests"
applications:
  - "Apply to all external API integrations"
  - "Refactor notification service to use batching"
---

# Batch processing reduces API costs by 60%

## Insight

Batching API requests instead of making individual calls reduces our third-party API costs by approximately 60% while maintaining acceptable latency.

## Discovery Context

During performance optimization work, we tested batching requests to our email service provider. Single request approach cost $0.001 per email, but their batch API charges $0.0004 per email when sending 25+ emails at once.

Testing showed:
- Average batch size: 47 emails
- Average delay introduced: 2.3 seconds
- Cost reduction: 60%
- User impact: negligible (notifications were already async)

## Applications

- Email notifications (already implemented)
- SMS notifications (in progress)
- Analytics events
- Audit log writes
- File upload operations

## Impact

Projected savings:
- Email costs: ~$2,400/month → ~$960/month
- If applied to all APIs: estimated $8,000/month total savings

Engineering impact:
- Need to implement batching infrastructure
- Add configurable batch windows
- Handle partial failures gracefully
- Monitor batch sizes and delays

## Related Considerations

Trade-offs to monitor:
- Latency vs cost (what's acceptable delay?)
- Batch size vs failure impact (larger batches = more to retry)
- Memory usage (batching requires buffering)
```

### Example 3: Product Strategy Insight

```markdown
---
doc_type: insight
title: "Enterprise customers need audit trails for compliance"
tags: ["enterprise", "compliance", "audit", "feature-request"]
category: business_logic
date: 2025-01-22
discovery_context: "Lost 2 enterprise deals, both cited missing audit trail as blocker"
---

# Enterprise customers need audit trails for compliance

## Insight

Audit trail functionality is not a "nice to have" for enterprise customers - it's a compliance requirement and deal-blocker. Without it, we cannot compete in the enterprise market.

## Discovery Context

In Q4 2024, we lost two significant enterprise deals ($120K and $85K ARR) in final stages. Both cited lack of comprehensive audit trails as the primary reason, specifically:
- Cannot prove who accessed sensitive customer data
- Cannot demonstrate compliance with SOC 2 requirements
- Cannot meet financial services regulatory requirements

Competitor analysis shows all major enterprise competitors have this feature.

## Applications

Required capabilities:
- Log all data access (who, what, when)
- Track all data modifications with before/after values
- Immutable audit log (cannot be altered)
- Export capabilities for auditors
- Retention policy management
- Query interface for compliance team

## Impact

Business impact:
- Blocking ~$200K+ ARR in current pipeline
- Enterprise segment is target growth area
- May affect SOC 2 certification timeline

Technical impact:
- Major feature implementation (estimated 6-8 weeks)
- Database design considerations (volume, retention)
- Performance implications (all writes must be logged)
- Storage costs (long-term retention)

## Related Considerations

Decision needed:
- Build in-house vs use third-party audit solution?
- Priority relative to other enterprise features?
- Minimum viable audit trail for first enterprise deals?
```
