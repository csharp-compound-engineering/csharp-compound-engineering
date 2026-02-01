---
name: cdocs:codebase
description: Captures architectural decisions, patterns, and codebase structure knowledge
allowed-tools:
  - Read
  - Write
  - Bash
preconditions:
  - Project activated via /cdocs:activate
  - Architectural decision has been made or pattern established
auto-invoke:
  trigger: conversation-pattern
  patterns:
    - "decided to"
    - "going with"
    - "settled on"
    - "our approach"
    - "the pattern is"
    - "architecture"
    - "structure"
---

# Codebase Documentation Skill

## Intake

This skill captures architectural decisions, code patterns, module interactions, and structural knowledge about the codebase. It expects the following context from the conversation:

- **Decision or pattern**: What architectural choice was made or what pattern is being used?
- **Rationale**: Why was this approach chosen?
- **Alternatives considered**: What other options were evaluated?
- **Trade-offs**: What are the pros and cons?
- **Scope**: Which parts of the codebase does this affect?

## Process

### Step 1: Gather Context

Extract from conversation history:
- **Core decision/pattern**: What architectural choice or pattern is being documented?
- **Component/module**: Which part of the system does this involve?
- **Rationale**: Why this approach?
- **Alternatives**: What else was considered?
- **Trade-offs**: Advantages and disadvantages
- **Dependencies**: What other components/libraries are involved?
- **Files involved**: Specific code locations

Use Sequential Thinking MCP to:
- Analyze trade-offs and alternatives
- Document the decision-making process
- Identify potential future implications
- Connect to related architectural decisions

**BLOCKING**: If the core decision or rationale is unclear, ask the user to clarify and WAIT for response.

### Step 2: Validate Schema

Load `schema.yaml` for the codebase doc-type.
Validate required fields:
- `doc_type` = "codebase"
- `title` (1-200 chars)
- `component` (1-200 chars) - the component or module being documented

Validate optional fields:
- `promotion_level`: must be one of ["standard", "promoted", "pinned"]

**BLOCK if validation fails** - show specific schema violations to the user.

### Step 3: Write Documentation

1. Generate filename: `{sanitized-title}-{YYYYMMDD}.md`
   - Sanitize title: lowercase, replace spaces with hyphens, remove special chars
   - Example: `repository-pattern-implementation-20250125.md`

2. Create directory if needed:
   ```bash
   mkdir -p ./csharp-compounding-docs/codebase/
   ```

3. Write file with YAML frontmatter + markdown body:
   ```markdown
   ---
   doc_type: codebase
   title: "Repository pattern implementation for data access"
   component: "Data Access Layer"
   tags: ["architecture", "repository", "data-access"]
   patterns: ["Repository Pattern", "Unit of Work"]
   module: "CompoundDocs.Data"
   date: 2025-01-25
   ---

   # Repository pattern implementation for data access

   ## Decision

   [What was decided or what pattern is being used]

   ## Rationale

   [Why this approach was chosen]

   ## Alternatives Considered

   [What other options were evaluated]

   ## Trade-offs

   ### Advantages
   - [Pro 1]
   - [Pro 2]

   ### Disadvantages
   - [Con 1]
   - [Con 2]

   ## Implementation Details

   [How this is implemented in the codebase]

   ## Dependencies

   [External libraries or internal modules this depends on]

   ## Files Involved

   - `src/path/to/file1.cs`
   - `src/path/to/file2.cs`

   ## Future Considerations

   [Potential impacts or evolution of this decision]
   ```

4. Use Sequential Thinking MCP when:
   - Analyzing complex trade-offs between alternatives
   - Documenting multi-layered architectural decisions
   - Connecting decision to broader system design
   - Evaluating long-term implications

### Step 4: Post-Capture Options

After successfully writing the document:

```
✓ Codebase documentation captured

File created: ./csharp-compounding-docs/codebase/{filename}.md

What's next?
1. Continue workflow
2. Link related docs (use /cdocs:related)
3. View documentation
4. Capture another architectural decision
```

Wait for user selection.

## Schema Reference

See `schema.yaml` in this directory for the complete codebase document schema.

Required fields:
- `doc_type`: "codebase"
- `title`: string (1-200 chars)
- `component`: string (1-200 chars) - the component or module being documented

Optional fields:
- `dependencies`: array of strings (max 200 chars each) - component dependencies
- `patterns`: array of strings (max 200 chars each) - design patterns used
- `promotion_level`: enum ["standard", "promoted", "pinned"] (default: "standard")
- `links`: array of URIs
- `tags`: array of strings (max 50 chars each)
- `module`: string (max 200 chars) - module path or namespace

