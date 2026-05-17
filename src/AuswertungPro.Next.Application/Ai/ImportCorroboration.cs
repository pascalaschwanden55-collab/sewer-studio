using System;
using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Application.Ai.QualityGate;

namespace AuswertungPro.Next.Application.Ai;

public sealed record ImportCorroborationMatch(
    string Code,
    double Meter,
    double DistanceMeters,
    bool ExactCodeMatch);

public static class ImportCorroboration
{
    public const double DefaultMaxDistanceMeters = 0.5;

    public static ImportCorroborationMatch? FindNearest(
        string? detectedCode,
        double detectedMeter,
        IEnumerable<(string Code, double Meter)> importEvents,
        double maxDistanceMeters = DefaultMaxDistanceMeters)
    {
        var normalizedDetected = NormalizeCode(detectedCode);
        if (normalizedDetected is null || maxDistanceMeters < 0)
            return null;

        var detectedFamily = Family(normalizedDetected);

        return importEvents
            .Select(e => new
            {
                Code = NormalizeCode(e.Code),
                e.Meter
            })
            .Where(e => e.Code is not null)
            .Select(e => new
            {
                Code = e.Code!,
                e.Meter,
                Distance = Math.Abs(e.Meter - detectedMeter)
            })
            .Where(e => e.Distance <= maxDistanceMeters)
            .Select(e => new
            {
                e.Code,
                e.Meter,
                e.Distance,
                Exact = string.Equals(e.Code, normalizedDetected, StringComparison.OrdinalIgnoreCase),
                SameFamily = string.Equals(Family(e.Code), detectedFamily, StringComparison.OrdinalIgnoreCase)
            })
            .Where(e => e.Exact || e.SameFamily)
            .OrderByDescending(e => e.Exact)
            .ThenBy(e => e.Distance)
            .ThenByDescending(e => e.Code.Length)
            .Select(e => new ImportCorroborationMatch(e.Code, e.Meter, e.Distance, e.Exact))
            .FirstOrDefault();
    }

    public static EvidenceVector BuildQwenEvidence(
        int severity,
        string? detectedCode,
        ImportCorroborationMatch? importMatch)
    {
        var qwenConf = Math.Clamp(severity / 5.0, 0.0, 1.0);
        var plausibility = 0.60;
        bool? codeAgreement = null;

        if (importMatch is not null)
        {
            if (importMatch.ExactCodeMatch)
            {
                qwenConf = Math.Max(qwenConf, 0.65);
                plausibility = 0.95;
                codeAgreement = true;
            }
            else
            {
                qwenConf = Math.Max(qwenConf, 0.55);
                plausibility = 0.80;
            }
        }

        return new EvidenceVector(
            QwenVisionConf: qwenConf,
            KbCodeAgreement: codeAgreement,
            PlausibilityScore: plausibility,
            DamageCategory: Family(NormalizeCode(detectedCode)));
    }

    private static string? NormalizeCode(string? code)
    {
        return VsaCodeResolver.NormalizeFindingCode(code)
               ?? (string.IsNullOrWhiteSpace(code) ? null : code.Trim().Replace(".", "").ToUpperInvariant());
    }

    private static string? Family(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return null;

        return code.Length >= 3 ? code[..3] : code;
    }
}
