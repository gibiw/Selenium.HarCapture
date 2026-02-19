# Feature Landscape

**Domain:** Selenium HAR Capture Library
**Researched:** 2026-02-19

## Table Stakes

Features users expect. Missing = product feels incomplete.

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| Basic HAR Export | Core purpose of library - export traffic as valid HAR 1.2 JSON | Low | Must produce valid HAR JSON format |
| Request/Response Headers | Essential for debugging HTTP issues - headers contain critical diagnostic info | Low | Part of HAR spec, CDP/INetwork provide this |
| Request/Response Bodies | Users need to see actual payload data for debugging | Medium | Must handle text and binary content, encoding issues |
| HTTP Timing Data | Performance analysis requires DNS, connect, wait, receive timings | Medium | CDP provides via Network.loadingFinished events |
| Cookies | Session debugging requires cookie visibility | Low | Part of standard HAR capture |
| HTTP Status Codes | Fundamental to understanding request success/failure | Low | Basic field in response object |
| Multiple Request Capture | Real pages make dozens/hundreds of requests - must capture all | Medium | Event-based listening, memory management |
| Start/Stop Control | Users need to control when capture begins/ends | Low | Simple state management |
| Save to File | Standard output format - write HAR to disk | Low | JSON serialization to file |

## Differentiators

Features that set product apart. Not expected, but valued.

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| CDP + INetwork Fallback | Reliability - works even when CDP unavailable | High | Two implementations, auto-detection logic |
| URL Pattern Filtering | Performance - reduce HAR size by filtering unwanted requests (images, analytics) | Medium | Regex or glob patterns to include/exclude URLs |
| Content Size Limits | Prevents memory issues with large responses (videos, downloads) | Medium | Configurable max body size, truncation handling |
| Sensitive Data Sanitization | Security - auto-remove auth headers, cookies, tokens before export | Medium | Pattern matching for sensitive fields, opt-in feature |
| Page Reference Support | Organize multi-page sessions - HAR spec supports page grouping | Medium | Track navigation events, associate requests with pages |
| Request/Response Modification | Testing - modify requests/responses for error simulation | High | Requires CDP Fetch domain, not available in INetwork |
| WebSocket Message Capture | Modern apps use WebSockets - capture WS frames in HAR | High | CDP supports, custom _webSocketMessages field |
| Async/Await API | Modern C# idiom - async methods throughout | Medium | Event-based CDP requires careful async handling |
| Incremental Export | Memory efficiency - stream entries as captured, not batch at end | High | Requires careful JSON streaming, partial writes |
| HAR Schema Validation | Quality assurance - validate output against HAR 1.2 schema | Low | JSON schema validation, optional feature |
| Binary Content Encoding | Proper base64 encoding for images/PDFs in HAR | Medium | Detect content type, encode appropriately |
| Compression Detection | Decode gzip/brotli responses, report transfer vs content size | Medium | Check Content-Encoding header, decompress for HAR |
| Performance Metrics | Export browser timing API data (DOMContentLoaded, onLoad) | Low | CDP provides via Page.loadEventFired, Page.domContentEventFired |
| Thread-Safe Operation | Reliability - handle concurrent Selenium operations | High | Thread-safe event handlers, collection management |

## Anti-Features

Features to explicitly NOT build.

| Anti-Feature | Why Avoid | What to Do Instead |
|--------------|-----------|-------------------|
| HAR Visualization/Analysis UI | Scope creep - many excellent tools exist (HAR Analyzer, DebugBear, GTmetrix) | Export valid HAR, let specialized tools visualize |
| Network Traffic Manipulation (proxy mode) | Complexity explosion - BrowserMob approach requires proxy server, certificate management | Use CDP for targeted interception only if needed |
| Cross-browser CDP polyfill | Maintenance nightmare - Firefox/Safari have different protocols | Document as Chromium-only for CDP, INetwork for cross-browser basics |
| HAR Import/Replay | Different problem domain - moves into mocking/stubbing territory | Focus on capture, not replay |
| Automatic Performance Analysis | Feature creep - metrics/recommendations require domain expertise | Provide raw data, let users/tools analyze |
| Built-in HAR Sanitizer UI | Scope creep - enterprise tools (HARmor, har-sanitizer) handle this | Provide optional sanitization API, not UI |
| Bandwidth Throttling | Wrong layer - BrowserMob feature, requires proxy | Document CDP Network.emulateNetworkConditions for users who need it |
| Request Blacklist/Whitelist | Filtering yes, blocking no - blocking changes behavior under test | Provide post-capture filtering, not request blocking |
| Multiple Output Formats | Maintenance burden - HAR is the standard | HAR only, users can convert if needed |