## Examples

### Example 1: Architectural Decision

```markdown
---
doc_type: codebase
title: "CQRS pattern for command and query separation"
component: "Application Layer"
tags: ["architecture", "cqrs", "design-pattern"]
patterns: ["CQRS", "Mediator"]
module: "CompoundDocs.Application"
dependencies: ["MediatR", "FluentValidation"]
date: 2025-01-20
---

# CQRS pattern for command and query separation

## Decision

Implement CQRS (Command Query Responsibility Segregation) pattern using MediatR library to separate write operations (commands) from read operations (queries).

## Rationale

- **Clarity**: Clear separation between operations that modify state vs those that read data
- **Scalability**: Queries and commands can be optimized independently
- **Validation**: Commands can have dedicated validation logic
- **Testing**: Easier to test commands and queries in isolation
- **Future flexibility**: Opens door to event sourcing if needed

## Alternatives Considered

1. **Traditional service layer**
   - Simpler to understand initially
   - Less boilerplate code
   - But: Mixing read/write concerns, harder to optimize separately

2. **Event sourcing**
   - Ultimate flexibility and audit trail
   - But: Significant complexity, steep learning curve, overkill for current needs

3. **Direct repository access from controllers**
   - Minimal abstraction
   - But: No separation of concerns, validation scattered, hard to test

## Trade-offs

### Advantages
- Clear separation of concerns
- Independent optimization of reads/writes
- Explicit validation per command
- Better testability
- Preparation for future scaling needs

### Disadvantages
- More files to maintain (handler per command/query)
- Learning curve for team members new to CQRS
- Slight performance overhead from MediatR pipeline
- Can feel like over-engineering for simple CRUD

## Implementation Details

Structure:
```
Application/
├── Commands/
│   ├── CreateDocument/
│   │   ├── CreateDocumentCommand.cs
│   │   ├── CreateDocumentHandler.cs
│   │   └── CreateDocumentValidator.cs
│   └── UpdateDocument/
│       ├── UpdateDocumentCommand.cs
│       └── UpdateDocumentHandler.cs
└── Queries/
    ├── GetDocument/
    │   ├── GetDocumentQuery.cs
    │   └── GetDocumentHandler.cs
    └── SearchDocuments/
        ├── SearchDocumentsQuery.cs
        └── SearchDocumentsHandler.cs
```

Commands return void or simple success/failure results.
Queries return DTOs optimized for specific use cases.

## Dependencies

- **MediatR** (v12.2.0): Mediator pattern implementation
- **FluentValidation** (v11.9.0): Command validation

## Files Involved

- `src/Application/Commands/` - All command handlers
- `src/Application/Queries/` - All query handlers
- `src/Api/Controllers/` - Controllers use MediatR to dispatch
- `src/Application/DependencyInjection.cs` - MediatR registration

## Future Considerations

- If we need read scalability, could introduce separate read models
- Queries could target read replicas while commands go to primary DB
- Could add event sourcing layer underneath commands if audit requirements increase
- May introduce caching layer for frequently accessed queries
```

### Example 2: Code Pattern

```markdown
---
doc_type: codebase
title: "Result pattern for error handling instead of exceptions"
component: "Core Domain"
tags: ["error-handling", "functional", "pattern"]
patterns: ["Result Pattern", "Railway Oriented Programming"]
module: "CompoundDocs.Core"
date: 2025-01-22
---

# Result pattern for error handling instead of exceptions

## Decision

Use Result<T> pattern for handling expected errors in domain logic instead of throwing exceptions. Exceptions reserved only for truly exceptional (unexpected) conditions.

## Rationale

- **Performance**: Exceptions have significant overhead, especially in loops or hot paths
- **Explicit error handling**: Result<T> forces caller to handle errors, no silent failures
- **Type safety**: Errors are part of the method signature
- **Composability**: Can chain operations with fluent API (Map, Bind, etc.)
- **Better for domain logic**: Business rule violations are expected, not exceptional

## Alternatives Considered

1. **Traditional exception throwing**
   - Standard C# approach
   - But: Performance overhead, implicit error paths, easy to miss error cases

2. **Tuple return (bool success, T value, string error)**
   - Simple to implement
   - But: No type safety, easy to forget to check success flag

3. **Custom error codes enum**
   - Explicit error types
   - But: Hard to compose, awkward API, loses error details

## Trade-offs

### Advantages
- Better performance (no exception stack unwinding)
- Explicit error handling in type system
- Compose operations with Railway Oriented Programming
- Clear success/failure paths
- Errors carry context without exceptions

### Disadvantages
- Learning curve for team members unfamiliar with pattern
- More verbose than simple exception throwing
- Need custom Result<T> implementation or library
- Doesn't integrate well with async/await without careful design

## Implementation Details

Core Result type:
```csharp
public class Result<T>
{
    public bool IsSuccess { get; }
    public T Value { get; }
    public Error Error { get; }

