# .NET Markdown Parser Library Research

**Date:** January 22, 2026
**Purpose:** Evaluate markdown parsing libraries for use in a C# MCP server requiring YAML frontmatter extraction, link parsing, header extraction, and code block preservation.

---

## Executive Summary

After comprehensive evaluation, **Markdig** is the clear recommendation for this project. It is the most actively maintained, feature-rich, and performant .NET markdown parser available. It provides full AST traversal capabilities, built-in YAML frontmatter support, and is officially recommended by Microsoft as a replacement for their deprecated Windows Community Toolkit parser.

---

## Library Comparison Matrix

| Feature | Markdig | CommonMark.NET | MarkedNet |
|---------|---------|----------------|-----------|
| **NuGet Version** | 0.44.0 | 0.15.1 | 2.1.4 |
| **License** | BSD-2-Clause | BSD-3-Clause | MIT |
| **Last Updated** | Nov 25, 2025 | Feb 20, 2017 | Jun 10, 2020 |
| **Total Downloads** | 50.2M | 6.9M | 118K |
| **GitHub Stars** | 5.1K | 1K | 38 |
| **Actively Maintained** | Yes | No (deprecated) | Limited |
| **.NET 8 Compatible** | Yes | Yes (legacy) | Yes |
| **AST Traversal** | Full | Limited | No |
| **YAML Frontmatter** | Built-in extension | No | No |
| **Extensible** | 20+ extensions | No | Limited |
| **CommonMark Compliant** | Yes (0.31.2) | Yes (0.27) | Partial |

---

## Detailed Library Evaluations

### 1. Markdig (Recommended)

**Package:** `Markdig`
**NuGet:** https://www.nuget.org/packages/Markdig/
**GitHub:** https://github.com/xoofx/markdig
**Version:** 0.44.0
**License:** BSD-2-Clause
**Last Updated:** November 25, 2025
**Downloads:** 50.2 million total

#### Target Frameworks
- .NET 8.0, .NET 9.0 (no dependencies)
- .NET Standard 2.0, 2.1 (System.Memory >= 4.6.3)
- .NET Framework 4.6.2+

#### Performance Characteristics
- Approximately 100x faster than MarkdownSharp
- 20% faster than the reference cmark C implementation
- No regex usage - pure parsing algorithms
- Very lightweight GC pressure
- Passes 600+ tests from CommonMark spec 0.31.2

#### Key Features for MCP Server Use Case
1. **Full AST with source locations** - Precise source code location tracking
2. **YAML frontmatter extension** - Built-in `UseYamlFrontMatter()` extension
3. **Pluggable parsing** - Can disable/modify built-in parsing behavior
4. **Roundtrip support** - Lossless parse-render for document modifications
5. **20+ built-in extensions** - Tables, footnotes, task lists, diagrams, etc.

#### API Examples

**Setup with Pipeline:**
```csharp
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Markdig.Extensions.Yaml;
using YamlDotNet.Serialization;

var pipeline = new MarkdownPipelineBuilder()
    .UseYamlFrontMatter()
    .UseAutoIdentifiers()
    .UsePreciseSourceLocation()
    .Build();

var document = Markdown.Parse(markdownContent, pipeline);
```

**Extracting YAML Frontmatter:**
```csharp
// Requires: dotnet add package YamlDotNet
var deserializer = new DeserializerBuilder()
    .IgnoreUnmatchedProperties()
    .Build();

var yamlBlock = document.Descendants<YamlFrontMatterBlock>().FirstOrDefault();
if (yamlBlock != null)
{
    var yaml = yamlBlock.Lines.ToString();
    var metadata = deserializer.Deserialize<Dictionary<string, object>>(yaml);
}
```

**Extracting All Links:**
```csharp
var links = document.Descendants<LinkInline>()
    .Where(link => !link.IsImage)
    .Select(link => new {
        Url = link.Url,
        Title = link.Title,
        Text = link.FirstChild?.ToString(),
        SourceSpan = link.Span
    })
    .ToList();
```

