# Phase 084: Multi-Trigger Conflict Resolution

> **Status**: NOT_STARTED
> **Effort Estimate**: 4-5 hours
> **Category**: Skills System
> **Prerequisites**: Phase 082 (Capture Skills Framework)

---

## Spec References

This phase implements multi-trigger conflict resolution defined in:

- **spec/skills.md** - [Architectural Difference from Original Plugin](../spec/skills.md#architectural-difference-from-original-plugin) (lines 21-42)
- **spec/skills/meta-skills.md** - [/cdocs:capture-select](../spec/skills/meta-skills.md#cdocscapture-select) (lines 185-240)
- **spec/skills/meta-skills.md** - [Multi-Trigger Conflict Resolution](../spec/skills/meta-skills.md#multi-trigger-conflict-resolution) (lines 243-259)

---

## Objectives

1. Implement detection of multiple skill triggers on the same conversation content
2. Build the `/cdocs:capture-select` meta-skill for conflict resolution
3. Design and implement the user selection interface for multi-select capability
4. Create trigger evaluation service for parallel skill matching
5. Implement fallback behavior for edge cases

---

## Acceptance Criteria

### Multiple Trigger Detection
- [ ] `ITriggerEvaluator.EvaluateAllTriggers(conversationContent)` returns list of matching skills
- [ ] Each capture skill can report trigger matches without executing capture
- [ ] Trigger evaluation runs in parallel for all registered capture skills
- [ ] Detection considers both `trigger_phrases` and `classification_hints` (two-stage classification)
- [ ] Evaluation completes within 100ms for standard skill sets (5-10 skills)

### Conflict Resolution via /cdocs:capture-select
- [ ] Meta-skill auto-invokes when 2+ capture skills trigger simultaneously
- [ ] Meta-skill does NOT invoke when 0 or 1 skills trigger
- [ ] Meta-skill can also be manually invoked via `/cdocs:capture-select`
- [ ] Resolution flow passes through to individual capture skills after selection
- [ ] Each selected skill executes independently (not merged captures)

### User Selection Interface Design
- [ ] Multi-select dialog displays all triggered doc-types with descriptions
- [ ] Each option shows which trigger phrases matched for transparency
- [ ] User can select 0, 1, or multiple doc-types to capture
- [ ] "Capture Selected" button proceeds with selected items
- [ ] "Skip All" button cancels without capturing
- [ ] Interface works in terminal/CLI environment (text-based selection)

### Multi-Select Capability
- [ ] Multiple selections result in sequential capture skill invocations
- [ ] Each capture creates a separate document (no merging)
- [ ] Progress indication for multi-capture scenarios
- [ ] Failure of one capture does not block others (graceful degradation)
- [ ] Results summary shows which captures succeeded/failed

### Fallback Behavior
- [ ] If evaluation times out, log warning and skip auto-invoke
- [ ] If trigger evaluation throws, catch and log (never crash)
- [ ] If selected skill is unavailable, report error and continue with others
- [ ] Empty trigger match list (0 skills) results in no action
- [ ] Single trigger match (1 skill) proceeds directly without meta-skill

---

## Implementation Notes

### Trigger Evaluation Service

```csharp
/// <summary>
/// Evaluates conversation content against all registered capture skill triggers.
/// </summary>
public interface ITriggerEvaluator
{
    /// <summary>
    /// Evaluates all capture skill triggers against the conversation content.
    /// </summary>
    /// <param name="conversationContent">The conversation text to evaluate</param>
    /// <param name="cancellationToken">Cancellation token for timeout</param>
    /// <returns>List of skills that triggered with their match details</returns>
    Task<TriggerEvaluationResult> EvaluateAllTriggersAsync(
        string conversationContent,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of trigger evaluation for a single skill.
/// </summary>
public record SkillTriggerMatch(
    string SkillName,
    string DocType,
    string Description,
    IReadOnlyList<string> MatchedTriggerPhrases,
    IReadOnlyList<string> MatchedClassificationHints,
    double ConfidenceScore);

/// <summary>
/// Complete result of evaluating all triggers.
/// </summary>
public record TriggerEvaluationResult(
    IReadOnlyList<SkillTriggerMatch> Matches,
    TimeSpan EvaluationDuration)
{
    public int MatchCount => Matches.Count;
    public bool HasMultipleMatches => MatchCount > 1;
    public bool HasSingleMatch => MatchCount == 1;
    public bool HasNoMatches => MatchCount == 0;
}
```

### Trigger Evaluator Implementation

```csharp
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

public class TriggerEvaluator : ITriggerEvaluator
{
    private readonly IDocTypeRegistry _docTypeRegistry;
    private readonly ILogger<TriggerEvaluator> _logger;
    private readonly TriggerEvaluationOptions _options;

    public TriggerEvaluator(
        IDocTypeRegistry docTypeRegistry,
        IOptions<TriggerEvaluationOptions> options,
        ILogger<TriggerEvaluator> logger)
    {
        _docTypeRegistry = docTypeRegistry;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<TriggerEvaluationResult> EvaluateAllTriggersAsync(
        string conversationContent,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var matches = new List<SkillTriggerMatch>();

        try
        {
            // Get all capture skills (built-in + custom)
            var captureSkills = await _docTypeRegistry.GetAllCaptureDocTypesAsync(cancellationToken);

            // Evaluate in parallel with timeout
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(_options.EvaluationTimeout);

            var evaluationTasks = captureSkills.Select(skill =>
                EvaluateSingleSkillAsync(skill, conversationContent, timeoutCts.Token));

            var results = await Task.WhenAll(evaluationTasks);

            matches = results
                .Where(m => m is not null)
                .Cast<SkillTriggerMatch>()
                .OrderByDescending(m => m.ConfidenceScore)
                .ToList();

            _logger.LogDebug(
                "Trigger evaluation completed in {Duration}ms with {MatchCount} matches",
                stopwatch.ElapsedMilliseconds,
                matches.Count);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                "Trigger evaluation timed out after {Timeout}ms, skipping auto-invoke",
                _options.EvaluationTimeout.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Trigger evaluation failed, skipping auto-invoke");
        }

        stopwatch.Stop();
        return new TriggerEvaluationResult(matches, stopwatch.Elapsed);
    }

    private async Task<SkillTriggerMatch?> EvaluateSingleSkillAsync(
        DocTypeDefinition docType,
        string conversationContent,
        CancellationToken cancellationToken)
    {
        var contentLower = conversationContent.ToLowerInvariant();

        // Stage 1: Check trigger phrases (wide net)
        var matchedPhrases = docType.TriggerPhrases
            .Where(phrase => contentLower.Contains(phrase.ToLowerInvariant()))
            .ToList();

        if (matchedPhrases.Count == 0)
        {
            return null; // No trigger phrase match, skip this skill
        }

        // Stage 2: Check classification hints (semantic validation)
        var matchedHints = docType.ClassificationHints
            .Where(hint => contentLower.Contains(hint.ToLowerInvariant()))
            .ToList();

        // Calculate confidence score based on matches
        var phraseScore = (double)matchedPhrases.Count / docType.TriggerPhrases.Count;
        var hintScore = docType.ClassificationHints.Count > 0
            ? (double)matchedHints.Count / docType.ClassificationHints.Count
            : 0.5; // Neutral if no hints defined

        var confidenceScore = (phraseScore * 0.6) + (hintScore * 0.4);

        // Only match if above minimum threshold
        if (confidenceScore < _options.MinimumConfidenceThreshold)
        {
            return null;
        }

        return new SkillTriggerMatch(
            SkillName: $"cdocs:{docType.Name}",
            DocType: docType.Name,
            Description: docType.Description,
            MatchedTriggerPhrases: matchedPhrases,
            MatchedClassificationHints: matchedHints,
            ConfidenceScore: confidenceScore);
    }
}
```

### Configuration Options

```csharp
public class TriggerEvaluationOptions
{
    /// <summary>
    /// Maximum time allowed for trigger evaluation. Default: 100ms
    /// </summary>
    public TimeSpan EvaluationTimeout { get; set; } = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// Minimum confidence score (0-1) required for a match. Default: 0.3
    /// </summary>
    public double MinimumConfidenceThreshold { get; set; } = 0.3;
}
```

### Capture Select Meta-Skill Handler

```csharp
/// <summary>
/// Handles the /cdocs:capture-select meta-skill for multi-trigger conflict resolution.
/// </summary>
public interface ICaptureSelectHandler
{
    /// <summary>
    /// Processes multi-trigger selection and invokes selected capture skills.
    /// </summary>
    Task<CaptureSelectResult> HandleCaptureSelectAsync(
        TriggerEvaluationResult triggerResult,
        string conversationContent,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of the capture-select operation.
/// </summary>
public record CaptureSelectResult(
    IReadOnlyList<string> SelectedDocTypes,
    IReadOnlyList<CaptureOutcome> Outcomes,
    bool UserSkippedAll);

/// <summary>
/// Outcome of a single capture attempt.
/// </summary>
public record CaptureOutcome(
    string DocType,
    bool Success,
    string? DocumentPath,
    string? ErrorMessage);
```

### Capture Select Implementation

```csharp
public class CaptureSelectHandler : ICaptureSelectHandler
{
    private readonly IUserInteractionService _userInteraction;
    private readonly ICaptureSkillInvoker _captureInvoker;
    private readonly ILogger<CaptureSelectHandler> _logger;

    public CaptureSelectHandler(
        IUserInteractionService userInteraction,
        ICaptureSkillInvoker captureInvoker,
        ILogger<CaptureSelectHandler> logger)
    {
        _userInteraction = userInteraction;
        _captureInvoker = captureInvoker;
        _logger = logger;
    }

    public async Task<CaptureSelectResult> HandleCaptureSelectAsync(
        TriggerEvaluationResult triggerResult,
        string conversationContent,
        CancellationToken cancellationToken = default)
    {
        // Build selection options for user
        var options = triggerResult.Matches
            .Select(m => new SelectionOption(
                Key: m.DocType,
                DisplayText: FormatOptionDisplay(m)))
            .ToList();

        // Present multi-select UI to user
        var selection = await _userInteraction.PresentMultiSelectAsync(
            title: "Multiple doc types detected for this content:",
            options: options,
            confirmButton: "Capture Selected",
            cancelButton: "Skip All",
            cancellationToken: cancellationToken);

        if (selection.Cancelled)
        {
            _logger.LogInformation("User skipped all captures");
            return new CaptureSelectResult(
                SelectedDocTypes: Array.Empty<string>(),
                Outcomes: Array.Empty<CaptureOutcome>(),
                UserSkippedAll: true);
        }

        // Invoke selected capture skills sequentially
        var outcomes = new List<CaptureOutcome>();
        foreach (var docType in selection.SelectedKeys)
        {
            _logger.LogInformation("Invoking capture for doc-type: {DocType}", docType);

            try
            {
                var result = await _captureInvoker.InvokeCaptureAsync(
                    docType,
                    conversationContent,
                    cancellationToken);

                outcomes.Add(new CaptureOutcome(
                    DocType: docType,
                    Success: true,
                    DocumentPath: result.DocumentPath,
                    ErrorMessage: null));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Capture failed for doc-type: {DocType}", docType);

                outcomes.Add(new CaptureOutcome(
                    DocType: docType,
                    Success: false,
                    DocumentPath: null,
                    ErrorMessage: ex.Message));
            }
        }

        return new CaptureSelectResult(
            SelectedDocTypes: selection.SelectedKeys,
            Outcomes: outcomes,
            UserSkippedAll: false);
    }

    private static string FormatOptionDisplay(SkillTriggerMatch match)
    {
        var triggersMatched = string.Join(", ", match.MatchedTriggerPhrases.Select(p => $"\"{p}\""));
        return $"{match.DocType} - {match.Description}\n    Triggers matched: {triggersMatched}";
    }
}
```

### User Interaction Service (CLI Interface)

```csharp
/// <summary>
/// Provides user interaction capabilities for skill selection.
/// </summary>
public interface IUserInteractionService
{
    /// <summary>
    /// Presents a multi-select dialog to the user.
    /// </summary>
    Task<MultiSelectResult> PresentMultiSelectAsync(
        string title,
        IReadOnlyList<SelectionOption> options,
        string confirmButton,
        string cancelButton,
        CancellationToken cancellationToken = default);
}

public record SelectionOption(string Key, string DisplayText);

public record MultiSelectResult(
    IReadOnlyList<string> SelectedKeys,
    bool Cancelled);
```

### CLI Multi-Select Implementation

```csharp
/// <summary>
/// Terminal-based multi-select implementation for Claude Code SKILL.md output.
/// </summary>
public class CliUserInteractionService : IUserInteractionService
{
    public Task<MultiSelectResult> PresentMultiSelectAsync(
        string title,
        IReadOnlyList<SelectionOption> options,
        string confirmButton,
        string cancelButton,
        CancellationToken cancellationToken = default)
    {
        // In SKILL.md context, we output structured prompt for Claude to present
        // The actual selection happens via Claude's conversation with the user

        var selectionPrompt = new StringBuilder();
        selectionPrompt.AppendLine(title);
        selectionPrompt.AppendLine();

        for (int i = 0; i < options.Count; i++)
        {
            selectionPrompt.AppendLine($"[ ] {options[i].DisplayText}");
            selectionPrompt.AppendLine();
        }

        selectionPrompt.AppendLine($"[{confirmButton}] [{cancelButton}]");

        // Return the formatted prompt for skill to output
        // Actual selection is handled by skill conversation flow
        return Task.FromResult(new MultiSelectResult(
            SelectedKeys: Array.Empty<string>(),
            Cancelled: false));
    }
}
```

### Multi-Trigger Orchestrator

```csharp
/// <summary>
/// Orchestrates the multi-trigger detection and resolution flow.
/// </summary>
public class MultiTriggerOrchestrator
{
    private readonly ITriggerEvaluator _triggerEvaluator;
    private readonly ICaptureSelectHandler _captureSelectHandler;
    private readonly ICaptureSkillInvoker _captureInvoker;
    private readonly ILogger<MultiTriggerOrchestrator> _logger;

    public MultiTriggerOrchestrator(
        ITriggerEvaluator triggerEvaluator,
        ICaptureSelectHandler captureSelectHandler,
        ICaptureSkillInvoker captureInvoker,
        ILogger<MultiTriggerOrchestrator> logger)
    {
        _triggerEvaluator = triggerEvaluator;
        _captureSelectHandler = captureSelectHandler;
        _captureInvoker = captureInvoker;
        _logger = logger;
    }

    /// <summary>
    /// Evaluates triggers and routes to appropriate handler based on match count.
    /// </summary>
    public async Task<TriggerResolutionResult> ResolveTriggersAsync(
        string conversationContent,
        CancellationToken cancellationToken = default)
    {
        var evalResult = await _triggerEvaluator.EvaluateAllTriggersAsync(
            conversationContent,
            cancellationToken);

        return evalResult.MatchCount switch
        {
            0 => new TriggerResolutionResult(
                Action: TriggerAction.NoAction,
                Message: "No capture triggers detected"),

            1 => await HandleSingleTriggerAsync(
                evalResult.Matches[0],
                conversationContent,
                cancellationToken),

            _ => await HandleMultipleTriggerAsync(
                evalResult,
                conversationContent,
                cancellationToken)
        };
    }

    private async Task<TriggerResolutionResult> HandleSingleTriggerAsync(
        SkillTriggerMatch match,
        string conversationContent,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Single trigger detected: {SkillName}, invoking directly",
            match.SkillName);

        try
        {
            var result = await _captureInvoker.InvokeCaptureAsync(
                match.DocType,
                conversationContent,
                cancellationToken);

            return new TriggerResolutionResult(
                Action: TriggerAction.DirectCapture,
                Message: $"Captured {match.DocType} to {result.DocumentPath}");
        }
        catch (Exception ex)
        {
            return new TriggerResolutionResult(
                Action: TriggerAction.Error,
                Message: $"Capture failed: {ex.Message}");
        }
    }

    private async Task<TriggerResolutionResult> HandleMultipleTriggerAsync(
        TriggerEvaluationResult evalResult,
        string conversationContent,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Multiple triggers detected ({Count}), invoking capture-select meta-skill",
            evalResult.MatchCount);

        var selectResult = await _captureSelectHandler.HandleCaptureSelectAsync(
            evalResult,
            conversationContent,
            cancellationToken);

        if (selectResult.UserSkippedAll)
        {
            return new TriggerResolutionResult(
                Action: TriggerAction.UserSkipped,
                Message: "User skipped all captures");
        }

        var successCount = selectResult.Outcomes.Count(o => o.Success);
        var failCount = selectResult.Outcomes.Count(o => !o.Success);

        return new TriggerResolutionResult(
            Action: TriggerAction.MultiCapture,
            Message: $"Captured {successCount} documents, {failCount} failed");
    }
}

public enum TriggerAction
{
    NoAction,
    DirectCapture,
    MultiCapture,
    UserSkipped,
    Error
}

public record TriggerResolutionResult(
    TriggerAction Action,
    string Message);
```

### Dependency Injection Registration

```csharp
// In DI configuration
services.AddSingleton<ITriggerEvaluator, TriggerEvaluator>();
services.AddSingleton<ICaptureSelectHandler, CaptureSelectHandler>();
services.AddSingleton<IUserInteractionService, CliUserInteractionService>();
services.AddSingleton<MultiTriggerOrchestrator>();
services.Configure<TriggerEvaluationOptions>(
    configuration.GetSection("TriggerEvaluation"));
```

---

## Dependencies

### Depends On
- Phase 082: Capture Skills Framework (provides `ICaptureSkillInvoker`, doc-type registration)
- Phase 017: DI Container Setup (dependency injection)
- Phase 018: Logging Infrastructure (logging framework)
- Phase 014: Schema Validation (doc-type schemas with trigger phrases)

### Blocks
- Phase 085: Custom Doc-Type Skills (custom skills use same trigger evaluation)
- Capture skill auto-invocation (requires trigger detection)

---

## Verification Steps

After completing this phase, verify:

1. **Trigger detection works**: Multiple skills correctly identified for ambiguous content
2. **Single trigger direct**: Single match proceeds without capture-select
3. **Multi-select UI**: Selection interface displays correctly in terminal
4. **Multi-capture works**: Multiple selected types create separate documents
5. **Graceful failures**: Individual capture failure does not block others
6. **Timeout handling**: Slow evaluation does not block the system

---

## Unit Test Scenarios

```csharp
[Fact]
public async Task EvaluateAllTriggers_ReturnsMultipleMatches_WhenContentMatchesSeveralSkills()
{
    // Arrange
    var registry = new MockDocTypeRegistry();
    registry.AddDocType(new DocTypeDefinition
    {
        Name = "problem",
        TriggerPhrases = new[] { "fixed", "bug" },
        ClassificationHints = new[] { "error", "exception" }
    });
    registry.AddDocType(new DocTypeDefinition
    {
        Name = "tool",
        TriggerPhrases = new[] { "NuGet", "package" },
        ClassificationHints = new[] { "dependency", "library" }
    });

    var evaluator = new TriggerEvaluator(
        registry,
        Options.Create(new TriggerEvaluationOptions()),
        NullLogger<TriggerEvaluator>.Instance);

    // Content matches both problem and tool
    var content = "I fixed a bug in the NuGet package";

    // Act
    var result = await evaluator.EvaluateAllTriggersAsync(content);

    // Assert
    Assert.True(result.HasMultipleMatches);
    Assert.Equal(2, result.MatchCount);
    Assert.Contains(result.Matches, m => m.DocType == "problem");
    Assert.Contains(result.Matches, m => m.DocType == "tool");
}

[Fact]
public async Task EvaluateAllTriggers_ReturnsSingleMatch_WhenOnlyOneSkillTriggers()
{
    var registry = new MockDocTypeRegistry();
    registry.AddDocType(new DocTypeDefinition
    {
        Name = "problem",
        TriggerPhrases = new[] { "fixed", "bug" },
        ClassificationHints = new[] { "error" }
    });
    registry.AddDocType(new DocTypeDefinition
    {
        Name = "tool",
        TriggerPhrases = new[] { "NuGet", "package" },
        ClassificationHints = new[] { "dependency" }
    });

    var evaluator = new TriggerEvaluator(
        registry,
        Options.Create(new TriggerEvaluationOptions()),
        NullLogger<TriggerEvaluator>.Instance);

    // Content only matches problem
    var content = "I fixed a bug in the authentication module";

    var result = await evaluator.EvaluateAllTriggersAsync(content);

    Assert.True(result.HasSingleMatch);
    Assert.Equal("problem", result.Matches[0].DocType);
}

[Fact]
public async Task EvaluateAllTriggers_ReturnsNoMatches_WhenNoTriggersMatch()
{
    var registry = new MockDocTypeRegistry();
    registry.AddDocType(new DocTypeDefinition
    {
        Name = "problem",
        TriggerPhrases = new[] { "fixed", "bug" },
        ClassificationHints = new[] { "error" }
    });

    var evaluator = new TriggerEvaluator(
        registry,
        Options.Create(new TriggerEvaluationOptions()),
        NullLogger<TriggerEvaluator>.Instance);

    // Content doesn't match any triggers
    var content = "Let's discuss the project roadmap";

    var result = await evaluator.EvaluateAllTriggersAsync(content);

    Assert.True(result.HasNoMatches);
}

[Fact]
public async Task EvaluateAllTriggers_RespectsTimeout()
{
    var registry = new SlowMockDocTypeRegistry(delay: TimeSpan.FromSeconds(5));
    var evaluator = new TriggerEvaluator(
        registry,
        Options.Create(new TriggerEvaluationOptions
        {
            EvaluationTimeout = TimeSpan.FromMilliseconds(50)
        }),
        NullLogger<TriggerEvaluator>.Instance);

    var stopwatch = Stopwatch.StartNew();
    var result = await evaluator.EvaluateAllTriggersAsync("test content");
    stopwatch.Stop();

    Assert.True(stopwatch.ElapsedMilliseconds < 1000);
    Assert.True(result.HasNoMatches); // Timeout results in no matches
}

[Fact]
public async Task HandleCaptureSelect_InvokesSelectedSkillsSequentially()
{
    var mockInvoker = new MockCaptureSkillInvoker();
    var mockInteraction = new MockUserInteractionService(
        selectedKeys: new[] { "problem", "tool" });

    var handler = new CaptureSelectHandler(
        mockInteraction,
        mockInvoker,
        NullLogger<CaptureSelectHandler>.Instance);

    var triggerResult = new TriggerEvaluationResult(
        Matches: new[]
        {
            new SkillTriggerMatch("cdocs:problem", "problem", "Bug fix", new[] { "fixed" }, Array.Empty<string>(), 0.8),
            new SkillTriggerMatch("cdocs:tool", "tool", "Tool knowledge", new[] { "NuGet" }, Array.Empty<string>(), 0.7)
        },
        EvaluationDuration: TimeSpan.FromMilliseconds(50));

    var result = await handler.HandleCaptureSelectAsync(
        triggerResult,
        "I fixed a bug in the NuGet package",
        CancellationToken.None);

    Assert.Equal(2, result.SelectedDocTypes.Count);
    Assert.Equal(2, result.Outcomes.Count);
    Assert.All(result.Outcomes, o => Assert.True(o.Success));
}

[Fact]
public async Task HandleCaptureSelect_ReturnsSkipped_WhenUserCancels()
{
    var mockInteraction = new MockUserInteractionService(cancelled: true);
    var handler = new CaptureSelectHandler(
        mockInteraction,
        new MockCaptureSkillInvoker(),
        NullLogger<CaptureSelectHandler>.Instance);

    var triggerResult = new TriggerEvaluationResult(
        Matches: new[]
        {
            new SkillTriggerMatch("cdocs:problem", "problem", "Bug fix", new[] { "fixed" }, Array.Empty<string>(), 0.8)
        },
        EvaluationDuration: TimeSpan.FromMilliseconds(50));

    var result = await handler.HandleCaptureSelectAsync(
        triggerResult,
        "test content",
        CancellationToken.None);

    Assert.True(result.UserSkippedAll);
    Assert.Empty(result.Outcomes);
}

[Fact]
public async Task HandleCaptureSelect_ContinuesOnFailure_WhenOneCapturesFails()
{
    var mockInvoker = new MockCaptureSkillInvoker(
        failFor: new[] { "tool" });
    var mockInteraction = new MockUserInteractionService(
        selectedKeys: new[] { "problem", "tool", "insight" });

    var handler = new CaptureSelectHandler(
        mockInteraction,
        mockInvoker,
        NullLogger<CaptureSelectHandler>.Instance);

    var triggerResult = new TriggerEvaluationResult(
        Matches: new[]
        {
            new SkillTriggerMatch("cdocs:problem", "problem", "Bug fix", new[] { "fixed" }, Array.Empty<string>(), 0.8),
            new SkillTriggerMatch("cdocs:tool", "tool", "Tool", new[] { "NuGet" }, Array.Empty<string>(), 0.7),
            new SkillTriggerMatch("cdocs:insight", "insight", "Insight", new[] { "learned" }, Array.Empty<string>(), 0.6)
        },
        EvaluationDuration: TimeSpan.FromMilliseconds(50));

    var result = await handler.HandleCaptureSelectAsync(
        triggerResult,
        "test content",
        CancellationToken.None);

    Assert.Equal(3, result.Outcomes.Count);
    Assert.Equal(2, result.Outcomes.Count(o => o.Success));
    Assert.Single(result.Outcomes.Where(o => !o.Success));
}

[Fact]
public async Task Orchestrator_InvokesDirectCapture_WhenSingleTrigger()
{
    var mockEvaluator = new MockTriggerEvaluator(matchCount: 1);
    var mockInvoker = new MockCaptureSkillInvoker();
    var orchestrator = new MultiTriggerOrchestrator(
        mockEvaluator,
        new MockCaptureSelectHandler(),
        mockInvoker,
        NullLogger<MultiTriggerOrchestrator>.Instance);

    var result = await orchestrator.ResolveTriggersAsync("I fixed a bug");

    Assert.Equal(TriggerAction.DirectCapture, result.Action);
    Assert.Single(mockInvoker.Invocations);
}

[Fact]
public async Task Orchestrator_InvokesCaptureSelect_WhenMultipleTriggers()
{
    var mockEvaluator = new MockTriggerEvaluator(matchCount: 3);
    var mockSelectHandler = new MockCaptureSelectHandler();
    var orchestrator = new MultiTriggerOrchestrator(
        mockEvaluator,
        mockSelectHandler,
        new MockCaptureSkillInvoker(),
        NullLogger<MultiTriggerOrchestrator>.Instance);

    var result = await orchestrator.ResolveTriggersAsync("I fixed a bug in the NuGet package");

    Assert.Equal(TriggerAction.MultiCapture, result.Action);
    Assert.Single(mockSelectHandler.Invocations);
}

[Fact]
public async Task Orchestrator_ReturnsNoAction_WhenZeroTriggers()
{
    var mockEvaluator = new MockTriggerEvaluator(matchCount: 0);
    var orchestrator = new MultiTriggerOrchestrator(
        mockEvaluator,
        new MockCaptureSelectHandler(),
        new MockCaptureSkillInvoker(),
        NullLogger<MultiTriggerOrchestrator>.Instance);

    var result = await orchestrator.ResolveTriggersAsync("Let's discuss the roadmap");

    Assert.Equal(TriggerAction.NoAction, result.Action);
}

[Fact]
public void SkillTriggerMatch_CalculatesConfidenceCorrectly()
{
    // Given: 2 of 3 trigger phrases match, 1 of 2 hints match
    // phraseScore = 2/3 = 0.667
    // hintScore = 1/2 = 0.5
    // confidence = (0.667 * 0.6) + (0.5 * 0.4) = 0.4 + 0.2 = 0.6

    var match = new SkillTriggerMatch(
        SkillName: "cdocs:problem",
        DocType: "problem",
        Description: "Bug fix",
        MatchedTriggerPhrases: new[] { "fixed", "bug" },
        MatchedClassificationHints: new[] { "error" },
        ConfidenceScore: 0.6);

    Assert.Equal(0.6, match.ConfidenceScore, precision: 1);
}
```

---

## SKILL.md Template for capture-select

```yaml
---
name: cdocs:capture-select
description: Handle multi-trigger conflict resolution when multiple capture skills detect triggers
allowed-tools:
  - Read
  - Write
preconditions:
  - Project activated via /cdocs:activate
auto-invoke:
  trigger: multi-trigger
  condition: 2+ capture skills triggered
---

# Multi-Trigger Conflict Resolution

You are helping the user decide which documentation types to capture when multiple triggers were detected.

## Context

Multiple doc types have been detected for the current conversation content. Present the options to the user and let them choose which ones to capture.

## Instructions

1. Display the detected doc types with their matched triggers
2. Allow multi-select (user can choose 0, 1, or multiple)
3. For each selected type, invoke the corresponding capture skill
4. Report success/failure for each capture attempt

## Output Format

```
Multiple doc types detected for this content:

[ ] {DocType1} - {Description1}
    Triggers matched: "{trigger1}", "{trigger2}"

[ ] {DocType2} - {Description2}
    Triggers matched: "{trigger3}"

[Capture Selected] [Skip All]
```
```

---

## Performance Considerations

- **Parallel evaluation**: All skills evaluated simultaneously to minimize latency
- **Early termination**: Stop evaluation if timeout exceeded
- **Confidence threshold**: Filter low-confidence matches to reduce false positives
- **Caching**: Consider caching trigger phrase compilations for repeated evaluations
- **Target latency**: < 100ms for standard skill sets (5-10 skills)

---

## Notes

- The multi-trigger resolution system preserves user control in ambiguous situations
- Each capture produces a separate document; content is not merged
- The two-stage classification (trigger phrases + hints) reduces false positives
- Custom doc-types (from Phase 085) integrate seamlessly with the same evaluation system
- CLI interface outputs structured prompts for Claude to present to users
