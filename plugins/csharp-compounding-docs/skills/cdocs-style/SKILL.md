---
name: cdocs:style
description: Captures coding conventions, style preferences, and development standards
allowed-tools:
  - Read
  - Write
  - Bash
preconditions:
  - Project activated via /cdocs:activate
  - Style convention or preference has been established
auto-invoke:
  trigger: conversation-pattern
  patterns:
    - "always"
    - "never"
    - "prefer"
    - "convention"
    - "standard"
    - "rule"
    - "don't forget"
    - "remember to"
---

# Style Documentation Skill

## Intake

This skill captures coding conventions, style preferences, and development standards for the project or team. It expects the following context from the conversation:

- **Style rule**: What convention or preference is being established?
- **Category**: What area does this apply to (naming, formatting, architecture, etc.)?
- **Rationale**: Why is this preferred?
- **Scope**: Does this apply to the whole project, specific modules, or team-wide?
- **Exceptions**: Are there valid cases where this rule doesn't apply?

## Process

### Step 1: Gather Context

Extract from conversation history:
- **Style rule**: The specific convention or preference
- **Category**: naming, formatting, architecture, error handling, testing, documentation, etc.
- **Rationale**: Why is this approach preferred?
- **Scope**: Where does this apply? (project, module, team)
- **Examples**: Code examples showing correct and incorrect usage
- **Exceptions**: Valid cases where the rule doesn't apply
- **Enforcement**: Is this checked by linter, code review, or manual?

Use Sequential Thinking MCP to:
- Document the rationale behind the style choice
- Identify valid exceptions to the rule
- Consider edge cases or special circumstances
- Evaluate consistency with existing standards

**BLOCKING**: If the style rule or rationale is unclear, ask the user to clarify and WAIT for response.

### Step 2: Validate Schema

Load `schema.yaml` for the style doc-type.
Validate required fields:
- `doc_type` = "style"
- `title` (1-200 chars)
- `category` (1-100 chars)

Validate optional fields:
- `promotion_level`: must be one of ["standard", "promoted", "pinned"]

**BLOCK if validation fails** - show specific schema violations to the user.

### Step 3: Write Documentation

1. Generate filename: `{sanitized-title}-{YYYYMMDD}.md`
   - Sanitize title: lowercase, replace spaces with hyphens, remove special chars
   - Example: `async-suffix-naming-convention-20250125.md`

2. Create directory if needed:
   ```bash
   mkdir -p ./csharp-compounding-docs/styles/
   ```

3. Write file with YAML frontmatter + markdown body:
   ```markdown
   ---
   doc_type: style
   title: "Async methods must have 'Async' suffix"
   category: "naming"
   tags: ["naming", "async", "convention"]
   language: "C#"
   applies_to: ["C#", "ASP.NET Core"]
   date: 2025-01-25
   ---

   # Async methods must have 'Async' suffix

   ## Rule

   [Clear statement of the style rule]

   ## Rationale

   [Why this convention is preferred]

   ## Examples

   ### ✓ Correct
   ```csharp
   // Good example
   ```

   ### ✗ Incorrect
   ```csharp
   // Bad example
   ```

   ## Exceptions

   [Valid cases where this rule doesn't apply]

   ## Enforcement

   [How this is checked - linter, code review, etc.]

   ## Related Rules

   [Links to related style guidelines]
   ```

4. Use Sequential Thinking MCP when:
   - Documenting complex rationale
   - Identifying valid exceptions
   - Evaluating consistency with other rules
   - Considering edge cases

### Step 4: Post-Capture Options

After successfully writing the document:

```
✓ Style documentation captured

File created: ./csharp-compounding-docs/styles/{filename}.md

What's next?
1. Continue workflow
2. Link related docs (use /cdocs:related)
3. View documentation
4. Capture another style convention
```

Wait for user selection.

## Schema Reference

See `schema.yaml` in this directory for the complete style document schema.

Required fields:
- `doc_type`: "style"
- `title`: string (1-200 chars)
- `category`: string (1-100 chars) - e.g., "naming", "formatting", "architecture"

Optional fields:
- `exceptions`: array of strings (max 500 chars each) - known exceptions to the rules
- `applies_to`: array of strings (max 100 chars each) - languages/frameworks this applies to
- `promotion_level`: enum ["standard", "promoted", "pinned"] (default: "standard")
- `links`: array of URIs
- `tags`: array of strings (max 50 chars each)
- `language`: string (max 50 chars) - primary programming language

