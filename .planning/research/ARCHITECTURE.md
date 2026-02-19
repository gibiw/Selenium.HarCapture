# Architecture Patterns

**Domain:** Selenium Network Traffic Capture to HAR Format
**Researched:** 2026-02-19
**Confidence:** MEDIUM (verified patterns from multiple sources, CDP/Selenium APIs from official docs, HAR spec from W3C community)

## Recommended Architecture

### High-Level Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                      Selenium.Hars Library                       │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  ┌────────────────┐         ┌──────────────────────┐           │
│  │ HarCapture     │─────────│ ICaptureStrategy     │           │
│  │ (Facade)       │         │ (Strategy Pattern)   │           │
│  └────────────────┘         └──────────────────────┘           │
│         │                            △                          │
│         │                            │                          │
│         │         ┌──────────────────┴──────────────────┐      │
│         │         │                                      │      │
│         │  ┌──────┴────────┐              ┌─────────────┴────┐ │
│         │  │ CdpStrategy   │              │ INetworkStrategy │ │
│         │  │ (Primary)     │              │ (Fallback)       │ │
│         │  └───────────────┘              └──────────────────┘ │
│         │         │                                │            │
│         │         ├──> Event Listeners             │            │
│         │         │    (CDP Network Domain)        │            │
│         │         │                                │            │
│         └─────────┼────────────────────────────────┘            │
│                   │                                             │
│         ┌─────────▼────────────────────┐                        │
│         │  RequestResponseCorrelator   │                        │
│         │  (Thread-safe tracking)      │                        │
│         │  ConcurrentDictionary        │                        │
│         └──────────────────────────────┘                        │
│                   │                                             │
│         ┌─────────▼────────────────────┐                        │
│         │  HarBuilder                  │                        │
│         │  (Constructs HAR structure)  │                        │
│         └──────────────────────────────┘                        │
│                   │                                             │
│         ┌─────────▼────────────────────┐                        │
│         │  HarSerializer               │                        │
│         │  (JSON serialization)        │                        │
│         └──────────────────────────────┘                        │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
              │                           │
    ┌─────────▼─────────┐       ┌────────▼──────────┐
    │ Chrome DevTools   │       │ Selenium INetwork │
    │ Protocol (CDP)    │       │ API               │
    │ Network Domain    │       │                   │
    └───────────────────┘       └───────────────────┘
```

### Component Boundaries

| Component | Responsibility | Depends On | Exposes To |
|-----------|---------------|------------|------------|
| **HarCapture** | Public API facade, lifecycle management, strategy selection | ICaptureStrategy, HarSerializer | Client Code |
| **ICaptureStrategy** | Strategy interface defining capture contract | None | HarCapture, Strategy Implementations |
| **CdpStrategy** | CDP Network domain event subscription and handling | IDevTools (Selenium), RequestResponseCorrelator | HarCapture (via interface) |
| **INetworkStrategy** | Selenium INetwork API handling | INetwork (Selenium), RequestResponseCorrelator | HarCapture (via interface) |
| **RequestResponseCorrelator** | Thread-safe request/response pairing, lifecycle tracking | ConcurrentDictionary, Lazy | Both Strategy Implementations |
| **HarBuilder** | Constructs HAR object model from correlated data | HAR model classes (Log, Entry, Request, Response, Timing) | HarCapture |
| **HarSerializer** | JSON serialization to HAR 1.2 format | System.Text.Json or Newtonsoft.Json | HarCapture |
| **HAR Model** | POCO classes representing HAR structure | None | HarBuilder, HarSerializer |

## Data Flow

### 1. Initialization Flow

```
Client Code
    │
    └──> HarCapture.StartCapture(IWebDriver driver)
            │
            ├──> Detect driver capabilities
            │    (Check for CDP support)
            │
            ├──> Select Strategy (Strategy Pattern)
            │    │
            │    ├──> If CDP available: new CdpStrategy()
            │    └──> Else: new INetworkStrategy()
            │
            └──> Strategy.Initialize()
                    │
                    ├──> CdpStrategy: Subscribe to CDP events
                    │    • Network.requestWillBeSent
                    │    • Network.responseReceived
                    │    • Network.dataReceived
                    │    • Network.loadingFinished
                    │    • Network.loadingFailed
                    │
                    └──> INetworkStrategy: Register INetwork handlers
                         • NetworkRequestSent event
                         • NetworkResponseReceived event
