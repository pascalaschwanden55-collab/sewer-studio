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
