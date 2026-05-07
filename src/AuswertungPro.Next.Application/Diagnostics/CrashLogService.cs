using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AuswertungPro.Next.Application.Diagnostics;

/// <summary>
/// Sprint 1 (2026-05-07): Listet die Crash-Logs (Dateien <c>crash-*.log</c>
/// im logs/-Verzeichnis), liefert Metadaten und kann sie aufraeumen.
/// Damit der User im Diagnose-Tab sieht ob/wann Crashes passiert sind.
/// </summary>
public sealed class CrashLogService
{
    private readonly Func<string> _getLogsDir;

    public CrashLogService(Func<string> getLogsDir)
    {
        _getLogsDir = getLogsDir ?? throw new ArgumentNullException(nameof(getLogsDir));
    }

    /// <summary>
    /// Listet alle <c>crash-*.log</c>-Dateien, juengste zuerst.
    /// </summary>
    public IReadOnlyList<CrashLogEntry> List(int maxResults = 50)
    {
        var dir = _getLogsDir();
        if (!Directory.Exists(dir)) return Array.Empty<CrashLogEntry>();

        var files = Directory.EnumerateFiles(dir, "crash-*.log", SearchOption.TopDirectoryOnly);
        return files
            .Select(p => new FileInfo(p))
            .OrderByDescending(fi => fi.LastWriteTimeUtc)
            .Take(maxResults)
            .Select(fi => new CrashLogEntry(
                Path: fi.FullName,
                Name: fi.Name,
                CreatedUtc: fi.CreationTimeUtc,
                LastWriteUtc: fi.LastWriteTimeUtc,
                Bytes: fi.Length))
            .ToList();
    }

    /// <summary>
    /// Loescht Crash-Logs aelter als <paramref name="keepDays"/>. Behaelt
    /// die juengsten <paramref name="keepCount"/> auf jeden Fall.
    /// </summary>
    public CrashLogPruneResult Prune(int keepCount = 10, int keepDays = 30)
    {
        var entries = List(maxResults: int.MaxValue);
        var cutoff = DateTime.UtcNow.AddDays(-keepDays);
        var toDelete = entries
            .Skip(keepCount) // immer mind. keepCount juengste behalten
            .Where(e => e.LastWriteUtc < cutoff)
            .ToList();

        var errors = new List<string>();
        long bytesDeleted = 0;
        int deleted = 0;
        foreach (var entry in toDelete)
        {
            try
            {
                File.Delete(entry.Path);
                bytesDeleted += entry.Bytes;
                deleted++;
            }
            catch (Exception ex)
            {
                errors.Add($"{entry.Name}: {ex.GetType().Name}: {ex.Message}");
            }
        }

        return new CrashLogPruneResult(
            Considered: entries.Count,
            Deleted: deleted,
            BytesDeleted: bytesDeleted,
            Errors: errors);
    }
}

/// <summary>Metadaten zu einer Crash-Log-Datei.</summary>
public sealed record CrashLogEntry(
    string Path,
    string Name,
    DateTime CreatedUtc,
    DateTime LastWriteUtc,
    long Bytes);

/// <summary>Zusammenfassung des Prune-Laufs.</summary>
public sealed record CrashLogPruneResult(
    int Considered,
    int Deleted,
    long BytesDeleted,
    IReadOnlyList<string> Errors);
