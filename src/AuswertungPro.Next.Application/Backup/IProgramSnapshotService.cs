using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AuswertungPro.Next.Application.Backup;

/// <summary>
/// Auftrag fuer eine Programm-Momentaufnahme: Quelle ist der Programmordner,
/// Ziel eine einzelne ZIP-Datei. Beide Pfade kommen von aussen, damit dieselbe
/// Logik auf einen Sicherungsdatentraeger, in einen Cloud-Ordner oder in einen
/// Testordner schreiben kann.
/// </summary>
/// <param name="VerifyArchive">
/// Liest die fertige ZIP vor der Veroeffentlichung noch einmal komplett und prueft
/// jeden Eintrag. Kostet einen zweiten Lesedurchlauf, deckt aber genau den Fall auf,
/// den eine Sicherung nie haben darf: technisch geschrieben, inhaltlich beschaedigt.
/// Nur fuer Tests abschaltbar.
/// </param>
public sealed record ProgramSnapshotRequest(
    string ProgramRoot,
    string ZipPath,
    bool VerifyArchive = true);

/// <summary>
/// Ergebnis einer Momentaufnahme. <paramref name="SkippedReparsePoints"/> wird
/// ausdruecklich mitgezaehlt: Uebersprungene Verknuepfungen sind kein Fehler,
/// duerfen aber nie unsichtbar bleiben.
/// </summary>
/// <param name="UnreadableDirectories">
/// Ordner, die beim Durchlauf nicht gelesen werden konnten (relativ zur Programmwurzel).
/// Ihr Inhalt fehlt in der Sicherung. Bei einem unersetzlichen Ordner
/// (<see cref="ProgramSnapshotFileCatalog.IsRequiredDirectory"/>) schlaegt die
/// Sicherung fehl; bei allen anderen bleibt sie erfolgreich, meldet die Liste aber
/// sichtbar weiter. Vor dem Gesamtaudit 2026-08-14 wurden solche Ordner still
/// uebersprungen und die Sicherung trotzdem als erfolgreich angezeigt.
/// </param>
/// <param name="ArchiveSha256">
/// SHA-256 der veroeffentlichten ZIP-Datei, zusaetzlich als Nebendatei
/// <c>&lt;name&gt;.zip.sha256</c> abgelegt. Damit ist spaeter pruefbar, ob die
/// Sicherung noch dieselbe ist. Im Manifest kann er nicht stehen — er wuerde sich
/// selbst enthalten.
/// </param>
public sealed record ProgramSnapshotResult(
    bool Success,
    string? Error,
    int FileCount,
    long SizeBytes,
    int SkippedReparsePoints,
    IReadOnlyList<string>? UnreadableDirectories = null,
    string? ArchiveSha256 = null)
{
    /// <summary>Nie null — erleichtert Anzeige und Tests.</summary>
    public IReadOnlyList<string> UnreadableDirectoriesOrEmpty
        => UnreadableDirectories ?? Array.Empty<string>();
}

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
