using System.Text.Json.Nodes;
using System.Globalization;
using System.Text.RegularExpressions;
using ImportRunContext = AuswertungPro.Next.Application.Import.ImportRunContext;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Infrastructure.Import.Common;
using AuswertungPro.Next.Infrastructure.Vsa;
using AuswertungPro.Next.Infrastructure;
using UglyToad.PdfPig;



namespace AuswertungPro.Next.Infrastructure.Import.Pdf;

public sealed partial class LegacyPdfImportService
{
    private static readonly string[] SchachtComponentOrder =
    {
        "Schachtdeckel",
        "Deckelrahmen",
        "Schachthals",
        "Konus",
        "Schachtrohr",
        "Bankett",
        "Durchlaufrinne",
        "Anschluss",
        "Leiter/Steigeisen",
        "Tauchbogen"
    };

    private readonly PdfParser _parser = new();

    public ImportStats ImportPdf(string pdfPath, Project project, string? explicitPdfToTextPath = null, bool fillMissingOnly = false, ImportRunContext? ctx = null)
    {
        var stats = new ImportStats();

        try
        {
            var extraction = PdfTextExtractor.ExtractPages(pdfPath, explicitPdfToTextPath);
            var usedOcrFallback = false;
            var effectivePages = extraction.Pages
                .Select(page => (page ?? string.Empty).Replace("\r\n", "\n").Trim())
                .Where(page => !string.IsNullOrWhiteSpace(page))
                .ToList();
            var fullText = string.Join("\n\n", effectivePages);
            if (string.IsNullOrWhiteSpace(fullText))
            {
                var ocrFallback = TryExtractAllPagesWithOcr(pdfPath);
                if (ocrFallback.Pages.Count == 0)
                {
                    stats.Errors++;
                    var ocrHint = string.IsNullOrWhiteSpace(ocrFallback.Message)
                        ? string.Empty
                        : $" OCR-Fallback: {ocrFallback.Message}";
                    stats.Messages.Add(new ImportMessage
                    {
                        Level = "Error",
                        Context = "PDF",
                        Message = $"Kein extrahierbarer Text in PDF: {Path.GetFileName(pdfPath)} (ggf. Scan ohne OCR).{ocrHint}"
                    });
                    return stats;
                }

                effectivePages = ocrFallback.Pages.ToList();
                fullText = string.Join("\n\n", effectivePages);
                usedOcrFallback = true;
                var ocrDetail = string.IsNullOrWhiteSpace(ocrFallback.Message)
                    ? string.Empty
                    : $" Hinweis: {ocrFallback.Message}";
                stats.Messages.Add(new ImportMessage
                {
                    Level = "Info",
                    Context = "PDF",
                    Message = $"OCR-Fallback aktiviert: {Path.GetFileName(pdfPath)} | OCR-Seiten={ocrFallback.Pages.Count}.{ocrDetail}"
                });
            }

            var effectiveExtraction = new PdfTextExtraction(effectivePages, fullText);

            if (LooksLikeSchachtProtokoll(fullText))
            {
                ImportSchachtPdf(pdfPath, fullText, project, stats);
                return stats;
            }

            ApplyProjectMetadata(effectiveExtraction, project, stats);
            var chunks = PdfChunking.SplitIntoHaltungChunks(effectiveExtraction.Pages, _parser);
            if (chunks.Count == 0 && !usedOcrFallback)
            {
                var ocrFallback = TryExtractAllPagesWithOcr(pdfPath);
                if (ocrFallback.Pages.Count > 0)
                {
                    fullText = string.Join("\n\n", ocrFallback.Pages);
                    effectiveExtraction = new PdfTextExtraction(ocrFallback.Pages, fullText);
                    chunks = PdfChunking.SplitIntoHaltungChunks(effectiveExtraction.Pages, _parser);

                    var ocrDetail = string.IsNullOrWhiteSpace(ocrFallback.Message)
                        ? string.Empty
                        : $" Hinweis: {ocrFallback.Message}";
                    stats.Messages.Add(new ImportMessage
                    {
                        Level = "Info",
                        Context = "PDF",
                        Message = $"OCR-Fallback fuer Haltungszuordnung aktiviert: {Path.GetFileName(pdfPath)} | OCR-Seiten={ocrFallback.Pages.Count}.{ocrDetail}"
                    });
                }
            }

            stats.Found = chunks.Count;
            stats.Uncertain = chunks.Count(c => c.IsUncertain);
            var importedChunks = 0;

            foreach (var chunk in chunks)
            {
                try
                {
                    var fields = _parser.ParseFields(chunk.Text ?? string.Empty);
                    var parsedPage = HoldingFolderDistributor.ParsePdfPage(chunk.Text ?? "", pdfPath);
                    if (parsedPage.Success)
                    {
                        if (parsedPage.Date is not null && string.IsNullOrWhiteSpace(fields.GetValueOrDefault("Datum_Jahr")))
                            fields["Datum_Jahr"] = parsedPage.Date.Value.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);

                        if (IsLikelyHoldingId(parsedPage.Haltung)
                            && (!fields.TryGetValue("Haltungsname", out var currentKey) || !IsLikelyHoldingId(currentKey)))
                        {
                            fields["Haltungsname"] = NormalizeHoldingId(parsedPage.Haltung!);
                        }
                    }

                    var key = TryResolveHoldingKey(fields, chunk, pdfPath);

                    if (string.IsNullOrWhiteSpace(key))
                    {
                        if (ShouldSkipUnknownChunk(fields, chunk))
                        {
                            stats.Messages.Add(new ImportMessage
                            {
                                Level = "Info",
                                Context = "PDF",
                                Message = $"Chunk {chunk.Index} (Seiten {chunk.PageRange}) ohne Haltungsdaten uebersprungen."
                            });
                            continue;
                        }

                        key = $"UNBEKANNT_{DateTime.Now:yyyyMMdd_HHmmss}_{chunk.Index}";
                        stats.Uncertain++;
                        fields["Bemerkungen"] = AppendLine(fields.TryGetValue("Bemerkungen", out var b) ? b : "",
                            $"[PDF Import] Keine ID erkannt. Seiten: {chunk.PageRange}");
                    }

                    key = key.Split('\n')[0].Trim();

                    var source = new HaltungRecord();
                    // Nur geparste Felder setzen (Rest leer lassen)
                    foreach (var kv in fields)
                        source.SetFieldValue(kv.Key, kv.Value, FieldSource.Pdf, userEdited: false);

                    // Stelle sicher, dass Haltungsname gesetzt ist
                    source.SetFieldValue("Haltungsname", key, FieldSource.Pdf, userEdited: false);

                    // Falls Anschlussmenge nicht direkt im PDF steht, aus Schadenscodierung ableiten.
                    if (string.IsNullOrWhiteSpace(source.GetFieldValue("Anschluesse_verpressen")))
                    {
                        var estimatedConnections = ConnectionCountEstimator.EstimateFromRecord(source) ?? 0;
                        source.SetFieldValue(
                            "Anschluesse_verpressen",
                            estimatedConnections.ToString(CultureInfo.InvariantCulture),
                            FieldSource.Pdf,
                            userEdited: false);
                    }

                    var target = FindByHaltungsname(project, key);
                    if (target is null)
                        target = FindCorruptPlaceholderRecord(project, source);
                    bool created = false;
                    if (target is null)
                    {
                        target = new HaltungRecord();
                        target.SetFieldValue("Haltungsname", key, FieldSource.Pdf, userEdited: false);
                        project.Data.Add(target);
                        created = true;
                        stats.CreatedRecords++;
                    }
                    else
                    {
                        // Repair old placeholder key (e.g. "Datum : ...") once we have a valid holding ID.
                        var existingKey = (target.GetFieldValue("Haltungsname") ?? "").Trim();
                        if (!IsLikelyHoldingId(existingKey) && IsLikelyHoldingId(key))
                            target.SetFieldValue("Haltungsname", key, FieldSource.Pdf, userEdited: false);
                    }

                    var mergeStats = MergeEngine.MergeRecord(target, source, FieldSource.Pdf, fillMissingOnly, ctx);

                    // Original-PDF verknuepfen
                    var existingPdfPath = target.GetFieldValue("PDF_Path")?.Trim();
                    if (string.IsNullOrWhiteSpace(existingPdfPath))
                    {
                        target.SetFieldValue("PDF_Path", pdfPath, FieldSource.Pdf, userEdited: false);
                    }
                    else if (!string.Equals(existingPdfPath, pdfPath, StringComparison.OrdinalIgnoreCase))
                    {
                        var allRaw = target.GetFieldValue("PDF_All")?.Trim();
                        var allSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        if (!string.IsNullOrWhiteSpace(allRaw))
                            foreach (var p in allRaw.Split(';', StringSplitOptions.RemoveEmptyEntries))
                                allSet.Add(p.Trim());
                        allSet.Add(existingPdfPath);
                        allSet.Add(pdfPath);
                        target.SetFieldValue("PDF_All", string.Join(";", allSet), FieldSource.Pdf, userEdited: false);
                    }

                    if (mergeStats.Updated > 0)
                    {
                        stats.UpdatedRecords += created ? 0 : 1;
                        stats.UpdatedFields += mergeStats.Updated;
                    }

                    stats.Conflicts += mergeStats.Conflicts;
                    stats.Errors += mergeStats.Errors;

                    foreach (var c in mergeStats.ConflictDetails)
                        stats.ConflictDetails.Add(c);

                    // Import history entry pro Chunk (kompakt)
                    var hist = new JsonObject
                    {
                        ["type"] = "pdf",
                        ["file"] = Path.GetFileName(pdfPath),
                        ["pages"] = chunk.PageRange,
                        ["detectedId"] = chunk.DetectedId ?? "",
                        ["usedKey"] = key,
                        ["timestampUtc"] = DateTime.UtcNow.ToString("o"),
                        ["uncertain"] = chunk.IsUncertain
                    };
                    project.ImportHistory.Add(hist);

                    // Konflikte auch ins Projekt schreiben
                    foreach (var c in mergeStats.ConflictDetails)
                        project.Conflicts.Add(c);

                    importedChunks++;
                }
                catch (Exception exChunk)
                {
                    stats.Errors++;
                    stats.Messages.Add(new ImportMessage
                    {
                        Level = "Error",
                        Context = "PDF",
                        Message = $"Chunk {chunk.Index} (Seiten {chunk.PageRange}): {exChunk.Message}"
                    });
                }
            }

            if (importedChunks == 0)
                TryImportFallbackHoldingFromWholeText(fullText, pdfPath, project, stats, fillMissingOnly, ctx);

            project.ModifiedAtUtc = DateTime.UtcNow;
            project.Dirty = true;

            CleanupCorruptPlaceholderRecords(project, stats);

            stats.Messages.Add(new ImportMessage
            {
                Level = "Info",
                Context = "PDF",
                Message = $"PDF importiert: {Path.GetFileName(pdfPath)} | Chunks={stats.Found}, Neu={stats.CreatedRecords}, Updates={stats.UpdatedFields}, Konflikte={stats.Conflicts}, Unklar={stats.Uncertain}"
            });
        }
        catch (Exception ex)
        {
            stats.Errors++;
            stats.Messages.Add(new ImportMessage { Level = "Error", Context = "PDF", Message = ex.Message });
        }

