# Domain Pitfalls: Selenium HAR Capture via CDP/INetwork

**Domain:** C#/.NET Selenium HAR capture library with CDP Network domain
**Researched:** 2026-02-19
**Confidence:** MEDIUM (WebSearch-verified with official CDP/HAR spec cross-reference)

---

## Critical Pitfalls

Mistakes that cause rewrites, data loss, or major correctness issues.

---

### Pitfall 1: CDP Event Ordering Race Conditions

**What goes wrong:**
`requestWillBeSentExtraInfo` and `requestWillBeSent` events have no guaranteed ordering. The same applies to response events. You may receive `responseReceivedExtraInfo` before or after `responseReceived` for the same request. Additionally, during page navigation, `requestWillBeSent` can fire AFTER the request has been sent and response received, but just BEFORE `responseReceived` fires.

**Why it happens:**
CDP Network domain events are fired by different parts of the browser stack. Extra info events come from lower-level network stack components, while main events come from higher-level request tracking. Navigation requests have special handling that violates normal event ordering.

**Consequences:**
- Incomplete HAR entries (missing headers, cookies, or timing data)
- Race condition where you try to correlate events that haven't arrived yet
- Corrupted request/response pairs where data from different requests gets mixed
- Navigation requests appearing out of order in HAR timeline

**Prevention:**
- Do NOT assume `requestWillBeSent` arrives first. Wait for BOTH `requestWillBeSent` and `requestWillBeSentExtraInfo` (if enabled).
- Use buffering: collect ALL events for a requestId, then merge them only after `loadingFinished` or `loadingFailed`.
- Track event arrival order with timestamps for debugging.
- Special-case navigation requests: detect via `type: "Document"` and handle ordering differently.

**Detection:**
- HAR validation shows missing `request.headers` or `request.cookies`
- Response data appears before request data in logs
- Navigation requests have incomplete timing data
- Unit tests with synthetic event ordering reveal correlation failures

**Phase mapping:** Phase 1 (Core CDP Event Handling) MUST address this. Critical for correctness.

