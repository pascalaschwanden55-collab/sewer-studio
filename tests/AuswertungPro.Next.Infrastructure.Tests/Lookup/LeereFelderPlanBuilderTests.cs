using AuswertungPro.Next.Application.Lookup;
using AuswertungPro.Next.Application.UseCases;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Tests.Lookup;

/// <summary>
/// Nachfuellen aus dem QGIS-Bestand: Nur leere Felder, nie ein gefuelltes, und
/// bei einem mehrdeutigen Namen gar nichts.
/// </summary>
public sealed class LeereFelderPlanBuilderTests
{
    // Die Regel, an der alles haengt. Sie zuerst.
    [Fact]
    public void Ein_gefuelltes_Feld_wird_nie_angefasst()
    {
        var record = Haltung("80638-80631");
        record.SetFieldValue(FieldKeys.PipeMaterial, "Beton", FieldSource.Manual, userEdited: true);

        var plan = LeereFelderPlanBuilder.BaueFuerHaltungen(
            new[] { record },
            Bestand(("80638-80631", ("ha_material", "Steinzeug"))));

        Assert.DoesNotContain(plan.Positionen, p => p.Feld == FieldKeys.PipeMaterial);
    }

    // Auch ein importierter Wert ist Inhalt. Die Herkunft spielt keine Rolle —
    // sonst haette der Bestand das letzte Wort ueber einen Protokollwert.
    [Theory]
    [InlineData(FieldSource.Xtf)]
    [InlineData(FieldSource.Pdf)]
    [InlineData(FieldSource.Protocol)]
    [InlineData(FieldSource.Manual)]
    public void Die_Herkunft_des_vorhandenen_Werts_spielt_keine_Rolle(FieldSource quelle)
    {
        var record = Haltung("80638-80631");
        record.SetFieldValue(FieldKeys.PipeMaterial, "Beton", quelle, userEdited: false);

        var plan = LeereFelderPlanBuilder.BaueFuerHaltungen(
            new[] { record },
            Bestand(("80638-80631", ("ha_material", "Steinzeug"))));

        Assert.Empty(plan.Positionen);
    }

    [Fact]
    public void Ein_leeres_Feld_wird_ergaenzt()
    {
        var plan = LeereFelderPlanBuilder.BaueFuerHaltungen(
            new[] { Haltung("80638-80631") },
            Bestand(("80638-80631", ("ha_material", "Steinzeug"))));

        var position = Assert.Single(plan.Positionen);

        Assert.Equal("80638-80631", position.Bauteil);
        Assert.Equal(FieldKeys.PipeMaterial, position.Feld);
        Assert.Equal("Steinzeug", position.Wert);
    }

    // 2574 Haltungsnamen tragen im Bestand mehr als ein Objekt. Einen davon zu
    // nehmen waere geraten und saehe wie eine Tatsache aus.
    [Fact]
    public void Ein_mehrdeutiger_Name_bekommt_nichts()
    {
        var bestand = new QgisBestand(
            new Dictionary<string, QgisBauteil>(StringComparer.OrdinalIgnoreCase),
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "80638-80631" },
            GeleseneObjekte: 2);

        var plan = LeereFelderPlanBuilder.BaueFuerHaltungen(new[] { Haltung("80638-80631") }, bestand);

