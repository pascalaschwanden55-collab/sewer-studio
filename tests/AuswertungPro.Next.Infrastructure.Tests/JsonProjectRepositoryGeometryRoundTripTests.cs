using System;
using System.IO;
using System.Linq;
using AuswertungPro.Next.Domain.Geometry;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import.Xtf;
using AuswertungPro.Next.Infrastructure.Projects;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Phase 1 (Geometrie-Fundament 2026-05-23):
/// Persistenz-Round-Trip. Speichert ein Project mit Geometrie als JSON,
/// laedt es neu und prueft dass Verlauf, Punkt und Source erhalten bleiben.
///
/// Wenn das nicht funktioniert, sind die Phase-1-Akzeptanzkriterien
/// nur scheinbar erfuellt - die Geometrie wuerde beim ersten Speichern
/// verloren gehen.
/// </summary>
public class JsonProjectRepositoryGeometryRoundTripTests
{
    private static string FixturePath => Path.Combine(
        AppContext.BaseDirectory, "Fixtures", "phase1_geometrie.xtf");

    [Fact]
    public void Project_mit_Haltungs_und_Schacht_Geometrie_ueberlebt_Save_Load_Zyklus()
    {
        // Arrange: Project mit Geometrie aufbauen
        var project = new Project();
        AddHaltung(project, "H1");
        AddHaltung(project, "H2");
        AddHaltung(project, "H3"); // bewusst ohne Verlauf in der Fixture

        var apply = XtfGeometryApplier.Apply(FixturePath, project);
        Assert.Equal(2, apply.HaltungenMitGeometrie);
        Assert.Equal(1, apply.SchaechteMitLage);

        // Act: Save + Load
        var repo = new JsonProjectRepository();
        var tempPath = Path.Combine(
            Path.GetTempPath(),
            $"phase1_roundtrip_{Guid.NewGuid():N}.haltproj.json");

        try
        {
            var saveResult = repo.Save(project, tempPath);
            Assert.True(saveResult.Ok,
                $"Save fehlgeschlagen: {saveResult.ErrorCode} - {saveResult.ErrorMessage}");

            var loadResult = repo.Load(tempPath);
            Assert.True(loadResult.Ok,
                $"Load fehlgeschlagen: {loadResult.ErrorCode} - {loadResult.ErrorMessage}");

            var loaded = loadResult.Value!;

            // Assert: Haltung mit Verlauf erhalten
            var h1 = FindHaltung(loaded, "H1");
            Assert.NotNull(h1.Geometrie);
            Assert.Equal(GeometrySource.Xtf, h1.Geometrie!.Source);
            Assert.Equal(2, h1.Geometrie.Verlauf.Count);
            Assert.Equal(2687970.000, h1.Geometrie.Verlauf[0].Ost, precision: 3);
            Assert.Equal(1168928.000, h1.Geometrie.Verlauf[0].Nord, precision: 3);
            Assert.Equal(2687980.000, h1.Geometrie.Verlauf[1].Ost, precision: 3);

            // Polylinie mit 3 Punkten
            var h2 = FindHaltung(loaded, "H2");
            Assert.NotNull(h2.Geometrie);
            Assert.Equal(3, h2.Geometrie!.Verlauf.Count);

            // AK3: Haltung ohne Geometrie bleibt nach Round-Trip null
            var h3 = FindHaltung(loaded, "H3");
            Assert.Null(h3.Geometrie);

            // Schacht mit Lage erhalten
            var s1 = loaded.SchaechteData
                .First(s => string.Equals(s.GetFieldValue("Bezeichnung"), "S1", StringComparison.OrdinalIgnoreCase));
            Assert.NotNull(s1.Lage);
            Assert.Equal(GeometrySource.Xtf, s1.Lage!.Source);
            Assert.Equal(2687970.000, s1.Lage.Punkt.Ost, precision: 3);
            Assert.Equal(1168928.000, s1.Lage.Punkt.Nord, precision: 3);
        }
        finally
        {
            try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
            try { if (File.Exists(tempPath + ".bak")) File.Delete(tempPath + ".bak"); } catch { }
        }
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
}
