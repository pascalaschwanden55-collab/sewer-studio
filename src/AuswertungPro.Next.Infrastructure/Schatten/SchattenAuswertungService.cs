using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Sanierung;
using AuswertungPro.Next.Application.Schatten;
using AuswertungPro.Next.Application.Vsa;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Schatten;

/// <summary>
/// Rechnet die eigenstaendige Parallel-Auswertung (Schattenauswertung) fuer ein Projekt.
///
/// Read-only-Garantie: Die VSA-Bewertung schreibt ihre Resultate in den uebergebenen
/// Record — deshalb bekommt sie hier IMMER nur eine Tiefkopie (HaltungRecordCloner);
/// abgelesen wird vom Klon. Massnahmen-Empfehlung und LLM-Optimierung sind rein lesend
/// und duerfen direkt aufs Original.
///
/// Hybrid-Ablauf: Phase 1 (Regeln, alle Haltungen, schnell) -> zwischenspeichern ->
/// Phase 2 (LLM je Haltung, sequenziell, Timeout je Haltung, nach jeder Haltung
/// zwischenspeichern). Abbruch liefert den bis dahin gerechneten Teil-Store.
/// </summary>
public sealed class SchattenAuswertungService : ISchattenAuswertungService
{
    // Zeitbudget je LLM-Haltung: eine haengende Ollama-Anfrage darf nicht den Rest blockieren.
    private static readonly TimeSpan KiTimeoutJeHaltung = TimeSpan.FromSeconds(90);

    private readonly IVsaEvaluationService _vsa;
    private readonly IMeasureRecommendationService _massnahmen;
    private readonly IAiSanierungOptimizationService? _ki;
    private readonly string? _kiModellName;

    public SchattenAuswertungService(
        IVsaEvaluationService vsa,
        IMeasureRecommendationService massnahmen,
        IAiSanierungOptimizationService? ki,
        string? kiModellName = null)
    {
        _vsa = vsa ?? throw new ArgumentNullException(nameof(vsa));
        _massnahmen = massnahmen ?? throw new ArgumentNullException(nameof(massnahmen));
        _ki = ki;
        _kiModellName = kiModellName;
    }

    public async Task<SchattenAuswertungStore> BerechneAsync(
        Project projekt,
        bool mitKi,
        IProgress<SchattenFortschritt>? fortschritt,
        Action<SchattenAuswertungStore>? zwischenspeichern,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(projekt);

        // Snapshot: ObservableCollection nie waehrend eines Hintergrundlaufs enumerieren.
        var haltungen = projekt.Data.ToList();

        var store = new SchattenAuswertungStore
        {
            LetzterLaufUtc = DateTime.UtcNow,
            KiModell = mitKi ? _kiModellName : null
        };

        // ── Phase 1: Regelteil (CPU + Klassifikationstabellen-I/O) — off-UI-thread ──
        await Task.Run(() =>
        {
            for (var i = 0; i < haltungen.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var record = haltungen[i];
                var name = record.GetFieldValue("Haltungsname");
                if (string.IsNullOrWhiteSpace(name))
                    name = record.Id.ToString();

                fortschritt?.Report(new SchattenFortschritt("Regeln", i + 1, haltungen.Count, name));
                store.ByHaltung[name] = BerechneRegelteil(record, name);
            }
        }, ct).ConfigureAwait(false);

        zwischenspeichern?.Invoke(store);

        // ── Phase 2: LLM je Haltung (sequenziell, abbruch- und timeoutfest) ──
        if (mitKi && _ki is not null)
        {
            var kandidaten = haltungen
                .Select(r => (Record: r, Name: KeyOf(r)))
                .Where(x => store.ByHaltung.TryGetValue(x.Name, out var e) && e.Status != SchattenStatus.OhneCodierung)
                .ToList();

            for (var i = 0; i < kandidaten.Count; i++)
            {
                if (ct.IsCancellationRequested)
                    break; // Teil-Store zurueckgeben — bereits Gerechnetes bleibt erhalten

                var (record, name) = kandidaten[i];
                fortschritt?.Report(new SchattenFortschritt("KI", i + 1, kandidaten.Count, name));

                var ergebnis = store.ByHaltung[name];
                store.ByHaltung[name] = await BerechneKiTeilAsync(record, ergebnis, ct).ConfigureAwait(false);
                zwischenspeichern?.Invoke(store);
            }
        }

        return store;
    }

