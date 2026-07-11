using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Sanierung;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Schatten;
using AuswertungPro.Next.Application.Vsa;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Vsa;
using AuswertungPro.Next.Infrastructure.Schatten;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Schatten;

/// <summary>
/// Kernvertraege der Schattenauswertung: (1) Das Original-Projekt bleibt unveraendert,
/// selbst wenn die VSA-Bewertung den uebergebenen Record aggressiv mutiert (beweist die
/// Klon-Uebergabe). (2) KI-Fehler/Fallback zerstoeren die Regelwerte nicht. (3) Haltungen
/// ohne Codierung erreichen die KI gar nicht.
/// </summary>
public sealed class SchattenAuswertungServiceTests
{
    // Fake-VSA, die sich wie die echte verhaelt: schreibt Noten in den UEBERGEBENEN Record.
    private sealed class MutierendeVsa : IVsaEvaluationService
    {
        public Result<IReadOnlyList<VsaConditionResult>> Evaluate(Project project)
            => Result<IReadOnlyList<VsaConditionResult>>.Success(Array.Empty<VsaConditionResult>());

        public Result<bool> EvaluateRecord(HaltungRecord record)
        {
            record.SetFieldValue("VSA_Zustandsnote_D", "3.4", FieldSource.Manual, false);
            record.SetFieldValue("Zustandsklasse", "4", FieldSource.Manual, false);
            return Result<bool>.Success(true);
        }

        public Result<string> Explain(Project project, HaltungRecord record)
            => Result<string>.Success("");
    }

    private sealed class FixeMassnahmen : IMeasureRecommendationService
    {
        public MeasureRecommendationResult Recommend(HaltungRecord record, int maxSuggestions = 5)
            => new(new[] { "Schlauchliner" }, 12000m, null, null, null, null, null, 3, false);
        public MeasureLearningStats GetStats() => new(0, 0, 0, false, null, null, "");
        public MeasureModelTrainingResult TrainModel(int minSamples = 25) => new(false, 0, minSamples, "", null, null);
        public bool Learn(HaltungRecord record) => false;
    }

    private sealed class SkriptKi : IAiSanierungOptimizationService
    {
        public int Aufrufe;
        public Func<SanierungOptimizationRequest, SanierungOptimizationResult>? Antwort;
        public Task<SanierungOptimizationResult> OptimizeAsync(SanierungOptimizationRequest request, CancellationToken ct)
        {
            Aufrufe++;
            return Task.FromResult(Antwort?.Invoke(request) ?? new SanierungOptimizationResult
            {
                RecommendedMeasure = "Schlauchliner (GFK)",
                Confidence = 0.8,
                CostEstimate = new CostBand { Min = 9000m, Expected = 12500m, Max = 16000m },
                Reasoning = "Begruendung"
            });
        }
    }

    private static Project ProjektMit(params HaltungRecord[] records)
    {
        var p = new Project();
        foreach (var r in records) p.Data.Add(r);
        return p;
    }

    private static HaltungRecord Haltung(string name, bool mitCodierung)
    {
        var r = new HaltungRecord();
        r.SetFieldValue("Haltungsname", name, FieldSource.Xtf, false);
        r.SetFieldValue("Zustandsklasse", "2", FieldSource.Manual, userEdited: true); // Mensch-Wert
        if (mitCodierung)
            r.VsaFindings.Add(new VsaFinding { KanalSchadencode = "BAB", Quantifizierung1 = "5" });
        return r;
    }

