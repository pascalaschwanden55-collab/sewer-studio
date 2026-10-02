using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Infrastructure.Tests.Backup;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Common;

/// <summary>
/// Gemeinsame Verknuepfungspruefung (Deepscan 02.10.2026, A5): Tabellentests fuer alle vier
/// Regelpunkte mit eingespielten Dateiattributen, dazu ein Test mit echter Junction.
/// </summary>
public sealed class VerknuepfungsSchutzTests
{
    private const string Wurzel = @"C:\vs\wurzel";
    private const string Tief = @"C:\vs\wurzel\a\b.txt";

    private static Func<string, FileAttributes?> Attribute(params (string Pfad, object Wert)[] eintraege)
    {
        var karte = eintraege.ToDictionary(e => e.Pfad, e => e.Wert, StringComparer.OrdinalIgnoreCase);
        return pfad => karte.TryGetValue(pfad, out var wert)
            ? wert switch
            {
                FileAttributes attribute => attribute,
                Exception fehler => throw fehler,
                _ => throw new InvalidOperationException()
            }
            : FileAttributes.Directory;
    }

    private static readonly VerknuepfungsRegel Streng = new() { WurzelEinschliessen = true, OberhalbPruefen = true, BeiFehlerSperren = true, FehlendErlaubt = false };

    [Fact]
    public void Eintrag_ohne_Verknuepfung_ist_sicher()
        => Assert.True(VerknuepfungsSchutz.PruefeEintrag(Tief, Streng, Attribute()).IstSicher);

    [Fact]
    public void Eintrag_mit_Verknuepfung_wird_gemeldet()
    {
        var befund = VerknuepfungsSchutz.PruefeEintrag(Tief, Streng, Attribute((Tief, FileAttributes.ReparsePoint)));

        Assert.Equal(VerknuepfungsBefund.Verknuepfung, befund.Befund);
        Assert.Equal(Tief, befund.Pfad);
    }

    [Theory]
    [InlineData(true, VerknuepfungsBefund.Sicher)]
    [InlineData(false, VerknuepfungsBefund.Fehlt)]
    public void Fehlender_Eintrag_folgt_der_Regel(bool fehlendErlaubt, VerknuepfungsBefund erwartet)
    {
        var regel = Streng with { FehlendErlaubt = fehlendErlaubt };
        var befund = VerknuepfungsSchutz.PruefeEintrag(Tief, regel, Attribute((Tief, new FileNotFoundException("fehlt"))));

        Assert.Equal(erwartet, befund.Befund);
        if (!fehlendErlaubt)
            Assert.IsType<FileNotFoundException>(befund.Fehler);
    }

    [Theory]
    [InlineData(true, VerknuepfungsBefund.Sicher)]
    [InlineData(false, VerknuepfungsBefund.Fehlt)]
    public void Eingespielter_Leser_meldet_einen_fehlenden_Eintrag_mit_null(bool fehlendErlaubt, VerknuepfungsBefund erwartet)
        => Assert.Equal(erwartet, VerknuepfungsSchutz.PruefeEintrag(Tief, Streng with { FehlendErlaubt = fehlendErlaubt }, _ => null).Befund);

    [Theory]
    [InlineData(true, VerknuepfungsBefund.NichtPruefbar)]
    [InlineData(false, VerknuepfungsBefund.Sicher)]
    public void Unlesbarer_Eintrag_folgt_der_Regel(bool beiFehlerSperren, VerknuepfungsBefund erwartet)
    {
        var regel = Streng with { BeiFehlerSperren = beiFehlerSperren };
        var befund = VerknuepfungsSchutz.PruefeEintrag(Tief, regel, Attribute((Tief, new UnauthorizedAccessException("gesperrt"))));

        Assert.Equal(erwartet, befund.Befund);
        if (beiFehlerSperren)
            Assert.IsType<UnauthorizedAccessException>(befund.Fehler);
    }

    [Theory]
    [InlineData(true, VerknuepfungsBefund.Verknuepfung)]
    [InlineData(false, VerknuepfungsBefund.Sicher)]
    public void Kette_prueft_die_Wurzel_nur_wenn_eingeschlossen(bool wurzelEinschliessen, VerknuepfungsBefund erwartet)
    {
        var regel = Streng with { WurzelEinschliessen = wurzelEinschliessen, OberhalbPruefen = false };

        var befund = VerknuepfungsSchutz.PruefeKette(Wurzel, Tief, regel, Attribute((Wurzel, FileAttributes.ReparsePoint)));

        Assert.Equal(erwartet, befund.Befund);
    }

    [Theory]
    [InlineData(true, VerknuepfungsBefund.Verknuepfung)]
    [InlineData(false, VerknuepfungsBefund.Sicher)]
    public void Kette_prueft_oberhalb_der_Wurzel_nur_wenn_verlangt(bool oberhalbPruefen, VerknuepfungsBefund erwartet)
    {
        var regel = Streng with { OberhalbPruefen = oberhalbPruefen };

        var befund = VerknuepfungsSchutz.PruefeKette(Wurzel, Tief, regel, Attribute((@"C:\vs", FileAttributes.ReparsePoint)));

        Assert.Equal(erwartet, befund.Befund);
    }

