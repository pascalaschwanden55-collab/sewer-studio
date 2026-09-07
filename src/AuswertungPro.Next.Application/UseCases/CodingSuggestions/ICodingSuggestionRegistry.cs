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
    /// <summary>Merkt den letzten Durchlauf einer Haltung. Eine leere Haltung wird ignoriert.</summary>
    void Merke(string haltung, CodingSuggestionSet set);

    /// <summary>Alle Laeufe seit Programmstart, je Haltung nur der letzte, juengster zuerst.</summary>
    IReadOnlyList<CodingSuggestionRun> Heute();

    /// <summary>Feuert nach jedem gemerkten Durchlauf.</summary>
    event Action? Geaendert;
}