## Feature Dependencies

```
Basic HAR Export → Request/Response Headers (requires export)
Request/Response Headers → Request/Response Bodies (builds on header capture)
Request/Response Bodies → Binary Content Encoding (handles non-text bodies)
Request/Response Bodies → Compression Detection (decodes encoded bodies)
Start/Stop Control → Multiple Request Capture (controls capture scope)
Multiple Request Capture → Page Reference Support (organizes captured requests)
Multiple Request Capture → Content Size Limits (prevents memory issues)
CDP + INetwork Fallback → All capture features (underlying mechanism)
Sensitive Data Sanitization → Request/Response Headers (modifies headers)
Sensitive Data Sanitization → Cookies (removes sensitive cookies)
```

## MVP Recommendation

Prioritize (in order):

1. **Basic HAR Export** - Core value proposition
2. **Request/Response Headers** - Diagnostic essential
3. **HTTP Timing Data** - Performance use case
4. **HTTP Status Codes** - Debugging essential
5. **Multiple Request Capture** - Real-world requirement
6. **Start/Stop Control** - Usability essential
7. **Save to File** - Output requirement
8. **CDP + INetwork Fallback** - Key differentiator
9. **Cookies** - Session debugging
10. **Request/Response Bodies** (text only) - Common debugging need

Defer for post-MVP:

- **Binary Content Encoding** - Nice-to-have, not critical for most use cases
- **WebSocket Message Capture** - Niche requirement
- **Request/Response Modification** - Advanced feature
- **Page Reference Support** - Organizational feature
- **Sensitive Data Sanitization** - Security feature for later phases
- **Content Size Limits** - Optimization feature
- **Incremental Export** - Performance optimization
- **All other differentiators** - Add based on user feedback

## Implementation Complexity Notes

### High Complexity Features
- **CDP + INetwork Fallback**: Requires two complete implementations, auto-detection, graceful fallback
- **Request/Response Modification**: Requires CDP Fetch domain, complex request lifecycle management
- **WebSocket Message Capture**: Non-standard HAR field, complex message tracking
- **Incremental Export**: JSON streaming, partial write handling, error recovery
- **Thread-Safe Operation**: Concurrent event handling, lock management, race condition prevention

### Medium Complexity Features
- **Request/Response Bodies**: Encoding detection, binary handling, size limits
- **HTTP Timing Data**: Event correlation, timing calculation from CDP events
- **Multiple Request Capture**: Event subscription, collection management, memory considerations
- **URL Pattern Filtering**: Regex/glob matching, configuration API
- **Content Size Limits**: Truncation logic, metadata preservation
- **Sensitive Data Sanitization**: Pattern detection, safe removal without breaking structure
- **Page Reference Support**: Navigation tracking, request-to-page association
- **Binary Content Encoding**: Content-Type detection, base64 encoding
- **Compression Detection**: Content-Encoding handling, decompression
- **Async/Await API**: Event-based CDP → Task-based pattern conversion

### Low Complexity Features
- **Basic HAR Export**: JSON serialization, schema compliance
- **Request/Response Headers**: Direct field mapping
- **Cookies**: Direct field extraction
- **HTTP Status Codes**: Direct field mapping
- **Start/Stop Control**: Boolean flag, event subscription management
- **Save to File**: File I/O, error handling
- **HAR Schema Validation**: JSON schema library integration
- **Performance Metrics**: Event subscription, field mapping

## Cross-Cutting Concerns

### Memory Management
Large HAR files (>100MB) are common with rich web apps. Need strategy for:
- Limiting body content size
- Streaming/incremental export
- Efficient event handler cleanup

### Error Handling
Network capture must be resilient:
- CDP connection failures → fallback to INetwork
- Missing fields in CDP events → safe defaults
- JSON serialization errors → partial export capability

