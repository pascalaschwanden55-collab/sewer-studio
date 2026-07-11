using System;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Schatten;

/// <summary>
/// Rechnet die eigenstaendige Parallel-Auswertung fuer ein ganzes Projekt.
/// Garantie: Das Projekt und seine HaltungRecords werden NICHT veraendert
/// (Bewertung laeuft auf Tiefkopien, siehe HaltungRecordCloner).
/// </summary>
public interface ISchattenAuswertungService
{
    /// <param name="projekt">Quelle (nur lesend).</param>
    /// <param name="mitKi">Phase 2 (LLM je Haltung) zusaetzlich ausfuehren.</param>
    /// <param name="fortschritt">Phase/Index/Haltung fuer die Seite.</param>
    /// <param name="zwischenspeichern">Wird nach Phase 1 und nach jeder KI-Haltung mit dem
    /// aktuellen Store aufgerufen — macht Abbruch/Absturz verlustfrei.</param>
    Task<SchattenAuswertungStore> BerechneAsync(
        Project projekt,
        bool mitKi,
        IProgress<SchattenFortschritt>? fortschritt,
        Action<SchattenAuswertungStore>? zwischenspeichern,
        CancellationToken ct);
}
