namespace AuswertungPro.Next.Application.Ai;

/// <summary>
/// Fachregeln fuer Live-Codier-Deduplication, bewusst frei von UI-Abhaengigkeiten.
/// </summary>
public static class CodingDedupPolicy
{
    private const double TerminalMeterTolerance = 0.05;

    public static bool IsOneTimeCode(string? code)
    {
        var main = MainCode(code);
        return main is "BCD" or "BCE" or "BDC";
    }

    public static bool CodesMatch(string? existingCode, string? newCode)
    {
        if (string.IsNullOrWhiteSpace(existingCode) || string.IsNullOrWhiteSpace(newCode))
            return false;

        if (string.Equals(existingCode, newCode, StringComparison.OrdinalIgnoreCase))
            return true;

        var existingMain = MainCode(existingCode);
        var newMain = MainCode(newCode);
        return existingMain is not null
            && newMain is not null
            && string.Equals(existingMain, newMain, StringComparison.OrdinalIgnoreCase);
    }

    public static bool ShouldStopAnalysisAfterTerminalCode(
        IEnumerable<(string? Code, double? Meter, TimeSpan? VideoTime)> terminalCandidates,
        double? currentMeter,
        TimeSpan? currentVideoTime)
    {
        foreach (var candidate in terminalCandidates)
        {
            var main = MainCode(candidate.Code);
            if (main is not ("BCE" or "BDC"))
                continue;

            if (candidate.Meter.HasValue && currentMeter.HasValue)
            {
                if (currentMeter.Value >= candidate.Meter.Value - TerminalMeterTolerance)
                    return true;

                continue;
            }

            if (candidate.VideoTime.HasValue && currentVideoTime.HasValue
                && currentVideoTime.Value >= candidate.VideoTime.Value)
                return true;

            if (!candidate.Meter.HasValue && !candidate.VideoTime.HasValue)
                return true;
        }

        return false;
    }

    public static bool ShouldDeferSpatialCodeUntilCloser(
        string? code,
        MetrierungProximityResult proximity)
    {
        if (MainCode(code) is not "BCC")
            return false;

        return !proximity.IsCodierbar;
    }

    /// <summary>Absolute Naehe-Toleranz zum Haltungsende, in Metern (User 2026-06-16: letzte 20 cm).</summary>
    private const double EndMeterAbsoluteTolerance = 0.20;

    /// <summary>Relative Naehe-Schwelle: erst ab diesem Anteil der Laenge gilt das Ende als nah.</summary>
    private const double EndMeterRelativeThreshold = 0.90;

    /// <summary>Obere Toleranz (Meter), die ein BCE-Meter ueber dem bekannten Haltungsende liegen darf.</summary>
    private const double EndMeterOvershootTolerance = 1.0;

    /// <summary>
    /// Plausibilitaet eines automatischen Rohrende-Vorschlags (BCE). BEIDSEITIG:
    /// - zu frueh: Der Klassifikator haelt manchmal das dunkle Tunnelende am Fluchtpunkt
    ///   faelschlich fuer das Rohrende -> akzeptieren erst nahe am Haltungsende
    ///   (innerhalb <see cref="EndMeterAbsoluteTolerance"/> m ODER ab
    ///   <see cref="EndMeterRelativeThreshold"/> der Laenge).
    /// - zu weit: Ein fehlerhaft gelesener OSD-Meter kann WEIT ueber dem Ende liegen
    ///   (z.B. 114 m bei 15.82 m Haltung). Solche Werte sind ebenfalls unplausibel und werden
    ///   verworfen (mehr als <see cref="EndMeterOvershootTolerance"/> m ueber dem Ende).
    ///
    /// Konservativ: Ist die Haltungslaenge (endMeter) oder die aktuelle Position unbekannt,
    /// gilt der Vorschlag als plausibel — sonst entstuende evtl. gar kein Rohrende.
    /// Nur BCE wird geprueft; BCD/andere Codes sind hier immer plausibel.
    /// </summary>
    public static bool IsBoundaryEndCodePlausible(string? code, double? currentMeter, double? endMeter)
    {
        if (MainCode(code) is not "BCE")
            return true;

        if (!endMeter.HasValue || endMeter.Value <= 0)
            return true;

        if (!currentMeter.HasValue)
            return true;

        // Obere Schranke: nicht weit ueber das Ende hinaus (kaputter OSD-Meter).
        if (currentMeter.Value > endMeter.Value + EndMeterOvershootTolerance)
            return false;

        double nearAbsolute = endMeter.Value - EndMeterAbsoluteTolerance;
        double nearRelative = endMeter.Value * EndMeterRelativeThreshold;
        double threshold = Math.Min(nearAbsolute, nearRelative);

        return currentMeter.Value >= threshold;
    }

    /// <summary>
    /// Liefert einen verlaesslichen Rohrende-Meter fuer das automatische BCE. Wenn der gelesene
    /// OSD-Meter unplausibel weit ueber dem bekannten Haltungsende liegt (kaputte OSD-Lesung),
    /// wird der verlaessliche Wert genommen: bevorzugt der Import-BCE-Meter, sonst das EndMeter.
    /// Liegt der OSD-Meter plausibel im Bereich, wird er beibehalten.
    /// Fachregel User 2026-06-16: BCE-Meter auf das Haltungsende korrigieren statt Falschwert.
    /// </summary>
    public static double ResolvePlausibleEndMeter(double? osdMeter, double? importEndMeter, double? vmEndMeter)
    {
        // Verlaessliches Ende: Import-BCE hat Vorrang, sonst VM-EndMeter.
        double? reliable = (importEndMeter is > 0) ? importEndMeter
                         : (vmEndMeter is > 0) ? vmEndMeter
                         : null;

        if (!osdMeter.HasValue)
            return reliable ?? 0.0;

        // Kein verlaessliches Ende bekannt -> OSD nehmen (nichts Besseres da).
        if (reliable is null)
            return osdMeter.Value;

        // OSD unplausibel weit ueber dem Ende -> auf das verlaessliche Ende korrigieren.
        if (osdMeter.Value > reliable.Value + EndMeterOvershootTolerance)
            return reliable.Value;

        return osdMeter.Value;
    }

    private static string? MainCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return null;

        var trimmed = code.Trim();
        return trimmed.Length >= 3 ? trimmed[..3].ToUpperInvariant() : trimmed.ToUpperInvariant();
    }
}
