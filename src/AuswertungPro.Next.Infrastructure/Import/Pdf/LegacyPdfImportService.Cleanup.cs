using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import.Common;

namespace AuswertungPro.Next.Infrastructure.Import.Pdf;

// LegacyPdfImportService Cleanup-Pfade: Findet korrupte Placeholder-Records,
// entfernt verwaiste/duplizierte Eintraege, baut Repair-Fingerprints fuer
// Dedup. Aus dem Hauptdatei extrahiert (Slice 23b).
public sealed partial class LegacyPdfImportService
{
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
