using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AuswertungPro.Next.Infrastructure.Import.Pdf;

namespace AuswertungPro.Next.Infrastructure;

/// <summary>
/// HoldingFolderDistributor — Schacht-PDF-Parsing (partial class).
///
/// Refactor 2026-05-07 (Etappe 3, Charge R7): Public ParseSchachtPdf*
/// und alle Schacht-spezifischen Helpers (Form-Field-Extraction,
/// Sibling-Protokoll-Datums-Resolution, Cache-Indizes) ausgegliedert.
/// Mechanisch — keine Verhaltensaenderung.
/// </summary>
public static partial class HoldingFolderDistributor
{
    private static readonly object SchachtDateIndexSync = new();
    private static readonly Dictionary<string, IReadOnlyDictionary<string, DateTime>> SchachtDateIndexCache =
        new(StringComparer.OrdinalIgnoreCase);

    public static ParsedShaftPdf ParseSchachtPdf(string text)
    {
        text = NormalizeText(text);
        if (string.IsNullOrWhiteSpace(text))
            return new ParsedShaftPdf(false, "Empty page", null, null);

        return ParseSchachtPdfPage(text);
    }

    public static ParsedShaftPdf ParseSchachtPdfPage(string text)
    {
        text = NormalizeText(text);
        if (string.IsNullOrWhiteSpace(text))
            return new ParsedShaftPdf(false, "Empty page", null, null);

        var shaftNumber = TryFindSchachtNumber(text);
        var date = TryFindSchachtDate(text);

        if (string.IsNullOrWhiteSpace(shaftNumber) && date is null)
            return new ParsedShaftPdf(false, "Schachtnummer und Datum nicht gefunden", null, null);
        if (string.IsNullOrWhiteSpace(shaftNumber))
            return new ParsedShaftPdf(false, "Schachtnummer nicht gefunden", date, null);
        if (date is null)
            return new ParsedShaftPdf(false, "Datum nicht gefunden", null, shaftNumber);

        return new ParsedShaftPdf(true, null, date, shaftNumber);
    }

    private static ParsedShaftPdf ParseSchachtPdfPageWithOcrFallback(PageInfo page)
    {
        var parsed = ParseSchachtPdfPage(page.Text);
        if (parsed.Success)
            return parsed;

        if (string.IsNullOrWhiteSpace(page.SourcePath) || !File.Exists(page.SourcePath))
            return parsed;

        var completedFromSibling = TryCompleteShaftDateFromSiblingProtocol(page.SourcePath, parsed);
        if (completedFromSibling is not null)
            return completedFromSibling;

        // Many Schachtprotokolle are interactive PDF forms where values are not in page text.
        var parsedFromForm = TryParseSchachtPdfPageFromFormFields(page.SourcePath, page.PageNumber);
        if (parsedFromForm is not null)
            return parsedFromForm;

        // OCR fallback is expensive; only try when baseline parsing has no usable result.
        var ocr = PdfOcrExtractor.TryExtractPageText(page.SourcePath, page.PageNumber);
        if (!ocr.Success || string.IsNullOrWhiteSpace(ocr.Text))
            return parsed;

        var parsedFromOcr = ParseSchachtPdfPage(ocr.Text);
        var mergedShaft = !string.IsNullOrWhiteSpace(parsedFromOcr.ShaftNumber) ? parsedFromOcr.ShaftNumber : parsed.ShaftNumber;
        var mergedDate = parsedFromOcr.Date ?? parsed.Date;
        if (string.IsNullOrWhiteSpace(mergedShaft))
            return parsed;

        if (mergedDate is null)
        {
            var resolvedDate = TryResolveDateFromSiblingProtocol(page.SourcePath, mergedShaft);
            if (resolvedDate is not null)
                mergedDate = resolvedDate;
        }

        if (mergedDate is null)
            return parsed;

        return new ParsedShaftPdf(
            true,
            MergeMessage(parsedFromOcr.Message, "aus OCR"),
            mergedDate,
            mergedShaft);
    }

