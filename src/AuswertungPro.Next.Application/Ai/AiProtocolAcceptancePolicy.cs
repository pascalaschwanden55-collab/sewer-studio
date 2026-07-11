using System;
using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Application.Ai;

/// <summary>
/// Uebernahme-Filter fuer KI-Vollanalyse-Protokolle (Fehlerpruefung 11.07., Kritisch 2):
/// Nur die vom Nutzer AUSGEWAEHLTEN Eintraege gelangen ins Fachprotokoll; uebernommene
/// werden als Ai.Accepted=true markiert. Reine Logik ohne I/O — testbar.
/// </summary>
public static class AiProtocolAcceptancePolicy
{
    /// <summary>
    /// Gemeinsame Uebernahme-Regel fuer den Codiermodus. Neue KI- sowie manuell
    /// gepruefte Events brauchen eine ausdrueckliche Annahme. Bereits vorhandene
    /// Import-/Protokolleintraege ohne Pruefkontext bleiben erhalten.
    /// </summary>
    public static bool CanApply(CodingEvent? codingEvent)
    {
        if (codingEvent is null || string.IsNullOrWhiteSpace(codingEvent.Entry.Code))
            return false;

        if (codingEvent.AiContext is not null)
            return IsAccepted(codingEvent.AiContext.Decision);

        if (codingEvent.ReviewContext is not null)
            return IsAccepted(codingEvent.ReviewContext.Decision);

        return true;
    }

    public static IReadOnlyList<CodingEvent> FilterCodingEvents(IEnumerable<CodingEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        return events.Where(CanApply).ToList();
    }

    /// <summary>
    /// Filtert beide Revisionen (Original + Current) auf die ausgewaehlten EntryIds.
    /// Nicht ausgewaehlte KI-Eintraege werden verworfen (nicht als geloescht markiert —
    /// sie waren nie Teil des Fachprotokolls).
    /// </summary>
    public static ProtocolDocument Apply(ProtocolDocument quelle, IReadOnlySet<Guid> ausgewaehlt)
    {
        ArgumentNullException.ThrowIfNull(quelle);
        ArgumentNullException.ThrowIfNull(ausgewaehlt);

        FiltereRevision(quelle.Original, ausgewaehlt);
        FiltereRevision(quelle.Current, ausgewaehlt);
        return quelle;
    }

    private static void FiltereRevision(ProtocolRevision? revision, IReadOnlySet<Guid> ausgewaehlt)
    {
        if (revision is null)
            return;

        var behalten = revision.Entries.Where(e => ausgewaehlt.Contains(e.EntryId)).ToList();
        foreach (var e in behalten)
        {
            if (e.Ai is not null)
                e.Ai.Accepted = true; // echte Nutzerentscheidung dokumentieren
        }

        revision.Entries.Clear();
        revision.Entries.AddRange(behalten);
        // Aenderungsliste auf die uebernommenen Eintraege beschraenken.
        var behalteneIds = behalten.Select(e => e.EntryId).ToHashSet();
        var changes = revision.Changes.Where(c => behalteneIds.Contains(c.EntryId)).ToList();
        revision.Changes.Clear();
        revision.Changes.AddRange(changes);
    }

    private static bool IsAccepted(CodingUserDecision decision)
        => decision is CodingUserDecision.Accepted or CodingUserDecision.AcceptedWithEdit;
}
