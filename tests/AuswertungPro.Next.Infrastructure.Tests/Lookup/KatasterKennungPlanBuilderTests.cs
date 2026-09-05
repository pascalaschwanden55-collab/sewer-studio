using AuswertungPro.Next.Application.Lookup;
using AuswertungPro.Next.Application.UseCases;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Tests.Lookup;

/// <summary>
/// Die Planung von "Katasterkennungen ergaenzen": nur eindeutige Treffer, nie
/// ueberschreiben, Gegenrichtung nur bei Haltungen.
/// </summary>
public sealed class KatasterKennungPlanBuilderTests
{
    private const string HaltungId = "ch23h1a4uL3A2Sjp";
    private const string KnotenId = "ch23h1a4ftlGdbHU";

    [Fact]
    public void Eine_Haltung_bekommt_ihre_Kennungen_bei_direktem_Treffer()
    {
        var bestand = Bestand(BauteilArt.Haltung, HaltungKennung("78998-79002"));

        var plan = KatasterKennungPlanBuilder.BaueFuerHaltungen([Haltung("78998-79002")], bestand);

        var position = Assert.Single(plan.Positionen);
        Assert.Equal("78998-79002", position.Bauteil);
        Assert.Equal(HaltungId, position.Kennung.Haltung);
        Assert.False(position.Gedreht);
        Assert.Equal(1, plan.GepruefteBauteile);
        Assert.Empty(plan.Hinweise);
    }

    // Bei einer Gegenbefahrung steht im Projekt der untere Schacht vorn. Der Kataster
    // kennt nur die eine Richtung — der Treffer ueber den gedrehten Namen zaehlt,
    // wird aber als gedreht markiert.
    [Fact]
    public void Eine_Haltung_wird_ueber_die_Gegenrichtung_gefunden()
    {
        var bestand = Bestand(BauteilArt.Haltung, HaltungKennung("78998-79002"));

        var plan = KatasterKennungPlanBuilder.BaueFuerHaltungen([Haltung("79002-78998")], bestand);

        var position = Assert.Single(plan.Positionen);
        Assert.True(position.Gedreht);
        Assert.Equal(1, plan.Gedreht);
    }

    [Fact]
    public void Der_direkte_Treffer_hat_Vorrang_vor_der_Gegenrichtung()
    {
        var bestand = Bestand(BauteilArt.Haltung,
            HaltungKennung("78998-79002", "ch23h1a4AAAAAAAA"),
            HaltungKennung("79002-78998", "ch23h1a4BBBBBBBB"));

        var plan = KatasterKennungPlanBuilder.BaueFuerHaltungen([Haltung("79002-78998")], bestand);

        var position = Assert.Single(plan.Positionen);
        Assert.Equal("ch23h1a4BBBBBBBB", position.Kennung.Haltung);
        Assert.False(position.Gedreht);
    }

    [Fact]
    public void Ein_mehrdeutiger_Name_bekommt_nichts()
    {
        var bestand = new KatasterKennungBestand(
            BauteilArt.Haltung,
            new Dictionary<string, KatasterKennung>(StringComparer.OrdinalIgnoreCase),
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "84102-84102" },
            3, "GEONIS-Kopie 2024-12");

        var plan = KatasterKennungPlanBuilder.BaueFuerHaltungen([Haltung("84102-84102")], bestand);

