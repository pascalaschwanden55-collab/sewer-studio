using System.Windows.Media;

namespace AuswertungPro.Next.UI.Ai;

public readonly record struct CodingLiveAiStatusState(string StatusText, string DetailText);

public static class CodingLiveAiButtonDisplayPolicy
{
    public static Color ActiveColor => Color.FromRgb(0x22, 0xC5, 0x5E);
    public static Color BlinkAlternateColor => Color.FromRgb(0x16, 0x65, 0x34);

    public static Color BlinkColor(bool blinkState)
        => blinkState ? ActiveColor : BlinkAlternateColor;

    public static CodingLiveAiStatusState BuildStatus(bool isActive, string compactModelName)
        => isActive
            ? new CodingLiveAiStatusState(
                "Automatische KI-Analyse aktiv",
                $"Intervall alle 5 Sekunden | {compactModelName}")
            : new CodingLiveAiStatusState(
                "K\u00fcnstliche Intelligenz bereit",
                $"Modell: {compactModelName}");
}
