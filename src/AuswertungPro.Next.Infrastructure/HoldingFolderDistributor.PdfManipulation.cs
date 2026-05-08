using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace AuswertungPro.Next.Infrastructure;

/// <summary>
/// HoldingFolderDistributor — PDF-Manipulation (partial class).
///
/// Refactor 2026-05-08 (Etappe 6, Charge R16): PDF-Schreibe- und
/// Korrektur-Pfade. WritePdfPages/AppendPdfFile fuer Page-Splitting,
/// BuildRenameReplacements + TryCorrectPdfTextLayer + Match-Helfer fuer
/// In-Place-Text-Korrektur. Mechanisch — keine Verhaltensaenderung.
/// </summary>
public static partial class HoldingFolderDistributor
{
    private sealed record PdfTextReplacement(string SearchText, string ReplacementText);

    private sealed record PdfTextReplacementMatch(
        PdfTextReplacement Replacement,
        int StartLetterIndex,
        int EndLetterIndex,
        double Left,
        double Bottom,
        double Right,
        double Top,
        PdfPoint StartBaseLine,
        double FontSize);

    private sealed record PdfCorrectionResult(
        bool Success,
        bool Corrected,
        string OutputPdfPath,
        int MatchCount,
        int PageCount,
        string Message);

    private static void WritePdfPages(string sourcePdfPath, IReadOnlyList<int> pages, string destPdfPath)
    {
        using var doc = PdfDocument.Open(sourcePdfPath);
        using var builder = new PdfDocumentBuilder();

        foreach (var pageNumber in pages)
            builder.AddPage(doc, pageNumber);

        var bytes = builder.Build();
        File.WriteAllBytes(destPdfPath, bytes);
    }

    private static void AppendPdfFile(string targetPdfPath, string additionalPdfPath, bool removeAdditionalWhenMoved)
    {
        if (string.IsNullOrWhiteSpace(targetPdfPath)
            || string.IsNullOrWhiteSpace(additionalPdfPath)
            || !File.Exists(targetPdfPath)
            || !File.Exists(additionalPdfPath))
            throw new FileNotFoundException("PDF for append not found.");

        if (string.Equals(targetPdfPath, additionalPdfPath, StringComparison.OrdinalIgnoreCase))
            return;

        var mergedTempPath = Path.Combine(Path.GetTempPath(), $"merge_{Guid.NewGuid():N}.pdf");
        try
        {
            using (var targetDoc = PdfDocument.Open(targetPdfPath))
            using (var additionalDoc = PdfDocument.Open(additionalPdfPath))
            using (var builder = new PdfDocumentBuilder())
            {
                foreach (var page in targetDoc.GetPages())
                    builder.AddPage(targetDoc, page.Number);

                foreach (var page in additionalDoc.GetPages())
                    builder.AddPage(additionalDoc, page.Number);

                var bytes = builder.Build();
                File.WriteAllBytes(mergedTempPath, bytes);
            }

            File.Copy(mergedTempPath, targetPdfPath, overwrite: true);

            if (removeAdditionalWhenMoved)
            {
                try
                {
                    if (File.Exists(additionalPdfPath))
                        File.Delete(additionalPdfPath);
                }
                catch
                {
                    // Best-effort cleanup for move semantics.
                }
            }
        }
        finally
        {
            try
            {
                if (File.Exists(mergedTempPath))
                    File.Delete(mergedTempPath);
            }
            catch
            {
                // ignore
            }
        }
    }

    private static IReadOnlyList<PdfTextReplacement> BuildRenameReplacements(string oldValue, string newValue)
    {
        var oldToken = (oldValue ?? string.Empty).Trim();
        var newToken = (newValue ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(oldToken)
            || string.IsNullOrWhiteSpace(newToken)
            || string.Equals(oldToken, newToken, StringComparison.OrdinalIgnoreCase))
            return Array.Empty<PdfTextReplacement>();

        return new[] { new PdfTextReplacement(oldToken, newToken) };
    }

