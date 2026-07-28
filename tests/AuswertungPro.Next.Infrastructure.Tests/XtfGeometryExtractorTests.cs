using System;
using System.IO;
using System.Linq;
using AuswertungPro.Next.Domain.Geometry;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import.Xtf;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Phase 1 (Geometrie-Fundament 2026-05-23):
/// Akzeptanz-Kriterium 4: "Ein Test-XTF importiert und reproduzierbar
/// im Modell ankommen."
/// </summary>
public class XtfGeometryExtractorTests
{
    private static string FixturePath => Path.Combine(
        AppContext.BaseDirectory, "Fixtures", "phase1_geometrie.xtf");

    [Fact]
    public void ExtractHaltungen_findet_H1_mit_zwei_LV95_Punkten()
    {
        var result = XtfGeometryExtractor.ExtractHaltungen(FixturePath);

        Assert.True(result.ContainsKey("H1"),
            "Haltung H1 muss extrahiert werden");

        var h1 = result["H1"];
        Assert.Equal(GeometrySource.Xtf, h1.Source);
        Assert.Equal(2, h1.Verlauf.Count);

        Assert.Equal(2687970.000, h1.Verlauf[0].Ost, precision: 3);
        Assert.Equal(1168928.000, h1.Verlauf[0].Nord, precision: 3);
        Assert.Equal(2687980.000, h1.Verlauf[1].Ost, precision: 3);
        Assert.Equal(1168935.000, h1.Verlauf[1].Nord, precision: 3);

        Assert.True(h1.Verlauf[0].IsPlausibleLv95,
            "LV95-Wertebereichspruefung muss greifen");
    }

    [Fact]
    public void ExtractHaltungen_liefert_H2_als_Polylinie_mit_drei_Punkten()
    {
        var result = XtfGeometryExtractor.ExtractHaltungen(FixturePath);

        Assert.True(result.ContainsKey("H2"));
        var h2 = result["H2"];

        Assert.Equal(3, h2.Verlauf.Count);
        Assert.Equal(2687985.500, h2.Verlauf[1].Ost, precision: 3);
        Assert.Equal(1168942.250, h2.Verlauf[1].Nord, precision: 3);

        // Start/Ende sind die Endpunkte der Polylinie
        Assert.Equal(h2.Verlauf[0], h2.Start);
        Assert.Equal(h2.Verlauf[2], h2.Ende);
    }

    [Fact]
    public void ExtractHaltungen_uebergeht_Haltung_ohne_Verlauf()
    {
        var result = XtfGeometryExtractor.ExtractHaltungen(FixturePath);

        Assert.False(result.ContainsKey("H3"),
            "Haltung ohne <Verlauf> darf NICHT im Geometrie-Ergebnis erscheinen");
    }

    [Fact]
    public void ExtractSchachtLagen_findet_S1_mit_LV95_Punkt()
    {
        var result = XtfGeometryExtractor.ExtractSchachtLagen(FixturePath);

        Assert.True(result.ContainsKey("S1"),
            "Schacht S1 mit <Lage> muss extrahiert werden");

        var s1 = result["S1"];
        Assert.Equal(GeometrySource.Xtf, s1.Source);
        Assert.Equal(2687970.000, s1.Punkt.Ost, precision: 3);
        Assert.Equal(1168928.000, s1.Punkt.Nord, precision: 3);
        Assert.True(s1.Punkt.IsPlausibleLv95);
    }

    [Fact]
    public void ExtractSchachtLagen_uebergeht_Knoten_ohne_Lage()
    {
        var result = XtfGeometryExtractor.ExtractSchachtLagen(FixturePath);

        Assert.False(result.ContainsKey("S2"),
            "Abwasserknoten ohne <Lage> darf NICHT im Geometrie-Ergebnis erscheinen");
    }

    [Fact]
    public void ApplyTo_setzt_Geometrie_auf_existierende_HaltungRecords()
    {
        // Project mit Haltungen H1/H2/H3 vorbereiten (so wie der Importer
        // sie typisch bereits angelegt hat, bevor die Geometrie zugewiesen wird).
        var project = new Project();
        AddHaltung(project, "H1");
        AddHaltung(project, "H2");
        AddHaltung(project, "H3");

        var stats = XtfGeometryApplier.Apply(FixturePath, project);

        // AK2: Haltung mit Verlauf bekommt Geometrie
        var h1 = FindHaltung(project, "H1");
        Assert.NotNull(h1.Geometrie);
        Assert.Equal(GeometrySource.Xtf, h1.Geometrie!.Source);
        Assert.Equal(2, h1.Geometrie.Verlauf.Count);
        Assert.Equal(2687970.000, h1.Geometrie.Start.Ost, precision: 3);
        Assert.Equal(1168935.000, h1.Geometrie.Ende.Nord, precision: 3);

        var h2 = FindHaltung(project, "H2");
        Assert.NotNull(h2.Geometrie);
        Assert.Equal(3, h2.Geometrie!.Verlauf.Count);

        // AK3: Haltung ohne Verlauf bleibt explizit ohne Geometrie
        var h3 = FindHaltung(project, "H3");
        Assert.Null(h3.Geometrie);

        Assert.Equal(2, stats.HaltungenMitGeometrie);
    }

    [Fact]
    public void ApplyTo_legt_SchachtRecord_mit_Lage_an_wenn_noch_nicht_vorhanden()
    {
        var project = new Project();

        var stats = XtfGeometryApplier.Apply(FixturePath, project);

        // AK1: Schacht mit Lage wird angelegt und gespeichert
        var s1 = project.SchaechteData
            .FirstOrDefault(s => s.GetFieldValue("Bezeichnung") == "S1");
        Assert.NotNull(s1);
        Assert.NotNull(s1!.Lage);
        Assert.Equal(GeometrySource.Xtf, s1.Lage!.Source);
        Assert.Equal(2687970.000, s1.Lage.Punkt.Ost, precision: 3);
        Assert.Equal(1168928.000, s1.Lage.Punkt.Nord, precision: 3);

        // S2 hat keine <Lage> im XTF - darf KEINEN Schacht-Record produzieren
        // (Phase 1: anlegen nur bei vorhandener Geometrie).
        var s2 = project.SchaechteData
            .FirstOrDefault(s => s.GetFieldValue("Bezeichnung") == "S2");
        Assert.Null(s2);

        Assert.Equal(1, stats.SchaechteMitLage);
    }

    private static void AddHaltung(Project project, string name)
    {
        var rec = new HaltungRecord();
        rec.SetFieldValue("Haltungsname", name, FieldSource.Manual, userEdited: false);
        project.Data.Add(rec);
    }

    private static HaltungRecord FindHaltung(Project project, string name)
        => project.Data.First(r =>
               string.Equals(r.GetFieldValue("Haltungsname"), name, StringComparison.OrdinalIgnoreCase));

    [Fact]
    public void Lv95Coordinate_lehnt_LV03_Werte_als_unplausibel_ab()
    {
        // LV03-Werte (alte CH-Koordinaten) liegen bei Ost ~600'000, Nord ~200'000.
        // Wenn sie versehentlich als LV95 gespeichert wuerden, muss IsPlausibleLv95 false sein.
        var lv03 = new Lv95Coordinate(Ost: 687970.0, Nord: 168928.0);
        Assert.False(lv03.IsPlausibleLv95);

        var lv95 = new Lv95Coordinate(Ost: 2687970.0, Nord: 1168928.0);
        Assert.True(lv95.IsPlausibleLv95);
    }
}
