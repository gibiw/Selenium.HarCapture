using FluentAssertions;
using Selenium.HarCapture.Capture;
using Selenium.HarCapture.IntegrationTests.Infrastructure;

namespace Selenium.HarCapture.IntegrationTests.Tests;

[Collection(IntegrationTestCollection.Name)]
public sealed class CancellationTokenTests : IntegrationTestBase
{
    public CancellationTokenTests(TestWebServer server)
        : base(server) { }

    [Fact]
    public async Task StartAsync_WithCancellationToken_WorksCorrectly()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        await using var capture = new HarCapture(Driver, NetworkOptions());

        // Act
        await capture.StartAsync(cancellationToken: cts.Token);
        NavigateTo("/api/data");
        WaitForNetworkIdle();
        var har = await capture.StopAsync(cts.Token);

        // Assert
        har.Log.Entries.Should().NotBeEmpty();
    }

    [Fact]
    public async Task StopAsync_WithAlreadyCancelledToken_ThrowsOperationCanceled()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await using var capture = new HarCapture(Driver, NetworkOptions());
        await capture.StartAsync();

        NavigateTo("/api/data");
        WaitForNetworkIdle();

        // Act & Assert
        var act = () => capture.StopAsync(cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task CaptureHarAsync_WithCancellationToken_ReturnsHar()
    {
        // Arrange
        using var cts = new CancellationTokenSource();

        // Act
        var har = await Extensions.WebDriverExtensions.CaptureHarAsync(
            Driver,
            async () =>
            {
                NavigateTo("/api/data");
                WaitForNetworkIdle();
                await Task.CompletedTask;
            },
            NetworkOptions(),
            cts.Token);

        // Assert
        har.Log.Entries.Should().NotBeEmpty();
    }
}
