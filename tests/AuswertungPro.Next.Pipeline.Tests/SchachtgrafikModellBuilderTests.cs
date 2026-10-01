using AuswertungPro.Next.Application.Lookup;
using AuswertungPro.Next.Application.Reports;
using AuswertungPro.Next.Application.Xtf;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Schachtgrafik Stammkarte: das Modell aus Datensatz, Haltungen, Lage und Koten. Alle Zahlen
/// stammen vom echten Schacht 80409 (siehe <see cref="SchachtgrafikBeispiel"/>).
/// </summary>
public sealed class SchachtgrafikModellBuilderTests
{
    [Fact]
    public void Anschluesse_werden_mit_Haltung_Richtung_und_Kote_verbunden()
    {
        var modell = SchachtgrafikBeispiel.Modell80409();

        Assert.Equal(4, modell.Anschluesse.Count);

        var a1 = modell.Anschluesse.Single(a => a.Nr == 1);
        Assert.True(a1.IstAuslauf);
        Assert.Equal("A1", a1.Kennung);
        Assert.Equal("80409-80538", a1.Haltungsname);
        Assert.Equal(250, a1.DnMm);
        Assert.Equal(3.45m, a1.TiefeM);
        Assert.Equal("Protokoll", a1.TiefeQuelle);
        Assert.Equal(274.6, a1.AzimutGrad!.Value, 1);
        Assert.Equal(495.140m, a1.KoteM);
        Assert.True(a1.ImProjekt);

        var e2 = modell.Anschluesse.Single(a => a.Nr == 2);
        Assert.Equal("E1", e2.Kennung); // Tabellenzeile 2 ist der erste Einlauf
        Assert.Equal("80547-80409", e2.Haltungsname);
        Assert.Equal(28.4, e2.AzimutGrad!.Value, 1);
        Assert.Equal(495.160m, e2.KoteM);

        var e3 = modell.Anschluesse.Single(a => a.Nr == 3);
        Assert.Equal("80467-80409", e3.Haltungsname);
        Assert.Equal(100, e3.DnMm);
        Assert.Equal(0.60m, e3.TiefeM);
        Assert.Null(e3.KoteM);

        var e4 = modell.Anschluesse.Single(a => a.Nr == 4);
        Assert.Null(e4.Haltungsname);
        Assert.False(e4.ImProjekt);
        Assert.Null(e4.AzimutGrad);
        Assert.Equal("E3", e4.Kennung); // Tabellenzeile 4 ist der dritte Einlauf — wie in der Skizze
        Assert.Contains(modell.Hinweise, h => h.Contains("E3", StringComparison.Ordinal) && h.Contains("nicht im Projekt", StringComparison.Ordinal));
        Assert.Contains("Richtung nicht erfasst: E3", modell.Hinweise);

        Assert.Same(a1, modell.Hauptauslauf);
        Assert.True(modell.HatRichtungen);
        Assert.Equal("Mischabwasser", modell.Nutzungsart);
        Assert.True(modell.SteigeisenVorhanden);
        Assert.Equal(660, modell.DeckelDurchmesserMm);
    }

    [Fact]
    public void Schaeden_werden_von_oben_nach_unten_nummeriert_und_die_Bemerkung_haengt_am_Anschluss()
    {
        var modell = SchachtgrafikBeispiel.Modell80409();

        Assert.Equal(5, modell.Schaeden.Count);
        Assert.Equal(
            new[] { SchachtBauteil.Konus, SchachtBauteil.Anschluss, SchachtBauteil.Bankett, SchachtBauteil.Bankett, SchachtBauteil.Durchlaufrinne },
            modell.Schaeden.Select(s => s.Bauteil).ToArray());
        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, modell.Schaeden.Select(s => s.Nr).ToArray());

        // «Einlauf 3 Ausgebrochen» meint den dritten Einlauf (Skizze E3) = Tabellenzeile 4,
        // nicht die Tabellennummer 3.
        var bemerkung = modell.Schaeden.Single(s => s.Bauteil == SchachtBauteil.Anschluss);
        Assert.Equal(4, bemerkung.AnschlussNr);
        Assert.StartsWith("E3:", bemerkung.Tooltip, StringComparison.Ordinal);
        Assert.Equal("break", bemerkung.Kategorie);
        Assert.Contains("Bemerkung", bemerkung.Tooltip, StringComparison.Ordinal);

