# Project Research Summary

**Project:** Selenium.Hars
**Domain:** .NET library for capturing HTTP network traffic from Selenium WebDriver into HAR (HTTP Archive) format
**Researched:** 2026-02-19
**Confidence:** HIGH

## Executive Summary

Selenium.Hars is a netstandard2.0 library that captures HTTP traffic from Selenium WebDriver sessions and exports it to HAR 1.2 format (the industry standard for network analysis). The recommended approach uses Chrome DevTools Protocol (CDP) as the primary capture mechanism with Selenium's INetwork API as a cross-browser fallback. This dual-strategy pattern provides both detailed Chromium capture and basic Firefox/Edge support without requiring proxy infrastructure.

The technical foundation is solid: netstandard2.0 provides maximum compatibility (.NET Framework 4.6.1+ through .NET 9+), Selenium 4.40.0 includes mature CDP support, and System.Text.Json delivers 2-3x faster serialization than alternatives. The architecture centers on event-based capture (not proxy interception), thread-safe request/response correlation using ConcurrentDictionary with Lazy pattern, and the Strategy pattern to encapsulate CDP vs INetwork implementations.

Critical risks center on CDP's event-driven complexity: event ordering race conditions, response body timing windows, redirect chain correlation, and thread-safety requirements. These are well-documented pitfalls with proven mitigation strategies. The research shows that successful HAR capture libraries consistently implement event buffering, immediate body retrieval, Lazy initialization for thread safety, and ConfigureAwait(false) throughout to prevent deadlocks in library code. With these patterns in place, the library can deliver reliable, high-performance network capture for debugging, performance analysis, and test automation use cases.

## Key Findings

### Recommended Stack

The stack emphasizes broad compatibility and performance. netstandard2.0 targets the widest ecosystem (Framework 4.6.1+ through .NET 9+) while sacrificing newer language features. Selenium 4.40.0 provides both CDP (detailed Chromium capture) and INetwork API (cross-browser fallback). System.Text.Json (10.0.3) delivers 2-3x faster HAR serialization than Newtonsoft.Json with half the memory allocation. The development stack uses xUnit for async-first testing, FluentAssertions for complex HAR structure validation, and MinVer for automatic git-tag-based versioning.

**Core technologies:**
- **netstandard2.0**: Maximum compatibility across .NET Framework 4.6.1+, .NET Core 2.0+, .NET 5-9+
- **Selenium.WebDriver 4.40.0**: Provides CDP Network domain access and INetwork API fallback
- **System.Text.Json 10.0.3**: HAR JSON serialization with 2-3x performance advantage over Newtonsoft.Json
- **Chrome DevTools Protocol (CDP)**: Primary capture mechanism for detailed network events (8+ per request)
- **xUnit 2.9.3+**: Async-first test framework for Selenium's async WebDriver patterns
- **MinVer 7.0.0**: Git-tag-based semantic versioning (no manual version management)

**Anti-recommendations:**
- **Selenium 3.x**: Missing CDP and INetwork APIs required for capture
- **Newtonsoft.Json**: Slower and higher memory usage unless exotic JSON features needed
- **netstandard2.1 or net8.0 as sole target**: Fragments user base; netstandard2.0 covers all scenarios
- **Browser-specific drivers as dependencies**: Library should work with any IWebDriver instance

### Expected Features

Research shows clear feature tiers based on HAR specification requirements, competitive libraries, and real-world debugging needs.

**Must have (table stakes):**
- **Basic HAR Export**: Valid HAR 1.2 JSON output (core value proposition)
- **Request/Response Headers**: Essential for HTTP debugging
- **HTTP Timing Data**: DNS, connect, wait, receive timings for performance analysis
- **HTTP Status Codes**: Fundamental for understanding request success/failure
- **Multiple Request Capture**: Real pages generate hundreds of requests
- **Start/Stop Control**: Explicit capture lifecycle management
- **Cookies**: Required for session debugging

