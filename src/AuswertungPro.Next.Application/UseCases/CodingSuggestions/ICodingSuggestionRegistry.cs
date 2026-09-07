using System;
using System.Collections.Generic;

namespace AuswertungPro.Next.Application.UseCases.CodingSuggestions;

/// <summary>Ein gemerkter Vorabdurchlauf: Haltung, Zeitpunkt und das Ergebnis-Set.</summary>
public sealed record CodingSuggestionRun(string Haltung, DateTimeOffset Zeitpunkt, CodingSuggestionSet Set);

/// <summary>
/// Sitzungsgedaechtnis der KI-Vorabdurchlaeufe fuer die Uebersicht (Inventar 4.1, Karte
/// "KI-Vorabdurchlauf"). Nur Arbeitsspeicher; nichts wird gespeichert oder als Gold gewertet.
/// </summary>
public interface ICodingSuggestionRegistry
{
    /// <summary>
    /// Merkt den letzten Durchlauf einer Haltung. Eine leere Haltung wird ignoriert.
    /// Loest danach <see cref="Geaendert"/> synchron auf demselben Thread aus, der
    /// diesen Aufruf taetigt (kein eigenes Marshalling in Application).
    /// </summary>
    void Merke(string haltung, CodingSuggestionSet set);

    /// <summary>Alle Laeufe seit Programmstart, je Haltung nur der letzte, juengster zuerst.</summary>
    IReadOnlyList<CodingSuggestionRun> Heute();

    /// <summary>
    /// Feuert nach jedem gemerkten Durchlauf synchron auf dem aufrufenden Thread
    /// (typischerweise ein Hintergrund-/Scan-Thread, nicht zwingend der UI-Thread).
    /// Diese Klasse liegt in Application und marshallt selbst nicht auf einen
    /// Dispatcher; ein UI-Abonnent muss selbst auf seinen Dispatcher wechseln.
    /// </summary>
    event Action? Geaendert;
}
