using FluentAssertions;
using Selenium.HarCapture.IntegrationTests.Infrastructure;

namespace Selenium.HarCapture.IntegrationTests.Tests;

[Collection(IntegrationTestCollection.Name)]
public sealed class BasicCaptureTests : IntegrationTestBase
{
    public BasicCaptureTests(TestWebServer server)
        : base(server) { }

    [Fact]
    public void Navigate_ToPage_CapturesEntriesWithCorrectUrls()
    {
        // Arrange
        using var capture = StartCapture(NetworkOptions());

        // Act
        NavigateTo("/");
        WaitForNetworkIdle();
        var har = capture.Stop();

        // Assert
        har.Log.Entries.Should().NotBeEmpty();
        har.Log.Entries.Should().Contain(e => e.Request.Url.Contains("/"));
    }

    [Fact]
    public void Navigate_ToApiEndpoint_CapturesStatusCodeAndMethod()
    {
        // Arrange
        using var capture = StartCapture(NetworkOptions());

        // Act
        NavigateTo("/api/data");
        WaitForNetworkIdle();
        var har = capture.Stop();

        // Assert
        var apiEntry = har.Log.Entries.Should()
            .Contain(e => e.Request.Url.Contains("/api/data"))
            .Which;
        apiEntry.Response.Status.Should().Be(200);
        apiEntry.Request.Method.Should().Be("GET");
    }
}
