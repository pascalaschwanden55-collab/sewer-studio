using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Infrastructure.Import.Xtf;

/// <summary>
/// Reiner Scorer fuer das Abgleichen von Protokolleintraegen gegen VSA-Befunde.
/// Kein IO, kein Dokumentzugriff — nur Distanz-/Code-/Foto-Ranking.
/// Extrahiert aus LegacyXtfImportService.
/// </summary>
internal static class FindingEntryMatcher
{
    /// <summary>
    /// Gibt den besten passenden VsaFinding-Eintrag fuer einen Protokolleintrag zurueck.
    /// Matching-Reihenfolge: enge Distanz → lockere Distanz+Code → Code → Foto/Distanz-Fallback.
    /// </summary>
    public static VsaFinding? FindBestFindingForEntry(ProtocolEntry entry, IReadOnlyList<VsaFinding> findings)
    {
        if (findings.Count == 0)
            return null;

        var entryMeter = entry.MeterStart ?? entry.MeterEnd;
        var entryCode = XtfValueNormalizer.NormalizeCode(entry.Code);
        var scored = new List<(VsaFinding Finding, double Delta, int CodeRank, bool HasPhoto)>(findings.Count);

        foreach (var finding in findings)
        {
            var findingMeter = GetFindingMeterStart(finding) ?? GetFindingMeterEnd(finding);
            var delta = (entryMeter.HasValue && findingMeter.HasValue)
                ? Math.Abs(findingMeter.Value - entryMeter.Value)
                : double.MaxValue;
            var codeRank = XtfValueNormalizer.GetCodeSimilarityRank(entryCode, XtfValueNormalizer.NormalizeCode(finding.KanalSchadencode));
            var hasPhoto = !string.IsNullOrWhiteSpace(finding.FotoPath);
            scored.Add((finding, delta, codeRank, hasPhoto));
        }

        if (entryMeter.HasValue && scored.Count > 0)
        {
            // Primär nach Distanz matchen; Codes dienen als Tiebreaker.
            var byMeter = scored
                .Where(s => s.Delta <= 0.15)
                .OrderBy(s => s.Delta)
                .ThenBy(s => s.CodeRank)
                .ThenByDescending(s => s.HasPhoto)
                .ToList();
            if (byMeter.Count > 0)
                return byMeter[0].Finding;

            var byMeterLoose = scored
                .Where(s => s.Delta <= 0.50 && s.CodeRank <= 1)
                .OrderBy(s => s.Delta)
                .ThenBy(s => s.CodeRank)
                .ThenByDescending(s => s.HasPhoto)
                .ToList();
            if (byMeterLoose.Count > 0)
                return byMeterLoose[0].Finding;
        }

        var byCode = scored
            .Where(s => s.CodeRank == 0 || s.CodeRank == 1)
            .OrderBy(s => s.CodeRank)
            .ThenByDescending(s => s.HasPhoto)
            .ThenBy(s => s.Delta)
            .ToList();
        if (byCode.Count > 0)
            return byCode[0].Finding;

        // Ohne Meter- oder Codebezug ist jede Zuordnung geraten. Ein solcher Fallback
        // hing frueher Foto und Zeit eines beliebigen Befunds an den Protokolleintrag.
        return null;
    }

    /// <summary>
    /// Gibt den effektiven Meterstart eines Befunds zurueck.
    /// SchadenlageAnfang ist VSA-Uhrlage, kein Meterwert.
    /// </summary>
    public static double? GetFindingMeterStart(VsaFinding finding)
        => finding.MeterStart;

    /// <summary>
    /// Gibt das effektive Meterende eines Befunds zurueck.
    /// SchadenlageEnde ist VSA-Uhrlage, kein Meterwert.
    /// </summary>
    public static double? GetFindingMeterEnd(VsaFinding finding)
        => finding.MeterEnd;
}
