using System.Globalization;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Application.UseCases.BendSuggestions;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Ai.Training.Services;
using AuswertungPro.Next.Infrastructure.Import.Pdf;

internal sealed class PdfCodeScanRunner
{
    private readonly LegacyPdfImportService _importer = new();
    private readonly PdfProtocolExtractor _positionExtractor = new();

    public async Task<PdfCodeScanReport> RunAsync(PdfCodeScanOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var rootPath = Path.GetFullPath(options.RootPath);
        if (!Directory.Exists(rootPath))
            throw new DirectoryNotFoundException($"Scan-Wurzel nicht gefunden: {rootPath}");

        EnsureOutputIsOutsideCustomerRoot(rootPath, options.OutPath);

        var folders = Directory.GetDirectories(rootPath);
        var results = new List<HoldingScanResult>();
        var importErrors = 0;
        var pdfReadErrors = 0;
        var holdingsWithoutReadablePdf = 0;
        var scanned = 0;

        Console.WriteLine(
            $"Scanne {folders.Length} Haltungsordner unter {rootPath} ueber den Importpfad, Praefix {options.CodePrefix} ...");

        foreach (var folder in folders.OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            var result = await ScanHoldingAsync(folder, options.CodePrefix);
            importErrors += result.PdfsFehler;
            pdfReadErrors += result.PdfsLesefehler;
            if (result.PdfsLesbar == 0)
                holdingsWithoutReadablePdf++;
            if (result.Codes.Length > 0 || result.Fotos > 0)
                results.Add(result);

            scanned++;
            if (scanned % 200 == 0)
                Console.WriteLine($"  ... {scanned}/{folders.Length}");
        }

        var findings = results.SelectMany(result => result.Positionen).ToArray();
        var holdingsWithPrefix = results.Count(result => result.BefundeMitPraefix > 0);
        var isBccScan = string.Equals(options.CodePrefix, "BCC", StringComparison.Ordinal);
        var exclusions = isBccScan
            ? results
                .Where(result => !result.AuswahlGeeignet && result.BefundeMitPraefix > 0)
                .Select(result => new BccExclusion(result.Haltung, result.Ausschlussgrund ?? "Nicht fuer die BCC-Messauswahl geeignet."))
                .ToArray()
            : Array.Empty<BccExclusion>();

        BccSelectionSummary? selection = null;
        IReadOnlyList<CodeSummary> codeSummary;
        if (isBccScan)
        {
            var selectedHoldings = results
                .Where(result => result.AuswahlGeeignet && result.Positionen.Any(position => position.IstGueltigerBccUntercode))
                .ToArray();
            var selectedFindings = selectedHoldings
                .SelectMany(result => result.Positionen)
                .Where(position => position.IstGueltigerBccUntercode)
                .ToArray();
            selection = new BccSelectionSummary(
                selectedHoldings.Length,
                selectedFindings.Length,
                selectedFindings.Count(position => position.MeterStart is not null),
                selectedFindings.Count(position => position.VideoCounterSeconds is not null),
                selectedFindings.Count(position => position.VideoPath is not null));

            codeSummary = BendProtocolPositionPolicy.SupportedCodes
                .Select(code => BuildCodeSummary(code, results, selectedHoldings))
                .ToArray();
        }
        else
        {
            codeSummary = findings
                .Select(position => position.Code)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(code => code, StringComparer.Ordinal)
                .Select(code => BuildCodeSummary(code, results, results))
                .ToArray();
        }

        return new PdfCodeScanReport(
            SchemaVersion: 2,
            Klasse: options.CodePrefix,
            Stamm: rootPath,
            ErstelltUtc: DateTime.UtcNow,
            OrdnerGescannt: folders.Length,
            Treffer: results.Count,
            Zusammenfassung: new ScanSummary(
                holdingsWithPrefix,
                findings.Length,
                importErrors,
                pdfReadErrors,
                holdingsWithoutReadablePdf,
                findings.Count(position => position.MeterStart is not null),
                findings.Count(position => position.VideoCounterSeconds is not null),
                findings.Count(position => position.VideoPath is not null)),
            Bestandsabgleich: BuildInventoryCheck(options, holdingsWithPrefix, findings.Length),
            Messauswahl: selection,
            Untercodes: codeSummary,
            Ausschluesse: exclusions,
            Ergebnisse: results);
    }