    private static PdfCorrectionResult TryCorrectPdfTextLayer(string sourcePdfPath, IReadOnlyList<PdfTextReplacement> replacements)
    {
        if (string.IsNullOrWhiteSpace(sourcePdfPath) || !File.Exists(sourcePdfPath))
            return new PdfCorrectionResult(false, false, sourcePdfPath, 0, 0, "PDF nicht gefunden.");

        if (replacements.Count == 0)
            return new PdfCorrectionResult(true, false, sourcePdfPath, 0, 0, string.Empty);

        var effectiveReplacements = replacements
            .Where(r => !string.IsNullOrWhiteSpace(r.SearchText)
                        && !string.IsNullOrWhiteSpace(r.ReplacementText)
                        && !string.Equals(r.SearchText, r.ReplacementText, StringComparison.OrdinalIgnoreCase))
            .GroupBy(r => r.SearchText.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();

        if (effectiveReplacements.Count == 0)
            return new PdfCorrectionResult(true, false, sourcePdfPath, 0, 0, string.Empty);

        var correctedTempPath = Path.Combine(Path.GetTempPath(), $"pdfcorr_{Guid.NewGuid():N}.pdf");
        try
        {
            using var sourceDocument = PdfDocument.Open(sourcePdfPath);
            using var builder = new PdfDocumentBuilder();
            var overlayFont = builder.AddStandard14Font(Standard14Font.Helvetica);

            var totalMatches = 0;
            var pageCount = 0;

            foreach (var page in sourceDocument.GetPages())
            {
                var pageBuilder = builder.AddPage(sourceDocument, page.Number);
                var matches = FindPdfTextReplacementMatches(page, effectiveReplacements);
                if (matches.Count == 0)
                    continue;

                pageCount++;
                totalMatches += matches.Count;

                pageBuilder.NewContentStreamAfter();
                foreach (var match in matches.OrderBy(m => m.StartLetterIndex))
                {
                    var width = Math.Max(1d, match.Right - match.Left);
                    var height = Math.Max(1d, match.Top - match.Bottom);
                    var left = Math.Max(0d, match.Left - 0.40d);
                    var bottom = Math.Max(0d, match.Bottom - 0.25d);
                    var drawWidth = width + 0.80d;
                    var drawHeight = height + 0.50d;

                    pageBuilder.SetTextAndFillColor(255, 255, 255);
                    pageBuilder.DrawRectangle(
                        new PdfPoint((decimal)left, (decimal)bottom),
                        (decimal)drawWidth,
                        (decimal)drawHeight,
                        0.1m,
                        fill: true);

                    var fontSize = Math.Max(1d, match.FontSize);
                    var textPosition = new PdfPoint(match.StartBaseLine.X, match.StartBaseLine.Y);
                    var measuredLetters = pageBuilder.MeasureText(match.Replacement.ReplacementText, (decimal)fontSize, textPosition, overlayFont);
                    var measuredWidth = MeasureLettersWidth(measuredLetters);
                    if (measuredWidth > width && measuredWidth > 0d)
                    {
                        var scale = width / measuredWidth;
                        fontSize = Math.Max(1d, fontSize * scale);
                    }

                    pageBuilder.SetTextAndFillColor(0, 0, 0);
                    pageBuilder.AddText(match.Replacement.ReplacementText, (decimal)fontSize, textPosition, overlayFont);
                    pageBuilder.ResetColor();
                }
            }

            if (totalMatches == 0)
                return new PdfCorrectionResult(true, false, sourcePdfPath, 0, 0, "Keine Treffer im Text-Layer gefunden.");

            var bytes = builder.Build();
            File.WriteAllBytes(correctedTempPath, bytes);
            return new PdfCorrectionResult(
                true,
                true,
                correctedTempPath,
                totalMatches,
                pageCount,
                $"Text-Layer aktualisiert ({totalMatches} Treffer auf {pageCount} Seiten).");
        }
        catch (Exception ex)
        {
            try
            {
                if (File.Exists(correctedTempPath))
                    File.Delete(correctedTempPath);
            }
            catch
            {
                // Best-effort cleanup.
            }

            return new PdfCorrectionResult(false, false, sourcePdfPath, 0, 0, $"PDF-Korrektur fehlgeschlagen: {ex.Message}");
        }
    }

    private static IReadOnlyList<PdfTextReplacementMatch> FindPdfTextReplacementMatches(
        Page page,
        IReadOnlyList<PdfTextReplacement> replacements)
    {
        var letters = page.Letters?.ToList() ?? new List<Letter>();
        if (letters.Count == 0 || replacements.Count == 0)
            return Array.Empty<PdfTextReplacementMatch>();

        var flatTextBuilder = new StringBuilder();
        var charToLetterIndex = new List<int>(letters.Count * 2);
        for (var i = 0; i < letters.Count; i++)
        {
            var value = letters[i].Value;
            if (string.IsNullOrEmpty(value))
                continue;

            flatTextBuilder.Append(value);
            for (var c = 0; c < value.Length; c++)
                charToLetterIndex.Add(i);
        }

        var flatText = flatTextBuilder.ToString();
        if (flatText.Length == 0 || charToLetterIndex.Count == 0)
            return Array.Empty<PdfTextReplacementMatch>();

        var matches = new List<PdfTextReplacementMatch>();
        foreach (var replacement in replacements)
        {
            var search = replacement.SearchText.Trim();
            if (string.IsNullOrWhiteSpace(search) || search.Length > flatText.Length)
                continue;

            var searchStart = 0;
            while (searchStart <= flatText.Length - search.Length)
            {
                var foundIndex = flatText.IndexOf(search, searchStart, StringComparison.OrdinalIgnoreCase);
                if (foundIndex < 0)
                    break;

                if (IsReplacementBoundary(flatText, foundIndex, search.Length))
                {
                    var foundEnd = foundIndex + search.Length - 1;
                    if (foundEnd >= 0 && foundEnd < charToLetterIndex.Count)
                    {
                        var startLetterIndex = charToLetterIndex[foundIndex];
                        var endLetterIndex = charToLetterIndex[foundEnd];
                        var match = TryBuildPdfTextReplacementMatch(letters, replacement, startLetterIndex, endLetterIndex);
                        if (match is not null)
                            matches.Add(match);
                    }
                }

                searchStart = foundIndex + search.Length;
            }
        }

        if (matches.Count <= 1)
            return matches;

        return FilterOverlappingReplacementMatches(matches);
    }

    private static PdfTextReplacementMatch? TryBuildPdfTextReplacementMatch(
        IReadOnlyList<Letter> letters,
        PdfTextReplacement replacement,
        int startLetterIndex,
        int endLetterIndex)
    {
        if (startLetterIndex < 0 || endLetterIndex < startLetterIndex || endLetterIndex >= letters.Count)
            return null;

        var left = double.MaxValue;
        var right = double.MinValue;
        var bottom = double.MaxValue;
        var top = double.MinValue;
        double fontSizeSum = 0;
        var fontSizeCount = 0;
        var startBaseline = letters[startLetterIndex].StartBaseLine;

        for (var i = startLetterIndex; i <= endLetterIndex; i++)
        {
            var glyph = letters[i].GlyphRectangle;
            left = Math.Min(left, glyph.Left);
            right = Math.Max(right, glyph.Right);
            bottom = Math.Min(bottom, glyph.Bottom);
            top = Math.Max(top, glyph.Top);

            if (letters[i].FontSize > 0)
            {
                fontSizeSum += letters[i].FontSize;
                fontSizeCount++;
            }
        }

        if (left == double.MaxValue || right == double.MinValue || bottom == double.MaxValue || top == double.MinValue)
            return null;

        var fontSize = fontSizeCount > 0 ? fontSizeSum / fontSizeCount : 9d;
        return new PdfTextReplacementMatch(
            replacement,
            startLetterIndex,
            endLetterIndex,
            left,
            bottom,
            right,
            top,
            startBaseline,
            fontSize);
    }

    private static IReadOnlyList<PdfTextReplacementMatch> FilterOverlappingReplacementMatches(
        IReadOnlyList<PdfTextReplacementMatch> matches)
    {
        var accepted = new List<PdfTextReplacementMatch>();
        foreach (var candidate in matches
                     .OrderBy(m => m.StartLetterIndex)
                     .ThenByDescending(m => m.EndLetterIndex - m.StartLetterIndex))
        {
            var overlaps = accepted.Any(existing =>
                !(candidate.EndLetterIndex < existing.StartLetterIndex
                  || candidate.StartLetterIndex > existing.EndLetterIndex));
            if (!overlaps)
                accepted.Add(candidate);
        }

        return accepted;
    }

    private static bool IsReplacementBoundary(string text, int startIndex, int length)
    {
        if (startIndex < 0 || length <= 0 || startIndex + length > text.Length)
            return false;

        var before = startIndex > 0 ? text[startIndex - 1] : '\0';
        var afterIndex = startIndex + length;
        var after = afterIndex < text.Length ? text[afterIndex] : '\0';
        return !IsIdentifierCharacter(before) && !IsIdentifierCharacter(after);
    }

    private static bool IsIdentifierCharacter(char ch)
    {
        if (ch == '\0')
            return false;

        return char.IsLetterOrDigit(ch)
               || ch == '-'
               || ch == '_'
               || ch == '.';
    }

    private static double MeasureLettersWidth(IReadOnlyList<Letter> letters)
    {
        if (letters.Count == 0)
            return 0d;

        var left = double.MaxValue;
        var right = double.MinValue;
        foreach (var letter in letters)
        {
            var glyph = letter.GlyphRectangle;
            left = Math.Min(left, glyph.Left);
            right = Math.Max(right, glyph.Right);
        }

        if (left == double.MaxValue || right == double.MinValue)
            return 0d;

        return Math.Max(0d, right - left);
    }
}