```

### 2. Request Capture Flow (CDP)

```
Browser HTTP Request
    │
    ▼
CDP: Network.requestWillBeSent event
    │
    ├──> Event Data:
    │    • RequestID (correlation key)
    │    • Request (URL, method, headers, postData)
    │    • Timestamp (monotonic)
    │    • WallTime (real time)
    │    • Initiator (what triggered request)
    │
    ▼
CdpStrategy.OnRequestWillBeSent(event)
    │
    └──> RequestResponseCorrelator.TrackRequest(requestId, requestData)
            │
            └──> ConcurrentDictionary.GetOrAdd(requestId,
                    new Lazy<RequestResponseEntry>(() =>
                        new RequestResponseEntry {
                            RequestId = requestId,
                            Request = requestData,
                            StartTime = timestamp
                        }
                    )
                 ).Value
```

### 3. Response Capture Flow (CDP)

```
Browser HTTP Response Headers Received
    │
    ▼
CDP: Network.responseReceived event
    │
    ├──> Event Data:
    │    • RequestID (correlation key)
    │    • Response (status, statusText, headers, mimeType)
    │    • Timestamp
    │    • Type (resource type)
    │
    ▼
CdpStrategy.OnResponseReceived(event)
    │
    └──> RequestResponseCorrelator.UpdateResponse(requestId, responseData)
            │
            └──> ConcurrentDictionary[requestId].Value
                    .UpdateResponse(responseData)

────────────────────────────────────────────────────────────

CDP: Network.dataReceived events (0 or more)
    │
    ├──> Event Data:
    │    • RequestID
    │    • DataLength
    │    • EncodedDataLength
    │
    ▼
CdpStrategy.OnDataReceived(event)
    │
    └──> RequestResponseCorrelator.AccumulateDataLength(requestId, dataLength)

────────────────────────────────────────────────────────────

CDP: Network.loadingFinished OR Network.loadingFailed
    │
    ├──> loadingFinished Data:
    │    • RequestID
    │    • EncodedDataLength (total bytes received)
    │
    ├──> loadingFailed Data:
    │    • RequestID
    │    • ErrorText
    │    • Canceled (bool)
    │
    ▼
CdpStrategy.OnLoadingComplete(event)
    │
    └──> RequestResponseCorrelator.CompleteRequest(requestId, finalData)
            │
            └──> Mark entry as complete, ready for HAR export
```

### 4. HAR Generation Flow

```
Client Code
    │
    └──> HarCapture.StopCapture()
            │
            ├──> Strategy.Stop()
            │    (Unsubscribe from events)
            │
            ├──> RequestResponseCorrelator.GetCompletedEntries()
            │    │
            │    └──> Returns: List<RequestResponseEntry>
            │         (only completed request/response pairs)
            │
            ├──> HarBuilder.BuildHar(entries)
            │    │
            │    ├──> Create HarLog
            │    │    • version: "1.2"
            │    │    • creator: { name, version }
            │    │    • browser: { name, version }
            │    │
            │    ├──> Build HarEntry for each RequestResponseEntry
            │    │    │
            │    │    ├──> startedDateTime (ISO 8601)
            │    │    ├──> time (total duration ms)
            │    │    ├──> request: HarRequest
            │    │    │    • method
            │    │    │    • url
            │    │    │    • headers: HarHeader[]
            │    │    │    • queryString: HarQueryParam[]
            │    │    │    • postData: HarPostData (if present)
            │    │    ├──> response: HarResponse
            │    │    │    • status
            │    │    │    • statusText
            │    │    │    • headers: HarHeader[]
            │    │    │    • content: HarContent
            │    │    │         - size
            │    │    │         - mimeType
            │    │    │         - text (optional)
            │    │    └──> timings: HarTimings
            │    │         • blocked: -1 (not available)
            │    │         • dns: -1 (not available)
            │    │         • connect: -1 (not available)
            │    │         • send: calculated
            │    │         • wait: calculated
            │    │         • receive: calculated
            │    │         • ssl: -1 (not available)
            │    │
            │    └──> Returns: HarLog
            │
            └──> HarSerializer.Serialize(harLog)
                    │
                    ├──> Convert to JSON (HAR 1.2 format)
                    ├──> Apply naming conventions (camelCase)
                    ├──> Ensure ISO 8601 date formatting
                    │
                    └──> Returns: string (HAR JSON)
