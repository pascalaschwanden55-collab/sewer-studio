using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.QgisBridge;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Eine GeoJSON-Datei ohne Objekte hat in QGIS auch keine Spalten. Jede Ebene mit
/// gespeicherter Abfrage ("geometrie_quelle = ..." / "haltung_kataster IS NULL")
/// laesst sich dann nicht mehr oeffnen — QGIS meldet "unsicher verortet" und der
/// Layer bleibt rot, bis der Nutzer seine Abfrage von Hand entfernt.
///
/// Darum liefert die Bruecke fuer einen leeren Stand eine einzelne Schemazeile OHNE
/// Geometrie. Gemessen mit QGIS 4.2: Der Layer bleibt gueltig und zeigt dank seines
/// Geometrietyp-Filters trotzdem null Objekte.
/// </summary>
public sealed class QgisLeerschemaTests
{
    public static TheoryData<string> Ebenen()
    {
        var data = new TheoryData<string>();
        foreach (var ebene in QgisLeerschema.Ebenen)
            data.Add(ebene);
        return data;
    }

    [Theory]
    [MemberData(nameof(Ebenen))]
    public void Jede_ebene_liefert_genau_eine_schemazeile_ohne_geometrie(string ebene)
    {
        var sammlung = QgisLeerschema.Baue(ebene);

        var feature = Assert.Single(sammlung.Features);
        Assert.Null(feature.Geometry);
        Assert.NotEmpty(feature.Properties);
        // Nur Spaltennamen, keine erfundenen Werte: sonst stuende in der Karte
        // eine Zeile, die es im Projekt gar nicht gibt.
        Assert.All(feature.Properties.Values, Assert.Null);
    }

    [Fact]
    public void Eine_unbekannte_ebene_liefert_keine_schemazeile()
    {
        Assert.Empty(QgisLeerschema.Baue("gibt_es_nicht").Features);
    }

    /// <summary>
    /// Der Waechter gegen Auseinanderlaufen: Die Schemazeile muss exakt dieselben
    /// Spalten nennen wie ein echtes Objekt derselben Ebene. Kommt im Builder ein
    /// Feld dazu, faellt genau dieser Test um.
    /// </summary>
    [Theory]
    [MemberData(nameof(Ebenen))]
    public void Schemafelder_stimmen_mit_den_echten_feldern_ueberein(string ebene)
    {
        using var fixture = QgisBridgeSnapshotBuilderTests.QgisBridgeFixture.Create();
        var builder = fixture.CreateBuilder();
        var snapshot = QgisProjectSnapshot.Capture(
            VollesProjekt(), "A-B", selectionStamp: 1, currentSchacht: "S1", schachtSelectionStamp: 1);

        var echt = EchteSammlung(builder, snapshot, ebene);
        var echteFelder = Assert.Single(echt.Features.Select(f => f.Properties.Keys.ToHashSet()).Distinct(
            HashSet<string>.CreateSetComparer()));
        var schemaFelder = Assert.Single(QgisLeerschema.Baue(ebene).Features).Properties.Keys.ToHashSet();

        Assert.Equal(echteFelder.OrderBy(n => n), schemaFelder.OrderBy(n => n));
    }

    private static GeoJsonFeatureCollection EchteSammlung(
        QgisBridgeSnapshotBuilder builder, QgisProjectSnapshot snapshot, string ebene)
        => ebene switch
        {
            "current" => builder.BuildCurrentGeoJson(snapshot),
            "current_schacht" => builder.BuildCurrentSchachtGeoJson(snapshot),
            "damages" => builder.BuildDamagesGeoJson(snapshot),
            "network" => builder.BuildNetworkGeoJson(snapshot),
            "sanierungstyp" => builder.BuildSanierungstypGeoJson(snapshot),
            "schaechte" => builder.BuildSchaechteGeoJson(snapshot),
            "schacht_sanierungstyp" => builder.BuildSchachtSanierungstypGeoJson(snapshot),
            _ => GeoJsonFeatureCollection.Empty
        };

    /// <summary>Ein Projekt, das jede der sieben Live-Ebenen mit echten Objekten fuellt.</summary>
    private static Project VollesProjekt()
    {
        var project = new Project { Name = "Leerschema-Test" };

        var haltung = new HaltungRecord();
        haltung.SetFieldValue("Haltungsname", "A-B", FieldSource.Manual, userEdited: true);
        haltung.SetFieldValue("Zustandsklasse", "4", FieldSource.Manual, userEdited: true);
        haltung.SetFieldValue("Nutzungsart", "Mischabwasser", FieldSource.Manual, userEdited: true);
        haltung.SetFieldValue("Ausgefuehrt_durch", "Baumeister", FieldSource.Manual, userEdited: true);
        haltung.SetFieldValue("NR", "1", FieldSource.Manual, userEdited: true);
        haltung.VsaFindings.Add(new VsaFinding
        {
            KanalSchadencode = "BAB",
            MeterStart = 5.0,
            Raw = "Riss bei 5 m"
        });
        project.Data.Add(haltung);

        var schacht = new SchachtRecord();
        schacht.SetFieldValue("Schachtnummer", "S1");
        schacht.SetFieldValue("Sanieren", "Ja");
        schacht.SetFieldValue("Pruefungsresultat", "dicht");
        schacht.SetFieldValue("Ausgefuehrt_durch", "Baumeister");
        schacht.SetFieldValue("NR", "1");
        project.SchaechteData.Add(schacht);

        return project;
    }
}
