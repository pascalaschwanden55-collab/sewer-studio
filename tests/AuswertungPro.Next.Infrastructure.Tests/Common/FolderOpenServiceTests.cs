using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Infrastructure.Common;

namespace AuswertungPro.Next.Infrastructure.Tests.Common;

public sealed class FolderOpenServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "FolderOpenServiceTests_" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void EnsureAndOpen_erstellt_den_Ordner_vor_dem_Oeffnen()
    {
        var target = Path.Combine(_root, "logs");
        var shell = new ShellOpenFake();
        IFolderOpenService service = new FolderOpenService(shell);

        var result = service.EnsureAndOpen(target);

        Assert.True(result.Success);
        Assert.True(Directory.Exists(target));
        Assert.Equal(target, shell.OpenedPath);
    }

    [Fact]
    public void EnsureAndOpen_gibt_den_Fehler_des_Oeffnungsdienstes_zurueck()
    {
        var target = Path.Combine(_root, "logs");
        var shell = new ShellOpenFake
        {
            Success = false,
            Error = "kein Zugriff"
        };
        IFolderOpenService service = new FolderOpenService(shell);

        var result = service.EnsureAndOpen(target);

        Assert.False(result.Success);
        Assert.Equal("kein Zugriff", result.Error);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }
        catch
        {
            // Test-Aufraeumen darf das eigentliche Ergebnis nicht verdecken.
        }
    }

    private sealed class ShellOpenFake : ISafeShellOpenService
    {
        public bool Success { get; init; } = true;
        public string? Error { get; init; }
        public string? OpenedPath { get; private set; }

        public bool TryOpen(string? path, out string? error)
        {
            OpenedPath = path;
            error = Error;
            return Success;
        }
    }
}