    [Fact]
    public async Task Lauf_LaesstOriginalVollstaendigUnveraendert_UndLiefertSchattenWerte()
    {
        var record = Haltung("H1", mitCodierung: true);
        var felderVorher = new Dictionary<string, string>(record.Fields);
        var modifiedVorher = record.ModifiedAtUtc;

        var service = new SchattenAuswertungService(new MutierendeVsa(), new FixeMassnahmen(), ki: null);
        var store = await service.BerechneAsync(ProjektMit(record), mitKi: false, null, null, CancellationToken.None);

        // Original: byte-gleich (die Fake-VSA HAT geschrieben — aber nur auf den Klon).
        Assert.Equal(felderVorher, record.Fields);
        Assert.Equal(modifiedVorher, record.ModifiedAtUtc);
        Assert.Equal("2", record.GetFieldValue("Zustandsklasse"));

        // Schatten-Ergebnis traegt die gerechneten Werte.
        var ergebnis = store.ByHaltung["H1"];
        Assert.Equal(SchattenStatus.NurRegeln, ergebnis.Status);
        Assert.Equal("4", ergebnis.Zustandsklasse);
        Assert.Equal("3.4", ergebnis.NoteD);
        Assert.Equal(new[] { "Schlauchliner" }, ergebnis.RegelMassnahmen);
        Assert.Equal(12000m, ergebnis.RegelKosten);
    }

    [Fact]
    public async Task KiErfolg_SetztStatusMitKi_KiFehlerBehaeltRegelwerte()
    {
        var gut = Haltung("OK", mitCodierung: true);
        var schlecht = Haltung("KAPUTT", mitCodierung: true);
        var ki = new SkriptKi
        {
            Antwort = req => req.HaltungId == "KAPUTT"
                ? throw new InvalidOperationException("Ollama nicht erreichbar")
                : new SanierungOptimizationResult
                {
                    RecommendedMeasure = "Schlauchliner (GFK)",
                    Confidence = 0.8,
                    CostEstimate = new CostBand { Min = 9000m, Expected = 12500m, Max = 16000m },
                    Reasoning = "passt"
                }
        };

        var service = new SchattenAuswertungService(new MutierendeVsa(), new FixeMassnahmen(), ki);
        var store = await service.BerechneAsync(ProjektMit(gut, schlecht), mitKi: true, null, null, CancellationToken.None);

        var ok = store.ByHaltung["OK"];
        Assert.Equal(SchattenStatus.MitKi, ok.Status);
        Assert.Equal("Schlauchliner (GFK)", ok.KiMassnahme);
        Assert.Equal(12500m, ok.KostenErwartet);

        var kaputt = store.ByHaltung["KAPUTT"];
        Assert.Equal(SchattenStatus.KiFallback, kaputt.Status);
        Assert.Contains("Ollama", kaputt.KiFehler);
        Assert.Equal(new[] { "Schlauchliner" }, kaputt.RegelMassnahmen); // Regelwerte ueberleben
        Assert.Equal(12000m, kaputt.RegelKosten);
    }

    [Fact]
    public async Task OhneCodierung_WirdAusgewiesen_UndErreichtDieKiNicht()
    {
        var leer = Haltung("LEER", mitCodierung: false);
        leer.SetFieldValue("Zustandsklasse", "", FieldSource.Manual, true);
        var ki = new SkriptKi();

        var service = new SchattenAuswertungService(new MutierendeVsa(), new FixeMassnahmen(), ki);
        var store = await service.BerechneAsync(ProjektMit(leer), mitKi: true, null, null, CancellationToken.None);

        Assert.Equal(SchattenStatus.OhneCodierung, store.ByHaltung["LEER"].Status);
        Assert.Equal(0, ki.Aufrufe);
    }

    [Fact]
    public async Task Zwischenspeichern_WirdNachPhase1UndJeKiHaltungGerufen()
    {
        var ki = new SkriptKi();
        var service = new SchattenAuswertungService(new MutierendeVsa(), new FixeMassnahmen(), ki);
        var aufrufe = 0;

        await service.BerechneAsync(
            ProjektMit(Haltung("A", true), Haltung("B", true)),
            mitKi: true,
            fortschritt: null,
            zwischenspeichern: _ => aufrufe++,
            CancellationToken.None);

        Assert.Equal(3, aufrufe); // 1x nach Phase 1 + 2x je KI-Haltung
    }
}
