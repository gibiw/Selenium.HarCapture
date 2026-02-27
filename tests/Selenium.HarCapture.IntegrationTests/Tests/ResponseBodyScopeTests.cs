using FluentAssertions;
using Selenium.HarCapture.Capture;
using Selenium.HarCapture.IntegrationTests.Infrastructure;

namespace Selenium.HarCapture.IntegrationTests.Tests;

[Collection(IntegrationTestCollection.Name)]
public sealed class ResponseBodyScopeTests : IntegrationTestBase
{
    public ResponseBodyScopeTests(TestWebServer server)
        : base(server) { }

    [Fact]
    public void ResponseBodyScope_None_SkipsAllBodies_INetwork()
    {
        // Arrange
        var options = NetworkOptions()
            .WithResponseBodyScope(ResponseBodyScope.None);

        using var capture = StartCapture(options);

        // Act
        NavigateTo("/api/data");
        WaitForNetworkIdle();
        var har = capture.Stop();

        // Assert
        var entry = har.Log.Entries.Should()
            .Contain(e => e.Request.Url.Contains("/api/data"))
            .Which;

        // Body should be empty or null when scope is None
        var bodyText = entry.Response.Content?.Text;
        bodyText.Should().BeNullOrEmpty("ResponseBodyScope.None should skip all bodies");
    }

    [Fact]
    public void ResponseBodyScope_All_CapturesAllBodies_INetwork()
    {
        // Arrange
        var options = NetworkOptions()
            .WithResponseBodyScope(ResponseBodyScope.All);

        using var capture = StartCapture(options);

        // Act
        NavigateTo("/api/data");
        WaitForNetworkIdle();
        var har = capture.Stop();

        // Assert
        var entry = har.Log.Entries.Should()
            .Contain(e => e.Request.Url.Contains("/api/data"))
            .Which;

        entry.Response.Content?.Text.Should().NotBeNullOrEmpty("ResponseBodyScope.All should capture JSON body");
        entry.Response.Content!.Text.Should().Contain("hello");
    }

    [Fact]
    public void ResponseBodyScope_PagesAndApi_CapturesJsonBody_INetwork()
    {
        // Arrange
        var options = NetworkOptions()
            .WithResponseBodyScope(ResponseBodyScope.PagesAndApi);

        using var capture = StartCapture(options);

        // Act — page with fetch to /api/data
        NavigateTo("/with-fetch");
        WaitForNetworkIdle(1000);
        var har = capture.Stop();

        // Assert — HTML body should be captured (text/html is in PagesAndApi)
        var htmlEntry = har.Log.Entries
            .FirstOrDefault(e => e.Request.Url.Contains("/with-fetch"));
        htmlEntry.Should().NotBeNull();
        htmlEntry!.Response.Content?.Text.Should().NotBeNullOrEmpty("HTML pages should be captured with PagesAndApi scope");

        // Assert — JSON body should be captured (application/json is in PagesAndApi)
        var apiEntry = har.Log.Entries
            .FirstOrDefault(e => e.Request.Url.Contains("/api/data"));
        if (apiEntry != null)
        {
            apiEntry.Response.Content?.Text.Should().NotBeNullOrEmpty("JSON API responses should be captured with PagesAndApi scope");
        }
    }

    [Fact]
    public void ResponseBodyScope_PagesAndApi_SkipsCssBody_INetwork()
    {
        // Arrange
        var options = NetworkOptions()
            .WithResponseBodyScope(ResponseBodyScope.PagesAndApi);

        using var capture = StartCapture(options);

        // Act — navigate to CSS directly
        NavigateTo("/style.css");
        WaitForNetworkIdle();
        var har = capture.Stop();

        // Assert — CSS body should be skipped (text/css is NOT in PagesAndApi)
        var cssEntry = har.Log.Entries
            .FirstOrDefault(e => e.Request.Url.Contains("/style.css"));
        if (cssEntry != null)
        {
            var bodyText = cssEntry.Response.Content?.Text;
            bodyText.Should().BeNullOrEmpty("CSS should be skipped with PagesAndApi scope");
        }
    }
}
