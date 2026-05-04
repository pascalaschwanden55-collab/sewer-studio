using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AuswertungPro.Next.Infrastructure.Sanierung;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Phase 2.5: Tests fuer RehabilitationRulesEngine.
///
/// Verifiziert:
///   - Hardcode-Fallback funktioniert wenn KEIN JSON-Pfad gesetzt ist
///   - JSON wird als fuehrende Quelle gelesen (Procedures, damage_groups, damage_matrix)
///   - Harte Regeln greifen: cipp_inliner bei breaks ausgeschlossen
///   - DN-Range-Check, Material-Check, Bogen-Check
///   - Fallback bei kaputtem JSON
/// </summary>
public sealed class RehabilitationRulesEngineTests : IDisposable
{
    private readonly string _tempDir;

    public RehabilitationRulesEngineTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(),
            $"sewer_rehab_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true); }
        catch { /* best effort */ }
    }

    // ── Harte Regeln (Hardcode-Fallback aktiv) ──────────────────────

    [Fact]
    public void HardcodeFallback_CippBeiBreak_IstAusgeschlossen()
    {
        // Ohne JSON-Pfad -> Hardcode-Defaults
        var engine = new RehabilitationRulesEngine();
        var ctx = new HaltungsKontext { DnMm = 300, Material = "Beton" };
        var codes = new[] { "BAC" }; // Bruch -> breaks-Gruppe

        var eval = engine.Evaluate(ctx, codes);

        // CIPP muss in Excluded sein, NICHT in Eligible
        Assert.DoesNotContain(eval.Eligible, e => e.Procedure.Id == "cipp_inliner");
        Assert.Contains(eval.Excluded, e => e.Procedure.Id == "cipp_inliner");
    }

    [Fact]
    public void HardcodeFallback_DnAusserhalbRange_AusgeschlossenMitBegruendung()
    {
        var engine = new RehabilitationRulesEngine();
        // shaft_rehabilitation: dn_min=800. Bei DN=300 muss es ausgeschlossen sein.
        var ctx = new HaltungsKontext { DnMm = 300 };
        var codes = new[] { "BAF" }; // corrosion

        var eval = engine.Evaluate(ctx, codes);

        var shaft = eval.Excluded.FirstOrDefault(e => e.Procedure.Id == "shaft_rehabilitation");
        Assert.NotNull(shaft);
        Assert.Contains("DN", shaft!.Reason);
    }

    [Fact]
    public void HardcodeFallback_AsbestzementMaterial_BlocktInkompatibleVerfahren()
    {
        var engine = new RehabilitationRulesEngine();
        var ctx = new HaltungsKontext { DnMm = 300, Material = "Asbestzement" };
        var codes = new[] { "BAB" }; // cracks

        var eval = engine.Evaluate(ctx, codes);

        // robotic_milling hat az_compatible=false
        var robo = eval.Excluded.FirstOrDefault(e => e.Procedure.Id == "robotic_milling");
        Assert.NotNull(robo);
        Assert.Contains("Asbest", robo!.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void HardcodeFallback_StarkerBogen_BlocktNichtBogengaengigeLiner()
    {
        var engine = new RehabilitationRulesEngine();
        var ctx = new HaltungsKontext { DnMm = 300, Material = "Beton", HasBendSevere = true };
        var codes = new[] { "BAB" }; // cracks

        var eval = engine.Evaluate(ctx, codes);

        // CIPP hat BendCapable=false -> bei starkem Bogen blockiert
        var cipp = eval.Excluded.FirstOrDefault(e => e.Procedure.Id == "cipp_inliner");
        Assert.NotNull(cipp);
        Assert.Contains("Bogen", cipp!.Reason, StringComparison.OrdinalIgnoreCase);
    }

    // ── JSON als fuehrende Quelle ────────────────────────────────────

    [Fact]
    public void JsonLeadsHardcode_NeueProcedureAusJson_WirdGeladen()
    {
        var jsonPath = Path.Combine(_tempDir, "rehab.json");
        File.WriteAllText(jsonPath, /*lang=json,strict*/ """
            {
              "procedures": [
                {
                  "id": "test_only_proc",
                  "name": "Nur-Test-Verfahren",
                  "category": "Reparatur",
                  "dn_min": 100,
                  "dn_max": 1000,
                  "az_compatible": true,
                  "gfk_compatible": true,
                  "steel_compatible": true,
                  "bend_capable": true,
                  "max_bend_degrees": 30
                }
              ],
              "damage_groups_by_vsa_code": {
                "BAB": "cracks"
              },
              "damage_matrix": {
                "test_only_proc": { "cracks": "eligible" }
              }
            }
            """);

        var engine = new RehabilitationRulesEngine(proceduresJsonPath: jsonPath);
        var ctx = new HaltungsKontext { DnMm = 300, Material = "Beton" };
        var codes = new[] { "BAB" };

        var eval = engine.Evaluate(ctx, codes);

        // Nur das eine Verfahren aus JSON ist geladen
        var allProcIds = eval.Eligible.Concat(eval.Conditional).Concat(eval.Excluded)
            .Select(m => m.Procedure.Id).Distinct().ToList();
        Assert.Single(allProcIds);
        Assert.Contains("test_only_proc", allProcIds);
        Assert.Contains(eval.Eligible, e => e.Procedure.Id == "test_only_proc");
    }

    [Fact]
    public void JsonLeadsHardcode_DamageMatrixAusJson_WirdRespektiert()
    {
        // Override: cipp_inliner erlaubt breaks (anders als Hardcode!)
        var jsonPath = Path.Combine(_tempDir, "override.json");
        File.WriteAllText(jsonPath, /*lang=json,strict*/ """
            {
              "procedures": [
                {
                  "id": "cipp_inliner",
                  "name": "CIPP-Schlauchlining (Inliner)",
                  "category": "Renovierung",
                  "dn_min": 100,
                  "dn_max": 2000,
                  "az_compatible": true,
                  "gfk_compatible": true,
                  "steel_compatible": true,
                  "bend_capable": true,
                  "max_bend_degrees": 30
                }
              ],
              "damage_groups_by_vsa_code": {
                "BAC": "breaks"
              },
              "damage_matrix": {
                "cipp_inliner": { "breaks": "eligible" }
              }
            }
            """);

        var engine = new RehabilitationRulesEngine(proceduresJsonPath: jsonPath);
        var ctx = new HaltungsKontext { DnMm = 300, Material = "Beton" };
        var codes = new[] { "BAC" };

        var eval = engine.Evaluate(ctx, codes);

        // ACHTUNG: Auch bei eligible-Override bleibt der hardgecodete Step 6
        // ("Bruch / Einsturz: nur Erneuerung") in der Engine — d.h. CIPP wird
        // weiterhin ausgeschlossen, aber nicht mehr durch die Matrix.
        // Stattdessen verifizieren wir dass "Schadensgruppe 'breaks' bei
        // CIPP explizit ausgeschlossen" NICHT erscheint (Matrix sagt eligible).
        var cipp = eval.Excluded.FirstOrDefault(e => e.Procedure.Id == "cipp_inliner");
        if (cipp is not null)
        {
            // Falls ausgeschlossen, dann nicht wegen Matrix-Status excluded
            Assert.DoesNotContain("explizit ausgeschlossen", cipp.Reason);
        }
    }

    [Fact]
    public void JsonLeadsHardcode_DamageGroupMappingAusJson_WirdRespektiert()
    {
        // Override: BAB -> "fancy_group" statt "cracks"
        var jsonPath = Path.Combine(_tempDir, "groupmap.json");
        File.WriteAllText(jsonPath, /*lang=json,strict*/ """
            {
              "procedures": [
                {
                  "id": "test_proc",
                  "name": "Test",
                  "category": "Reparatur",
                  "dn_min": 100,
                  "dn_max": 1000,
                  "az_compatible": true,
                  "gfk_compatible": true,
                  "steel_compatible": true,
                  "bend_capable": true,
                  "max_bend_degrees": 30
                }
              ],
              "damage_groups_by_vsa_code": {
                "BAB": "fancy_group"
              },
              "damage_matrix": {
                "test_proc": { "fancy_group": "eligible" }
              }
            }
            """);

        var engine = new RehabilitationRulesEngine(proceduresJsonPath: jsonPath);
        var ctx = new HaltungsKontext { DnMm = 300, Material = "Beton" };
        var codes = new[] { "BAB" };

        var eval = engine.Evaluate(ctx, codes);

        // Damage-Group muss "fancy_group" enthalten (aus JSON)
        Assert.Contains("fancy_group", eval.DamageGroups);
        // Verfahren ist eligible weil Matrix das so sagt
        Assert.Contains(eval.Eligible, e => e.Procedure.Id == "test_proc");
    }

    // ── Defensive Fallbacks ──────────────────────────────────────────

    [Fact]
    public void KaputtesJson_FaelltAufHardcodeZurueck()
    {
        var jsonPath = Path.Combine(_tempDir, "broken.json");
        File.WriteAllText(jsonPath, "{ this is not valid json");

        var engine = new RehabilitationRulesEngine(proceduresJsonPath: jsonPath);
        var ctx = new HaltungsKontext { DnMm = 300, Material = "Beton" };
        var codes = new[] { "BAB" };

        var eval = engine.Evaluate(ctx, codes);

        // Hardcode-Fallback hat 10 Verfahren -> mind. 1 in einer der Listen
        var totalProcs = eval.Eligible.Count + eval.Conditional.Count + eval.Excluded.Count;
        Assert.True(totalProcs >= 5, $"Erwarte mind. 5 Verfahren aus Hardcode-Fallback, gefunden: {totalProcs}");
    }

    [Fact]
    public void NichtExistierendesJson_NutztHardcode()
    {
        var engine = new RehabilitationRulesEngine(
            proceduresJsonPath: Path.Combine(_tempDir, "does_not_exist.json"));
        var ctx = new HaltungsKontext { DnMm = 300, Material = "Beton" };
        var codes = new[] { "BAB" };

        var eval = engine.Evaluate(ctx, codes);

        // Hardcode-Default hat cipp_inliner als eligible bei cracks (BAB=cracks)
        Assert.Contains(eval.Eligible, e => e.Procedure.Id == "cipp_inliner");
    }

    [Fact]
    public void JsonOhneDamageMatrix_NutztHardcodeMatrix()
    {
        // JSON liefert nur procedures, keine damage_matrix oder damage_groups
        var jsonPath = Path.Combine(_tempDir, "minimal.json");
        File.WriteAllText(jsonPath, /*lang=json,strict*/ """
            {
              "procedures": [
                {
                  "id": "cipp_inliner",
                  "name": "CIPP-Schlauchlining",
                  "category": "Renovierung",
                  "dn_min": 100,
                  "dn_max": 2000,
                  "az_compatible": true,
                  "gfk_compatible": true,
                  "steel_compatible": true,
                  "bend_capable": false,
                  "max_bend_degrees": 15
                }
              ]
            }
            """);

        var engine = new RehabilitationRulesEngine(proceduresJsonPath: jsonPath);
        var ctx = new HaltungsKontext { DnMm = 300, Material = "Beton" };
        var codes = new[] { "BAC" }; // breaks via Hardcode-Mapping

        var eval = engine.Evaluate(ctx, codes);

        // Ohne damage_groups in JSON: Hardcode-Mapping greift -> "breaks"
        Assert.Contains("breaks", eval.DamageGroups);
        // Ohne damage_matrix in JSON: Hardcode-Matrix sagt cipp+breaks=excluded
        Assert.Contains(eval.Excluded, e => e.Procedure.Id == "cipp_inliner");
    }

    // ── Konfig-File aus dem Repo wird sauber geladen ────────────────

    [Fact]
    public void RepoJsonFile_LaedtErfolgreich_HatErwarteteVerfahren()
    {
        // Nutzt das tatsaechliche src/AuswertungPro.Next.UI/Config/rehabilitation_methods.json
        // — verifiziert dass das Repo-File mit Phase-2.5-Schema (damage_groups,
        // damage_matrix) noch funktioniert.
        var repoJson = LocateRepoJson();
        if (repoJson is null) return; // Skip wenn Repo-Layout anders aussieht

        var engine = new RehabilitationRulesEngine(proceduresJsonPath: repoJson);
        var ctx = new HaltungsKontext { DnMm = 300, Material = "Beton" };
        var codes = new[] { "BAB" }; // cracks

        var eval = engine.Evaluate(ctx, codes);

        // 10 Verfahren erwartet
        var allIds = eval.Eligible.Concat(eval.Conditional).Concat(eval.Excluded)
            .Select(m => m.Procedure.Id).Distinct().ToList();
        Assert.Equal(10, allIds.Count);

        // CIPP muss bei cracks eligible sein
        Assert.Contains(eval.Eligible, e => e.Procedure.Id == "cipp_inliner");
    }

    private static string? LocateRepoJson()
    {
        // tests/.../bin/Debug/.../*.dll  ->  ../../../../../src/AuswertungPro.Next.UI/Config/rehabilitation_methods.json
        var here = AppContext.BaseDirectory;
        for (var i = 0; i < 8; i++)
        {
            var candidate = Path.Combine(here, "src", "AuswertungPro.Next.UI",
                "Config", "rehabilitation_methods.json");
            if (File.Exists(candidate)) return candidate;
            here = Path.GetFullPath(Path.Combine(here, ".."));
        }
        return null;
    }
}
