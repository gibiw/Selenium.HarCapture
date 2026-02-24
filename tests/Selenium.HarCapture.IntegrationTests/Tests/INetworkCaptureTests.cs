using System;
using System.IO;
using System.Linq;
using System.Threading;
using FluentAssertions;
using OpenQA.Selenium.Chrome;
using Selenium.HarCapture.Capture;
using Selenium.HarCapture.IntegrationTests.Infrastructure;
using Selenium.HarCapture.Serialization;

namespace Selenium.HarCapture.IntegrationTests.Tests;

[Trait("Category", "Integration")]
public sealed class INetworkCaptureTests : IAsyncLifetime
{
    private ChromeDriver _driver = null!;
    private TestWebServer _server = null!;

    public async Task InitializeAsync()
    {
        _server = new TestWebServer();
        await _server.InitializeAsync();

        var options = new ChromeOptions();
        options.AddArgument("--headless=new");
        options.AddArgument("--no-sandbox");
        options.AddArgument("--disable-dev-shm-usage");
        options.AddArgument("--disable-gpu");
        options.AddArgument("--disable-extensions");

        _driver = new ChromeDriver(options);
    }

    public async Task DisposeAsync()
    {
        _driver?.Quit();
        _driver?.Dispose();
        await _server.DisposeAsync();
    }

    [Fact]
    public void Navigate_ToPage_INetworkCapturesNonEmptyEntries()
    {
        // Arrange
        var options = new CaptureOptions()
            .WithCaptureTypes(CaptureType.AllText)
            .ForceSeleniumNetwork();

        using var capture = new HarCapture(_driver, options);
        capture.Start();

        // Act
        _driver.Navigate().GoToUrl($"{_server.BaseUrl}/");
        Thread.Sleep(500);
        var har = capture.Stop();

        // Assert
        capture.ActiveStrategyName.Should().Be("INetwork");
        har.Log.Entries.Should().NotBeEmpty("INetwork should capture HTTP requests during navigation");
        har.Log.Entries.Count.Should().BeGreaterThan(0);
        har.Log.Version.Should().Be("1.2");
    }

