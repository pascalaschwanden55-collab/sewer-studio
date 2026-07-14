using AuswertungPro.Next.Application.DataPage;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Media;

namespace AuswertungPro.Next.Infrastructure.Tests.Media;

public sealed class DichtheitProtocolFileLocatorTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "DichtheitProtocolFileLocatorTests_" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void FindPdfPaths_findet_nur_DP_im_festen_Haltungsordner_neueste_zuerst()
    {
        var haltung = new HaltungRecord();
        haltung.SetFieldValue("Haltungsname", "58951-58950", FieldSource.Xtf, userEdited: false);

        var ziel = Path.Combine(_root, "Altdorf", "2026", "58951-58950");
        Directory.CreateDirectory(ziel);
        var alt = LegeDateiAn(ziel, "20260501_58951-58950_DP.pdf");
        var neu = LegeDateiAn(ziel, "20260622_58951-58950_DP.pdf");
        LegeDateiAn(ziel, "20260622_58951-58950.pdf");

        IDichtheitProtocolFileLocator locator = new DichtheitProtocolFileLocator();

        var gefunden = locator.FindPdfPaths(haltung, projectFolder: null, configuredRoot: _root);

        Assert.Equal([neu, alt], gefunden);
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
            // Temp-Aufraeumen darf den Testlauf nicht verdecken.
        }
    }

    private static string LegeDateiAn(string ordner, string name)
    {
        var pfad = Path.Combine(ordner, name);
        File.WriteAllText(pfad, "%PDF");
        return pfad;
    }
}