    private SchattenHaltungErgebnis BerechneRegelteil(HaltungRecord record, string name)
    {
        var hash = SchattenCodierungsHash.Compute(record);

        var hatCodierung = record.VsaFindings.Count > 0
            || !string.IsNullOrWhiteSpace(record.GetFieldValue("Primaere_Schaeden"))
            || record.Protocol?.Current?.Entries.Any(e => !e.IsDeleted && !string.IsNullOrWhiteSpace(e.Code)) == true;

        if (!hatCodierung)
        {
            return new SchattenHaltungErgebnis
            {
                Haltung = name,
                CodierungsHash = hash,
                BerechnetUtc = DateTime.UtcNow,
                Status = SchattenStatus.OhneCodierung
            };
        }

        // Zustand: auf der Tiefkopie rechnen, vom Klon ablesen — Original bleibt still.
        var klon = HaltungRecordCloner.CloneForEvaluation(record);
        var vsaOk = false;
        try
        {
            vsaOk = _vsa.EvaluateRecord(klon).Ok;
        }
        catch
        {
            // Bewertungsfehler einer Haltung darf den Lauf nicht stoppen — Noten bleiben leer.
        }

        // Massnahmen: rein lesende Empfehlung direkt vom Original.
        MeasureRecommendationResult empfehlung;
        try
        {
            empfehlung = _massnahmen.Recommend(record, maxSuggestions: 5);
        }
        catch
        {
            empfehlung = MeasureRecommendationResult.Empty;
        }

        return new SchattenHaltungErgebnis
        {
            Haltung = name,
            CodierungsHash = hash,
            BerechnetUtc = DateTime.UtcNow,
            Status = SchattenStatus.NurRegeln,
            NoteD = LeereAlsNull(klon.GetFieldValue("VSA_Zustandsnote_D")),
            NoteS = LeereAlsNull(klon.GetFieldValue("VSA_Zustandsnote_S")),
            NoteB = LeereAlsNull(klon.GetFieldValue("VSA_Zustandsnote_B")),
            Zustandsklasse = vsaOk ? LeereAlsNull(klon.GetFieldValue("Zustandsklasse")) : null,
            Geschaetzt = string.Equals(klon.GetFieldValue("VSA_Geschaetzt"), "Ja", StringComparison.OrdinalIgnoreCase),
            RegelMassnahmen = empfehlung.Measures,
            RegelKosten = empfehlung.EstimatedTotalCost,
            AehnlicheFaelle = empfehlung.SimilarCasesCount,
            RegelModellGenutzt = empfehlung.UsedTrainedModel
        };
    }

    private async Task<SchattenHaltungErgebnis> BerechneKiTeilAsync(
        HaltungRecord record,
        SchattenHaltungErgebnis basis,
        CancellationToken ct)
    {
        var rule = new RuleRecommendationDto
        {
            Measures = basis.RegelMassnahmen,
            EstimatedCost = basis.RegelKosten,
            UsedTrainedModel = basis.RegelModellGenutzt
        };
        var request = SchattenLlmRequestBuilder.Build(record, basis.Zustandsklasse, rule);

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(KiTimeoutJeHaltung);

            var result = await _ki!.OptimizeAsync(request, timeoutCts.Token).ConfigureAwait(false);

            return basis with
            {
                BerechnetUtc = DateTime.UtcNow,
                Status = result.IsFallback ? SchattenStatus.KiFallback : SchattenStatus.MitKi,
                KiMassnahme = LeereAlsNull(result.RecommendedMeasure),
                KiConfidence = result.Confidence,
                KostenMin = result.CostEstimate.Min,
                KostenErwartet = result.CostEstimate.Expected,
                KostenMax = result.CostEstimate.Max,
                KiBegruendung = LeereAlsNull(result.Reasoning),
                RisikoFlags = result.RiskFlags,
                IsFallback = result.IsFallback,
                KiFehler = result.Error
            };
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Nutzer-Abbruch: Regelwerte behalten, Status unveraendert lassen.
            return basis;
        }
        catch (OperationCanceledException)
        {
            // Timeout dieser einen Haltung: weiter mit der naechsten.
            return basis with
            {
                Status = SchattenStatus.KiFallback,
                IsFallback = true,
                KiFehler = $"Zeitueberschreitung nach {KiTimeoutJeHaltung.TotalSeconds:0}s"
            };
        }
        catch (Exception ex)
        {
            return basis with
            {
                Status = SchattenStatus.KiFallback,
                IsFallback = true,
                KiFehler = ex.Message
            };
        }
    }

    private static string KeyOf(HaltungRecord record)
    {
        var name = record.GetFieldValue("Haltungsname");
        return string.IsNullOrWhiteSpace(name) ? record.Id.ToString() : name;
    }

    private static string? LeereAlsNull(string? s)
        => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
