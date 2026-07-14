using System.Diagnostics;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Infrastructure.Common;

namespace AuswertungPro.Next.Infrastructure.Tests.Common;

public sealed class ExplorerRevealLauncherTests : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(
        Path.GetTempPath(),
        $"ExplorerRevealLauncherTests_{Guid.NewGuid():N}");

    [Fact]
    public void TryReveal_StartsInjectedExplorerForExistingFile()
    {
        Directory.CreateDirectory(_tempDirectory);
        var filePath = Path.Combine(_tempDirectory, "report.pdf");
        File.WriteAllText(filePath, "test");
        ProcessStartInfo? captured = null;
        IExplorerRevealService service = new ExplorerRevealLauncher(
            startInfo => captured = startInfo);

        var success = service.TryReveal(filePath, out var error);

        Assert.True(success, error);
        Assert.NotNull(captured);
        Assert.Equal("explorer.exe", captured.FileName);
        Assert.Equal($"/select,\"{Path.GetFullPath(filePath)}\"", captured.Arguments);
    }

    [Fact]
    public void TryReveal_DoesNotStartExplorerForMissingTarget()
    {
        var startCalls = 0;
        IExplorerRevealService service = new ExplorerRevealLauncher(_ => startCalls++);

        var success = service.TryReveal(
            Path.Combine(_tempDirectory, "missing.pdf"),
            out var error);

        Assert.False(success);
        Assert.Equal(0, startCalls);
        Assert.Equal("Datei oder Ordner nicht gefunden.", error);
    }

    [Fact]
    public void TryReveal_ReturnsStartFailureWithoutThrowing()
    {
        Directory.CreateDirectory(_tempDirectory);
        IExplorerRevealService service = new ExplorerRevealLauncher(
            _ => throw new InvalidOperationException("Start blockiert"));

        var success = service.TryReveal(_tempDirectory, out var error);

        Assert.False(success);
        Assert.Equal("Start blockiert", error);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDirectory))
                Directory.Delete(_tempDirectory, recursive: true);
        }
        catch
        {
            // Test-Aufraeumen ist best effort.
        }
    }
}
