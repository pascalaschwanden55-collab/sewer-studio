namespace AuswertungPro.Next.Application.Import;

/// <summary>Ergebnis eines Recovery-Laufs beim Projekt-Laden.</summary>
public enum ImportRecoveryOutcome
{
    /// <summary>Kein offener Marker — nichts zu tun.</summary>
    None,

    /// <summary>Unvollstaendiger Import: veroeffentlichte Dateien wurden zurueckgenommen.</summary>
    RolledBack,

    /// <summary>Import war committed: nur Arbeitsordner + Marker aufgeraeumt.</summary>
    CompletedCleanup,

    /// <summary>Marker vorhanden, aber nicht sicher lesbar: Projekt-Oeffnen muss stoppen.</summary>
    Blocked
}

/// <summary>Ergebnis samt optionaler, dem Nutzer anzeigbarer Meldung.</summary>
/// <param name="ProjectFolderModified">
/// Wahr, sobald die Wiederherstellung im Projektordner etwas veraendert hat (Datei
/// zurueckgenommen, Arbeitsordner entfernt, Marker geloescht). Die Oberflaeche darf den
/// beruhigenden Zusatz "nicht veraendert" nur anhaengen, wenn das falsch ist - frueher
/// stand er pauschal an jeder gesperrten Meldung, auch neben "3 Datei(en) zurueckgenommen".
/// </param>
public sealed record ImportRecoveryResult(
    ImportRecoveryOutcome Outcome,
    string? Message,
    bool ProjectFolderModified = false);

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
