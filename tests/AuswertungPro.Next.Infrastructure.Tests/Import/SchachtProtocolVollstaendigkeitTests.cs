using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import.Pdf;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

/// <summary>
/// Haelt die am 19.09.2026 gemessenen Fehler fest. Gemessen wurden 264 SchachtPro-Protokolle
/// aus Goeschenen; drei unabhaengige Zaehlungen (Kennungsspalte, Datenzeilen, Skizzenlegende)
/// ergaben den Sollwert. Mit einer ungeeigneten pdftotext-Fassung fehlten 63 von 704
/// Anschluessen, und gelesene Anschluesse trugen fremde Tiefen.
///
/// Die Vorlagen sind der echte Seitentext beider Protokolle, gelesen mit der geprueften
/// Fassung; nur der Name des Ausfuehrenden ist ersetzt.
/// </summary>
public sealed class SchachtProtocolVollstaendigkeitTests
{
    private static string Vorlage(string datei)
        => File.ReadAllText(Path.Combine(
            TestRepoPaths.RepoRoot(), "tests", "Fixtures", "Schachtprotokolle", datei));

    private static IReadOnlyList<SchachtAnschluss> Anschluesse(string datei)
        => SchachtProtocolZusatzParser.Parse(Vorlage(datei)).Anschluesse;

    [Fact]
    public void Schacht_10039_liefert_alle_vier_Anschluesse()
    {
        // Mit Xpdf 4.00 waren es zwei: E2 und E3 fielen aus, weil ihre Zeilen zerrissen wurden.
        var anschluesse = Anschluesse("10039_schachtpro_poppler_seite1.txt");

        Assert.Equal(4, anschluesse.Count);
        Assert.Equal(new[] { "Auslauf", "Einlauf", "Einlauf", "Einlauf" }, anschluesse.Select(a => a.Art).ToArray());
        Assert.Equal(new[] { "12", "2", "6", "7" }, anschluesse.Select(a => a.Uhr).ToArray());
    }

    [Fact]
    public void Jede_Tiefe_gehoert_zu_ihrem_eigenen_Anschluss()
    {
        // Der eigentliche Schaden der alten Fassung: Einlauf 1 bekam Tiefe und Durchmesser
        // von Einlauf 2 (0,52 statt 0,39). Ein falscher Messwert faellt spaeter niemandem auf.
        var anschluesse = Anschluesse("10039_schachtpro_poppler_seite1.txt");

        Assert.Equal(new decimal[] { 1.06m, 0.39m, 0.52m, 0.97m }, anschluesse.Select(a => a.TiefeM!.Value).ToArray());
        Assert.Equal(new[] { 200, 150, 120, 200 }, anschluesse.Select(a => a.DnMm!.Value).ToArray());
        Assert.Equal(
            new[] { "Polyvinylchlorid (PVC)", "Polyethylen (PE)", "Normalbeton (NB)", "Polyvinylchlorid (PVC)" },
            anschluesse.Select(a => a.Material).ToArray());
    }

    [Fact]
    public void Der_Zustand_steht_an_seinem_Anschluss()
    {
        var anschluesse = Anschluesse("10039_schachtpro_poppler_seite1.txt");

        Assert.Equal(
            new[] { "Mangelhaft eingebunden", "Mangelhaft eingebunden", "in Ordnung", "Mangelhaft eingebunden" },
            anschluesse.Select(a => a.Zustand).ToArray());
        Assert.All(anschluesse, a => Assert.False(a.ZustandUnvollstaendig));
    }

    [Fact]
    public void Ein_vom_Protokoll_gekuerzter_Zustand_gilt_als_unvollstaendig()
    {
        // Schacht 10091: «Ausgebrochen • Mangelha…» — SchachtPro schneidet die zu lange Zelle
        // im PDF ab. Der Rest steht nirgends im Dokument und darf nicht vollstaendig wirken.
        var anschluesse = Anschluesse("10091_schachtpro_poppler_seite1.txt");

        var gekuerzte = anschluesse.Where(a => a.ZustandUnvollstaendig).ToList();
        Assert.Equal(2, gekuerzte.Count);
        Assert.All(gekuerzte, a => Assert.StartsWith("Ausgebrochen", a.Zustand!, StringComparison.Ordinal));
        Assert.All(gekuerzte, a => Assert.DoesNotContain('…', a.Zustand!));

        var vollstaendig = anschluesse.Single(a => a.Art == "Auslauf");
        Assert.Equal("Mangelhaft eingebunden", vollstaendig.Zustand);
        Assert.False(vollstaendig.ZustandUnvollstaendig);
    }

