using System;
using System.Collections.Generic;
using System.Linq;

using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Application.Reports;

/// <summary>
/// Teilt eine (nach Meter sortierte) Beobachtungsliste in Inspektionssegmente:
/// Hauptinspektion und – ab dem ersten Abbruchcode (BDC*) – Gegeninspektion.
/// Der Abbruch-Eintrag beendet die Hauptinspektion (bleibt in Segment 1); alle
/// folgenden Einträge bilden die Gegeninspektion. Dient der Trennlinie im Protokoll
/// (analog zum Originalprotokoll).
/// </summary>
public static class InspectionSegmenter
{
    public sealed record Segment(string? Title, IReadOnlyList<ProtocolEntry> Entries);

    public static IReadOnlyList<Segment> Segments(IReadOnlyList<ProtocolEntry>? entries)
    {
        var list = entries ?? (IReadOnlyList<ProtocolEntry>)Array.Empty<ProtocolEntry>();
        if (list.Count == 0)
            return new[] { new Segment(null, list) };

        var abortIndex = -1;
        for (var i = 0; i < list.Count; i++)
        {
            if (ProtocolTextHelpers.IsAbortCode(list[i]))
            {
                abortIndex = i;
                break;
            }
        }

        // Kein Abbruch, oder Abbruch ist der letzte Eintrag (nichts folgt) -> ein Segment.
        if (abortIndex < 0 || abortIndex >= list.Count - 1)
            return new[] { new Segment(null, list) };

        var main = list.Take(abortIndex + 1).ToList();
        var gegen = list.Skip(abortIndex + 1).ToList();
        return new[]
        {
            new Segment(null, main),
            new Segment("Gegeninspektion", gegen)
        };
    }
}