## Examples

### Example 1: Naming Convention

```markdown
---
doc_type: style
title: "Async methods must have 'Async' suffix"
category: "naming"
tags: ["naming", "async", "convention", "csharp"]
language: "C#"
applies_to: ["C#"]
date: 2025-01-20
---

# Async methods must have 'Async' suffix

## Rule

All async methods (methods returning `Task` or `Task<T>`) must have names ending with "Async" suffix.

## Rationale

1. **Clarity**: Makes it immediately obvious that the method is async at call site
2. **Convention**: Follows .NET Framework Design Guidelines
3. **Prevents mistakes**: Helps developers remember to `await` the call
4. **Consistency**: Standard across entire .NET ecosystem

Without the suffix, developers might call async methods without awaiting:

```csharp
// Easy to miss that this needs await
var doc = GetDocument(id);  // ❌ Returns Task<Document>, not Document!

// Suffix makes it obvious
var doc = await GetDocumentAsync(id);  // ✓ Clear that this is async
```

## Examples

### ✓ Correct

```csharp
public async Task<Document> GetDocumentAsync(int id)
{
    return await _context.Documents.FindAsync(id);
}

public async Task SaveChangesAsync()
{
    await _context.SaveChangesAsync();
}

public async Task<bool> ExistsAsync(string title)
{
    return await _context.Documents.AnyAsync(d => d.Title == title);
}
```

### ✗ Incorrect

```csharp
// ❌ Missing Async suffix
public async Task<Document> GetDocument(int id)
{
    return await _context.Documents.FindAsync(id);
}

// ❌ Missing Async suffix
public async Task SaveChanges()
{
    await _context.SaveChangesAsync();
}
```

## Exceptions

1. **Event handlers**: `async void` event handlers don't need Async suffix
   ```csharp
   private async void Button_Click(object sender, EventArgs e)
   {
       await ProcessAsync();
   }
   ```

2. **Interface implementations**: When implementing interfaces you don't control
   ```csharp
   // External interface requires this exact name
   public async Task Execute()
   {
       await DoWorkAsync();
   }
   ```

3. **Framework overrides**: When overriding framework methods
   ```csharp
   protected override async Task OnInitializedAsync()
   {
       await base.OnInitializedAsync();
   }
   ```

## Enforcement

- **Analyzer**: Enabled via `.editorconfig` (IDE0030)
- **Code Review**: Reviewers check for compliance
- **CI/CD**: Build fails if analyzer warnings present

## Related Rules

- [Repository methods naming convention](./repository-method-naming-20250118.md)
- [Public API method naming](./public-api-naming-20250115.md)
```

### Example 2: Architecture Convention

```markdown
---
doc_type: style
title: "Controllers must not contain business logic"
category: "architecture"
tags: ["architecture", "mvc", "separation-of-concerns"]
language: "C#"
applies_to: ["ASP.NET Core"]
date: 2025-01-22
---

# Controllers must not contain business logic

## Rule

ASP.NET Core controllers should only:
1. Accept request input
2. Validate input (basic validation, not business rules)
3. Call appropriate service/handler
4. Map result to response

Controllers must NOT contain business logic, data access, or complex processing.

## Rationale

1. **Testability**: Business logic in services is easier to unit test (no HTTP context needed)
2. **Reusability**: Logic in services can be called from controllers, background jobs, SignalR hubs, etc.
3. **Separation of concerns**: Controllers handle HTTP, services handle business logic
4. **Maintainability**: Changes to business logic don't affect HTTP handling

## Examples

### ✓ Correct

```csharp
[ApiController]
[Route("api/documents")]
public class DocumentsController : ControllerBase
{
    private readonly IMediator _mediator;

