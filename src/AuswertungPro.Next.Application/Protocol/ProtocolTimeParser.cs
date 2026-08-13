using System.Globalization;

namespace AuswertungPro.Next.Application.Protocol;

/// <summary>
/// Parses the time formats used by protocol entries and imported MPEG positions.
/// </summary>
public static class ProtocolTimeParser
{
    private static readonly string[] MpegTimeFormats =
    [
        @"hh\:mm\:ss",
        @"mm\:ss",
        @"h\:mm\:ss",
        @"m\:ss",
        @"hh\:mm\:ss\.fff",
        @"mm\:ss\.fff"
    ];

    public static TimeSpan? ParseMpegTime(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var text = raw.Trim();
        if (TimeSpan.TryParseExact(text, MpegTimeFormats, CultureInfo.InvariantCulture, out var parsed))
            return parsed;

        // VOR TimeSpan.TryParse: .NET liest "00:00:15:00" als d:hh:mm:ss und macht
        // aus 15 Sekunden stillschweigend 15 Minuten. Der vierteilige Zaehlerstand
        // muss deshalb zuerst drankommen.
        if (ParseWithFrames(text) is { } mitBildern)
            return mitBildern;

        if (TimeSpan.TryParse(text, CultureInfo.InvariantCulture, out parsed))
            return parsed;

        return null;
    }

    /// <summary>
    /// Vierteiliger Zaehlerstand "hh:mm:ss:ff" — so liefern ihn die VSA-KEK-XTF
    /// von Abwasser Uri (real gesehen: &lt;Videozaehlerstand&gt;00:00:15:00&lt;/&gt;).
    ///
    /// Der vierte Teil sind Einzelbilder, keine Millisekunden. Er wird bewusst
    /// verworfen statt umgerechnet: Ohne bekannte Bildrate waere jede Umrechnung
    /// geraten, und der Fehler bleibt unter einer Sekunde. Ein Zaehlerstand ist
    /// laut Norm ohnehin die Sekunde ab Dateianfang.
    /// </summary>
    private static TimeSpan? ParseWithFrames(string text)
    {
        var teile = text.Split(':');
        if (teile.Length != 4)
            return null;

        // Der vierte Teil muss eine reine Zahl sein; sonst ist es kein Zaehlerstand.
        if (!int.TryParse(teile[3], NumberStyles.None, CultureInfo.InvariantCulture, out _))
            return null;

        var ohneBilder = string.Join(':', teile[0], teile[1], teile[2]);
        return TimeSpan.TryParseExact(ohneBilder, MpegTimeFormats, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }
}
