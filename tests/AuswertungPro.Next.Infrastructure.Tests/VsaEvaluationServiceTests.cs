using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Vsa;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class VsaEvaluationServiceTests
{
    private static VsaEvaluationService CreateService()
    {
        var root = TestPaths.FindSolutionRoot();
        var channelsTable = Path.Combine(root, "src", "AuswertungPro.Next.UI", "Data", "classification_channels.json");
        var manholesTable = Path.Combine(root, "src", "AuswertungPro.Next.UI", "Data", "classification_manholes.json");
        return new VsaEvaluationService(channelsTable, manholesTable);
    }

    [Fact]
    public void ResolveFindings_EnrichesBaseCodeWithFullCodeFromPrimaryDamages()
    {
        var rec = new HaltungRecord
        {
            VsaFindings = new List<VsaFinding>
            {
                new()
                {
                    KanalSchadencode = "BAJ",
                    SchadenlageAnfang = 0.10,
                    Raw = "Rohrverbindung Knick, Winkel = 10°, an Verbindung"
                }
            }
        };
        rec.SetFieldValue(
            "Primaere_Schaeden",
            "BAJ.C @0.10m (Rohrverbindung Knick)",
            FieldSource.Xtf,
            userEdited: false);

        var findings = VsaEvaluationService.ResolveFindings(rec, new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "BAJ" });

        Assert.Single(findings);
        Assert.Equal("BAJC", findings[0].KanalSchadencode);
        Assert.Equal("10", findings[0].Quantifizierung1);
        Assert.Equal("Rohrverbindung Knick, Winkel = 10°, an Verbindung", findings[0].Raw);
    }

    [Fact]
    public void ResolveFindings_DoesNotUseStationReferenceAsFindingCode()
    {
        var rec = new HaltungRecord
        {
            VsaFindings = new List<VsaFinding>
            {
                new()
                {
                    KanalSchadencode = "BDA",
                    SchadenlageAnfang = 0.90,
                    Raw = "Allgemeinzustand, Fotobeispiel, Foto 2 zu Station BAF.B.E"
                }
            }
        };
        rec.SetFieldValue(
            "Primaere_Schaeden",
            "BDA @0.90m (Allgemeinzustand, Fotobeispiel, Foto 2 zu Station BAF.B.E)",
            FieldSource.Xtf,
            userEdited: false);

        var findings = VsaEvaluationService.ResolveFindings(rec, new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "BDA" });

        Assert.Single(findings);
        Assert.Equal("BDA", findings[0].KanalSchadencode);
    }

    [Theory]
    [InlineData("Riss laengs, Breite = 4mm, geschaetzt", "4")]
    [InlineData("Rohrverbindung Knick, Winkel = 10°, geschaetzt", "10")]
    [InlineData("Einragendes Dichtungsmaterial, Querschnittsreduzierung = 3%", "3")]
    [InlineData("Riss radial, Breite = 1,5 mm", "1.5")]
    public void ExtractQuantValue_ReadsMeasurementFromRawText(string raw, string expected)
    {
        Assert.Equal(expected, VsaEvaluationService.ExtractQuantValue(raw));
    }

    [Fact]
    public void Evaluate_UsesSchadencodeRules_WhenRuleExists()
    {
        var project = new Project();
        var rec = new HaltungRecord();
        rec.SetFieldValue("Haltungsname", "H1", FieldSource.Xtf, userEdited: false);
        rec.SetFieldValue("Haltungslaenge_m", "10", FieldSource.Xtf, userEdited: false);

        rec.VsaFindings = new List<VsaFinding>
        {
            new() { KanalSchadencode = "BAF", LL = 3.0 }
        };

        project.Data.Add(rec);

        var svc = CreateService();
        var res = svc.Evaluate(project);
        Assert.True(res.Ok, res.ErrorMessage);

        // With at least one classified finding, notes must be numeric (not n/a)
        Assert.NotEqual("n/a", rec.GetFieldValue("VSA_Zustandsnote_D"));
        Assert.NotEqual("n/a", rec.GetFieldValue("VSA_Zustandsnote_S"));
        Assert.NotEqual("n/a", rec.GetFieldValue("VSA_Zustandsnote_B"));

        // Zustandsklasse should be set (0..4 or n/a)
        Assert.False(string.IsNullOrWhiteSpace(rec.GetFieldValue("Zustandsklasse")));
    }

    [Fact]
    public void Evaluate_DoesNotFallbackToEinzelschadenklasse_WhenSchadencodeUnknown()
    {
        var project = new Project();
        var rec = new HaltungRecord();
        rec.SetFieldValue("Haltungsname", "H2", FieldSource.Xtf, userEdited: false);
        rec.SetFieldValue("Haltungslaenge_m", "10", FieldSource.Xtf, userEdited: false);

        // Unknown Schadencode, but importer might have filled EZ* from Einzelschadenklasse.
        rec.VsaFindings = new List<VsaFinding>
        {
            new()
            {
                KanalSchadencode = "ZZZ999",
                EZD = 1,
                EZS = 1,
                EZB = 1,
                LL = 3.0
            }
        };

        project.Data.Add(rec);

        var svc = CreateService();
        var res = svc.Evaluate(project);
        Assert.True(res.Ok, res.ErrorMessage);

        // With unknown code and no rule contribution, requirement results must be n/a (not silently using EZ fallback)
        Assert.Equal("n/a", rec.GetFieldValue("VSA_Zustandsnote_D"));
        Assert.Equal("n/a", rec.GetFieldValue("VSA_Zustandsnote_S"));
        Assert.Equal("n/a", rec.GetFieldValue("VSA_Zustandsnote_B"));
        Assert.Equal("n/a", rec.GetFieldValue("Zustandsklasse"));
    }

    [Fact]
    public void Evaluate_ParsesCodesFromPrimaereSchaeden_WhenNoFindings()
    {
        var project = new Project();
        var rec = new HaltungRecord();
        rec.SetFieldValue("Haltungsname", "H3", FieldSource.Xtf, userEdited: false);
        rec.SetFieldValue("Haltungslaenge_m", "10", FieldSource.Xtf, userEdited: false);

        // No structured findings, but textual primary damage codes exist.
        rec.SetFieldValue("Primaere_Schaeden", "BAF @12.3m (foo)", FieldSource.Xtf, userEdited: false);

        project.Data.Add(rec);

        var svc = CreateService();
        var res = svc.Evaluate(project);
        Assert.True(res.Ok, res.ErrorMessage);

        Assert.NotEqual("n/a", rec.GetFieldValue("VSA_Zustandsnote_D"));
        Assert.NotEqual("n/a", rec.GetFieldValue("VSA_Zustandsnote_S"));
        Assert.NotEqual("n/a", rec.GetFieldValue("VSA_Zustandsnote_B"));
        Assert.False(string.IsNullOrWhiteSpace(rec.GetFieldValue("Zustandsklasse")));
    }

    // ── Quantifizierungs-Tests ──────────────────────────────────────────

    [Fact]
    public void Evaluate_UsesQuantRules_WhenQ1Provided()
    {
        // BAA mit Q1=0.5mm (Rissbreite < 1mm) → EZS=3, EZB=3 (gemäss quantRules)
        // Ohne Q1 wäre der statische Default: EZS=2, EZB=2
        var project = new Project();
        var rec = new HaltungRecord();
        rec.SetFieldValue("Haltungsname", "H4_quant", FieldSource.Xtf, userEdited: false);
        rec.SetFieldValue("Haltungslaenge_m", "10", FieldSource.Xtf, userEdited: false);

        rec.VsaFindings = new List<VsaFinding>
        {
            new() { KanalSchadencode = "BAA", Quantifizierung1 = "0.5", LL = 3.0 }
        };

        project.Data.Add(rec);

        var svc = CreateService();
        var res = svc.Evaluate(project);
        Assert.True(res.Ok, res.ErrorMessage);

        // Quantifizierte BAA-Bewertung muss erfolgen (nicht n/a)
        Assert.NotEqual("n/a", rec.GetFieldValue("VSA_Zustandsnote_S"));
        Assert.NotEqual("n/a", rec.GetFieldValue("VSA_Zustandsnote_B"));

        // Die ZN sollte höher sein bei kleinem Riss (EZ=3 statt Default EZ=2)
        var znS = rec.GetFieldValue("VSA_Zustandsnote_S");
        Assert.True(double.TryParse(znS?.Replace(',', '.'),
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var znSVal));
        // EZ=3 + 0.4 = 3.4 (Startwert vor Abminderung) → ZN muss > 2.4 sein
        Assert.True(znSVal > 2.4, $"ZN_S mit Q1=0.5mm (EZ=3) sollte > 2.4 sein, ist aber {znSVal}");
    }

    [Fact]
    public void Evaluate_FallsBackToStatic_WhenQ1Null()
    {
        // BAA ohne Q1 → statische Defaults: EZS=2, EZB=2
        var project = new Project();
        var recWithQ1 = new HaltungRecord();
        recWithQ1.SetFieldValue("Haltungsname", "H5_noQ1", FieldSource.Xtf, userEdited: false);
        recWithQ1.SetFieldValue("Haltungslaenge_m", "10", FieldSource.Xtf, userEdited: false);

        recWithQ1.VsaFindings = new List<VsaFinding>
        {
            new() { KanalSchadencode = "BAA", Quantifizierung1 = null, LL = 3.0 }
        };

        project.Data.Add(recWithQ1);

        var svc = CreateService();
        var res = svc.Evaluate(project);
        Assert.True(res.Ok, res.ErrorMessage);

        // Muss trotzdem klassifiziert werden (nicht n/a)
        Assert.NotEqual("n/a", recWithQ1.GetFieldValue("VSA_Zustandsnote_S"));
        Assert.NotEqual("n/a", recWithQ1.GetFieldValue("VSA_Zustandsnote_B"));

        // EZ=2 (Default) + 0.4 = 2.4 → ZN muss <= 2.4 sein
        var znS = recWithQ1.GetFieldValue("VSA_Zustandsnote_S");
        Assert.True(double.TryParse(znS?.Replace(',', '.'),
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var znSVal));
        Assert.True(znSVal <= 2.5, $"ZN_S ohne Q1 (Default EZ=2) sollte <= 2.5 sein, ist aber {znSVal}");
    }

    [Fact]
    public void Evaluate_UsesQuantRules_LargeQ1GivesWorseEZ()
    {
        // BAA mit Q1=10mm (Rissbreite > 5mm) → EZS=1, EZB=1 (schlechter Zustand)
        var project = new Project();
        var rec = new HaltungRecord();
        rec.SetFieldValue("Haltungsname", "H6_largeQ1", FieldSource.Xtf, userEdited: false);
        rec.SetFieldValue("Haltungslaenge_m", "10", FieldSource.Xtf, userEdited: false);

        rec.VsaFindings = new List<VsaFinding>
        {
            new() { KanalSchadencode = "BAA", Quantifizierung1 = "10", LL = 3.0 }
        };

        project.Data.Add(rec);

        var svc = CreateService();
        var res = svc.Evaluate(project);
        Assert.True(res.Ok, res.ErrorMessage);

        // Grosser Riss → schlechtere ZN (EZ=1 + 0.4 = 1.4 Startwert)
        var znS = rec.GetFieldValue("VSA_Zustandsnote_S");
        Assert.True(double.TryParse(znS?.Replace(',', '.'),
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var znSVal));
        Assert.True(znSVal < 2.0, $"ZN_S mit Q1=10mm (EZ=1) sollte < 2.0 sein, ist aber {znSVal}");
    }

    [Fact]
    public void Evaluate_ClassifiesNewBCACodes()
    {
        // BCA (Anschluss) – neu hinzugefügter Code muss klassifiziert werden
        var project = new Project();
        var rec = new HaltungRecord();
        rec.SetFieldValue("Haltungsname", "H7_BCA", FieldSource.Xtf, userEdited: false);
        rec.SetFieldValue("Haltungslaenge_m", "10", FieldSource.Xtf, userEdited: false);

        rec.VsaFindings = new List<VsaFinding>
        {
            new() { KanalSchadencode = "BCA", LL = 3.0 }
        };

        project.Data.Add(rec);

        var svc = CreateService();
        var res = svc.Evaluate(project);
        Assert.True(res.Ok, res.ErrorMessage);

        // BCA hat EZD=2, EZB=2 → darf nicht n/a sein
        Assert.NotEqual("n/a", rec.GetFieldValue("VSA_Zustandsnote_D"));
        Assert.NotEqual("n/a", rec.GetFieldValue("VSA_Zustandsnote_B"));
    }

    [Fact]
    public void Evaluate_ShadowMode_LogsExpectedDriftWithoutChangingProductiveResult()
    {
        var root = TestPaths.FindSolutionRoot();
        var channelsTable = Path.Combine(root, "src", "AuswertungPro.Next.UI", "Data", "classification_channels.json");
        var manholesTable = Path.Combine(root, "src", "AuswertungPro.Next.UI", "Data", "classification_manholes.json");
        var tempDir = Path.Combine(Path.GetTempPath(), "sewer-vsa-shadow-tests", Guid.NewGuid().ToString("N"));
        var shadowLogPath = Path.Combine(tempDir, "vsa_shadow.jsonl");

        try
        {
            var project = new Project();
            var rec = new HaltungRecord();
            rec.SetFieldValue("Haltungsname", "H_shadow", FieldSource.Xtf, userEdited: false);
            rec.SetFieldValue("Haltungslaenge_m", "10", FieldSource.Xtf, userEdited: false);
            rec.SetFieldValue("Rohrmaterial", "Beton", FieldSource.Xtf, userEdited: false);
            rec.SetFieldValue("DN_mm", "300", FieldSource.Xtf, userEdited: false);
            rec.VsaFindings = new List<VsaFinding>
            {
                new() { KanalSchadencode = "BAAA", Quantifizierung1 = "0.5" }
            };
            project.Data.Add(rec);

            var svc = new VsaEvaluationService(
                channelsTable,
                manholesTable,
                shadowModeEnabled: true,
                shadowLogPath: shadowLogPath);

            var res = svc.Evaluate(project);

            Assert.True(res.Ok, res.ErrorMessage);
            Assert.Equal("3.28", rec.GetFieldValue("VSA_Zustandsnote_S"));
            Assert.True(File.Exists(shadowLogPath), $"Shadow log missing: {shadowLogPath}");

            using var doc = JsonDocument.Parse(File.ReadLines(shadowLogPath)
                .Single(line => line.Contains("\"requirement\":\"S\"", StringComparison.OrdinalIgnoreCase)));
            var entry = doc.RootElement;
            Assert.Equal("BAAA", entry.GetProperty("code").GetString());
            Assert.Equal("BAA", entry.GetProperty("base_code").GetString());
            Assert.Equal("S", entry.GetProperty("requirement").GetString());
            Assert.Equal(3, entry.GetProperty("legacy_ez").GetInt32());
            Assert.Equal(4, entry.GetProperty("v2_ez").GetInt32());
            Assert.True(entry.GetProperty("expected_drift").GetBoolean());
            Assert.Equal("A", entry.GetProperty("ch1").GetString());
            Assert.Equal(JsonValueKind.Null, entry.GetProperty("ch2").ValueKind);
            Assert.Equal("0.5", entry.GetProperty("q1").GetString());
            Assert.Equal(JsonValueKind.Null, entry.GetProperty("q2").ValueKind);
            Assert.Equal("Beton", entry.GetProperty("material").GetString());
            Assert.Equal("300", entry.GetProperty("dn").GetString());
            Assert.False(string.IsNullOrWhiteSpace(entry.GetProperty("v2_rule_id").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(entry.GetProperty("v2_source_ref").GetString()));
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Evaluate_ShadowMode_DoesNotLogWhenV2MatchesLegacy()
    {
        var root = TestPaths.FindSolutionRoot();
        var channelsTable = Path.Combine(root, "src", "AuswertungPro.Next.UI", "Data", "classification_channels.json");
        var manholesTable = Path.Combine(root, "src", "AuswertungPro.Next.UI", "Data", "classification_manholes.json");
        var tempDir = Path.Combine(Path.GetTempPath(), "sewer-vsa-shadow-tests", Guid.NewGuid().ToString("N"));
        var shadowLogPath = Path.Combine(tempDir, "vsa_shadow.jsonl");

        try
        {
            var project = new Project();
            var rec = new HaltungRecord();
            rec.SetFieldValue("Haltungsname", "H_shadow_match", FieldSource.Xtf, userEdited: false);
            rec.SetFieldValue("Haltungslaenge_m", "10", FieldSource.Xtf, userEdited: false);
            rec.VsaFindings = new List<VsaFinding>
            {
                new() { KanalSchadencode = "BAN" }
            };
            project.Data.Add(rec);

            var svc = new VsaEvaluationService(
                channelsTable,
                manholesTable,
                shadowModeEnabled: true,
                shadowLogPath: shadowLogPath);

            var res = svc.Evaluate(project);

            Assert.True(res.Ok, res.ErrorMessage);
            Assert.False(File.Exists(shadowLogPath));
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Evaluate_ShadowMode_LogsV2DiagnosticReason_WhenV2CannotClassify()
    {
        var root = TestPaths.FindSolutionRoot();
        var channelsTable = Path.Combine(root, "src", "AuswertungPro.Next.UI", "Data", "classification_channels.json");
        var manholesTable = Path.Combine(root, "src", "AuswertungPro.Next.UI", "Data", "classification_manholes.json");
        var tempDir = Path.Combine(Path.GetTempPath(), "sewer-vsa-shadow-tests", Guid.NewGuid().ToString("N"));
        var shadowLogPath = Path.Combine(tempDir, "vsa_shadow.jsonl");

        try
        {
            var project = new Project();
            var rec = new HaltungRecord();
            rec.SetFieldValue("Haltungsname", "H_shadow_reason", FieldSource.Xtf, userEdited: false);
            rec.SetFieldValue("Haltungslaenge_m", "10", FieldSource.Xtf, userEdited: false);
            rec.VsaFindings = new List<VsaFinding>
            {
                new() { KanalSchadencode = "BCA" }
            };
            project.Data.Add(rec);

            var svc = new VsaEvaluationService(
                channelsTable,
                manholesTable,
                shadowModeEnabled: true,
                shadowLogPath: shadowLogPath);

            var res = svc.Evaluate(project);

            Assert.True(res.Ok, res.ErrorMessage);
            Assert.True(File.Exists(shadowLogPath), $"Shadow log missing: {shadowLogPath}");

            using var doc = JsonDocument.Parse(File.ReadLines(shadowLogPath)
                .Single(line => line.Contains("\"requirement\":\"D\"", StringComparison.OrdinalIgnoreCase)));
            var entry = doc.RootElement;
            Assert.Equal("BCA", entry.GetProperty("code").GetString());
            Assert.Null(entry.GetProperty("v2_ez").GetString());
            Assert.Equal("rule-not-found", entry.GetProperty("v2_reason").GetString());
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }
}
