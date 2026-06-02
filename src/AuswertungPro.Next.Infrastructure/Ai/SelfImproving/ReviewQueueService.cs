using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;

namespace AuswertungPro.Next.Infrastructure.Ai.SelfImproving;

public sealed class ReviewQueueService
{
    private readonly List<ReviewQueueItem> _queue = new();
    private readonly object _lock = new();
    private static readonly object _fileLock = new();
    private readonly string? _persistPath;

    /// <summary>Produktions-Factory: persistiert die Self-Training-Kandidaten (Nachtlauf) im
    /// Knowledge-Ordner, damit sie Fenster-Schliessen/Neustart ueberleben.</summary>
    public static ReviewQueueService CreatePersistent()
        => new(Path.Combine(KnowledgeBasePaths.GetRoot(), "review_queue.json"));

    /// <param name="persistencePath">null = reine In-Memory-Queue ohne Datei-IO (z.B. fuer Tests).</param>
    public ReviewQueueService(string? persistencePath = null)
    {
        _persistPath = persistencePath;
        // S9: Beim Start die persistierten Self-Training-Kandidaten wieder laden.
        if (_persistPath is not null)
            LoadSelfTrainingItems();
    }

    public void Enqueue(MappedProtocolEntry entry)
    {
        if (entry.QualityGateResult is not { IsYellow: true }) return;

        var priority = ComputePriority(entry);
        var item = new ReviewQueueItem(
            Id: Guid.NewGuid().ToString(),
            Entry: entry,
            Priority: priority,
            EnqueuedUtc: DateTime.UtcNow);

        lock (_lock)
        {
            _queue.Add(item);
            ResortByPriorityDesc();
        }
    }

    public void EnqueueRange(IEnumerable<MappedProtocolEntry> entries)
    {
        foreach (var entry in entries)
            Enqueue(entry);
    }

    public void EnqueueFromSelfTraining(
        string caseId,
        string vsaCode,
        string suggestedCode,
        double meter,
        string framePath,
        string matchLevel,
        string? reason = null)
    {
        var priority = matchLevel switch
        {
            MatchLevelNames.Mismatch => 0.9,
            MatchLevelNames.PartialMatch => 0.6,
            _ => 0.3
        };

        var item = new ReviewQueueItem(
            Id: Guid.NewGuid().ToString(),
            Entry: null,
            Priority: priority,
            EnqueuedUtc: DateTime.UtcNow)
        {
            SelfTrainingCaseId = caseId,
            SelfTrainingVsaCode = vsaCode,
            SelfTrainingSuggestedCode = suggestedCode,
            SelfTrainingMeter = meter,
            SelfTrainingFramePath = framePath,
            SelfTrainingMatchLevel = matchLevel,
            SelfTrainingReason = reason
        };

        lock (_lock)
        {
            _queue.Add(item);
            ResortByPriorityDesc();
        }

        PersistSelfTrainingItems();
    }

    public IReadOnlyList<ReviewQueueItem> GetAll()
    {
        lock (_lock) return _queue.ToList();
    }

    public IReadOnlyList<ReviewQueueItem> GetTop(int count)
    {
        lock (_lock) return _queue.Take(count).ToList();
    }

    public bool Remove(string itemId)
    {
        bool removed;
        lock (_lock) removed = _queue.RemoveAll(q => q.Id == itemId) > 0;
        if (removed) PersistSelfTrainingItems();
        return removed;
    }

    public int Count
    {
        get { lock (_lock) return _queue.Count; }
    }

    private static double ComputePriority(MappedProtocolEntry entry)
    {
        var epistemic = entry.Uncertainty?.EpistemicUncertainty ?? 0.5;
        var closenessTo05 = 1.0 - Math.Abs(2.0 * entry.Confidence - 1.0);
        return 0.6 * epistemic + 0.4 * closenessTo05;
    }

    /// <summary>Sortiert die Queue absteigend nach Prioritaet. Aufrufer muss _lock halten.</summary>
    private void ResortByPriorityDesc()
        => _queue.Sort((a, b) => b.Priority.CompareTo(a.Priority));

