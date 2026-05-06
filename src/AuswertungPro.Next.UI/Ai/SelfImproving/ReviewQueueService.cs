using System;
using AuswertungPro.Next.Application.Ai.QualityGate;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AuswertungPro.Next.UI.Ai.QualityGate;
using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Ai.SelfImproving;

/// <summary>
/// Manages the review queue for Yellow-TrafficLight detections.
/// Priority = 0.6 × EpistemicUncertainty + 0.4 × Closeness-to-0.5
/// Higher priority = more urgent for human review.
///
/// K1 (V4.2 Gesamt-Audit): Self-Training-Items werden via <see cref="ReviewQueueStore"/>
/// persistiert, damit die Queue einen App-Neustart ueberlebt. Yellow-Zone-Items (mit
/// <see cref="MappedProtocolEntry"/>) bleiben transient — sie entstehen in jedem
/// Pipeline-Run neu.
/// </summary>
public sealed class ReviewQueueService
{
    private readonly List<ReviewQueueItem> _queue = new();
    private readonly object _lock = new();

    /// <summary>Wenn false, wird nicht persistiert (z.B. in Unit-Tests).</summary>
    private readonly bool _persistEnabled;
    private bool _loaded;

    /// <summary>Default: Persistenz aktiv. Fuer Tests via Parameter deaktivierbar.</summary>
    public ReviewQueueService(bool persistEnabled = true)
    {
        _persistEnabled = persistEnabled;
    }

    /// <summary>
    /// Laedt persistierte Self-Training-Items lazy beim ersten Zugriff.
    /// Idempotent, Thread-safe, blockierend-synchron (Datei typischerweise &lt; 100 KB).
    /// </summary>
    private void EnsureLoaded()
    {
        if (_loaded || !_persistEnabled) return;
        lock (_lock)
        {
            if (_loaded) return;
            try
            {
                // ConfigureAwait(false) im Store → kein Deadlock-Risiko im UI-Thread.
                var persisted = ReviewQueueStore.LoadAsync().GetAwaiter().GetResult();
                foreach (var item in persisted)
                {
                    if (!string.IsNullOrEmpty(item.SelfTrainingSampleId)
                        && _queue.Any(q => q.SelfTrainingSampleId == item.SelfTrainingSampleId))
                        continue;
                    _queue.Add(item);
                }
                _queue.Sort((a, b) => b.Priority.CompareTo(a.Priority));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[ReviewQueueService] Load fehlgeschlagen: {ex.Message}");
            }
            finally
            {
                _loaded = true;
            }
        }
    }

    /// <summary>Snapshot der aktuellen Queue fuer Persistenz-Save.</summary>
    private List<ReviewQueueItem> SnapshotForSave()
    {
        lock (_lock) return _queue.ToList();
    }

    /// <summary>Fire-and-forget Save nach jeder Mutation. Exceptions werden geschluckt.</summary>
    private void SaveInBackground()
    {
        if (!_persistEnabled) return;
        var snapshot = SnapshotForSave();
        _ = Task.Run(async () =>
        {
            try
            {
                await ReviewQueueStore.SaveAsync(snapshot).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[ReviewQueueService] Save fehlgeschlagen: {ex.Message}");
            }
        });
    }

    /// <summary>Add a detection to the review queue if it's Yellow zone.</summary>
    public void Enqueue(MappedProtocolEntry entry)
    {
        if (entry.QualityGateResult is not { IsYellow: true }) return;
        EnsureLoaded();

        var priority = ComputePriority(entry);
        var item = new ReviewQueueItem(
            Id: Guid.NewGuid().ToString(),
            Entry: entry,
            Priority: priority,
            EnqueuedUtc: DateTime.UtcNow);

        lock (_lock)
        {
            _queue.Add(item);
            _queue.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        }
        // Yellow-Items werden nicht persistiert (Store filtert sie aus).
    }

    /// <summary>Add multiple detections to the queue.</summary>
    public void EnqueueRange(IEnumerable<MappedProtocolEntry> entries)
    {
        foreach (var entry in entries)
            Enqueue(entry);
    }

