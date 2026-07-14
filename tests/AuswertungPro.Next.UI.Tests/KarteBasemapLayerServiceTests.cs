using AuswertungPro.Next.UI.Mapping;
using System.IO;

namespace AuswertungPro.Next.UI.Tests;

public sealed class KarteBasemapLayerServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "KarteBasemapLayerServiceTests_" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void Offline_Layer_werden_nur_fuer_vorhandene_Kartenordner_erstellt()
    {
        Directory.CreateDirectory(Path.Combine(_root, KarteBasemapLayerFactory.SatellitSubfolder));
        IKarteBasemapLayerFactory factory = new KarteBasemapLayerService();

        var satellit = factory.CreateOfflineSatellit(_root);
        var av = factory.CreateOfflineAv(_root);

        Assert.NotNull(satellit);
        Assert.Equal(KarteBasemapLayerFactory.SatellitLayerName, satellit!.Name);
        Assert.Null(av);
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