        Assert.Empty(plan.Positionen);
        Assert.Equal(1, plan.Anzahl(LeerfeldGrund.Mehrdeutig));
    }

    [Fact]
    public void Ein_unbekannter_Name_wird_gemeldet()
    {
        var plan = LeereFelderPlanBuilder.BaueFuerHaltungen(
            new[] { Haltung("99-999") },
            Bestand(("80638-80631", ("ha_material", "Steinzeug"))));

        Assert.Empty(plan.Positionen);
        Assert.Equal(1, plan.Anzahl(LeerfeldGrund.NichtGefunden));
    }

    // "unbekannt" steht im Bestand auf 59 % der Leitungen bei Verbindungsart und
    // Bettung. Es ist keine Angabe und fuellt deshalb nichts.
    [Theory]
    [InlineData("unbekannt")]
    [InlineData("UNBEKANNT")]
    [InlineData("")]
    [InlineData("   ")]
    public void Ohne_Angabe_wird_nichts_ergaenzt(string bestandswert)
    {
        var plan = LeereFelderPlanBuilder.BaueFuerHaltungen(
            new[] { Haltung("80638-80631") },
            Bestand(("80638-80631", ("ka_verbindungsart", bestandswert))));

        Assert.Empty(plan.Positionen);
        Assert.Equal(1, plan.Anzahl(LeerfeldGrund.NichtsZuErgaenzen));
    }

    // Ein Wert, den die Norm nicht kennt, wird nicht uebernommen — im Bestand
    // stehen 383 solche bei der funktionalen Hierarchie.
    [Theory]
    [InlineData("ka_funktionhierarchisch", "SAA.Sammelkanal")]
    [InlineData("ka_funktionhierarchisch", ".")]
    [InlineData("ka_verbindungsart", "Klebemuffe")]
    [InlineData("bw_status", "stillgelegt")]
    public void Ein_modellwidriger_Bestandswert_wird_nicht_uebernommen(string spalte, string wert)
    {
        var plan = LeereFelderPlanBuilder.BaueFuerHaltungen(
            new[] { Haltung("80638-80631") },
            Bestand(("80638-80631", (spalte, wert))));

        Assert.Empty(plan.Positionen);
    }

    // Die Null bedeutet im Bestand "unbekannt" — bei 39486 von 109871 Leitungen.
    [Fact]
    public void Eine_lichte_Hoehe_von_null_ist_keine_Angabe()
    {
        var plan = LeereFelderPlanBuilder.BaueFuerHaltungen(
            new[] { Haltung("80638-80631") },
            Bestand(("80638-80631", ("ha_lichte_hoehe", "0"))));

        Assert.Empty(plan.Positionen);
    }

    [Fact]
    public void Der_bauliche_Zustand_kommt_als_Ziffer_ins_Projekt()
    {
        var plan = LeereFelderPlanBuilder.BaueFuerHaltungen(
            new[] { Haltung("80638-80631") },
            Bestand(("80638-80631", ("bw_baulicherzustand", "Z3"))));

        var position = Assert.Single(plan.Positionen);

        Assert.Equal(FieldKeys.ConditionClass, position.Feld);
        Assert.Equal("3", position.Wert);
    }

    [Fact]
    public void Der_Eigentuemer_kommt_zeichengleich_aus_dem_Bestand()
    {
        var plan = LeereFelderPlanBuilder.BaueFuerHaltungen(
            new[] { Haltung("80638-80631") },
            Bestand(("80638-80631", ("org_eigentuemer", "Meliorationsgesellschaft Seedorf"))));

        Assert.Equal("Meliorationsgesellschaft Seedorf", Assert.Single(plan.Positionen).Wert);
    }

    [Fact]
    public void Ein_Zeitpunkt_wird_zum_Datum()
    {
        var plan = LeereFelderPlanBuilder.BaueFuerHaltungen(
            new[] { Haltung("80638-80631") },
            Bestand(("80638-80631", ("ha_letzte_aenderung", "2025-02-14T01:00:00"))));

        var position = Assert.Single(plan.Positionen);

        Assert.Equal(FieldKeys.CadastreLastChange, position.Feld);
        Assert.Equal("14.02.2025", position.Wert);
    }

    [Fact]
    public void Ein_Bauteil_ohne_Namen_wird_uebersprungen()
    {
        var plan = LeereFelderPlanBuilder.BaueFuerHaltungen(
            new[] { new HaltungRecord() },
            Bestand(("80638-80631", ("ha_material", "Steinzeug"))));

        Assert.Empty(plan.Positionen);
        Assert.Empty(plan.Hinweise);
        Assert.Equal(0, plan.GepruefteBauteile);
    }

    // --- Schaechte ---

    [Fact]
    public void Eine_runde_Schachtdimension_steht_in_einem_Feld()
    {
        var plan = LeereFelderPlanBuilder.BaueFuerSchaechte(
            new[] { Schacht("80401") },
            Bestand(BauteilArt.Schacht,
                ("80401", ("ns_dimension1", "600")), ("80401", ("ns_dimension2", "600"))));

        var position = Assert.Single(plan.Positionen, p => p.Feld == "Dimension");
        Assert.Equal("600 mm", position.Wert);
    }

    [Fact]
    public void Eine_eckige_Schachtdimension_nennt_beide_Masse()
    {
        var plan = LeereFelderPlanBuilder.BaueFuerSchaechte(
            new[] { Schacht("80401") },
            Bestand(BauteilArt.Schacht,
                ("80401", ("ns_dimension1", "1100")), ("80401", ("ns_dimension2", "900"))));

        Assert.Equal("1100 x 900 mm", Assert.Single(plan.Positionen, p => p.Feld == "Dimension").Wert);
    }

    // Der AWU-Export schreibt am Schacht Werte aus der ROHR-Materialliste.
    // Beim Nachfuellen muss daraus der Begriff des Programms werden.
    [Fact]
    public void Das_Schachtmaterial_kommt_aus_der_kurzen_Liste()
    {
        var plan = LeereFelderPlanBuilder.BaueFuerSchaechte(
            new[] { Schacht("80401") },
            Bestand(BauteilArt.Schacht, ("80401", ("ns_material", "Beton_unbekannt"))));

        Assert.Equal("Beton", Assert.Single(plan.Positionen).Wert);
    }

    [Fact]
    public void Der_Bericht_zaehlt_je_Feld()
    {
        var plan = LeereFelderPlanBuilder.BaueFuerHaltungen(
            new[] { Haltung("80638-80631"), Haltung("80631-80551") },
            Bestand(
                ("80638-80631", ("ha_material", "Steinzeug")),
                ("80631-80551", ("ha_material", "Steinzeug"))));

        var jeFeld = Assert.Single(plan.JeFeld);

        Assert.Equal(FieldKeys.PipeMaterial, jeFeld.Key);
        Assert.Equal(2, jeFeld.Value);
        Assert.Equal(2, plan.BetroffeneBauteile);
        Assert.Equal(2, plan.GepruefteBauteile);
    }

    private static HaltungRecord Haltung(string name)
    {
        var record = new HaltungRecord();
        record.SetFieldValue(FieldKeys.HoldingName, name, FieldSource.Xtf, userEdited: false);
        return record;
    }

    private static SchachtRecord Schacht(string nummer)
    {
        var record = new SchachtRecord();
        record.SetFieldValue("Schachtnummer", nummer, FieldSource.Xtf, userEdited: false);
        return record;
    }

    private static QgisBestand Bestand(params (string Name, (string Spalte, string Wert) Feld)[] eintraege)
        => Bestand(BauteilArt.Haltung, eintraege);

    private static QgisBestand Bestand(
        BauteilArt art, params (string Name, (string Spalte, string Wert) Feld)[] eintraege)
    {
        var jeName = new Dictionary<string, QgisBauteil>(StringComparer.OrdinalIgnoreCase);

        foreach (var (name, feld) in eintraege)
        {
            if (!jeName.TryGetValue(name, out var vorhanden))
            {
                jeName[name] = new QgisBauteil(
                    name,
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        [feld.Spalte] = feld.Wert
                    });
                continue;
            }

            var werte = new Dictionary<string, string>(vorhanden.Werte, StringComparer.OrdinalIgnoreCase)
            {
                [feld.Spalte] = feld.Wert
            };
            jeName[name] = vorhanden with { Werte = werte };
        }

        return new QgisBestand(
            jeName,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            jeName.Count);
    }
}
