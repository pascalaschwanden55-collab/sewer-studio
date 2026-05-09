using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Ai.Training;

// BatchSelfTrainingOrchestrator History-Tracking: Merkt sich verarbeitete
// Haltungen in C:\KI_BRAIN\batch_processed.txt damit ein zweiter Lauf nicht
// alles nochmal durchlaufen muss. Inkl. Path-Normalisierung und automatischer
// Rotation bei >50000 Eintraegen. Aus dem Hauptdatei extrahiert (Slice 17b).
public sealed partial class BatchSelfTrainingOrchestrator
{
    // ═══ Batch-History: Merkt sich welche Ordner bereits verarbeitet wurden ═══

    private static string GetBatchHistoryPath()
        => Path.Combine(KnowledgeRoot.GetRoot(), "batch_processed.txt");

    /// <summary>
    /// Laedt die Liste bereits verarbeiteter Batch-Keys.
    /// Nur v2-Eintraege werden ausgewertet (Legacy-IDs ohne Pfadkontext werden ignoriert).
    /// </summary>
    private static async Task<HashSet<string>> LoadBatchHistoryAsync()
    {
        var path = GetBatchHistoryPath();
        if (!File.Exists(path))
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var lines = await File.ReadAllLinesAsync(path).ConfigureAwait(false);
        return new HashSet<string>(
            lines.Where(l =>
                    !string.IsNullOrWhiteSpace(l)
                    && l.StartsWith("v2|", StringComparison.OrdinalIgnoreCase))
                .Select(l => l.Trim()),
            StringComparer.OrdinalIgnoreCase);
    }

    // Schuetzt Parallel-Append an batch_processed.txt. Bei MaxParallelHaltungen>1
    // wuerde ohne Lock `File.AppendAllTextAsync` verschraenkte Zeilen schreiben
    // koennen — mit Folge: Haltungen doppelt verarbeitet oder faelschlich als
    // "erledigt" markiert.
    private static readonly SemaphoreSlim _batchHistoryLock = new(1, 1);

    /// <summary>Fuegt einen Batch-History-Key hinzu (append, kein volles Neuschreiben).</summary>
    private static async Task AppendBatchHistoryAsync(string historyKey)
    {
        var path = GetBatchHistoryPath();
        var dir = Path.GetDirectoryName(path);
        if (dir is not null) Directory.CreateDirectory(dir);
        await _batchHistoryLock.WaitAsync().ConfigureAwait(false);
        try
        {
            await File.AppendAllTextAsync(path, historyKey + Environment.NewLine).ConfigureAwait(false);
        }
        finally
        {
            _batchHistoryLock.Release();
        }

        // Rotation: aelteste Eintraege entfernen wenn Datei zu gross wird
        await RotateBatchHistoryIfNeededAsync(path).ConfigureAwait(false);
    }

    /// <summary>
    /// Baut einen stabilen History-Key fuer eine Haltung.
    /// Enthaelt HaltungId + absolute Video/Protokoll-Pfade, um Kollisionen bei gleichen IDs zu vermeiden.
    /// </summary>
    private static string BuildHistoryKey(DiscoveredHaltung h)
    {
        var id = h.HaltungId.Trim().ToUpperInvariant();
        var video = NormalizePathForHistory(h.VideoPath);
        var protocol = NormalizePathForHistory(h.ProtocolSource);
        return $"v2|{id}|{video}|{protocol}";
    }

    private static string NormalizePathForHistory(string path)
    {
        try
        {
            return Path.GetFullPath(path)
                .Trim()
                .Replace('\\', '/')
                .ToLowerInvariant();
        }
        catch
        {
            return path.Trim().Replace('\\', '/').ToLowerInvariant();
        }
    }

    private static async Task RotateBatchHistoryIfNeededAsync(string path)
    {
        const int maxLines = 5000;
        const int trimTo = 4000;

        try
        {
            var lines = await File.ReadAllLinesAsync(path).ConfigureAwait(false);
            if (lines.Length <= maxLines) return;

            // Aelteste Eintraege (oben) entfernen, neueste behalten
            var kept = lines.Skip(lines.Length - trimTo).ToArray();
            await File.WriteAllLinesAsync(path, kept).ConfigureAwait(false);
        }
        catch { /* Best effort — Rotation darf nie die Hauptlogik stoeren */ }
    }
}
