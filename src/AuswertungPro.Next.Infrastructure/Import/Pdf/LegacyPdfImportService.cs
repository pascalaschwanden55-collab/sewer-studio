using System.Text.Json.Nodes;
using System.Globalization;
using System.Text.RegularExpressions;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import.Common;
using AuswertungPro.Next.Infrastructure.Vsa;
using AuswertungPro.Next.Infrastructure;
using UglyToad.PdfPig;



namespace AuswertungPro.Next.Infrastructure.Import.Pdf;

public sealed class LegacyPdfImportService
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

    public ImportStats ImportPdf(string pdfPath, Project project, string? explicitPdfToTextPath = null)
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
                        var estimatedConnections = ConnectionCountEstimator.EstimateFromRecord(source);
                        if (estimatedConnections is > 0)
                        {
                            source.SetFieldValue(
                                "Anschluesse_verpressen",
                                estimatedConnections.Value.ToString(CultureInfo.InvariantCulture),
                                FieldSource.Pdf,
                                userEdited: false);
                        }
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

                    var mergeStats = MergeEngine.MergeRecord(target, source, FieldSource.Pdf);
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
                TryImportFallbackHoldingFromWholeText(fullText, pdfPath, project, stats);

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

    private static void TryImportFallbackHoldingFromWholeText(string fullText, string pdfPath, Project project, ImportStats stats)
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

        var mergeStats = MergeEngine.MergeRecord(target, source, FieldSource.Pdf);
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

    private static bool LooksLikeSchachtProtokoll(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        return text.Contains("Schachtprotokoll", StringComparison.OrdinalIgnoreCase);
    }

    private static void ImportSchachtPdf(string pdfPath, string fullText, Project project, ImportStats stats)
    {
        var parsed = ParseSchachtFields(fullText);
        stats.Found = 1;

        if (string.IsNullOrWhiteSpace(parsed.SchachtNummer))
        {
            stats.Errors++;
            stats.Messages.Add(new ImportMessage
            {
                Level = "Error",
                Context = "PDF-SCHACHT",
                Message = $"Schachtnummer nicht gefunden: {Path.GetFileName(pdfPath)}"
            });
            return;
        }

        var key = parsed.SchachtNummer.Trim();
        var target = project.SchaechteData.FirstOrDefault(r =>
            string.Equals((r.GetFieldValue("Schachtnummer") ?? "").Trim(), key, StringComparison.OrdinalIgnoreCase) ||
            string.Equals((r.GetFieldValue("Nr.") ?? "").Trim(), key, StringComparison.OrdinalIgnoreCase) ||
            string.Equals((r.GetFieldValue("NR.") ?? "").Trim(), key, StringComparison.OrdinalIgnoreCase));

        var created = false;
        if (target is null)
        {
            target = new SchachtRecord();
            project.SchaechteData.Add(target);
            stats.CreatedRecords++;
            created = true;
        }

        SetSchachtField(target, "Schachtnummer", key);
        SetSchachtField(target, "NR.", key);
        SetSchachtField(target, "Nr.", key);

        if (!string.IsNullOrWhiteSpace(parsed.Datum))
            SetSchachtField(target, "Ausfuehrung Datum/Jahr", parsed.Datum);

        if (!string.IsNullOrWhiteSpace(parsed.Funktion))
            SetSchachtField(target, "Funktion", parsed.Funktion);

        if (!string.IsNullOrWhiteSpace(parsed.PrimaereSchaeden))
            SetSchachtField(target, "Primaere Schaeden", parsed.PrimaereSchaeden);

        if (!string.IsNullOrWhiteSpace(parsed.Bemerkungen))
            SetSchachtField(target, "Bemerkungen", parsed.Bemerkungen);

        if (!string.IsNullOrWhiteSpace(parsed.Link))
            SetSchachtField(target, "Link", parsed.Link);

        if (!string.IsNullOrWhiteSpace(parsed.Status))
            SetSchachtField(target, "Status offen/abgeschlossen", parsed.Status);

        project.ModifiedAtUtc = DateTime.UtcNow;
        project.Dirty = true;

        if (!created)
            stats.UpdatedRecords++;

        var imported = new List<string>();
        if (!string.IsNullOrWhiteSpace(parsed.SchachtNummer)) imported.Add("Schachtnummer");
        if (!string.IsNullOrWhiteSpace(parsed.Datum)) imported.Add("Ausfuehrung Datum/Jahr");
        if (!string.IsNullOrWhiteSpace(parsed.Funktion)) imported.Add("Funktion");
        if (!string.IsNullOrWhiteSpace(parsed.PrimaereSchaeden)) imported.Add("Primaere Schaeden");
        if (!string.IsNullOrWhiteSpace(parsed.Bemerkungen)) imported.Add("Bemerkungen");

        stats.Messages.Add(new ImportMessage
        {
            Level = "Info",
            Context = "PDF-SCHACHT",
            Message = $"Schacht importiert: {Path.GetFileName(pdfPath)} | Schacht={key} | Felder={string.Join(", ", imported)}"
        });
    }

    public sealed record ParsedSchachtFields(
        string? SchachtNummer,
        string? Datum,
        string? Funktion,
        string? PrimaereSchaeden,
        string? Bemerkungen,
        string? Status,
        string? Link);

    public static ParsedSchachtFields ParseSchachtFields(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new ParsedSchachtFields(null, null, null, null, null, null, null);

        var normalized = text.Replace("\r\n", "\n");

        string? GetFirst(string pattern)
        {
            var m = Regex.Match(normalized, pattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);
            return m.Success ? m.Groups["v"].Value.Trim() : null;
        }

        var schachtNummer = GetFirst(@"\bNr\.?\s*[:\-]?\s*(?<v>\d{3,})\b")
                            ?? GetFirst(@"\bSchachtnummer\s*[:\-]?\s*(?<v>\d{3,})\b");

        var dateRaw = GetFirst(@"\bDatum\s*[:\-]?\s*(?<v>\d{2}[./-]\d{2}[./-]\d{2,4})\b");
        var datum = NormalizeDate(dateRaw);

        var funktion = GetFirst(@"\bSchachttyp\s+(?<v>[^\n\r]+)")?.Trim();

        var primaryDamages = ParsePrimaryDamagesFromConditionSection(normalized);
        var maengelfrei = Regex.IsMatch(normalized, @"\bM\S*ngelfrei\b", RegexOptions.IgnoreCase)
            ? "Maengelfrei"
            : null;
        var effectivePrimaryDamages = !string.IsNullOrWhiteSpace(primaryDamages) ? primaryDamages : maengelfrei;
        var status = DeriveSchachtStatus(effectivePrimaryDamages, normalized);

        var bemerkung = GetFirst(@"\bBemerkung(?:en)?\s*[:\-]?\s*(?<v>[^\n\r]+)");

        return new ParsedSchachtFields(
            SchachtNummer: schachtNummer,
            Datum: datum,
            Funktion: funktion,
            PrimaereSchaeden: effectivePrimaryDamages,
            Bemerkungen: bemerkung,
            Status: status,
            Link: null);
    }

    private static string? DeriveSchachtStatus(string? primaryDamages, string fullText)
    {
        // If explicit status text exists in PDF, trust that first.
        var explicitStatus = TryParseExplicitStatus(fullText);
        if (!string.IsNullOrWhiteSpace(explicitStatus))
            return explicitStatus;

        // Otherwise derive from damage interpretation.
        if (string.IsNullOrWhiteSpace(primaryDamages))
            return null;

        return string.Equals(primaryDamages.Trim(), "Maengelfrei", StringComparison.OrdinalIgnoreCase)
            ? "abgeschlossen"
            : "offen";
    }

    private static string? TryParseExplicitStatus(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var normalized = text.Replace("\r\n", "\n");
        foreach (var lineRaw in normalized.Split('\n'))
        {
            var line = (lineRaw ?? "").Trim();
            if (line.Length == 0)
                continue;

            if (!line.Contains("Status", StringComparison.OrdinalIgnoreCase))
                continue;

            if (Regex.IsMatch(line, @"\babgeschlossen\b", RegexOptions.IgnoreCase))
                return "abgeschlossen";
            if (Regex.IsMatch(line, @"\boffen\b", RegexOptions.IgnoreCase))
                return "offen";
        }

        return null;
    }

    private static string? ParsePrimaryDamagesFromConditionSection(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var normalized = NormalizeCheckboxGlyphs(text);
        var lines = normalized.Split('\n');
        var entries = new List<(string Component, string Damage, int EncounterIndex)>();
        var encounterIndex = 0;

        foreach (var rawLine in lines)
        {
            var line = rawLine?.Trim();
            if (string.IsNullOrWhiteSpace(line))
                continue;

            if (!TryExtractComponentTail(line!, out var component, out var tail))
                continue;

            foreach (var damage in GetDamageCandidatesForComponent(component))
            {
                if (!IsMarkedDamage(tail, damage))
                    continue;

                entries.Add((component, damage, encounterIndex++));
            }
        }

        if (entries.Count == 0)
            return null;

        var deduped = entries
            .GroupBy(x => $"{x.Component}|{x.Damage}", StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(x => GetComponentOrderIndex(x.Component))
            .ThenBy(x => GetDamageOrderIndex(x.Component, x.Damage))
            .ThenBy(x => x.EncounterIndex)
            .ToList();

        return string.Join("\n", deduped.Select(x => $"{x.Component}: {x.Damage}"));
    }

    private static string NormalizeCheckboxGlyphs(string text)
    {
        return text
            .Replace("â—", "●")
            .Replace("â€¢", "●")
            .Replace("âœ“", "✓")
            .Replace("âœ”", "✓")
            .Replace("âœ—", "✗")
            .Replace("âœ˜", "✗")
            .Replace("☒", "☒")
            .Replace("☑", "☑")
            .Replace("☐", "☐")
            .Replace("■", "■")
            .Replace("□", "□")
            .Replace("•", "●")
            .Replace("✔", "✓")
            .Replace("✘", "✗");
    }

    private static bool TryExtractComponentTail(string line, out string component, out string tail)
    {
        foreach (var candidate in SchachtComponentOrder)
        {
            var m = Regex.Match(line, @"^\s*" + Regex.Escape(candidate) + @"\b(?<tail>.*)$", RegexOptions.IgnoreCase);
            if (!m.Success)
                continue;

            component = candidate;
            tail = m.Groups["tail"].Value ?? "";
            return true;
        }

        component = "";
        tail = "";
        return false;
    }

    private static IReadOnlyList<string> GetDamageCandidatesForComponent(string component)
    {
        if (component.Equals("Schachtdeckel", StringComparison.OrdinalIgnoreCase))
            return new[] { "gerissen", "ausgebrochen", "korrodiert", "klemmt" };

        if (component.Equals("Deckelrahmen", StringComparison.OrdinalIgnoreCase))
            return new[] { "gerissen", "ausgebrochen", "lose" };

        if (component.Equals("Schachthals", StringComparison.OrdinalIgnoreCase)
            || component.Equals("Konus", StringComparison.OrdinalIgnoreCase)
            || component.Equals("Schachtrohr", StringComparison.OrdinalIgnoreCase))
            return new[] { "gerissen", "ausgebrochen", "korrodiert", "fugen mangelhaft verputzt" };

        if (component.Equals("Bankett", StringComparison.OrdinalIgnoreCase)
            || component.Equals("Durchlaufrinne", StringComparison.OrdinalIgnoreCase))
            return new[] { "gerissen", "ausgebrochen", "korrodiert", "ablagerungen" };

        if (component.Equals("Anschluss", StringComparison.OrdinalIgnoreCase))
            return new[] { "gerissen", "ausgebrochen", "mangelhaft eingebunden" };

        if (component.Equals("Leiter/Steigeisen", StringComparison.OrdinalIgnoreCase))
            return new[] { "fehlt", "zu kurz", "verrostet", "defekt" };

        if (component.Equals("Tauchbogen", StringComparison.OrdinalIgnoreCase))
            return new[] { "fehlt", "defekt" };

        return Array.Empty<string>();
    }

    private static bool IsMarkedDamage(string tail, string damage)
    {
        if (string.IsNullOrWhiteSpace(tail) || string.IsNullOrWhiteSpace(damage))
            return false;

        var marker = @"(?:●|•|■|☒|☑|✓|✔|✗|✘|\[\s*[xX]\s*\]|\(\s*[xX]\s*\))";
        var d = Regex.Escape(damage);

        // Marker unmittelbar vor dem Schaden: "● ausgebrochen" / "[x] korrodiert"
        var before = marker + @"\s*" + d + @"\b";
        if (Regex.IsMatch(tail, before, RegexOptions.IgnoreCase))
            return true;

        // Marker unmittelbar nach dem Schaden: "ausgebrochen ●" / "korrodiert [x]"
        var after = d + @"\b\s*" + marker;
        if (Regex.IsMatch(tail, after, RegexOptions.IgnoreCase))
            return true;

        // Robustheitsfall: marker und Schaden in unmittelbarer Nachbarschaft (max 8 Zeichen)
        var nearBefore = marker + @"[^\n\r]{0,8}\b" + d + @"\b";
        if (Regex.IsMatch(tail, nearBefore, RegexOptions.IgnoreCase))
            return true;

        var nearAfter = @"\b" + d + @"\b[^\n\r]{0,8}" + marker;
        return Regex.IsMatch(tail, nearAfter, RegexOptions.IgnoreCase);
    }

    private static int GetComponentOrderIndex(string component)
    {
        for (var i = 0; i < SchachtComponentOrder.Length; i++)
        {
            if (string.Equals(SchachtComponentOrder[i], component, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return int.MaxValue;
    }

    private static int GetDamageOrderIndex(string component, string damage)
    {
        var candidates = GetDamageCandidatesForComponent(component);
        for (var i = 0; i < candidates.Count; i++)
        {
            if (string.Equals(candidates[i], damage, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return int.MaxValue;
    }

    private static string? NormalizeDate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var candidate = raw.Trim();
        var formats = new[] { "dd.MM.yyyy", "dd.MM.yy", "dd/MM/yyyy", "dd/MM/yy", "dd-MM-yyyy", "dd-MM-yy", "yyyy-MM-dd" };
        if (DateTime.TryParseExact(candidate, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            return parsed.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);

        return candidate;
    }

    private static void SetSchachtField(SchachtRecord record, string logicalField, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        foreach (var candidate in GetSchachtFieldAliases(logicalField))
            record.SetFieldValue(candidate, value);
    }

    private static IReadOnlyList<string> GetSchachtFieldAliases(string logicalField)
    {
        return logicalField switch
        {
            "Schachtnummer" => new[] { "Schachtnummer" },
            "Funktion" => new[] { "Funktion" },
            "Primaere Schaeden" => new[] { "Primäre Schäden", "Primaere Schaeden", "PrimÃ¤re SchÃ¤den" },
            "Bemerkungen" => new[] { "Bemerkungen" },
            "Link" => new[] { "Link" },
            "NR." => new[] { "NR.", "Nr." },
            "Nr." => new[] { "Nr.", "NR." },
            "Ausfuehrung Datum/Jahr" => new[] { "Ausführung Datum/Jahr", "Ausfuehrung Datum/Jahr", "AusfÃ¼hrung Datum/Jahr" },
            "Status offen/abgeschlossen" => new[] { "Status offen/abgeschlossen" },
            _ => new[] { logicalField }
        };
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
