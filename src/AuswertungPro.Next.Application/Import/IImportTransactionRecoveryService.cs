namespace AuswertungPro.Next.Application.Import;

/// <summary>Ergebnis eines Recovery-Laufs beim Projekt-Laden.</summary>
public enum ImportRecoveryOutcome
{
    /// <summary>Kein offener Marker — nichts zu tun.</summary>
    None,

    /// <summary>Unvollstaendiger Import: veroeffentlichte Dateien wurden zurueckgenommen.</summary>
    RolledBack,

    /// <summary>Import war committed: nur Arbeitsordner + Marker aufgeraeumt.</summary>
    CompletedCleanup
}

/// <summary>Ergebnis samt optionaler, dem Nutzer anzeigbarer Meldung.</summary>
public sealed record ImportRecoveryResult(ImportRecoveryOutcome Outcome, string? Message);

/// <summary>
/// Prueft beim Projekt-Laden, ob eine Import-Transaktion durch einen Prozess-Absturz
/// unterbrochen wurde (Marker vorhanden), und stellt den Alles-oder-nichts-Zustand her:
/// committed ⇒ nur aufraeumen, sonst ⇒ veroeffentlichte Dateien zurueckrollen.
/// </summary>
public interface IImportTransactionRecoveryService
{
    /// <param name="projectRoot">Projekt-Wurzelordner (enthaelt ggf. den Marker).</param>
    /// <param name="committedImportTxId">
    /// <c>Project.LastCommittedImportTxId</c> des gerade geladenen Projekts (Commit-Beweis).
    /// </param>
    ImportRecoveryResult RecoverIfNeeded(string projectRoot, string? committedImportTxId);
}
