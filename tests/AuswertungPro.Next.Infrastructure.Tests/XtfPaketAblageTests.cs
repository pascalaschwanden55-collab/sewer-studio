using System.IO.Compression;
using AuswertungPro.Next.Infrastructure.Import.Xtf;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>Echte Dateien: anlegen, packen, verwerfen. Nichts wird ueberschrieben.</summary>
public sealed class XtfPaketAblageTests : IDisposable
{
    private readonly string _ziel = Path.Combine(Path.GetTempPath(), "SewerStudio-Paket-" + Guid.NewGuid().ToString("N"));

    public XtfPaketAblageTests() => Directory.CreateDirectory(_ziel);
    public void Dispose() { if (Directory.Exists(_ziel)) Directory.Delete(_ziel, true); }

    [Fact]
    public void Packt_beide_Ordner_und_die_Liesmich_Datei_in_eine_Zip()
    {
        var ablage = new XtfPaketAblage();
        var ort = ablage.Beginne(_ziel, "Projekt Bürglen");
        File.WriteAllText(Path.Combine(ort.AenderungenOrdner, "a.xtf"), "A");
        File.WriteAllText(Path.Combine(ort.VollstaendigOrdner, "b.xtf"), "B");

        var zip = ablage.Schliesse(ort, new("Erklärung für den Empfänger", "Bericht A", "Bericht B"));

        Assert.True(File.Exists(zip));
        Assert.Equal(Path.GetFullPath(_ziel), Path.GetFullPath(Path.GetDirectoryName(zip)!));
        Assert.Contains("Projekt Bürglen", Path.GetFileName(ort.Paketordner), StringComparison.Ordinal);
        Assert.Equal("Erklärung für den Empfänger", File.ReadAllText(Path.Combine(ort.Paketordner, XtfPaketAblage.Liesmichname)));

        using var archiv = ZipFile.OpenRead(zip);
        var eintraege = archiv.Entries.Select(e => e.FullName.Replace('\\', '/')).ToArray();
        Assert.Contains(eintraege, e => e.EndsWith("/1 Aenderungen/a.xtf", StringComparison.Ordinal));
        Assert.Contains(eintraege, e => e.EndsWith("/2 Vollstaendig/b.xtf", StringComparison.Ordinal));
        Assert.Contains(eintraege, e => e.EndsWith("/" + XtfPaketAblage.Liesmichname, StringComparison.Ordinal));
        Assert.Equal(2, eintraege.Count(e => e.EndsWith("/" + XtfPaketAblage.Berichtname, StringComparison.Ordinal)));
        Assert.Equal("Bericht A", File.ReadAllText(Path.Combine(ort.AenderungenOrdner, XtfPaketAblage.Berichtname)));
        // Der Paketordner bleibt zum Nachschauen liegen.
        Assert.True(Directory.Exists(ort.Paketordner));
    }

    [Fact]
    public void Verwirf_entfernt_nur_das_selbst_angelegte_Paket()
    {
        var ablage = new XtfPaketAblage();
        var ort = ablage.Beginne(_ziel, "P");
        File.WriteAllText(Path.Combine(ort.AenderungenOrdner, "halb.xtf"), "x");

        ablage.Verwirf(ort);
        Assert.False(Directory.Exists(ort.Paketordner));

        // Ein fremder Ordner wird nie angefasst, auch wenn er genauso heisst.
        var fremd = Path.Combine(_ziel, "fremd");
        Directory.CreateDirectory(fremd);
        new XtfPaketAblage().Verwirf(new(fremd, fremd, fremd));
        Assert.True(Directory.Exists(fremd));
    }

    [Fact]
    public void Zweites_Paket_derselben_Sekunde_bekommt_einen_freien_Namen()
    {
        var ablage = new XtfPaketAblage();
        var erstes = ablage.Beginne(_ziel, "P");
        var zweites = ablage.Beginne(_ziel, "P");
        Assert.NotEqual(erstes.Paketordner, zweites.Paketordner);
        Assert.True(Directory.Exists(erstes.Paketordner));

        File.WriteAllText(Path.Combine(erstes.AenderungenOrdner, "a.xtf"), "A");
        File.WriteAllText(Path.Combine(zweites.AenderungenOrdner, "a.xtf"), "A");
        Assert.NotEqual(ablage.Schliesse(erstes, new("1", "a", "b")), ablage.Schliesse(zweites, new("2", "a", "b")));
    }
}