**Extracting Headers for Chunking:**
```csharp
var headers = document.Descendants<HeadingBlock>()
    .Select(h => new {
        Level = h.Level,
        Text = h.Inline?.FirstChild?.ToString(),
        Line = h.Line,
        SourceSpan = h.Span
    })
    .ToList();

// Build section hierarchy
var sections = new List<Section>();
HeadingBlock currentHeader = null;
foreach (var block in document)
{
    if (block is HeadingBlock heading)
    {
        currentHeader = heading;
        // Start new section
    }
    else
    {
        // Add block to current section
    }
}
```

**Extracting Code Blocks:**
```csharp
var codeBlocks = document.Descendants<FencedCodeBlock>()
    .Select(cb => new {
        Language = cb.Info,
        Code = cb.Lines.ToString(),
        Arguments = cb.Arguments,
        Line = cb.Line,
        SourceSpan = cb.Span
    })
    .ToList();

// Also get indented code blocks
var indentedCodeBlocks = document.Descendants<CodeBlock>()
    .Where(cb => cb is not FencedCodeBlock)
    .Select(cb => new {
        Code = cb.Lines.ToString(),
        Line = cb.Line
    })
    .ToList();
```

**Full Document Traversal:**
```csharp
foreach (var block in document)
{
    switch (block)
    {
        case HeadingBlock heading:
            ProcessHeading(heading);
            break;
        case ParagraphBlock paragraph:
            foreach (var inline in paragraph.Inline ?? Enumerable.Empty<Inline>())
            {
                if (inline is LinkInline link)
                    ProcessLink(link);
            }
            break;
        case FencedCodeBlock codeBlock:
            ProcessCodeBlock(codeBlock);
            break;
        case YamlFrontMatterBlock yaml:
            ProcessFrontmatter(yaml);
            break;
    }
}
```

#### Pros
- Most actively maintained .NET markdown library
- Excellent performance with minimal memory pressure
- Comprehensive AST with source location tracking
- Built-in YAML frontmatter support via extension
- Microsoft officially recommends Markdig over their deprecated toolkit
- Extensive extension ecosystem
- Well-documented with good community support
- Supports roundtrip parsing for document modification

#### Cons
- Requires YamlDotNet as additional dependency for frontmatter deserialization
- Learning curve for extension development
- Some extensions may need to be explicitly enabled

---

### 2. CommonMark.NET

**Package:** `CommonMark.NET`
**NuGet:** https://www.nuget.org/packages/CommonMark.NET/
**GitHub:** https://github.com/Knagis/CommonMark.NET
**Version:** 0.15.1
**License:** BSD-3-Clause
**Last Updated:** February 20, 2017
**Downloads:** 6.9 million total

**STATUS: DEPRECATED - No longer maintained**

#### Target Frameworks
- Broad compatibility from .NET 2.0 through modern .NET
- No external dependencies

#### Performance Characteristics
- Based on C port of reference cmark implementation
- Non-recursive algorithms prevent stack overflow
- Benchmark: 4ms compared to MarkdownSharp's 55ms

#### Key Features
- Strict CommonMark 0.27 compliance
- Syntax tree accessible for custom processing
- Zero dependencies
- Cross-platform support

#### Limitations for MCP Server Use Case
1. **No YAML frontmatter support** - Would require custom implementation
2. **No extension system** - Cannot add custom parsing features
3. **Limited AST capabilities** - Less comprehensive than Markdig
4. **Deprecated** - Repository explicitly states "no longer maintained" and recommends Markdig

#### API Example (Basic)
```csharp
using CommonMark;

// Convert to HTML
var html = CommonMarkConverter.Convert(markdown);

// Access syntax tree
var settings = CommonMarkSettings.Default.Clone();
var document = CommonMarkConverter.Parse(markdown, settings);
// Walk the tree manually - less convenient than Markdig's Descendants<T>()
```

#### Pros
- Zero dependencies
- Very stable codebase
- Good for simple markdown-to-HTML conversion

#### Cons
- **Deprecated and unmaintained**
- No YAML frontmatter support
- No extension system
- Limited AST traversal API
- Older CommonMark spec compliance (0.27 vs 0.31.2)

---

### 3. MarkedNet

**Package:** `MarkedNet`
**NuGet:** https://www.nuget.org/packages/MarkedNet/
**GitHub:** https://github.com/alex-titarenko/markednet
**Version:** 2.1.4
**License:** MIT
**Last Updated:** June 10, 2020
**Downloads:** 118K total

