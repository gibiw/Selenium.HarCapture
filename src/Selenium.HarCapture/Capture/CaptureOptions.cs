using System.Collections.Generic;

namespace Selenium.HarCapture.Capture;

/// <summary>
/// Configuration options for HAR capture sessions.
/// Controls what data is captured, how URLs are filtered, and other capture behavior.
/// </summary>
public sealed class CaptureOptions
{
    /// <summary>
    /// Gets or sets the types of traffic data to capture.
    /// Default is <see cref="CaptureType.AllText"/> (headers, cookies, text content, timings).
    /// </summary>
    public CaptureType CaptureTypes { get; set; } = CaptureType.AllText;

    /// <summary>
    /// Gets or sets the name of the creator tool to include in the HAR file metadata.
    /// Default is "Selenium.HarCapture".
    /// </summary>
    public string CreatorName { get; set; } = "Selenium.HarCapture";

    /// <summary>
    /// Gets or sets whether to force the use of Selenium's INetwork API even if CDP is available.
    /// Default is false (CDP will be used if available).
    /// </summary>
    /// <remarks>
    /// Set to true to explicitly use INetwork API for cross-browser compatibility testing.
    /// Note that INetwork lacks detailed timings and response body capture.
    /// </remarks>
    public bool ForceSeleniumNetworkApi { get; set; } = false;

    /// <summary>
    /// Gets or sets the maximum size in bytes for response bodies to capture.
    /// Default is 0 (unlimited). Set a positive value to limit captured body size and reduce memory usage.
    /// </summary>
    /// <remarks>
    /// When set to a positive value, response bodies larger than this limit will be truncated.
    /// A value of 0 means no limit (all response bodies are captured in full).
    /// </remarks>
    public long MaxResponseBodySize { get; set; } = 0;

    /// <summary>
    /// Gets or sets URL patterns to include for capture (glob patterns like "https://api.example.com/**").
    /// Default is null (all URLs are included).
    /// </summary>
    /// <remarks>
    /// When null, all URLs are captured (no filtering).
    /// When set, only URLs matching at least one pattern are captured.
    /// Exclude patterns take precedence over include patterns.
    /// </remarks>
    public IReadOnlyList<string>? UrlIncludePatterns { get; set; }

    /// <summary>
    /// Gets or sets URL patterns to exclude from capture (glob patterns like "**/*.png").
    /// Default is null (nothing is explicitly excluded).
    /// </summary>
    /// <remarks>
    /// When null, no URLs are explicitly excluded.
    /// When set, URLs matching any pattern are excluded from capture.
    /// Exclude patterns take precedence over include patterns.
    /// </remarks>
    public IReadOnlyList<string>? UrlExcludePatterns { get; set; }
}
