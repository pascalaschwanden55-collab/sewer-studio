using System;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Vision;
using AuswertungPro.Next.Domain.Ai.Vision;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai.SelfImproving;

namespace AuswertungPro.Next.Application.Ai.SelfImproving;

/// <summary>
/// Persistenter Speicher fuer die Review-Queue (K1 aus V4.2 Gesamt-Audit).
///
/// Persistiert werden NUR Self-Training-Items (IsFromSelfTraining == true), weil:
///  - Diese enthalten nur primitive Felder (saubere JSON-Serialisierung).
///  - Sie sind der Haupt-Use-Case (KB-Vergiftung abarbeiten, 96% Red).
///  - Yellow-Zone-Items (mit MappedProtocolEntry) werden bei jedem Pipeline-Run
///    ohnehin neu erzeugt und brauchen keine Persistenz.
///
/// Thread-Safety: Prozess-weiter Semaphore schuetzt die Datei gegen parallele Schreibzugriffe.
/// </summary>
public static class ReviewQueueStore
{
    private static readonly SemaphoreSlim _fileLock = new(1, 1);

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>Pfad zur JSON-Datei im KnowledgeRoot.</summary>
    public static string FilePath =>
        Path.Combine(KnowledgeRootProvider.GetRoot(), "review_queue.json");

    /// <summary>
    /// Laedt die persistierte Review-Queue. Bei korruptem JSON oder fehlender Datei
    /// wird eine leere Liste zurueckgegeben (kein Crash).
    /// </summary>
    public static async Task<List<ReviewQueueItem>> LoadAsync(CancellationToken ct = default)
    {
        var path = FilePath;
        if (!File.Exists(path)) return [];

        await _fileLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var json = await File.ReadAllTextAsync(path, ct).ConfigureAwait(false);
            var dtos = JsonSerializer.Deserialize<List<PersistedItem>>(json, JsonOpts) ?? [];
            return dtos.Select(Rehydrate).ToList();
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            // Korruptes JSON oder I/O-Problem: nicht crashen, leere Queue liefern.
            System.Diagnostics.Debug.WriteLine(
                $"[ReviewQueueStore] Load fehlgeschlagen ({ex.GetType().Name}): {ex.Message}");
            return [];
        }
        finally { _fileLock.Release(); }
    }

    /// <summary>
    /// Speichert die Review-Queue (nur Self-Training-Items). Atomic Write via tmp+rename.
    /// </summary>
    public static async Task SaveAsync(
        IEnumerable<ReviewQueueItem> items,
        CancellationToken ct = default)
    {
        // Nur persistier-faehige Items (primitive Felder) uebernehmen.
        var dtos = items
            .Where(i => i.IsFromSelfTraining)
            .Select(PersistedItem.FromItem)
            .ToList();

        await _fileLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var path = FilePath;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            var tmp = path + ".tmp";
            var json = JsonSerializer.Serialize(dtos, JsonOpts);
            await File.WriteAllTextAsync(tmp, json, ct).ConfigureAwait(false);
            File.Move(tmp, path, overwrite: true);
        }
        finally { _fileLock.Release(); }
    }

    // ── DTO + Konvertierung ─────────────────────────────────────────

    private sealed class PersistedItem
    {
        public string Id { get; set; } = "";
        public double Priority { get; set; }
        public DateTime EnqueuedUtc { get; set; }

        public string? SelfTrainingCaseId { get; set; }
        public string? SelfTrainingVsaCode { get; set; }
        public string? SelfTrainingSuggestedCode { get; set; }
        public double? SelfTrainingMeter { get; set; }
        public string? SelfTrainingFramePath { get; set; }
        public string? SelfTrainingMatchLevel { get; set; }
        public string? SelfTrainingSampleId { get; set; }

        public static PersistedItem FromItem(ReviewQueueItem item) => new()
        {
            Id = item.Id,
            Priority = item.Priority,
            EnqueuedUtc = item.EnqueuedUtc,
            SelfTrainingCaseId = item.SelfTrainingCaseId,
            SelfTrainingVsaCode = item.SelfTrainingVsaCode,
            SelfTrainingSuggestedCode = item.SelfTrainingSuggestedCode,
            SelfTrainingMeter = item.SelfTrainingMeter,
            SelfTrainingFramePath = item.SelfTrainingFramePath,
            SelfTrainingMatchLevel = item.SelfTrainingMatchLevel,
            SelfTrainingSampleId = item.SelfTrainingSampleId
        };
    }

    private static ReviewQueueItem Rehydrate(PersistedItem dto) =>
        new(Id: dto.Id,
            Entry: null,
            Priority: dto.Priority,
            EnqueuedUtc: dto.EnqueuedUtc)
        {
            SelfTrainingCaseId = dto.SelfTrainingCaseId,
            SelfTrainingVsaCode = dto.SelfTrainingVsaCode,
            SelfTrainingSuggestedCode = dto.SelfTrainingSuggestedCode,
            SelfTrainingMeter = dto.SelfTrainingMeter,
            SelfTrainingFramePath = dto.SelfTrainingFramePath,
            SelfTrainingMatchLevel = dto.SelfTrainingMatchLevel,
            SelfTrainingSampleId = dto.SelfTrainingSampleId
        };
}
