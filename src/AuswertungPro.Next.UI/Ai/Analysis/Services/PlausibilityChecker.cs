// AuswertungPro – KI Videoanalyse Modul
using System.Collections.Generic;
using AuswertungPro.Next.UI.Ai.Analysis.Models;
using AuswertungPro.Next.UI.Ai.Shared;

namespace AuswertungPro.Next.UI.Ai.Analysis.Services;

/// <summary>
/// Regelbasierte Plausibilitätsprüfung: Passt die Quantifizierung zum Code-Typ?
/// Ergänzt <see cref="VsaCodeValidator"/> um semantische Regeln.
/// </summary>
public static class PlausibilityChecker
{
    /// <summary>
    /// Prüft die Beobachtung auf Plausibilität.
    /// Fügt Flags hinzu und reduziert Quantification-Konfidenz bei Verstößen.
    /// </summary>
    public static void Check(AnalysisObservation obs)
    {
        var flags = obs.ValidationFlags;
        var penalized = false;

        if (obs.Quantification is { } q)
        {
            var info = VsaCatalog.Get(obs.VsaCode);

            // Riss/Bruch → Einheit sollte "mm" sein (Spaltbreite)
            if (obs.VsaCode.StartsWith("BA") && q.Unit == "%")
            {
                flags.Add($"P01: Riss/Bruch-Code '{obs.VsaCode}' mit Einheit '%' unplausibel (erwartet mm)");
                penalized = true;
            }

            // Verformung/Einragung → Einheit sollte "%" sein (Querschnitt)
            if ((obs.VsaCode is "BBA" or "BCA" or "BCB" or "BCC" or "BFB" or "BFC")
                && q.Unit == "mm")
            {
                flags.Add($"P02: Code '{obs.VsaCode}' mit Einheit 'mm' unplausibel (erwartet %)");
                penalized = true;
            }

            // Wert > plausibles Maximum
            if (q.Unit == "%" && q.Value > 100)
            {
                flags.Add($"P03: Prozentwert {q.Value}% > 100");
                penalized = true;
            }

            if (q.Unit == "mm" && q.Value > 500)
            {
                flags.Add($"P04: Millimeterwert {q.Value}mm > 500 (unplausibel)");
                penalized = true;
            }
        }

        // Meter-Plausibilität
        if (obs.MeterStart < 0)
        {
            flags.Add($"P05: MeterStart {obs.MeterStart:F2} < 0");
            penalized = true;
        }

        if (obs.MeterEnd > 2000)
        {
            flags.Add($"P06: MeterEnd {obs.MeterEnd:F2} > 2000 m (unplausibel für Haltung)");
        }

        if (penalized)
        {
            var qConf = System.Math.Max(0.0, obs.Confidence.Quantification - 0.2);
            obs.Confidence = obs.Confidence with { Quantification = qConf };
        }
    }

    /// <summary>Prüft alle Beobachtungen in einer Liste in-place.</summary>
    public static IReadOnlyList<AnalysisObservation> CheckAll(
        IEnumerable<AnalysisObservation> observations)
    {
        var result = new System.Collections.Generic.List<AnalysisObservation>();
        foreach (var obs in observations)
        {
            Check(obs);
            result.Add(obs);
        }
        return result;
    }
}
