using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace AuswertungPro.Next.Infrastructure;

/// <summary>
/// Statische Text-Helfer fuer Haltungsordner-Verteilung.
/// Extrahiert aus HoldingFolderDistributor.TextUtils – verhaltensneutral.
/// </summary>
internal static class HoldingTextNormalizer
{
    /// <summary>
    /// Ersetzt Sonderzeichen (NBSP, Gedankenstriche, Tabulatoren) durch ASCII-Aequivalente.
    /// </summary>
    internal static string NormalizeText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text ?? string.Empty;

        return text
            .Replace(' ', ' ')
            .Replace('–', '-')
            .Replace('—', '-')
            .Replace('−', '-')
            .Replace("\t", " ");
    }

    /// <summary>
    /// Versucht einen Datumsstring in gaengigen Formaten zu parsen.
    /// </summary>
    internal static bool TryParseDateString(string value, out DateTime date)
    {
        return DateTime.TryParseExact(
            value,
            new[] { "dd.MM.yyyy", "dd.MM.yy", "dd/MM/yyyy", "dd/MM/yy", "dd-MM-yyyy", "dd-MM-yy", "yyyy-MM-dd" },
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out date);
    }

    /// <summary>
    /// Normalisiert einen Schuessel auf Kleinbuchstaben und nur alphanumerische Zeichen.
    /// </summary>
    internal static string NormalizeKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        var sb = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            if (char.IsLetterOrDigit(ch))
                sb.Append(char.ToLowerInvariant(ch));
        }
        return sb.ToString();
    }

    /// <summary>
    /// Prueft einen normalisierten Objektschluessel im urspruenglichen Dateinamen.
    /// Trennzeichen innerhalb des Schluessels duerfen variieren; direkt davor und
    /// danach muss aber eine echte alphanumerische Grenze liegen. So trifft
    /// "100-200" auf "H_100-200_001", aber nicht auf "H_100-2000_001".
    /// </summary>
    internal static bool ContainsKeyAtBoundary(string text, string key)
        => ContainsNormalizedKeyAtBoundary(text, NormalizeKey(NormalizeText(key)));

    internal static bool ContainsNormalizedKeyAtBoundary(
        string text,
        string normalizedKey)
    {
        if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(normalizedKey))
            return false;

        var source = NormalizeText(text);
        var normalizedChars = new List<char>(source.Length);
        var sourceIndexes = new List<int>(source.Length);
        for (var index = 0; index < source.Length; index++)
        {
            if (!char.IsLetterOrDigit(source[index]))
                continue;

            normalizedChars.Add(char.ToLowerInvariant(source[index]));
            sourceIndexes.Add(index);
        }

        var normalizedText = new string(normalizedChars.ToArray());
        var searchFrom = 0;
        while (searchFrom <= normalizedText.Length - normalizedKey.Length)
        {
            var matchIndex = normalizedText.IndexOf(
                normalizedKey,
                searchFrom,
                StringComparison.OrdinalIgnoreCase);
            if (matchIndex < 0)
                return false;

            var sourceStart = sourceIndexes[matchIndex];
            var sourceEnd = sourceIndexes[matchIndex + normalizedKey.Length - 1];
            var startsAtBoundary = sourceStart == 0
                                   || !char.IsLetterOrDigit(source[sourceStart - 1]);
            var endsAtBoundary = sourceEnd == source.Length - 1
                                 || !char.IsLetterOrDigit(source[sourceEnd + 1]);
            if (startsAtBoundary && endsAtBoundary)
                return true;

            searchFrom = matchIndex + 1;
        }

        return false;
    }

    /// <summary>
    /// Erstellt einen Seitenbereich-String aus einer sortierten Seitenliste (z.B. "3-7").
    /// </summary>
    internal static string BuildPageRange(IReadOnlyList<int> pages)
    {
        if (pages.Count == 0) return "";
        var sorted = pages.Distinct().OrderBy(p => p).ToList();
        return sorted.Count == 1 ? $"{sorted[0]}" : $"{sorted[0]}-{sorted[^1]}";
    }

    /// <summary>
    /// Prueft ob ein Text ein Inhaltsverzeichnis darstellt.
    /// </summary>
    internal static bool IsContentsPage(string text)
        => text.Contains("Inhaltsverzeichnis", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Kombiniert zwei optionale Meldungsstrings mit Semikolon-Trenner.
    /// </summary>
    internal static string? MergeMessage(string? a, string? b)
    {
        if (string.IsNullOrWhiteSpace(a))
            return string.IsNullOrWhiteSpace(b) ? null : b;
        if (string.IsNullOrWhiteSpace(b))
            return a;
        return $"{a}; {b}";
    }

    /// <summary>
    /// Normalisiert einen Video-Dateinamen: entfernt Anfuehrungszeichen, Trailing-Satzzeichen
    /// und extrahiert nur den Dateinamen ohne Pfad.
    /// </summary>
    internal static string? NormalizeVideoFileName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var candidate = value.Trim().Trim('"', '\'');
        candidate = candidate.TrimEnd('.', ',', ';', ':', ')', ']', '}', '>');
        if (string.IsNullOrWhiteSpace(candidate))
            return null;

        candidate = candidate.Replace('\\', '/');
        var fileName = Path.GetFileName(candidate).Trim();
        if (string.IsNullOrWhiteSpace(fileName))
            return null;

        return fileName.Trim('"', '\'');
    }
}
