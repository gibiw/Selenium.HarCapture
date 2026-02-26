namespace Selenium.HarCapture.Capture;

/// <summary>
/// Controls which response bodies are retrieved via CDP <c>Network.getResponseBody</c>.
/// Limiting body retrieval reduces CDP WebSocket contention and improves navigation speed.
/// </summary>
public enum ResponseBodyScope
{
    /// <summary>
    /// Retrieve bodies for all responses (current default behavior).
    /// </summary>
    All,

    /// <summary>
    /// Retrieve bodies only for pages and API responses:
    /// text/html, application/json, application/xml, text/xml, multipart/form-data, application/x-www-form-urlencoded.
    /// </summary>
    PagesAndApi,

    /// <summary>
    /// Retrieve bodies for all text content:
    /// text/* (prefix match), application/json, application/xml, application/javascript, application/x-javascript.
    /// </summary>
    TextContent,

    /// <summary>
    /// Skip all body retrieval. Only headers and timings are captured.
    /// </summary>
    None
}
