using FluentAssertions;
using Selenium.HarCapture.Capture;
using Selenium.HarCapture.Capture.Internal;
using Xunit;

namespace Selenium.HarCapture.Tests.Capture.Internal;

public sealed class MimeTypeMatcherTests
{
    [Theory]
    [InlineData("text/html")]
    [InlineData("application/json")]
    [InlineData("image/png")]
    [InlineData("font/woff2")]
    [InlineData("application/octet-stream")]
    public void CaptureAll_AllMimeTypes_ReturnsTrue(string mimeType)
    {
        var matcher = MimeTypeMatcher.CaptureAll;
        matcher.ShouldRetrieveBody(mimeType).Should().BeTrue();
    }

    [Theory]
    [InlineData("text/html")]
    [InlineData("application/json")]
    [InlineData("application/xml")]
    [InlineData("text/xml")]
    [InlineData("multipart/form-data")]
    [InlineData("application/x-www-form-urlencoded")]
    public void PagesAndApi_HtmlJson_ReturnsTrue(string mimeType)
    {
        var matcher = MimeTypeMatcher.FromScope(ResponseBodyScope.PagesAndApi, null);
        matcher.ShouldRetrieveBody(mimeType).Should().BeTrue();
    }

    [Theory]
    [InlineData("text/css")]
    [InlineData("application/javascript")]
    [InlineData("image/png")]
    [InlineData("image/jpeg")]
    [InlineData("font/woff2")]
    [InlineData("application/octet-stream")]
    public void PagesAndApi_CssJsImage_ReturnsFalse(string mimeType)
    {
        var matcher = MimeTypeMatcher.FromScope(ResponseBodyScope.PagesAndApi, null);
        matcher.ShouldRetrieveBody(mimeType).Should().BeFalse();
    }

    [Theory]
    [InlineData("text/html")]
    [InlineData("text/css")]
    [InlineData("text/plain")]
    [InlineData("text/javascript")]
    [InlineData("application/json")]
    [InlineData("application/xml")]
    [InlineData("application/javascript")]
    [InlineData("application/x-javascript")]
    public void TextContent_AllText_ReturnsTrue(string mimeType)
    {
        var matcher = MimeTypeMatcher.FromScope(ResponseBodyScope.TextContent, null);
        matcher.ShouldRetrieveBody(mimeType).Should().BeTrue();
    }

    [Theory]
    [InlineData("image/png")]
    [InlineData("image/jpeg")]
    [InlineData("font/woff2")]
    [InlineData("application/octet-stream")]
    [InlineData("audio/mpeg")]
    public void TextContent_ImageFont_ReturnsFalse(string mimeType)
    {
        var matcher = MimeTypeMatcher.FromScope(ResponseBodyScope.TextContent, null);
        matcher.ShouldRetrieveBody(mimeType).Should().BeFalse();
    }

    [Theory]
    [InlineData("text/html")]
    [InlineData("application/json")]
    [InlineData("image/png")]
    [InlineData("font/woff2")]
    public void None_AlwaysFalse(string mimeType)
    {
        var matcher = MimeTypeMatcher.FromScope(ResponseBodyScope.None, null);
        matcher.ShouldRetrieveBody(mimeType).Should().BeFalse();
    }

    [Fact]
    public void ExtraMimeTypes_MatchAdditionalTypes()
    {
        var matcher = MimeTypeMatcher.FromScope(ResponseBodyScope.None, new[] { "image/png", "image/svg+xml" });

        matcher.ShouldRetrieveBody("image/png").Should().BeTrue();
        matcher.ShouldRetrieveBody("image/svg+xml").Should().BeTrue();
        matcher.ShouldRetrieveBody("image/jpeg").Should().BeFalse();
        matcher.ShouldRetrieveBody("text/html").Should().BeFalse();
    }

    [Fact]
    public void PresetPlusExtra_BothMatch()
    {
        var matcher = MimeTypeMatcher.FromScope(ResponseBodyScope.PagesAndApi, new[] { "image/png" });

        matcher.ShouldRetrieveBody("text/html").Should().BeTrue();
        matcher.ShouldRetrieveBody("application/json").Should().BeTrue();
        matcher.ShouldRetrieveBody("image/png").Should().BeTrue();
        matcher.ShouldRetrieveBody("text/css").Should().BeFalse();
        matcher.ShouldRetrieveBody("image/jpeg").Should().BeFalse();
    }

    [Fact]
    public void NullMimeType_ReturnsTrue()
    {
        // Safe default: retrieve body when MIME type is unknown
        var matcher = MimeTypeMatcher.FromScope(ResponseBodyScope.PagesAndApi, null);
        matcher.ShouldRetrieveBody(null).Should().BeTrue();
    }

    [Fact]
    public void EmptyMimeType_ReturnsTrue()
    {
        var matcher = MimeTypeMatcher.FromScope(ResponseBodyScope.TextContent, null);
        matcher.ShouldRetrieveBody("").Should().BeTrue();
    }

    [Theory]
    [InlineData("text/html; charset=utf-8")]
    [InlineData("application/json; charset=utf-8")]
    [InlineData("TEXT/HTML; charset=UTF-8")]
    public void MimeTypeWithCharset_MatchesPrefix(string mimeType)
    {
        var matcher = MimeTypeMatcher.FromScope(ResponseBodyScope.PagesAndApi, null);
        matcher.ShouldRetrieveBody(mimeType).Should().BeTrue();
    }

    [Theory]
    [InlineData("TEXT/HTML")]
    [InlineData("Application/Json")]
    [InlineData("IMAGE/PNG")]
    public void CaseInsensitive_MatchesCorrectly(string mimeType)
    {
        var pagesAndApi = MimeTypeMatcher.FromScope(ResponseBodyScope.PagesAndApi, null);
        pagesAndApi.ShouldRetrieveBody("TEXT/HTML").Should().BeTrue();
        pagesAndApi.ShouldRetrieveBody("Application/Json").Should().BeTrue();
        pagesAndApi.ShouldRetrieveBody("IMAGE/PNG").Should().BeFalse();
    }

    [Fact]
    public void AllScopeWithExtra_IgnoresExtra()
    {
        // All already retrieves everything, extra types are irrelevant
        var matcher = MimeTypeMatcher.FromScope(ResponseBodyScope.All, new[] { "image/png" });
        matcher.ShouldRetrieveBody("font/woff2").Should().BeTrue();
        matcher.ShouldRetrieveBody("image/png").Should().BeTrue();
    }

    [Fact]
    public void FromScope_AllWithNoExtra_ReturnsSingleton()
    {
        var matcher = MimeTypeMatcher.FromScope(ResponseBodyScope.All, null);
        matcher.Should().BeSameAs(MimeTypeMatcher.CaptureAll);
    }
}
