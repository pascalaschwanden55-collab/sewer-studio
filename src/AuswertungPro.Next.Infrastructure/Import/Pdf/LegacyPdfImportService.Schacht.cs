using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Infrastructure.Import.Common;

namespace AuswertungPro.Next.Infrastructure.Import.Pdf;

// LegacyPdfImportService Schacht-PDF-Verarbeitung: Erkennt Schachtprotokoll-
// PDFs (LooksLikeSchachtProtokoll), parst alle Schachtfelder (ParseSchacht
// Fields), extrahiert primaere Schaeden aus Checkbox-Tabellen, leitet Status
// ab und mappt logische Feldnamen auf SchachtRecord-Aliasse.
// Aus dem Hauptdatei extrahiert (Slice 23a).
public sealed partial class LegacyPdfImportService
{
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

        // PDF-Pfad speichern fuer spaeteres Oeffnen per Rechtsklick
        target.SetFieldValue("PDF_Path", pdfPath);

        // Strukturiertes Protokoll aus Bauteil-Schaeden erstellen
        var damageEntries = ParseSchachtDamageEntries(fullText);
        if (damageEntries.Count > 0)
        {
            var protocolEntries = damageEntries.Select(d => new ProtocolEntry
            {
                Code = d.Component,
                Beschreibung = d.Damage,
                Source = ProtocolEntrySource.Imported
            }).ToList();

            var originalRevision = new ProtocolRevision
            {
                Comment = $"Import aus PDF: {Path.GetFileName(pdfPath)}",
                Entries = protocolEntries
            };
            var currentRevision = new ProtocolRevision
            {
                Comment = "Arbeitskopie",
                Entries = protocolEntries.Select(e => new ProtocolEntry
                {
                    Code = e.Code,
                    Beschreibung = e.Beschreibung,
                    Source = e.Source
                }).ToList()
            };

            target.Protocol = new ProtocolDocument
            {
                HaltungId = key,
                Original = originalRevision,
                Current = currentRevision
            };
        }

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
        if (damageEntries.Count > 0) imported.Add($"Protokoll ({damageEntries.Count} Beobachtungen)");

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

    /// <summary>
    /// Parst die strukturierten Bauteil-Schaeden aus dem Zustandsabschnitt des Schachtprotokolls.
    /// </summary>
    internal static IReadOnlyList<(string Component, string Damage)> ParseSchachtDamageEntries(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Array.Empty<(string, string)>();

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
            return Array.Empty<(string, string)>();

        return entries
            .GroupBy(x => $"{x.Component}|{x.Damage}", StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(x => GetComponentOrderIndex(x.Component))
            .ThenBy(x => GetDamageOrderIndex(x.Component, x.Damage))
            .ThenBy(x => x.EncounterIndex)
            .Select(x => (x.Component, x.Damage))
            .ToList();
    }

    private static string? ParsePrimaryDamagesFromConditionSection(string text)
    {
        var entries = ParseSchachtDamageEntries(text);
        if (entries.Count == 0)
            return null;

        return string.Join("\n", entries.Select(x => $"{x.Component}: {x.Damage}"));
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
}