### Performance Impact
Capture should minimize test overhead:
- Async event handling
- Lazy body content retrieval
- Optional features disabled by default

### API Design
.NET developers expect:
- Async/await throughout
- IDisposable for resource cleanup
- Fluent configuration API
- Standard cancellation token support

## Sources

**BrowserMob Proxy capabilities:**
- [BrowserMob Proxy GitHub](https://github.com/lightbody/browsermob-proxy)
- [BrowserMob Proxy README](https://github.com/lightbody/browsermob-proxy/blob/master/README.md)
- [Performance Capture with BrowserMob-Proxy](https://dzone.com/articles/performance-capture-i-export-har-using-selenium-an)

**HAR format specification:**
- [HAR 1.2 Spec](http://www.softwareishard.com/blog/har-12-spec/)
- [W3C HTTP Archive Format](https://w3c.github.io/web-performance/specs/HAR/Overview.html)
- [Understanding HAR Files](https://support.peoplefluent.com/hc/en-us/articles/47740509887513-Understanding-HAR-Files)
- [Comprehensive Guide on HAR Files](https://www.keysight.com/blogs/en/tech/nwvs/2022/05/27/a-comprehensive-guide-on-har-files)

**Selenium 4 and CDP integration:**
- [Selenium 4 HAR Capture with CDP](https://medium.com/bliblidotcom-techblog/blibli-automation-journey-how-to-capture-network-traffic-with-har-utility-in-selenium-4-379ad03386d7)
- [Capturing HAR Files in Selenium](https://octopus.com/blog/selenium/13-capturing-har-files/capturing-har-files)
- [Chrome DevTools Protocol](https://chromedevtools.github.io/devtools-protocol/)
- [Selenium CDP Documentation](https://www.selenium.dev/documentation/webdriver/bidi/cdp/)
- [Chrome DevTools Protocol Guide](https://applitools.com/blog/selenium-chrome-devtools-protocol-cdp-how-does-it-work/)

**Selenium INetwork API:**
- [Interface INetwork Documentation](https://www.selenium.dev/selenium/docs/api/dotnet/webdriver/OpenQA.Selenium.INetwork.html)
- [Selenium WebDriver Tutorial](https://www.browserstack.com/guide/selenium-webdriver-tutorial)
- [Selenium 4 Features](https://www.browserstack.com/guide/selenium-4-features)

**.NET CDP libraries:**
- [AsyncChromeDriver](https://github.com/ToCSharp/AsyncChromeDriver)
- [SeleniumCaptureHttpResponse](https://github.com/metaljase/SeleniumCaptureHttpResponse)
- [MasterDevs ChromeDevTools](https://github.com/MasterDevs/ChromeDevTools)
- [Capturing Network Activity in .NET Core](https://dotjord.wordpress.com/2020/09/13/how-to-capture-network-activity-with-selenium-4-in-asp-net-core-3-1/)

**Network interception and mocking:**
- [Intercepting and Mocking with Selenium CDP](https://blogs.perficient.com/2024/01/30/intercepting-and-mocking-network-responses-with-selenium-chrome-devtools/)
- [Selenium 4 Network Interception](https://rahulshettyacademy.com/blog/index.php/2021/11/04/selenium-4-key-feature-network-interception/)
- [Selenium DevTools Guide](https://www.browserstack.com/guide/selenium-devtools)
- [Chrome DevTools Network Features](https://www.selenium.dev/documentation/webdriver/bidi/cdp/network/)
- [CDP Protocol Comprehensive Guide](https://www.devzery.com/post/cdp-protocol-a-comprehensive-guide-for-selenium-testing)

**WebSocket support:**
- [WebSocket Traffic in HAR Capture](https://www.keysight.com/blogs/en/tech/nwvs/2022/07/23/looking-into-websocket-traffic-in-har-capture)
- [WebSocket messages in HAR exports](https://bugzilla.mozilla.org/show_bug.cgi?id=1575465)
- [Playwright WebSocket in HAR](https://github.com/microsoft/playwright/issues/30315)
- [Web Sockets in HAR proposal](https://groups.google.com/g/http-archive-specification/c/_DBaSKch_-s)

**Sensitive data and privacy:**
- [Introducing HAR Sanitizer](https://blog.cloudflare.com/introducing-har-sanitizer-secure-har-sharing/)
- [Google HAR Sanitizer](https://github.com/google/har-sanitizer)
- [Protect Sensitive Data in HAR Files](https://www.nightfall.ai/blog/how-to-discover-and-protect-sensitive-data-in-har-files)
- [Frontegg HARmor](https://frontegg.com/blog/frontegg-harmor)
- [Secure Sensitive Data in HAR Files](https://www.strac.io/blog/identify-and-secure-sensitive-data-in-har-file)

**Binary content and compression:**
- [Include content text in response](https://github.com/sitespeedio/chrome-har/issues/8)
- [Large response bodies truncated](https://bugzilla.mozilla.org/show_bug.cgi?id=1223726)
- [HAR encoding for base64 resources](https://bugzilla.mozilla.org/show_bug.cgi?id=1221754)
- [Reading HAR response body content](https://github.com/Netflix/pollyjs/issues/249)

**Performance and optimization:**
- [Analyzing HAR Files for Performance](https://www.browserstack.com/guide/http-archive-har-files)
- [Analyze HAR File Guide](https://webvizio.com/blog/how-to-download-view-and-analyze-har-files/)
- [shrink-har tool](https://github.com/markSmurphy/shrink-har)
- [HAR Files to Analyze Performance Over Time](https://www.bomberbot.com/performance/how-to-use-har-files-to-analyze-website-performance-over-time/)

**Waterfall visualization and timing:**
- [How to Read a Waterfall Chart](https://www.debugbear.com/docs/waterfall)
- [Navigating Waterfall Charts](https://docs.thousandeyes.com/product-documentation/browser-synthetics/navigating-waterfall-charts-for-page-load-and-transaction-tests)
- [Interpreting Waterfall Charts](https://help.rigor.com/hc/en-us/articles/115004750168-How-Do-I-Interpret-a-Waterfall-Chart)
- [Waterfall Chart for Beginners](https://gtmetrix.com/blog/how-to-read-a-waterfall-chart-for-beginners/)

**HAR schema and validation:**
- [har-schema GitHub](https://github.com/ahmadnassri/har-schema)
- [har/schema.json](https://github.com/jarib/har/blob/master/schema.json)
- [HAR Specification](https://www.w3.org/community/bigdata-tools/files/2017/10/HAR_Spec_TO_HAR_Vocabulary.pdf)

**.NET async patterns:**
- [Event-based Asynchronous Pattern](https://learn.microsoft.com/en-us/dotnet/standard/asynchronous-programming-patterns/event-based-asynchronous-pattern-overview)
- [Task-based Asynchronous Pattern](https://learn.microsoft.com/en-us/dotnet/standard/asynchronous-programming-patterns/)
- [How Async/Await Works in C#](https://devblogs.microsoft.com/dotnet/how-async-await-really-works/)
- [EAP to TAP Conversion](https://www.jenx.si/2019/10/02/c-from-event-based-asynchronous-pattern-to-task-based-asynchronous-pattern/)

**Common pitfalls:**
- [HAR export support feature request](https://github.com/SeleniumHQ/selenium/issues/10153)
- [Selenium HAR export errors](https://github.com/SeleniumHQ/selenium/issues/7706)
- [Collect requests without proxy](https://github.com/selenide/selenide/issues/190)

**Puppeteer Sharp:**
- [Debug Puppeteer Sharp network issues](https://webscraping.ai/faq/puppeteer-sharp/how-can-i-debug-puppeteer-sharp-s-network-issues)
- [Capture network traffic in Puppeteer Sharp](https://webscraping.ai/faq/puppeteer-sharp/how-can-i-capture-network-traffic-for-analysis-in-puppeteer-sharp)
- [Intercept requests with Puppeteer Sharp](https://webscraping.ai/faq/puppeteer-sharp/is-it-possible-to-intercept-network-requests-with-puppeteer-sharp)
- [Network Response Analysis in Puppeteer](https://latenode.com/blog/network-response-analysis-and-processing-in-puppeteer-monitoring-and-modification)

**Additional HAR tools:**
- [selenium-capture](https://github.com/mike10004/selenium-capture)
- [request-har wrapper](https://github.com/maciejmaciejewski/request-har)
- [Selenium Wire PyPI](https://pypi.org/project/selenium-wire/)