    // ─── S9: Persistenz der Self-Training-Kandidaten (Nachtlauf) ──────────────────────
    // Nur Self-Training-Items (Entry == null) werden persistiert. Die QualityGate-Items
    // (Entry != null) sind In-Session-Daten eines laufenden Analyselaufs und tragen einen
    // komplexen MappedProtocolEntry-Graphen, der nicht verlustfrei rekonstruierbar ist.
    private sealed record PersistedItem(
        string Id,
        double Priority,
        DateTime EnqueuedUtc,
        string? SelfTrainingCaseId,
        string? SelfTrainingVsaCode,
        string? SelfTrainingSuggestedCode,
        double? SelfTrainingMeter,
        string? SelfTrainingFramePath,
        string? SelfTrainingMatchLevel,
        string? SelfTrainingReason);

    private void LoadSelfTrainingItems()
    {
        try
        {
            if (_persistPath is null || !File.Exists(_persistPath)) return;

            var items = JsonSerializer.Deserialize<List<PersistedItem>>(File.ReadAllText(_persistPath));
            if (items is null) return;

            lock (_lock)
            {
                foreach (var p in items)
                {
                    if (p.SelfTrainingCaseId is null) continue; // nur Self-Training-Kandidaten
                    _queue.Add(new ReviewQueueItem(p.Id, null, p.Priority, p.EnqueuedUtc)
                    {
                        SelfTrainingCaseId = p.SelfTrainingCaseId,
                        SelfTrainingVsaCode = p.SelfTrainingVsaCode,
                        SelfTrainingSuggestedCode = p.SelfTrainingSuggestedCode,
                        SelfTrainingMeter = p.SelfTrainingMeter,
                        SelfTrainingFramePath = p.SelfTrainingFramePath,
                        SelfTrainingMatchLevel = p.SelfTrainingMatchLevel,
                        SelfTrainingReason = p.SelfTrainingReason
                    });
                }
                ResortByPriorityDesc();
            }
        }
        catch { /* best-effort: korrupte/fehlende Datei darf den Start nicht verhindern */ }
    }

    private void PersistSelfTrainingItems()
    {
        if (_persistPath is null) return;
        try
        {
            List<PersistedItem> items;
            lock (_lock)
            {
                items = _queue
                    .Where(q => q.IsFromSelfTraining)
                    .Select(q => new PersistedItem(
                        q.Id, q.Priority, q.EnqueuedUtc,
                        q.SelfTrainingCaseId, q.SelfTrainingVsaCode, q.SelfTrainingSuggestedCode,
                        q.SelfTrainingMeter, q.SelfTrainingFramePath, q.SelfTrainingMatchLevel,
                        q.SelfTrainingReason))
                    .ToList();
            }

            // Datei-Schreibzugriff prozessweit serialisieren; eindeutiger tmp-Name verhindert
            // Kollisionen, falls mehrere Instanzen/Threads zugleich persistieren.
            lock (_fileLock)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_persistPath)!);
                var tmp = _persistPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
                File.WriteAllText(tmp, JsonSerializer.Serialize(
                    items, AuswertungPro.Next.Application.Common.JsonDefaults.Indented));
                File.Move(tmp, _persistPath, overwrite: true);
            }
        }
        catch { /* best-effort: Persistenz-Fehler darf den Lauf nicht abbrechen */ }
    }
}

public sealed record ReviewQueueItem(
    string Id,
    MappedProtocolEntry? Entry,
    double Priority,
    DateTime EnqueuedUtc)
{
    public bool IsFromSelfTraining => SelfTrainingCaseId is not null;

    public string? SelfTrainingCaseId { get; init; }
    public string? SelfTrainingVsaCode { get; init; }
    public string? SelfTrainingSuggestedCode { get; init; }
    public double? SelfTrainingMeter { get; init; }
    public string? SelfTrainingFramePath { get; init; }
    public string? SelfTrainingMatchLevel { get; init; }
    /// <summary>Warum der Fall geprueft werden muss (z.B. "HumanReviewRequired"). Null = aus MatchLevel ableitbar.</summary>
    public string? SelfTrainingReason { get; init; }

    public string Label => IsFromSelfTraining
        ? $"{SelfTrainingVsaCode} @ {SelfTrainingMeter:F1}m ({SelfTrainingMatchLevel})"
        : Entry!.Detection.FindingLabel;

    public string? SuggestedCode => IsFromSelfTraining ? SelfTrainingSuggestedCode : Entry?.SuggestedCode;
    public double Confidence => IsFromSelfTraining ? 0 : Entry?.Confidence ?? 0;
    public string PriorityLabel => Priority >= 0.7 ? "Hoch" : Priority >= 0.4 ? "Mittel" : "Niedrig";
}
