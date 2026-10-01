using System;
using System.IO;
using System.Linq;
using AuswertungPro.Next.Application.WebGis;
using AuswertungPro.Next.Infrastructure.WebGis;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.WebGis;

/// <summary>Plan WG03: Das Änderungslog verschluckt keinen Fehler und trägt die Laufkennung auf jeder Zeile.</summary>
public sealed class WebGisLaufLogTests : IDisposable
{
    private readonly string _projekt = Path.Combine(Path.GetTempPath(), $"webgis-lauflog-{Guid.NewGuid():N}");

    private string Ordner()
    {
        Directory.CreateDirectory(_projekt);
        return WebGisBerichtAblage.Ordner(Path.Combine(_projekt, "projekt.json"))
               ?? throw new InvalidOperationException("Ablageordner fehlt.");
    }

    [Fact]
    public void Jede_zeile_traegt_dieselbe_laufkennung()
    {
        var ordner = Ordner();
        var log = new WebGisLaufLog(ordner, "L42");

        log.Schreibe("Start");
        log.Schreibe("Objekt A" + Environment.NewLine + "Objekt B");
        log.Schreibe("Abschluss");

        var zeilen = File.ReadAllLines(Path.Combine(ordner, WebGisBerichtAblage.LogDatei));
        Assert.Equal(4, zeilen.Length);
        Assert.All(zeilen, z => Assert.StartsWith("Lauf L42 | ", z));
    }

    [Fact]
    public void Gesperrte_logdatei_wirft_und_versuche_liefert_false()
    {
        var ordner = Ordner();
        var datei = Path.Combine(ordner, WebGisBerichtAblage.LogDatei);
        var log = new WebGisLaufLog(ordner);

        using (new FileStream(datei, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
        {
            Assert.Throws<WebGisLogException>(() => log.Schreibe("Start"));
            Assert.False(log.VersucheSchreibe("Start"));
        }

        Assert.True(log.VersucheSchreibe("Start"));
        Assert.Single(File.ReadAllLines(datei).Where(z => z.EndsWith("Start", StringComparison.Ordinal)));
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_projekt)) Directory.Delete(_projekt, recursive: true); }
        catch (IOException) { }
    }
}