        Assert.Empty(plan.Positionen);
        Assert.Equal(1, plan.Anzahl(KatasterKennungGrund.Mehrdeutig));
    }

    [Fact]
    public void Ein_unbekannter_Name_wird_gemeldet()
    {
        var bestand = Bestand(BauteilArt.Haltung, HaltungKennung("78998-79002"));

        var plan = KatasterKennungPlanBuilder.BaueFuerHaltungen([Haltung("1-2")], bestand);

        Assert.Empty(plan.Positionen);
        Assert.Equal(1, plan.Anzahl(KatasterKennungGrund.NichtGefunden));
    }

    // Eine schon vorhandene Kennung kann aus einem GEONIS-Export stammen, der neuer ist
    // als die Kopie. Sie bleibt — gleich oder abweichend.
    [Fact]
    public void Eine_vorhandene_Kennung_wird_nie_ersetzt()
    {
        var bestand = Bestand(BauteilArt.Haltung, HaltungKennung("78998-79002"));
        var gleich = Haltung("78998-79002");
        gleich.Geonis = new GeonisKennungen { Haltung = HaltungId };
        gleich.SetFieldValue(FieldKeys.GeonisId, HaltungId, FieldSource.Kataster, false);
        var anders = Haltung("78998-79002");
        anders.Geonis = new GeonisKennungen { Haltung = "ch23h1a4XXXXXXXX" };

        var plan = KatasterKennungPlanBuilder.BaueFuerHaltungen([gleich, anders], bestand);

        Assert.Empty(plan.Positionen);
        Assert.Equal(1, plan.Anzahl(KatasterKennungGrund.BereitsVorhanden));
        Assert.Equal(1, plan.Anzahl(KatasterKennungGrund.Abweichend));
    }

    // Bestand von vor dem Anzeigefeld: Die Kennung ist da, das Feld leer. Nachziehen,
    // ohne die Kennungen anzufassen.
    [Fact]
    public void Ein_leeres_Anzeigefeld_wird_bei_vorhandener_Kennung_nachgezogen()
    {
        var bestand = Bestand(BauteilArt.Haltung, HaltungKennung("78998-79002"));
        var record = Haltung("78998-79002");
        record.Geonis = new GeonisKennungen { Haltung = HaltungId, Kanal = "ch23h1a4XXXXXXXX" };

        var plan = KatasterKennungPlanBuilder.BaueFuerHaltungen([record], bestand);

        var position = Assert.Single(plan.Positionen);
        Assert.True(position.NurAnzeige);
        Assert.Equal(0, plan.Neu);
        Assert.Equal(1, plan.NurAnzeige);

        Assert.Equal(1, KatasterKennungAnwender.WendeAnAufHaltungen([record], plan));
        Assert.Equal(HaltungId, record.GetFieldValue(FieldKeys.GeonisId));
        Assert.Equal("ch23h1a4XXXXXXXX", record.Geonis.Kanal);
        Assert.Contains("nur das Feld \"GEONIS-Kennung\" wird nachgezogen",
            KatasterKennungBericht.Schreibe(plan, "x"), StringComparison.Ordinal);
    }

    // Ein XTF-Import legt die TID in Objekt_ID ab. Widerspricht sie der Kopie, stammt sie
    // aus einer neueren Quelle und gewinnt; stimmt sie ueberein, fehlen nur die
    // Verbundkennungen und die Uebernahme laeuft.
    [Fact]
    public void Eine_importierte_TID_in_Objekt_ID_sperrt_eine_abweichende_Kopie()
    {
        var bestand = Bestand(BauteilArt.Haltung, HaltungKennung("78998-79002"));
        var abweichend = Haltung("78998-79002");
        abweichend.SetFieldValue(FieldKeys.CadastreObjectId, "ch23h1a4NEUERXTF", FieldSource.Xtf405, false);
        var gleich = Haltung("78998-79002");
        gleich.SetFieldValue(FieldKeys.CadastreObjectId, HaltungId, FieldSource.Xtf405, false);
        var lisag = Haltung("78998-79002");
        lisag.SetFieldValue(FieldKeys.CadastreObjectId, "866789", FieldSource.Kataster, false);

        var plan = KatasterKennungPlanBuilder.BaueFuerHaltungen([abweichend, gleich, lisag], bestand);

        Assert.Equal(2, plan.Positionen.Count);
        Assert.Equal(1, plan.Anzahl(KatasterKennungGrund.Abweichend));
    }

    [Fact]
    public void Das_GEONIS_Aenderungsdatum_wird_mituebernommen()
    {
        var stand = new DateTime(2024, 5, 27, 14, 37, 28, DateTimeKind.Utc);
        var bestand = Bestand(BauteilArt.Haltung, HaltungKennung("78998-79002") with { GeonisGeaendert = stand });
        var record = Haltung("78998-79002");
        var plan = KatasterKennungPlanBuilder.BaueFuerHaltungen([record], bestand);

        KatasterKennungAnwender.WendeAnAufHaltungen([record], plan);

        Assert.Equal(stand, record.Geonis!.GeonisGeaendert);
    }

    [Fact]
    public void Schaechte_kennen_keine_Gegenrichtung()
    {
        var bestand = Bestand(BauteilArt.Schacht, KatasterKennung.FuerSchacht("78998", "Altdorf", KnotenId, null));

        var plan = KatasterKennungPlanBuilder.BaueFuerSchaechte([Schacht("78998"), Schacht("79002")], bestand);

        var position = Assert.Single(plan.Positionen);
        Assert.Equal("78998", position.Bauteil);
        Assert.Equal(KnotenId, position.Kennung.Knoten);
        Assert.Equal(1, plan.Anzahl(KatasterKennungGrund.NichtGefunden));
    }

    [Theory]
    [InlineData("78998-79002", "79002-78998")]
    [InlineData("07.638905-78998", "78998-07.638905")]
    [InlineData("78998", null)]
    [InlineData("1-2-3", null)]
    [InlineData("-78998", null)]
    public void Die_Gegenrichtung_dreht_nur_zweiteilige_Namen(string name, string? erwartet)
        => Assert.Equal(erwartet, KatasterKennungPlanBuilder.Gegenrichtung(name));

    [Fact]
    public void Der_Anwender_schreibt_die_Kennungen_und_dreht_die_Punkte_bei_Gegenrichtung()
    {
        var bestand = Bestand(BauteilArt.Haltung, HaltungKennung("78998-79002"));
        var record = Haltung("79002-78998");
        var plan = KatasterKennungPlanBuilder.BaueFuerHaltungen([record], bestand);

        var geschrieben = KatasterKennungAnwender.WendeAnAufHaltungen([record], plan);

        Assert.Equal(1, geschrieben);
        Assert.NotNull(record.Geonis);
        Assert.Equal(HaltungId, record.Geonis!.Haltung);
        Assert.Equal("ch23h1a46oVbkGmT", record.Geonis.Kanal);
        Assert.True(record.Geonis.RichtungGedreht);
        // Im Projekt steht 79002 vorn: Der "von"-Punkt ist im Kataster der Endpunkt E.
        Assert.Equal("ch23h1a44Op5RVY5", record.Geonis.VonPunkt);
        Assert.Equal("E75394", record.Geonis.VonPunktBezeichnung);
        Assert.Equal("ch23h1a4CNjzeqBU", record.Geonis.NachPunkt);
        Assert.Equal("A75394", record.Geonis.NachPunktBezeichnung);
        Assert.Equal("GEONIS-Kopie 2024-12", record.Geonis.Quelle);
        // Das Anzeigefeld spiegelt die Hauptkennung, ohne Handmarkierung.
        Assert.Equal(HaltungId, record.GetFieldValue(FieldKeys.GeonisId));
        Assert.False(record.FieldMeta[FieldKeys.GeonisId].UserEdited);
        Assert.Equal(FieldSource.Kataster, record.FieldMeta[FieldKeys.GeonisId].Source);
    }

    [Fact]
    public void Der_Anwender_laesst_eine_inzwischen_gesetzte_Kennung_stehen()
    {
        var bestand = Bestand(BauteilArt.Haltung, HaltungKennung("78998-79002"));
        var record = Haltung("78998-79002");
        var plan = KatasterKennungPlanBuilder.BaueFuerHaltungen([record], bestand);
        record.Geonis = new GeonisKennungen { Haltung = "ch23h1a4XXXXXXXX" };

        var geschrieben = KatasterKennungAnwender.WendeAnAufHaltungen([record], plan);

        Assert.Equal(0, geschrieben);
        Assert.Equal("ch23h1a4XXXXXXXX", record.Geonis!.Haltung);
    }

    [Fact]
    public void Der_Anwender_schreibt_Knoten_und_Bauwerk_des_Schachts()
    {
        var bestand = Bestand(BauteilArt.Schacht,
            KatasterKennung.FuerSchacht("78998", "Altdorf", KnotenId, "ch23h1a4Umcgr2UF"));
        var record = Schacht("78998");
        var plan = KatasterKennungPlanBuilder.BaueFuerSchaechte([record], bestand);

        Assert.Equal(1, KatasterKennungAnwender.WendeAnAufSchaechte([record], plan));
        Assert.Equal(KnotenId, record.Geonis!.Knoten);
        Assert.Equal("ch23h1a4Umcgr2UF", record.Geonis.Bauwerk);
        Assert.Equal(KnotenId, record.GetFieldValue(FieldKeys.GeonisId));
    }

    [Fact]
    public void Der_Bericht_nennt_Stand_Treffer_und_Gruende()
    {
        var bestand = Bestand(BauteilArt.Haltung, HaltungKennung("78998-79002"));
        var plan = KatasterKennungPlanBuilder.BaueFuerHaltungen(
            [Haltung("79002-78998"), Haltung("1-2")], bestand);

        var bericht = KatasterKennungBericht.Schreibe(plan, @"D:\Layer\Kennungen.gpkg");

        Assert.Contains("GEONIS-Kopie 2024-12", bericht, StringComparison.Ordinal);
        Assert.Contains("1 Haltungen würden ihre GEONIS-Kennungen bekommen", bericht, StringComparison.Ordinal);
        Assert.Contains("davon 1 über die Gegenrichtung", bericht, StringComparison.Ordinal);
        Assert.Contains("1 in der Kennungstabelle nicht gefunden", bericht, StringComparison.Ordinal);
        Assert.Contains("keine Fachwerte", bericht, StringComparison.Ordinal);
    }

    private static KatasterKennung HaltungKennung(string name, string haltungId = HaltungId)
        => KatasterKennung.FuerHaltung(
            name, "Altdorf", haltungId, "ch23h1a46oVbkGmT",
            "ch23h1a4CNjzeqBU", "A75394", "ch23h1a44Op5RVY5", "E75394",
            "ch23h1a43obhLa8B", "unbekannt");

    private static KatasterKennungBestand Bestand(BauteilArt art, params KatasterKennung[] kennungen)
        => new(
            art,
            kennungen.ToDictionary(k => k.Name, k => k, StringComparer.OrdinalIgnoreCase),
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            kennungen.Length,
            "GEONIS-Kopie 2024-12");

    private static HaltungRecord Haltung(string name)
    {
        var record = new HaltungRecord();
        record.SetFieldValue(FieldKeys.HoldingName, name, FieldSource.Manual, true);
        return record;
    }

    private static SchachtRecord Schacht(string nummer)
    {
        var record = new SchachtRecord();
        record.SetFieldValue("Schachtnummer", nummer, FieldSource.Manual, true);
        return record;
    }
}