    /// <summary>
    /// Fuegt ein Self-Training-Ergebnis in die Review Queue ein.
    /// Fuer PartialMatch/Mismatch-Ergebnisse die menschliche Pruefung benoetigen.
    /// </summary>
    /// <param name="sampleId">Stabiler SampleId fuer eindeutiges Mapping bei Review-Korrektur.</param>
    public void EnqueueFromSelfTraining(
        string caseId, string vsaCode, string suggestedCode,
        double meter, string framePath, string matchLevel,
        string sampleId,
        double? priorityOverride = null)
    {
        EnsureLoaded();

        // V4.2 Phase 1.4: Optionaler Priority-Override vom UncertaintySamplingService.
        // Sonst Fallback auf MatchLevel-basierte Heuristik.
        double priority = priorityOverride ?? matchLevel switch
        {
            MatchLevelNames.Mismatch => 0.9,
            MatchLevelNames.PartialMatch => 0.6,
            _ => 0.3
        };

        var item = new ReviewQueueItem(
            Id: Guid.NewGuid().ToString(),
            Entry: null!,
            Priority: priority,
            EnqueuedUtc: DateTime.UtcNow)
        {
            SelfTrainingCaseId = caseId,
            SelfTrainingVsaCode = vsaCode,
            SelfTrainingSuggestedCode = suggestedCode,
            SelfTrainingMeter = meter,
            SelfTrainingFramePath = framePath,
            SelfTrainingMatchLevel = matchLevel,
            SelfTrainingSampleId = sampleId
        };

        bool added;
        lock (_lock)
        {
            // Deduplizierung: gleiches Sample nicht doppelt einspeisen
            // (Orchestrator und ViewModel koennten denselben Kandidaten einspeisen)
            if (!string.IsNullOrEmpty(sampleId)
                && _queue.Any(q => q.SelfTrainingSampleId == sampleId))
            {
                added = false;
            }
            else
            {
                _queue.Add(item);
                _queue.Sort((a, b) => b.Priority.CompareTo(a.Priority));
                added = true;
            }
        }

        if (added) SaveInBackground();
    }

    /// <summary>Get all items sorted by priority (highest first).</summary>
    public IReadOnlyList<ReviewQueueItem> GetAll()
    {
        EnsureLoaded();
        lock (_lock) return _queue.ToList();
    }

    /// <summary>Get the next N items for review.</summary>
    public IReadOnlyList<ReviewQueueItem> GetTop(int count)
    {
        EnsureLoaded();
        lock (_lock) return _queue.Take(count).ToList();
    }

    /// <summary>Remove a reviewed item from the queue.</summary>
    public bool Remove(string itemId)
    {
        EnsureLoaded();
        bool removed;
        lock (_lock) removed = _queue.RemoveAll(q => q.Id == itemId) > 0;
        if (removed) SaveInBackground();
        return removed;
    }

    /// <summary>Number of items pending review.</summary>
    public int Count
    {
        get
        {
            EnsureLoaded();
            lock (_lock) return _queue.Count;
        }
    }

    // ── 3-Button-Flow (K1 aus V4.2 Gesamt-Audit) ─────────────────────
    //
    // Diese Methoden geben das entfernte Item zurueck, damit der Aufrufer
    // (Window oder ViewModel) entscheiden kann, was damit passiert:
    //  - Approve: Item soll in die KB indiziert werden (Aufrufer ruft KbManager)
    //  - Reject: Item war falsch, kein Lerneffekt
    //  - Correct: Item mit korrigiertem Code wird zurueckgegeben

    /// <summary>
    /// Akzeptiert ein Review-Item und entfernt es aus der Queue.
    /// Rueckgabe: das entfernte Item, damit der Aufrufer es in die KB indizieren kann.
    /// null wenn nicht gefunden.
    /// </summary>
    public ReviewQueueItem? ApproveItem(string itemId)
    {
        EnsureLoaded();
        ReviewQueueItem? taken;
        lock (_lock)
        {
            taken = _queue.FirstOrDefault(q => q.Id == itemId);
            if (taken is null) return null;
            _queue.Remove(taken);
        }
        SaveInBackground();
        return taken;
    }

