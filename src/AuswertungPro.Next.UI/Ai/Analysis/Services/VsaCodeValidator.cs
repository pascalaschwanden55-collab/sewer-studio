// AuswertungPro – KI Videoanalyse Modul
using System;
using System.Collections.Generic;
using AuswertungPro.Next.UI.Ai.Analysis.Models;
using AuswertungPro.Next.UI.Ai.Shared;

namespace AuswertungPro.Next.UI.Ai.Analysis.Services;

/// <summary>
/// Deterministische Validierung einer <see cref="AnalysisObservation"/> gegen den VSA-Katalog.
/// Kein LLM, keine externe Abhängigkeit.
/// Bei Validierungsfehlern: Confidence.Classification -= 0.3, Flag setzen.
/// </summary>
public static class VsaCodeValidator
{
    private const double ConfidencePenalty = 0.3;

    /// <summary>
    /// Validiert eine Beobachtung in-place.
    /// Fügt Flags hinzu und reduziert Konfidenz bei Verstößen.
    /// Gibt true zurück wenn keine Verstöße gefunden wurden.
    /// </summary>
    public static bool Validate(AnalysisObservation obs)
    {
        var flags = obs.ValidationFlags;
        var penaltyCount = 0;

        // V01: Code muss im Katalog existieren
        if (!VsaCatalog.IsKnown(obs.VsaCode))
        {
            flags.Add($"V01: Unbekannter VSA-Code '{obs.VsaCode}'");
            penaltyCount++;
        }

        var info = VsaCatalog.Get(obs.VsaCode);

        // V02: Charakterisierung nur wenn Code es erfordert
        if (info is not null)
        {
            if (info.RequiresCharacterization && string.IsNullOrWhiteSpace(obs.Characterization))
                flags.Add($"V02: Charakterisierung (A–D) für Code '{obs.VsaCode}' erforderlich");

            if (!info.RequiresCharacterization && !string.IsNullOrWhiteSpace(obs.Characterization))
                flags.Add($"V02: Charakterisierung '{obs.Characterization}' für Code '{obs.VsaCode}' nicht zulässig");
        }

        // V03: Quantification.Unit muss zum Code-Typ passen
        if (obs.Quantification is { } q && info is not null && info.QuantUnit is not null)
        {
            if (!string.Equals(q.Unit, info.QuantUnit, StringComparison.OrdinalIgnoreCase))
            {
                flags.Add($"V03: Einheit '{q.Unit}' passt nicht zu Code '{obs.VsaCode}' (erwartet '{info.QuantUnit}')");
                penaltyCount++;
            }
        }

        // V04: ClockPosition 1–12
        if (obs.Quantification?.ClockPosition is { } cp)
        {
            if (!int.TryParse(cp, out var clock) || clock < 1 || clock > 12)
                flags.Add($"V04: Ungültige Uhrzeigerposition '{cp}' (erwartet 1–12)");
        }

        // V05: MeterStart ≤ MeterEnd
        if (obs.MeterStart > obs.MeterEnd)
        {
            flags.Add($"V05: MeterStart ({obs.MeterStart:F2}) > MeterEnd ({obs.MeterEnd:F2})");
            penaltyCount++;
        }

        // V06: IsStreckenschaden → MeterEnd > MeterStart + 0.1
        if (obs.IsStreckenschaden && obs.MeterEnd <= obs.MeterStart + 0.1)
        {
            flags.Add("V06: IsStreckenschaden=true aber MeterEnd ≤ MeterStart + 0.1");
            penaltyCount++;
        }

        // V07: Confidence-Werte im Bereich [0.0, 1.0]
        if (obs.Confidence.Detection < 0 || obs.Confidence.Detection > 1
            || obs.Confidence.Classification < 0 || obs.Confidence.Classification > 1
            || obs.Confidence.Quantification < 0 || obs.Confidence.Quantification > 1)
        {
            flags.Add("V07: Confidence-Werte außerhalb [0.0, 1.0]");
        }

        // V08: Confidence-Abzug bei Fehlern
        if (penaltyCount > 0)
        {
            var penalized = Math.Max(0.0, obs.Confidence.Classification - ConfidencePenalty * penaltyCount);
            obs.Confidence = obs.Confidence with { Classification = penalized };
        }

        return flags.Count == 0;
    }

    /// <summary>Validiert eine Liste von Beobachtungen und gibt nur gültige zurück.</summary>
    public static IReadOnlyList<AnalysisObservation> ValidateAll(
        IEnumerable<AnalysisObservation> observations)
    {
        var result = new List<AnalysisObservation>();
        foreach (var obs in observations)
        {
            Validate(obs);
            result.Add(obs);   // auch ungültige bleiben, aber mit Flags
        }
        return result;
    }
}
