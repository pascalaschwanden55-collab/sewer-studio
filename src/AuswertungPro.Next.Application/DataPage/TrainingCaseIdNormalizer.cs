using System;
using System.Text.RegularExpressions;

namespace AuswertungPro.Next.Application.DataPage;

/// <summary>
/// Reine Normalisierungs-Hilfsklasse fuer Training-CaseIds und Haltungsnamen.
/// Aus <c>DataPageViewModel.NormalizeTrainingCaseId</c> und
/// <c>DataPageViewModel.StripNodePrefixes</c> extrahiert (verhaltensneutral).
/// </summary>
public static class TrainingCaseIdNormalizer
{
    private static readonly Regex DatePrefixRegex = new(@"^\d{8}_", RegexOptions.Compiled);
    private static readonly Regex NodePrefixRegex = new(@"^\d{1,2}\.", RegexOptions.Compiled);

    /// <summary>
    /// Normalisiert eine Training-CaseId zu einem Haltungsnamen.
    /// Entfernt Datums-Prefixe wie "20250602_" (z.B. "20250602_06.24341-35625" → "06.24341-35625").
    /// </summary>
    public static string NormalizeCaseId(string? caseId)
    {
        var v = (caseId ?? "").Trim();
        v = DatePrefixRegex.Replace(v, "");
        return v;
    }

    /// <summary>
    /// Entfernt XX. Knoten-Praefixe (1-2 Ziffern + Punkt) von beiden Seiten eines
    /// Haltungsnamens mit Bindestrich-Trennzeichen.
    /// Beispiel: "07.1028055-10.1064892" → "1028055-1064892"
    /// </summary>
    public static string StripNodePrefixes(string holdingKey)
    {
        var dashIdx = holdingKey.IndexOf('-');
        if (dashIdx < 0)
            return NodePrefixRegex.Replace(holdingKey, "");

        var left = holdingKey[..dashIdx];
        var right = holdingKey[(dashIdx + 1)..];
        left = NodePrefixRegex.Replace(left, "");
        right = NodePrefixRegex.Replace(right, "");
        return $"{left}-{right}";
    }
}