    private static InventoryCheck? BuildInventoryCheck(
        PdfCodeScanOptions options,
        int holdings,
        int findings)
    {
        if (options.ExpectedHoldings is null || options.ExpectedFindings is null)
            return null;

        return new InventoryCheck(
            options.ExpectedHoldings.Value,
            options.ExpectedFindings.Value,
            holdings,
            findings,
            options.ExpectedHoldings.Value == holdings && options.ExpectedFindings.Value == findings);
    }

    private async Task<HoldingScanResult> ScanHoldingAsync(string folder, string codePrefix)
    {
        var holding = Path.GetFileName(folder);
        var pdfs = PdfCodeScanEvidenceReader.EnumeratePdfs(folder).ToArray();
        var videos = PdfCodeScanEvidenceReader.EnumerateVideos(folder).ToArray();
        var codes = new SortedSet<string>(StringComparer.Ordinal);
        var positions = new List<ProtocolPosition>();
        var photos = 0;
        var pdfsOk = 0;
        var pdfsError = 0;
        var pdfsReadable = 0;
        var pdfReadErrors = 0;
        var hasMalformedBcc = false;

        foreach (var pdf in pdfs)
        {
            var evidence = PdfCodeScanEvidenceReader.ReadPdfEvidence(pdf);
            photos += evidence.PhotoCount;
            hasMalformedBcc |= evidence.ContainsMalformedBcc;
            if (evidence.Readable)
                pdfsReadable++;
            else
                pdfReadErrors++;

            IReadOnlyList<VsaFinding> matchingFindings = Array.Empty<VsaFinding>();
            try
            {
                var project = new Project();
                var stats = _importer.ImportPdf(pdf, project, explicitPdfToTextPath: null);
                if (stats.Errors == 0)
                {
                    pdfsOk++;
                    matchingFindings = project.Data
                        .SelectMany(record => record.VsaFindings)
                        .Where(finding => NormalizeCode(finding.KanalSchadencode)
                            .StartsWith(codePrefix, StringComparison.Ordinal))
                        .ToArray();
                }
                else
                {
                    pdfsError++;
                }
            }
            catch
            {
                pdfsError++;
            }

            IReadOnlyList<GroundTruthEntry> extractedPositions = Array.Empty<GroundTruthEntry>();
            if (matchingFindings.Count > 0)
            {
                try
                {
                    extractedPositions = await _positionExtractor.ExtractAsync(pdf);
                }
                catch
                {
                    // Der echte Importbefund bleibt erhalten; nur die Zeit-Anreicherung fehlt.
                }
            }

            var usedExtractedPositions = new HashSet<int>();
            var videoMatch = PdfCodeScanEvidenceReader.MatchVideo(pdf, videos);
            foreach (var finding in matchingFindings)
            {
                var code = NormalizeCode(finding.KanalSchadencode);
                codes.Add(code);
                var counter = ResolveVideoCounter(finding, code, extractedPositions, usedExtractedPositions);
                positions.Add(new ProtocolPosition(
                    code,
                    finding.MeterStart,
                    finding.MeterEnd,
                    counter.DisplayValue,
                    counter.Value?.TotalSeconds,
                    counter.Source,
                    "vsa_finding",
                    pdf,
                    null,
                    videoMatch.Path,
                    videoMatch.Status,
                    string.Equals(codePrefix, "BCC", StringComparison.Ordinal)
                    && BendProtocolPositionPolicy.IsSupportedCode(code)));
            }

        }

        var malformedBcc = hasMalformedBcc
                           || positions.Any(position => BendProtocolPositionPolicy.ExcludesHolding(position.Code));
        var hasValidBcc = positions.Any(position => position.IstGueltigerBccUntercode);
        var isBccScan = string.Equals(codePrefix, "BCC", StringComparison.Ordinal);
        var selectionEligible = !isBccScan || (hasValidBcc && !malformedBcc);
        var exclusionReason = malformedBcc
            ? "Rohcode BCC.YB: ungueltiger Punkt; die ganze Haltung wird nicht gemessen."
            : isBccScan && positions.Count > 0 && !hasValidBcc
                ? "Keiner der acht gueltigen BCC-Untercodes vorhanden."
                : null;

        return new HoldingScanResult(
            holding,
            codes.ToArray(),
            positions.Count,
            photos,
            pdfs.Length,
            pdfsOk,
            pdfsError,
            pdfsReadable,
            pdfReadErrors,
            videos,
            selectionEligible,
            exclusionReason,
            positions);
    }

