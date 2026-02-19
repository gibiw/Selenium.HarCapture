# Technology Stack

**Project:** Selenium.Hars
**Researched:** 2026-02-19
**Overall Confidence:** HIGH

## Recommended Stack

### Core Framework & Runtime

| Technology | Version | Purpose | Why |
|------------|---------|---------|-----|
| .NET Target Framework | netstandard2.0 | Library target | Maximum compatibility: supports .NET Framework 4.6.1+, .NET Core 2.0+, .NET 5-9+, Mono, Xamarin. Trade-off: netstandard2.1 adds performance primitives (IAsyncEnumerable, Span<T> native) but drops .NET Framework support. For a library, netstandard2.0 provides widest adoption. |
| C# Language Version | 7.3+ | Language features | Maximum language version supported by netstandard2.0 compilers without requiring runtime features unavailable in older frameworks. |

**Rationale:** netstandard2.0 remains the gold standard for library distribution in 2026 when .NET Framework compatibility is desired. While netstandard2.1 and net8.0 offer better performance primitives, they fragment the potential user base. For a network capture utility library, broad compatibility outweighs cutting-edge features.

**Confidence:** HIGH (verified with [Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/standard/net-standard))

### Selenium & Browser Automation

| Technology | Version | Purpose | Why |
|------------|---------|---------|-----|
| Selenium.WebDriver | 4.40.0 | WebDriver core API | Latest stable release (Jan 18, 2026). Includes INetwork API for basic capture and BiDi support for modern browsers. 4.x series required for INetwork fallback mechanism. |
| CDP (Chrome DevTools Protocol) | Via Selenium.WebDriver | Primary capture mechanism | Built into Selenium 4.x via GetDevToolsSession(). Selenium supports the 3 most recent Chrome versions. CDP provides granular network event capture including request/response bodies, timings, and headers. |
| INetwork API | Via Selenium.WebDriver | Fallback capture | Cross-browser network interception API. Limited compared to CDP but works with Firefox, Edge, Safari (when supported). Single handler chain limitation: first match wins, others ignored. |

**Rationale:** Selenium 4.40.0 provides both CDP (best-in-class capture for Chromium browsers) and INetwork (cross-browser fallback). BiDi support is improving but not yet mature enough to rely on exclusively in 2026. CDP remains the gold standard for HAR capture quality.

