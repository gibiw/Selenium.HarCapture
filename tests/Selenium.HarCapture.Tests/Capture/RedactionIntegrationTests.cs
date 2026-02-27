using FluentAssertions;
using Selenium.HarCapture.Capture;
using Xunit;

namespace Selenium.HarCapture.Tests.Capture;

public sealed class RedactionIntegrationTests
{
    [Fact]
    public void CaptureOptions_WithSensitiveHeaders_SetsProperty()
    {
        // Arrange & Act
        var options = new CaptureOptions()
            .WithSensitiveHeaders("Authorization", "X-Api-Key");

        // Assert
        options.SensitiveHeaders.Should().NotBeNull();
        options.SensitiveHeaders.Should().HaveCount(2);
        options.SensitiveHeaders.Should().Contain("Authorization");
        options.SensitiveHeaders.Should().Contain("X-Api-Key");
    }

    [Fact]
    public void CaptureOptions_WithSensitiveCookies_SetsProperty()
    {
        // Arrange & Act
        var options = new CaptureOptions()
            .WithSensitiveCookies("session_id", "auth_token");

        // Assert
        options.SensitiveCookies.Should().NotBeNull();
        options.SensitiveCookies.Should().HaveCount(2);
        options.SensitiveCookies.Should().Contain("session_id");
        options.SensitiveCookies.Should().Contain("auth_token");
    }

    [Fact]
    public void CaptureOptions_WithSensitiveQueryParams_SetsProperty()
    {
        // Arrange & Act
        var options = new CaptureOptions()
            .WithSensitiveQueryParams("api_*", "token");

        // Assert
        options.SensitiveQueryParams.Should().NotBeNull();
        options.SensitiveQueryParams.Should().HaveCount(2);
        options.SensitiveQueryParams.Should().Contain("api_*");
        options.SensitiveQueryParams.Should().Contain("token");
    }

    [Fact]
    public void CaptureOptions_FluentChaining_Works()
    {
        // Arrange & Act
        var options = new CaptureOptions()
            .WithSensitiveHeaders("Authorization")
            .WithSensitiveCookies("session_id")
            .WithSensitiveQueryParams("api_*")
            .WithMaxResponseBodySize(1024000);

        // Assert
        options.SensitiveHeaders.Should().NotBeNull();
        options.SensitiveHeaders.Should().Contain("Authorization");
        options.SensitiveCookies.Should().NotBeNull();
        options.SensitiveCookies.Should().Contain("session_id");
        options.SensitiveQueryParams.Should().NotBeNull();
        options.SensitiveQueryParams.Should().Contain("api_*");
        options.MaxResponseBodySize.Should().Be(1024000);
    }

    [Fact]
    public void CaptureOptions_Defaults_RedactionPropertiesAreNull()
    {
        // Arrange & Act
        var options = new CaptureOptions();

        // Assert
        options.SensitiveHeaders.Should().BeNull();
        options.SensitiveCookies.Should().BeNull();
        options.SensitiveQueryParams.Should().BeNull();
    }
}