    private static ParsedShaftPdf? TryParseSchachtPdfPageFromFormFields(string pdfPath, int pageNumber)
    {
        var entries = PdfFormFieldExtractor.GetPageFieldEntries(pdfPath, pageNumber);
        if (entries.Count == 0)
            return null;

        // First pass: label-preserving synthetic text for existing parser rules.
        var syntheticText = BuildSyntheticFormText(entries);
        var parsed = ParseSchachtPdfPage(syntheticText);
        if (parsed.Success)
        {
            return new ParsedShaftPdf(
                true,
                MergeMessage(parsed.Message, "aus PDF-Formular"),
                parsed.Date,
                parsed.ShaftNumber);
        }

        // Second pass: value-only heuristics for generic field names.
        var date = TryExtractDateFromFormEntries(entries);
        var shaft = TryExtractSchachtNumberFromFormEntries(entries);
        if (string.IsNullOrWhiteSpace(shaft) || date is null)
            return null;

        return new ParsedShaftPdf(true, "aus PDF-Formular", date, shaft);
    }

    private static string BuildSyntheticFormText(IReadOnlyList<PdfFormFieldEntry> entries)
    {
        var lines = new List<string>(entries.Count * 2);
        foreach (var entry in entries)
        {
            var labels = new[] { entry.PartialName, entry.AlternateName, entry.MappingName }
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (labels.Count == 0)
            {
                lines.Add(entry.Value);
                continue;
            }

            foreach (var label in labels)
                lines.Add($"{label}: {entry.Value}");
        }

        return string.Join("\n", lines);
    }

    private static string? TryExtractSchachtNumberFromFormEntries(IReadOnlyList<PdfFormFieldEntry> entries)
    {
        // Prefer explicit labels.
        foreach (var entry in entries)
        {
            var label = BuildFormEntryLabel(entry);
            if (!ContainsSchachtNumberLabel(label))
                continue;

            var candidate = ExtractShaftNumberToken(entry.Value);
            if (!string.IsNullOrWhiteSpace(candidate))
                return candidate;
        }

        // Fallback: strict numeric tokens only.
        foreach (var entry in entries)
        {
            var candidate = ExtractShaftNumberToken(entry.Value);
            if (!string.IsNullOrWhiteSpace(candidate))
                return candidate;
        }

        return null;
    }

