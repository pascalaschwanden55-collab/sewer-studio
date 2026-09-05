using System;
using System.Collections.Generic;
using AuswertungPro.Next.Application.UseCases.BendSuggestions;

namespace AuswertungPro.Next.Application.UseCases.CodingSuggestions;

/// <summary>
/// Sucht in der Meterspur des Bogen-Durchlaufs den Punkt, der einer Videosekunde
/// am naechsten liegt. Die Spur ist mit 1 Bild je Sekunde aufgenommen; 1,5 s
/// Toleranz erlaubt genau den Nachbarn, aber keinen Sprung ueber eine Luecke.
/// </summary>
public static class CodingSuggestionMeterLookup
{
    public static MeterTrackPoint? Find(
        IReadOnlyList<MeterTrackPoint> track,
        double timeSeconds,
        double toleranceSeconds = 1.5)
    {
        ArgumentNullException.ThrowIfNull(track);

        MeterTrackPoint? bester = null;
        var besterAbstand = double.PositiveInfinity;
        foreach (var punkt in track)
        {
            var abstand = Math.Abs(punkt.TimeSeconds - timeSeconds);
            if (abstand <= toleranceSeconds && abstand < besterAbstand)
            {
                bester = punkt;
                besterAbstand = abstand;
            }
        }

        return bester;
    }
}