```

## Patterns to Follow

### Pattern 1: Strategy Pattern for Capture Backends

**What:** Encapsulate different network capture approaches (CDP vs INetwork) behind a common interface.

**When:** Need to support multiple browsers with different capabilities.

**Why:**
- CDP is more powerful but only available for Chromium-based browsers
- INetwork API is broader but less detailed
- Allows runtime selection based on driver capabilities
- Enables future capture mechanisms without changing client code

**Example:**
```csharp
public interface ICaptureStrategy
{
    void Initialize(IWebDriver driver);
    void Start();
    void Stop();
    IEnumerable<RequestResponseEntry> GetCapturedData();
}

public class CdpStrategy : ICaptureStrategy
{
    private IDevTools _devTools;
    private IDevToolsSession _session;
    private RequestResponseCorrelator _correlator;

    public void Initialize(IWebDriver driver)
    {
        _devTools = driver as IDevTools
            ?? throw new NotSupportedException("CDP not available");
        _session = _devTools.GetDevToolsSession();
        _correlator = new RequestResponseCorrelator();
    }

    public void Start()
    {
        _session.SendCommand("Network.enable");
        _session.DevToolsEventReceived += OnCdpEvent;
    }

    private void OnCdpEvent(object sender, DevToolsEventReceivedEventArgs e)
    {
        switch (e.EventName)
        {
            case "Network.requestWillBeSent":
                HandleRequestWillBeSent(e.EventData);
                break;
            case "Network.responseReceived":
                HandleResponseReceived(e.EventData);
                break;
            // ... other events
        }
    }

    // Implementation details...
}

public class INetworkStrategy : ICaptureStrategy
{
    private INetwork _network;
    private RequestResponseCorrelator _correlator;

    public void Initialize(IWebDriver driver)
    {
        _network = driver.Manage().Network;
        _correlator = new RequestResponseCorrelator();
    }

    public void Start()
    {
        _network.NetworkRequestSent += OnRequestSent;
        _network.NetworkResponseReceived += OnResponseReceived;
        _network.StartMonitoring();
    }

    // Implementation details...
}
```

### Pattern 2: Request/Response Correlation with Thread-Safe Dictionary

**What:** Use ConcurrentDictionary with Lazy initialization to correlate requests with responses across multiple events.

**When:** Events arrive asynchronously and potentially out of order from multiple threads.

**Why:**
- CDP events fire asynchronously (requestWillBeSent, responseReceived, dataReceived, loadingFinished)
- Multiple concurrent requests need independent tracking
- GetOrAdd with Lazy ensures thread-safe, run-once initialization
- Prevents race conditions during correlation

**Example:**
```csharp
public class RequestResponseCorrelator
{
    private readonly ConcurrentDictionary<string, Lazy<RequestResponseEntry>> _entries;

    public RequestResponseCorrelator()
    {
        _entries = new ConcurrentDictionary<string, Lazy<RequestResponseEntry>>();
    }

    public void TrackRequest(string requestId, RequestData request)
    {
        var entry = _entries.GetOrAdd(
            requestId,
            key => new Lazy<RequestResponseEntry>(() =>
                new RequestResponseEntry
                {
                    RequestId = key,
                    StartTime = DateTime.UtcNow
                }
            )
        ).Value;

        entry.UpdateRequest(request);
    }

    public void UpdateResponse(string requestId, ResponseData response)
    {
        if (_entries.TryGetValue(requestId, out var lazyEntry))
        {
            lazyEntry.Value.UpdateResponse(response);
        }
    }

