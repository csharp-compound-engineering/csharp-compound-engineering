# Phase 015: Markdown Parser Integration (Markdig)

> **Status**: NOT_STARTED
> **Effort Estimate**: 4-6 hours
> **Category**: Infrastructure Setup
> **Prerequisites**: Phase 001 (Solution & Project Structure)

---

## Spec References

This phase implements markdown parsing infrastructure defined in:

- **SPEC.md** - [Technology Stack](../SPEC.md#technology-stack) (line 308: Markdig for link resolution)
- **spec/mcp-server/chunking.md** - [Chunking Algorithm](../spec/mcp-server/chunking.md#chunking-algorithm) (lines 99-125)
- **research/dotnet-markdown-parser-research.md** - Full library evaluation and API examples

---

## Objectives

1. Add Markdig and YamlDotNet NuGet packages to the solution
2. Create a shared `MarkdownPipeline` configuration with required extensions
3. Implement YAML frontmatter extraction service
4. Implement header hierarchy parsing for document chunking
5. Implement link extraction for cross-reference resolution
6. Implement code block handling for content preservation
7. Create unit tests for all parsing functionality

---

## Acceptance Criteria

### Package Integration
- [ ] `Markdig` package added to `CompoundDocs.Common` project
- [ ] `YamlDotNet` package added to `CompoundDocs.Common` project
- [ ] Package versions defined in `Directory.Packages.props`

### Pipeline Configuration
- [ ] Shared `MarkdownPipeline` instance created as singleton
- [ ] Pipeline configured with:
  - [ ] `UseYamlFrontMatter()` extension enabled
  - [ ] `UseAutoIdentifiers()` extension enabled
  - [ ] `UsePreciseSourceLocation()` extension enabled
- [ ] Pipeline is thread-safe and reusable across the application

### YAML Frontmatter Extraction
- [ ] `IFrontmatterParser` interface defined
- [ ] `MarkdigFrontmatterParser` implementation extracts frontmatter to `Dictionary<string, object>`
- [ ] Strongly-typed frontmatter models for each doc-type (problem, insight, codebase, tool, style)
- [ ] Graceful handling of missing or malformed frontmatter
- [ ] Line number tracking for frontmatter block (for error reporting)

### Header Hierarchy Parsing
- [ ] `IHeaderExtractor` interface defined
- [ ] Extracts all H1, H2, and H3 headers with:
  - [ ] Header level (1, 2, or 3)
  - [ ] Header text content
  - [ ] Source line number
  - [ ] Source span (start/end position)
- [ ] Builds hierarchical header path (e.g., `## Root Cause > ### Database Layer`)
- [ ] Identifies section boundaries for chunking
- [ ] Content extraction between headers (for chunk content)

### Link Extraction
- [ ] `ILinkExtractor` interface defined
- [ ] Extracts all markdown links with:
  - [ ] URL/path
  - [ ] Link title
  - [ ] Link text (visible text)
  - [ ] Source line number
  - [ ] Is relative path flag
- [ ] Filters out image links (`![](...)`)
- [ ] Supports both inline links `[text](url)` and reference links `[text][ref]`
- [ ] Handles anchor links (`#section-name`)

### Code Block Handling
- [ ] `ICodeBlockExtractor` interface defined
- [ ] Extracts fenced code blocks (```) with:
  - [ ] Language identifier
  - [ ] Code content
  - [ ] Arguments (if any)
  - [ ] Source line range
- [ ] Extracts indented code blocks
- [ ] Preserves code block boundaries for chunking (never split mid-block)

### Testing
- [ ] Unit tests for frontmatter parsing (valid, missing, malformed)
- [ ] Unit tests for header extraction (nested hierarchy, flat structure)
- [ ] Unit tests for link extraction (relative, absolute, anchors, images filtered)
- [ ] Unit tests for code block extraction (fenced, indented, multiple languages)
- [ ] Unit tests for full document parsing integration
- [ ] Test coverage meets 100% line/branch/method requirement

---

## Implementation Notes

### NuGet Package Installation

Add packages to `Directory.Packages.props`:

```xml
<ItemGroup>
  <!-- Markdown Parsing -->
  <PackageVersion Include="Markdig" Version="0.44.0" />
  <PackageVersion Include="YamlDotNet" Version="16.3.0" />
</ItemGroup>
```

Add package references in `CompoundDocs.Common.csproj`:

```xml
<ItemGroup>
  <PackageReference Include="Markdig" />
  <PackageReference Include="YamlDotNet" />
</ItemGroup>
```

### Shared Pipeline Configuration

Create a static pipeline factory in `CompoundDocs.Common/Markdown/MarkdownPipelines.cs`:

```csharp
using Markdig;

namespace CompoundDocs.Common.Markdown;

/// <summary>
/// Provides pre-configured Markdig pipelines for document parsing.
/// Pipelines are immutable and thread-safe.
/// </summary>
public static class MarkdownPipelines
{
    /// <summary>
    /// Default pipeline with YAML frontmatter, auto-identifiers, and precise source locations.
    /// </summary>
    public static readonly MarkdownPipeline Default = new MarkdownPipelineBuilder()
        .UseYamlFrontMatter()
        .UseAutoIdentifiers()
        .UsePreciseSourceLocation()
        .Build();

    /// <summary>
    /// Pipeline for parsing external documents (no auto-identifiers needed).
    /// </summary>
    public static readonly MarkdownPipeline External = new MarkdownPipelineBuilder()
        .UseYamlFrontMatter()
        .UsePreciseSourceLocation()
        .Build();
}
```

### Interface Definitions

Create interfaces in `CompoundDocs.Common/Markdown/Abstractions/`:

```csharp
// IFrontmatterParser.cs
public interface IFrontmatterParser
{
    FrontmatterResult Parse(string markdownContent);
    T? ParseAs<T>(string markdownContent) where T : class, new();
}

public record FrontmatterResult(
    Dictionary<string, object>? Data,
    int StartLine,
    int EndLine,
    bool IsPresent,
    string? ParseError
);

// IHeaderExtractor.cs
public interface IHeaderExtractor
{
    IReadOnlyList<HeaderInfo> Extract(string markdownContent);
    IReadOnlyList<DocumentSection> ExtractSections(string markdownContent);
}

public record HeaderInfo(
    int Level,
    string Text,
    int Line,
    int StartPosition,
    int EndPosition
);

public record DocumentSection(
    string HeaderPath,
    int StartLine,
    int EndLine,
    string Content,
    int HeaderLevel
);

// ILinkExtractor.cs
public interface ILinkExtractor
{
    IReadOnlyList<LinkInfo> Extract(string markdownContent);
    IReadOnlyList<LinkInfo> ExtractRelativeLinks(string markdownContent);
}

public record LinkInfo(
    string Url,
    string? Title,
    string Text,
    int Line,
    bool IsRelative,
    bool IsAnchorOnly
);

// ICodeBlockExtractor.cs
public interface ICodeBlockExtractor
{
    IReadOnlyList<CodeBlockInfo> Extract(string markdownContent);
}

public record CodeBlockInfo(
    string? Language,
    string Content,
    string? Arguments,
    int StartLine,
    int EndLine,
    bool IsFenced
);
```

### Frontmatter Parser Implementation

```csharp
using Markdig;
using Markdig.Extensions.Yaml;
using Markdig.Syntax;
using YamlDotNet.Serialization;

namespace CompoundDocs.Common.Markdown;

public class MarkdigFrontmatterParser : IFrontmatterParser
{
    private readonly IDeserializer _deserializer;

    public MarkdigFrontmatterParser()
    {
        _deserializer = new DeserializerBuilder()
            .IgnoreUnmatchedProperties()
            .Build();
    }

    public FrontmatterResult Parse(string markdownContent)
    {
        var document = Markdown.Parse(markdownContent, MarkdownPipelines.Default);
        var yamlBlock = document.Descendants<YamlFrontMatterBlock>().FirstOrDefault();

        if (yamlBlock is null)
        {
            return new FrontmatterResult(null, 0, 0, false, null);
        }

        try
        {
            var yaml = yamlBlock.Lines.ToString();
            var data = _deserializer.Deserialize<Dictionary<string, object>>(yaml);
            return new FrontmatterResult(data, yamlBlock.Line, yamlBlock.Line + yamlBlock.Lines.Count, true, null);
        }
        catch (Exception ex)
        {
            return new FrontmatterResult(null, yamlBlock.Line, yamlBlock.Line + yamlBlock.Lines.Count, true, ex.Message);
        }
    }

    public T? ParseAs<T>(string markdownContent) where T : class, new()
    {
        var document = Markdown.Parse(markdownContent, MarkdownPipelines.Default);
        var yamlBlock = document.Descendants<YamlFrontMatterBlock>().FirstOrDefault();

        if (yamlBlock is null) return null;

        var yaml = yamlBlock.Lines.ToString();
        return _deserializer.Deserialize<T>(yaml);
    }
}
```

### Header Extractor Implementation

```csharp
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace CompoundDocs.Common.Markdown;

public class MarkdigHeaderExtractor : IHeaderExtractor
{
    public IReadOnlyList<HeaderInfo> Extract(string markdownContent)
    {
        var document = Markdown.Parse(markdownContent, MarkdownPipelines.Default);

        return document.Descendants<HeadingBlock>()
            .Select(h => new HeaderInfo(
                Level: h.Level,
                Text: GetHeaderText(h),
                Line: h.Line,
                StartPosition: h.Span.Start,
                EndPosition: h.Span.End
            ))
            .ToList();
    }

    public IReadOnlyList<DocumentSection> ExtractSections(string markdownContent)
    {
        var document = Markdown.Parse(markdownContent, MarkdownPipelines.Default);
        var lines = markdownContent.Split('\n');
        var sections = new List<DocumentSection>();
        var headerStack = new Stack<(int Level, string Text, int Line)>();

        var headings = document.Descendants<HeadingBlock>().ToList();

        for (int i = 0; i < headings.Count; i++)
        {
            var heading = headings[i];
            var nextHeading = i + 1 < headings.Count ? headings[i + 1] : null;

            // Update header stack for hierarchy
            while (headerStack.Count > 0 && headerStack.Peek().Level >= heading.Level)
            {
                headerStack.Pop();
            }
            headerStack.Push((heading.Level, GetHeaderText(heading), heading.Line));

            // Build header path
            var headerPath = string.Join(" > ",
                headerStack.Reverse().Select(h => $"{new string('#', h.Level)} {h.Text}"));

            // Calculate section boundaries
            var startLine = heading.Line;
            var endLine = nextHeading?.Line - 1 ?? lines.Length - 1;

            // Extract content
            var content = string.Join("\n", lines.Skip(startLine).Take(endLine - startLine + 1));

            sections.Add(new DocumentSection(
                HeaderPath: headerPath,
                StartLine: startLine,
                EndLine: endLine,
                Content: content,
                HeaderLevel: heading.Level
            ));
        }

        return sections;
    }

    private static string GetHeaderText(HeadingBlock heading)
    {
        return heading.Inline?.FirstChild?.ToString() ?? string.Empty;
    }
}
```

### Link Extractor Implementation

```csharp
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace CompoundDocs.Common.Markdown;

public class MarkdigLinkExtractor : ILinkExtractor
{
    public IReadOnlyList<LinkInfo> Extract(string markdownContent)
    {
        var document = Markdown.Parse(markdownContent, MarkdownPipelines.Default);

        return document.Descendants<LinkInline>()
            .Where(link => !link.IsImage)
            .Select(link => new LinkInfo(
                Url: link.Url ?? string.Empty,
                Title: link.Title,
                Text: GetLinkText(link),
                Line: link.Line,
                IsRelative: IsRelativePath(link.Url),
                IsAnchorOnly: link.Url?.StartsWith('#') ?? false
            ))
            .ToList();
    }

    public IReadOnlyList<LinkInfo> ExtractRelativeLinks(string markdownContent)
    {
        return Extract(markdownContent)
            .Where(link => link.IsRelative && !link.IsAnchorOnly)
            .ToList();
    }

    private static string GetLinkText(LinkInline link)
    {
        return link.FirstChild?.ToString() ?? string.Empty;
    }

    private static bool IsRelativePath(string? url)
    {
        if (string.IsNullOrEmpty(url)) return false;
        if (url.StartsWith('#')) return true;
        if (url.StartsWith("http://") || url.StartsWith("https://")) return false;
        if (url.StartsWith("mailto:") || url.StartsWith("tel:")) return false;
        return true;
    }
}
```

### Code Block Extractor Implementation

```csharp
using Markdig;
using Markdig.Syntax;

namespace CompoundDocs.Common.Markdown;

public class MarkdigCodeBlockExtractor : ICodeBlockExtractor
{
    public IReadOnlyList<CodeBlockInfo> Extract(string markdownContent)
    {
        var document = Markdown.Parse(markdownContent, MarkdownPipelines.Default);
        var codeBlocks = new List<CodeBlockInfo>();

        // Fenced code blocks
        codeBlocks.AddRange(document.Descendants<FencedCodeBlock>()
            .Select(cb => new CodeBlockInfo(
                Language: cb.Info,
                Content: cb.Lines.ToString(),
                Arguments: cb.Arguments,
                StartLine: cb.Line,
                EndLine: cb.Line + cb.Lines.Count,
                IsFenced: true
            )));

        // Indented code blocks
        codeBlocks.AddRange(document.Descendants<CodeBlock>()
            .Where(cb => cb is not FencedCodeBlock)
            .Select(cb => new CodeBlockInfo(
                Language: null,
                Content: cb.Lines.ToString(),
                Arguments: null,
                StartLine: cb.Line,
                EndLine: cb.Line + cb.Lines.Count,
                IsFenced: false
            )));

        return codeBlocks.OrderBy(cb => cb.StartLine).ToList();
    }
}
```

### Frontmatter Models

Create strongly-typed models in `CompoundDocs.Common/Models/Frontmatter/`:

```csharp
// BaseFrontmatter.cs
public abstract class BaseFrontmatter
{
    public string Title { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string DocType { get; set; } = string.Empty;
    public string PromotionLevel { get; set; } = "standard";
}

// ProblemFrontmatter.cs
public class ProblemFrontmatter : BaseFrontmatter
{
    public string ProblemType { get; set; } = string.Empty; // bug, performance, design, etc.
    public string Severity { get; set; } = string.Empty; // low, medium, high, critical
    public List<string> Symptoms { get; set; } = new();
    public string RootCause { get; set; } = string.Empty;
    public string Solution { get; set; } = string.Empty;
}

// InsightFrontmatter.cs
public class InsightFrontmatter : BaseFrontmatter
{
    public string InsightType { get; set; } = string.Empty; // product, project, technical
    public List<string> Tags { get; set; } = new();
}

// CodebaseFrontmatter.cs
public class CodebaseFrontmatter : BaseFrontmatter
{
    public string Area { get; set; } = string.Empty; // architecture, pattern, convention
    public List<string> RelatedFiles { get; set; } = new();
}

// ToolFrontmatter.cs
public class ToolFrontmatter : BaseFrontmatter
{
    public string ToolName { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty; // library, framework, utility
}

// StyleFrontmatter.cs
public class StyleFrontmatter : BaseFrontmatter
{
    public string StyleType { get; set; } = string.Empty; // naming, formatting, pattern
    public string Scope { get; set; } = string.Empty; // project, team, organization
}
```

### Composite Document Parser

Create a facade that combines all extractors for convenient document analysis:

```csharp
namespace CompoundDocs.Common.Markdown;

public interface IDocumentParser
{
    ParsedDocument Parse(string markdownContent);
}

public record ParsedDocument(
    FrontmatterResult Frontmatter,
    IReadOnlyList<HeaderInfo> Headers,
    IReadOnlyList<DocumentSection> Sections,
    IReadOnlyList<LinkInfo> Links,
    IReadOnlyList<CodeBlockInfo> CodeBlocks,
    int TotalLines
);

public class MarkdigDocumentParser : IDocumentParser
{
    private readonly IFrontmatterParser _frontmatterParser;
    private readonly IHeaderExtractor _headerExtractor;
    private readonly ILinkExtractor _linkExtractor;
    private readonly ICodeBlockExtractor _codeBlockExtractor;

    public MarkdigDocumentParser(
        IFrontmatterParser frontmatterParser,
        IHeaderExtractor headerExtractor,
        ILinkExtractor linkExtractor,
        ICodeBlockExtractor codeBlockExtractor)
    {
        _frontmatterParser = frontmatterParser;
        _headerExtractor = headerExtractor;
        _linkExtractor = linkExtractor;
        _codeBlockExtractor = codeBlockExtractor;
    }

    public ParsedDocument Parse(string markdownContent)
    {
        var lines = markdownContent.Split('\n');

        return new ParsedDocument(
            Frontmatter: _frontmatterParser.Parse(markdownContent),
            Headers: _headerExtractor.Extract(markdownContent),
            Sections: _headerExtractor.ExtractSections(markdownContent),
            Links: _linkExtractor.Extract(markdownContent),
            CodeBlocks: _codeBlockExtractor.Extract(markdownContent),
            TotalLines: lines.Length
        );
    }
}
```

### Dependency Injection Registration

```csharp
using Microsoft.Extensions.DependencyInjection;

namespace CompoundDocs.Common.Markdown;

public static class MarkdownServiceCollectionExtensions
{
    public static IServiceCollection AddMarkdownParsing(this IServiceCollection services)
    {
        services.AddSingleton<IFrontmatterParser, MarkdigFrontmatterParser>();
        services.AddSingleton<IHeaderExtractor, MarkdigHeaderExtractor>();
        services.AddSingleton<ILinkExtractor, MarkdigLinkExtractor>();
        services.AddSingleton<ICodeBlockExtractor, MarkdigCodeBlockExtractor>();
        services.AddSingleton<IDocumentParser, MarkdigDocumentParser>();

        return services;
    }
}
```

---

## File Structure

After completion, the following files should exist:

```
src/CompoundDocs.Common/
├── Markdown/
│   ├── Abstractions/
│   │   ├── IFrontmatterParser.cs
│   │   ├── IHeaderExtractor.cs
│   │   ├── ILinkExtractor.cs
│   │   ├── ICodeBlockExtractor.cs
│   │   └── IDocumentParser.cs
│   ├── MarkdownPipelines.cs
│   ├── MarkdigFrontmatterParser.cs
│   ├── MarkdigHeaderExtractor.cs
│   ├── MarkdigLinkExtractor.cs
│   ├── MarkdigCodeBlockExtractor.cs
│   ├── MarkdigDocumentParser.cs
│   └── MarkdownServiceCollectionExtensions.cs
├── Models/
│   └── Frontmatter/
│       ├── BaseFrontmatter.cs
│       ├── ProblemFrontmatter.cs
│       ├── InsightFrontmatter.cs
│       ├── CodebaseFrontmatter.cs
│       ├── ToolFrontmatter.cs
│       └── StyleFrontmatter.cs
tests/CompoundDocs.Tests/
└── Markdown/
    ├── FrontmatterParserTests.cs
    ├── HeaderExtractorTests.cs
    ├── LinkExtractorTests.cs
    ├── CodeBlockExtractorTests.cs
    └── DocumentParserIntegrationTests.cs
```

---

## Dependencies

### Depends On
- Phase 001: Solution & Project Structure (solution file, directory structure)
- Phase 002: CompoundDocs.Common Library (project must exist to add packages)

### Blocks
- Phase XXX: Document Chunking Service (requires header extraction)
- Phase XXX: Document Indexing Service (requires frontmatter parsing)
- Phase XXX: Cross-Reference Resolution (requires link extraction)
- Phase XXX: RAG Query Tool (requires full document parsing)

---

## Verification Steps

After completing this phase, verify:

1. **Package installation**: `dotnet restore` succeeds with Markdig and YamlDotNet
2. **Build**: `dotnet build` completes without errors
3. **Tests pass**: `dotnet test` runs all markdown parsing tests successfully
4. **Coverage**: Code coverage report shows 100% for all markdown parsing code
5. **Thread safety**: Verify pipeline can be used concurrently (run parallel test)

### Sample Test Document

Use this document to verify all extractors:

```markdown
---
title: Test Document
date: 2025-01-24
doc_type: problem
severity: high
symptoms:
  - slow performance
  - memory leaks
---

# Main Title

Introduction paragraph with a [relative link](./other-doc.md) and an [absolute link](https://example.com).

## Section One

Content under section one with [anchor link](#section-two).

### Subsection 1.1

```csharp
public class Example
{
    public void Test() { }
}
```

## Section Two

More content here.

![This is an image](./image.png)
```

---

## Notes

- The `MarkdownPipeline` is immutable and thread-safe; use the static instances for best performance
- YamlDotNet's `IgnoreUnmatchedProperties()` ensures forward compatibility as frontmatter schemas evolve
- Source line numbers from Markdig are 0-based; convert to 1-based for user-facing display
- The chunking algorithm (defined in spec/mcp-server/chunking.md) will use `ExtractSections()` to identify chunk boundaries
- Code blocks should never be split during chunking; the `CodeBlockExtractor` provides boundaries to respect
