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

    private static HaltungRecord? FindByHaltungsname(Project project, string key)
        => project.Data.FirstOrDefault(r => string.Equals(r.GetFieldValue("Haltungsname")?.Trim(), key.Trim(), StringComparison.Ordinal));

    private static HaltungRecord? FindCorruptPlaceholderRecord(Project project, HaltungRecord source)
    {
        // Primary strategy: stable fingerprint match even if Datum_Jahr is missing.
        var sourceFingerprint = BuildRepairFingerprint(source);
        if (!string.IsNullOrWhiteSpace(sourceFingerprint))
        {
            var fpCandidates = project.Data.Where(r =>
            {
                var key = (r.GetFieldValue("Haltungsname") ?? "").Trim();
                if (IsLikelyHoldingId(key) || !IsKnownPlaceholderKey(key))
                    return false;

                var candidateFingerprint = BuildRepairFingerprint(r);
                return !string.IsNullOrWhiteSpace(candidateFingerprint)
                       && string.Equals(candidateFingerprint, sourceFingerprint, StringComparison.Ordinal);
            }).Take(2).ToList();

            if (fpCandidates.Count == 1)
                return fpCandidates[0];
        }

        // Fallback strategy for weaker datasets.
        var srcDamages = NormalizeForFingerprint(source.GetFieldValue("Primaere_Schaeden"));
        if (string.IsNullOrWhiteSpace(srcDamages))
            return null;

        var srcDate = NormalizeForFingerprint(source.GetFieldValue("Datum_Jahr"));
        var srcDir = NormalizeForFingerprint(source.GetFieldValue("Inspektionsrichtung"));
        var srcUse = NormalizeForFingerprint(source.GetFieldValue("Nutzungsart"));

        var candidates = project.Data.Where(r =>
        {
            var key = (r.GetFieldValue("Haltungsname") ?? "").Trim();
            if (IsLikelyHoldingId(key))
                return false;
            if (!IsKnownPlaceholderKey(key))
                return false;

            var damages = NormalizeForFingerprint(r.GetFieldValue("Primaere_Schaeden"));
            if (!string.Equals(damages, srcDamages, StringComparison.Ordinal))
                return false;

            var date = NormalizeForFingerprint(r.GetFieldValue("Datum_Jahr"));
            if (!string.IsNullOrWhiteSpace(srcDate) && !string.Equals(date, srcDate, StringComparison.Ordinal))
                return false;

            var dir = NormalizeForFingerprint(r.GetFieldValue("Inspektionsrichtung"));
            var use = NormalizeForFingerprint(r.GetFieldValue("Nutzungsart"));
            if (!string.IsNullOrWhiteSpace(srcDir) && !string.Equals(dir, srcDir, StringComparison.Ordinal))
                return false;
            if (!string.IsNullOrWhiteSpace(srcUse) && !string.Equals(use, srcUse, StringComparison.Ordinal))
                return false;

            return true;
        }).Take(2).ToList();

        return candidates.Count == 1 ? candidates[0] : null;
    }

    private static bool ShouldSkipUnknownChunk(Dictionary<string, string> fields, PdfChunk chunk)
    {
        // Ignore table/header/meta chunks with no usable inspection payload.
        bool hasUsefulPayload =
            !string.IsNullOrWhiteSpace(fields.GetValueOrDefault("Primaere_Schaeden")) ||
            !string.IsNullOrWhiteSpace(fields.GetValueOrDefault("Inspektionsrichtung")) ||
            !string.IsNullOrWhiteSpace(fields.GetValueOrDefault("Nutzungsart")) ||
            !string.IsNullOrWhiteSpace(fields.GetValueOrDefault("DN_mm")) ||
            !string.IsNullOrWhiteSpace(fields.GetValueOrDefault("Haltungslaenge_m"));

        if (hasUsefulPayload)
            return false;

        var text = chunk.Text ?? "";
        if (Regex.IsMatch(text, @"(?im)^\s*\d[\d\.]*\s*[-/]\s*\d[\d\.]*\s+\d{2}\.\d{2}\.\d{4}\b"))
            return false;

        return true;
    }

    private static bool IsKnownPlaceholderKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return true;

        if (key.StartsWith("UNBEKANNT_", StringComparison.OrdinalIgnoreCase))
            return true;

        if (IsHeaderPlaceholderKey(key))
            return true;

        return key.Equals("Datum :", StringComparison.OrdinalIgnoreCase)
               || key.Equals("Datum", StringComparison.OrdinalIgnoreCase)
               || key.Equals("Haltungsname :", StringComparison.OrdinalIgnoreCase)
               || key.Equals("Haltungsname", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsHeaderPlaceholderKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return false;

        if (!Regex.IsMatch(key, @"(?i)^\s*(?:Haltungsname\s*:)?\s*Datum\s*:"))
            return false;

        return Regex.IsMatch(key, @"(?i)\bWetter\s*:") ||
               Regex.IsMatch(key, @"(?i)\bOperator\s*:") ||
               Regex.IsMatch(key, @"(?i)\bAuftrag\s*Nr\.?\s*:");
    }

    private static int CleanupCorruptPlaceholderRecords(Project project, ImportStats stats)
    {
        var placeholders = project.Data
            .Where(r =>
            {
                var key = (r.GetFieldValue("Haltungsname") ?? "").Trim();
                return !IsLikelyHoldingId(key) && IsKnownPlaceholderKey(key);
            })
            .ToList();

        if (placeholders.Count == 0)
            return 0;

        var validByFingerprint = project.Data
            .Where(r =>
            {
                var key = (r.GetFieldValue("Haltungsname") ?? "").Trim();
                return IsLikelyHoldingId(key);
            })
            .Select(r => new { Record = r, Fingerprint = BuildRepairFingerprint(r) })
            .Where(x => !string.IsNullOrWhiteSpace(x.Fingerprint))
            .GroupBy(x => x.Fingerprint!, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Record).ToList(), StringComparer.Ordinal);

        var toRemove = new List<HaltungRecord>();
        foreach (var ph in placeholders)
        {
            var fp = BuildRepairFingerprint(ph);
            if (string.IsNullOrWhiteSpace(fp))
                continue;

            if (!validByFingerprint.TryGetValue(fp, out var matches))
                continue;

            if (matches.Count == 1)
                toRemove.Add(ph);
        }

        if (toRemove.Count == 0)
            return 0;

        foreach (var row in toRemove)
            project.Data.Remove(row);

        project.ModifiedAtUtc = DateTime.UtcNow;
        project.Dirty = true;
        stats.Messages.Add(new ImportMessage
        {
            Level = "Info",
            Context = "PDF",
            Message = $"Bereinigt: {toRemove.Count} fehlerhafte Placeholder-Zeilen (z.B. 'Datum :')."
        });
        return toRemove.Count;
    }

    private static int CleanupOrphanPlaceholderRecords(Project project, ImportStats stats)
    {
        var toRemove = project.Data
            .Where(r =>
            {
                var key = (r.GetFieldValue("Haltungsname") ?? "").Trim();
                if (IsLikelyHoldingId(key))
                    return false;

                if (IsHeaderPlaceholderKey(key))
                    return true;

                if (key.StartsWith("UNBEKANNT_", StringComparison.OrdinalIgnoreCase))
                    return !HasMeaningfulInspectionPayload(r);

                return false;
            })
            .ToList();

        if (toRemove.Count == 0)
            return 0;

        foreach (var row in toRemove)
            project.Data.Remove(row);

        project.ModifiedAtUtc = DateTime.UtcNow;
        project.Dirty = true;
        stats.Messages.Add(new ImportMessage
        {
            Level = "Info",
            Context = "PDF",
            Message = $"Bereinigt (erweitert): {toRemove.Count} verwaiste Header/Placeholder-Zeilen."
        });
        return toRemove.Count;
    }

    private static bool HasMeaningfulInspectionPayload(HaltungRecord r)
    {
        return HasMeaningfulText(r.GetFieldValue("Primaere_Schaeden"))
               || HasMeaningfulText(r.GetFieldValue("Inspektionsrichtung"))
               || HasMeaningfulText(r.GetFieldValue("Nutzungsart"))
               || HasMeaningfulText(r.GetFieldValue("DN_mm"))
               || HasMeaningfulText(r.GetFieldValue("Haltungslaenge_m"))
               || HasMeaningfulText(r.GetFieldValue("Rohrmaterial"))
               || HasMeaningfulText(r.GetFieldValue("Datum_Jahr"))
               || HasMeaningfulText(r.GetFieldValue("Link"));
    }

    private static bool HasMeaningfulText(string? value)
    {
        var v = NormalizeForFingerprint(value);
        if (string.IsNullOrWhiteSpace(v))
            return false;

        return Regex.IsMatch(v, @"[\p{L}\p{N}]");
    }

    private static string? BuildRepairFingerprint(HaltungRecord r)
    {
        var damages = NormalizeForFingerprint(r.GetFieldValue("Primaere_Schaeden"));
        if (string.IsNullOrWhiteSpace(damages))
            return null;

        var dir = NormalizeForFingerprint(r.GetFieldValue("Inspektionsrichtung"));
        var use = NormalizeForFingerprint(r.GetFieldValue("Nutzungsart"));
        var dn = NormalizeForFingerprint(r.GetFieldValue("DN_mm"));
        var len = NormalizeForFingerprint(r.GetFieldValue("Haltungslaenge_m"));
        var mat = NormalizeForFingerprint(r.GetFieldValue("Rohrmaterial"));

        return $"{damages}|{dir}|{use}|{dn}|{len}|{mat}";
    }

    private static string NormalizeForFingerprint(string? value)
    {
        var v = (value ?? "").Trim();
        if (v.Length == 0)
            return "";

        v = Regex.Replace(v, @"\s+", " ");
        return v;
    }

    private static string AppendLine(string baseText, string line)
    {
        baseText ??= "";
        if (string.IsNullOrWhiteSpace(baseText)) return line;
        return baseText.TrimEnd() + "\n" + line;
    }
}
