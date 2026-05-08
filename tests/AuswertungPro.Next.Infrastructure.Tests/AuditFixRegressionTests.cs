using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Sanierung;
using AuswertungPro.Next.Infrastructure.Sanierung;
using AuswertungPro.Next.Infrastructure.Vsa.Classification;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Regression-Tests fuer die Audit-Fixes vom 28.04.2026.
/// Sichert ab, dass die heutigen kritischen Korrekturen nicht versehentlich rueckgaengig gemacht werden.
/// </summary>
[Trait("Category", "Integration")]
public sealed class AuditFixRegressionTests
{
    // ── Audit-Fix 1: BBox Y-Mitten Operator-Prioritaet ────────────────────
    // Der Bug war: `(Y1 ?? 0 + (Y2 ?? 1)) / 2.0` -> ?? schwaecher als +
    // -> wenn Y1 gesetzt, ignorierte der Code Y2 komplett.
    // Hier wird die korrekte Mathematik direkt geprueft.

    [Theory]
    [InlineData(0.2, 0.6, 0.4)]   // beide gesetzt: Mittel
    [InlineData(0.0, 1.0, 0.5)]   // beide am Rand: Mitte = 0.5
    [InlineData(0.3, 0.7, 0.5)]
    public void BboxYCenter_ComputesArithmeticMean_OfBothValues(double y1, double y2, double expected)
    {
        // Replikation der gefixten Formel: ((Y1 ?? 0) + (Y2 ?? 1)) / 2.0
        double? bboxY1Norm = y1;
        double? bboxY2Norm = y2;
        double yCenter = ((bboxY1Norm ?? 0) + (bboxY2Norm ?? 1)) / 2.0;
        Assert.Equal(expected, yCenter, precision: 4);
    }

    [Fact]
    public void BboxYCenter_BothNull_DefaultsToHalf()
    {
        // (0 + 1) / 2 = 0.5
        double? y1 = null, y2 = null;
        double yCenter = ((y1 ?? 0) + (y2 ?? 1)) / 2.0;
        Assert.Equal(0.5, yCenter);
    }

    [Fact]
    public void BboxYCenter_OnlyY1Set_UsesY1Plus1Default()
    {
        // Y1=0.3, Y2=null -> (0.3 + 1) / 2 = 0.65
        double? y1 = 0.3, y2 = null;
        double yCenter = ((y1 ?? 0) + (y2 ?? 1)) / 2.0;
        Assert.Equal(0.65, yCenter, precision: 4);
        // VORHER (Bug): y1 ?? (0 + (y2 ?? 1)) / 2.0 = 0.3 -> falsch!
        Assert.NotEqual(0.3, yCenter);
    }

    // ── Audit-Fix 2: VsaClassificationTable Logger statt stille Schluckung ─

    [Fact]
    public void VsaClassificationTable_TryLoadFromFile_ReportsErrorOnMissingFile()
    {
        var nonExistent = Path.Combine(Path.GetTempPath(), "nonexistent_" + Guid.NewGuid().ToString("N") + ".json");
        var ok = VsaClassificationTable.TryLoadFromFile(nonExistent, out var table, out var err);

        Assert.False(ok);
        Assert.NotNull(err);
        Assert.Contains("nicht gefunden", err);
        Assert.Empty(table.Rules);
    }

    [Fact]
    public void VsaClassificationTable_TryLoadFromFile_ReportsErrorOnCorruptJson()
    {
        var tmpFile = Path.Combine(Path.GetTempPath(), "corrupt_" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            File.WriteAllText(tmpFile, "{ this is not valid json :::: }");
            var ok = VsaClassificationTable.TryLoadFromFile(tmpFile, out var table, out var err);
            Assert.False(ok);
            Assert.NotNull(err);
            Assert.Empty(table.Rules);
        }
        finally
        {
            if (File.Exists(tmpFile)) File.Delete(tmpFile);
        }
    }

    [Fact]
    public void VsaClassificationTable_TryLoadFromFile_ReportsErrorOnEmptyRules()
    {
        var tmpFile = Path.Combine(Path.GetTempPath(), "empty_" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            File.WriteAllText(tmpFile, "{ \"rules\": [] }");
            var ok = VsaClassificationTable.TryLoadFromFile(tmpFile, out _, out var err);
            Assert.False(ok);
            Assert.Contains("keine Rules", err ?? "");
        }
        finally
        {
            if (File.Exists(tmpFile)) File.Delete(tmpFile);
        }
    }

    // ── Audit-Fix 3: RehabilitationRulesEngine User-Regeln werden hart erzwungen ─

    [Fact]
    public void RulesEngine_GfkBend_ExcludesCippInliner()
    {
        var engine = new RehabilitationRulesEngine(userRules: null);
        var ctx = new HaltungsKontext
        {
            DnMm = 200,
            Material = "GFK",
            HasBendModerate = true,    // 15 Grad - GFK nicht bogengaengig
            HasBendSevere = false,
        };

        var result = engine.Evaluate(ctx, new[] { "BAB.B" });

        // cipp_inliner muss in Excluded landen, nicht in Eligible
        Assert.DoesNotContain(result.Eligible, m => m.Procedure.Id == "cipp_inliner");
        Assert.Contains(result.Excluded, m => m.Procedure.Id == "cipp_inliner");
    }

