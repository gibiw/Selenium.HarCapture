# Selenium.HarCapture

## What This Is

A C#/.NET library for capturing network traffic from Selenium WebDriver into HAR (HTTP Archive) format. Provides a BrowserMob Proxy-like experience natively in C#, using CDP as primary capture mechanism and Selenium INetwork API as fallback. Targets netstandard2.0 for broad compatibility.

## Core Value

Developers can capture complete HTTP traffic from Selenium browser sessions into standard HAR format with a single line of code — no external proxies, no complex setup.

## Requirements

### Validated

<!-- Shipped and confirmed valuable. -->

(None yet — ship to validate)

### Active

<!-- Current scope. Building toward these. -->

- [ ] HAR model classes fully compliant with HAR 1.2 spec (17 classes)
- [ ] CDP-based capture strategy for Chromium browsers (detailed timings, response bodies)
- [ ] Selenium INetwork API fallback for all browsers
- [ ] Automatic strategy selection with runtime fallback
- [ ] JSON serialization/deserialization via System.Text.Json
- [ ] Save/load HAR files
- [ ] Multi-page capture support (NewPage)
- [ ] URL filtering (include/exclude patterns)
- [ ] Response body size limits
- [ ] CaptureType flags for granular control (headers, cookies, content, timings)
- [ ] WebDriver extension methods for one-liner usage
- [ ] Thread-safe capture with deep-clone snapshots

### Out of Scope

- WebSocket traffic capture — HAR spec doesn't standardize WebSocket entries
- Proxy-based capture (BrowserMob Proxy integration) — goal is proxy-free native approach
- Request modification/interception — separate concern, not HAR capture
- HAR merging/diffing utilities — can be added later if needed
- GUI/viewer — use existing tools (Chrome DevTools, HAR Viewer)

## Context

- In the Java ecosystem, BrowserMob Proxy is the de-facto standard for HAR capture with Selenium
- In C#/.NET, there is no equivalent — only HarSharp (parser only), and low-level CDP/INetwork APIs
- Selenium 4+ exposes IDevTools for CDP and INetwork for cross-browser network interception
- CDP provides ResourceTiming data that maps to HAR timings; INetwork does not
- HAR 1.2 specification: http://www.softwareishard.com/blog/har-12-spec/

## Constraints

- **Target framework**: netstandard2.0 — maximum compatibility (requires Microsoft.Bcl.AsyncInterfaces for IAsyncDisposable)
- **Dependencies**: Selenium.WebDriver >= 4.0.0, System.Text.Json >= 6.0.0 — kept minimal
- **Single package**: One NuGet package `Selenium.HarCapture` — no splitting into multiple packages
- **No external processes**: No proxies or external tools required — everything in-process

## Key Decisions

<!-- Decisions that constrain future work. Add throughout project lifecycle. -->

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| CDP as primary, INetwork as fallback | CDP provides detailed timings and response bodies; INetwork works cross-browser but lacks timings | -- Pending |
| System.Text.Json over Newtonsoft.Json | Modern, no extra dependency for .NET 6+, source-gen ready | -- Pending |
| netstandard2.0 target | Maximum .NET Framework / .NET Core / .NET 5+ compatibility | -- Pending |
| Strategy pattern for capture backends | Clean separation, testable, extensible for future strategies | -- Pending |
| Sealed model classes | HAR model is data-only, no inheritance needed, better performance | -- Pending |

---
| Rename to Selenium.HarCapture | More descriptive, clearer intent than "Hars", follows NuGet naming conventions | -- Pending |

---
*Last updated: 2026-02-19 after rename to Selenium.HarCapture*
