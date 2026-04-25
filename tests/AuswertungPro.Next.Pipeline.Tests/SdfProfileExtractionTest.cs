using System;
using System.IO;
using System.Linq;
using AuswertungPro.Next.Infrastructure.Import.WinCan;
using Xunit;
using Xunit.Abstractions;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Integrationstest: Extrahiert Inspektions-Profile aus den drei SDF-konvertierten
/// WinCan-VX-Datenbanken (Andermatt 2.11/2.12 + Erstfeld 6.19) und speichert sie.
/// Nur Profile + Muster — keine Frame-Extraktion (braucht Videos, Stunden-Aufwand).
///
/// Audit 2026-04-25 STAB-M9: Schreib-Pfade ueber `KI_BRAIN_ROOT` env-var (Default
/// Path.GetTempPath()/SewerStudio_TestArtifacts) statt hartkodiert C:\KI_BRAIN.
/// Test wird uebersprungen wenn die SDF-konvertierten DB3-Dateien fehlen — keine
/// rote CI mehr auf Maschinen ohne lokale Eval-Daten.
/// </summary>
public class SdfProfileExtractionTest
{
    private readonly ITestOutputHelper _output;
    public SdfProfileExtractionTest(ITestOutputHelper output) => _output = output;

    private static string GetKiBrainRoot()
    {
        var env = Environment.GetEnvironmentVariable("KI_BRAIN_ROOT");
        if (!string.IsNullOrWhiteSpace(env)) return env;
        // Fallback: lokales C:\KI_BRAIN wenn da, sonst Temp.
        if (Directory.Exists(@"C:\KI_BRAIN")) return @"C:\KI_BRAIN";
        return Path.Combine(Path.GetTempPath(), "SewerStudio_TestArtifacts");
    }

    [Fact]
    [Trait("Category", "GpuEval")]
    public void ExtractAllThreeConvertedSdfs()
    {
        var root = GetKiBrainRoot();
        var dbs = new[]
        {
            Path.Combine(root, "sdf_converted", "Andermatt_Zone_2.11.db3"),
            Path.Combine(root, "sdf_converted", "Andermatt_Zone_2.12.db3"),
            Path.Combine(root, "sdf_converted", "Erstfeld_Zone_6.19.db3"),
        };

        // Ueberspringen wenn keine Test-Daten vorhanden — verhindert UnauthorizedAccessException
        // auf Maschinen ohne die lokalen Eval-Daten.
        if (!dbs.Any(File.Exists))
        {
            _output.WriteLine($"Test-Daten fehlen unter {root}/sdf_converted/ — Test uebersprungen.");
            _output.WriteLine("Setze Env-Var KI_BRAIN_ROOT auf das Verzeichnis mit sdf_converted/ um den Test laufen zu lassen.");
            return;
        }

        var outDir = Path.Combine(root, "inspection_profiles");
        Directory.CreateDirectory(outDir);

        int totalProfiles = 0;
        int totalEvents = 0;

        foreach (var db in dbs)
        {
            if (!File.Exists(db))
            {
                _output.WriteLine($"FEHLT: {db}");
                continue;
            }
            var profiles = InspectionProfileExtractor.ExtractFromDb3(db);
            InspectionProfileExtractor.SaveProfiles(profiles, outDir);
            var evCount = profiles.Sum(p => p.Ereignisse.Count);
            totalProfiles += profiles.Count;
            totalEvents += evCount;
            _output.WriteLine($"  {Path.GetFileName(db),-35} {profiles.Count,4} Profile / {evCount,5} Ereignisse");
        }

        _output.WriteLine("");
        _output.WriteLine($"TOTAL: {totalProfiles} Profile / {totalEvents} Ereignisse extrahiert.");

        // Muster-Aggregation
        var allProfiles = new System.Collections.Generic.List<InspectionProfile>();
        foreach (var db in dbs.Where(File.Exists))
            allProfiles.AddRange(InspectionProfileExtractor.ExtractFromDb3(db));
        if (allProfiles.Count > 0)
        {
            var patterns = InspectionPatternAggregator.Aggregate(allProfiles);
            InspectionPatternAggregator.SavePatterns(patterns,
                Path.Combine(root, "inspection_patterns_sdf.json"));
            _output.WriteLine($"Muster aggregiert: Median Codes/m = {patterns.MedianCodierungenProMeter:F2}, " +
                $"Median Speed = {patterns.MedianFahrgeschwindigkeit:F3} m/s");
        }

        // InspectionProfileExtractor filtert Haltungen ohne komplette Stammdaten raus,
        // daher weniger als die 176 gezaehlten SECTION-Eintraege. 96 gueltige Profile mit
        // 1'568 Ereignissen ist der beobachtete Wert der heute extrahiert wurde.
        Assert.True(totalProfiles >= 80, $"Erwartet ~96 Profile, bekommen {totalProfiles}");
        Assert.True(totalEvents >= 1400, $"Erwartet ~1568 Ereignisse, bekommen {totalEvents}");
    }
}
