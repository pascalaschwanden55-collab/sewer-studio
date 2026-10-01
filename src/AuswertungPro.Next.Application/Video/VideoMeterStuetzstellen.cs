using System;
using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Application.Video;

/// <summary>
/// Sammelt die Stuetzstellen fuer <see cref="VideoMeterSpur"/> aus dem aktuellen
/// Protokoll: jede codierte Beobachtung, die BEIDES traegt — eine Videozeit und
/// einen Meterwert. Genau diese Paare verbinden Videozeit und Stationierung.
///
/// Reine Leselogik ohne Oberflaeche; das Protokoll wird nicht veraendert.
/// </summary>
public static class VideoMeterStuetzstellen
{
    public static IReadOnlyList<VideoMeterStuetzstelle> AusProtokoll(ProtocolDocument? protokoll)
    {
        if (protokoll?.Current?.Entries is not { Count: > 0 } entries)
            return Array.Empty<VideoMeterStuetzstelle>();

        var stellen = new List<VideoMeterStuetzstelle>();
        foreach (var entry in entries)
        {
            if (entry is null || entry.IsDeleted)
                continue;

            if (Videozeit(entry) is not { } zeit)
                continue;
            if (entry.MeterStart is not { } meter)
                continue;
            if (double.IsNaN(meter) || double.IsInfinity(meter) || meter < 0.0)
                continue;

            stellen.Add(new VideoMeterStuetzstelle(zeit, meter));
        }

        // Die Reihenfolge im Protokoll folgt dem Meter, nicht zwingend der Videozeit
        // (Gegenfahrt, nachtraeglich eingefuegte Befunde). Die Spur braucht die Zeit.
        return stellen.OrderBy(s => s.Zeit).ToList();
    }

    /// <summary>
    /// Bevorzugt das strukturierte Feld; der MPEG-Text ist der Rueckfall fuer
    /// Altbestaende, die nur ihn tragen.
    /// </summary>
    private static TimeSpan? Videozeit(ProtocolEntry entry)
        => entry.Zeit ?? ProtocolTimeParser.ParseMpegTime(entry.Mpeg);
}