    [Fact]
    public void Auch_die_Kurzform_mit_weiteren_Befunden_gilt_als_unvollstaendig()
    {
        // Woertlich aus dem Bestand: Statt der Ellipse nennt SchachtPro hier nur die Zahl
        // der weggelassenen Befunde. Die Zahl bleibt stehen, der Text gilt als unvollstaendig.
        const string text = """
            SCHACHTPRO
            Anschluss   Uhrzeit   Tiefe     Durchmesser        Typ              Medium            Material                     Zustand

            A1          12        1.72      200 mm             Auslauf          Schmutzabwasser   Polyvinylchlorid (PVC)       Ausgebrochen • Breite Fuge +3
            E1          6         1.29      150 mm             Einlauf          Schmutzabwasser   Polyvinylchlorid (PVC)       in Ordnung
            """;

        var anschluesse = SchachtProtocolZusatzParser.Parse(text).Anschluesse;

        Assert.Equal(2, anschluesse.Count);
        Assert.True(anschluesse[0].ZustandUnvollstaendig);
        Assert.Equal("Ausgebrochen • Breite Fuge +3", anschluesse[0].Zustand);
        Assert.False(anschluesse[1].ZustandUnvollstaendig);
        Assert.Equal("in Ordnung", anschluesse[1].Zustand);
    }

    [Fact]
    public void Die_LV95_Koordinaten_werden_uebernommen()
    {
        var zusatz = SchachtProtocolZusatzParser.Parse(Vorlage("10039_schachtpro_poppler_seite1.txt"));

        Assert.Equal("2687939.868", zusatz.KoordinateOst);
        Assert.Equal("1169144.662", zusatz.KoordinateNord);
    }

    [Theory]
    // Beide Dezimaltrenner kommen im Bestand vor.
    [InlineData("Koordinaten (LV95): E 2687825,168767 / N 1169222,842858", "2687825.169", "1169222.843")]
    [InlineData("Koordinaten (LV95): E 2687994.940430 / N 1168947.437747", "2687994.94", "1168947.438")]
    public void Punkt_und_Komma_werden_gleich_gelesen(string zeile, string ost, string nord)
    {
        var (gelesenOst, gelesenNord) = SchachtProtocolZusatzParser.Koordinaten(zeile);

        Assert.Equal(ost, gelesenOst);
        Assert.Equal(nord, gelesenNord);
    }

    [Theory]
    // Ausserhalb der Schweizer LV95-Ausdehnung, vertauscht, halb oder gar nicht vorhanden.
    [InlineData("Koordinaten (LV95): E 1169144.662031 / N 2687939.868408")]
    [InlineData("Koordinaten (LV95): E 9999999.0 / N 1169144.6")]
    [InlineData("Koordinaten (LV95): E 2687939.868408")]
    [InlineData("Schachtprotokoll Schacht Nr. 10039")]
    public void Eine_unplausible_Lage_wird_nicht_uebernommen(string zeile)
    {
        var (ost, nord) = SchachtProtocolZusatzParser.Koordinaten(zeile);

        Assert.Null(ost);
        Assert.Null(nord);
    }

    [Fact]
    public void Koordinaten_ueberschreiben_keinen_Handwert_und_keinen_Katasterwert()
    {
        var schacht = new SchachtRecord();
        schacht.SetFieldValue("Koordinate_East", "2600000", FieldSource.Manual, userEdited: true);
        schacht.SetFieldValue("Koordinate_North", "1200000", FieldSource.Kataster, userEdited: false);

        var zusatz = SchachtProtocolZusatzParser.Parse(Vorlage("10039_schachtpro_poppler_seite1.txt"));
        SchachtProtocolApplier.ApplyZusatz(schacht, zusatz, rebuildFromProtocol: true, onlyMissing: false);

        Assert.Equal("2600000", schacht.GetFieldValue("Koordinate_East"));
        Assert.Equal("1200000", schacht.GetFieldValue("Koordinate_North"));
    }

    [Fact]
    public void In_ein_leeres_Feld_werden_die_Koordinaten_geschrieben()
    {
        var schacht = new SchachtRecord();
        var zusatz = SchachtProtocolZusatzParser.Parse(Vorlage("10039_schachtpro_poppler_seite1.txt"));

        SchachtProtocolApplier.ApplyZusatz(schacht, zusatz, rebuildFromProtocol: false, onlyMissing: true);

        Assert.Equal("2687939.868", schacht.GetFieldValue("Koordinate_East"));
        Assert.Equal("1169144.662", schacht.GetFieldValue("Koordinate_North"));
        Assert.NotNull(schacht.Anschluesse);
        Assert.Equal(4, schacht.Anschluesse!.Count);
        Assert.Equal("in Ordnung", schacht.Anschluesse![2].Zustand);
    }

    [Fact]
    public void Das_Uri_Formular_bleibt_ohne_Koordinaten_und_ohne_Zustand()
    {
        // Das Uri-Kaestchenformular nennt beides nicht. Nichts wird erfunden.
        var zusatz = SchachtProtocolZusatzParser.Parse(Vorlage("80409_seite1_layout.txt"));

        Assert.Null(zusatz.KoordinateOst);
        Assert.Null(zusatz.KoordinateNord);
        Assert.All(zusatz.Anschluesse, a => Assert.Null(a.Zustand));
    }
}
