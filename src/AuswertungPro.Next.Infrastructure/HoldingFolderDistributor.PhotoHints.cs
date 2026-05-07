using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace AuswertungPro.Next.Infrastructure;

/// <summary>
/// HoldingFolderDistributor — Photo-Hint-Extraction (partial class).
///
/// Refactor 2026-05-07 (Etappe 5, Charge R12): Extraktion von
/// Foto-Datei-Verweisen aus PDF-Texten (fuer CDIndex-Photo-Hint-Voting
/// in TryFindVideoFromCdIndexPhotoHints). Mechanisch — keine
/// Verhaltensaenderung.
/// </summary>
public static partial class HoldingFolderDistributor
{
    private static readonly Regex PhotoAfterLabelRegex = new(
        @"Foto\s*:\s*(?<name>\d{1,5}_\d{1,5}_\d{1,7}_[A-Za-z](?:\.(?:jpe?g|png|bmp|tif|tiff))?)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex PhotoTokenRegex = new(
        @"(?<![A-Za-z0-9])(?<name>\d{1,5}_\d{1,5}_\d{1,7}_[A-Za-z](?:\.(?:jpe?g|png|bmp|tif|tiff))?)(?![A-Za-z])",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static IReadOnlyList<string> ExtractPhotoHintsFromPdf(string pdfPath)
    {
        if (string.IsNullOrWhiteSpace(pdfPath) || !File.Exists(pdfPath))
            return Array.Empty<string>();

        const int maxPagesWithLabeledHints = 2;
        var labeledPhotoKeys = new List<string>();
        var genericPhotoKeys = new List<string>();
        IReadOnlyList<PageInfo> pages;
        try
        {
            pages = ReadPdfPages(pdfPath);
        }
        catch
        {
            return Array.Empty<string>();
        }

        var labeledPages = 0;
        foreach (var page in pages)
        {
            var labeledCountBefore = labeledPhotoKeys.Count;
            foreach (Match m in PhotoAfterLabelRegex.Matches(page.Text))
            {
                AddPhotoLookupKeys(m.Groups["name"].Value, labeledPhotoKeys);
            }

            if (labeledPhotoKeys.Count > labeledCountBefore)
            {
                labeledPages++;
                if (labeledPages >= maxPagesWithLabeledHints)
                    break;
            }

            foreach (Match m in PhotoTokenRegex.Matches(page.Text))
            {
                AddPhotoLookupKeys(m.Groups["name"].Value, genericPhotoKeys);
            }
        }

        if (labeledPhotoKeys.Count > 0)
            return labeledPhotoKeys;

        return genericPhotoKeys;
    }

    private static void AddPhotoLookupKeys(string? raw, List<string> keys)
    {
        foreach (var key in EnumeratePhotoLookupKeys(raw))
        {
            if (!keys.Contains(key, StringComparer.OrdinalIgnoreCase))
                keys.Add(key);
        }
    }

    private static IEnumerable<string> EnumeratePhotoLookupKeys(string? raw)
    {
        var fileName = NormalizeVideoFileName(raw);
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
}
