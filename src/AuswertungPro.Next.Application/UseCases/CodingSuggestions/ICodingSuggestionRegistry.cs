using System;
using System.Collections.Generic;

namespace AuswertungPro.Next.Application.UseCases.CodingSuggestions;

/// <summary>Ein gemerkter Vorabdurchlauf: Projekt, Haltung, Zeitpunkt und das Ergebnis-Set.</summary>
public sealed record CodingSuggestionRun(Guid Projekt, string Haltung, DateTimeOffset Zeitpunkt, CodingSuggestionSet Set);

/// <summary>
/// Sitzungsgedaechtnis der KI-Vorabdurchlaeufe fuer die Uebersicht (Inventar 4.1, Karte
/// "KI-Vorabdurchlauf"). Nur Arbeitsspeicher; nichts wird gespeichert oder als Gold gewertet.
/// Jeder Lauf gehoert zu genau einem Projekt (R4, Gesamtaudit 08.09.2026): Haltungsnamen
/// wiederholen sich zwischen Projekten, ein reiner Namensschluessel vermischte sie.
/// </summary>
public interface ICodingSuggestionRegistry
{
    /// <summary>
    /// Merkt den letzten Durchlauf einer Haltung im angegebenen Projekt. Eine leere Haltung
    /// und ein unbekanntes Projekt (<see cref="Guid.Empty"/>) werden ignoriert — ein Lauf ohne
    /// Projektbezug waere sonst in jedem Projekt sichtbar.
    /// Loest danach <see cref="Geaendert"/> synchron auf demselben Thread aus, der
    /// diesen Aufruf taetigt (kein eigenes Marshalling in Application).
    /// </summary>
    void Merke(Guid projekt, string haltung, CodingSuggestionSet set);

    /// <summary>
    /// Alle Laeufe dieses Projekts seit Programmstart, je Haltung nur der letzte,
    /// juengster zuerst. Laeufe anderer Projekte erscheinen nie.
    /// </summary>
    IReadOnlyList<CodingSuggestionRun> Heute(Guid projekt);

    /// <summary>
    /// Feuert nach jedem gemerkten Durchlauf synchron auf dem aufrufenden Thread
    /// (typischerweise ein Hintergrund-/Scan-Thread, nicht zwingend der UI-Thread).
    /// Diese Klasse liegt in Application und marshallt selbst nicht auf einen
    /// Dispatcher; ein UI-Abonnent muss selbst auf seinen Dispatcher wechseln.
    /// </summary>
    event Action? Geaendert;
}