**Should have (competitive differentiators):**
- **CDP + INetwork Fallback**: Auto-detection and graceful degradation across browsers
- **URL Pattern Filtering**: Reduce HAR size by filtering images, analytics, etc.
- **Content Size Limits**: Prevent memory issues with large responses (configurable truncation)
- **Binary Content Encoding**: Proper base64 encoding for images/PDFs with "encoding" attribute
- **Async/Await API**: Modern C# idiom throughout (avoids blocking)
- **Thread-Safe Operation**: Safe concurrent Selenium operations

**Defer (v2+):**
- **Sensitive Data Sanitization**: Auto-remove auth headers/tokens (security feature)
- **Page Reference Support**: Organize multi-page sessions with HAR page grouping
- **WebSocket Message Capture**: Modern apps use WebSockets (CDP-only, custom field)
- **Request/Response Modification**: Error simulation via CDP Fetch domain
- **Incremental Export**: Stream HAR as captured for memory efficiency
- **Compression Detection**: Decode gzip/brotli, report transfer vs content size

**Anti-features (explicitly avoid):**
- **HAR visualization UI**: Use existing tools (HAR Analyzer, DebugBear, GTmetrix)
- **Network proxy mode**: BrowserMob complexity (certificates, ports) not needed
- **HAR import/replay**: Different problem domain (mocking/stubbing)
- **Automatic performance analysis**: Provide raw data, let users analyze
- **Multiple output formats**: HAR only; users convert if needed

### Architecture Approach

The architecture uses Strategy pattern to encapsulate CDP vs INetwork capture implementations behind a common ICaptureStrategy interface. A facade (HarCapture) provides simplified public API, selecting strategy based on driver capabilities at runtime. Request/response correlation uses ConcurrentDictionary with Lazy initialization to ensure thread-safe, run-once entry creation across async CDP events. The Builder pattern separates HAR construction logic from capture strategy, converting internal RequestResponseEntry models to HAR 1.2 structure with proper field mappings and ISO 8601 formatting.

**Major components:**
1. **HarCapture (Facade)**: Public API managing strategy selection, lifecycle (Start/Stop), IDisposable cleanup
2. **ICaptureStrategy**: Strategy interface with CdpStrategy (CDP Network domain events) and INetworkStrategy (Selenium INetwork API) implementations
3. **RequestResponseCorrelator**: Thread-safe ConcurrentDictionary<string, Lazy<RequestResponseEntry>> for pairing requests with responses across multiple async events
4. **HarBuilder**: Converts RequestResponseEntry models to HAR 1.2 structure (Log → Entries → Request/Response/Timings)
5. **HarSerializer**: JSON serialization to HAR 1.2 format with System.Text.Json, ensuring camelCase and ISO 8601 dates
6. **HAR Model**: POCO classes representing HAR specification (Log, Entry, Request, Response, Timings, etc.)

**Key patterns:**
- **Event-based capture (not proxy)**: Subscribe to browser CDP/INetwork events, avoiding proxy setup complexity, certificate warnings, and container issues
- **ConcurrentDictionary + Lazy**: Thread-safe request/response correlation without manual locking; Lazy ensures initialization runs once
- **Event buffering**: Collect ALL events for a requestId, merge only after loadingFinished (handles out-of-order arrival)
- **Opt-in response bodies**: Don't capture by default (massive memory overhead); require explicit configuration
- **ConfigureAwait(false) everywhere**: Library code must avoid deadlocks when consumers call synchronously

**Build order (dependency graph):**
1. **Phase 1**: HAR model classes + ICaptureStrategy interface (foundation, no dependencies)
2. **Phase 2**: RequestResponseCorrelator (depends on models, critical for thread safety)
3. **Phase 3**: CdpStrategy (depends on correlator, primary mechanism, complex event handling)
4. **Phase 4**: INetworkStrategy (depends on correlator, simpler than CDP, fallback)
5. **Phase 5**: HarBuilder (depends on models and correlation data, pure transformation)
6. **Phase 6**: HarSerializer (depends on HAR structure, straightforward JSON)
7. **Phase 7**: HarCapture facade (depends on all, orchestrates system)

### Critical Pitfalls

Research identified 14 documented pitfalls with verified mitigation strategies. The top risks involve CDP event complexity and thread safety.