**Source confidence:** HIGH - [Chrome DevTools Protocol Network domain](https://chromedevtools.github.io/devtools-protocol/tot/Network/), [CDP event timing GitHub issue](https://github.com/ChromeDevTools/devtools-protocol/issues/73)

---

### Pitfall 2: Network.getResponseBody Timing Race Condition

**What goes wrong:**
Calling `Network.getResponseBody` immediately after `responseReceived` or even after `loadingFinished` can fail with "No resource with given identifier found." The browser may have already cleaned up the response body from memory.

**Why it happens:**
Browser's internal resource cleanup runs asynchronously. Once the page has processed a response, Chrome may discard the body to free memory, especially for:
- Large responses
- Cached responses (304 Not Modified)
- CORS preflight OPTIONS requests
- Responses consumed by JavaScript (fetch, XHR)

**Consequences:**
- Missing `response.content.text` in HAR entries
- Intermittent failures that are hard to reproduce
- Complete data loss for specific response types
- Production HAR files with incomplete network data

**Prevention:**
- Call `Network.getResponseBody` IMMEDIATELY in the `responseReceived` handler, before any async operations.
- Implement retry logic with exponential backoff (2-3 attempts max).
- Accept failure gracefully: populate HAR with headers/timing but mark body as unavailable.
- For critical responses, enable `Network.enable` with `maxResourceBufferSize` and `maxPostDataSize` parameters to hint browser to retain data longer.
- Alternative: subscribe to `Network.dataReceived` and buffer chunks yourself (more memory, but reliable).

**Detection:**
- Error logs: "Protocol error (Network.getResponseBody): No resource with given identifier found"
- HAR entries with complete headers but empty `content.text`
- Specific failure rate for XHR/fetch requests vs navigation requests
- Debugging shows successful event capture but failed body retrieval

**Phase mapping:** Phase 1 (Core CDP Event Handling) MUST implement immediate retrieval. Phase 2 (Reliability) adds retry logic and chunk buffering fallback.

**Source confidence:** HIGH - [chromedp issue #1317](https://github.com/chromedp/chromedp/issues/1317), [puppeteer issue #2258](https://github.com/puppeteer/puppeteer/issues/2258), [SeleniumBase discussion #2731](https://github.com/seleniumbase/SeleniumBase/discussions/2731)

---

### Pitfall 3: ConcurrentDictionary.GetOrAdd Is Not Thread-Safe for Factory Methods

**What goes wrong:**
Using `ConcurrentDictionary<string, HarEntry>.GetOrAdd(requestId, id => new HarEntry())` for request/response correlation can execute the factory delegate MULTIPLE TIMES for the same key when multiple threads request it simultaneously. This creates duplicate `HarEntry` objects, and different threads may operate on different instances.

**Why it happens:**
`ConcurrentDictionary` uses fine-grained locking. The `GetOrAdd` delegate is called OUTSIDE the lock to avoid deadlocks from executing unknown code. If threads A and B both check for `requestId` simultaneously, both may call the factory, and only one result is stored (the other is discarded).

**Consequences:**
- Race condition: events for the same request update DIFFERENT `HarEntry` objects
- Lost data: some events populate the discarded entry that never makes it to HAR
- Timing corruption: request events go to one object, response events to another
- Intermittent test failures that disappear under debugging (timing changes)

**Prevention:**
- Use `Lazy<HarEntry>` pattern:
  ```csharp
  private readonly ConcurrentDictionary<string, Lazy<HarEntry>> _entries = new();

  var entry = _entries.GetOrAdd(requestId,
      id => new Lazy<HarEntry>(() => new HarEntry { RequestId = id }))
      .Value;
  ```
- The `Lazy<T>.Value` getter is thread-safe and ensures single initialization.
- Alternative: use lock-based Dictionary if contention is low (simpler, may be faster).
- Never perform heavy initialization (I/O, serialization) inside GetOrAdd factory.

**Detection:**
- HAR has duplicate entries with same URL but incomplete data
- Thread-safety stress tests show data loss
- Logging reveals factory delegate executing multiple times for same requestId
- ConcurrentDictionary count doesn't match number of factory executions

**Phase mapping:** Phase 1 (Core CDP Event Handling) MUST use `Lazy<T>` pattern from the start. Critical for multi-threaded correctness.

**Source confidence:** HIGH - [dotnet/runtime issue #33221](https://github.com/dotnet/runtime/issues/33221), [Andrew Lock blog](https://andrewlock.net/making-getoradd-on-concurrentdictionary-thread-safe-using-lazy/), [Bar Arnon blog](http://blog.i3arnon.com/2018/01/16/concurrent-dictionary-tolist/)

---

### Pitfall 4: ConfigureAwait(false) Omission Causes Deadlocks in Library Code

**What goes wrong:**
In netstandard2.0 library code, forgetting `ConfigureAwait(false)` on awaited tasks causes deadlocks when consumers call library methods synchronously (e.g., `harCapture.StopAsync().Result`). The continuation tries to resume on the original SynchronizationContext (e.g., UI thread), which is blocked waiting for the task to complete.

**Why it happens:**
By default, `await` captures the current SynchronizationContext and resumes there. If a library method awaits without `ConfigureAwait(false)`, and the calling code blocks the context thread (`.Result`, `.Wait()`), you get:
1. Caller blocks context thread waiting for task
2. Task completes, tries to resume on context thread
3. Context thread is blocked → deadlock

**Consequences:**
- Application hangs indefinitely (no exception, no timeout)
- Happens intermittently based on how consumers use the library
- Extremely hard to debug: no error message, just freeze
- Consumer blame: "Your library deadlocks when I call it"

**Prevention:**
- ALL library async methods MUST use `.ConfigureAwait(false)` on EVERY await:
  ```csharp
  public async Task<Har> StopAsync()
  {
      await DisableCdpEventsAsync().ConfigureAwait(false);
      var har = await BuildHarAsync().ConfigureAwait(false);
      return har;
  }
  ```
- Enable analyzer CA2007 (Do not directly await a Task) in library projects.
- Document: "Library provides async APIs. Consumers should await them, not block (.Result/.Wait())."
- Alternative: Make APIs synchronous if performance allows (simpler, no deadlock risk).

**Detection:**
- Application freezes when calling library methods
- Debugger shows thread blocked on `.Result` and continuation waiting for SynchronizationContext
- Happens in WPF/WinForms apps but not console apps (console has no SynchronizationContext)
- Deadlock detection tools flag await continuations

**Phase mapping:** Phase 1 (Core API) MUST use ConfigureAwait(false) everywhere. Non-negotiable for library code. Enable CA2007 analyzer.

**Source confidence:** HIGH - [Microsoft CA2007 rule](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/ca2007), [Stephen Cleary blog](https://blog.stephencleary.com/2012/07/dont-block-on-async-code.html), [ConfigureAwait FAQ](https://devblogs.microsoft.com/dotnet/configureawait-faq/)

---

### Pitfall 5: Redirect Request Correlation Requires Same RequestId Tracking

**What goes wrong:**
HTTP redirects (301, 302, 307, 308) reuse the SAME `requestId` for the entire redirect chain in CDP. If you treat each `requestWillBeSent` event as a new request, you'll create duplicate HAR entries or lose redirect chain information.

**Why it happens:**
CDP design decision: redirects are part of the same logical fetch operation. When a request redirects:
1. `responseReceived` fires with redirect status (301/302/etc) and `Location` header
2. Browser automatically follows redirect
3. `requestWillBeSent` fires AGAIN with SAME `requestId` for the new URL
4. Eventually, `responseReceived` fires with final (200) response

**Consequences:**
- HAR shows multiple entries with same requestId (invalid HAR)
- Lost redirect chain: can't reconstruct 301 → 302 → 200 sequence
- Timing corruption: redirect overhead not accounted for
- HAR viewers fail to render redirect relationships
- Request count mismatch: CDP shows N requests, HAR has N*redirectCount entries

**Prevention:**
- Track redirect chains: use list/stack structure per requestId:
  ```csharp
  class RequestData {
      public string RequestId { get; set; }
      public List<RedirectEntry> Redirects { get; set; } = new();
      public HarEntry FinalEntry { get; set; }
  }
  ```
- On `responseReceived`: if status is 3xx, store as redirect entry.
- On next `requestWillBeSent` with same requestId: append to redirect chain.
- On final 2xx/4xx/5xx: populate `FinalEntry` and close chain.
- HAR format: populate `entry.response.redirectURL` with Location header value.
- Alternatively: collapse entire chain into single HAR entry (less detail, simpler).

**Detection:**
- HAR validation: multiple entries with identical requestId
- Redirect chains missing from HAR (no 301/302 entries)
- Timing doesn't account for redirect overhead
- CDP logs show requestId reused, HAR shows separate entries

**Phase mapping:** Phase 1 (Core CDP Event Handling) MUST handle redirect detection. Phase 2 (HAR Serialization) adds proper redirect chain formatting.

**Source confidence:** HIGH - [PyCDP Network documentation](https://py-cdp.readthedocs.io/en/latest/api/network.html), [Chrome DevTools Protocol Network domain](https://chromedevtools.github.io/devtools-protocol/tot/Network/)

---

### Pitfall 6: Missing "encoding": "base64" Attribute for Binary Response Bodies

**What goes wrong:**
When capturing binary responses (images, PDFs, zips) into HAR format, the response body must be base64-encoded. However, many implementations forget to set the `"encoding": "base64"` attribute in the HAR JSON. This makes the HAR unreadable by parsers that expect UTF-8 text by default.

**Why it happens:**
HAR spec requires response content to be UTF-8. Binary data can't be represented in UTF-8, so it MUST be base64-encoded AND marked with `"encoding": "base64"`. Developers often remember to encode but forget the attribute. Firefox and Edge have shipped HAR exporters with this bug.

**Consequences:**
- HAR parsers fail to decode content (try to parse base64 as UTF-8)
- Binary content corruption: base64 string interpreted as text
- HAR viewers can't display images/files
- Interoperability failure: HAR generated by your library can't be read by standard tools

**Prevention:**
- When encoding response body as base64, ALWAYS set `"encoding": "base64"`:
  ```csharp
  if (IsBinaryContent(contentType)) {
      content.Text = Convert.ToBase64String(bodyBytes);
      content.Encoding = "base64";  // CRITICAL
  }
  ```
- Detect binary content: check Content-Type header for non-text types.
- Binary types: `image/*`, `application/pdf`, `application/zip`, `application/octet-stream`, etc.
- Default to base64 if Content-Type is missing or unknown.
- Validate HAR output: ensure `encoding` field exists when `text` contains base64.

**Detection:**
- HAR parsers/viewers reject or misrender binary content
- Manual inspection: base64 strings without `"encoding": "base64"`
- Validation tools flag missing encoding attribute
- Images don't display in HAR viewers (trying to decode as UTF-8)

**Phase mapping:** Phase 2 (HAR Serialization) MUST implement encoding attribute. Add validation test: generate HAR with binary response, verify encoding attribute present.

**Source confidence:** MEDIUM - [Mozilla bug 1221754](https://bugzilla.mozilla.org/show_bug.cgi?id=1221754), [HAR specification](https://w3c.github.io/web-performance/specs/HAR/Overview.html), [requestly discussion #2259](https://github.com/orgs/requestly/discussions/2259)

---

## Moderate Pitfalls

Mistakes that cause bugs or poor performance but can be fixed without major refactoring.

---

### Pitfall 7: CDP Version Dependency and Browser Compatibility

**What goes wrong:**
CDP is not designed for stable APIs. Methods, events, and data structures change between Chrome versions. Code that works on Chrome 120 may break on Chrome 121. Additionally, CDP only works on Chromium-based browsers (Chrome, Edge), not Firefox or Safari.

**Why it happens:**
CDP is Chrome DevTools' internal protocol for debugging, not a public API for testing. Google makes no backward compatibility guarantees. Firefox and Safari use different protocols (Remote Debugging Protocol, WebInspector).

**Consequences:**
- Library breaks when users upgrade Chrome
- Different Chrome versions return different event data
- Firefox/Safari users can't use CDP-based capture
- Maintenance burden: track Chrome releases, update CDP mappings

**Prevention:**
- Document supported Chrome versions explicitly (e.g., "Chrome 115+").
- Implement INetwork API fallback (Selenium's cross-browser abstraction).
- Use defensive deserialization: ignore unknown fields, handle missing fields gracefully.
- Subscribe to Chrome release notes, test against Chrome Beta/Dev channels.
- Long-term: migrate to WebDriver BiDi when stable (Selenium's future direction).

**Detection:**
- CDP command failures after Chrome update
- Different data structure in event payloads across versions
- Firefox/Safari users report "CDP not supported" errors
- Selenium throws NotImplementedException on non-Chrome browsers

**Phase mapping:** Phase 1 (Core) implements CDP. Phase 2 (Fallback) adds INetwork API. Phase 3+ monitors Chrome releases.

**Source confidence:** HIGH - [Selenium CDP documentation](https://www.selenium.dev/documentation/webdriver/bidi/cdp/), [Selenium BiDi direction](https://saucelabs.com/resources/blog/bidirectional-apis)

---

### Pitfall 8: HAR Timing Calculation Errors

**What goes wrong:**
HAR spec requires `entry.time` to equal the sum of `timings.blocked + timings.dns + timings.connect + timings.send + timings.wait + timings.receive + timings.ssl` (excluding -1 values). Many implementations miscalculate this, causing validation failures.

**Why it happens:**
- CDP's `Network.responseReceived.timing` uses milliseconds relative to `requestTime` (seconds), requiring conversion.
- Negative values (-1) mean "not applicable" (e.g., no DNS lookup for cached connection), must be excluded from sum.
- Developers sum ALL values including -1, or forget to convert units.

**Consequences:**
- HAR validation tools reject output
- Incorrect performance analysis (timings don't add up)
- HAR viewers display wrong waterfall charts
- Lost trust: "Your library generates invalid HARs"

**Prevention:**
- Extract timing from CDP's `responseReceived.timing` object.
- Convert: `requestTime` is seconds (baseline), individual times are milliseconds relative to it.
- Exclude -1 values from sum:
  ```csharp
  var timings = new[] { blocked, dns, connect, send, wait, receive, ssl }
      .Where(t => t >= 0);
  entry.Time = timings.Sum();
  ```
- Validate: assert `entry.Time == sum(timings)` before serialization.
- Add unit test with synthetic timing data to verify calculation.

**Detection:**
- HAR validators report "time does not match sum of timings"
- Manual inspection: `entry.time` != sum of timing phases
- Negative values included in sum (incorrect)
- Unit conversion errors: milliseconds vs seconds mismatch

**Phase mapping:** Phase 2 (HAR Serialization) MUST implement correct timing calculation. Add validation test.

**Source confidence:** HIGH - [HAR 1.2 spec](http://www.softwareishard.com/blog/har-12-spec/), [Chrome DevTools timing explanation](https://groups.google.com/g/google-chrome-developer-tools/c/FCCV2J7BaIY)

---

### Pitfall 9: HTTP 304 Not Modified Has No Response Body

**What goes wrong:**
When a request results in HTTP 304 Not Modified (cached resource), the response has NO body (empty). Calling `Network.getResponseBody` will fail or return empty string. Attempting to populate `response.content.text` with non-existent data creates invalid HAR entries.

**Why it happens:**
HTTP 304 specification: response MUST NOT contain a message body. It only includes headers (Cache-Control, ETag, etc.) to indicate the cached version is still valid. CDP respects this: `responseReceived` fires with status 304, but `dataReceived` and `loadingFinished` may not fire or indicate 0 bytes.

**Consequences:**
- `getResponseBody` fails with "No resource with given identifier found"
- HAR entry shows `content.size: 0` but attempts to populate `text` anyway
- Invalid HAR: body present for 304 (violates HTTP spec)
- Errors logged for expected behavior (304 is not an error)

**Prevention:**
- Check status code before calling `getResponseBody`:
  ```csharp
  if (response.Status == 304) {
      entry.Response.Content.Size = 0;
      entry.Response.Content.Text = string.Empty;
      // Don't call getResponseBody
  }
  ```
- Similarly for 204 No Content, 205 Reset Content, and 1xx informational responses.
- Set `content.size` from `response.headers["Content-Length"]` or `encodedDataLength` from `loadingFinished`.
- Document: "304/204 responses have no body, this is expected behavior."

**Detection:**
- Errors: "getResponseBody failed for requestId X (status 304)"
- HAR contains body text for 304 responses (invalid)
- Unnecessary error logging for expected status codes
- Response body size mismatch: headers say 0, HAR shows data

**Phase mapping:** Phase 1 (Core) MUST check status code before body retrieval. Phase 2 (HAR Serialization) validates status-specific requirements.

**Source confidence:** HIGH - [MDN HTTP 304](https://developer.mozilla.org/en-US/docs/Web/HTTP/Reference/Status/304), [HTTP 304 specification](https://http.dev/304)

---

### Pitfall 10: CDP Event Subscription Memory Leak

**What goes wrong:**
Subscribing to CDP Network domain events (`Network.requestWillBeSent`, `Network.responseReceived`, etc.) without proper cleanup causes memory leaks. Event handlers accumulate, and the CDP session holds references preventing garbage collection.

**Why it happens:**
CDP event subscriptions register callbacks that remain active until explicitly removed or the session is disposed. In long-running test suites, repeatedly creating and disposing WebDriver instances without cleaning up CDP subscriptions leaks memory.

**Consequences:**
- Memory usage grows linearly with test count
- Out-of-memory crashes in long test suites
- Selenium Hub/Grid nodes become unstable
- Slower test execution over time

**Prevention:**
- Implement IDisposable pattern for HAR capture class:
  ```csharp
  public class HarCapture : IDisposable {
      private IDevToolsSession _session;

      public async Task StartAsync() {
          _session = driver.GetDevToolsSession();
          await _session.Network.Enable(/* ... */);
          _session.Network.RequestWillBeSent += OnRequestWillBeSent;
          // ... other subscriptions
      }

      public void Dispose() {
          if (_session != null) {
              _session.Network.RequestWillBeSent -= OnRequestWillBeSent;
              // ... remove all subscriptions
              _session.Network.Disable().Wait();
              _session.Dispose();
          }
      }
  }
  ```
- Call `Network.Disable()` to stop events before disposing session.
- Unsubscribe ALL event handlers (use -= for each += subscription).
- Document: "Call Dispose() or use `using` statement when done capturing."

**Detection:**
- Memory profiler shows IDevToolsSession instances not collected
- Event handler count grows with each test
- Selenium Hub reports memory leak with CDP enabled
- Application memory usage increases throughout test suite

**Phase mapping:** Phase 1 (Core) MUST implement IDisposable with proper cleanup. Critical for production use.

**Source confidence:** MEDIUM - [Selenium CDP memory leak issue #2312](https://github.com/SeleniumHQ/docker-selenium/issues/2312), general .NET event subscription patterns

---

## Minor Pitfalls

Mistakes that cause annoyances or edge-case issues but have simple fixes.

---

### Pitfall 11: Missing Request Body for POST/PUT Requests

**What goes wrong:**
`Network.requestWillBeSent` event includes request body in `request.postData` field, but only up to the limit specified in `Network.enable(maxPostDataSize)`. If omitted or set too low, request bodies are truncated or missing entirely.

**Why it happens:**
CDP optimizes memory by limiting body capture. Default may be 0 (no bodies captured). Large request bodies (file uploads, JSON payloads) exceed limit and are truncated.

**Prevention:**
- Call `Network.enable` with explicit limits:
  ```csharp
  await session.Network.Enable(new Network.EnableCommandSettings {
      MaxPostDataSize = 10 * 1024 * 1024,  // 10MB
      MaxResourceBufferSize = 50 * 1024 * 1024  // 50MB
  });
  ```
- Document limits for users (configurable).
- Handle truncation: if `postData` is incomplete, set HAR `postData.comment` = "truncated".

**Detection:**
- HAR shows empty `request.postData` for POST/PUT requests
- Partial request bodies (cut off mid-JSON)
- Users report missing request payloads

**Phase mapping:** Phase 1 (Core) MUST set reasonable defaults.

**Source confidence:** MEDIUM - [Chrome DevTools Protocol Network.enable](https://chromedevtools.github.io/devtools-protocol/tot/Network/)

---

### Pitfall 12: Data URL and file:// Requests May Not Fire Events

**What goes wrong:**
CDP's `requestWillBeSent` may not fire for `data:` URLs or `file://` requests, or fires inconsistently across browsers. Firefox CDP specifically has known gaps for these request types.

**Why it happens:**
These aren't traditional HTTP requests. `data:` URLs are inline content, `file://` is local filesystem. CDP implementation varies by browser.

**Prevention:**
- Document limitation: "data: and file: URLs may not be captured."
- Filter by resource type: focus on `type: "Fetch" | "XHR" | "Document"`.
- Don't rely on capturing ALL requests; HAR is for network traffic.

**Detection:**
- Users report missing data URLs in HAR
- file:// links not captured
- Inconsistent behavior across browsers

**Phase mapping:** Phase 3 (Documentation) clarifies scope.

**Source confidence:** MEDIUM - [Mozilla bug 1535104](https://bugzilla.mozilla.org/show_bug.cgi?id=1535104)

---

### Pitfall 13: Timestamp Inconsistencies Between Events

**What goes wrong:**
CDP events may have timestamps where `responseReceived.timestamp` > `loadingFinished.timestamp`, violating logical ordering. This is rare but documented.

**Why it happens:**
Events are timestamped by different browser subsystems with slight clock skew or processing delays.

**Prevention:**
- Use `MonotonicTime` (if available) instead of wall-clock timestamps for ordering.
- Normalize timestamps: if sequence is known (responseReceived → loadingFinished), enforce min(received, finished).
- Don't use timestamps alone for ordering; track event sequence explicitly.

**Detection:**
- HAR timings show negative durations
- Events out of logical order by timestamp
- Timing waterfall charts look wrong

**Phase mapping:** Phase 2 (HAR Serialization) adds timestamp normalization.

**Source confidence:** MEDIUM - [Chrome Debugging Protocol discussion](https://groups.google.com/g/chrome-debugging-protocol/c/FofPysNnHx4)

---

### Pitfall 14: System.Text.Json netstandard2.0 Assembly Version Conflicts

**What goes wrong:**
When targeting netstandard2.0 and using System.Text.Json NuGet package, consumers may encounter `FileLoadException: "The located assembly's manifest definition does not match the assembly reference"`. This happens when consumer's runtime has a different System.Text.Json version than library expects.

**Why it happens:**
System.Text.Json is inbox for .NET Core 3.1+ but NuGet package for netstandard2.0. Different package versions across dependencies cause assembly binding conflicts.

**Prevention:**
- Consider Newtonsoft.Json for netstandard2.0 (more stable, widely adopted).
- If using System.Text.Json, document minimum version (e.g., 8.0.0+).
- Provide binding redirects or runtime configuration guidance for consumers.
- Test library against multiple consumer scenarios (.NET Framework, .NET Core, .NET 5+).

**Detection:**
- Consumer projects throw FileLoadException on library usage
- Assembly version mismatch errors in logs
- Works in some consumer projects, fails in others (version-dependent)

**Phase mapping:** Phase 1 (Serialization) chooses JSON library. Test cross-version compatibility early.

**Source confidence:** MEDIUM - [dotnet/runtime issue #78754](https://github.com/dotnet/runtime/issues/78754), [Unity discussions](https://discussions.unity.com/t/systen-text-json-net-standard-2-0/868255)

---

## Phase-Specific Warnings

| Phase Topic | Likely Pitfall | Mitigation |
|-------------|---------------|------------|
| Phase 1: Core CDP Event Handling | Event ordering race conditions (Pitfall 1), ConcurrentDictionary thread-safety (Pitfall 3), ConfigureAwait deadlocks (Pitfall 4) | Implement event buffering, use Lazy<T> pattern, add CA2007 analyzer |
| Phase 1: Response Body Capture | getResponseBody timing race (Pitfall 2), 304 No Content handling (Pitfall 9) | Immediate body retrieval, status code checks |
| Phase 1: Resource Cleanup | CDP event subscription leaks (Pitfall 10) | IDisposable pattern with proper unsubscribe |
| Phase 2: HAR Serialization | Base64 encoding attribute (Pitfall 6), timing calculation errors (Pitfall 8) | Validation tests for binary content and timing sums |
| Phase 2: Redirect Handling | Same requestId for redirect chains (Pitfall 5) | Redirect chain tracking data structure |
| Phase 2: Fallback Implementation | CDP version dependency (Pitfall 7) | INetwork API fallback for cross-browser support |
| Phase 3: Edge Cases | POST body limits (Pitfall 11), data:/file: URLs (Pitfall 12), timestamp inconsistencies (Pitfall 13) | Configuration options, documentation, normalization |
| All Phases: Serialization | System.Text.Json version conflicts (Pitfall 14) | Choose stable JSON library for netstandard2.0 |

---

## Sources

### High Confidence (Official Documentation, Specifications)

- [Chrome DevTools Protocol - Network domain](https://chromedevtools.github.io/devtools-protocol/tot/Network/) - Official CDP specification
- [HAR 1.2 Specification](http://www.softwareishard.com/blog/har-12-spec/) - HTTP Archive format specification
- [W3C HAR Format](https://w3c.github.io/web-performance/specs/HAR/Overview.html) - Official W3C specification
- [Microsoft Learn - CA2007: Do not directly await a Task](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/ca2007) - ConfigureAwait guidance
- [Stephen Cleary - Don't Block on Async Code](https://blog.stephencleary.com/2012/07/dont-block-on-async-code.html) - Async/await deadlock patterns
- [Microsoft ConfigureAwait FAQ](https://devblogs.microsoft.com/dotnet/configureawait-faq/) - Official ConfigureAwait guidance
- [Andrew Lock - Making GetOrAdd Thread Safe Using Lazy](https://andrewlock.net/making-getoradd-on-concurrentdictionary-thread-safe-using-lazy/) - ConcurrentDictionary patterns
- [Selenium - Chrome DevTools Protocol](https://www.selenium.dev/documentation/webdriver/bidi/cdp/) - Official Selenium CDP documentation
- [MDN - HTTP 304 Not Modified](https://developer.mozilla.org/en-us/docs/Web/HTTP/Reference/Status/304) - HTTP status specification

### Medium Confidence (Issue Trackers, Community Documentation)

- [chromedp issue #1317](https://github.com/chromedp/chromedp/issues/1317) - getResponseBody "No resource with given identifier" error
- [puppeteer issue #2258](https://github.com/puppeteer/puppeteer/issues/2258) - Network.getResponseBody failures
- [dotnet/runtime issue #33221](https://github.com/dotnet/runtime/issues/33221) - ConcurrentDictionary.GetOrAdd thread-safety
- [Bar Arnon - ConcurrentDictionary Is Not Always Thread-Safe](http://blog.i3arnon.com/2018/01/16/concurrent-dictionary-tolist/) - Thread-safety edge cases
- [PyCDP - Network domain documentation](https://py-cdp.readthedocs.io/en/latest/api/network.html) - CDP Network events reference
- [Mozilla bug 1221754](https://bugzilla.mozilla.org/show_bug.cgi?id=1221754) - HAR base64 encoding attribute missing
- [requestly discussion #2259](https://github.com/orgs/requestly/discussions/2259) - Base64 encoding in HAR
- [Selenium CDP memory leak issue #2312](https://github.com/SeleniumHQ/docker-selenium/issues/2312) - Memory leaks with CDP enabled
- [SeleniumBase discussion #2731](https://github.com/seleniumbase/SeleniumBase/discussions/2731) - Unable to fetch XHR response body
- [ChromeDevTools issue #73](https://github.com/ChromeDevTools/devtools-protocol/issues/73) - requestWillBeSent timing issues
- [Chrome Debugging Protocol discussion](https://groups.google.com/g/chrome-debugging-protocol/c/FofPysNnHx4) - Timestamp inconsistencies
- [Mozilla bug 1535104](https://bugzilla.mozilla.org/show_bug.cgi?id=1535104) - data: and file: URL tracking
- [dotnet/runtime issue #78754](https://github.com/dotnet/runtime/issues/78754) - System.Text.Json version conflicts
- [Sauce Labs - BiDirectional APIs Guide](https://saucelabs.com/resources/blog/bidirectional-apis) - Selenium BiDi future direction
- [Chrome DevTools timing discussion](https://groups.google.com/g/google-chrome-developer-tools/c/FCCV2J7BaIY) - Understanding Network.responseReceived timing
