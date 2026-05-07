using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure;

/// <summary>
/// HoldingFolderDistributor — Schacht-PDF-Distribution (partial class).
///
/// Refactor 2026-05-07 (Etappe 4, Charge R10): Public DistributeShafts*
/// + DistributeShaftCore + ExpandSelectedShaftPdfFiles ausgegliedert.
/// Mechanisch — keine Verhaltensaenderung.
///
/// Per-Page-Splitter SplitPdfIntoShafts und Per-Result-Distributor
/// HandleParsedShaftDistribution bleiben in der Hauptdatei (gehoeren
/// semantisch zur Schacht-Domain, sind aber tief mit weiteren Helfern
/// verflochten — Bewegung in spaetere Charge moeglich).
/// </summary>
public static partial class HoldingFolderDistributor
{
    public static IReadOnlyList<DistributionResult> DistributeShafts(
        string pdfSourceFolder,
        string destGemeindeFolder,
        bool moveInsteadOfCopy = false,
        bool overwrite = false,
        Project? project = null,
        IProgress<DistributionProgress>? progress = null)
    {
        if (!Directory.Exists(pdfSourceFolder))
            return new[] { new DistributionResult(false, $"PDF folder not found: {pdfSourceFolder}", pdfSourceFolder, null, null, null, null, null, VideoMatchStatus.NotChecked) };

        var pdfFiles = Directory.EnumerateFiles(pdfSourceFolder, "*.pdf", SearchOption.AllDirectories)
            .Where(p => !Path.GetFileName(p).StartsWith("split_", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (pdfFiles.Count == 0)
            return new[] { new DistributionResult(false, $"No PDF files found (recursive) in: {pdfSourceFolder}", pdfSourceFolder, null, null, null, null, null, VideoMatchStatus.NotChecked) };

        return DistributeShaftCore(pdfFiles, destGemeindeFolder, moveInsteadOfCopy, overwrite, project, progress);
    }

    public static IReadOnlyList<DistributionResult> DistributeShaftFiles(
        IEnumerable<string> pdfFiles,
        string destGemeindeFolder,
        bool moveInsteadOfCopy = false,
        bool overwrite = false,
        Project? project = null,
        IProgress<DistributionProgress>? progress = null)
    {
        var validPdfFiles = pdfFiles
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p.Trim())
            .Where(File.Exists)
            .Where(p => string.Equals(Path.GetExtension(p), ".pdf", StringComparison.OrdinalIgnoreCase))
            .Where(p => !Path.GetFileName(p).StartsWith("split_", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        validPdfFiles = ExpandSelectedShaftPdfFiles(validPdfFiles);

        if (validPdfFiles.Count == 0)
            return new[] { new DistributionResult(false, "No valid PDF files selected.", "", null, null, null, null, null, VideoMatchStatus.NotChecked) };

        return DistributeShaftCore(validPdfFiles, destGemeindeFolder, moveInsteadOfCopy, overwrite, project, progress);
    }

    private static IReadOnlyList<DistributionResult> DistributeShaftCore(
        IReadOnlyList<string> pdfFiles,
        string destGemeindeFolder,
        bool moveInsteadOfCopy,
        bool overwrite,
        Project? project,
        IProgress<DistributionProgress>? progress)
    {
        var results = new List<DistributionResult>();
        var shaftOutputPathByKey = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var processed = 0;
        foreach (var pdfPath in pdfFiles)
        {
            try
            {
                var pages = ReadPdfPages(pdfPath);
                var chunks = SplitPdfIntoShafts(pages);

                if (chunks.Count == 0)
                {
                    var pdfText = string.Join("\n\n", pages.Select(p => p.Text));
                    var parsed = ParseSchachtPdf(pdfText);
                    if (!parsed.Success)
                    {
                        results.Add(new DistributionResult(false, $"Parse failed: {parsed.Message}", pdfPath, null, null, null, null, null, VideoMatchStatus.NotChecked));
                        continue;
                    }

                    results.Add(HandleParsedShaftDistribution(parsed, pdfPath, pdfPath, destGemeindeFolder, moveInsteadOfCopy, overwrite, null, shaftOutputPathByKey, project));
                    continue;
                }

                if (chunks.Count == 1 && pages.Count == chunks[0].Pages.Count)
                {
                    results.Add(HandleParsedShaftDistribution(chunks[0].Parsed, pdfPath, pdfPath, destGemeindeFolder, moveInsteadOfCopy, overwrite, null, shaftOutputPathByKey, project));
                    continue;
                }

                foreach (var chunk in chunks)
                {
                    if (!chunk.Parsed.Success)
                    {
                        results.Add(new DistributionResult(false, "Parse failed for chunk", pdfPath, null, null, null, null, null, VideoMatchStatus.NotChecked));
                        continue;
                    }

                    var pageRange = BuildPageRange(chunk.Pages);
                    var tempPdfPath = Path.Combine(Path.GetTempPath(), $"split_{Guid.NewGuid():N}.pdf");
                    try
                    {
                        WritePdfPages(pdfPath, chunk.Pages, tempPdfPath);
                        results.Add(HandleParsedShaftDistribution(chunk.Parsed, pdfPath, tempPdfPath, destGemeindeFolder, moveInsteadOfCopy: false, overwrite, pageRange, shaftOutputPathByKey, project));
                    }
                    finally
                    {
                        try { if (File.Exists(tempPdfPath)) File.Delete(tempPdfPath); } catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                results.Add(new DistributionResult(false, ex.Message, pdfPath, null, null, null, null, null, VideoMatchStatus.NotChecked));
            }
            finally
            {
                processed++;
                progress?.Report(new DistributionProgress(processed, pdfFiles.Count, pdfPath));
            }
        }

        return results;
    }

    private static List<string> ExpandSelectedShaftPdfFiles(IReadOnlyList<string> selectedPdfFiles)
    {
        var expanded = new HashSet<string>(selectedPdfFiles, StringComparer.OrdinalIgnoreCase);

        foreach (var pdfPath in selectedPdfFiles)
        {
            var directory = Path.GetDirectoryName(pdfPath);
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
                continue;

            var name = Path.GetFileNameWithoutExtension(pdfPath);
            var isProtocol = name.Contains("schachtprotokoll", StringComparison.OrdinalIgnoreCase);
            var isPhotos = name.Contains("schachtfotos", StringComparison.OrdinalIgnoreCase);
            if (!isProtocol && !isPhotos)
                continue;

            foreach (var sibling in Directory.EnumerateFiles(directory, "*.pdf", SearchOption.TopDirectoryOnly))
            {
                var siblingName = Path.GetFileNameWithoutExtension(sibling);
                if (isProtocol && siblingName.Contains("schachtfotos", StringComparison.OrdinalIgnoreCase))
                    expanded.Add(sibling);
                else if (isPhotos && siblingName.Contains("schachtprotokoll", StringComparison.OrdinalIgnoreCase))
                    expanded.Add(sibling);
            }
        }

        return expanded.ToList();
    }
}
