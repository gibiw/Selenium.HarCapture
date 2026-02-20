using System;
using Selenium.HarCapture.Models;

namespace Selenium.HarCapture.Capture.Internal;

/// <summary>
/// Maps Chrome DevTools Protocol ResourceTiming data to HAR timings format.
/// Implementation follows Chrome DevTools HAREntry.js pattern.
/// See: .planning/phases/03-cdp-strategy/03-RESEARCH.md
/// </summary>
internal static class CdpTimingMapper
{
    /// <summary>
    /// Converts CDP ResourceTiming fields to HAR timings.
    /// </summary>
    /// <param name="dnsStart">DNS lookup start time in ms (relative to requestTime). -1 if not applicable.</param>
    /// <param name="dnsEnd">DNS lookup end time in ms (relative to requestTime). -1 if not applicable.</param>
    /// <param name="connectStart">Connection start time in ms (relative to requestTime). -1 if not applicable.</param>
    /// <param name="connectEnd">Connection end time in ms (relative to requestTime). -1 if not applicable.</param>
    /// <param name="sslStart">SSL handshake start time in ms (relative to requestTime). -1 if not applicable.</param>
    /// <param name="sslEnd">SSL handshake end time in ms (relative to requestTime). -1 if not applicable.</param>
    /// <param name="sendStart">Request send start time in ms (relative to requestTime). -1 if not applicable.</param>
    /// <param name="sendEnd">Request send end time in ms (relative to requestTime). -1 if not applicable.</param>
    /// <param name="receiveHeadersEnd">Response headers received time in ms (relative to requestTime). -1 if not applicable.</param>
    /// <param name="requestTime">Request timestamp in seconds (absolute time).</param>
    /// <param name="responseReceivedTime">Response received timestamp in seconds (absolute time).</param>
    /// <returns>HAR timings object with all timing phases.</returns>
    /// <remarks>
    /// Per HAR 1.2 spec:
    /// - SSL time is INCLUDED in connect time (not added separately to total)
    /// - Negative values (-1) indicate timing phase is not applicable (e.g., cached connection, reused connection)
    /// - Receive time is calculated from responseReceivedTime baseline and receiveHeadersEnd
    /// - All returned times are in milliseconds
    /// </remarks>
    public static HarTimings MapToHarTimings(
        double dnsStart, double dnsEnd,
        double connectStart, double connectEnd,
        double sslStart, double sslEnd,
        double sendStart, double sendEnd,
        double receiveHeadersEnd,
        double requestTime,
        double responseReceivedTime)
    {
        // Calculate blocked time: time from request start to first positive timing phase
        // Use the first positive value among dnsStart, connectStart, sendStart
        double? blocked = null;
        if (dnsStart >= 0)
            blocked = dnsStart;
        else if (connectStart >= 0)
            blocked = connectStart;
        else if (sendStart >= 0)
            blocked = sendStart;

        // Calculate DNS time: only if both dnsStart and dnsEnd are valid (>= 0)
        double? dns = (dnsStart >= 0 && dnsEnd >= 0) ? dnsEnd - dnsStart : null;

        // Calculate connect time: only if both connectStart and connectEnd are valid (>= 0)
        // Note: This includes SSL time per HAR 1.2 spec
        double? connect = (connectStart >= 0 && connectEnd >= 0) ? connectEnd - connectStart : null;

        // Calculate SSL time: only if both sslStart and sslEnd are valid (>= 0)
        // This is informational only - not added to total time
        double? ssl = (sslStart >= 0 && sslEnd >= 0) ? sslEnd - sslStart : null;

        // Calculate send time: time to send request
        double send = (sendStart >= 0 && sendEnd >= 0) ? sendEnd - sendStart : 0;

        // Calculate wait time: time from request sent to response headers received
        double wait = (sendEnd >= 0 && receiveHeadersEnd >= 0) ? receiveHeadersEnd - sendEnd : 0;

        // Calculate receive time: time to receive response body
        // Formula: (responseReceivedTime - requestTime) * 1000 - receiveHeadersEnd
        // responseReceivedTime and requestTime are in seconds, need to convert to ms
        // Clamp to non-negative (edge case where times are very close)
        double receive = 0;
        if (receiveHeadersEnd >= 0 && responseReceivedTime > requestTime)
        {
            double totalTimeMs = (responseReceivedTime - requestTime) * 1000;
            receive = Math.Max(0, totalTimeMs - receiveHeadersEnd);
        }

        return new HarTimings
        {
            Blocked = blocked,
            Dns = dns,
            Connect = connect,
            Ssl = ssl,
            Send = send,
            Wait = wait,
            Receive = receive
        };
    }
}
