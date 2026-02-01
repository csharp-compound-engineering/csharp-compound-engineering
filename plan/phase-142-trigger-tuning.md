# Phase 142: Trigger Phrase Tuning and Testing

> **Status**: NOT_STARTED
> **Effort Estimate**: 6-8 hours
> **Category**: Skills System
> **Prerequisites**: Phase 082 (Auto-Invoke System)

---

## Spec References

This phase implements trigger phrase effectiveness testing and refinement as defined in:

- **spec/doc-types.md** - [Two-Stage Classification Model](../spec/doc-types.md#two-stage-classification-model) - Trigger phrases and classification hints working together to reduce false positives
- **spec/skills/skill-patterns.md** - [Capture Detection](../spec/skills/skill-patterns.md#capture-detection) - Auto-trigger patterns for doc-creation skills
- **spec/doc-types/built-in-types.md** - Complete trigger phrases and classification hints for all 5 built-in doc-types

---

## Objectives

1. Create comprehensive test suite for trigger phrase effectiveness
2. Establish metrics and benchmarks for trigger accuracy
3. Implement false positive reduction validation through classification hints
4. Design trigger phrase refinement workflow
5. Document test cases for each doc-type with positive and negative examples
6. Create tooling to measure and tune trigger precision/recall

---

## Acceptance Criteria

### Trigger Phrase Effectiveness Testing

- [ ] `TriggerEffectivenessTestSuite` class created with test categories:
  - [ ] True positive tests (correctly triggers on relevant content)
  - [ ] True negative tests (correctly ignores irrelevant content)
  - [ ] False positive tests (incorrectly triggers on irrelevant content)
  - [ ] False negative tests (fails to trigger on relevant content)
- [ ] Test corpus of 50+ conversation samples covering all doc-types
- [ ] Each doc-type has at least 10 test cases (5 positive, 5 negative)
- [ ] Automated test runner reports precision, recall, and F1 score per doc-type
- [ ] Baseline metrics established for acceptable trigger accuracy (target: 85% precision, 90% recall)

### False Positive Reduction via Classification Hints

- [ ] `ClassificationHintEffectivenessTests` validates two-stage filtering:
  - [ ] Generic trigger phrases (e.g., "always", "never") filtered by classification hints
  - [ ] Context-appropriate triggers pass both stages
  - [ ] Classification hint threshold validated (default 30%)
- [ ] Test cases for each built-in type demonstrating false positive prevention:
  - [ ] `style`: "always" in casual conversation vs. coding standard context
  - [ ] `problem`: "error" in everyday usage vs. debugging context
  - [ ] `insight`: "learned that" in personal vs. product context
  - [ ] `codebase`: "decided to" in personal decision vs. architecture context
  - [ ] `tool`: "package" in shipping vs. NuGet context
- [ ] Metrics for classification hint contribution to false positive reduction
- [ ] Configurable threshold testing (20%, 30%, 40%, 50%)

### Trigger Phrase Refinement Process

- [ ] `ITriggerRefinementService` interface defined with methods:
  - [ ] `AnalyzeTriggerEffectiveness(string docType)` - Returns effectiveness metrics
  - [ ] `SuggestRefinements(TriggerAnalysisResult analysis)` - Returns suggested changes
  - [ ] `ValidateRefinement(TriggerRefinement refinement)` - Tests proposed changes
- [ ] Refinement workflow documented:
  - [ ] Collect trigger event telemetry (captured, skipped, false positive reported)
  - [ ] Analyze patterns in false positives/negatives
  - [ ] Generate refinement suggestions (add, remove, modify phrases)
  - [ ] Validate refinements against test corpus
  - [ ] Update schema with approved refinements
- [ ] Backward compatibility for existing documents maintained during refinement
- [ ] Version tracking for trigger phrase sets (schema versioning)

### Test Cases for Each Doc-Type

#### Problem Doc-Type Tests

- [ ] True Positive Test Cases:
  - [ ] "I finally fixed the null reference exception in the authentication module"
  - [ ] "The bug was caused by a race condition in the database connection pool"
  - [ ] "Problem solved - the issue was an incorrect connection string"
  - [ ] "After debugging for hours, I found the error was in the serialization logic"
  - [ ] "The crash happened because the cache wasn't thread-safe"
- [ ] True Negative Test Cases:
  - [ ] "Let's discuss the project roadmap for next quarter"
  - [ ] "Can you help me write a new feature spec?"
  - [ ] "I prefer using Visual Studio over VS Code"
  - [ ] "The team meeting is scheduled for 3pm"
  - [ ] "Please review this pull request"
- [ ] False Positive Candidates (should NOT trigger due to classification hints):
  - [ ] "I fixed my lunch plans for tomorrow" (no debugging context)
  - [ ] "There was an error in my calendar entry" (no technical context)
  - [ ] "The bug in the garden ate my tomatoes" (no code context)

#### Insight Doc-Type Tests

- [ ] True Positive Test Cases:
  - [ ] "Users want real-time notifications rather than email digests"
  - [ ] "Interesting that customers prefer monthly billing over annual"
  - [ ] "The reason our churn is high is the onboarding complexity"
  - [ ] "Apparently, most users only use 3 features regularly"
  - [ ] "I realized the enterprise segment needs different pricing tiers"
- [ ] True Negative Test Cases:
  - [ ] "Let's implement the new authentication flow"
  - [ ] "The deployment script needs updating"
  - [ ] "Can you fix the CI pipeline?"
  - [ ] "We need to refactor the data access layer"
  - [ ] "Add more unit tests for the API endpoints"
- [ ] False Positive Candidates (should NOT trigger due to classification hints):
  - [ ] "I learned that recipe from my grandmother" (no product context)
  - [ ] "Apparently, it's going to rain tomorrow" (no business context)
  - [ ] "The reason I was late is traffic" (no domain context)

#### Codebase Doc-Type Tests

- [ ] True Positive Test Cases:
  - [ ] "We decided to use the repository pattern for data access"
  - [ ] "Our approach is to separate commands and queries (CQRS)"
  - [ ] "The architecture uses a clean layered structure with domain at the center"
  - [ ] "Going with dependency injection for all services"
  - [ ] "The pattern is to use value objects for all identifiers"
- [ ] True Negative Test Cases:
  - [ ] "Need to fix a bug in the login flow"
  - [ ] "Users are requesting dark mode"
  - [ ] "The deployment failed last night"
  - [ ] "Please add logging to this endpoint"
  - [ ] "The NuGet package needs updating"
- [ ] False Positive Candidates (should NOT trigger due to classification hints):
  - [ ] "I decided to have pizza for dinner" (no architecture context)
  - [ ] "Going with the blue shirt today" (no code context)
  - [ ] "Our approach to vacation is flexible" (no module/component context)

#### Tool Doc-Type Tests

- [ ] True Positive Test Cases:
  - [ ] "Gotcha with Newtonsoft.Json - it doesn't handle DateOnly correctly in v12"
  - [ ] "Watch out for Entity Framework Core's lazy loading - it can cause N+1 queries"
  - [ ] "Heads up: Polly's retry policy needs explicit timeout configuration"
  - [ ] "The NuGet package AutoMapper has a breaking change in v12"
  - [ ] "Careful with Serilog's file sink - it doesn't rotate by default"
- [ ] True Negative Test Cases:
  - [ ] "Let's discuss the sprint planning"
  - [ ] "The user interface needs a redesign"
  - [ ] "We should add more documentation"
  - [ ] "The performance is acceptable"
  - [ ] "Review the pull request by EOD"
- [ ] False Positive Candidates (should NOT trigger due to classification hints):
  - [ ] "Careful with the hot coffee" (no library context)
  - [ ] "Watch out for traffic on the highway" (no SDK context)
  - [ ] "The package arrived today" (no NuGet context - shipping package)

#### Style Doc-Type Tests

- [ ] True Positive Test Cases:
  - [ ] "Always use PascalCase for public methods and properties"
  - [ ] "Never use var for primitive types - be explicit"
  - [ ] "Our convention is to prefix private fields with underscore"
  - [ ] "The standard is async suffix for all async methods"
  - [ ] "Remember to add XML documentation for public APIs"
- [ ] True Negative Test Cases:
  - [ ] "The deployment completed successfully"
  - [ ] "Users reported a bug in the cart"
  - [ ] "The new feature shipped yesterday"
  - [ ] "Performance tests passed"
  - [ ] "Security audit scheduled for next week"
- [ ] False Positive Candidates (should NOT trigger due to classification hints):
  - [ ] "I always drink coffee in the morning" (no coding context)
  - [ ] "Never forget to call your mother" (no convention context)
  - [ ] "I prefer chocolate over vanilla" (no formatting context)

### Metrics for Trigger Accuracy

- [ ] `TriggerMetrics` record defined with properties:
  - [ ] `DocType` - Which doc-type being measured
  - [ ] `TruePositives` - Correct triggers on relevant content
  - [ ] `TrueNegatives` - Correct non-triggers on irrelevant content
  - [ ] `FalsePositives` - Incorrect triggers on irrelevant content
  - [ ] `FalseNegatives` - Missed triggers on relevant content
  - [ ] `Precision` - TP / (TP + FP)
  - [ ] `Recall` - TP / (TP + FN)
  - [ ] `F1Score` - 2 * (Precision * Recall) / (Precision + Recall)
  - [ ] `ClassificationHintContribution` - % of FP prevented by hints
- [ ] Metrics collection automated in test suite
- [ ] Metrics dashboard/report generation
- [ ] Threshold alerts for metric degradation

### Tuning Recommendations

- [ ] `TuningRecommendation` record defined with properties:
  - [ ] `DocType` - Affected doc-type
  - [ ] `RecommendationType` - enum: AddPhrase, RemovePhrase, ModifyPhrase, AdjustThreshold
  - [ ] `CurrentValue` - Current phrase/threshold
  - [ ] `ProposedValue` - Suggested change
  - [ ] `ExpectedImpact` - Predicted metric improvement
  - [ ] `Justification` - Reason for recommendation
- [ ] Automated recommendation engine based on:
  - [ ] Low precision: Suggests removing overly generic phrases
  - [ ] Low recall: Suggests adding missed trigger patterns
  - [ ] High false positives: Suggests adding classification hints
- [ ] Human review workflow before applying recommendations

---

## Implementation Notes

### Trigger Effectiveness Test Framework

```csharp
/// <summary>
/// Framework for testing trigger phrase effectiveness across doc-types.
/// </summary>
public interface ITriggerEffectivenessTestRunner
{
    /// <summary>
    /// Runs all trigger effectiveness tests for a specific doc-type.
    /// </summary>
    Task<DocTypeTriggerMetrics> RunTestsAsync(
        string docType,
        CancellationToken ct = default);

    /// <summary>
    /// Runs all trigger effectiveness tests for all doc-types.
    /// </summary>
    Task<TriggerEffectivenessReport> RunAllTestsAsync(
        CancellationToken ct = default);

    /// <summary>
    /// Validates a single test case and returns the result.
    /// </summary>
    Task<TestCaseResult> ValidateTestCaseAsync(
        TriggerTestCase testCase,
        CancellationToken ct = default);
}

/// <summary>
/// A single test case for trigger evaluation.
/// </summary>
public record TriggerTestCase
{
    public required string Id { get; init; }
    public required string DocType { get; init; }
    public required string ConversationContent { get; init; }
    public required TriggerTestExpectation Expectation { get; init; }
    public string? Description { get; init; }
    public string? Category { get; init; } // e.g., "false-positive-candidate"
}

public enum TriggerTestExpectation
{
    ShouldTrigger,      // True positive expected
    ShouldNotTrigger,   // True negative expected
    ShouldFilterByHints // Trigger phrase matches but hints should filter
}

/// <summary>
/// Result of a single test case validation.
/// </summary>
public record TestCaseResult
{
    public required string TestCaseId { get; init; }
    public required string DocType { get; init; }
    public required TriggerTestExpectation Expectation { get; init; }
    public required bool ActualTriggered { get; init; }
    public required bool TriggerPhrasesMatched { get; init; }
    public required double ClassificationScore { get; init; }
    public required bool TestPassed { get; init; }
    public IReadOnlyList<string> MatchedPhrases { get; init; } = [];
    public IReadOnlyList<string> MatchedHints { get; init; } = [];
    public string? FailureReason { get; init; }
}
```

### Trigger Effectiveness Test Runner Implementation

```csharp
public class TriggerEffectivenessTestRunner : ITriggerEffectivenessTestRunner
{
    private readonly ITriggerPhraseMatcherService _triggerMatcher;
    private readonly IClassificationValidator _classificationValidator;
    private readonly ISchemaClassificationLoader _schemaLoader;
    private readonly ITriggerTestCorpusProvider _testCorpus;
    private readonly ILogger<TriggerEffectivenessTestRunner> _logger;

    public TriggerEffectivenessTestRunner(
        ITriggerPhraseMatcherService triggerMatcher,
        IClassificationValidator classificationValidator,
        ISchemaClassificationLoader schemaLoader,
        ITriggerTestCorpusProvider testCorpus,
        ILogger<TriggerEffectivenessTestRunner> logger)
    {
        _triggerMatcher = triggerMatcher;
        _classificationValidator = classificationValidator;
        _schemaLoader = schemaLoader;
        _testCorpus = testCorpus;
        _logger = logger;
    }

    public async Task<DocTypeTriggerMetrics> RunTestsAsync(
        string docType,
        CancellationToken ct = default)
    {
        var testCases = await _testCorpus.GetTestCasesForDocTypeAsync(docType, ct);
        var (triggerPhrases, classificationHints) = _schemaLoader.LoadClassificationMetadata(docType);

        var results = new List<TestCaseResult>();

        foreach (var testCase in testCases)
        {
            var result = await EvaluateTestCaseAsync(
                testCase,
                triggerPhrases,
                classificationHints,
                ct);
            results.Add(result);
        }

        return CalculateMetrics(docType, results);
    }

    public async Task<TriggerEffectivenessReport> RunAllTestsAsync(CancellationToken ct = default)
    {
        var builtInTypes = new[] { "problem", "insight", "codebase", "tool", "style" };
        var allMetrics = new List<DocTypeTriggerMetrics>();

        foreach (var docType in builtInTypes)
        {
            var metrics = await RunTestsAsync(docType, ct);
            allMetrics.Add(metrics);

            _logger.LogInformation(
                "Trigger effectiveness for {DocType}: Precision={Precision:P1}, Recall={Recall:P1}, F1={F1:P1}",
                docType, metrics.Precision, metrics.Recall, metrics.F1Score);
        }

        return new TriggerEffectivenessReport
        {
            GeneratedAt = DateTimeOffset.UtcNow,
            DocTypeMetrics = allMetrics,
            OverallPrecision = allMetrics.Average(m => m.Precision),
            OverallRecall = allMetrics.Average(m => m.Recall),
            OverallF1Score = allMetrics.Average(m => m.F1Score)
        };
    }

    public async Task<TestCaseResult> ValidateTestCaseAsync(
        TriggerTestCase testCase,
        CancellationToken ct = default)
    {
        var (triggerPhrases, classificationHints) =
            _schemaLoader.LoadClassificationMetadata(testCase.DocType);

        return await EvaluateTestCaseAsync(testCase, triggerPhrases, classificationHints, ct);
    }

    private async Task<TestCaseResult> EvaluateTestCaseAsync(
        TriggerTestCase testCase,
        IReadOnlyList<string> triggerPhrases,
        IReadOnlyList<string> classificationHints,
        CancellationToken ct)
    {
        // Stage 1: Trigger phrase matching
        var matchedPhrases = _triggerMatcher.MatchTriggerPhrases(
            testCase.ConversationContent,
            triggerPhrases);

        var triggerPhrasesMatched = matchedPhrases.Count > 0;

        // Stage 2: Classification hint validation
        var classificationScore = 0.0;
        var matchedHints = new List<string>();

        if (triggerPhrasesMatched)
        {
            matchedHints = _triggerMatcher.MatchTriggerPhrases(
                testCase.ConversationContent,
                classificationHints).ToList();

            classificationScore = classificationHints.Count > 0
                ? (double)matchedHints.Count / classificationHints.Count
                : 1.0;
        }

        // Determine if would actually trigger (both stages pass)
        var actualTriggered = triggerPhrasesMatched && classificationScore >= 0.3;

        // Evaluate test result
        var (testPassed, failureReason) = EvaluateExpectation(
            testCase.Expectation,
            triggerPhrasesMatched,
            classificationScore,
            actualTriggered);

        return new TestCaseResult
        {
            TestCaseId = testCase.Id,
            DocType = testCase.DocType,
            Expectation = testCase.Expectation,
            ActualTriggered = actualTriggered,
            TriggerPhrasesMatched = triggerPhrasesMatched,
            ClassificationScore = classificationScore,
            TestPassed = testPassed,
            MatchedPhrases = matchedPhrases,
            MatchedHints = matchedHints,
            FailureReason = failureReason
        };
    }

    private static (bool Passed, string? FailureReason) EvaluateExpectation(
        TriggerTestExpectation expectation,
        bool triggerPhrasesMatched,
        double classificationScore,
        bool actualTriggered)
    {
        return expectation switch
        {
            TriggerTestExpectation.ShouldTrigger => actualTriggered
                ? (true, null)
                : (false, $"Expected trigger but did not trigger. " +
                    $"Phrases matched: {triggerPhrasesMatched}, Classification score: {classificationScore:P0}"),

            TriggerTestExpectation.ShouldNotTrigger => !actualTriggered
                ? (true, null)
                : (false, $"Expected no trigger but triggered. Classification score: {classificationScore:P0}"),

            TriggerTestExpectation.ShouldFilterByHints =>
                triggerPhrasesMatched && !actualTriggered
                    ? (true, null)
                    : triggerPhrasesMatched
                        ? (false, $"Classification hints did not filter. Score: {classificationScore:P0}")
                        : (false, "Expected trigger phrase match but none matched"),

            _ => throw new ArgumentOutOfRangeException(nameof(expectation))
        };
    }

    private static DocTypeTriggerMetrics CalculateMetrics(
        string docType,
        IReadOnlyList<TestCaseResult> results)
    {
        var truePositives = results.Count(r =>
            r.Expectation == TriggerTestExpectation.ShouldTrigger && r.ActualTriggered);

        var trueNegatives = results.Count(r =>
            r.Expectation == TriggerTestExpectation.ShouldNotTrigger && !r.ActualTriggered);

        var falsePositives = results.Count(r =>
            r.Expectation == TriggerTestExpectation.ShouldNotTrigger && r.ActualTriggered);

        var falseNegatives = results.Count(r =>
            r.Expectation == TriggerTestExpectation.ShouldTrigger && !r.ActualTriggered);

        // Count how many false positives were prevented by classification hints
        var filteredByHints = results.Count(r =>
            r.Expectation == TriggerTestExpectation.ShouldFilterByHints &&
            r.TriggerPhrasesMatched && !r.ActualTriggered);

        var totalFilterCandidates = results.Count(r =>
            r.Expectation == TriggerTestExpectation.ShouldFilterByHints);

        var precision = truePositives + falsePositives > 0
            ? (double)truePositives / (truePositives + falsePositives)
            : 0.0;

        var recall = truePositives + falseNegatives > 0
            ? (double)truePositives / (truePositives + falseNegatives)
            : 0.0;

        var f1Score = precision + recall > 0
            ? 2 * (precision * recall) / (precision + recall)
            : 0.0;

        var hintContribution = totalFilterCandidates > 0
            ? (double)filteredByHints / totalFilterCandidates
            : 0.0;

        return new DocTypeTriggerMetrics
        {
            DocType = docType,
            TotalTestCases = results.Count,
            TruePositives = truePositives,
            TrueNegatives = trueNegatives,
            FalsePositives = falsePositives,
            FalseNegatives = falseNegatives,
            Precision = precision,
            Recall = recall,
            F1Score = f1Score,
            ClassificationHintContribution = hintContribution,
            FailedTestCases = results.Where(r => !r.TestPassed).ToList()
        };
    }
}
```

### Metrics and Report Models

```csharp
/// <summary>
/// Trigger effectiveness metrics for a single doc-type.
/// </summary>
public record DocTypeTriggerMetrics
{
    public required string DocType { get; init; }
    public int TotalTestCases { get; init; }
    public int TruePositives { get; init; }
    public int TrueNegatives { get; init; }
    public int FalsePositives { get; init; }
    public int FalseNegatives { get; init; }
    public double Precision { get; init; }
    public double Recall { get; init; }
    public double F1Score { get; init; }
    public double ClassificationHintContribution { get; init; }
    public IReadOnlyList<TestCaseResult> FailedTestCases { get; init; } = [];

    public bool MeetsAcceptanceCriteria =>
        Precision >= 0.85 && Recall >= 0.90;
}

/// <summary>
/// Complete trigger effectiveness report for all doc-types.
/// </summary>
public record TriggerEffectivenessReport
{
    public DateTimeOffset GeneratedAt { get; init; }
    public IReadOnlyList<DocTypeTriggerMetrics> DocTypeMetrics { get; init; } = [];
    public double OverallPrecision { get; init; }
    public double OverallRecall { get; init; }
    public double OverallF1Score { get; init; }

    public bool AllDocTypesMeetCriteria =>
        DocTypeMetrics.All(m => m.MeetsAcceptanceCriteria);
}
```

### Trigger Refinement Service

```csharp
/// <summary>
/// Service for analyzing trigger effectiveness and suggesting refinements.
/// </summary>
public interface ITriggerRefinementService
{
    /// <summary>
    /// Analyzes trigger effectiveness for a doc-type.
    /// </summary>
    Task<TriggerAnalysisResult> AnalyzeTriggerEffectivenessAsync(
        string docType,
        CancellationToken ct = default);

    /// <summary>
    /// Suggests refinements based on analysis results.
    /// </summary>
    Task<IReadOnlyList<TriggerRefinementSuggestion>> SuggestRefinementsAsync(
        TriggerAnalysisResult analysis,
        CancellationToken ct = default);

    /// <summary>
    /// Validates a proposed refinement against the test corpus.
    /// </summary>
    Task<RefinementValidationResult> ValidateRefinementAsync(
        TriggerRefinementSuggestion refinement,
        CancellationToken ct = default);
}

public record TriggerAnalysisResult
{
    public required string DocType { get; init; }
    public required DocTypeTriggerMetrics CurrentMetrics { get; init; }
    public IReadOnlyList<string> OverlyGenericPhrases { get; init; } = [];
    public IReadOnlyList<string> MissedPatterns { get; init; } = [];
    public IReadOnlyList<string> IneffectiveHints { get; init; } = [];
}

public enum RefinementType
{
    AddTriggerPhrase,
    RemoveTriggerPhrase,
    ModifyTriggerPhrase,
    AddClassificationHint,
    RemoveClassificationHint,
    AdjustThreshold
}

public record TriggerRefinementSuggestion
{
    public required string DocType { get; init; }
    public required RefinementType Type { get; init; }
    public string? CurrentValue { get; init; }
    public string? ProposedValue { get; init; }
    public string Justification { get; init; } = "";
    public double ExpectedPrecisionDelta { get; init; }
    public double ExpectedRecallDelta { get; init; }
}

public record RefinementValidationResult
{
    public required TriggerRefinementSuggestion Refinement { get; init; }
    public required DocTypeTriggerMetrics MetricsBefore { get; init; }
    public required DocTypeTriggerMetrics MetricsAfter { get; init; }
    public bool Improved => MetricsAfter.F1Score > MetricsBefore.F1Score;
    public double F1ScoreDelta => MetricsAfter.F1Score - MetricsBefore.F1Score;
}
```

### Test Corpus Provider

```csharp
/// <summary>
/// Provides test cases for trigger effectiveness testing.
/// </summary>
public interface ITriggerTestCorpusProvider
{
    /// <summary>
    /// Gets all test cases for a specific doc-type.
    /// </summary>
    Task<IReadOnlyList<TriggerTestCase>> GetTestCasesForDocTypeAsync(
        string docType,
        CancellationToken ct = default);

    /// <summary>
    /// Gets all test cases across all doc-types.
    /// </summary>
    Task<IReadOnlyList<TriggerTestCase>> GetAllTestCasesAsync(
        CancellationToken ct = default);

    /// <summary>
    /// Adds a new test case to the corpus.
    /// </summary>
    Task AddTestCaseAsync(
        TriggerTestCase testCase,
        CancellationToken ct = default);
}

/// <summary>
/// File-based test corpus provider using JSON test case files.
/// </summary>
public class FileTriggerTestCorpusProvider : ITriggerTestCorpusProvider
{
    private readonly string _corpusPath;
    private readonly ILogger<FileTriggerTestCorpusProvider> _logger;

    public FileTriggerTestCorpusProvider(
        string corpusPath,
        ILogger<FileTriggerTestCorpusProvider> logger)
    {
        _corpusPath = corpusPath;
        _logger = logger;
    }

    public async Task<IReadOnlyList<TriggerTestCase>> GetTestCasesForDocTypeAsync(
        string docType,
        CancellationToken ct = default)
    {
        var filePath = Path.Combine(_corpusPath, $"{docType}-test-cases.json");

        if (!File.Exists(filePath))
        {
            _logger.LogWarning("Test corpus file not found: {FilePath}", filePath);
            return [];
        }

        var json = await File.ReadAllTextAsync(filePath, ct);
        var testCases = JsonSerializer.Deserialize<List<TriggerTestCase>>(json) ?? [];

        return testCases;
    }

    // ... implementation details
}
```

### Classification Threshold Tuning Tests

```csharp
/// <summary>
/// Tests for validating classification hint threshold effectiveness.
/// </summary>
public class ClassificationThresholdTuningTests
{
    private readonly ITriggerEffectivenessTestRunner _testRunner;

    [Theory]
    [InlineData(0.2)]
    [InlineData(0.3)]
    [InlineData(0.4)]
    [InlineData(0.5)]
    public async Task EvaluateThresholdEffectiveness(double threshold)
    {
        // Run tests with different thresholds
        // Compare precision/recall trade-offs
        // Document optimal threshold per doc-type
    }

    [Fact]
    public async Task StyleDocType_BenefitsFromHigherThreshold()
    {
        // Style has generic triggers like "always", "never"
        // Higher threshold should reduce false positives
    }

    [Fact]
    public async Task ProblemDocType_WorksWithLowerThreshold()
    {
        // Problem triggers are more specific
        // Lower threshold maintains recall without many false positives
    }
}
```

---

## File Structure

After implementation, the following files should exist:

```
src/CompoundDocs.Common/
├── Skills/
│   └── TriggerTuning/
│       ├── Abstractions/
│       │   ├── ITriggerEffectivenessTestRunner.cs
│       │   ├── ITriggerRefinementService.cs
│       │   └── ITriggerTestCorpusProvider.cs
│       ├── Models/
│       │   ├── TriggerTestCase.cs
│       │   ├── TriggerTestExpectation.cs
│       │   ├── TestCaseResult.cs
│       │   ├── DocTypeTriggerMetrics.cs
│       │   ├── TriggerEffectivenessReport.cs
│       │   ├── TriggerAnalysisResult.cs
│       │   ├── TriggerRefinementSuggestion.cs
│       │   └── RefinementValidationResult.cs
│       ├── TriggerEffectivenessTestRunner.cs
│       ├── TriggerRefinementService.cs
│       └── FileTriggerTestCorpusProvider.cs
tests/CompoundDocs.Tests/
└── Skills/
    └── TriggerTuning/
        ├── TriggerEffectivenessTests.cs
        ├── ClassificationHintEffectivenessTests.cs
        ├── ClassificationThresholdTuningTests.cs
        ├── TriggerRefinementServiceTests.cs
        ├── ProblemTriggerTests.cs
        ├── InsightTriggerTests.cs
        ├── CodebaseTriggerTests.cs
        ├── ToolTriggerTests.cs
        ├── StyleTriggerTests.cs
        └── TestCorpus/
            ├── problem-test-cases.json
            ├── insight-test-cases.json
            ├── codebase-test-cases.json
            ├── tool-test-cases.json
            └── style-test-cases.json
```

---

## Dependencies

### Depends On
- **Phase 082**: Auto-Invoke System (provides trigger matching, classification validation, schema classification loading)
- **Phase 017**: DI Container Setup (dependency injection)
- **Phase 018**: Logging Infrastructure (logging framework)
- **Phase 110**: xUnit Test Configuration (test framework setup)

### Blocks
- Trigger phrase updates to built-in schemas (awaits validation through this phase)
- Custom doc-type trigger phrase guidance (uses metrics from this phase)
- Production trigger monitoring (builds on test framework)

---

## Verification Steps

After completing this phase, verify:

1. **Test Suite Completeness**
   - All 5 built-in doc-types have 10+ test cases
   - True positive, true negative, and false positive candidate coverage
   - Test corpus files exist and are valid JSON

2. **Metrics Calculation**
   - Precision, recall, F1 score correctly calculated
   - Classification hint contribution metric accurate
   - Report generation works for all doc-types

3. **False Positive Prevention**
   - Generic "style" triggers ("always", "never") filtered in non-coding contexts
   - Generic "problem" triggers ("error", "fixed") filtered in non-debugging contexts
   - Classification hints demonstrably reduce false positives

4. **Refinement Workflow**
   - Analysis correctly identifies overly generic phrases
   - Suggestions include justification and expected impact
   - Validation compares before/after metrics

5. **Acceptance Thresholds**
   - All doc-types achieve 85%+ precision
   - All doc-types achieve 90%+ recall
   - Documentation of any exceptions with justification

---

## Testing Notes

### Key Test Scenarios

```csharp
// TriggerEffectivenessTests.cs
[Fact] public async Task RunAllTests_AllDocTypesMeetAcceptanceCriteria()
[Fact] public async Task ProblemDocType_AchievesTargetPrecision()
[Fact] public async Task StyleDocType_ClassificationHintsReduceFalsePositives()

// ClassificationHintEffectivenessTests.cs
[Fact] public async Task GenericTrigger_FilteredByMissingHints()
[Fact] public async Task SpecificTrigger_PassesWithMatchingHints()
[Fact] public async Task Threshold30Percent_BalancesPrecisionRecall()

// ProblemTriggerTests.cs
[Theory]
[InlineData("I finally fixed the null reference exception", true)]
[InlineData("I fixed my lunch plans", false)]
[InlineData("The error was in the serialization", true)]
[InlineData("There was an error in my calendar", false)]
public async Task Problem_TriggersCorrectly(string content, bool shouldTrigger)

// StyleTriggerTests.cs
[Theory]
[InlineData("Always use PascalCase for public methods", true)]
[InlineData("I always drink coffee in the morning", false)]
[InlineData("Our convention is underscore prefix for private fields", true)]
[InlineData("The convention center is downtown", false)]
public async Task Style_TriggersCorrectly(string content, bool shouldTrigger)
```

---

## Performance Considerations

- **Test Execution Speed**: Test suite should complete in < 10 seconds for all doc-types
- **Parallel Evaluation**: Test cases can run in parallel within a doc-type
- **Caching**: Compiled regex patterns should be cached for repeated matching
- **Corpus Size**: Balance thoroughness with maintenance burden (50-100 total test cases)

---

## Notes

- **Iterative Refinement**: This phase establishes baseline metrics. Actual trigger phrase changes should be made cautiously with full regression testing.
- **Living Test Corpus**: Test cases should be expanded over time as new edge cases are discovered in production.
- **Threshold Trade-offs**: Different doc-types may benefit from different classification thresholds. Document optimal per-type thresholds.
- **False Positive Reporting**: Consider adding user feedback mechanism to flag incorrect auto-invokes, feeding back into test corpus.
- **Spec Alignment**: Any trigger phrase changes must be reflected back in spec/doc-types/built-in-types.md to maintain consistency.
