using AuswertungPro.Next.Infrastructure.Output.Offers;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class OfferHtmlToPdfRendererTimeoutTests
{
    [Fact]
    public async Task RenderAsync_ends_hanging_browser_work_at_total_timeout()
    {
        using var directory = new TempDirectory();
        var templatePath = Path.Combine(directory.Path, "offer.sbnhtml");
        var outputPath = Path.Combine(directory.Path, "offer.pdf");
        File.WriteAllText(templatePath, "<html><body>Test</body></html>");
        var renderer = new OfferHtmlToPdfRenderer(
            TimeSpan.FromMilliseconds(50),
            (_, _, ct) => Task.Delay(Timeout.InfiniteTimeSpan, ct));

        var exception = await Assert.ThrowsAsync<System.TimeoutException>(() =>
            renderer.RenderAsync(new object(), templatePath, outputPath, logoPngPath: null));

        Assert.Contains("50", exception.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(outputPath));
    }

    [Fact]
    public async Task RenderAsync_preserves_caller_cancellation_as_operation_canceled()
    {
        using var directory = new TempDirectory();
        var templatePath = Path.Combine(directory.Path, "offer.sbnhtml");
        var outputPath = Path.Combine(directory.Path, "offer.pdf");
        File.WriteAllText(templatePath, "<html><body>Test</body></html>");
        var renderer = new OfferHtmlToPdfRenderer(
            TimeSpan.FromSeconds(5),
            (_, _, ct) => Task.Delay(Timeout.InfiniteTimeSpan, ct));
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(30));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            renderer.RenderAsync(new object(), templatePath, outputPath, logoPngPath: null, cts.Token));
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = Directory.CreateTempSubdirectory().FullName;

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }
}
