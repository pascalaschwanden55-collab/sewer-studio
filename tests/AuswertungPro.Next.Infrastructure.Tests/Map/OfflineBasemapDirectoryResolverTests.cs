using AuswertungPro.Next.Application.Map;
using AuswertungPro.Next.Infrastructure.Map;

namespace AuswertungPro.Next.Infrastructure.Tests.Map;

public sealed class OfflineBasemapDirectoryResolverTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "OfflineBasemapDirectoryResolverTests_" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void Resolve_verwendet_Elternordner_wenn_veralteter_Uri_Pfad_gespeichert_ist()
    {
        Directory.CreateDirectory(Path.Combine(_root, "av"));
        var stalePath = Path.Combine(_root, "uri");
        Directory.CreateDirectory(stalePath);
        IOfflineBasemapPathResolver resolver = new OfflineBasemapDirectoryResolver();

        var result = resolver.Resolve(stalePath);

        Assert.Equal(_root, result);
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