    public void CompleteRequest(string requestId, CompletionData data)
    {
        if (_entries.TryGetValue(requestId, out var lazyEntry))
        {
            var entry = lazyEntry.Value;
            entry.MarkComplete(data);
        }
    }

    public IEnumerable<RequestResponseEntry> GetCompletedEntries()
    {
        return _entries.Values
            .Select(lazy => lazy.Value)
            .Where(entry => entry.IsComplete)
            .ToList();
    }
}
```

### Pattern 3: Builder Pattern for HAR Construction

**What:** Separate HAR object construction logic from the capture strategy.

**When:** Converting captured network data into HAR 1.2 specification format.

**Why:**
- HAR has complex nested structure (Log → Entries → Request/Response/Timings)
- Field mappings require conversions (CDP timestamps → HAR ISO 8601)
- Separates concerns (capture vs serialization)
- Enables HAR format changes without modifying capture logic

**Example:**
```csharp
public class HarBuilder
{
    public HarLog BuildHar(IEnumerable<RequestResponseEntry> entries, BrowserInfo browser)
    {
        var log = new HarLog
        {
            Version = "1.2",
            Creator = new HarCreator
            {
                Name = "Selenium.Hars",
                Version = GetAssemblyVersion()
            },
            Browser = new HarBrowser
            {
                Name = browser.Name,
                Version = browser.Version
            },
            Pages = new HarPage[0], // Optional: can track page navigations
            Entries = entries.Select(BuildEntry).ToArray()
        };

        return log;
    }

    private HarEntry BuildEntry(RequestResponseEntry entry)
    {
        return new HarEntry
        {
            StartedDateTime = entry.StartTime.ToString("o"), // ISO 8601
            Time = entry.TotalDuration.TotalMilliseconds,
            Request = BuildRequest(entry.Request),
            Response = BuildResponse(entry.Response),
            Timings = BuildTimings(entry),
            ServerIPAddress = entry.ServerIP,
            Connection = entry.ConnectionId
        };
    }

    private HarRequest BuildRequest(RequestData data)
    {
        var uri = new Uri(data.Url);
        return new HarRequest
        {
            Method = data.Method,
            Url = data.Url,
            HttpVersion = "HTTP/1.1", // CDP doesn't always provide this
            Headers = data.Headers.Select(h =>
                new HarHeader { Name = h.Key, Value = h.Value }
            ).ToArray(),
            QueryString = ParseQueryString(uri.Query),
            PostData = BuildPostData(data.PostData, data.Headers),
            HeadersSize = -1, // Not available from CDP
            BodySize = data.PostData?.Length ?? 0
        };
    }

    private HarTimings BuildTimings(RequestResponseEntry entry)
    {
        // CDP doesn't provide granular timing breakdown
        // Approximate based on available timestamps
        return new HarTimings
        {
            Blocked = -1,
            Dns = -1,
            Connect = -1,
            Ssl = -1,
            Send = 0, // Approximate as instantaneous
            Wait = entry.FirstByteTime.TotalMilliseconds,
            Receive = entry.ReceiveTime.TotalMilliseconds
        };
    }
}
```

### Pattern 4: Facade Pattern for Public API

**What:** Provide a simplified, high-level API hiding complexity of strategy selection and lifecycle management.

**When:** Exposing the library to client code.

**Why:**
- Simplifies client usage (one entry point)
- Hides strategy selection logic
- Manages capture lifecycle
- Provides consistent API regardless of backend

**Example:**
```csharp
public class HarCapture : IDisposable
{
    private ICaptureStrategy _strategy;
    private bool _isCapturing;

    public void StartCapture(IWebDriver driver)
    {
        if (_isCapturing)
            throw new InvalidOperationException("Capture already started");

        _strategy = SelectStrategy(driver);
        _strategy.Initialize(driver);
        _strategy.Start();
        _isCapturing = true;
    }

    public string StopCapture()
    {
        if (!_isCapturing)
            throw new InvalidOperationException("No active capture");

        _strategy.Stop();
        var entries = _strategy.GetCapturedData();

        var builder = new HarBuilder();
        var harLog = builder.BuildHar(entries, GetBrowserInfo());

        var serializer = new HarSerializer();
        var harJson = serializer.Serialize(harLog);

        _isCapturing = false;
        return harJson;
    }