    public DocumentsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateDocumentRequest request)
    {
        // Just validation and delegation
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var command = new CreateDocumentCommand
        {
            Title = request.Title,
            Content = request.Content
        };

        var result = await _mediator.Send(command);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Value.Id }, result.Value)
            : BadRequest(result.Error);
    }
}
```

### ✗ Incorrect

```csharp
[ApiController]
[Route("api/documents")]
public class DocumentsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateDocumentRequest request)
    {
        // ❌ Business validation in controller
        if (await _context.Documents.AnyAsync(d => d.Title == request.Title))
            return BadRequest("Document with this title already exists");

        // ❌ Business logic in controller
        var document = new Document
        {
            Title = request.Title,
            Content = request.Content,
            Status = DetermineInitialStatus(request),  // Complex logic
            CreatedAt = DateTime.UtcNow,
            CreatedBy = User.Identity.Name
        };

        // ❌ Data access in controller
        _context.Documents.Add(document);
        await _context.SaveChangesAsync();

        // ❌ Additional business operations
        await NotifyWatchers(document);
        await UpdateSearchIndex(document);

        return Created($"/api/documents/{document.Id}", document);
    }

    // ❌ Helper methods with business logic
    private DocumentStatus DetermineInitialStatus(CreateDocumentRequest request)
    {
        // Complex business logic...
    }
}
```

## Exceptions

1. **Authorization checks**: Controller-level authorization is acceptable
   ```csharp
   if (!User.IsInRole("Admin"))
       return Forbid();
   ```

2. **Simple transformation**: Basic DTO mapping can stay in controller if trivial
   ```csharp
   var dto = new DocumentDto
   {
       Id = document.Id,
       Title = document.Title
   };
   ```

## Enforcement

- **Code Review**: Primary enforcement mechanism
- **Architecture Tests**: Use NetArchTest to enforce layer boundaries
- **Team Standards**: Regular architecture reviews

## Related Rules

- [Use MediatR for command/query handling](./mediatr-cqrs-pattern-20250120.md)
- [Service layer patterns](./service-layer-guidelines-20250115.md)
```

### Example 3: Error Handling Convention

```markdown
---
doc_type: style
title: "Never catch and ignore exceptions without logging"
category: "error_handling"
tags: ["exceptions", "error-handling", "logging"]
language: "C#"
applies_to: ["C#"]
date: 2025-01-25
---

# Never catch and ignore exceptions without logging

## Rule

Empty catch blocks are prohibited. If you catch an exception, you must either:
1. Log it
2. Wrap and rethrow with context
3. Convert to expected error (with logging)

## Rationale

Silent failures make debugging extremely difficult:
- Production issues with no error trail
- Bugs that appear as "it just doesn't work"
- No visibility into failure patterns
- Unable to track down root causes

Even if you decide to ignore an exception (rare cases), you must log WHY you're ignoring it.

## Examples

### ✓ Correct

```csharp
// Option 1: Log and continue
try
{
    await UpdateCacheAsync(data);
}
catch (Exception ex)
{
    _logger.LogWarning(ex, "Cache update failed, continuing without cache");
    // Continue execution - cache is non-critical
}

// Option 2: Log and rethrow with context
try
{
    await _httpClient.GetAsync(url);
}
catch (HttpRequestException ex)
{
    _logger.LogError(ex, "Failed to fetch data from {Url}", url);
    throw new DataFetchException($"Unable to fetch from {url}", ex);
}

// Option 3: Convert to expected error (Result pattern)
try
{
    var doc = await _repository.GetAsync(id);
    return Result.Success(doc);
}
catch (NotFoundException ex)
{
    _logger.LogInformation("Document {Id} not found", id);
    return Result.Failure<Document>("Document not found");
}
```

### ✗ Incorrect

```csharp
// ❌ Empty catch - silent failure
try
{
    await SaveToDatabase(data);
}
catch
{
    // Silently fails, no one knows save failed
}

// ❌ Catch without logging
try
{
    var result = await ProcessAsync();
}
catch (Exception ex)
{
    return null;  // Silent failure
}

// ❌ Generic catch all without context
try
{
    await ComplexOperation();
}
catch (Exception ex)
{
    _logger.LogError("Error occurred");  // No exception details, no context
    throw;
}
```

## Exceptions

1. **Known, expected exceptions in hot paths** - but must be documented
   ```csharp
   try
   {
       return int.Parse(value);
   }
   catch (FormatException)
   {
       // Explicitly documented: invalid format is expected input scenario
       // Logged at caller level, not here to avoid log spam
       return 0;
   }
   ```

2. **Framework requirements** - some frameworks require empty catch
   ```csharp
   // Windows Forms requires try-catch in event handlers
   private void Button_Click(object sender, EventArgs e)
   {
       try
       {
           ProcessClick();
       }
       catch (Exception ex)
       {
           _logger.LogError(ex, "Error in button click handler");
           MessageBox.Show("An error occurred");
       }
   }
   ```

## Enforcement

- **Code Review**: Primary enforcement
- **Static Analysis**: Consider enabling CA1031 (Do not catch general exception types)
- **SonarQube**: Flags empty catch blocks

## Related Rules

- [Use structured logging with Serilog](./structured-logging-20250118.md)
- [Exception handling in async methods](./async-exception-handling-20250120.md)
```