**Confidence:** HIGH (verified with [NuGet Gallery](https://www.nuget.org/packages/Selenium.WebDriver), [Selenium documentation](https://www.selenium.dev/documentation/webdriver/bidi/cdp/))

### Serialization

| Technology | Version | Purpose | Why |
|------------|---------|---------|-----|
| System.Text.Json | 10.0.3 | HAR JSON serialization | Latest version (Jan 13, 2026). 2-3x faster than Newtonsoft.Json with half the memory allocation. netstandard2.0 compatible. Source generation support (3x faster serialization when enabled). Built-in async streaming. HAR 1.2 spec is straightforward JSON - STJ's performance wins here. |

**Alternative Considered:**
| Technology | Why Not |
|------------|---------|
| Newtonsoft.Json | Slower (2-3x), higher memory usage (2x), mature but System.Text.Json is now the .NET standard. Only choose if exotic JSON features needed (dynamic typing, complex converters). HAR format doesn't require this. |

**Rationale:** System.Text.Json is the modern .NET standard with superior performance. HAR 1.2 format is well-defined JSON schema - no edge cases requiring Newtonsoft's flexibility. For a library that may serialize large network captures, 2-3x performance difference is significant.

**Confidence:** HIGH (verified with [NuGet Gallery](https://www.nuget.org/packages/System.Text.Json), [performance benchmarks](https://trevormccubbin.medium.com/net-performance-analysis-newtonsoft-json-vs-system-text-json-in-net-9-1ac673502dbf))

### Supporting Libraries

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| System.Memory | 4.6.3 | Span<T> support for netstandard2.0 | Efficient byte/string operations without allocation. Useful for parsing headers, bodies. Provides modern memory types to netstandard2.0. Optional but recommended for performance. |
| System.Text.Encodings.Web | 10.0.3 | URL/HTML encoding | Required by System.Text.Json for safe JSON encoding of URLs, headers. Automatically restored as transitive dependency. |

**Rationale:** System.Memory brings .NET Core performance primitives to netstandard2.0 via portable implementation. For network data parsing (headers, chunked encoding, etc.), Span<T> eliminates string allocation overhead.

**Confidence:** MEDIUM (System.Memory verified with [NuGet Gallery](https://www.nuget.org/packages/System.Memory/), encoding library is transitive dependency)

## Development & Testing Stack

### Testing Framework

| Technology | Version | Purpose | Why |
|------------|---------|---------|-----|
| xUnit | 2.9.3+ | Unit test framework | Default for modern .NET, fastest execution, excellent async support. Highest NuGet downloads. Modern design over NUnit/MSTest. Selenium tests are async-heavy - xUnit handles this natively. |
| xUnit.runner.visualstudio | 2.8.2+ | Visual Studio integration | Test discovery in VS and Rider. |
| Microsoft.NET.Test.Sdk | 17.12.0+ | Test platform | Required for dotnet test CLI. |

**Alternatives Considered:**
| Technology | Why Not |
|------------|---------|
| NUnit | Mature, feature-rich, but xUnit is faster and more aligned with modern .NET patterns. |
| MSTest | Microsoft-centric, legacy feel, fewer community resources compared to xUnit. |

**Rationale:** xUnit's performance advantage matters for Selenium tests (slow by nature - browser startup, navigation). Async-first design matches WebDriver 4.x async patterns.

**Confidence:** HIGH (verified with [comparison sources](https://medium.com/@robertdennyson/xunit-vs-nunit-vs-mstest-choosing-the-right-testing-framework-for-net-applications-b6b9b750bec6))

### Assertion Library

| Technology | Version | Purpose | Why |
|------------|---------|---------|-----|
| FluentAssertions | 8.8.0 | Test assertions | Readable assertions for complex HAR structure validation. `har.Log.Entries.Should().AllSatisfy(e => e.Request.Url.Should().NotBeNullOrEmpty())` vs xUnit's basic Assert. Supports all major test frameworks. |

**Rationale:** HAR structure validation requires deep object inspection. FluentAssertions makes test failures diagnostic: shows exact path where assertion failed, expected vs actual values. Essential for complex JSON structure testing.

**Confidence:** HIGH (verified with [NuGet Gallery](https://www.nuget.org/packages/fluentassertions/))

### Code Coverage

| Technology | Version | Purpose | Why |
|------------|---------|---------|-----|
| coverlet.collector | 8.0.0 | Code coverage | Cross-platform, default for .NET 8+. Generates Cobertura XML for CI/CD pipelines. Integrated into `dotnet test --collect:"XPlat Code Coverage"`. Open-source, actively maintained (last updated Feb 14, 2026). |

**Rationale:** Standard coverage tool for modern .NET libraries. GitHub Actions, Azure Pipelines, GitLab CI all support Cobertura format natively.

**Confidence:** HIGH (verified with [Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-code-coverage), [NuGet Gallery](https://www.nuget.org/packages/coverlet.collector))

## Packaging & Distribution

### NuGet Package Configuration

| Technology | Version | Purpose | Why |
|------------|---------|---------|-----|
| Microsoft.SourceLink.GitHub | 10.0.102 | Source debugging | Enables "Step Into" for consumers. Embeds git commit metadata in symbols. Distributes .snupkg symbol package to NuGet.org. Standard practice for open-source libraries. Users can debug your code without cloning repo. |
| MinVer | 7.0.0 | Automatic versioning | Git tag-based semantic versioning. No manual version editing. Tag `v1.2.3` → NuGet package 1.2.3. Simpler than GitVersion, no branching strategy assumptions. Industry standard for .NET library versioning. |

**Rationale:** Source Link is expected for modern libraries - dramatically improves debugging experience. MinVer eliminates version management toil - version is single source of truth (git tag).

**Confidence:** HIGH (verified with [Microsoft.SourceLink.GitHub](https://www.nuget.org/packages/Microsoft.SourceLink.GitHub), [MinVer](https://www.nuget.org/packages/MinVer/))

### Package Metadata Requirements

| Property | Value | Rationale |
|----------|-------|-----------|
| PackageId | Selenium.Hars | Clear, discoverable name. Follows Selenium.* convention. |
| Authors | [Your name/org] | Required for NuGet.org |
| Description | Captures HTTP traffic from Selenium WebDriver into HAR format. Supports CDP and INetwork API. | Short, searchable, explains purpose immediately. |
| PackageTags | selenium;har;http-archive;webdriver;network;cdp;devtools | NuGet.org search algorithm uses tags heavily. |
| RepositoryUrl | [GitHub URL] | Automatically set by SourceLink, links package to source. |
| PackageLicenseExpression | MIT | Standard for open-source libraries. |
| PackageProjectUrl | [GitHub URL] | Where users go for docs/issues. |
| PackageReadme | README.md | Rendered on NuGet.org package page. |
| GenerateDocumentationFile | true | Generates XML docs for IntelliSense. |
| PublishRepositoryUrl | true | SourceLink requirement. |
| EmbedUntrackedSources | true | SourceLink requirement. |
| IncludeSymbols | true | Generate .snupkg symbol package. |
| SymbolPackageFormat | snupkg | Modern symbol package format for NuGet.org. |

**Confidence:** HIGH (verified with [Microsoft Learn - NuGet best practices](https://learn.microsoft.com/en-us/nuget/create-packages/package-authoring-best-practices))

## HAR Format Compliance

### Specification

| Specification | Version | URL | Notes |
|--------------|---------|-----|-------|
| HTTP Archive (HAR) | 1.2 | http://www.softwareishard.com/blog/har-12-spec/ | Frozen spec (Feb 16, 2011). JSON-based, UTF-8 encoded. Key fields: log, entries, request, response, timings, content. Version 1.2 adds: SSL timings, serverIPAddress, connection, secure flag for cookies, comment fields. |

**Rationale:** HAR 1.2 is the industry standard, supported by Chrome DevTools, Firefox, HttpWatch, Fiddler, etc. No newer spec exists - 1.2 is the target.

**Confidence:** HIGH (verified with [HAR 1.2 spec](http://www.softwareishard.com/blog/har-12-spec/))

## Installation

### Consumer Installation

```bash
dotnet add package Selenium.Hars
```

### Development Setup

```bash
# Clone repository
git clone https://github.com/[your-org]/Selenium.Hars.git
cd Selenium.Hars

# Restore dependencies
dotnet restore

# Build
dotnet build

# Run tests
dotnet test

# Run tests with coverage
dotnet test --collect:"XPlat Code Coverage"

# Pack NuGet package (version from git tags via MinVer)
dotnet pack -c Release
```

## Version Strategy

| Scenario | Git Tag | NuGet Version | Rationale |
|----------|---------|---------------|-----------|
| Initial release | v1.0.0 | 1.0.0 | Semantic versioning starts at 1.0.0 for stable libraries. |
| Bug fix | v1.0.1 | 1.0.1 | PATCH version increment. |
| New feature (backward compatible) | v1.1.0 | 1.1.0 | MINOR version increment. |
| Breaking change | v2.0.0 | 2.0.0 | MAJOR version increment. |
| Pre-release | v1.1.0-beta.1 | 1.1.0-beta.1 | SemVer pre-release identifiers. |

**MinVer Behavior:**
- Commit with tag `v1.2.3` → version 1.2.3
- Commit without tag → finds latest tag, increments patch, adds `-alpha.0.{height}.{commit-sha}`
- Pre-release packages: `dotnet pack` creates version like `1.2.4-alpha.0.5.a1b2c3d` (5 commits after v1.2.3)

**Confidence:** HIGH (verified with [MinVer documentation](https://github.com/adamralph/minver))

## .csproj Configuration Example

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <!-- Target Framework -->
    <TargetFramework>netstandard2.0</TargetFramework>
    <LangVersion>latest</LangVersion>

    <!-- Package Metadata -->
    <PackageId>Selenium.Hars</PackageId>
    <Authors>Your Name</Authors>
    <Description>Captures HTTP traffic from Selenium WebDriver into HAR format. Supports CDP and INetwork API.</Description>
    <PackageTags>selenium;har;http-archive;webdriver;network;cdp;devtools</PackageTags>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    <PackageProjectUrl>https://github.com/your-org/Selenium.Hars</PackageProjectUrl>
    <PackageReadme>README.md</PackageReadme>

    <!-- Documentation -->
    <GenerateDocumentationFile>true</GenerateDocumentationFile>

    <!-- SourceLink -->
    <PublishRepositoryUrl>true</PublishRepositoryUrl>
    <EmbedUntrackedSources>true</EmbedUntrackedSources>
    <IncludeSymbols>true</IncludeSymbols>
    <SymbolPackageFormat>snupkg</SymbolPackageFormat>
  </PropertyGroup>

  <ItemGroup>
    <!-- Core Dependencies -->
    <PackageReference Include="Selenium.WebDriver" Version="4.40.0" />
    <PackageReference Include="System.Text.Json" Version="10.0.3" />
    <PackageReference Include="System.Memory" Version="4.6.3" />

    <!-- Build-time Only (PrivateAssets="All") -->
    <PackageReference Include="Microsoft.SourceLink.GitHub" Version="10.0.102" PrivateAssets="All" />
    <PackageReference Include="MinVer" Version="7.0.0" PrivateAssets="All" />
  </ItemGroup>

  <ItemGroup>
    <!-- README for NuGet.org -->
    <None Include="README.md" Pack="true" PackagePath="\" />
  </ItemGroup>
</Project>
```

**Notes:**
- `PrivateAssets="All"` prevents SourceLink and MinVer from being transitive dependencies
- `LangVersion=latest` allows C# 7.3 features (pattern matching, tuples, etc.) while targeting netstandard2.0
- System.Text.Encodings.Web is transitive dependency of System.Text.Json - no explicit reference needed

**Confidence:** HIGH (standard .NET SDK-style project format)

## Anti-Recommendations

### Do NOT Use

| Technology | Why Avoid |
|------------|-----------|
| Selenium 3.x | Missing INetwork API, CDP support. 4.x is required. |
| netstandard2.1 as sole target | Drops .NET Framework support. Use netstandard2.0 unless performance profiling shows bottleneck in Span<T> operations AND you're willing to drop .NET Framework users. |
| net8.0 as sole target | Fragments user base. Multi-target `<TargetFrameworks>net8.0;netstandard2.0</TargetFrameworks>` if needed, but netstandard2.0 alone is sufficient for this library's needs. |
| Newtonsoft.Json | Slower, more memory. Only if System.Text.Json proves unable to handle HAR schema (unlikely - it's straightforward JSON). |
| Selenium WebDriver-specific browser drivers (ChromeDriver, GeckoDriver) | User's responsibility to install. Library should be browser-agnostic - works with any IWebDriver instance. |
| JSON.NET | Old name for Newtonsoft.Json. Same reasoning: use System.Text.Json. |
| .NET Framework 4.x as target | Library consumers can use netstandard2.0 library from .NET Framework 4.6.1+. No need to explicitly target framework. |
| NuGet.Versioning for manual version management | MinVer automates this via git tags. Manual versioning error-prone. |

**Confidence:** HIGH (based on ecosystem research and best practices)

## Dependencies Tree (NuGet)

```
Selenium.Hars (library)
├── Selenium.WebDriver (4.40.0)
│   └── Newtonsoft.Json (>= 13.0.1) [transitive]
├── System.Text.Json (10.0.3)
│   ├── System.Text.Encodings.Web (10.0.3) [transitive]
│   └── System.Memory (4.5.5+) [transitive, satisfied by explicit 4.6.3]
└── System.Memory (4.6.3)
    ├── System.Buffers (4.5.1) [transitive]
    ├── System.Numerics.Vectors (4.5.0) [transitive]
    └── System.Runtime.CompilerServices.Unsafe (4.5.3) [transitive]
```

**Note:** Selenium.WebDriver itself depends on Newtonsoft.Json for internal use. This doesn't conflict with System.Text.Json for your library's HAR serialization. Both can coexist in consumer's dependency tree.

**Confidence:** MEDIUM (transitive dependencies subject to Selenium.WebDriver updates)

## Future Considerations

### WebDriver BiDi

**Status (2026):** Improving but not production-ready for .NET. Selenium 4.26 added enhanced BiDi support for .NET, but CDP remains more mature.

**When to migrate:** When WebDriver BiDi reaches feature parity with CDP for network capture (request/response bodies, timings, headers) and is stable across Chrome, Firefox, Edge. Monitor Selenium release notes.

**Action:** Design abstraction that can swap CDP → BiDi without breaking API. Consider `ICaptureProvider` interface with `CdpCaptureProvider` and future `BiDiCaptureProvider`.

**Confidence:** MEDIUM (BiDi status verified with [Selenium documentation](https://www.selenium.dev/documentation/webdriver/bidi/), but timeline uncertain)

### .NET 9+ Native AOT

**Status:** .NET 9 improves native AOT support, but Selenium.WebDriver likely not AOT-compatible (uses reflection for CDP protocol).

**When relevant:** If Selenium adds AOT support AND library consumers demand it for trimmed/AOT-published apps.

**Action:** Test with `<PublishAot>true</PublishAot>` before claiming compatibility. System.Text.Json source generators help AOT readiness.

**Confidence:** LOW (speculative, depends on Selenium roadmap)

## Sources

### Official Documentation
- [.NET Standard specification](https://learn.microsoft.com/en-us/dotnet/standard/net-standard)
- [Cross-platform targeting for .NET libraries](https://learn.microsoft.com/en-us/dotnet/standard/library-guidance/cross-platform-targeting)
- [NuGet Package authoring best practices](https://learn.microsoft.com/en-us/nuget/create-packages/package-authoring-best-practices)
- [Source Link and .NET libraries](https://learn.microsoft.com/en-us/dotnet/standard/library-guidance/sourcelink)
- [Unit testing code coverage](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-code-coverage)
- [HAR 1.2 Specification](http://www.softwareishard.com/blog/har-12-spec/)
- [Selenium WebDriver documentation](https://www.selenium.dev/documentation/webdriver/)
- [Selenium BiDi documentation](https://www.selenium.dev/documentation/webdriver/bidi/)
- [Selenium CDP documentation](https://www.selenium.dev/documentation/webdriver/bidi/cdp/)

### NuGet Packages (verified versions)
- [Selenium.WebDriver 4.40.0](https://www.nuget.org/packages/Selenium.WebDriver)
- [System.Text.Json 10.0.3](https://www.nuget.org/packages/System.Text.Json)
- [System.Memory 4.6.3](https://www.nuget.org/packages/System.Memory/)
- [FluentAssertions 8.8.0](https://www.nuget.org/packages/fluentassertions/)
- [coverlet.collector 8.0.0](https://www.nuget.org/packages/coverlet.collector)
- [Microsoft.SourceLink.GitHub 10.0.102](https://www.nuget.org/packages/Microsoft.SourceLink.GitHub)
- [MinVer 7.0.0](https://www.nuget.org/packages/MinVer/)

### Performance & Comparison Research
- [System.Text.Json vs Newtonsoft.Json Performance Analysis (.NET 9)](https://trevormccubbin.medium.com/net-performance-analysis-newtonsoft-json-vs-system-text-json-in-net-9-1ac673502dbf)
- [xUnit vs NUnit vs MSTest comparison](https://medium.com/@robertdennyson/xunit-vs-nunit-vs-mstest-choosing-the-right-testing-framework-for-net-applications-b6b9b750bec6)
- [.NET 8 vs .NET 9 support policy](https://learn.microsoft.com/en-us/dotnet/core/releases-and-support)

### GitHub Repositories
- [MinVer source](https://github.com/adamralph/minver)
- [Source Link source](https://github.com/dotnet/sourcelink)
- [coverlet source](https://github.com/coverlet-coverage/coverlet)
- [FluentAssertions source](https://github.com/fluentassertions/fluentassertions)