    private ICaptureStrategy SelectStrategy(IWebDriver driver)
    {
        // Try CDP first (more detailed data)
        if (driver is IDevTools)
        {
            return new CdpStrategy();
        }

        // Fallback to INetwork
        if (driver.Manage() is INetwork)
        {
            return new INetworkStrategy();
        }

        throw new NotSupportedException(
            "Driver does not support network capture (requires CDP or INetwork)");
    }

    public void Dispose()
    {
        if (_isCapturing)
        {
            StopCapture();
        }
    }
}
```

## Anti-Patterns to Avoid

### Anti-Pattern 1: Mixing Strategy-Specific Code in Facade

**What:** Putting CDP or INetwork specific logic directly in the HarCapture class.

**Why bad:**
- Violates Single Responsibility Principle
- Makes adding new capture mechanisms difficult
- Creates tight coupling between API and implementation
- Hard to test in isolation

**Instead:**
- Keep HarCapture strategy-agnostic
- All driver-specific code belongs in strategy implementations
- Use interface contracts only

### Anti-Pattern 2: Using Regular Dictionary for Request Correlation

**What:** Using `Dictionary<string, RequestResponseEntry>` with manual locking.

**Why bad:**
- Race conditions when multiple events arrive simultaneously
- Dead-locks possible with incorrect lock ordering
- Performance bottleneck (coarse-grained locking)
- Complex error-prone code

**Instead:**
- Use `ConcurrentDictionary<string, Lazy<RequestResponseEntry>>`
- Lazy ensures initialization runs once
- Lock-free for read operations
- Fine-grained locking only for modifications

### Anti-Pattern 3: Blocking on CDP Event Handlers

**What:** Performing expensive operations (I/O, heavy computation) directly in CDP event callbacks.

**Why bad:**
- CDP events fire on internal Chrome thread
- Blocking prevents other events from being processed
- Can cause browser hangs or timeouts
- Events may be dropped

**Instead:**
- Keep event handlers fast (< 10ms)
- Store event data in concurrent collection
- Process asynchronously or on StopCapture()

### Anti-Pattern 4: Assuming Event Order

**What:** Relying on events arriving in specific order (request → response → dataReceived → loadingFinished).

**Why bad:**
- CDP makes no ordering guarantees
- Network conditions can cause reordering
- Cached responses may skip events
- Failed requests may never complete

**Instead:**
- Use correlation ID to match events
- Handle events arriving out of order
- Support partial data (request without response)
- Track completion state explicitly

### Anti-Pattern 5: Capturing Response Bodies Without Opt-In

**What:** Automatically capturing response content (Network.getResponseBody) for all requests.

**Why bad:**
- Extremely memory intensive for large responses (images, videos, files)
- Significant performance impact on browser
- Not needed for most HAR use cases (timing analysis)
- May violate privacy/security policies

**Instead:**
- Make body capture opt-in via configuration
- Allow filtering by content type
- Use size limits
- Document memory implications

## CDP Event Flow Detail

### Request Lifecycle Events

```
1. Network.requestWillBeSent
   ├─ Fires: Before HTTP request sent
   ├─ Data: URL, method, headers, postData, initiator
   ├─ Timing: request start timestamp
   └─ Use: Create request entry

