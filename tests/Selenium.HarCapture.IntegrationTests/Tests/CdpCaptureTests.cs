using FluentAssertions;
using Selenium.HarCapture.IntegrationTests.Infrastructure;
using Selenium.HarCapture.Serialization;

namespace Selenium.HarCapture.IntegrationTests.Tests;

[Collection(IntegrationTestCollection.Name)]
public sealed class CdpCaptureTests : IntegrationTestBase
{
    public CdpCaptureTests(TestWebServer server)
        : base(server) { }

    [Fact]
    public void Navigate_ToPage_CdpCapturesNonEmptyEntries()
    {
        // Skip if browser version not CDP-compatible
        if (!IsCdpCompatible())
        {
            return;
        }

        // Arrange
        using var capture = StartCapture(CdpOptions());

        // Assert strategy is CDP
        capture.ActiveStrategyName.Should().Be("CDP");

        // Act
        NavigateTo("/");
        WaitForNetworkIdle();
        var har = capture.Stop();

        // Assert
        har.Log.Entries.Should().NotBeEmpty("CDP should capture HTTP requests during navigation");
        har.Log.Entries.Count.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Navigate_ToPage_CdpProducesValidSerializableHar()
    {
        // Skip if browser version not CDP-compatible
        if (!IsCdpCompatible())
        {
            return;
        }

        // Arrange
        using var capture = StartCapture(CdpOptions());

        // Act
        NavigateTo("/");
        WaitForNetworkIdle();
        var har = capture.Stop();

        // Serialize to JSON
        var json = HarSerializer.Serialize(har, writeIndented: false);

        // Assert
        json.Length.Should().BeGreaterThan(1024, "HAR JSON should be > 1KB");

        // Deserialize back
        var deserializedHar = HarSerializer.Deserialize(json);

        // Assert deserialization worked
        deserializedHar.Log.Entries.Count.Should().Be(har.Log.Entries.Count);
    }

    [Fact]
    public void Navigate_ToApiEndpoint_CdpCapturesStatusAndHeaders()
    {
        // Skip if browser version not CDP-compatible
        if (!IsCdpCompatible())
        {
            return;
        }

        // Arrange
        using var capture = StartCapture(CdpOptions());

        // Act
        NavigateTo("/api/data");
        WaitForNetworkIdle();
        var har = capture.Stop();

        // Assert - find entry with /api/data URL
        var apiEntry = har.Log.Entries.Should()
            .Contain(e => e.Request.Url.Contains("/api/data"), "CDP should capture API endpoint request")
            .Which;

        // Verify status and method
        apiEntry.Response.Status.Should().Be(200);
        apiEntry.Request.Method.Should().Be("GET");

        // Verify headers are captured (validates header casting fix)
        apiEntry.Response.Headers.Should().NotBeNull();
        apiEntry.Response.Headers.Should().NotBeEmpty("CDP should capture response headers");
    }

    [Fact]
    public void Navigate_ToFetchPage_CdpCapturesSubresourceRequests()
    {
        // Skip if browser version not CDP-compatible
        if (!IsCdpCompatible())
        {
            return;
        }

        // Arrange
        using var capture = StartCapture(CdpOptions());

        // Act
        NavigateTo("/with-fetch");
        WaitForNetworkIdle(1000); // Give fetch time to complete

        var har = capture.Stop();

        // Assert - document request
        har.Log.Entries.Should()
            .Contain(e => e.Request.Url.Contains("/with-fetch"), "CDP should capture document request");

        // Assert - fetch subrequest
        har.Log.Entries.Should()
            .Contain(e => e.Request.Url.Contains("/api/data"), "CDP should capture fetch subrequest");
    }

    [Fact]
    public void MultipleCdpCaptures_ProduceConsistentResults()
    {
        // Skip if browser version not CDP-compatible
        if (!IsCdpCompatible())
        {
            return;
        }

        var entryCounts = new List<int>();

        // Run capture 3 times
        for (int i = 0; i < 3; i++)
        {
            using var capture = StartCapture(CdpOptions());
            NavigateTo("/");
            WaitForNetworkIdle();
            var har = capture.Stop();

            entryCounts.Add(har.Log.Entries.Count);
        }

        // Assert all runs produced entries
        entryCounts.Should().AllSatisfy(count => count.Should().BeGreaterThan(0, "CDP should produce consistent non-empty results"));
        entryCounts.Should().NotContain(0, "No run should produce 0 entries");
    }
}