1. **CDP Event Ordering Race Conditions**: `requestWillBeSentExtraInfo` and `requestWillBeSent` have no guaranteed order; same for response events. **Prevention**: Buffer ALL events per requestId, merge only after `loadingFinished`. Special-case navigation requests (type: "Document").

2. **Network.getResponseBody Timing Race**: Calling `getResponseBody` too late fails with "No resource with given identifier found" as browser cleans up memory. **Prevention**: Call IMMEDIATELY in `responseReceived` handler, implement retry with exponential backoff, accept graceful failure for unavailable bodies.

3. **ConcurrentDictionary.GetOrAdd Factory Not Thread-Safe**: Factory delegate runs OUTSIDE lock, can execute multiple times for same key, creating duplicate objects that get discarded. **Prevention**: Use `Lazy<T>` pattern: `ConcurrentDictionary<string, Lazy<HarEntry>>` ensures single initialization.

4. **ConfigureAwait(false) Omission**: Library code without `.ConfigureAwait(false)` deadlocks when consumers call `.Result` or `.Wait()` on async methods. **Prevention**: ALL awaits MUST use `ConfigureAwait(false)`, enable CA2007 analyzer, document "await library methods, don't block."

5. **HTTP Redirect Correlation**: Redirects reuse SAME requestId for entire chain (301 → 302 → 200). Treating each `requestWillBeSent` as new request creates duplicate entries. **Prevention**: Track redirect chains per requestId, detect 3xx status codes, populate HAR `redirectURL` field.

**Additional critical items:**
- **Binary response encoding**: Always set `"encoding": "base64"` attribute when base64-encoding binary content, or HAR parsers fail
- **HAR timing calculation**: Exclude -1 values from sum; ensure `entry.time` equals sum of timing phases (validation requirement)
- **HTTP 304 No Content**: Check status code before calling `getResponseBody` (304/204 have no body by spec)
- **CDP event subscription leaks**: Implement IDisposable, unsubscribe ALL handlers, call `Network.Disable()` before disposing session
- **CDP version dependency**: Document supported Chrome versions (115+), implement INetwork fallback for non-Chromium browsers

## Implications for Roadmap

Based on research, the phase structure follows dependency order and risk mitigation:

### Phase 1: Core Models and Event Correlation
**Rationale:** Foundation must be solid before capture logic. HAR models define contracts for entire system. RequestResponseCorrelator is critical component used by both strategies and requires careful thread-safety implementation.

**Delivers:**
- HAR 1.2 POCO model classes (Log, Entry, Request, Response, Timings, Header, Cookie, etc.)
- ICaptureStrategy interface definition
- RequestResponseEntry (internal correlation model)
- RequestResponseCorrelator with ConcurrentDictionary + Lazy pattern
- Comprehensive unit tests for thread safety

**Addresses:**
- Table stakes: Basic HAR Export structure
- Pitfall #3: ConcurrentDictionary thread-safety (Lazy pattern from start)

**Avoids:**
- Architecture anti-pattern: mixing strategy-specific code in models
- Pitfall: assuming event ordering (design for out-of-order from start)