    private static CodeSummary BuildCodeSummary(
        string code,
        IReadOnlyCollection<HoldingScanResult> allHoldings,
        IReadOnlyCollection<HoldingScanResult> selectedHoldings)
    {
        var before = allHoldings
            .Where(result => result.Positionen.Any(position => string.Equals(position.Code, code, StringComparison.Ordinal)))
            .ToArray();
        var selected = selectedHoldings
            .Where(result => result.Positionen.Any(position => string.Equals(position.Code, code, StringComparison.Ordinal)))
            .ToArray();

        return new CodeSummary(
            code,
            before.Length,
            before.Sum(result => result.Positionen.Count(position => string.Equals(position.Code, code, StringComparison.Ordinal))),
            selected.Length,
            selected.Sum(result => result.Positionen.Count(position => string.Equals(position.Code, code, StringComparison.Ordinal))));
    }

    private static VideoCounterResolution ResolveVideoCounter(
        VsaFinding finding,
        string code,
        IReadOnlyList<GroundTruthEntry> extracted,
        ISet<int> usedIndices)
    {
        var direct = ProtocolTimeParser.ParseMpegTime(finding.MPEG);
        if (direct is not null)
            return new VideoCounterResolution(direct, finding.MPEG?.Trim(), "vsa_finding_mpeg");

        if (finding.Timestamp is not null)
        {
            var value = finding.Timestamp.Value.TimeOfDay;
            return new VideoCounterResolution(value, value.ToString("c", CultureInfo.InvariantCulture), "vsa_finding_timestamp");
        }

        var rawTime = ProtocolFindingRawParser.TryParseTimeFromRaw(finding.Raw ?? string.Empty);
        var parsedRaw = ProtocolTimeParser.ParseMpegTime(rawTime);
        if (parsedRaw is not null)
            return new VideoCounterResolution(parsedRaw, rawTime, "vsa_finding_raw");

        var candidates = Enumerable.Range(0, extracted.Count)
            .Where(index => !usedIndices.Contains(index))
            .Where(index => CodesAreEquivalent(extracted[index].VsaCode, code))
            .Where(index => finding.MeterStart is null
                            || Math.Abs(extracted[index].MeterStart - finding.MeterStart.Value) <= 0.011)
            .ToArray();

        if (candidates.Length == 0)
            return new VideoCounterResolution(null, null, "kein_eindeutiger_protokolltreffer");

        var distinctPositions = candidates
            .Select(index => new
            {
                MeterStart = Math.Round(extracted[index].MeterStart, 3),
                MeterEnd = Math.Round(extracted[index].MeterEnd, 3),
                TimeTicks = extracted[index].Zeit?.Ticks
            })
            .Distinct()
            .ToArray();
        if (distinctPositions.Length > 1)
            return new VideoCounterResolution(null, null, "mehrdeutiger_protokolltreffer");

        var selectedIndex = candidates[0];
        usedIndices.Add(selectedIndex);
        var timestamp = extracted[selectedIndex].Zeit;
        return timestamp is null
            ? new VideoCounterResolution(null, null, "protokolltreffer_ohne_videozaehler")
            : new VideoCounterResolution(
                timestamp,
                timestamp.Value.ToString("c", CultureInfo.InvariantCulture),
                "protokoll_code_meter");
    }

    private static string NormalizeCode(string? code)
        => (code ?? string.Empty).Trim().ToUpperInvariant();

    private static bool CodesAreEquivalent(string? left, string? right)
        => string.Equals(
            NormalizeCode(left).Replace(".", string.Empty, StringComparison.Ordinal),
            NormalizeCode(right).Replace(".", string.Empty, StringComparison.Ordinal),
            StringComparison.Ordinal);

    private static void EnsureOutputIsOutsideCustomerRoot(string rootPath, string? outPath)
    {
        if (string.IsNullOrWhiteSpace(outPath))
            return;

        var fullOutput = Path.GetFullPath(outPath);
        var relative = Path.GetRelativePath(rootPath, fullOutput);
        if (relative == "."
            || (!Path.IsPathRooted(relative)
                && relative != ".."
                && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                "Der Bericht darf nicht in die gescannte Kundenwurzel geschrieben werden.");
        }
    }
}