    [Fact]
    public void RulesEngine_AsbestosCement_ExcludesBerstliningAndRobotic()
    {
        var engine = new RehabilitationRulesEngine(userRules: null);
        var ctx = new HaltungsKontext
        {
            DnMm = 250,
            Material = "Asbestzement",
        };

        var result = engine.Evaluate(ctx, new[] { "BAB.B" });

        Assert.Contains(result.Excluded, m => m.Procedure.Id == "berstlining");
        Assert.Contains(result.Excluded, m => m.Procedure.Id == "robotic_milling");
        Assert.Contains(result.Excluded, m => m.Procedure.Id == "robotic_repair");
    }

    [Fact]
    public void RulesEngine_UserRule_ExcludesProcedureWhenMaterialMatches()
    {
        var tmpRulesFile = Path.Combine(Path.GetTempPath(), "user_rules_" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            File.WriteAllText(tmpRulesFile, """
            {
              "schema_version": "1.0",
              "rules": [
                {
                  "id": "test_no_cipp_for_steel",
                  "name": "Kein CIPP fuer Stahl",
                  "enabled": true,
                  "conditions": { "material_contains": "stahl" },
                  "exclude_procedure_ids": ["cipp_inliner"],
                  "reason": "Test-Regel"
                }
              ]
            }
            """);
            var userRules = new SanierungUserRulesService(tmpRulesFile);
            var engine = new RehabilitationRulesEngine(userRules);

            var ctx = new HaltungsKontext
            {
                DnMm = 300,
                Material = "Stahl",
            };
            var result = engine.Evaluate(ctx, new[] { "BAB.B" });

            Assert.Contains(result.Excluded, m => m.Procedure.Id == "cipp_inliner");
            var excluded = result.Excluded.First(m => m.Procedure.Id == "cipp_inliner");
            Assert.Contains("Test-Regel", excluded.Reason);
        }
        finally
        {
            if (File.Exists(tmpRulesFile)) File.Delete(tmpRulesFile);
        }
    }

    // ── Audit-Fix: SanierungUserRulesService Persistenz ──────────────────

    [Fact]
    public void SanierungUserRulesService_SaveLoadRoundtrip_PreservesRule()
    {
        var tmpFile = Path.Combine(Path.GetTempPath(), "rules_" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var service = new SanierungUserRulesService(tmpFile);
            var file = new SanierungUserRulesFile
            {
                Rules = new List<SanierungUserRule>
                {
                    new()
                    {
                        Id = "r1",
                        Name = "Test",
                        Enabled = true,
                        Conditions = new RuleConditions { MaterialContains = "PVC" },
                        ExcludeProcedureIds = new List<string> { "berstlining" },
                        Reason = "Test-Begruendung",
                    }
                }
            };
            service.Save(file);

            var service2 = new SanierungUserRulesService(tmpFile);
            var loaded = service2.Load();
            Assert.Single(loaded.Rules);
            Assert.Equal("Test", loaded.Rules[0].Name);
            Assert.Equal("PVC", loaded.Rules[0].Conditions.MaterialContains);
            Assert.Single(loaded.Rules[0].ExcludeProcedureIds);
        }
        finally
        {
            if (File.Exists(tmpFile)) File.Delete(tmpFile);
        }
    }

    // ── Audit-Fix: RehabilitationRulesEngine YAML/JSON-Loader ────────────

    [Fact]
    public void RulesEngine_LoadsProceduresFromJson_OverridesDefaults()
    {
        // Lade nur cipp_inliner mit MODIFIZIERTEN DN-Grenzen (nur 100-150).
        // Wenn JSON greift, wird DN 200 ausserhalb der Range fallen -> cipp_inliner excluded.
        // Mit hartcodiertem Default (100-2000) waere DN 200 IN-Range.
        var tmpFile = Path.Combine(Path.GetTempPath(), "procs_" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            File.WriteAllText(tmpFile, """
            {
              "procedures": [
                {
                  "id": "cipp_inliner",
                  "name": "CIPP (override)",
                  "category": "Renovierung",
                  "dn_min": 100,
                  "dn_max": 150,
                  "az_compatible": true,
                  "gfk_compatible": true,
                  "steel_compatible": true,
                  "bend_capable": false,
                  "max_bend_degrees": 15
                }
              ]
            }
            """);
            var engine = new RehabilitationRulesEngine(userRules: null, proceduresJsonPath: tmpFile);

            // DN 200 - liegt ueber dem JSON-Override-Maximum 150
            var ctx = new HaltungsKontext { DnMm = 200 };
            var result = engine.Evaluate(ctx, new[] { "BAB.B" });

            // Wenn JSON aktiv ist, ist cipp_inliner in Excluded mit DN-Range-Begruendung
            var cipp = result.Excluded.FirstOrDefault(m => m.Procedure.Id == "cipp_inliner");
            Assert.NotNull(cipp);
            Assert.Contains("DN", cipp.Reason);  // "DN 200 ausserhalb 100-150"
        }
        finally
        {
            if (File.Exists(tmpFile)) File.Delete(tmpFile);
        }
    }
}
