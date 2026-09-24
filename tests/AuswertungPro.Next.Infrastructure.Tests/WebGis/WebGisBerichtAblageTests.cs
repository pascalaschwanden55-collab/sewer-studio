using System;
using System.IO;
using System.Linq;
using AuswertungPro.Next.Infrastructure.Tests.Backup;
using AuswertungPro.Next.Infrastructure.WebGis;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.WebGis;

/// <summary>
/// Berichte und Log von Senden und Holen liegen unter &lt;Projektordner&gt;\__WebGIS_Export. Pruefung 22.09.2026, C3:
/// Geschrieben wurde ohne die Schreibgrenze des Projekts — ein verknuepfter Ordner haette die Berichte ausserhalb
/// abgelegt. Die Ablage ist jetzt eine Stelle fuer beide Wege.
/// </summary>
public sealed class WebGisBerichtAblageTests : IDisposable
{
    private readonly string _wurzel = Path.Combine(Path.GetTempPath(), "sewerstudio-webgis-ablage-" + Guid.NewGuid().ToString("N"));

    public WebGisBerichtAblageTests() => Directory.CreateDirectory(_wurzel);

    public void Dispose()
    {
        try { Directory.Delete(_wurzel, recursive: true); }
        catch (IOException) { /* Testordner; ein gesperrter Rest stoert keinen weiteren Test. */ }
    }

    [Fact]
    public void Der_ordner_liegt_neben_der_projektdatei()
    {
        var ordner = WebGisBerichtAblage.Ordner(Path.Combine(_wurzel, "projekt.json"));

        Assert.Equal(Path.Combine(_wurzel, "__WebGIS_Export"), ordner);
        Assert.True(Directory.Exists(ordner));
    }

    [Fact]
    public void Projektdateien_zaehlt_als_projektordner()
    {
        Directory.CreateDirectory(Path.Combine(_wurzel, "Projektdateien"));

        var ordner = WebGisBerichtAblage.Ordner(Path.Combine(_wurzel, "Projektdateien", "projekt.json"));

        Assert.Equal(Path.Combine(_wurzel, "__WebGIS_Export"), ordner);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Ohne_projektpfad_gibt_es_keinen_ordner(string? projektPfad)
        => Assert.Null(WebGisBerichtAblage.Ordner(projektPfad));

    [Fact]
    public void Bericht_und_log_landen_im_ordner()
    {
        var ordner = WebGisBerichtAblage.Ordner(Path.Combine(_wurzel, "projekt.json"));

        var bericht = WebGisBerichtAblage.SchreibeBericht(ordner, "Holen-Vorschau", "Inhalt");
        WebGisBerichtAblage.HaengeAnLog(ordner, "Zeile 1");
        WebGisBerichtAblage.HaengeAnLog(ordner, "Zeile 2");

        Assert.NotNull(bericht);
        Assert.StartsWith("WebGIS_Holen-Vorschau_", Path.GetFileName(bericht));
        Assert.Equal("Inhalt", File.ReadAllText(bericht!));
        Assert.Equal(new[] { "Zeile 1", "Zeile 2" }, File.ReadAllLines(Path.Combine(ordner!, "WebGIS_Log.txt")));
    }

    [Fact]
    public void Ohne_ordner_wird_nichts_geschrieben()
    {
        Assert.Null(WebGisBerichtAblage.SchreibeBericht(null, "Vorschau", "Inhalt"));
        WebGisBerichtAblage.HaengeAnLog(null, "Zeile"); // darf nicht werfen
    }

    [JunctionFact]
    public void Ein_verknuepfter_berichtsordner_wird_nicht_beschrieben()
    {
        var projekt = Path.Combine(_wurzel, "projekt");
        var fremd = Path.Combine(_wurzel, "fremd");
        Directory.CreateDirectory(projekt);
        Directory.CreateDirectory(fremd);
        JunctionTestSupport.CreateDirectoryLink(Path.Combine(projekt, "__WebGIS_Export"), fremd);

        var ordner = WebGisBerichtAblage.Ordner(Path.Combine(projekt, "projekt.json"));
        WebGisBerichtAblage.HaengeAnLog(ordner, "Zeile");
        WebGisBerichtAblage.SchreibeBericht(ordner, "Vorschau", "Inhalt");

        Assert.Null(ordner);
        Assert.Empty(Directory.EnumerateFileSystemEntries(fremd));
    }
}