    [Fact]
    public void Navigate_ToPage_INetworkProducesValidSerializableHar()
    {
        string? tempFile = null;

        try
        {
            // Arrange
            var options = new CaptureOptions()
                .WithCaptureTypes(CaptureType.AllText)
                .ForceSeleniumNetwork();

            using var capture = new HarCapture(_driver, options);
            capture.Start();

            // Act
            _driver.Navigate().GoToUrl($"{_server.BaseUrl}/");
            Thread.Sleep(500);
            var har = capture.Stop();

            // Serialize to JSON string
            var json = HarSerializer.Serialize(har);
            json.Length.Should().BeGreaterThan(100, "Serialized HAR should be > 100 bytes");

            // Save to temp file
            tempFile = Path.Combine(Path.GetTempPath(), $"inetwork_test_{Guid.NewGuid()}.har");
            HarSerializer.Save(har, tempFile);

            // Load back from file
            var loadedHar = HarSerializer.Load(tempFile);

            // Assert
            loadedHar.Log.Entries.Count.Should().Be(har.Log.Entries.Count, "Loaded HAR should have same entry count as original");
            loadedHar.Log.Version.Should().Be("1.2");
        }
        finally
        {
            if (tempFile != null && File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public void Navigate_ToApiEndpoint_INetworkCapturesResponseBody()
    {
        // Arrange
        var options = new CaptureOptions()
            .WithCaptureTypes(CaptureType.AllText)
            .ForceSeleniumNetwork();

        using var capture = new HarCapture(_driver, options);
        capture.Start();

        // Act
        _driver.Navigate().GoToUrl($"{_server.BaseUrl}/api/data");
        Thread.Sleep(500);
        var har = capture.Stop();

        // Assert - find entry matching /api/data URL
        var apiEntry = har.Log.Entries.Should()
            .Contain(e => e.Request.Url.Contains("/api/data"), "INetwork should capture API endpoint request")
            .Which;

        apiEntry.Response.Status.Should().Be(200);
        apiEntry.Response.Content.Should().NotBeNull();
        apiEntry.Response.Content.Text.Should().NotBeNullOrEmpty("Response body should be captured with AllText");

        // The /api/data endpoint returns JSON with "hello" and 42
        var responseBody = apiEntry.Response.Content.Text;
        responseBody.Should().Contain("hello", "Response should contain 'hello' from JSON");
        responseBody.Should().Contain("42", "Response should contain value 42 from JSON");

        apiEntry.Response.Headers.Should().NotBeNull();
        apiEntry.Response.Headers.Should().NotBeEmpty("Response headers should be captured");
    }

    [Fact]
    public void Navigate_ToLargeResponse_INetworkCapturesWithoutTruncation()
    {
        string? tempFile = null;

        try
        {
            // Arrange
            var options = new CaptureOptions()
                .WithCaptureTypes(CaptureType.AllText)
                .ForceSeleniumNetwork();

            using var capture = new HarCapture(_driver, options);
            capture.Start();

            // Act - navigate to 500KB response endpoint
            _driver.Navigate().GoToUrl($"{_server.BaseUrl}/api/very-large");
            Thread.Sleep(1000); // Give time for large response to be captured
            var har = capture.Stop();

            // Serialize to JSON string
            var json = HarSerializer.Serialize(har);

            // Assert - serialized JSON should contain the full 500KB body
            json.Length.Should().BeGreaterThan(500_000, "Serialized HAR should contain full 500KB response body without truncation");

            // Save to temp file and load back
            tempFile = Path.Combine(Path.GetTempPath(), $"inetwork_test_{Guid.NewGuid()}.har");
            HarSerializer.Save(har, tempFile);

            var loadedHar = HarSerializer.Load(tempFile);

            // Find the large response entry
            var largeEntry = loadedHar.Log.Entries.Should()
                .Contain(e => e.Request.Url.Contains("/api/very-large"), "HAR should contain the large response entry")
                .Which;

            largeEntry.Response.Content.Text.Should().NotBeNullOrEmpty();
            largeEntry.Response.Content.Text!.Length.Should().BeGreaterOrEqualTo(500_000, "Response body should be at least 500KB");

            // Verify JSON is valid by successful deserialization
            loadedHar.Log.Version.Should().Be("1.2");
        }
        finally
        {
            if (tempFile != null && File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public void SaveAsync_CalledSynchronously_INetworkHarNotTruncated()
    {
        string? tempFile = null;

        try
        {
            // Arrange - reproduce EXACT user pattern
            var options = new CaptureOptions()
                .WithCaptureTypes(CaptureType.AllText)
                .ForceSeleniumNetwork();

            using var capture = new HarCapture(_driver, options);
            capture.Start();

            // Act - navigate to page with fetch (page + subrequest)
            _driver.Navigate().GoToUrl($"{_server.BaseUrl}/with-fetch");
            Thread.Sleep(1000);
            var har = capture.Stop();

            // User pattern: SaveAsync called synchronously via .GetAwaiter().GetResult()
            // This is intentionally blocking to reproduce the reported issue
            tempFile = Path.Combine(Path.GetTempPath(), $"inetwork_test_{Guid.NewGuid()}.har");
#pragma warning disable xUnit1031
            HarSerializer.SaveAsync(har, tempFile).GetAwaiter().GetResult();
#pragma warning restore xUnit1031

            // Load back
            var loadedHar = HarSerializer.Load(tempFile);

            // Assert - loaded HAR should have same entry count as original
            loadedHar.Log.Entries.Count.Should().Be(har.Log.Entries.Count, "SaveAsync called synchronously should produce complete HAR file");

            // Verify file size matches in-memory serialization
            var fileSize = new FileInfo(tempFile).Length;
            var expectedSize = HarSerializer.Serialize(har).Length;
            fileSize.Should().Be(expectedSize, "File size should match in-memory JSON serialization");

            loadedHar.Log.Version.Should().Be("1.2");
        }
        finally
        {
            if (tempFile != null && File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }
}
