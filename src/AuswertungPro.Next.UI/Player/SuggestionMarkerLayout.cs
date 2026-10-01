namespace AuswertungPro.Next.UI.Player;

/// <summary>Lage eines Vorschlagsmarkers auf der Zeitleiste — nach Videozeit, nicht nach Meter.</summary>
public static class SuggestionMarkerLayout
{
    public static double? CalculateX(double timeSeconds, double durationSeconds, double offsetX, double trackWidth)
    {
        if (durationSeconds <= 0 || trackWidth <= 0)
            return null;
        if (timeSeconds < 0 || timeSeconds > durationSeconds)
            return null;
        return offsetX + timeSeconds / durationSeconds * trackWidth;
    }
}
