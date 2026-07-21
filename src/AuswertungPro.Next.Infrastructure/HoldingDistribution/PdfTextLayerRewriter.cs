using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using AuswertungPro.Next.Application.Export;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Infrastructure.Import.Pdf;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace AuswertungPro.Next.Infrastructure.HoldingDistribution;

/// <summary>
/// Erstellt korrigierte PDF-Kopien und kann sie atomar am bestehenden Zielpfad veroeffentlichen.
/// </summary>
public sealed class PdfTextLayerRewriteService : IPdfTextLayerRewriter
{
    private readonly IAtomicPdfFileReplacer _atomicPdfFileReplacer;
    private readonly ILogger<PdfTextLayerRewriteService> _logger;

    public PdfTextLayerRewriteService()
        : this(AtomicPdfFileReplacer.Current, NullLogger<PdfTextLayerRewriteService>.Instance)
    {
    }

    public PdfTextLayerRewriteService(IAtomicPdfFileReplacer atomicPdfFileReplacer)
        : this(atomicPdfFileReplacer, NullLogger<PdfTextLayerRewriteService>.Instance)
    {
    }

    public PdfTextLayerRewriteService(
        IAtomicPdfFileReplacer atomicPdfFileReplacer,
        ILogger<PdfTextLayerRewriteService> logger)
    {
        _atomicPdfFileReplacer = atomicPdfFileReplacer
            ?? throw new ArgumentNullException(nameof(atomicPdfFileReplacer));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public bool CanRewrite(string? oldValue, string? newValue)
        => BuildRenameReplacements(oldValue, newValue).Count > 0;

    public PdfTextLayerRewriteResult TryRewriteHoldingNumber(
        string sourcePdfPath,
        string? oldValue,
        string? newValue)
        => TryRewrite(sourcePdfPath, BuildRenameReplacements(oldValue, newValue));

    public PdfTextLayerBatchRewriteResult RewriteIdentifierInPlace(
        IReadOnlyList<string> pdfPaths,
        string? oldValue,
        string? newValue)
    {
        if (pdfPaths is null || pdfPaths.Count == 0 || !CanRewrite(oldValue, newValue))
            return new PdfTextLayerBatchRewriteResult(0, 0, 0);

        var rewritten = 0;
        var skipped = 0;
        var failed = 0;
        var failures = new List<PdfTextLayerBatchFailure>();
        foreach (var pdfPath in pdfPaths)
        {
            if (string.IsNullOrWhiteSpace(pdfPath) || !File.Exists(pdfPath))
            {
                skipped++;
                continue;
            }

            string? temporaryPdfPath = null;
            try
            {
                var result = TryRewriteHoldingNumber(pdfPath, oldValue, newValue);
                if (!result.Success)
                {
                    failed++;
                    RecordFailure(failures, pdfPath, result.Message);
                    continue;
                }

                if (!result.Corrected)
                {
                    skipped++;
                    continue;
                }

                temporaryPdfPath = result.OutputPdfPath;
                if (string.IsNullOrWhiteSpace(temporaryPdfPath)
                    || string.Equals(temporaryPdfPath, pdfPath, StringComparison.OrdinalIgnoreCase)
                    || !File.Exists(temporaryPdfPath))
                {
                    failed++;
                    RecordFailure(
                        failures,
                        pdfPath,
                        "Die erzeugte PDF-Korrekturdatei fehlt oder verweist auf die Quelldatei.");
                    continue;
                }

                _atomicPdfFileReplacer.ReplaceValidated(temporaryPdfPath, pdfPath);
                rewritten++;
            }
            catch (Exception ex)
            {
                failed++;
                RecordFailure(failures, pdfPath, ex.Message, ex);
            }
            finally
            {
                if (!string.IsNullOrWhiteSpace(temporaryPdfPath)
                    && !string.Equals(temporaryPdfPath, pdfPath, StringComparison.OrdinalIgnoreCase))
                {
                    TryDelete(temporaryPdfPath);
                }
            }
        }

        return new PdfTextLayerBatchRewriteResult(
            rewritten,
            skipped,
            failed,
            failures.ToArray());
    }

    private void RecordFailure(
        ICollection<PdfTextLayerBatchFailure> failures,
        string pdfPath,
        string? message,
        Exception? exception = null)
    {
        var effectiveMessage = string.IsNullOrWhiteSpace(message)
            ? "Unbekannter Fehler bei der PDF-Textkorrektur."
            : message.Trim();
        failures.Add(new PdfTextLayerBatchFailure(pdfPath, effectiveMessage));

        if (exception is null)
        {
            _logger.LogWarning(
                "PDF-Textkorrektur fehlgeschlagen fuer {PdfPath}: {Message}",
                pdfPath,
                effectiveMessage);
            return;
        }

        _logger.LogWarning(
            exception,
            "PDF-Textkorrektur fehlgeschlagen fuer {PdfPath}: {Message}",
            pdfPath,
            effectiveMessage);
    }

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

    private static PdfTextLayerRewriteResult TryRewrite(
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

/// <summary>Kompatible Fassade für bestehende Verteiler-Aufrufe.</summary>
public static class PdfTextLayerRewriter
{
    private static readonly IPdfTextLayerRewriter Default = new PdfTextLayerRewriteService();

    public static IPdfTextLayerRewriter Current => Default;

    [Obsolete("Die PDF-Textkorrektur-Fassade ist unveraenderbar. Abhaengigkeit direkt uebergeben.")]
    public static void Use(IPdfTextLayerRewriter rewriter)
    {
        ArgumentNullException.ThrowIfNull(rewriter);
        throw new NotSupportedException(
            "Die PDF-Textkorrektur-Fassade kann nicht mehr global ersetzt werden.");
    }

    internal static bool CanRewrite(string? oldValue, string? newValue) =>
        Current.CanRewrite(oldValue, newValue);

    internal static PdfTextLayerRewriteResult TryRewriteHoldingNumber(
        string sourcePdfPath,
        string? oldValue,
        string? newValue) =>
        Current.TryRewriteHoldingNumber(sourcePdfPath, oldValue, newValue);
}
