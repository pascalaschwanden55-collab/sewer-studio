using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.Application.Ai;

public static class PipelineReachLengthParser
{
    // Haltungslaenge nach der gemeinsamen Leseregel (Deepscan A4); nur positive Werte gelten.
    public static double? TryParse(string? raw)
        => HaltungFeldwerte.LiesLaenge(raw) is double reachLength && reachLength > 0 ? reachLength : null;
}
