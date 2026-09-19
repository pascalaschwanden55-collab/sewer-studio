using AuswertungPro.Next.Infrastructure.HoldingDistribution;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Pegel-Dichtheitspruefungen an Behaeltern (Regenbecken, Referenzgefaess) sind keine
/// Haltungspruefungen. Anlass: KIT-Pruefbericht Beckenmessung RB2 Ellbogenkapelle und
/// KIT-Pruefbericht Referenzmessung vom 16.09.2026. Beide erzeugten frueher erfundene
/// Haltungen ("2005-2025" aus der Hersteller-Fusszeile, "000-100" aus der Masstabelle).
/// Die Textausschnitte stammen woertlich aus dem PdfPig-Textlauf dieser Dateien.
/// </summary>
public sealed class DichtheitBehaelterpruefungTests
{
    private const string BeckenmessungSeite1 = """
        BAUINSPEKT                                    prufen 1 dokumentieren / beraten
        Pegel-Dichtheitsprufung nach SIA190:2017/VSARLDicht:2023 (Wasser)

        Auftraggeber:               Abwasser Uri
        Bauvorhaben:                RB 2 Ellbogenkapelle
                                    Schutzenbrunnen - West
        Prufobjekt:                 6473 Silenen
                                    RB2
        Prufabschnitt:

        Priifdurchfuhrung:          Erstprufung (Grundwasserschutzzone) Behalter

        Hohe Wasserpegel:           3.600 m
        Beginn SSttigung:           16.09.202612:03:18
        Prufungsende nach:          16.09.202613:20:49
        """;

    private const string ReferenzmessungSeite1 = """
        BAUINSPEKT                              prufen 1 dokumentieren 1 beraten
        Pegel-DichtheitsprufungnachSIAI 90:2017/VSARLDicht:2023(Wasser)

        Auftraggeber:           Abwasser Uri
        Bauvorhaben:            Dichtheitsprufung
        Prufobjekt:             RB 2 Ellbogenkapelle
        StraBe:                 Schutzenbrunnen - West
        Ort:                    Refer.Gefass-KLEIN
                                Gefass mit Wasser im RB 2 instaliert.

        Priifdurchfiihrung:     Erstprufung (Grundwasserschutzzone) Behalter
        Beginn Sattigung:       16.09.2026 12:06:03
        """;

    private const string HaltungsPruefberichtSeite1 = """
        BAUINSPEKT                              prufen 1 dokumentieren 1 beraten
        Prufbericht-prufprotokoii LbOI-KanalOberdruckLuft

        Datum / Prufnummer                      2026/09/16 / 002/3119/EK
        Prufgegenstand / Haltung
        Haltungsname                            Abwasser Uri, Altdorf
        Material / DN / Lange                   40905-41500      gepruft bei 40905, 6473 Silenen
        """;

    // Anlage-Seite der Referenzmessung: hieraus entstand frueher die Haltung "000-100".
    private const string MasstabelleSeite = """
        Pegel-Dichtheitsprufung nach SIA190:2017/VSARL Dicht:2023 (Wasser)
        Berechnung der Prufobjektdaten zu Protokolldatei:

        Messwert / Eigenschaft        ReferenzmesSchacht 2   Schacht3  Schacht4
        Querschnitt oberer Schachtring -
        Durchm. oberer Schachtring [m] -
        Hohe oberer Schachtring [m] 0.000
        Querschnitt unt. Schachtring  Rechteck
        Matehal unt. Schachtring      Aluminium
        Durchm. unt. Schachtring [m]  0.200x0.200
        HOhe unterer Schachtring [m]  0. 100
        """;

    [Fact]
    public void Erkenne_Beckenmessung_LiefertPruefobjektRb2()
    {
        var treffer = BehaelterPruefungParser.Erkenne(BeckenmessungSeite1);

        Assert.NotNull(treffer);
        Assert.Equal("RB2", treffer!.Objekt);
    }

    [Fact]
    public void Erkenne_Referenzmessung_LiefertDasselbeBauwerk()
    {
        var treffer = BehaelterPruefungParser.Erkenne(ReferenzmessungSeite1);

        Assert.NotNull(treffer);
        Assert.Equal("RB2", treffer!.Objekt);
    }

    [Fact]
    public void Erkenne_HaltungsPruefbericht_IstKeineBehaelterpruefung()
    {
        Assert.Null(BehaelterPruefungParser.Erkenne(HaltungsPruefberichtSeite1));
    }

    [Fact]
    public void Erkenne_OhneLesbaresPruefobjekt_MeldetErkanntAberOhneNamen()
    {
        var ohneName = BeckenmessungSeite1
            .Replace("RB 2 Ellbogenkapelle", "Klaerbecken der Gemeinde")
            .Replace("RB2", "unbenannt");

        var treffer = BehaelterPruefungParser.Erkenne(ohneName);

        Assert.NotNull(treffer);
        Assert.Null(treffer!.Objekt);
    }

    [Fact]
    public void IsNoiseLine_HerstellerFusszeile_IstRauschen()
    {
        Assert.True(ShaftCandidateScanner.IsNoiseLine("       m ©2005-2025 MesSen Nord GmbH"));
    }

    [Fact]
    public void IsNoiseLine_MasszeileMitEinheit_IstRauschen()
    {
        Assert.True(ShaftCandidateScanner.IsNoiseLine("Hohe oberer Schachtring [m] 0.000"));
        Assert.True(ShaftCandidateScanner.IsNoiseLine("Durchm. unt. Schachtring [m]  0.200x0.200"));
    }

    [Fact]
    public void IsNoiseLine_EchteHaltungszeile_BleibtErhalten()
    {
        Assert.False(ShaftCandidateScanner.IsNoiseLine(
            "Prufgegenstand / Haltung                40859 - 14.37937"));
        Assert.False(ShaftCandidateScanner.IsNoiseLine("Oberer Schacht: 40905"));
    }

    [Fact]
    public void TryExtractFromShafts_Masstabelle_ErfindetKeineHaltung()
    {
        Assert.Null(ShaftCandidateScanner.TryExtractFromShafts(MasstabelleSeite));
    }

    [Fact]
    public void Erkenne_Beckenmessung_LiestDasPruefdatum()
    {
        var treffer = BehaelterPruefungParser.Erkenne(BeckenmessungSeite1);

        Assert.Equal(new DateTime(2026, 9, 16), treffer!.Datum);
    }

    [Fact]
    public void Erkenne_Referenzmessung_LiestDasPruefdatum()
    {
        var treffer = BehaelterPruefungParser.Erkenne(ReferenzmessungSeite1);

        Assert.Equal(new DateTime(2026, 9, 16), treffer!.Datum);
    }
}
