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

        if (TimeSpan.TryParse(text, CultureInfo.InvariantCulture, out parsed))
            return parsed;

        return null;
    }
}