#### Target Frameworks
- .NET Standard 2.0
- .NET Framework 4.5.1
- No dependencies

#### Key Features
- Port of the JavaScript `marked` library
- Simple markdown to HTML conversion

#### Limitations for MCP Server Use Case
1. **No AST access** - Only provides HTML output, not a traversable parse tree
2. **No YAML frontmatter support**
3. **Limited maintenance** - Last update in 2020
4. **Small community** - Only 38 GitHub stars

#### API Example
```csharp
using MarkedNet;

var marked = new Marked();
var html = marked.Parse(markdown);
```

#### Pros
- Simple API
- No dependencies
- MIT license

#### Cons
- **No AST/parse tree access** - Cannot extract structural elements
- No frontmatter support
- Limited feature set
- Minimal maintenance activity

---

### 4. Other Libraries Evaluated

#### Westwind.AspNetCore.Markdown
- **Uses Markdig internally** - Just a wrapper for ASP.NET Core integration
- Adds TagHelper and middleware for web scenarios
- Not suitable for direct parsing/AST access in MCP server

#### markdown-it (.NET)
- **No native .NET port exists** - Only TypeScript bindings for Bridge.NET
- The JavaScript library is excellent, but no viable C# implementation

#### MarkdownSharp
- Legacy library from Stack Overflow
- Significantly slower than alternatives (100x slower than Markdig)
- No longer actively maintained
- No AST support

---

## Recommendation

### Primary Choice: Markdig

**Markdig is the definitive choice** for this MCP server project for the following reasons:

1. **Full AST Access**: Provides complete document traversal via `Descendants<T>()` API with precise source locations - essential for extracting links, headers, and code blocks.

2. **Built-in YAML Frontmatter**: The `UseYamlFrontMatter()` extension integrates seamlessly and exposes frontmatter as a `YamlFrontMatterBlock` in the AST.

3. **Active Maintenance**: Regular releases (latest Nov 2025), responsive maintainer, 5.1K GitHub stars, 50M+ NuGet downloads.

4. **Microsoft Endorsement**: Microsoft deprecated their own Windows Community Toolkit markdown parser and officially recommends Markdig.

5. **Performance**: Best-in-class performance for .NET, important when parsing many documents in an MCP server context.

6. **.NET 8 Native**: Full .NET 8 support with no additional dependencies for modern frameworks.

7. **Extensibility**: If additional parsing capabilities are needed (custom syntax, etc.), Markdig's extension system supports this.

### Implementation Notes

1. **Additional Dependency**: For YAML frontmatter deserialization, add `YamlDotNet` package:
   ```bash
   dotnet add package Markdig
   dotnet add package YamlDotNet
   ```

2. **Pipeline Configuration**: Create a shared pipeline instance for reuse:
   ```csharp
   public static class MarkdownPipelines
   {
       public static readonly MarkdownPipeline Default = new MarkdownPipelineBuilder()
           .UseYamlFrontMatter()
           .UseAutoIdentifiers()
           .UsePreciseSourceLocation()
           .Build();
   }
   ```

3. **Thread Safety**: The `MarkdownPipeline` is immutable and thread-safe. The `Markdown.Parse()` method is also thread-safe when using a shared pipeline.

---

## References

- [Markdig GitHub Repository](https://github.com/xoofx/markdig)
- [Markdig NuGet Package](https://www.nuget.org/packages/Markdig/)
- [Markdig AST Documentation](https://github.com/xoofx/markdig/blob/master/doc/parsing-ast.md)
- [CommonMark.NET GitHub (Deprecated)](https://github.com/Knagis/CommonMark.NET/)
- [Parse Markdown Front Matter With C# - Khalid Abuhakmeh](https://khalidabuhakmeh.com/parse-markdown-front-matter-with-csharp)
- [Rendering Markdown to HTML and Parsing YAML Front Matter in C# - Mark Heath](https://markheath.net/post/markdown-html-yaml-front-matter)
- [How to edit Markdown files in C# with Markdig - Luis Llamas](https://www.luisllamas.es/en/csharp-markdig/)
