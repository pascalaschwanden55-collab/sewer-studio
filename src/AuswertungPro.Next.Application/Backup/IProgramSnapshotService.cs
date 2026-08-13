using System;
using System.Threading;
using System.Threading.Tasks;

namespace AuswertungPro.Next.Application.Backup;

/// <summary>
/// Auftrag fuer eine Programm-Momentaufnahme: Quelle ist der Programmordner,
/// Ziel eine einzelne ZIP-Datei. Beide Pfade kommen von aussen, damit dieselbe
/// Logik auf einen Sicherungsdatentraeger, in einen Cloud-Ordner oder in einen
/// Testordner schreiben kann.
/// </summary>
public sealed record ProgramSnapshotRequest(string ProgramRoot, string ZipPath);

/// <summary>
/// Ergebnis einer Momentaufnahme. <paramref name="SkippedReparsePoints"/> wird
/// ausdruecklich mitgezaehlt: Uebersprungene Verknuepfungen sind kein Fehler,
/// duerfen aber nie unsichtbar bleiben.
/// </summary>
public sealed record ProgramSnapshotResult(
    bool Success,
    string? Error,
    int FileCount,
    long SizeBytes,
    int SkippedReparsePoints);

/// <summary>
/// Packt den Programmstand in eine einzelne, atomar veroeffentlichte ZIP-Datei.
/// Gedacht als zusaetzliche Kopie neben dem Systemschutz — etwa fuer einen
/// Cloud-Ordner, in dem hunderttausende Einzeldateien nichts zu suchen haben.
/// Der Programmordner wird dabei nur gelesen.
/// </summary>
public interface IProgramSnapshotService
{
    Task<ProgramSnapshotResult> CreateAsync(
        ProgramSnapshotRequest request,
        IProgress<string>? progress = null,
        CancellationToken ct = default);
}
