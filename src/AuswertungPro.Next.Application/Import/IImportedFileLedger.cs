using System.Collections.Generic;
using System.Threading;

namespace AuswertungPro.Next.Application.Import;

/// <summary>
/// Zustand des Projektordners vor den Dateioperationen eines Importlaufs.
/// Enthaelt relative Pfade mit Dateigroesse sowie die vorhandenen Unterordner.
/// </summary>
public sealed record ImportFolderSnapshot(
    string ProjectFolder,
    IReadOnlyDictionary<string, long> FileSizesByRelativePath,
    IReadOnlySet<string> RelativeDirectories);

/// <summary>
/// Ergebnis einer Ruecknahme.
/// </summary>
/// <param name="RolledBack">
/// true, wenn die Ruecknahme durchgefuehrt wurde. false bedeutet: sie wurde aus
/// Sicherheitsgruenden verweigert — dann bleibt alles unveraendert liegen.
/// </param>
/// <param name="DeletedFiles">Zahl entfernter, vom Lauf neu erzeugter Dateien.</param>
/// <param name="KeptFiles">
/// Zahl neuer Dateien, die bewusst bleiben (z.B. Importberichte) oder nicht sicher
/// entfernt werden konnten.
/// </param>
public sealed record ImportRollbackResult(
    bool RolledBack,
    int DeletedFiles,
    int KeptFiles,
    IReadOnlyList<string> Messages);

/// <summary>
/// Nimmt die Dateien eines abgebrochenen Ein-Knopf-Imports zurueck
/// (Gesamtaudit 2026-08-14, P1-5).
///
/// Hintergrund: Archivierung und Medienverteilung schreiben direkt in den
/// Projektordner, bevor das Importergebnis uebernommen wird. Wird der Lauf danach
/// verworfen — Ausnahme, Projektwechsel, zwischenzeitliche Bearbeitung oder
/// fehlgeschlagene Pruefung — blieben diese Dateien bisher unbemerkt liegen.
///
/// Bewusste Grenze: Das ist eine Ruecknahme, keine echte Transaktion. Bei einem
/// Prozessabsturz mitten im Lauf gibt es niemanden, der sie ausfuehrt. Ein
/// vollstaendiges Staging aller Verteilerwege ist ein eigenes Arbeitspaket, weil
/// spaetere Importschritte die zuvor geschriebenen Dateien wieder lesen.
///
/// Sicherheitsregeln der Umsetzung:
/// * Es werden nur Dateien entfernt, die in der Momentaufnahme fehlten.
/// * Fehlt eine zuvor vorhandene Datei, wird gar nichts geloescht (fail-closed):
///   dann ist im Ordner mehr passiert als ein reines Hinzufuegen.
/// * Verknuepfungen und Pfade ausserhalb des Projektordners werden nie angefasst.
/// </summary>
public interface IImportedFileLedger
{
    ImportFolderSnapshot Capture(string projectFolder, CancellationToken cancellationToken = default);

    ImportRollbackResult RollbackNewFiles(
        ImportFolderSnapshot before,
        CancellationToken cancellationToken = default);
}