        return stats;
    }

    public ImportStats CleanupCorruptHoldingNames(Project project)
    {
        var stats = new ImportStats();
        if (project is null)
        {
            stats.Errors++;
            stats.Messages.Add(new ImportMessage
            {
                Level = "Error",
                Context = "PDF",
                Message = "Projekt ist null."
            });
            return stats;
        }

        var removedCorrupt = CleanupCorruptPlaceholderRecords(project, stats);
        var removedOrphan = CleanupOrphanPlaceholderRecords(project, stats);
        stats.UpdatedRecords += removedCorrupt + removedOrphan;
        return stats;
    }

    private static void TryImportFallbackHoldingFromWholeText(string fullText, string pdfPath, Project project, ImportStats stats, bool fillMissingOnly = false, ImportRunContext? ctx = null)
    {
        var parsed = HoldingFolderDistributor.ParsePdfPage(fullText, pdfPath);
        var fallbackHolding = IsLikelyHoldingId(parsed.Haltung)
            ? NormalizeHoldingId(parsed.Haltung!)
            : TryExtractHoldingIdFromFileName(pdfPath);
        var fallbackDate = parsed.Date ?? TryExtractDateFromFileName(pdfPath);

        if (!IsLikelyHoldingId(fallbackHolding))
        {
            stats.Messages.Add(new ImportMessage
            {
                Level = "Warn",
                Context = "PDF",
                Message = $"Keine verwertbare Haltung aus PDF ableitbar: {Path.GetFileName(pdfPath)}"
            });
            return;
        }

        var key = NormalizeHoldingId(fallbackHolding!);
        var source = new HaltungRecord();
        source.SetFieldValue("Haltungsname", key, FieldSource.Pdf, userEdited: false);
        if (fallbackDate is not null)
            source.SetFieldValue("Datum_Jahr", fallbackDate.Value.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture), FieldSource.Pdf, userEdited: false);
        source.SetFieldValue("Anschluesse_verpressen", "0", FieldSource.Pdf, userEdited: false);

        var target = FindByHaltungsname(project, key);
        var created = false;
        if (target is null)
        {
            target = new HaltungRecord();
            target.SetFieldValue("Haltungsname", key, FieldSource.Pdf, userEdited: false);
            project.Data.Add(target);
            stats.CreatedRecords++;
            created = true;
        }

        var mergeStats = MergeEngine.MergeRecord(target, source, FieldSource.Pdf, fillMissingOnly, ctx);

        // Original-PDF verknuepfen
        var existingPdfPath = target.GetFieldValue("PDF_Path")?.Trim();
        if (string.IsNullOrWhiteSpace(existingPdfPath))
        {
            target.SetFieldValue("PDF_Path", pdfPath, FieldSource.Pdf, userEdited: false);
        }
        else if (!string.Equals(existingPdfPath, pdfPath, StringComparison.OrdinalIgnoreCase))
        {
            var allRaw = target.GetFieldValue("PDF_All")?.Trim();
            var allSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(allRaw))
                foreach (var p in allRaw.Split(';', StringSplitOptions.RemoveEmptyEntries))
                    allSet.Add(p.Trim());
            allSet.Add(existingPdfPath);
            allSet.Add(pdfPath);
            target.SetFieldValue("PDF_All", string.Join(";", allSet), FieldSource.Pdf, userEdited: false);
        }

        stats.UpdatedFields += mergeStats.Updated;
        if (!created && mergeStats.Updated > 0)
            stats.UpdatedRecords++;
        stats.Conflicts += mergeStats.Conflicts;
        stats.Errors += mergeStats.Errors;
        foreach (var c in mergeStats.ConflictDetails)
        {
            stats.ConflictDetails.Add(c);
            project.Conflicts.Add(c);
        }

        stats.Messages.Add(new ImportMessage
        {
            Level = "Info",
            Context = "PDF",
            Message = $"Fallback-Import angewendet: {Path.GetFileName(pdfPath)} -> {key}"
        });
        stats.Uncertain++;
    }

    private static string? TryExtractHoldingIdFromFileName(string pdfPath)
    {
        var name = Path.GetFileNameWithoutExtension(pdfPath) ?? "";
        // e.g. 32953_1225 -> 32953-1225
        var underscorePair = Regex.Match(name, @"(?<!\d)(\d{3,})_(\d{3,})(?!\d)");
        if (underscorePair.Success)
            return $"{underscorePair.Groups[1].Value}-{underscorePair.Groups[2].Value}";

        var dashPair = Regex.Match(name, @"(?<!\d)(\d[\d\.]*-\d[\d\.]*)(?!\d)");
        if (dashPair.Success)
            return NormalizeHoldingId(dashPair.Groups[1].Value);

        return null;
    }

    private static DateTime? TryExtractDateFromFileName(string pdfPath)
    {
        var name = Path.GetFileNameWithoutExtension(pdfPath) ?? "";
        var ymd = Regex.Match(name, @"(?<!\d)(\d{4})(\d{2})(\d{2})(?!\d)");
        if (ymd.Success && DateTime.TryParseExact(ymd.Value, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateYmd))
            return dateYmd;

        var dmy = Regex.Match(name, @"(?<!\d)(\d{2})[._-](\d{2})[._-](\d{2,4})(?!\d)");
        if (dmy.Success)
        {
            var candidate = $"{dmy.Groups[1].Value}.{dmy.Groups[2].Value}.{dmy.Groups[3].Value}";
            var formats = new[] { "dd.MM.yyyy", "dd.MM.yy" };
            if (DateTime.TryParseExact(candidate, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateDmy))
                return dateDmy;
        }

        return null;
    }

    private sealed record OcrImportFallback(IReadOnlyList<string> Pages, string? Message);

    private static OcrImportFallback TryExtractAllPagesWithOcr(string pdfPath)
    {
        var pageCount = TryGetPdfPageCount(pdfPath);
        if (pageCount <= 0)
            return new OcrImportFallback(Array.Empty<string>(), "Keine Seiten fuer OCR erkannt.");

        var pages = new List<string>();
        string? firstError = null;

        for (var pageNumber = 1; pageNumber <= pageCount; pageNumber++)
        {
            var ocr = PdfOcrExtractor.TryExtractPageText(pdfPath, pageNumber);
            if (ocr.Success && !string.IsNullOrWhiteSpace(ocr.Text))
            {
                pages.Add(ocr.Text.Replace("\r\n", "\n").Trim());
                continue;
            }

            if (string.IsNullOrWhiteSpace(firstError) && !string.IsNullOrWhiteSpace(ocr.Message))
                firstError = ocr.Message;
        }

        if (pages.Count == 0)
            return new OcrImportFallback(Array.Empty<string>(), firstError ?? "OCR lieferte keinen verwertbaren Text.");

        return new OcrImportFallback(pages, firstError);
    }

    private static int TryGetPdfPageCount(string pdfPath)
    {
        try
        {
            using var document = PdfDocument.Open(pdfPath);
            return document.NumberOfPages;
        }
        catch
        {
            return 0;
        }
    }

    private static string? TryResolveHoldingKey(
        Dictionary<string, string> fields,
        PdfChunk chunk,
        string pdfPath)
    {
        if (fields.TryGetValue("Haltungsname", out var fromField) && IsLikelyHoldingId(fromField))
            return NormalizeHoldingId(fromField);

        if (IsLikelyHoldingId(chunk.DetectedId))
            return NormalizeHoldingId(chunk.DetectedId!);

        var rowMatch = Regex.Match(
            chunk.Text ?? "",
            @"(?im)^\s*(?<id>\d[\d\.]*\s*[-/]\s*\d[\d\.]*)\s+\d{2}\.\d{2}\.\d{4}\b");
        if (rowMatch.Success)
            return NormalizeHoldingId(rowMatch.Groups["id"].Value);

        var parsed = HoldingFolderDistributor.ParsePdfPage(chunk.Text ?? "", pdfPath);
        if (parsed.Success && parsed.Date is not null && IsLikelyHoldingId(parsed.Haltung))
            return NormalizeHoldingId(parsed.Haltung!);

        return null;
    }

    private static bool IsLikelyHoldingId(string? value)
        => !string.IsNullOrWhiteSpace(value)
           && Regex.IsMatch(value!, @"^\s*\d[\d\.]*\s*[-/]\s*\d[\d\.]*\s*$");

    private static string NormalizeHoldingId(string value)
    {
        var normalized = value.Trim();
        normalized = Regex.Replace(normalized, @"\s+", "");
        normalized = normalized.Replace('/', '-');
        return normalized;
    }


    private static void ApplyProjectMetadata(PdfTextExtraction extraction, Project project, ImportStats stats)
    {
        project.EnsureMetadataDefaults();

        var parsed = PdfProjectMetadataParser.Parse(extraction);
        var applied = 0;

        foreach (var kv in parsed.Values)
        {
            if (string.IsNullOrWhiteSpace(kv.Value))
                continue;

            if (!project.Metadata.TryGetValue(kv.Key, out var existing) || string.IsNullOrWhiteSpace(existing))
            {
                project.Metadata[kv.Key] = kv.Value.Trim();
                applied++;
            }
        }

        if (!string.IsNullOrWhiteSpace(parsed.ProjectName)
            && (string.IsNullOrWhiteSpace(project.Name) || string.Equals(project.Name, "Neues Projekt", StringComparison.Ordinal)))
        {
            project.Name = parsed.ProjectName!.Trim();
            applied++;
        }

        if (applied > 0)
        {
            stats.Messages.Add(new ImportMessage
            {
                Level = "Info",
                Context = "PDF",
                Message = $"Projektdaten importiert: {applied} Felder"
            });
        }
    }

}
