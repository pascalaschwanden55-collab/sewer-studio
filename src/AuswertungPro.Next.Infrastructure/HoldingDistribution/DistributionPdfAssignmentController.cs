using System.Globalization;
using System.Text.RegularExpressions;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Export;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Map;

namespace AuswertungPro.Next.Infrastructure.HoldingDistribution;

internal sealed record DichtheitPageAssignment(
    int MainPage,
    IReadOnlyList<int> PageNumbers,
    string? HaltungId,
    string DateStamp,
    bool IsSchacht);

/// <summary>
/// Liest Verteil-PDFs und ordnet Dichtheitsseiten bzw. unbekannte PDFs einer Haltung zu.
/// Dateiweise Fehlerbehandlung bleibt beim aufrufenden Verteiler, damit ein defektes PDF
/// die weiteren Dateien nicht stoppt.
/// </summary>
internal static class DistributionPdfAssignmentController
{
    private const string UnassignedFolderName = "keine_Zuordnung";
    private static readonly Regex PhotoAfterLabelRegex = new(
        @"Foto\s*:\s*(?<name>\d{1,5}_\d{1,5}_\d{1,7}_[A-Za-z](?:\.(?:jpe?g|png|bmp|tif|tiff))?)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex PhotoTokenRegex = new(
        @"(?<![A-Za-z0-9])(?<name>\d{1,5}_\d{1,5}_\d{1,7}_[A-Za-z](?:\.(?:jpe?g|png|bmp|tif|tiff))?)(?![A-Za-z])",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    internal static IReadOnlyList<DistributionPdfPage> ReadPages(string pdfPath)
        => DistributionPdfPageReader.Current.ReadPages(pdfPath);

    internal static IReadOnlyList<DichtheitPageAssignment> ExtractDichtheitPerPage(
        IReadOnlyList<DistributionPdfPage> pages,
        Project? project,
        string destinationMunicipalityFolder,
        IHaltungCadastreResolver? cadastre = null)
    {
        var results = new List<DichtheitPageAssignment>();

        foreach (var page in pages)
        {
            var text = page.Text;
            if (text.Contains("Kontrollinformation"))
            {
                if (results.Count > 0)
                {
                    var previous = results[^1];
                    var extendedPages = new List<int>(previous.PageNumbers) { page.PageNumber };
                    results[^1] = previous with { PageNumbers = extendedPages };
                }
                continue;
            }

            var dateMatch = Regex.Match(text, @"(\d{4})/(\d{2})/(\d{2})");
            var dateStamp = dateMatch.Success
                ? $"{dateMatch.Groups[1].Value}{dateMatch.Groups[2].Value}{dateMatch.Groups[3].Value}"
                : HoldingTextParser.TryFindInspectionDate(text)?.ToString("yyyyMMdd", CultureInfo.InvariantCulture)
                  ?? "00000000";

            var isShaft = text.Contains("Prufgegenstand / Schacht", StringComparison.OrdinalIgnoreCase)
                          || text.Contains("Pruefgegenstand / Schacht", StringComparison.OrdinalIgnoreCase)
                          || text.Contains("Prüfgegenstand / Schacht", StringComparison.OrdinalIgnoreCase);
            string? holdingId = null;

            foreach (var line in text.Split('\n'))
            {
                if (line.Contains("Ebikon", StringComparison.OrdinalIgnoreCase)
                    || line.Contains("Altdorf", StringComparison.OrdinalIgnoreCase)
                    || line.Contains("GPS", StringComparison.OrdinalIgnoreCase)
                    || Regex.IsMatch(line, @"gepr[uü]ft\s+bei", RegexOptions.IgnoreCase)
                    || ShaftCandidateScanner.IsNoiseLine(line))
                {
                    continue;
                }

                var pair = DichtheitShaftParser.TryMatchPairLine(line);
                if (pair is null)
                    continue;

                var (a, b) = pair.Value;
                holdingId = ResolveHoldingOrder(a, b, project, destinationMunicipalityFolder) ?? $"{a}-{b}";
                break;
            }

            if (holdingId == null && isShaft)
            {
                var shaftMatch = Regex.Match(text, @"(?<!\d)(\d{4,6})\s*:?\s*Strang", RegexOptions.IgnoreCase);
                if (shaftMatch.Success)
                    holdingId = $"Schacht_{shaftMatch.Groups[1].Value}";
            }

            if (holdingId == null)
            {
                var (shaftA, shaftB) = DichtheitShaftParser.TryExtractShafts(text);
                if (!string.IsNullOrWhiteSpace(shaftA) && !string.IsNullOrWhiteSpace(shaftB))
                {
                    holdingId = ResolveHoldingOrder(shaftA, shaftB, project, destinationMunicipalityFolder)
                                ?? $"{shaftA}-{shaftB}";
                }
            }
            if (holdingId == null)
                holdingId = ShaftCandidateScanner.TryExtractFromShafts(text);

            if (cadastre is not null && cadastre.Count > 0)
            {
                var (pairA, pairB) = string.IsNullOrWhiteSpace(holdingId)
                    ? ("", "")
                    : HaltungCadastreExtractor.SplitShaftPair(holdingId!);

                if (!string.IsNullOrEmpty(pairA) && cadastre.TryResolvePair(pairA, pairB, out var canonical))
                    holdingId = canonical;
                else if (string.IsNullOrWhiteSpace(holdingId))
                    holdingId = ResolveViaCadastre(text, cadastre);
            }

            results.Add(new DichtheitPageAssignment(
                page.PageNumber,
                new List<int> { page.PageNumber },
                holdingId,
                dateStamp,
                isShaft && holdingId?.StartsWith("Schacht_") == true));
        }

        return results;
    }

    internal static string? ResolveViaCadastre(string text, IHaltungCadastreResolver? cadastre)
    {
        if (cadastre is null || cadastre.Count == 0 || string.IsNullOrWhiteSpace(text))
            return null;

        var hits = cadastre.ResolveFromCandidates(ShaftCandidateScanner.GatherShaftCandidates(text));
        if (hits.Count != 1)
            hits = cadastre.ResolveFromCandidates(ShaftCandidateScanner.GatherAllNumberCandidates(text));
        return hits.Count == 1 ? hits[0] : null;
    }

    internal static string ResolveDistributionRoot(
        string destinationMunicipalityFolder,
        string? holdingId,
        IHaltungCadastreResolver? cadastre)
    {
        if (cadastre is null || cadastre.Count == 0 || string.IsNullOrWhiteSpace(holdingId))
            return destinationMunicipalityFolder;
        if (holdingId.StartsWith("Schacht_", StringComparison.OrdinalIgnoreCase))
            return destinationMunicipalityFolder;

        var (a, b) = HaltungCadastreExtractor.SplitShaftPair(holdingId);
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
            return destinationMunicipalityFolder;

        return cadastre.PairExists(a, b)
            ? destinationMunicipalityFolder
            : Path.Combine(destinationMunicipalityFolder, UnassignedFolderName);
    }

    internal static string ResolveHoldingOrder(
        string a,
        string b,
        Project? project,
        string destinationMunicipalityFolder)
    {
        var ab = $"{a}-{b}";
        var ba = $"{b}-{a}";

        if (project is not null)
        {
            foreach (var record in project.Data)
            {
                var name = record.GetFieldValue("Haltungsname")?.Trim() ?? "";
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                var normalized = HoldingIdNormalizer.NormalizeHaltungId(name);
                var stripped = HoldingIdNormalizer.StripNodePrefixes(ProjectPathResolver.SanitizePathSegment(normalized));
                if (Matches(normalized, stripped, ab))
                    return ab;
                if (Matches(normalized, stripped, ba))
                    return ba;
            }
        }

        if (Directory.Exists(destinationMunicipalityFolder))
        {
            var abSanitized = ProjectPathResolver.SanitizePathSegment(HoldingIdNormalizer.NormalizeHaltungId(ab));
            var baSanitized = ProjectPathResolver.SanitizePathSegment(HoldingIdNormalizer.NormalizeHaltungId(ba));
            var abStripped = HoldingIdNormalizer.StripNodePrefixes(abSanitized);
            var baStripped = HoldingIdNormalizer.StripNodePrefixes(baSanitized);

            foreach (var directory in Directory.EnumerateDirectories(destinationMunicipalityFolder))
            {
                var directoryName = Path.GetFileName(directory) ?? "";
                var directoryStripped = HoldingIdNormalizer.StripNodePrefixes(directoryName);
                if (string.Equals(directoryName, abSanitized, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(directoryStripped, abStripped, StringComparison.OrdinalIgnoreCase))
                {
                    return ab;
                }
                if (string.Equals(directoryName, baSanitized, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(directoryStripped, baStripped, StringComparison.OrdinalIgnoreCase))
                {
                    return ba;
                }
            }
        }

        return ab;
    }

    internal static string? MatchPdfToHolding(
        string pdfPath,
        IReadOnlyDictionary<string, string> distributedHoldings)
    {
        if (distributedHoldings.Count == 0)
            return null;

        var fileName = Path.GetFileNameWithoutExtension(pdfPath) ?? "";
        var pairMatch = Regex.Match(
            fileName,
            @"((?:\d{2,}\.\d{2,}|\d{4,})\s*[-]\s*(?:\d{2,}\.\d{2,}|\d{4,}))");
        if (pairMatch.Success)
        {
            var extracted = HoldingIdNormalizer.NormalizeHaltungId(pairMatch.Groups[1].Value);
            if (distributedHoldings.TryGetValue(extracted, out var folder))
                return folder;

            var stripped = HoldingIdNormalizer.StripNodePrefixes(extracted);
            foreach (var pair in distributedHoldings)
            {
                if (string.Equals(
                        HoldingIdNormalizer.StripNodePrefixes(pair.Key),
                        stripped,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return pair.Value;
                }
            }
        }

        foreach (var pair in distributedHoldings)
        {
            var holdingDirectoryName = Path.GetFileName(pair.Value) ?? "";
            if (!string.IsNullOrWhiteSpace(holdingDirectoryName)
                && fileName.Contains(holdingDirectoryName, StringComparison.OrdinalIgnoreCase))
            {
                return pair.Value;
            }
        }

        return null;
    }

    internal static IReadOnlyList<string> ExtractPhotoHints(string pdfPath)
    {
        if (string.IsNullOrWhiteSpace(pdfPath) || !File.Exists(pdfPath))
            return Array.Empty<string>();

        const int maxPagesWithLabeledHints = 2;
        var labeledPhotoKeys = new List<string>();
        var genericPhotoKeys = new List<string>();
        IReadOnlyList<DistributionPdfPage> pages;
        try
        {
            pages = ReadPages(pdfPath);
        }
        catch
        {
            return Array.Empty<string>();
        }

        var labeledPages = 0;
        foreach (var page in pages)
        {
            var labeledCountBefore = labeledPhotoKeys.Count;
            foreach (Match match in PhotoAfterLabelRegex.Matches(page.Text))
                PhotoTokenNormalizer.AddPhotoLookupKeys(match.Groups["name"].Value, labeledPhotoKeys);

            if (labeledPhotoKeys.Count > labeledCountBefore)
            {
                labeledPages++;
                if (labeledPages >= maxPagesWithLabeledHints)
                    break;
            }

            foreach (Match match in PhotoTokenRegex.Matches(page.Text))
                PhotoTokenNormalizer.AddPhotoLookupKeys(match.Groups["name"].Value, genericPhotoKeys);
        }

        return labeledPhotoKeys.Count > 0 ? labeledPhotoKeys : genericPhotoKeys;
    }

    private static bool Matches(string normalized, string stripped, string candidate)
    {
        var sanitizedCandidate = ProjectPathResolver.SanitizePathSegment(candidate);
        return string.Equals(normalized, candidate, StringComparison.OrdinalIgnoreCase)
               || string.Equals(
                   stripped,
                   HoldingIdNormalizer.StripNodePrefixes(sanitizedCandidate),
                   StringComparison.OrdinalIgnoreCase);
    }
}