        Assert.Equal("incrustation", modell.Schaeden[0].Kategorie);
        Assert.Equal("deposit", modell.Schaeden[3].Kategorie);
    }

    [Fact]
    public void Eine_Bemerkung_zu_einem_Anschluss_den_es_nicht_gibt_erzeugt_keinen_Schaden()
    {
        var schacht = SchachtgrafikBeispiel.Schacht80409();
        schacht.SetFieldValue("Bemerkungen", "Einlauf 7 Ausgebrochen", FieldSource.Manual, true);

        var modell = SchachtgrafikModellBuilder.Baue(schacht, SchachtgrafikBeispiel.Haltungen80409(), null, null, "#006E9C");

        Assert.Equal(4, modell.Schaeden.Count);
        Assert.DoesNotContain(modell.Schaeden, s => s.Bauteil == SchachtBauteil.Anschluss);
    }

    [Fact]
    public void Ohne_Tabelle_werden_die_Haltungen_zu_Anschluessen_mit_dem_Auslauf_zuerst()
    {
        var modell = SchachtgrafikBeispiel.Modell80409(mitTabelle: false);

        Assert.Equal(3, modell.Anschluesse.Count);
        Assert.Equal(new[] { "A1", "E1", "E2" }, modell.Anschluesse.Select(a => a.Kennung).ToArray());
        Assert.Equal("80409-80538", modell.Anschluesse[0].Haltungsname);
        Assert.Equal("80467-80409", modell.Anschluesse[1].Haltungsname);
        Assert.Equal("80547-80409", modell.Anschluesse[2].Haltungsname);

        // Ohne Tabellentiefe ergibt Deckelkote minus Rohrsohlenkote die Tiefe — als Katasterwert.
        Assert.Equal(3.480m, modell.Anschluesse[0].TiefeM);
        Assert.Equal("Kataster", modell.Anschluesse[0].TiefeQuelle);
        Assert.Null(modell.Anschluesse[1].TiefeM);
        Assert.True(modell.Anschluesse.All(a => a.ImProjekt));
    }

    [Fact]
    public void Die_Tiefe_kommt_aus_dem_Kataster_wenn_das_Feld_leer_ist()
    {
        var modell = SchachtgrafikBeispiel.Modell80409(mitTiefe: false);

        Assert.Equal(3.470m, modell.TiefeM);
        Assert.Equal("Kataster", modell.TiefeQuelle);
        Assert.DoesNotContain(modell.Hinweise, h => h.StartsWith("Tiefe nicht erfasst", StringComparison.Ordinal));
    }

    [Fact]
    public void Ein_Widerspruch_zwischen_Protokoll_und_Kataster_steht_in_den_Hinweisen()
    {
        var zusatz = new SchachtgrafikZusatz(
            null,
            new SchachtKoten(498.700m, 495.150m, new Dictionary<string, decimal>()));

        var modell = SchachtgrafikModellBuilder.Baue(SchachtgrafikBeispiel.Schacht80409(), null, null, zusatz, "#006E9C");

        Assert.Equal(3.45m, modell.TiefeM);
        Assert.Equal("Protokoll", modell.TiefeQuelle);
        Assert.Contains(modell.Hinweise, h => h.StartsWith("Kataster: Tiefe 3.55 m", StringComparison.Ordinal));
    }

    [Fact]
    public void Die_Kennungen_zaehlen_je_Typ_wie_die_Skizze_des_Inspekteurs()
    {
        var modell = SchachtgrafikBeispiel.Modell80409();

        // Tabelle: 1 Auslauf, 2 Einlauf, 3 Einlauf, 4 Einlauf -> A1, E1, E2, E3 (Skizze: 74 von 74 Uri-PDFs).
        Assert.Equal(new[] { "A1", "E1", "E2", "E3" }, modell.Anschluesse.Select(a => a.Kennung).ToArray());
        Assert.Equal(new[] { 1, 2, 3, 4 }, modell.Anschluesse.Select(a => a.Nr).ToArray());
    }

    [Fact]
    public void Bei_gleichem_Durchmesser_trennt_die_Tiefe_aus_dem_Kataster_die_Haltungen()
    {
        // Zwei Einlaeufe DN 150: einer bei 0.60 m (Hausanschluss), einer bei 3.38 m (Hauptleitung).
        var schacht = new SchachtRecord();
        schacht.SetFieldValue("Schachtnummer", "500", FieldSource.Manual, false);
        schacht.SetzeAnschluesse(
        [
            new SchachtAnschluss { Nr = 1, Art = "Auslauf", DnMm = 250, TiefeM = 3.45m },
            new SchachtAnschluss { Nr = 2, Art = "Einlauf", DnMm = 150, TiefeM = 0.60m },
            new SchachtAnschluss { Nr = 3, Art = "Einlauf", DnMm = 150, TiefeM = 3.38m },
        ]);
        var haltungen = new List<HaltungRecord> { Haltung("400-500", "150"), Haltung("450-500", "150"), Haltung("500-600", "250") };
        var koten = new SchachtKoten(500.00m, 496.55m, new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            ["400-500"] = 496.60m, // 3.40 m unter dem Deckel
            ["450-500"] = 499.40m, // 0.60 m unter dem Deckel
        });

        var modell = SchachtgrafikModellBuilder.Baue(schacht, haltungen, null, new SchachtgrafikZusatz(null, koten), "#006E9C");

        Assert.Equal("450-500", modell.Anschluesse.Single(a => a.Nr == 2).Haltungsname);
        Assert.Equal("400-500", modell.Anschluesse.Single(a => a.Nr == 3).Haltungsname);
        Assert.True(modell.Anschluesse.All(a => a.ImProjekt));
    }

    [Fact]
    public void Ohne_Tiefe_bleiben_zwei_gleiche_Durchmesser_unzugeordnet_statt_geraten()
    {
        var schacht = new SchachtRecord();
        schacht.SetFieldValue("Schachtnummer", "500", FieldSource.Manual, false);
        schacht.SetzeAnschluesse(
        [
            new SchachtAnschluss { Nr = 1, Art = "Einlauf", DnMm = 150, TiefeM = 0.60m },
            new SchachtAnschluss { Nr = 2, Art = "Einlauf", DnMm = 150, TiefeM = 3.38m },
        ]);
        var haltungen = new List<HaltungRecord> { Haltung("400-500", "150"), Haltung("450-500", "150") };

        var modell = SchachtgrafikModellBuilder.Baue(schacht, haltungen, null, null, "#006E9C");

        // Zwei Zeilen ohne Haltung und zwei Haltungen ohne Zeile — keine davon wird geraten.
        Assert.Equal(4, modell.Anschluesse.Count);
        Assert.Equal(2, modell.Anschluesse.Count(a => !a.ImProjekt));
    }

    [Fact]
    public void Eine_Tabellenzeile_ohne_Projekthaltung_bekommt_die_Richtung_der_Katasterleitung()
    {
        // 80792: Die vierte Zeile (DN 115 PE) ist keine Projekthaltung; die Kopie fuehrt sie als u-80792.
        var (schacht, haltungen, lage) = Schacht80792();

        var modell = SchachtgrafikModellBuilder.Baue(schacht, haltungen, null, new SchachtgrafikZusatz(lage, null), "#006E9C");

        Assert.Equal(new[] { "A1", "E1", "E2", "E3" }, modell.Anschluesse.Select(a => a.Kennung).ToArray());
        var e3 = modell.Anschluesse.Single(a => a.Nr == 4);
        Assert.Equal("u-80792", e3.Haltungsname);
        Assert.False(e3.ImProjekt);
        Assert.False(e3.NurImKataster);
        Assert.Equal(107.0, e3.AzimutGrad!.Value, 1);
        Assert.Equal(2.18m, e3.TiefeM);
        Assert.Contains(modell.Hinweise, h => h.StartsWith("E3 steht im Protokoll, aber nicht im Projekt (Kataster: u-80792)", StringComparison.Ordinal));
        Assert.DoesNotContain(modell.Hinweise, h => h.StartsWith("Richtung nicht erfasst", StringComparison.Ordinal));
        Assert.True(modell.Anschluesse.All(a => a.AzimutGrad is not null));
    }

    [Fact]
    public void Ohne_Tabelle_erscheint_eine_Katasterleitung_als_eigener_Anschluss()
    {
        var (schacht, haltungen, lage) = Schacht80792(mitTabelle: false);

        var modell = SchachtgrafikModellBuilder.Baue(schacht, haltungen, null, new SchachtgrafikZusatz(lage, null), "#006E9C");

        Assert.Equal(4, modell.Anschluesse.Count);
        var kataster = modell.Anschluesse.Single(a => a.NurImKataster);
        Assert.Equal("E3", kataster.Kennung);
        Assert.Equal("u-80792", kataster.Haltungsname);
        Assert.Equal(115, kataster.DnMm);
        Assert.False(kataster.IstAuslauf);
        Assert.Null(kataster.TiefeM);
        Assert.Contains("nur im Kataster", kataster.Beschreibung, StringComparison.Ordinal);
        Assert.Contains(modell.Hinweise, h => h.StartsWith("E3 nur im Kataster (u-80792)", StringComparison.Ordinal));
    }

    [Fact]
    public void Eine_Uhrlage_der_Tabelle_gibt_die_Richtung_wenn_die_Kopie_fehlt()
    {
        // SchachtPro (Goeschenen 8705): A1 12 Uhr, E1 4 Uhr, E2 6 Uhr, E3 7 Uhr.
        var schacht = new SchachtRecord();
        schacht.SetFieldValue("Schachtnummer", "8705", FieldSource.Manual, false);
        schacht.SetzeAnschluesse(
        [
            new SchachtAnschluss { Nr = 1, Art = "Auslauf", DnMm = 150, TiefeM = 1.28m, Uhr = "12", Quelle = "SchachtPro" },
            new SchachtAnschluss { Nr = 2, Art = "Einlauf", DnMm = 100, TiefeM = 0.67m, Uhr = "4", Quelle = "SchachtPro" },
            new SchachtAnschluss { Nr = 3, Art = "Einlauf", DnMm = 120, TiefeM = 1.20m, Uhr = "6", Quelle = "SchachtPro" },
            new SchachtAnschluss { Nr = 4, Art = "Einlauf", DnMm = 150, TiefeM = 1.22m, Uhr = "7", Quelle = "SchachtPro" },
        ]);

        var modell = SchachtgrafikModellBuilder.Baue(schacht, null, null, null, "#006E9C");

        Assert.Equal(new[] { 0d, 120d, 180d, 210d }, modell.Anschluesse.Select(a => a.UhrGrad!.Value).ToArray());
        Assert.True(modell.HatRichtungen);
        Assert.Contains("Richtungen teilweise aus der Uhrlage des Protokolls (nicht vermessen)", modell.Hinweise);
        Assert.DoesNotContain("Richtungen nicht erfasst: Grundriss schematisch", modell.Hinweise);
        Assert.Contains("7 Uhr", modell.Anschluesse[3].Beschreibung, StringComparison.Ordinal);
    }

    [Fact]
    public void Eine_Bemerkung_Anschluss_N_meint_die_Tabellennummer()
    {
        var schacht = SchachtgrafikBeispiel.Schacht80409();
        schacht.SetFieldValue("Bemerkungen", "Anschluss 3 gerissen", FieldSource.Manual, true);

        var modell = SchachtgrafikModellBuilder.Baue(schacht, SchachtgrafikBeispiel.Haltungen80409(), null, null, "#006E9C");

        var schaden = modell.Schaeden.Single(s => s.Bauteil == SchachtBauteil.Anschluss);
        Assert.Equal(3, schaden.AnschlussNr);
        Assert.StartsWith("E2:", schaden.Tooltip, StringComparison.Ordinal);
    }

    private static (SchachtRecord Schacht, List<HaltungRecord> Haltungen, SchachtLage Lage) Schacht80792(bool mitTabelle = true)
    {
        var s = new SchachtRecord();
        s.SetFieldValue("Schachtnummer", "80792", FieldSource.Pdf, false);
        s.SetFieldValue("Schachttiefe", "2.35", FieldSource.Pdf, false);
        if (mitTabelle)
        {
            s.SetzeAnschluesse(
            [
                new SchachtAnschluss { Nr = 1, Art = "Auslauf", DnMm = 300, TiefeM = 2.35m, Material = "Zement", Quelle = "PDF" },
                new SchachtAnschluss { Nr = 2, Art = "Einlauf", DnMm = 300, TiefeM = 2.33m, Material = "Zement", Quelle = "PDF" },
                new SchachtAnschluss { Nr = 3, Art = "Einlauf", DnMm = 150, TiefeM = 2.20m, Material = "Polyethylen", Quelle = "PDF" },
                new SchachtAnschluss { Nr = 4, Art = "Einlauf", DnMm = 115, TiefeM = 2.18m, Material = "Polyethylen", Quelle = "PDF" },
            ]);
        }

        var haltungen = new List<HaltungRecord> { Haltung("80808-80792", "300"), Haltung("80792-80722", "300"), Haltung("80789-80792", "150") };
        var lage = new SchachtLage(
            new XtfPunkt(2692445.021, 1192495.376),
            new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["80792-80722"] = 220.1,
                ["80808-80792"] = 40.2,
                ["80789-80792"] = 100.6,
            },
            [new SchachtLageLeitung("u-80792", 107.0, EndetImSchacht: true, 115, "Kunststoff")]);
        return (s, haltungen, lage);
    }

    private static HaltungRecord Haltung(string name, string dn)
    {
        var h = new HaltungRecord();
        h.SetFieldValue(FieldKeys.HoldingName, name, FieldSource.Manual, false);
        h.SetFieldValue(FieldKeys.NominalDiameterMm, dn, FieldSource.Manual, false);
        return h;
    }

    [Fact]
    public void Ohne_Tiefe_Masse_und_Zusatz_bleiben_die_Hinweise_ehrlich()
    {
        var schacht = new SchachtRecord();
        schacht.SetFieldValue("Schachtnummer", "S1", FieldSource.Manual, false);

        var modell = SchachtgrafikModellBuilder.Baue(schacht, null, null, null, "#006E9C");

        Assert.Null(modell.TiefeM);
        Assert.Null(modell.TiefeQuelle);
        Assert.False(modell.HatMasse);
        Assert.Null(modell.SteigeisenVorhanden);
        Assert.Empty(modell.Anschluesse);
        Assert.Empty(modell.Schaeden);
        Assert.Contains(modell.Hinweise, h => h.StartsWith("Tiefe nicht erfasst", StringComparison.Ordinal));
        Assert.Contains(modell.Hinweise, h => h.StartsWith("Innenmasse nicht erfasst", StringComparison.Ordinal));
        Assert.DoesNotContain(modell.Hinweise, h => h.Contains("Richtung", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("vorhanden", true)]
    [InlineData("Steigeisen", true)]
    [InlineData("Leiter", true)]
    [InlineData("fehlt", false)]
    [InlineData("nicht notwendig", false)]
    [InlineData("", null)]
    public void Die_Steighilfe_wird_gelesen(string wert, bool? erwartet)
    {
        var schacht = new SchachtRecord();
        schacht.SetFieldValue("Schachtnummer", "S1", FieldSource.Manual, false);
        if (wert.Length > 0)
            schacht.SetFieldValue("Steighilfe", wert, FieldSource.Manual, false);

        var modell = SchachtgrafikModellBuilder.Baue(schacht, null, null, null, "#006E9C");

        Assert.Equal(erwartet, modell.SteigeisenVorhanden);
    }

    [Fact]
    public void Die_Haltungen_werden_ueber_die_Schachtfelder_zugeordnet_wenn_sie_gefuellt_sind()
    {
        var haltungen = SchachtgrafikBeispiel.Haltungen80409();
        // Gegenbefahrung: Der Name sagt 80547-80409, die Felder sagen, 80409 sei OBEN.
        haltungen[0].SetFieldValue("Schacht_oben", "80409", FieldSource.Manual, true);
        haltungen[0].SetFieldValue("Schacht_unten", "80547", FieldSource.Manual, true);

        var modell = SchachtgrafikModellBuilder.Baue(SchachtgrafikBeispiel.Schacht80409(mitTabelle: false), haltungen, null, null, "#006E9C");

        var gegen = modell.Anschluesse.Single(a => a.Haltungsname == "80547-80409");
        Assert.True(gegen.IstAuslauf);
    }

    [Fact]
    public void Der_Modellbauer_veraendert_den_Datensatz_nicht()
    {
        var schacht = SchachtgrafikBeispiel.Schacht80409();
        var felderVorher = new Dictionary<string, string>(schacht.Fields);
        var eintraegeVorher = schacht.Protocol!.Current.Entries.Count;
        var anschluesseVorher = schacht.Anschluesse!.Count;
        var geaendert = schacht.ModifiedAtUtc;

        SchachtgrafikBeispiel.Modell80409();
        SchachtgrafikModellBuilder.Baue(schacht, SchachtgrafikBeispiel.Haltungen80409(), null, SchachtgrafikBeispiel.Zusatz80409(), "#006E9C");

        Assert.Equal(felderVorher, schacht.Fields);
        Assert.Equal(eintraegeVorher, schacht.Protocol.Current.Entries.Count);
        Assert.Equal(anschluesseVorher, schacht.Anschluesse!.Count);
        Assert.Equal(geaendert, schacht.ModifiedAtUtc);
        Assert.All(schacht.Protocol.Current.Entries, e => Assert.Null(e.CodeMeta));
    }

    [Fact]
    public void Ein_geloeschter_Eintrag_wird_nicht_gezeichnet()
    {
        var schacht = SchachtgrafikBeispiel.Schacht80409();
        schacht.Protocol!.Current.Entries[0].IsDeleted = true;

        var modell = SchachtgrafikModellBuilder.Baue(schacht, null, null, null, "#006E9C");

        Assert.DoesNotContain(modell.Schaeden, s => s.Bauteil == SchachtBauteil.Konus);
    }
}
