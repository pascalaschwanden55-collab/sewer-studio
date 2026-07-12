using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AuswertungPro.Next.Infrastructure.Import.Pdf;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace AuswertungPro.Next.Infrastructure.HoldingDistribution;

internal sealed record PdfTextLayerRewriteResult(
    bool Success,
    bool Corrected,
    string OutputPdfPath,
    int MatchCount,
    int PageCount,
    string Message);

/// <summary>
/// Erstellt eine korrigierte PDF-Kopie. Das Ersetzen der Originaldatei bleibt beim Aufrufer.
/// </summary>
internal static class PdfTextLayerRewriter
{
    internal static bool CanRewrite(string? oldValue, string? newValue)
        => BuildRenameReplacements(oldValue, newValue).Count > 0;

    internal static PdfTextLayerRewriteResult TryRewriteHoldingNumber(
        string sourcePdfPath,
        string? oldValue,
        string? newValue)
        => TryRewrite(sourcePdfPath, BuildRenameReplacements(oldValue, newValue));

    private static IReadOnlyList<PdfTextReplacementTarget> BuildRenameReplacements(
        string? oldValue,
        string? newValue)
    {
        var oldToken = (oldValue ?? string.Empty).Trim();
        var newToken = (newValue ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(oldToken)
            || string.IsNullOrWhiteSpace(newToken)
            || string.Equals(oldToken, newToken, StringComparison.OrdinalIgnoreCase))
            return Array.Empty<PdfTextReplacementTarget>();

        return new[] { new PdfTextReplacementTarget(oldToken, newToken) };
    }

    internal static PdfTextLayerRewriteResult TryRewrite(
        string sourcePdfPath,
        IReadOnlyList<PdfTextReplacementTarget> replacements)
    {
        if (string.IsNullOrWhiteSpace(sourcePdfPath) || !File.Exists(sourcePdfPath))
            return new PdfTextLayerRewriteResult(false, false, sourcePdfPath, 0, 0, "PDF nicht gefunden.");

        if (replacements.Count == 0)
            return new PdfTextLayerRewriteResult(true, false, sourcePdfPath, 0, 0, string.Empty);

        var effectiveReplacements = replacements
            .Where(r => !string.IsNullOrWhiteSpace(r.SearchText)
                        && !string.IsNullOrWhiteSpace(r.ReplacementText)
                        && !string.Equals(r.SearchText, r.ReplacementText, StringComparison.OrdinalIgnoreCase))
            .GroupBy(r => r.SearchText.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();

        if (effectiveReplacements.Count == 0)
            return new PdfTextLayerRewriteResult(true, false, sourcePdfPath, 0, 0, string.Empty);

        var correctedTempPath = Path.Combine(Path.GetTempPath(), $"pdfcorr_{Guid.NewGuid():N}.pdf");
        try
        {
            PdfImportSafetyPolicy.ThrowIfFileTooLarge(sourcePdfPath);
            using var sourceDocument = PdfDocument.Open(sourcePdfPath);
            PdfImportSafetyPolicy.ThrowIfTooManyPages(sourceDocument.NumberOfPages);
            using var builder = new PdfDocumentBuilder();
            var overlayFont = builder.AddStandard14Font(Standard14Font.Helvetica);

            var totalMatches = 0;
            var pageCount = 0;

            foreach (var page in sourceDocument.GetPages())
            {
                var pageBuilder = builder.AddPage(sourceDocument, page.Number);
                var matches = PdfTextReplacementMatcher.FindMatches(page, effectiveReplacements);
                if (matches.Count == 0)
                    continue;

                pageCount++;
                totalMatches += matches.Count;

                pageBuilder.NewContentStreamAfter();
                foreach (var match in matches.OrderBy(m => m.StartLetterIndex))
                    DrawReplacement(pageBuilder, overlayFont, match);
            }

            if (totalMatches == 0)
                return new PdfTextLayerRewriteResult(true, false, sourcePdfPath, 0, 0, "Keine Treffer im Text-Layer gefunden.");

            File.WriteAllBytes(correctedTempPath, builder.Build());
            return new PdfTextLayerRewriteResult(
                true,
                true,
                correctedTempPath,
                totalMatches,
                pageCount,
                $"Text-Layer aktualisiert ({totalMatches} Treffer auf {pageCount} Seiten).");
        }
        catch (Exception ex)
        {
            TryDelete(correctedTempPath);
            return new PdfTextLayerRewriteResult(
                false,
                false,
                sourcePdfPath,
                0,
                0,
                $"PDF-Korrektur fehlgeschlagen: {ex.Message}");
        }
    }

    private static void DrawReplacement(
        PdfPageBuilder pageBuilder,
        PdfDocumentBuilder.AddedFont overlayFont,
        PdfTextReplacementMatch match)
    {
        var width = Math.Max(1d, match.Right - match.Left);
        var height = Math.Max(1d, match.Top - match.Bottom);
        var left = Math.Max(0d, match.Left - 0.40d);
        var bottom = Math.Max(0d, match.Bottom - 0.25d);

        pageBuilder.SetTextAndFillColor(255, 255, 255);
        pageBuilder.DrawRectangle(
            new PdfPoint((decimal)left, (decimal)bottom),
            (decimal)(width + 0.80d),
            (decimal)(height + 0.50d),
            0.1m,
            fill: true);

        var fontSize = Math.Max(1d, match.FontSize);
        var textPosition = new PdfPoint(match.StartBaseLine.X, match.StartBaseLine.Y);
        var measuredLetters = pageBuilder.MeasureText(
            match.Replacement.ReplacementText,
            (decimal)fontSize,
            textPosition,
            overlayFont);
        var measuredWidth = MeasureLettersWidth(measuredLetters);
        if (measuredWidth > width && measuredWidth > 0d)
            fontSize = Math.Max(1d, fontSize * (width / measuredWidth));

        pageBuilder.SetTextAndFillColor(0, 0, 0);
        pageBuilder.AddText(match.Replacement.ReplacementText, (decimal)fontSize, textPosition, overlayFont);
        pageBuilder.ResetColor();
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

        return left == double.MaxValue || right == double.MinValue ? 0d : Math.Max(0d, right - left);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Best-effort cleanup.
        }
    }
}
