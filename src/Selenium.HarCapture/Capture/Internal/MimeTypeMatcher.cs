using System;
using System.Collections.Generic;
using System.Linq;

namespace Selenium.HarCapture.Capture.Internal;

/// <summary>
/// Determines whether a response body should be retrieved based on MIME type.
/// Used to skip expensive CDP <c>Network.getResponseBody</c> calls for resource types
/// the user doesn't need (CSS, JS, images, fonts, etc.).
/// </summary>
internal sealed class MimeTypeMatcher
{
    private static readonly string[] PagesAndApiTypes =
    {
        "text/html",
        "application/json",
        "application/xml",
        "text/xml",
        "multipart/form-data",
        "application/x-www-form-urlencoded"
    };

    private static readonly string[] TextContentExactTypes =
    {
        "application/json",
        "application/xml",
        "application/javascript",
        "application/x-javascript"
    };

    /// <summary>
    /// Singleton instance that matches all MIME types (used for <see cref="ResponseBodyScope.All"/>).
    /// </summary>
    public static MimeTypeMatcher CaptureAll { get; } = new(ResponseBodyScope.All, null);

    private readonly ResponseBodyScope _scope;
    private readonly HashSet<string>? _extraMimeTypes;

    private MimeTypeMatcher(ResponseBodyScope scope, IReadOnlyList<string>? extraMimeTypes)
    {
        _scope = scope;
        if (extraMimeTypes != null && extraMimeTypes.Count > 0)
        {
            _extraMimeTypes = new HashSet<string>(
                extraMimeTypes.Select(m => m.ToLowerInvariant()),
                StringComparer.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Creates a <see cref="MimeTypeMatcher"/> from scope and optional extra MIME types.
    /// Extra types are additive to the preset — if EITHER preset OR extra types match, body is retrieved.
    /// </summary>
    public static MimeTypeMatcher FromScope(ResponseBodyScope scope, IReadOnlyList<string>? extraMimeTypes)
    {
        if (scope == ResponseBodyScope.All && (extraMimeTypes == null || extraMimeTypes.Count == 0))
            return CaptureAll;

        return new MimeTypeMatcher(scope, extraMimeTypes);
    }

    /// <summary>
    /// Determines whether the response body should be retrieved for the given MIME type.
    /// </summary>
    /// <param name="mimeType">The MIME type from CDP response (e.g., "text/html; charset=utf-8"). May be null.</param>
    /// <returns>true if the body should be retrieved; false to skip.</returns>
    public bool ShouldRetrieveBody(string? mimeType)
    {
        if (_scope == ResponseBodyScope.All)
            return true;

        if (_scope == ResponseBodyScope.None)
            return MatchesExtra(mimeType);

        // null/empty mimeType → retrieve (safe default)
        if (string.IsNullOrEmpty(mimeType))
            return true;

        var normalized = StripParameters(mimeType!).ToLowerInvariant();

        if (MatchesPreset(normalized))
            return true;

        return MatchesExtra(mimeType);
    }

    private bool MatchesPreset(string normalizedMimeType)
    {
        switch (_scope)
        {
            case ResponseBodyScope.PagesAndApi:
                return Array.IndexOf(PagesAndApiTypes, normalizedMimeType) >= 0;

            case ResponseBodyScope.TextContent:
                if (normalizedMimeType.StartsWith("text/", StringComparison.Ordinal))
                    return true;
                return Array.IndexOf(TextContentExactTypes, normalizedMimeType) >= 0;

            default:
                return false;
        }
    }

    private bool MatchesExtra(string? mimeType)
    {
        if (_extraMimeTypes == null || string.IsNullOrEmpty(mimeType))
            return false;

        var normalized = StripParameters(mimeType!).ToLowerInvariant();
        return _extraMimeTypes.Contains(normalized);
    }

    /// <summary>
    /// Strips parameters from MIME type (e.g., "text/html; charset=utf-8" → "text/html").
    /// </summary>
    private static string StripParameters(string mimeType)
    {
        var semicolonIndex = mimeType.IndexOf(';');
        if (semicolonIndex >= 0)
            return mimeType.Substring(0, semicolonIndex).Trim();
        return mimeType.Trim();
    }
}
