using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AuswertungPro.Next.Application.Backup;

/// <summary>
/// Komplette Datensicherung fuer den PC-Ausfall-Schutz:
/// spiegelt Programm, Projekte, KI-Gehirn, Einstellungen und Logs inkrementell in einen Zielordner.
/// Projektvideos sind standardmaessig aus Platzgruenden ausgeschaltet.
/// </summary>
public interface IFullBackupService
{
    /// <summary>
    /// Enumeriert alle Quellen und liefert Groessen/Dateizahlen pro Komponente,
    /// ohne etwas zu kopieren (Anzeige vor dem Lauf).
    /// </summary>
    Task<FullBackupSizeReport> AnalyzeAsync(IProgress<string>? progress = null, CancellationToken ct = default);

    /// <summary>
    /// Fuehrt den inkrementellen Spiegel in &lt;zielOrdner&gt;\SewerStudio_Datensicherung\ aus.
    /// Kopiert nur fehlende/geaenderte Dateien, entfernt im Ziel Verwaistes (Spiegel-Semantik).
    /// </summary>
    Task<FullBackupResult> RunAsync(
        string targetFolder,
        IProgress<FullBackupProgress>? progress = null,
        CancellationToken ct = default);
}

/// <summary>Groesse und Dateizahl einer Backup-Komponente (vor dem Lauf ermittelt).</summary>
public sealed record ComponentSize(
    string Name,
    string Beschreibung,
    long Bytes,
    int FileCount,
    bool SourceFound);

/// <summary>Groessen-Report aller Komponenten vor dem Lauf.</summary>
public sealed record FullBackupSizeReport(
    IReadOnlyList<ComponentSize> Components,
    long TotalBytes,
    int TotalFiles);

/// <summary>Fortschritt waehrend des Laufs. Prozent = BytesDone/BytesTotal.</summary>
public sealed record FullBackupProgress(
    string Component,
    string CurrentFile,
    long BytesDone,
    long BytesTotal,
    int FilesDone,
    int FilesTotal);

/// <summary>Endergebnis eines Sicherungslaufs.</summary>
public sealed record FullBackupResult(
    bool Success,
    string? Error,
    string TargetRoot,
    long TotalBytes,
    int FilesCopied,
    int FilesUnchanged,
    int FilesDeleted,
    IReadOnlyList<string> SkippedFiles,
    TimeSpan Duration,
    int FilesVerified = 0,
    int DatabasesSnapshotted = 0,
    long RequiredFreeBytes = 0,
    long AvailableFreeBytes = 0);