    [Fact]
    public void Kette_meldet_das_unterste_verknuepfte_Glied_unter_der_Wurzel()
    {
        var befund = VerknuepfungsSchutz.PruefeKette(
            Wurzel, Tief, VerknuepfungsRegel.Spiegel,
            Attribute((@"C:\vs\wurzel\a", FileAttributes.ReparsePoint | FileAttributes.Directory)));

        Assert.Equal(VerknuepfungsBefund.Verknuepfung, befund.Befund);
        Assert.Equal(@"C:\vs\wurzel\a", befund.Pfad);
    }

    [Fact]
    public void Kette_ausserhalb_der_Wurzel_prueft_alle_Vorfahren_und_meldet_ausserhalb()
    {
        Assert.Equal(VerknuepfungsBefund.Ausserhalb,
            VerknuepfungsSchutz.PruefeKette(Wurzel, @"C:\anderswo\x.txt", VerknuepfungsRegel.Spiegel, Attribute()).Befund);
        Assert.Equal(VerknuepfungsBefund.Verknuepfung,
            VerknuepfungsSchutz.PruefeKette(Wurzel, @"C:\anderswo\x.txt", VerknuepfungsRegel.Spiegel,
                Attribute((@"C:\anderswo", FileAttributes.ReparsePoint))).Befund);
    }

    [Fact]
    public void Kette_mit_Pfad_gleich_Wurzel_prueft_nur_nach_Regel()
    {
        var attribute = Attribute((Wurzel, FileAttributes.ReparsePoint));

        Assert.True(VerknuepfungsSchutz.PruefeKette(Wurzel, Wurzel, VerknuepfungsRegel.Spiegel, attribute).IstSicher);
        Assert.False(VerknuepfungsSchutz.PruefeKette(Wurzel, Wurzel, VerknuepfungsRegel.Streng, attribute).IstSicher);
    }

    [Fact]
    public void Benannte_Regeln_halten_die_bisherigen_Unterschiede_fest()
    {
        Assert.Equal(new VerknuepfungsRegel { WurzelEinschliessen = false, OberhalbPruefen = false, BeiFehlerSperren = false, FehlendErlaubt = true }, VerknuepfungsRegel.Spiegel);
        Assert.Equal(new VerknuepfungsRegel { WurzelEinschliessen = true, OberhalbPruefen = true, BeiFehlerSperren = true, FehlendErlaubt = true }, VerknuepfungsRegel.ProjektSchreibgrenze);
        Assert.Equal(new VerknuepfungsRegel { WurzelEinschliessen = true, OberhalbPruefen = false, BeiFehlerSperren = true, FehlendErlaubt = false }, VerknuepfungsRegel.Streng);
        Assert.Equal(new VerknuepfungsRegel { WurzelEinschliessen = false, OberhalbPruefen = false, BeiFehlerSperren = true, FehlendErlaubt = true }, VerknuepfungsRegel.GanzerPfad);
    }

    [Fact]
    public void Pfad_ab_Laufwerk_prueft_jeden_Vorfahren_ausser_dem_Laufwerk()
    {
        Assert.Equal(VerknuepfungsBefund.Verknuepfung,
            VerknuepfungsSchutz.PruefePfadAbLaufwerk(Tief, VerknuepfungsRegel.GanzerPfad, Attribute((@"C:\vs", FileAttributes.ReparsePoint))).Befund);
        Assert.True(VerknuepfungsSchutz.PruefePfadAbLaufwerk(Tief, VerknuepfungsRegel.GanzerPfad, Attribute((@"C:\", FileAttributes.ReparsePoint))).IstSicher);
    }

    [JunctionFact]
    public void Kette_erkennt_eine_echte_Junction_unter_der_Wurzel()
    {
        var wurzel = Path.Combine(Path.GetTempPath(), "verknuepfungsschutz_" + Guid.NewGuid().ToString("N"));
        var ziel = Path.Combine(wurzel, "ziel");
        var link = Path.Combine(wurzel, "link");
        Directory.CreateDirectory(ziel);
        try
        {
            JunctionTestSupport.CreateDirectoryLink(link, ziel);
            File.WriteAllText(Path.Combine(ziel, "datei.txt"), "x");

            var befund = VerknuepfungsSchutz.PruefeKette(wurzel, Path.Combine(link, "datei.txt"), VerknuepfungsRegel.Streng);

            Assert.Equal(VerknuepfungsBefund.Verknuepfung, befund.Befund);
            Assert.Equal(link, befund.Pfad);
            Assert.True(VerknuepfungsSchutz.PruefeKette(wurzel, Path.Combine(ziel, "datei.txt"), VerknuepfungsRegel.Streng).IstSicher);
        }
        finally
        {
            if (Directory.Exists(link))
                Directory.Delete(link);
            Directory.Delete(wurzel, recursive: true);
        }
    }
}