### Phase 2: CDP Strategy Implementation
**Rationale:** Primary capture mechanism delivers most value. CDP provides detailed network data (8+ events per request vs INetwork's 2). Most complex event handling, so tackle early to surface integration issues.

**Delivers:**
- CdpStrategy class implementing ICaptureStrategy
- CDP Network domain event subscriptions (requestWillBeSent, responseReceived, dataReceived, loadingFinished/Failed)
- Event buffering for out-of-order arrival
- Immediate getResponseBody retrieval with retry logic
- HTTP redirect chain tracking (same requestId handling)
- HTTP 304/204 status code checks
- IDisposable implementation with proper event unsubscribe
- ConfigureAwait(false) on all async operations

**Addresses:**
- Table stakes: Request/Response Headers, HTTP Timing Data, HTTP Status Codes, Multiple Request Capture, Cookies
- Differentiator: CDP as primary strategy (detailed capture)
- Pitfall #1: Event ordering race conditions (buffering)
- Pitfall #2: getResponseBody timing race (immediate retrieval)
- Pitfall #4: ConfigureAwait deadlocks (false everywhere)
- Pitfall #5: Redirect correlation (track chains)
- Pitfall #9: HTTP 304 handling (status checks)
- Pitfall #10: Event subscription leaks (IDisposable cleanup)

**Avoids:**
- Anti-pattern: blocking on CDP event handlers (keep fast)
- Anti-pattern: assuming event order (use buffering)
- Pitfall: missing request bodies for POST/PUT (set maxPostDataSize in Network.enable)

### Phase 3: HAR Builder and Serializer
**Rationale:** Once capture works, convert to HAR format. Pure transformation logic (deterministic, testable). No threading concerns. Serialization is straightforward with System.Text.Json.

**Delivers:**
- HarBuilder class converting RequestResponseEntry → HAR structure
- Timestamp formatting (ISO 8601 for startedDateTime)
- Header parsing, query string extraction from URLs
- Binary content detection and base64 encoding with "encoding" attribute
- HAR timing calculation excluding -1 values, validating sum
- HarSerializer with System.Text.Json configuration
- HAR 1.2 format compliance validation

**Addresses:**
- Table stakes: Basic HAR Export (complete implementation)
- Table stakes: Save to File capability
- Differentiator: Binary Content Encoding (base64 with attribute)
- Pitfall #6: Base64 encoding attribute (always set)
- Pitfall #8: HAR timing calculation (correct sum)

**Avoids:**
- Pitfall: capturing response bodies by default (opt-in only)
- Anti-pattern: mixing serialization logic in strategy classes

### Phase 4: INetwork Strategy Fallback
**Rationale:** After core CDP works, add cross-browser support. Similar structure to Phase 2 (reuse patterns) but simpler (fewer events). Lower priority as CDP is primary mechanism.

**Delivers:**
- INetworkStrategy class implementing ICaptureStrategy
- Selenium INetwork API event handlers (NetworkRequestSent, NetworkResponseReceived)
- Integration with RequestResponseCorrelator
- Graceful degradation (less detailed data than CDP)

**Addresses:**
- Differentiator: CDP + INetwork Fallback (reliability across browsers)
- Pitfall #7: CDP version dependency (INetwork works on Firefox/Safari)

**Avoids:**
- Anti-pattern: duplicating correlation logic (reuse correlator)

### Phase 5: Public API Facade
**Rationale:** After all components exist, provide clean public interface. Orchestrates system with strategy selection logic and lifecycle management.

**Delivers:**
- HarCapture class (facade pattern)
- Auto-detection: CDP available → CdpStrategy, else INetworkStrategy
- Start/Stop lifecycle methods
- IDisposable implementation (delegates to strategy)
- Configuration options (URL filtering, content size limits, opt-in body capture)
- Thread-safe state management
- Clear exception messages for unsupported drivers

**Addresses:**
- Table stakes: Start/Stop Control
- Differentiator: Async/Await API (modern C#)
- Differentiator: Thread-Safe Operation
- Differentiator: URL Pattern Filtering (configuration)
- Differentiator: Content Size Limits (configuration)

**Avoids:**
- Anti-pattern: mixing strategy-specific code in facade
- Anti-pattern: exposing internal complexity to consumers

### Phase 6: Testing and Validation
**Rationale:** After implementation complete, comprehensive testing ensures reliability and HAR spec compliance.

**Delivers:**
- xUnit test project with async test support
- FluentAssertions for HAR structure validation
- Unit tests: RequestResponseCorrelator thread safety, HarBuilder transformations, timing calculations
- Integration tests: CDP strategy with ChromeDriver, INetwork strategy with multiple browsers
- HAR schema validation tests
- Performance tests: 100, 1000, 10000 request scenarios
- Memory leak detection tests (CDP subscription cleanup)

**Addresses:**
- Confidence validation for all critical pitfalls
- HAR 1.2 spec compliance verification

**Avoids:**
- Pitfall: untested thread-safety assumptions
- Pitfall: unvalidated HAR output (schema compliance)

### Phase 7: Packaging and Documentation
**Rationale:** After code is stable and tested, prepare for NuGet distribution and consumer onboarding.

**Delivers:**
- .csproj package metadata (PackageId, Description, Tags, License)
- README.md with quick start examples
- XML documentation comments (IntelliSense support)
- SourceLink configuration (Microsoft.SourceLink.GitHub)
- MinVer setup for git-tag versioning
- NuGet package generation and symbol package (.snupkg)
- GitHub repository setup with CI/CD

**Addresses:**
- Professional library presentation
- NuGet discoverability (tags: selenium;har;http-archive;webdriver;network;cdp;devtools)
- Developer experience (IntelliSense, source stepping)

### Phase Ordering Rationale

- **Foundation first**: Models and correlator have no dependencies, establish contracts
- **CDP before INetwork**: Primary mechanism more complex, surface issues early
- **Serialization after capture**: Depends on working correlation data
- **Facade last**: Requires all components to orchestrate
- **Testing throughout**: Each phase includes unit tests, integration tests after Phase 4
- **Risk mitigation**: Thread-safety patterns (Phase 1), CDP event complexity (Phase 2), timing calculations (Phase 3) addressed in order
- **Dependency flow**: No backward dependencies; each phase builds on previous

### Research Flags

Phases likely needing deeper research during planning:
- **Phase 2 (CDP Strategy)**: Complex CDP Network domain event choreography; may need supplemental research on redirect handling, body retrieval timing, specific CDP command parameters
- **Phase 3 (HAR Builder)**: HAR 1.2 spec interpretation for edge cases (e.g., WebSocket messages in custom fields, partial responses); validation against multiple HAR parsers

Phases with standard patterns (skip research-phase):
- **Phase 1 (Core Models)**: Well-documented POCO patterns, ConcurrentDictionary + Lazy is proven pattern
- **Phase 4 (INetwork Strategy)**: Simpler than CDP, reuses Phase 2 patterns
- **Phase 5 (Facade)**: Standard facade/strategy pattern implementation
- **Phase 6 (Testing)**: xUnit and FluentAssertions are well-documented
- **Phase 7 (Packaging)**: Standard .NET library packaging, NuGet best practices documented

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Stack | HIGH | netstandard2.0, Selenium 4.40.0, System.Text.Json all verified with official docs and NuGet versions. HAR 1.2 spec frozen since 2011. |
| Features | HIGH | Feature tiers derived from HAR spec (mandatory fields), BrowserMob Proxy capabilities (competitive baseline), and multiple CDP library examples. Clear MVP scope. |
| Architecture | MEDIUM | Strategy pattern, Builder pattern, thread-safe correlation all well-documented. CDP event flow verified with official protocol docs. Build order logical but untested in practice. |
| Pitfalls | MEDIUM | 14 pitfalls documented with verified sources (GitHub issues, official protocol docs, Microsoft guidance). Prevention strategies proven but require disciplined implementation. |

**Overall confidence:** HIGH

### Gaps to Address

Research was thorough but some areas need validation during implementation:

- **CDP version variability**: Research documents Chrome 115+ support, but specific CDP Network domain API changes between versions may surface. **Mitigation**: Test against Chrome Stable, Beta, Dev channels during Phase 2; document minimum Chrome version based on actual testing.

- **INetwork API maturity**: Selenium INetwork API is newer and less documented than CDP. Real-world cross-browser behavior (Firefox, Edge, Safari) needs validation. **Mitigation**: Phase 4 includes explicit cross-browser integration tests; document browser-specific limitations discovered.

- **HAR 1.2 interpretation for modern features**: HAR spec predates WebSockets, HTTP/2, QUIC. How to represent these in HAR 1.2 format requires research or pragmatic decisions. **Mitigation**: Phase 3 research on custom fields (e.g., `_webSocketMessages`), consult other HAR exporters (Chrome DevTools, Firefox) for precedent.

- **System.Text.Json netstandard2.0 compatibility**: Research notes potential assembly version conflicts. Real impact on consumers unclear. **Mitigation**: Phase 1 includes multi-target framework testing (.NET Framework 4.6.1, .NET Core 3.1, .NET 6+); consider Newtonsoft.Json if conflicts emerge.

- **Performance at scale**: Research provides scalability guidance (100, 10K, 100K+ requests) but no empirical data for this library's implementation. **Mitigation**: Phase 6 includes performance benchmarks; implement streaming/eviction if memory issues surface.

## Sources

### Primary (HIGH confidence)
- [HAR 1.2 Specification](http://www.softwareishard.com/blog/har-12-spec/) - Official HTTP Archive format specification
- [W3C HTTP Archive Format](https://w3c.github.io/web-performance/specs/HAR/Overview.html) - W3C community specification
- [Chrome DevTools Protocol - Network domain](https://chromedevtools.github.io/devtools-protocol/tot/Network/) - Official CDP specification
- [.NET Standard specification](https://learn.microsoft.com/en-us/dotnet/standard/net-standard) - Target framework guidance
- [Selenium WebDriver documentation](https://www.selenium.dev/documentation/webdriver/) - Official Selenium docs
- [Selenium CDP documentation](https://www.selenium.dev/documentation/webdriver/bidi/cdp/) - CDP integration in Selenium
- [NuGet Package authoring best practices](https://learn.microsoft.com/en-us/nuget/create-packages/package-authoring-best-practices) - Packaging guidance
- [Microsoft CA2007 rule](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/ca2007) - ConfigureAwait guidance
- [Microsoft ConfigureAwait FAQ](https://devblogs.microsoft.com/dotnet/configureawait-faq/) - Async/await best practices
- [Andrew Lock - Making GetOrAdd Thread Safe Using Lazy](https://andrewlock.net/making-getoradd-on-concurrentdictionary-thread-safe-using-lazy/) - Thread-safety pattern

### Secondary (MEDIUM confidence)
- [NuGet Gallery: Selenium.WebDriver 4.40.0](https://www.nuget.org/packages/Selenium.WebDriver) - Version verification
- [NuGet Gallery: System.Text.Json 10.0.3](https://www.nuget.org/packages/System.Text.Json) - Version verification
- [NuGet Gallery: xUnit 2.9.3+](https://www.nuget.org/packages/xunit) - Test framework
- [NuGet Gallery: FluentAssertions 8.8.0](https://www.nuget.org/packages/fluentassertions/) - Assertion library
- [NuGet Gallery: MinVer 7.0.0](https://www.nuget.org/packages/MinVer/) - Versioning tool
- [PyCDP Network API Documentation](https://py-cdp.readthedocs.io/en/latest/api/network.html) - CDP event reference
- [chromedp issue #1317](https://github.com/chromedp/chromedp/issues/1317) - getResponseBody timing issue
- [puppeteer issue #2258](https://github.com/puppeteer/puppeteer/issues/2258) - Response body retrieval failures
- [dotnet/runtime issue #33221](https://github.com/dotnet/runtime/issues/33221) - ConcurrentDictionary thread-safety
- [Mozilla bug 1221754](https://bugzilla.mozilla.org/show_bug.cgi?id=1221754) - HAR base64 encoding attribute
- [BrowserMob Proxy capabilities](https://github.com/lightbody/browsermob-proxy) - Competitive feature analysis
- [selenium-capture (Java)](https://github.com/mike10004/selenium-capture) - Reference implementation
- [HarSharp (.NET)](https://github.com/giacomelli/HarSharp) - Existing .NET HAR library

### Tertiary (LOW confidence)
- [System.Text.Json performance benchmarks](https://trevormccubbin.medium.com/net-performance-analysis-newtonsoft-json-vs-system-text-json-in-net-9-1ac673502dbf) - Serialization comparison
- [xUnit vs NUnit vs MSTest comparison](https://medium.com/@robertdennyson/xunit-vs-nunit-vs-mstest-choosing-the-right-testing-framework-for-net-applications-b6b9b750bec6) - Test framework selection

---
*Research completed: 2026-02-19*
*Ready for roadmap: yes*