2. Network.requestWillBeSentExtraInfo (optional)
   ├─ Fires: When additional request info available
   ├─ Data: Actual request headers sent (may differ from #1)
   └─ Use: Update request headers (more accurate)

3. Network.responseReceived
   ├─ Fires: Response headers received
   ├─ Data: Status, statusText, headers, mimeType, remote address
   ├─ Timing: response start timestamp
   └─ Use: Create response entry, correlate with request

4. Network.responseReceivedExtraInfo (optional)
   ├─ Fires: When additional response info available
   ├─ Data: Actual response headers (including blocked headers)
   └─ Use: Update response headers (more accurate)

5. Network.dataReceived (0 or more)
   ├─ Fires: Each time data chunk received
   ├─ Data: dataLength, encodedDataLength
   └─ Use: Track download progress, calculate receive timing

6. Network.loadingFinished OR Network.loadingFailed
   ├─ loadingFinished:
   │  ├─ Data: encodedDataLength (total bytes)
   │  └─ Use: Mark complete, finalize size/timing
   └─ loadingFailed:
      ├─ Data: errorText, canceled, blockedReason
      └─ Use: Mark failed, record error info
```

### CDP Network Domain Commands

| Command | Purpose | When to Use |
|---------|---------|-------------|
| `Network.enable` | Start capturing network events | On StartCapture() |
| `Network.disable` | Stop capturing network events | On StopCapture() |
| `Network.getResponseBody` | Retrieve response content | Optional, for specific requests |
| `Network.setExtraHTTPHeaders` | Inject custom headers | For authentication, testing |
| `Network.clearBrowserCache` | Clear cache | Before test runs |

### Correlation Gotchas

**Multiple Events Same RequestID:**
- requestWillBeSent can fire multiple times (redirects)
- Track redirect chain via `redirectResponse` field
- Use `loaderId` for additional correlation

**Missing Events:**
- Cached responses may skip requestWillBeSent
- Set `Network.setCacheDisabled(true)` for complete capture
- Failed DNS lookups may only fire loadingFailed

**Timing Calculations:**
- CDP timestamps are monotonic (not wall time)
- Convert to wall time using `wallTime` from requestWillBeSent
- Timing breakdown limited (no DNS/connect/SSL separate timing)

## Selenium INetwork API Comparison

| Feature | CDP (Chrome/Edge) | INetwork API | Implications |
|---------|-------------------|--------------|--------------|
| **Availability** | Chromium-based only | Broader browser support | Use strategy pattern for fallback |
| **Event Detail** | Very detailed (8+ events per request) | Simplified (2 events) | CDP provides richer HAR data |
| **Request Body** | Available in requestWillBeSent | Available in RequestSent | Both capture POST data |
| **Response Body** | Requires separate getResponseBody call | Available in ResponseReceived | INetwork more convenient but less control |
| **Timing Granularity** | Millisecond precision | Lower precision | CDP better for performance analysis |
| **Thread Safety** | Events on Chrome thread | Events on Selenium thread | Both require concurrent handling |
| **Redirect Handling** | Explicit redirect chain | May combine redirects | CDP tracks full redirect sequence |
| **Failure Reporting** | Detailed error codes | Basic error info | CDP better for debugging |

## Scalability Considerations

| Concern | At 100 Requests | At 10K Requests | At 100K+ Requests |
|---------|-----------------|-----------------|-------------------|
| **Memory** | Negligible (~10MB) | Moderate (~500MB) | High (5GB+) - implement streaming |
| **Request Correlation** | ConcurrentDictionary performs well | ConcurrentDictionary performs well | Consider time-based eviction of old entries |
| **Capture Overhead** | Minimal (<5% slowdown) | Noticeable (10-20% slowdown) | Significant (30%+ slowdown) - consider sampling |
| **Serialization** | Instant (<10ms) | Fast (<1s) | Slow (10s+) - stream to file incrementally |
| **Response Bodies** | Optional, manageable | DO NOT capture all bodies | Filter by content type, implement size limits |

### Mitigation Strategies for High Volume

**Memory Management:**
```csharp
// Time-based eviction
_correlator.EvictEntriesOlderThan(TimeSpan.FromMinutes(5));

// Size-based limits
if (_correlator.EntryCount > 10000)
{
    // Flush oldest entries to disk
    FlushAndClearOldest(5000);
}
```

**Streaming Serialization:**
```csharp
// Instead of building entire HAR in memory
public void StopCapture(Stream outputStream)
{
    using (var writer = new StreamWriter(outputStream))
    using (var jsonWriter = new JsonTextWriter(writer))
    {
        // Write HAR structure incrementally
        WriteHarHeader(jsonWriter);

        foreach (var entry in _correlator.GetCompletedEntries())
        {
            WriteHarEntry(jsonWriter, entry);
            _correlator.Remove(entry.RequestId); // Free memory
        }

        WriteHarFooter(jsonWriter);
    }
}
```

**Sampling:**
```csharp
// Capture percentage of requests
public class SamplingCaptureStrategy : ICaptureStrategy
{
    private readonly ICaptureStrategy _innerStrategy;
    private readonly double _samplingRate; // 0.1 = 10%

    private bool ShouldCapture(string requestId)
    {
        // Deterministic sampling based on request ID
        var hash = requestId.GetHashCode();
        return (hash % 100) < (_samplingRate * 100);
    }
}
```

## Build Order Implications

### Phase 1: Core Models and Strategy Interface
**What to build:**
- HAR model classes (Log, Entry, Request, Response, Timings, etc.)
- ICaptureStrategy interface
- RequestResponseEntry (internal correlation model)

**Why first:**
- No external dependencies
- Foundation for all other components
- Can be fully unit tested in isolation
- Defines contracts for entire system

**Estimated complexity:** Low
**Estimated time:** 1-2 days

### Phase 2: Request/Response Correlator
**What to build:**
- RequestResponseCorrelator class
- Thread-safe correlation logic
- Entry lifecycle management (pending → complete)

**Why second:**
- Depends only on Phase 1 models
- Critical component used by both strategies
- Complex thread-safety concerns best isolated
- Can be thoroughly tested before strategy implementation

**Estimated complexity:** Medium
**Estimated time:** 2-3 days

### Phase 3: CDP Strategy Implementation
**What to build:**
- CdpStrategy class
- CDP event subscriptions (Network domain)
- Event handler methods
- Integration with correlator

**Why third:**
- Primary capture mechanism (most important)
- Depends on correlator from Phase 2
- Most complex event handling logic
- Enables end-to-end testing

**Estimated complexity:** High
**Estimated time:** 4-5 days

### Phase 4: INetwork Strategy Implementation
**What to build:**
- INetworkStrategy class
- INetwork event handlers
- Integration with correlator

**Why fourth:**
- Fallback mechanism (lower priority)
- Similar structure to Phase 3 (reuse patterns)
- Simpler than CDP (fewer events)
- Provides browser compatibility

**Estimated complexity:** Medium
**Estimated time:** 2-3 days

### Phase 5: HAR Builder
**What to build:**
- HarBuilder class
- Conversion logic (RequestResponseEntry → HAR structure)
- Timestamp formatting (ISO 8601)
- Header parsing, query string extraction

**Why fifth:**
- Depends on Phase 1 models and Phase 2 correlation data
- Pure transformation logic (deterministic, testable)
- No threading concerns

**Estimated complexity:** Medium
**Estimated time:** 2-3 days

### Phase 6: HAR Serializer
**What to build:**
- HarSerializer class
- JSON serialization configuration
- HAR 1.2 format compliance
- Custom converters (DateTime, etc.)

**Why sixth:**
- Depends on Phase 5 HAR structure
- Straightforward JSON serialization
- Format validation

**Estimated complexity:** Low
**Estimated time:** 1 day

### Phase 7: Public API Facade
**What to build:**
- HarCapture class (facade)
- Strategy selection logic
- Lifecycle management (Start/Stop)
- IDisposable implementation

**Why last:**
- Depends on all previous phases
- Orchestrates entire system
- Simplest once components exist
- Public contract requires careful design

**Estimated complexity:** Low-Medium
**Estimated time:** 1-2 days

### Dependency Graph

```
Phase 1: Core Models ─────┬─────────────────────┐
                          │                     │
                          ▼                     │
Phase 2: Correlator ──────┼─────┐               │
                          │     │               │
                          ▼     ▼               │
Phase 3: CDP Strategy     │   Phase 4: INetwork │
                          │                     │
                          └─────┬───────────────┤
                                │               │
                                ▼               │
                          Phase 5: Builder ◄────┘
                                │
                                ▼
                          Phase 6: Serializer
                                │
                                ▼
                          Phase 7: Facade
```

## Critical Design Decisions

### 1. CDP as Primary Strategy
**Decision:** Use CDP Network domain as primary capture mechanism with INetwork as fallback.

**Rationale:**
- CDP provides most detailed network data (8+ events per request vs 2)
- Supports all HAR 1.2 fields
- Better timing precision
- Redirect chain tracking
- Error details

**Trade-off:** Chromium-only, requires strategy pattern for broader browser support.

### 2. Event-Based Capture (Not Proxy)
**Decision:** Subscribe to browser events rather than HTTP proxy interception.

**Rationale:**
- No proxy setup complexity (certificates, trust, ports)
- No HTTPS certificate warnings
- No browser configuration required
- Works in containerized environments
- No additional processes to manage

**Trade-off:** Dependent on browser API availability and stability.

### 3. ConcurrentDictionary + Lazy Pattern
**Decision:** Use ConcurrentDictionary<string, Lazy<RequestResponseEntry>> for correlation.

**Rationale:**
- Thread-safe without manual locking
- Lazy ensures initialization happens once
- Lock-free reads
- Proven pattern (used by ASP.NET Core team)

**Trade-off:** Slightly more complex than simple Dictionary, but necessary for thread safety.

### 4. netstandard2.0 Target
**Decision:** Target netstandard2.0 for maximum compatibility.

**Rationale:**
- Supports .NET Framework 4.6.1+
- Supports .NET Core 2.0+
- Supports .NET 5+ (all versions)
- Broadest ecosystem compatibility

**Trade-off:** Cannot use newer C# language features (e.g., nullable reference types, records).

### 5. Opt-In Response Body Capture
**Decision:** Do not capture response bodies by default.

**Rationale:**
- Massive memory overhead for large responses
- Performance impact (requires separate CDP call per request)
- Most HAR use cases analyze timing, not content
- Privacy/security concerns

**Trade-off:** Users wanting response bodies must explicitly enable.

## Sources

**CDP Network Domain:**
- [Chrome DevTools Protocol - Network domain](https://chromedevtools.github.io/devtools-protocol/tot/Network/)
- [PyCDP Network API Documentation](https://py-cdp.readthedocs.io/en/latest/api/network.html)
- [The power of Chrome Devtools Protocol — Part II](https://medium.com/globant/the-power-of-chrome-devtools-protocol-part-ii-3fb8239785db)
- [Decoding Web Interactions: Selenium CDP Listeners](https://blogs.perficient.com/2024/01/24/decoding-web-interactions-unleashing-selenium-cdp-listeners-to-extract-network-responses/)

**Selenium INetwork API:**
- [Interface INetwork - Selenium .NET API](https://www.selenium.dev/selenium/docs/api/dotnet/webdriver/OpenQA.Selenium.INetwork.html)
- [Selenium INetwork Source Code](https://github.com/SeleniumHQ/selenium/blob/trunk/dotnet/src/webdriver/INetwork.cs)
- [Selenium C# network traffic logging example](https://gist.github.com/jimevans/d53e0d4150fa784674594c65be152ddf)

**HAR Specification:**
- [HAR 1.2 Spec](http://www.softwareishard.com/blog/har-12-spec/) (MEDIUM confidence - cert expired but authoritative)
- [W3C HTTP Archive (HAR) format](https://w3c.github.io/web-performance/specs/HAR/Overview.html)
- [What is a HAR file and How it is structured](https://www.zipy.ai/blog/what-is-har-file)
- [HAR Entry Timings](https://metacpan.org/pod/Archive::Har::Entry::Timings)

**Architecture Patterns:**
- [Strategy in C# / Design Patterns](https://refactoring.guru/design-patterns/strategy/csharp/example)
- [6 Ways To Implement The Strategy Pattern In C#](https://blog.jamesmichaelhickey.com/strategy-pattern-implementations/)
- [Making ConcurrentDictionary GetOrAdd thread safe using Lazy](https://andrewlock.net/making-getoradd-on-concurrentdictionary-thread-safe-using-lazy/)
- [Correlation IDs - Engineering Fundamentals Playbook](https://microsoft.github.io/code-with-engineering-playbook/observability/correlation-id/)

**Existing HAR Libraries:**
- [selenium-capture (Java)](https://github.com/mike10004/selenium-capture)
- [HarSharp (.NET)](https://github.com/giacomelli/HarSharp)