    public Result<TNew> Map<TNew>(Func<T, TNew> mapper);
    public Result<TNew> Bind<TNew>(Func<T, Result<TNew>> binder);
    public T Match(Func<T, T> onSuccess, Func<Error, T> onFailure);
}
```

Usage example:
```csharp
public Result<Document> CreateDocument(CreateDocumentRequest request)
{
    return ValidateRequest(request)
        .Bind(req => CheckDuplicates(req))
        .Bind(req => CreateEntity(req))
        .Bind(doc => SaveToDatabase(doc));
}
```

## Dependencies

- Custom implementation in `Core/Shared/Result.cs`
- No external dependencies

## Files Involved

- `src/Core/Shared/Result.cs` - Result<T> implementation
- `src/Core/Shared/Error.cs` - Error type
- `src/Domain/Services/` - All domain services use Result<T>

## Future Considerations

- Consider using LanguageExt library for richer functional types
- May introduce Result<T, TError> for typed errors
- Could add implicit conversion operators for cleaner syntax
```

### Example 3: Module Interaction

```markdown
---
doc_type: codebase
title: "Event-driven communication between bounded contexts"
component: "Domain Events"
tags: ["events", "domain-driven-design", "messaging"]
patterns: ["Domain Events", "Publish-Subscribe"]
module: "CompoundDocs.Infrastructure"
dependencies: ["MassTransit", "RabbitMQ"]
date: 2025-01-25
---

# Event-driven communication between bounded contexts

## Decision

Use domain events with MassTransit + RabbitMQ for communication between bounded contexts rather than direct method calls or shared databases.

## Rationale

- **Loose coupling**: Bounded contexts can evolve independently
- **Scalability**: Async processing, natural backpressure handling
- **Resilience**: Message persistence, retry logic, dead letter queues
- **Audit trail**: Events provide natural history of what happened
- **Extensibility**: Easy to add new subscribers without modifying publishers

## Alternatives Considered

1. **Direct service-to-service HTTP calls**
   - Simpler to understand
   - But: Tight coupling, cascading failures, synchronous

2. **Shared database**
   - No messaging infrastructure needed
   - But: Tight coupling at data layer, bounded context boundaries violated

3. **Azure Service Bus**
   - Fully managed
   - But: Vendor lock-in, higher cost, overkill for current scale

## Trade-offs

### Advantages
- True decoupling between contexts
- Async processing improves user experience
- Built-in retry and error handling
- Easy to add new functionality (new subscribers)
- Natural audit log of domain events

### Disadvantages
- Added infrastructure complexity (RabbitMQ)
- Eventual consistency challenges
- Harder to debug distributed flows
- Need to handle message versioning
- Operational overhead (monitoring queues)

## Implementation Details

Event publishing:
```csharp
// Domain entity raises event
public class Document : Entity
{
    public void Publish()
    {
        AddDomainEvent(new DocumentPublishedEvent(Id, Title));
    }
}

// Infrastructure publishes to message bus
public class DomainEventDispatcher
{
    public async Task DispatchAsync(DomainEvent evt)
    {
        await _bus.Publish(evt);
    }
}
```

Event subscription:
```csharp
// Consumer in different bounded context
public class DocumentPublishedConsumer : IConsumer<DocumentPublishedEvent>
{
    public async Task Consume(ConsumeContext<DocumentPublishedEvent> context)
    {
        // Handle event in this bounded context
        await _searchIndexer.IndexDocument(context.Message);
    }
}
```

## Dependencies

- **MassTransit** (v8.1.0): Messaging abstraction
- **RabbitMQ**: Message broker
- **Polly**: Retry policies

## Files Involved

- `src/Infrastructure/Messaging/DomainEventDispatcher.cs`
- `src/Infrastructure/Messaging/MassTransitConfiguration.cs`
- `src/Domain/Events/` - Domain event definitions
- `src/*/Consumers/` - Event consumers per bounded context

## Future Considerations

- May need to implement event versioning strategy as schemas evolve
- Consider implementing outbox pattern for guaranteed event publishing
- Monitor queue depths and add auto-scaling if needed
- May introduce event sourcing if stronger audit requirements emerge
```
