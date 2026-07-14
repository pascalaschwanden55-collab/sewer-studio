using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Infrastructure.Common;

namespace AuswertungPro.Next.Infrastructure.Tests.Common;

public sealed class SafeShellOpenServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "SafeShellOpenServiceTests_" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void TryOpen_blockiert_nicht_freigegebenen_Dateityp_vor_dem_Prozessstart()
    {
        Directory.CreateDirectory(_root);
        var path = Path.Combine(_root, "script.cmd");
        File.WriteAllText(path, "echo test");
        ISafeShellOpenService service = new SafeShellOpenService();

        var success = service.TryOpen(path, out var error);

        Assert.False(success);
        Assert.Equal("Dateityp nicht zum direkten Oeffnen freigegeben: .cmd", error);
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
}
