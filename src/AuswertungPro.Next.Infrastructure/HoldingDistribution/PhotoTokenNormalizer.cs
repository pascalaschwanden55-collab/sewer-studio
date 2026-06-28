using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace AuswertungPro.Next.Infrastructure.HoldingDistribution;

/// <summary>
/// Reine String-Helfer zur Normalisierung von Foto-Token-Schluesseln.
/// Extrahiert aus HoldingFolderDistributor.PdfParsing – verhaltensneutral.
/// </summary>
internal static class PhotoTokenNormalizer
{
    /// <summary>
    /// Liefert alle moeglichen Lookup-Schluessel fuer einen Foto-Dateinamen.
    /// </summary>
    internal static IEnumerable<string> EnumeratePhotoLookupKeys(string? raw)
    {
        var fileName = HoldingTextNormalizer.NormalizeVideoFileName(raw);
        if (string.IsNullOrWhiteSpace(fileName))
            yield break;

        var noExt = Path.GetFileNameWithoutExtension(fileName);
        var ext = Path.GetExtension(fileName);
        var hasImageExt = HasImageExtension(fileName);

        if (hasImageExt)
            yield return fileName;

        if (!string.IsNullOrWhiteSpace(noExt))
            yield return noExt;

        var normalizedNoExt = NormalizePhotoToken(noExt);
        if (string.IsNullOrWhiteSpace(normalizedNoExt))
            yield break;

        if (!string.Equals(normalizedNoExt, noExt, StringComparison.OrdinalIgnoreCase))
            yield return normalizedNoExt;

        if (hasImageExt && !string.IsNullOrWhiteSpace(ext))
            yield return $"{normalizedNoExt}{ext}";
    }

    /// <summary>
    /// Fuegt die Lookup-Schluessel eines Foto-Tokens zur Liste hinzu (ohne Duplikate).
    /// </summary>
    internal static void AddPhotoLookupKeys(string? raw, List<string> keys)
    {
        foreach (var key in EnumeratePhotoLookupKeys(raw))
        {
            if (!keys.Contains(key, StringComparer.OrdinalIgnoreCase))
                keys.Add(key);
        }
    }

    /// <summary>
    /// Normalisiert ein Foto-Token auf kanonische Form (fuehrende Nullen entfernt, Grossbuchstabe am Ende).
    /// Gibt null zurueck wenn das Muster nicht passt.
    /// </summary>
    internal static string? NormalizePhotoToken(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;

        var m = Regex.Match(token, @"(?<a>\d{1,5})_(?<b>\d{1,5})_(?<c>\d{1,7})_(?<d>[A-Za-z])");
        if (!m.Success)
            return null;

        var a = TrimLeadingZerosValue(m.Groups["a"].Value);
        var b = TrimLeadingZerosValue(m.Groups["b"].Value);
        var c = TrimLeadingZerosValue(m.Groups["c"].Value);
        var d = char.ToUpperInvariant(m.Groups["d"].Value[0]);
        return $"{a}_{b}_{c}_{d}";
    }

    /// <summary>
    /// Entfernt fuehrende Nullen aus einem Ziffernstring; "000" wird zu "0".
    /// </summary>
    internal static string TrimLeadingZerosValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;

        var trimmed = value.TrimStart('0');
        return string.IsNullOrEmpty(trimmed) ? "0" : trimmed;
    }

    // ── Hilfsmethode (intern, delegiert an HoldingTextNormalizer) ─────────────────

    private static bool HasImageExtension(string fileName)
    {
        var normalized = HoldingTextNormalizer.NormalizeVideoFileName(fileName);
        return AuswertungPro.Next.Infrastructure.Media.MediaFileTypes.HasImageExtension(normalized ?? "");
    }
}