    /// <summary>
    /// Weist ein Review-Item zurueck und entfernt es aus der Queue.
    /// Kein Lerneffekt fuer die KB.
    /// </summary>
    public bool RejectItem(string itemId) => Remove(itemId);

    /// <summary>
    /// Korrigiert den VSA-Code eines Review-Items und entfernt es aus der Queue.
    /// Rueckgabe: Item mit korrigiertem SelfTrainingVsaCode und MatchLevel=ReviewCorrected,
    /// damit der Aufrufer es in die KB indizieren kann. null wenn nicht gefunden.
    /// </summary>
    public ReviewQueueItem? CorrectItem(string itemId, string correctedCode)
    {
        if (string.IsNullOrWhiteSpace(correctedCode))
            throw new ArgumentException("correctedCode darf nicht leer sein.", nameof(correctedCode));
        EnsureLoaded();

        ReviewQueueItem? original;
        lock (_lock)
        {
            original = _queue.FirstOrDefault(q => q.Id == itemId);
            if (original is null) return null;
            _queue.Remove(original);
        }
        SaveInBackground();

        return original with
        {
            SelfTrainingVsaCode = correctedCode,
            SelfTrainingMatchLevel = MatchLevelNames.ReviewCorrected
        };
    }

    private static double ComputePriority(MappedProtocolEntry entry)
    {
        var epistemic = entry.Uncertainty?.EpistemicUncertainty ?? 0.5;
        var closenessTo05 = 1.0 - Math.Abs(2.0 * entry.Confidence - 1.0);
        return 0.6 * epistemic + 0.4 * closenessTo05;
    }
}

public sealed record ReviewQueueItem(
    string Id,
    MappedProtocolEntry? Entry,
    double Priority,
    DateTime EnqueuedUtc
)
{
    /// <summary>True wenn aus Self-Training statt aus Inference-Pipeline.</summary>
    public bool IsFromSelfTraining => SelfTrainingCaseId is not null;

    // Self-Training-Felder (optional)
    public string? SelfTrainingCaseId { get; init; }
    public string? SelfTrainingVsaCode { get; init; }
    public string? SelfTrainingSuggestedCode { get; init; }
    public double? SelfTrainingMeter { get; init; }
    public string? SelfTrainingFramePath { get; init; }
    public string? SelfTrainingMatchLevel { get; init; }
    /// <summary>Stabiler SampleId fuer eindeutiges Mapping bei Review-Korrektur (Finding 2 Fix).</summary>
    public string? SelfTrainingSampleId { get; init; }

    public string Label
    {
        get
        {
            if (IsFromSelfTraining)
            {
                var code = SelfTrainingVsaCode ?? "";
                var klartext = VsaCodeResolver.LookupLabel(code);
                var codeWithLabel = string.IsNullOrWhiteSpace(klartext) ? code : $"{code} — {klartext}";
                return $"{codeWithLabel} @ {SelfTrainingMeter:F1}m ({SelfTrainingMatchLevel})";
            }
            return Entry!.Detection.FindingLabel;
        }
    }
    public string? SuggestedCode
    {
        get
        {
            var raw = IsFromSelfTraining ? SelfTrainingSuggestedCode : Entry?.SuggestedCode;
            if (string.IsNullOrWhiteSpace(raw)) return raw;
            var klartext = VsaCodeResolver.LookupLabel(raw);
            return string.IsNullOrWhiteSpace(klartext) ? raw : $"{raw} — {klartext}";
        }
    }
    public double Confidence => IsFromSelfTraining ? 0 : Entry?.Confidence ?? 0;
    public string PriorityLabel => Priority >= 0.7 ? "Hoch" : Priority >= 0.4 ? "Mittel" : "Niedrig";
}
