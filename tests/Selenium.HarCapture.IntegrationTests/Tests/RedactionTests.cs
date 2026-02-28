using FluentAssertions;
using Selenium.HarCapture.Capture;
using Selenium.HarCapture.IntegrationTests.Infrastructure;

namespace Selenium.HarCapture.IntegrationTests.Tests;

[Collection(IntegrationTestCollection.Name)]
public sealed class RedactionTests : IntegrationTestBase
{
    public RedactionTests(TestWebServer server)
        : base(server) { }

    [Fact]
    public void SensitiveHeaders_AreRedacted_InHarOutput()
    {
        // Arrange
        var options = NetworkOptions()
            .WithSensitiveHeaders("Authorization", "X-Api-Key");

        using var capture = StartCapture(options);

        // Act
        NavigateTo("/api/sensitive");
        WaitForNetworkIdle();
        var har = capture.Stop();

        // Assert
        var entry = har.Log.Entries.Should()
            .Contain(e => e.Request.Url.Contains("/api/sensitive"))
            .Which;

        // Sensitive headers should be redacted
        var authHeader = entry.Response.Headers
            .FirstOrDefault(h => h.Name.Equals("Authorization", StringComparison.OrdinalIgnoreCase));
        if (authHeader != null)
        {
            authHeader.Value.Should().Be("[REDACTED]");
        }

        var apiKeyHeader = entry.Response.Headers
            .FirstOrDefault(h => h.Name.Equals("X-Api-Key", StringComparison.OrdinalIgnoreCase));
        if (apiKeyHeader != null)
        {
            apiKeyHeader.Value.Should().Be("[REDACTED]");
        }

        // Non-sensitive headers should NOT be redacted
        var customHeader = entry.Response.Headers
            .FirstOrDefault(h => h.Name.Equals("X-Custom-Header", StringComparison.OrdinalIgnoreCase));
        if (customHeader != null)
        {
            customHeader.Value.Should().NotBe("[REDACTED]");
        }
    }

    [Fact]
    public void SensitiveCookies_AreRedacted_InHarOutput()
    {
        // Arrange
        var options = NetworkOptions()
            .WithSensitiveCookies("session-id");

        using var capture = StartCapture(options);

        // Act
        NavigateTo("/api/sensitive");
        WaitForNetworkIdle();
        var har = capture.Stop();

        // Assert
        var entry = har.Log.Entries.Should()
            .Contain(e => e.Request.Url.Contains("/api/sensitive"))
            .Which;

        // Check response cookies
        if (entry.Response.Cookies != null && entry.Response.Cookies.Count > 0)
        {
            var sessionCookie = entry.Response.Cookies
                .FirstOrDefault(c => c.Name == "session-id");
            if (sessionCookie != null)
            {
                sessionCookie.Value.Should().Be("[REDACTED]");
            }

            // Non-sensitive cookie should NOT be redacted
            var trackingCookie = entry.Response.Cookies
                .FirstOrDefault(c => c.Name == "tracking");
            if (trackingCookie != null)
            {
                trackingCookie.Value.Should().NotBe("[REDACTED]");
            }
        }
    }

    [Fact]
    public void SensitiveQueryParams_AreRedacted_InHarUrl()
    {
        // Arrange
        var options = NetworkOptions()
            .WithSensitiveQueryParams("token", "api_*");

        using var capture = StartCapture(options);

        // Act — navigate with sensitive query params
        NavigateTo("/api/with-query?token=secret123&api_key=sk-456&page=1");
        WaitForNetworkIdle();
        var har = capture.Stop();

        // Assert
        var entry = har.Log.Entries.Should()
            .Contain(e => e.Request.Url.Contains("/api/with-query"))
            .Which;

        // URL should have redacted query params
        entry.Request.Url.Should().Contain("token=[REDACTED]");
        entry.Request.Url.Should().Contain("api_key=[REDACTED]");
        // Non-sensitive param should be preserved
        entry.Request.Url.Should().Contain("page=1");

        // QueryString collection should also be redacted
        if (entry.Request.QueryString != null && entry.Request.QueryString.Count > 0)
        {
            var tokenParam = entry.Request.QueryString
                .FirstOrDefault(q => q.Name == "token");
            if (tokenParam != null)
            {
                tokenParam.Value.Should().Be("[REDACTED]");
            }
        }
    }

    [Fact]
    public void NoRedactionConfigured_ValuesArePreserved()
    {
        // Arrange — no redaction options
        var options = NetworkOptions();

        using var capture = StartCapture(options);

        // Act
        NavigateTo("/api/sensitive");
        WaitForNetworkIdle();
        var har = capture.Stop();

        // Assert — values should NOT be redacted
        var entry = har.Log.Entries.Should()
            .Contain(e => e.Request.Url.Contains("/api/sensitive"))
            .Which;

        // Headers should contain original values
        var headers = entry.Response.Headers;
        headers.Should().NotBeEmpty();

        // At least check no [REDACTED] appears when not configured
        var redactedHeaders = headers.Where(h => h.Value == "[REDACTED]").ToList();
        redactedHeaders.Should().BeEmpty("no redaction was configured");
    }
}
