# .NET Multi-Platform Publishing Research

> Comprehensive guide for compiling .NET applications for multiple platforms (Windows, macOS, Linux) while outputting single self-contained executable files.

**Research Date:** January 2026
**Target Use Case:** MCP server distribution as self-contained executables

---

## Table of Contents

1. [Runtime Identifiers (RIDs)](#1-runtime-identifiers-rids)
2. [Self-Contained Deployment](#2-self-contained-deployment)
3. [Single-File Publishing](#3-single-file-publishing)
4. [Project File Configuration (.csproj)](#4-project-file-configuration-csproj)
5. [dotnet publish Command](#5-dotnet-publish-command)
6. [Trimming for Smaller Executables](#6-trimming-for-smaller-executables)
7. [ReadyToRun Compilation](#7-readytorun-compilation)
8. [Native AOT (Ahead of Time)](#8-native-aot-ahead-of-time)
9. [Cross-Compilation](#9-cross-compilation)
10. [Build Scripts and Automation](#10-build-scripts-and-automation)
11. [Output Organization](#11-output-organization)
12. [Complete Examples](#12-complete-examples)
13. [Common Issues and Solutions](#13-common-issues-and-solutions)

---

## 1. Runtime Identifiers (RIDs)

### What Are RIDs?

**RID** stands for **Runtime Identifier**. RIDs are used to identify target platforms where .NET applications run. They are essential for:

- Identifying target platforms in NuGet packages
- Representing platform-specific assets
- Specifying which platforms can restore packages with native dependencies

### RID Naming Pattern

Concrete operating system RIDs follow this pattern:

```
[os].[version]-[architecture]-[additional qualifiers]
```

- **`[os]`**: OS/platform moniker (e.g., `ubuntu`, `win`, `osx`)
- **`[version]`**: OS version as dot-separated numbers (e.g., `15.10`)
- **`[architecture]`**: Processor architecture (`x86`, `x64`, `arm`, `arm64`)
- **`[additional qualifiers]`**: Further differentiators (e.g., `aot`)

### Common RIDs for MCP Server Distribution

#### Windows RIDs
| RID | Description |
|-----|-------------|
| `win-x64` | 64-bit Windows |
| `win-x86` | 32-bit Windows |
| `win-arm64` | ARM 64-bit Windows |

#### Linux RIDs
| RID | Description |
|-----|-------------|
| `linux-x64` | Most desktop distributions (CentOS, Debian, Fedora, Ubuntu) |
| `linux-arm64` | 64-bit ARM (Raspberry Pi 3+, Ubuntu Server on ARM) |
| `linux-arm` | 32-bit ARM (Raspbian on Raspberry Pi 2+) |
| `linux-musl-x64` | Lightweight distributions using musl (Alpine Linux) |
| `linux-musl-arm64` | Alpine Linux on ARM64 |

#### macOS RIDs
| RID | Description |
|-----|-------------|
| `osx-x64` | Intel Mac (minimum: macOS 10.12 Sierra) |
| `osx-arm64` | Apple Silicon (M1, M2, M3, M4) |

### Portable vs Platform-Specific RIDs

#### Portable RIDs (Recommended)
- **Not tied** to specific OS versions or distributions
- Format: `[os]-[architecture]` (e.g., `linux-x64`, `win-arm64`)
- **Best for:** Building platform-specific applications and NuGet packages

#### Platform-Specific RIDs (Legacy - Not Recommended)
- Tied to specific OS versions or distributions
- Example: `ubuntu.20.04-x64` (no longer recommended)

### .NET 8+ Changes

Starting with .NET 8:
- SDK uses portable RID graph (non-version-specific, non-distro-specific)
- `RuntimeInformation.RuntimeIdentifier` returns the platform the runtime was built for
- Version-specific RID file (`runtime.json`) is no longer updated

### Using RIDs in Project Files

**Single RID:**
```xml
<PropertyGroup>
  <RuntimeIdentifier>win-x64</RuntimeIdentifier>
</PropertyGroup>
```

**Multiple RIDs:**
```xml
<PropertyGroup>
  <RuntimeIdentifiers>win-x64;linux-x64;osx-arm64</RuntimeIdentifiers>
</PropertyGroup>
```

### RID Graph (Fallback Hierarchy)

The RID graph defines compatible RIDs. When NuGet restores packages:
1. Tries to find an exact match for the specified RID
2. If not found, walks back the graph to find the closest compatible system

**Fallback hierarchy example:**
```
linux-arm64     linux-x64
     |     \   /     |
     |     linux     |
     |       |       |
unix-arm64   |    unix-x64
         \   |   /
           unix
             |
            any
```

---

## 2. Self-Contained Deployment

### Framework-Dependent vs Self-Contained

| Feature | Framework-Dependent | Self-Contained |
|---------|--------------------| ----------------|
| .NET Runtime | Required on target | Included in output |
| Deployment Size | Small (app + dependencies) | Large (includes runtime) |
| Security Patches | Uses latest patched runtime | Must redeploy for patches |
| Portability | Requires matching runtime | Runs anywhere |
| Typical Use | Internal deployments | Distribution to end users |

### Enabling Self-Contained Deployment

**In .csproj:**
```xml
<PropertyGroup>
  <SelfContained>true</SelfContained>
  <RuntimeIdentifier>win-x64</RuntimeIdentifier>
</PropertyGroup>
```

**Via CLI:**
```bash
dotnet publish -r win-x64 --self-contained true
```

### RuntimeIdentifier vs RuntimeIdentifiers

| Property | Usage |
|----------|-------|
| `RuntimeIdentifier` (singular) | Single target platform; used for actual build |
| `RuntimeIdentifiers` (plural) | List of platforms; used for restore; must publish separately |

**Important:** You cannot publish to multiple RIDs in a single command. With `RuntimeIdentifiers`, you must run `dotnet publish -r <rid>` for each target.

### Output Contents

Self-contained deployment includes:
- Your application DLL/EXE
- All NuGet dependencies
- .NET runtime libraries
- Native host executable (apphost)
- `*.deps.json` and `*.runtimeconfig.json` files

---

## 3. Single-File Publishing

### How Single-File Bundling Works

Single-file deployment bundles all application-dependent files into a single binary:
- **Managed DLLs**: Bundled and loaded from memory
- **Native libraries**: By default, remain separate (can be embedded)
- **Configuration files**: `*.runtimeconfig.json` and `*.deps.json` included

### Key Properties

```xml
<PropertyGroup>
  <PublishSingleFile>true</PublishSingleFile>
</PropertyGroup>
```

### Extraction vs In-Memory Loading

| Mode | Behavior | Use Case |
|------|----------|----------|
| **In-Memory (Default)** | Managed DLLs loaded from memory | Most applications |
| **Extraction** | Files extracted to disk before loading | Compatibility issues |

### Embedding Native Libraries

```xml
<PropertyGroup>
  <!-- Embed native libraries in the single file -->
  <IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>
</PropertyGroup>
```

### Full Extraction Mode

```xml
<PropertyGroup>
  <!-- Extract all files to disk before running -->
  <IncludeAllContentForSelfExtract>true</IncludeAllContentForSelfExtract>
</PropertyGroup>
```

Extraction locations:
- **Linux/macOS**: `$HOME/.net`
- **Windows**: `%TEMP%/.net`
- **Custom**: Set `DOTNET_BUNDLE_EXTRACT_BASE_DIR` environment variable

### Enabling Compression

```xml
<PropertyGroup>
  <EnableCompressionInSingleFile>true</EnableCompressionInSingleFile>
</PropertyGroup>
```

**Trade-off:** Smaller file size vs. slower startup (decompression overhead)

### Excluding Files from Bundle

```xml
<ItemGroup>
  <Content Update="Plugin.dll">
    <CopyToPublishDirectory>PreserveNewest</CopyToPublishDirectory>
    <ExcludeFromSingleFile>true</ExcludeFromSingleFile>
  </Content>
</ItemGroup>
```

### Embedding PDB Files

```xml
<PropertyGroup>
  <DebugType>embedded</DebugType>
</PropertyGroup>
```

---

## 4. Project File Configuration (.csproj)

### Complete Multi-Platform Configuration

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>

    <!-- Multi-platform targets (for restore) -->
    <RuntimeIdentifiers>win-x64;win-arm64;linux-x64;linux-arm64;osx-x64;osx-arm64</RuntimeIdentifiers>

    <!-- Self-contained deployment -->
    <SelfContained>true</SelfContained>

    <!-- Single-file output -->
    <PublishSingleFile>true</PublishSingleFile>
    <IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>

    <!-- Size optimization -->
    <PublishTrimmed>true</PublishTrimmed>
    <EnableCompressionInSingleFile>true</EnableCompressionInSingleFile>

    <!-- Startup optimization -->
    <PublishReadyToRun>true</PublishReadyToRun>

    <!-- Debug symbols -->
    <DebugType>embedded</DebugType>
  </PropertyGroup>
</Project>
```

### Property Reference

| Property | Description | Default |
|----------|-------------|---------|
| `SelfContained` | Include .NET runtime | `false` |
| `PublishSingleFile` | Bundle into single file | `false` |
| `PublishTrimmed` | Remove unused code | `false` |
| `PublishReadyToRun` | AOT compile for faster startup | `false` |
| `PublishAot` | Native AOT compilation | `false` |
| `EnableCompressionInSingleFile` | Compress bundled assemblies | `false` |
| `IncludeNativeLibrariesForSelfExtract` | Embed native libs | `false` |
| `IncludeAllContentForSelfExtract` | Extract all at runtime | `false` |
| `DebugType` | Symbol handling (`embedded`, `portable`, `full`) | `portable` |

---

## 5. dotnet publish Command

### Basic Syntax

```bash
dotnet publish [<PROJECT>] [options]
```

### Essential Options

| Option | Description |
|--------|-------------|
| `-c\|--configuration` | Build configuration (`Debug`, `Release`) |
| `-r\|--runtime` | Target runtime identifier |
| `-f\|--framework` | Target framework |
| `-o\|--output` | Output directory |
| `--self-contained` | Include .NET runtime |
| `--no-self-contained` | Framework-dependent deployment |
| `-p:<PROPERTY>=<VALUE>` | Set MSBuild property |

### Publishing for Specific Platforms

**Single platform:**
```bash
# Windows x64
dotnet publish -c Release -r win-x64 --self-contained

# Linux x64
dotnet publish -c Release -r linux-x64 --self-contained

# macOS ARM64 (Apple Silicon)
dotnet publish -c Release -r osx-arm64 --self-contained
```

**With single-file:**
```bash
dotnet publish -c Release -r win-x64 -p:PublishSingleFile=true --self-contained
```

**With all optimizations:**
```bash
dotnet publish -c Release -r linux-x64 \
  -p:PublishSingleFile=true \
  -p:PublishTrimmed=true \
  -p:EnableCompressionInSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  --self-contained
```

### Output Paths

**Default output:**
- Framework-dependent: `./bin/<CONFIG>/<TFM>/publish/`
- Self-contained: `./bin/<CONFIG>/<TFM>/<RID>/publish/`

**Custom output:**
```bash
dotnet publish -c Release -r win-x64 -o ./publish/win-x64
```

### Building All Platforms (Separate Commands Required)

```bash
# Cannot build multiple RIDs in one command
# Must run separately for each target

dotnet publish -c Release -r win-x64 -o ./artifacts/win-x64
dotnet publish -c Release -r win-arm64 -o ./artifacts/win-arm64
dotnet publish -c Release -r linux-x64 -o ./artifacts/linux-x64
dotnet publish -c Release -r linux-arm64 -o ./artifacts/linux-arm64
dotnet publish -c Release -r osx-x64 -o ./artifacts/osx-x64
dotnet publish -c Release -r osx-arm64 -o ./artifacts/osx-arm64
```

---

## 6. Trimming for Smaller Executables

### Enabling Trimming

**In .csproj:**
```xml
<PropertyGroup>
  <PublishTrimmed>true</PublishTrimmed>
</PropertyGroup>
```

**Via CLI:**
```bash
dotnet publish -r win-x64 -p:PublishTrimmed=true
```

### Trim Modes

| Mode | Description |
|------|-------------|
| `full` (default) | Aggressively trims all unused code |
| `partial` | Only trims assemblies marked as `IsTrimmable` |

```xml
<PropertyGroup>
  <TrimMode>full</TrimMode>
</PropertyGroup>
```

### Preserving Specific Assemblies

```xml
<ItemGroup>
  <TrimmerRootAssembly Include="MyPlugin" />
</ItemGroup>
```

### Framework Feature Trimming

Disable specific framework features for smaller output:

```xml
<PropertyGroup>
  <!-- Remove globalization data -->
  <InvariantGlobalization>true</InvariantGlobalization>

  <!-- Remove debugger support -->
  <DebuggerSupport>false</DebuggerSupport>

  <!-- Remove EventSource support -->
  <EventSourceSupport>false</EventSourceSupport>

  <!-- Remove HTTP/3 support -->
  <Http3Support>false</Http3Support>

  <!-- Use minimal exception messages -->
  <UseSystemResourceKeys>true</UseSystemResourceKeys>
</PropertyGroup>
```

### Handling Reflection Warnings

**Using DynamicallyAccessedMembersAttribute:**
```csharp
public void Process([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type type)
{
    // Reflection-based code here
}
```

**Suppressing warnings (last resort):**
```csharp
[UnconditionalSuppressMessage("Trimming", "IL2026",
    Justification = "This code path is safe because...")]
public void MyMethod() { }
```

### Size Reduction Expectations

| Configuration | Typical Size | Notes |
|---------------|--------------|-------|
| Self-contained (no trim) | 60-150+ MB | Includes full .NET runtime |
| With PublishTrimmed | 10-30 MB | Depends on dependencies |
| With compression | 30-50% smaller | Adds startup latency |
| Native AOT | 5-20 MB | Best size, most restrictions |

---

## 7. ReadyToRun Compilation

### What is ReadyToRun (R2R)?

ReadyToRun is ahead-of-time (AOT) compilation that pre-compiles assemblies to native code, reducing JIT work at startup.

### Enabling R2R

**In .csproj:**
```xml
<PropertyGroup>
  <PublishReadyToRun>true</PublishReadyToRun>
</PropertyGroup>
```

**Via CLI:**
```bash
dotnet publish -c Release -r win-x64 -p:PublishReadyToRun=true
```

### Trade-offs

| Aspect | Impact |
|--------|--------|
| **Startup Time** | Significantly improved |
| **File Size** | 2-3x larger |
| **Build Time** | Slower compilation |
| **Runtime Performance** | Comparable (tiered compilation) |

### Advanced Options

**Generate symbols for profilers:**
```xml
<PropertyGroup>
  <PublishReadyToRunEmitSymbols>true</PublishReadyToRunEmitSymbols>
</PropertyGroup>
```

**Composite R2R (better optimization, slower build):**
```xml
<PropertyGroup>
  <PublishReadyToRunComposite>true</PublishReadyToRunComposite>
</PropertyGroup>
```

**Exclude specific assemblies:**
```xml
<ItemGroup>
  <PublishReadyToRunExclude Include="Contoso.Example.dll" />
</ItemGroup>
```

### When to Use R2R

**Use R2R for:**
- Large applications with many assemblies
- Startup-time sensitive scenarios
- Server applications that must be responsive quickly

**Skip R2R for:**
- Small applications (minimal benefit)
- When file size is critical
- Short-lived processes

---

## 8. Native AOT (Ahead of Time)

### What is Native AOT?

Native AOT compiles .NET code directly to native machine code at publish time, eliminating the need for JIT compilation at runtime.

### Key Benefits

- **Faster startup** - No JIT compilation delay
- **Smaller memory footprint** - No JIT overhead
- **Single native binary** - True native executable
- **Runs in restricted environments** - Where JIT is prohibited

### Enabling Native AOT

**In .csproj:**
```xml
<PropertyGroup>
  <PublishAot>true</PublishAot>
</PropertyGroup>
```

**Publish command:**
```bash
dotnet publish -c Release -r win-x64
```

**Note:** `PublishAot` should be in the project file, not passed via CLI, as it affects build-time analysis.

### Supported Platforms (.NET 9+)

| Platform | Architectures |
|----------|---------------|
| Windows | x64, Arm64, x86 |
| Linux | x64, Arm64, Arm |
| macOS | x64, Arm64 |

### Prerequisites

**Windows:**
- Visual Studio 2022+ with "Desktop development with C++" workload

**Ubuntu:**
```bash
sudo apt-get install clang zlib1g-dev
```

**Alpine:**
```bash
sudo apk add clang build-base zlib-dev
```

### Limitations

| Limitation | Impact |
|-----------|--------|
| No dynamic assembly loading | `Assembly.LoadFile` not supported |
| No runtime code generation | `System.Reflection.Emit` not supported |
| Limited reflection | Must be statically analyzable |
| No C++/CLI | Interop limitation |
| Larger binary (sometimes) | Due to included code |

### When to Use Native AOT

**Use Native AOT for:**
- Cloud/hyper-scale services
- Restricted runtime environments
- Performance-critical startup scenarios
- Container deployments with size constraints

**Avoid Native AOT if:**
- Heavy use of reflection or dynamic code
- Plugin/extension architecture needed
- Compatibility with all libraries required

### Native AOT vs Standard Publishing

| Aspect | Standard + Trim | Native AOT |
|--------|-----------------|------------|
| Startup Time | Good | Excellent |
| File Size | 10-30 MB | 5-20 MB |
| Memory Usage | Standard | Lower |
| Compatibility | High | Limited |
| Build Time | Fast | Slower |

---

## 9. Cross-Compilation

### Cross-OS Limitations

**Native AOT does NOT support cross-OS compilation:**
- Cannot build macOS binaries on Windows/Linux
- Cannot build Windows binaries on Linux/macOS
- Cannot build Linux binaries on Windows/macOS

**Workaround:** Use CI/CD with platform-specific runners or virtual machines.

### Cross-Architecture Support (Same OS)

Native AOT supports limited cross-architecture compilation between `x64` and `arm64`:

**Windows (x64 to ARM64):**
```bash
# Install VS 2022 C++ ARM64 build tools first
dotnet publish -r win-arm64 -c Release
```

**macOS:**
Default XCode includes both x64 and arm64 toolchains.

**Linux (Ubuntu amd64 to linux-arm64):**
```bash
# Install cross-compilation toolchain
sudo dpkg --add-architecture arm64
sudo apt install clang llvm binutils-aarch64-linux-gnu gcc-aarch64-linux-gnu zlib1g-dev:arm64

# Then publish
dotnet publish -r linux-arm64 -c Release
```

### Standard Publishing (Non-AOT)

Standard publishing (without Native AOT) can cross-compile freely:
```bash
# On Windows, build for Linux
dotnet publish -r linux-x64 -c Release --self-contained

# On Linux, build for Windows
dotnet publish -r win-x64 -c Release --self-contained
```

### CI/CD Strategy for True Cross-Platform

Use GitHub Actions or similar with matrix builds:

```yaml
strategy:
  matrix:
    include:
      - os: windows-latest
        rid: win-x64
      - os: windows-latest
        rid: win-arm64
      - os: ubuntu-latest
        rid: linux-x64
      - os: ubuntu-latest
        rid: linux-arm64
      - os: macos-latest
        rid: osx-x64
      - os: macos-latest
        rid: osx-arm64
```

---

## 10. Build Scripts and Automation

### Bash Script for Multi-Platform Builds

```bash
#!/bin/bash

# build-all-platforms.sh
# Build self-contained single-file executables for all platforms

set -e

PROJECT_NAME="MyMcpServer"
OUTPUT_DIR="./artifacts"
CONFIGURATION="Release"

# Define target platforms
RIDS=(
    "win-x64"
    "win-arm64"
    "linux-x64"
    "linux-arm64"
    "osx-x64"
    "osx-arm64"
)

# Clean output directory
rm -rf "$OUTPUT_DIR"
mkdir -p "$OUTPUT_DIR"

# Build for each platform
for RID in "${RIDS[@]}"; do
    echo "Building for $RID..."

    OUTPUT_PATH="$OUTPUT_DIR/$RID"

    dotnet publish \
        -c "$CONFIGURATION" \
        -r "$RID" \
        -o "$OUTPUT_PATH" \
        -p:PublishSingleFile=true \
        -p:PublishTrimmed=true \
        -p:EnableCompressionInSingleFile=true \
        -p:IncludeNativeLibrariesForSelfExtract=true \
        --self-contained

    # Rename output based on platform
    if [[ "$RID" == win-* ]]; then
        mv "$OUTPUT_PATH/$PROJECT_NAME.exe" "$OUTPUT_PATH/${PROJECT_NAME}-${RID}.exe"
    else
        mv "$OUTPUT_PATH/$PROJECT_NAME" "$OUTPUT_PATH/${PROJECT_NAME}-${RID}"
    fi

    echo "Built: $OUTPUT_PATH/${PROJECT_NAME}-${RID}"
done

echo "All builds complete!"
ls -la "$OUTPUT_DIR"/*
```

### PowerShell Script for Multi-Platform Builds

```powershell
# build-all-platforms.ps1
# Build self-contained single-file executables for all platforms

$ErrorActionPreference = "Stop"

$ProjectName = "MyMcpServer"
$OutputDir = "./artifacts"
$Configuration = "Release"

# Define target platforms
$Rids = @(
    "win-x64",
    "win-arm64",
    "linux-x64",
    "linux-arm64",
    "osx-x64",
    "osx-arm64"
)

# Clean output directory
if (Test-Path $OutputDir) {
    Remove-Item -Recurse -Force $OutputDir
}
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

# Build for each platform
foreach ($Rid in $Rids) {
    Write-Host "Building for $Rid..." -ForegroundColor Cyan

    $OutputPath = Join-Path $OutputDir $Rid

    dotnet publish `
        -c $Configuration `
        -r $Rid `
        -o $OutputPath `
        -p:PublishSingleFile=true `
        -p:PublishTrimmed=true `
        -p:EnableCompressionInSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        --self-contained

    if ($LASTEXITCODE -ne 0) {
        throw "Build failed for $Rid"
    }

    # Rename output based on platform
    if ($Rid -like "win-*") {
        $OldPath = Join-Path $OutputPath "$ProjectName.exe"
        $NewPath = Join-Path $OutputPath "$ProjectName-$Rid.exe"
    } else {
        $OldPath = Join-Path $OutputPath $ProjectName
        $NewPath = Join-Path $OutputPath "$ProjectName-$Rid"
    }

    Rename-Item -Path $OldPath -NewName (Split-Path $NewPath -Leaf)

    Write-Host "Built: $NewPath" -ForegroundColor Green
}

Write-Host "`nAll builds complete!" -ForegroundColor Green
Get-ChildItem $OutputDir -Recurse -File | Format-Table Name, Length
```

### MSBuild Targets for Multiple RIDs

Add to your .csproj:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <RuntimeIdentifiers>win-x64;win-arm64;linux-x64;linux-arm64;osx-x64;osx-arm64</RuntimeIdentifiers>
    <PublishSingleFile>true</PublishSingleFile>
    <SelfContained>true</SelfContained>
    <PublishTrimmed>true</PublishTrimmed>
  </PropertyGroup>

  <Target Name="PublishAllRids">
    <ItemGroup>
      <_RidsToPublish Include="$(RuntimeIdentifiers.Split(';'))" />
    </ItemGroup>
    <MSBuild Projects="$(MSBuildProjectFullPath)"
             Targets="Publish"
             Properties="RuntimeIdentifier=%(_RidsToPublish.Identity);PublishDir=$(MSBuildProjectDirectory)\artifacts\%(_RidsToPublish.Identity)\" />
  </Target>
</Project>
```

**Usage:**
```bash
dotnet msbuild -t:PublishAllRids -p:Configuration=Release
```

---

## 11. Output Organization

### Recommended Folder Structure

```
artifacts/
├── win-x64/
│   └── MyMcpServer-win-x64.exe
├── win-arm64/
│   └── MyMcpServer-win-arm64.exe
├── linux-x64/
│   └── MyMcpServer-linux-x64
├── linux-arm64/
│   └── MyMcpServer-linux-arm64
├── osx-x64/
│   └── MyMcpServer-osx-x64
└── osx-arm64/
    └── MyMcpServer-osx-arm64
```

### Distribution Package Structure

```
releases/
├── v1.0.0/
│   ├── MyMcpServer-v1.0.0-win-x64.zip
│   ├── MyMcpServer-v1.0.0-win-arm64.zip
│   ├── MyMcpServer-v1.0.0-linux-x64.tar.gz
│   ├── MyMcpServer-v1.0.0-linux-arm64.tar.gz
│   ├── MyMcpServer-v1.0.0-osx-x64.tar.gz
│   ├── MyMcpServer-v1.0.0-osx-arm64.tar.gz
│   └── checksums.sha256
```

### Naming Conventions

Recommended naming pattern:
```
{ProductName}-{Version}-{RID}.{extension}
```

Examples:
- `MyMcpServer-v1.0.0-win-x64.exe`
- `MyMcpServer-v1.0.0-linux-x64`
- `MyMcpServer-v1.0.0-osx-arm64`

---

## 12. Complete Examples

### Minimal .csproj for MCP Server

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>

    <!-- Multi-platform support -->
    <RuntimeIdentifiers>win-x64;win-arm64;linux-x64;linux-arm64;osx-x64;osx-arm64</RuntimeIdentifiers>

    <!-- Deployment settings -->
    <SelfContained>true</SelfContained>
    <PublishSingleFile>true</PublishSingleFile>
    <IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>

    <!-- Size optimization -->
    <PublishTrimmed>true</PublishTrimmed>
    <EnableCompressionInSingleFile>true</EnableCompressionInSingleFile>

    <!-- Debug symbols -->
    <DebugType>embedded</DebugType>
  </PropertyGroup>

  <ItemGroup>
    <!-- Your package references here -->
  </ItemGroup>
</Project>
```

### Optimized .csproj with ReadyToRun

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>

    <RuntimeIdentifiers>win-x64;win-arm64;linux-x64;linux-arm64;osx-x64;osx-arm64</RuntimeIdentifiers>

    <SelfContained>true</SelfContained>
    <PublishSingleFile>true</PublishSingleFile>
    <IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>
    <PublishTrimmed>true</PublishTrimmed>
    <EnableCompressionInSingleFile>true</EnableCompressionInSingleFile>

    <!-- Faster startup -->
    <PublishReadyToRun>true</PublishReadyToRun>

    <!-- Reduce size further -->
    <InvariantGlobalization>true</InvariantGlobalization>
    <DebuggerSupport>false</DebuggerSupport>
    <EventSourceSupport>false</EventSourceSupport>

    <DebugType>embedded</DebugType>
  </PropertyGroup>
</Project>
```

### Native AOT .csproj (Maximum Performance)

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>

    <RuntimeIdentifiers>win-x64;linux-x64;osx-arm64</RuntimeIdentifiers>

    <!-- Native AOT -->
    <PublishAot>true</PublishAot>

    <!-- Size optimization -->
    <InvariantGlobalization>true</InvariantGlobalization>
    <StackTraceSupport>false</StackTraceSupport>
    <UseSystemResourceKeys>true</UseSystemResourceKeys>

    <!-- Stripping -->
    <StripSymbols>true</StripSymbols>
  </PropertyGroup>
</Project>
```

### GitHub Actions Workflow

```yaml
name: Build Multi-Platform

on:
  push:
    tags:
      - 'v*'
  workflow_dispatch:

jobs:
  build:
    strategy:
      matrix:
        include:
          - os: windows-latest
            rid: win-x64
            artifact: MyMcpServer-win-x64.exe
          - os: windows-latest
            rid: win-arm64
            artifact: MyMcpServer-win-arm64.exe
          - os: ubuntu-latest
            rid: linux-x64
            artifact: MyMcpServer-linux-x64
          - os: ubuntu-latest
            rid: linux-arm64
            artifact: MyMcpServer-linux-arm64
          - os: macos-latest
            rid: osx-x64
            artifact: MyMcpServer-osx-x64
          - os: macos-latest
            rid: osx-arm64
            artifact: MyMcpServer-osx-arm64

    runs-on: ${{ matrix.os }}

    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'

      - name: Restore dependencies
        run: dotnet restore

      - name: Publish
        run: |
          dotnet publish -c Release -r ${{ matrix.rid }} -o ./publish/${{ matrix.rid }} \
            -p:PublishSingleFile=true \
            -p:PublishTrimmed=true \
            -p:EnableCompressionInSingleFile=true \
            -p:IncludeNativeLibrariesForSelfExtract=true \
            --self-contained

      - name: Upload artifact
        uses: actions/upload-artifact@v4
        with:
          name: ${{ matrix.artifact }}
          path: ./publish/${{ matrix.rid }}/

  release:
    needs: build
    runs-on: ubuntu-latest
    if: startsWith(github.ref, 'refs/tags/')

    steps:
      - name: Download all artifacts
        uses: actions/download-artifact@v4
        with:
          path: ./artifacts

      - name: Create Release
        uses: softprops/action-gh-release@v1
        with:
          files: ./artifacts/**/*
        env:
          GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}
```

---

## 13. Common Issues and Solutions

### Missing Native Dependencies

**Problem:** Application fails with missing DLL/SO errors.

**Solution:**
```xml
<PropertyGroup>
  <IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>
</PropertyGroup>
```

### Trimming Breaks Reflection

**Problem:** Runtime errors after enabling trimming.

**Solutions:**

1. **Preserve specific types:**
```xml
<ItemGroup>
  <TrimmerRootAssembly Include="MyReflectionAssembly" />
</ItemGroup>
```

2. **Use trim annotations:**
```csharp
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
public Type MyType { get; set; }
```

3. **Switch to partial trimming:**
```xml
<PropertyGroup>
  <TrimMode>partial</TrimMode>
</PropertyGroup>
```

### Platform-Specific Code

**Problem:** Code that only works on specific platforms.

**Solution:** Use preprocessor directives:
```csharp
#if WINDOWS
    // Windows-specific code
#elif LINUX
    // Linux-specific code
#elif OSX
    // macOS-specific code
#endif
```

Or runtime checks:
```csharp
if (OperatingSystem.IsWindows())
{
    // Windows-specific code
}
else if (OperatingSystem.IsLinux())
{
    // Linux-specific code
}
else if (OperatingSystem.IsMacOS())
{
    // macOS-specific code
}
```

### File Size Too Large

**Problem:** Self-contained executable is too large.

**Solutions:**

1. Enable trimming:
```xml
<PublishTrimmed>true</PublishTrimmed>
```

2. Enable compression:
```xml
<EnableCompressionInSingleFile>true</EnableCompressionInSingleFile>
```

3. Disable unnecessary features:
```xml
<InvariantGlobalization>true</InvariantGlobalization>
<DebuggerSupport>false</DebuggerSupport>
```

4. Consider Native AOT:
```xml
<PublishAot>true</PublishAot>
```

### Slow Startup Time

**Problem:** Application takes too long to start.

**Solutions:**

1. Enable ReadyToRun:
```xml
<PublishReadyToRun>true</PublishReadyToRun>
```

2. Use Native AOT:
```xml
<PublishAot>true</PublishAot>
```

3. Disable compression (if enabled):
```xml
<EnableCompressionInSingleFile>false</EnableCompressionInSingleFile>
```

### Linux Binary Won't Execute

**Problem:** "Permission denied" when running Linux binary.

**Solution:**
```bash
chmod +x ./MyMcpServer-linux-x64
./MyMcpServer-linux-x64
```

### macOS Gatekeeper Blocks App

**Problem:** macOS prevents running unsigned app.

**Solutions:**

1. Ad-hoc signing:
```bash
codesign --force --deep -s - ./MyMcpServer-osx-arm64
```

2. Remove quarantine attribute:
```bash
xattr -d com.apple.quarantine ./MyMcpServer-osx-arm64
```

### Build Fails on CI for ARM64

**Problem:** Cannot cross-compile to ARM64 with Native AOT.

**Solution:** Use platform-specific runners:
```yaml
# For linux-arm64, use ARM runner
- os: ubuntu-24.04-arm
  rid: linux-arm64
```

Or use standard publishing (not Native AOT) which supports cross-compilation.

---

## Quick Reference

### Essential Commands

```bash
# Basic self-contained single-file
dotnet publish -c Release -r win-x64 -p:PublishSingleFile=true --self-contained

# Optimized build
dotnet publish -c Release -r linux-x64 \
  -p:PublishSingleFile=true \
  -p:PublishTrimmed=true \
  -p:EnableCompressionInSingleFile=true \
  --self-contained

# Native AOT
dotnet publish -c Release -r osx-arm64 -p:PublishAot=true
```

### Common RIDs for MCP Servers

| Platform | RID | Notes |
|----------|-----|-------|
| Windows (Intel/AMD) | `win-x64` | Most common |
| Windows (ARM) | `win-arm64` | Surface Pro X, etc. |
| Linux (Intel/AMD) | `linux-x64` | Servers, desktops |
| Linux (ARM) | `linux-arm64` | Raspberry Pi 4, ARM servers |
| macOS (Intel) | `osx-x64` | Older Macs |
| macOS (Apple Silicon) | `osx-arm64` | M1/M2/M3/M4 Macs |

---

## Sources

- [.NET Runtime Identifier (RID) Catalog - Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/core/rid-catalog)
- [.NET Application Publishing Overview - Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/core/deploying/)
- [Create a Single File for Application Deployment - Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview)
- [Native AOT Deployment Overview - Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/)
- [Cross-Compilation - Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/cross-compile)
- [Trim Self-Contained Applications - Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/trim-self-contained)
- [Trimming Options - Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/trimming-options)
- [ReadyToRun Compilation - Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/core/deploying/ready-to-run)
- [dotnet publish Command - Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-publish)
- [GitHub Actions and .NET - Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/devops/github-actions-overview)
- [Fixing Trim Warnings - Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/fixing-warnings)