### Example 4: Testing Convention

```markdown
---
doc_type: style
title: "Test method naming: MethodName_StateUnderTest_ExpectedBehavior"
category: "testing"
tags: ["testing", "naming", "unit-tests"]
language: "C#"
applies_to: ["xUnit", "NUnit", "MSTest"]
date: 2025-01-23
---

# Test method naming: MethodName_StateUnderTest_ExpectedBehavior

## Rule

Unit test methods must follow this naming pattern:
```
[MethodName]_[StateUnderTest]_[ExpectedBehavior]
```

Example: `CreateDocument_WithDuplicateTitle_ReturnsValidationError`

## Rationale

1. **Self-documenting**: Test name describes exactly what is being tested
2. **Failure clarity**: When test fails, you immediately know what broke
3. **Discoverability**: Easy to find tests for specific scenarios
4. **Consistency**: Standard pattern across entire test suite

## Examples

### ✓ Correct

```csharp
public class DocumentServiceTests
{
    [Fact]
    public void CreateDocument_WithValidData_ReturnsSuccess()
    {
        // Arrange
        var service = new DocumentService();
        var request = new CreateDocumentRequest { Title = "Test" };

        // Act
        var result = service.CreateDocument(request);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void CreateDocument_WithDuplicateTitle_ReturnsValidationError()
    {
        // Arrange
        var service = new DocumentService();
        service.CreateDocument(new CreateDocumentRequest { Title = "Test" });

        // Act
        var result = service.CreateDocument(new CreateDocumentRequest { Title = "Test" });

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("DUPLICATE_TITLE", result.Error.Code);
    }

    [Fact]
    public void CreateDocument_WhenDatabaseUnavailable_ThrowsDataException()
    {
        // Arrange
        var mockDb = new Mock<IDatabase>();
        mockDb.Setup(x => x.SaveAsync(It.IsAny<Document>()))
              .ThrowsAsync(new DatabaseUnavailableException());
        var service = new DocumentService(mockDb.Object);

        // Act & Assert
        Assert.ThrowsAsync<DataException>(() =>
            service.CreateDocumentAsync(new CreateDocumentRequest { Title = "Test" }));
    }
}
```

### ✗ Incorrect

```csharp
public class DocumentServiceTests
{
    // ❌ Vague name
    [Fact]
    public void Test1()
    {
        var service = new DocumentService();
        var result = service.CreateDocument(new CreateDocumentRequest { Title = "Test" });
        Assert.True(result.IsSuccess);
    }

    // ❌ Doesn't describe state under test
    [Fact]
    public void CreateDocument_ReturnsError()
    {
        // Which error? Under what conditions?
    }

    // ❌ Too verbose
    [Fact]
    public void WhenCreatingADocumentWithADuplicateTitleTheServiceShouldReturnAValidationError()
    {
        // Hard to read, hard to scan test list
    }
}
```

## Exceptions

1. **Theory tests with InlineData** - can use descriptive sentences
   ```csharp
   [Theory]
   [InlineData("")]
   [InlineData(null)]
   [InlineData("   ")]
   public void CreateDocument_WithInvalidTitle_ReturnsValidationError(string title)
   {
       // Pattern still followed, but InlineData makes intent clear
   }
   ```

2. **Integration tests** - may use longer descriptive names
   ```csharp
   public async Task EndToEnd_UserCreatesAndPublishesDocument_AppearsInSearch()
   {
       // Integration test - more descriptive name is acceptable
   }
   ```

## Enforcement

- **Code Review**: Reviewers check test names
- **Team Standard**: Discussed in onboarding
- **Examples**: Template test file in repository

## Related Rules

- [Arrange-Act-Assert pattern for test structure](./arrange-act-assert-20250120.md)
- [One assertion per test (with exceptions)](./single-assertion-tests-20250118.md)
```