    private static string BuildFormEntryLabel(PdfFormFieldEntry entry)
    {
        return string.Join(" ",
            new[] { entry.PartialName, entry.AlternateName, entry.MappingName }
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x!.Trim()));
    }

    private static bool ContainsDateLabel(string? label)
    {
        if (string.IsNullOrWhiteSpace(label))
            return false;

        return label.Contains("datum", StringComparison.OrdinalIgnoreCase)
               || label.Contains("date", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsSchachtNumberLabel(string? label)
    {
        if (string.IsNullOrWhiteSpace(label))
            return false;

        return label.Contains("schacht", StringComparison.OrdinalIgnoreCase)
               || label.Contains("nummer", StringComparison.OrdinalIgnoreCase)
               || Regex.IsMatch(label, @"\bnr\.?\b", RegexOptions.IgnoreCase);
    }

    private static string? ExtractShaftNumberToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        // Prefer standalone numeric values, common for Schachtnummer forms.
        var direct = Regex.Match(value.Trim(), @"^(?<nr>\d{3,8})$");
        if (direct.Success)
            return direct.Groups["nr"].Value;

        var any = Regex.Match(value, @"\b(?<nr>\d{3,8})\b");
        if (!any.Success)
            return null;

        var token = any.Groups["nr"].Value;
        // Avoid obvious date fragments (e.g. year values).
        if (token.Length == 4 && int.TryParse(token, out var year) && year >= 1900 && year <= 2100)
            return null;

        return token;
    }

    private static ParsedShaftPdf? TryCompleteShaftDateFromSiblingProtocol(string sourcePdfPath, ParsedShaftPdf parsed)
    {
        if (string.IsNullOrWhiteSpace(parsed.ShaftNumber) || parsed.Date is not null)
            return null;

        var resolvedDate = TryResolveDateFromSiblingProtocol(sourcePdfPath, parsed.ShaftNumber);
        if (resolvedDate is null)
            return null;

        return new ParsedShaftPdf(
            true,
            MergeMessage(parsed.Message, "Datum aus Schachtprotokoll"),
            resolvedDate,
            parsed.ShaftNumber);
    }

    private static DateTime? TryResolveDateFromSiblingProtocol(string sourcePdfPath, string shaftNumber)
    {
        if (string.IsNullOrWhiteSpace(sourcePdfPath)
            || string.IsNullOrWhiteSpace(shaftNumber)
            || !File.Exists(sourcePdfPath))
            return null;

        var dir = Path.GetDirectoryName(sourcePdfPath);
        if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
            return null;

        var normalizedShaft = NormalizeShaftNumberKey(shaftNumber);
        if (string.IsNullOrWhiteSpace(normalizedShaft))
            return null;

        var siblingProtocolPdfs = Directory.EnumerateFiles(dir, "*.pdf", SearchOption.TopDirectoryOnly)
            .Where(path => !string.Equals(path, sourcePdfPath, StringComparison.OrdinalIgnoreCase))
            .Where(path =>
            {
                var name = Path.GetFileName(path);
                return name.Contains("schachtprotokoll", StringComparison.OrdinalIgnoreCase)
                       || name.Contains("protokoll", StringComparison.OrdinalIgnoreCase);
            })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (siblingProtocolPdfs.Count == 0)
            return null;

        foreach (var protocolPdf in siblingProtocolPdfs)
        {
            var index = GetOrBuildSchachtDateIndex(protocolPdf);
            if (index.Count == 0)
                continue;

            if (index.TryGetValue(normalizedShaft, out var date))
                return date;
        }

        return null;
    }

    private static IReadOnlyDictionary<string, DateTime> GetOrBuildSchachtDateIndex(string protocolPdfPath)
    {
        lock (SchachtDateIndexSync)
        {
            if (SchachtDateIndexCache.TryGetValue(protocolPdfPath, out var cached))
                return cached;
        }

        var built = BuildSchachtDateIndex(protocolPdfPath);

        lock (SchachtDateIndexSync)
        {
            SchachtDateIndexCache[protocolPdfPath] = built;
        }

        return built;
    }

    private static IReadOnlyDictionary<string, DateTime> BuildSchachtDateIndex(string protocolPdfPath)
    {
        var index = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var extraction = PdfTextExtractor.ExtractPages(protocolPdfPath);
            for (var i = 0; i < extraction.Pages.Count; i++)
            {
                ParsedShaftPdf? parsed = null;

                var fromText = ParseSchachtPdfPage(extraction.Pages[i]);
                if (fromText.Success)
                {
                    parsed = fromText;
                }
                else
                {
                    parsed = TryParseSchachtPdfPageFromFormFields(protocolPdfPath, i + 1);
                }

                if (parsed is null || !parsed.Success || parsed.Date is null || string.IsNullOrWhiteSpace(parsed.ShaftNumber))
                    continue;

                var key = NormalizeShaftNumberKey(parsed.ShaftNumber);
                if (string.IsNullOrWhiteSpace(key))
                    continue;

                if (!index.ContainsKey(key))
                    index[key] = parsed.Date.Value;
            }
        }
        catch
        {
            // Best effort date index.
        }

        return index;
    }

    private static string NormalizeShaftNumberKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var digits = Regex.Replace(value, @"\D", "");
        if (string.IsNullOrWhiteSpace(digits))
            return string.Empty;

        return TrimLeadingZerosValue(digits);
    }
}
