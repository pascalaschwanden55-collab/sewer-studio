using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Import.Xtf;

internal static class M150MdbImportHelper
{
    private static readonly Regex HoldingRx = new(@"(?<!\d)((?:\d{3,}|\d{1,3}(?:\.\d+)+)\s*[-/]\s*(?:\d{3,}|\d{1,3}(?:\.\d+)+))(?!\d)", RegexOptions.Compiled);
    private static readonly Regex PointRx = new(@"^[A-Za-z0-9][A-Za-z0-9._-]*$", RegexOptions.Compiled);
    private static readonly Regex DateRx = new(@"(\d{2}[./-]\d{2}[./-]\d{2,4}|\d{4}-\d{2}-\d{2})", RegexOptions.Compiled);

    private static readonly string[] HoldingKeyHints =
    [
        "haltung", "leitungsname", "leitungsnummer", "leitung", "kanalnummer", "abschnitt", "strang", "bezeichnung",
        // Common M150 code fields that may already contain the combined section id.
        "hg001", "kg001"
    ];

    private static readonly string[] FromPointKeyHints =
    [
        "schachtoben", "von", "vonpunkt", "startschacht", "obererpunkt", "hg003", "hg011", "hg201", "kg011"
    ];

    private static readonly string[] ToPointKeyHints =
    [
        "schachtunten", "nach", "nachpunkt", "endschacht", "untererpunkt", "hg004", "hg012", "hg202", "kg012"
    ];

    private static readonly string[] DateKeyHints =
    [
        "datum", "date", "zeitpunkt", "inspektionsdatum", "befahrungsdatum", "aufnahmedatum", "inspection", "hi003", "hi104"
    ];

    private static readonly string[] LengthKeyHints =
    [
        "laenge", "length", "inspizierte", "rohrlaenge", "hg008", "hg310", "hi101"
    ];

    private static readonly string[] DnKeyHints =
    [
        "dn", "durchmesser", "nennweite", "weite", "lichte", "hg306", "hg307"
    ];

    private static readonly string[] MaterialKeyHints =
    [
        "material", "werkstoff", "rohrmaterial", "hg304"
    ];

    private static readonly string[] DirectionKeyHints =
    [
        "richtung", "inspektionsrichtung", "fliessrichtung", "flowdirection"
    ];

    private static readonly string[] RemarkKeyHints =
    [
        "bemerk", "anmerk", "kommentar", "comment", "hinweis"
    ];

    private static readonly string[] LinkKeyHints =
    [
        "hi116", "film", "video", "videofile", "dateiname", "link", "pfad", "hi006"
    ];

    public static List<HaltungRecord> ParseM150File(string path, out List<string> warnings)
    {
        warnings = new List<string>();
        var text = File.ReadAllText(path, Encoding.UTF8);
        var entries = new List<ImportEntry>();

        try
        {
            var doc = XDocument.Load(path, LoadOptions.PreserveWhitespace);
            entries.AddRange(ExtractEntriesFromXml(doc));
        }
        catch (Exception ex)
        {
            warnings.Add($"M150 XML konnte nicht direkt gelesen werden: {ex.Message}");
        }

        if (entries.Count == 0)
            entries.AddRange(ExtractEntriesFromRawText(text));
        return BuildRecords(entries);
    }

    public static (int HgCount, int HiCount) GetM150XmlNodeCounts(string path)
    {
        try
        {
            var doc = XDocument.Load(path, LoadOptions.PreserveWhitespace);
            var hgCount = doc.Descendants().Count(e => e.Name.LocalName.Equals("HG", StringComparison.OrdinalIgnoreCase));
            var hiCount = doc.Descendants().Count(e => e.Name.LocalName.Equals("HI", StringComparison.OrdinalIgnoreCase));
            return (hgCount, hiCount);
        }
        catch
        {
            return (0, 0);
        }
    }

    public static bool TryParseMdbFile(string path, out List<HaltungRecord> records, out string? error, out List<string> warnings)
    {
        records = new List<HaltungRecord>();
        warnings = new List<string>();
        error = null;

        if (!TryDumpMdbRows(path, out var rows, out error))
            return false;

        var entries = new List<ImportEntry>();
        var winCanViewerEntries = ExtractEntriesFromWinCanViewerRows(rows, warnings);
        if (winCanViewerEntries.Count > 0)
        {
            entries.AddRange(winCanViewerEntries);
        }
        else
        {
            foreach (var row in rows)
            {
                var entry = ExtractEntryFromRow(row);
                if (entry is not null)
                    entries.Add(entry);
            }
        }

        // Fallback: parse complete row text if no structured row yielded a record.
        if (entries.Count == 0)
        {
            var allText = string.Join("\n", rows.Select(r => string.Join(" | ", r.Select(kv => $"{kv.Key}={kv.Value}"))));
            entries.AddRange(ExtractEntriesFromRawText(allText));
        }

        records = BuildRecords(entries);
        return true;
    }

    private static List<ImportEntry> ExtractEntriesFromWinCanViewerRows(
        IReadOnlyList<Dictionary<string, string>> rows,
        List<string> warnings)
    {
        var sections = new Dictionary<string, WinCanSection>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            if (!GetTableName(row).Equals("S_T", StringComparison.OrdinalIgnoreCase))
                continue;

            var sectionId = GetRowValue(row, "S_ID");
            if (string.IsNullOrWhiteSpace(sectionId))
                continue;

            sections[sectionId] = new WinCanSection(
                SectionId: sectionId,
                StartNode: GetRowValue(row, "S_StartNode"),
                EndNode: GetRowValue(row, "S_EndNode"),
                Length: GetRowValue(row, "S_Sectionlength"),
                Material: GetRowValue(row, "S_PipeMaterial"),
                Direction: GetRowValue(row, "S_SectionFlow"),
                Date: GetRowValue(row, "S_CreationDate"));
        }

        if (sections.Count == 0)
            return new List<ImportEntry>();

        var entries = new List<ImportEntry>();
        var hasInspectionRows = false;
        foreach (var row in rows)
        {
            if (!GetTableName(row).Equals("SI_T", StringComparison.OrdinalIgnoreCase))
                continue;

            hasInspectionRows = true;

            var sectionId = GetRowValue(row, "SI_Section_ID");
            if (string.IsNullOrWhiteSpace(sectionId))
                continue;
            if (!sections.TryGetValue(sectionId, out var section))
                continue;

            var dirRaw = Coalesce(
                GetRowValue(row, "SI_InspectionDir"),
                section.Direction);

            var holding = BuildHoldingFromWinCanSection(section.StartNode, section.EndNode, dirRaw);
            if (!IsHoldingId(holding))
                continue;

            var date = TryNormalizeDate(Coalesce(
                GetRowValue(row, "SI_InspDate"),
                GetRowValue(row, "SI_InspectionDate"),
                GetRowValue(row, "SI_Date"),
                section.Date));

            var link = PickWinCanVideoLink(row);
            var direction = NormalizeWinCanDirection(dirRaw);

            entries.Add(new ImportEntry(
                NormalizeHolding(holding),
                date,
                NormalizeNumberText(section.Length),
                null,
                NullIfWhite(section.Material),
                NullIfWhite(direction),
                null,
                NullIfWhite(link),
                Array.Empty<VsaFinding>()));
        }

        if (entries.Count == 0 && hasInspectionRows)
        {
            // Some viewer exports do not persist SI rows; keep minimal section-level import.
            foreach (var section in sections.Values)
            {
                var holding = BuildHoldingFromWinCanSection(section.StartNode, section.EndNode, section.Direction);
                if (!IsHoldingId(holding))
                    continue;

                entries.Add(new ImportEntry(
                    NormalizeHolding(holding),
                    TryNormalizeDate(section.Date),
                    NormalizeNumberText(section.Length),
                    null,
                    NullIfWhite(section.Material),
                    NullIfWhite(NormalizeWinCanDirection(section.Direction)),
                    null,
                    null,
                    Array.Empty<VsaFinding>()));
            }
        }

        if (entries.Count > 0)
            warnings.Add($"WinCan Viewer MDB erkannt: {entries.Count} Haltungen aus S_T/SI_T.");

        return entries;
    }

    private static bool IsWinCanViewerTable(string? tableName)
        => string.Equals(tableName, "S_T", StringComparison.OrdinalIgnoreCase)
           || string.Equals(tableName, "SI_T", StringComparison.OrdinalIgnoreCase);

    private static string GetTableName(Dictionary<string, string> row)
        => GetRowValue(row, "__table");

    private static string GetRowValue(Dictionary<string, string> row, string key)
        => row.TryGetValue(key, out var value) ? (value ?? "").Trim() : string.Empty;

    private static string Coalesce(params string[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }

        return string.Empty;
    }

    private static string PickWinCanVideoLink(Dictionary<string, string> row)
    {
        var candidates = new[]
        {
            GetRowValue(row, "SI_Virtual~ClipFilename"),
            GetRowValue(row, "SI_Video_1_Filename"),
            GetRowValue(row, "SI_Video_2_Filename"),
            GetRowValue(row, "SI_ProtocolFile")
        };

        foreach (var candidate in candidates)
        {
            if (LooksLikeVideoLink(candidate))
                return candidate;
        }

        return string.Empty;
    }

    private static string BuildHoldingFromWinCanSection(string startRaw, string endRaw, string dirRaw)
    {
        var start = ExtractPointId(startRaw);
        var end = ExtractPointId(endRaw);
        if (!IsPointId(start) || !IsPointId(end))
            return string.Empty;

        return ShouldReverseWinCanDirection(dirRaw)
            ? NormalizeHolding($"{end}-{start}")
            : NormalizeHolding($"{start}-{end}");
    }

    private static string ExtractPointId(string? raw)
    {
        var value = (raw ?? "").Trim();
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        if (IsPointId(value))
            return value;

        var m = Regex.Match(value, @"(\d{2,}(?:\.\d+)+|\d{3,})");
        return m.Success ? m.Groups[1].Value : string.Empty;
    }

    private static bool ShouldReverseWinCanDirection(string? raw)
    {
        var dir = (raw ?? "").Trim();
        if (string.IsNullOrWhiteSpace(dir))
            return false;

        return dir.Equals("U", StringComparison.OrdinalIgnoreCase)
               || dir.Equals("UP", StringComparison.OrdinalIgnoreCase)
               || dir.Equals("UPSTREAM", StringComparison.OrdinalIgnoreCase)
               || dir.Equals("2", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeWinCanDirection(string? raw)
    {
        var dir = (raw ?? "").Trim();
        if (string.IsNullOrWhiteSpace(dir))
            return string.Empty;

        if (ShouldReverseWinCanDirection(dir))
            return "unten -> oben";

        if (dir.Equals("D", StringComparison.OrdinalIgnoreCase)
            || dir.Equals("DOWN", StringComparison.OrdinalIgnoreCase)
            || dir.Equals("DOWNSTREAM", StringComparison.OrdinalIgnoreCase)
            || dir.Equals("1", StringComparison.OrdinalIgnoreCase))
            return "oben -> unten";

        return NormalizeDirection(dir);
    }

    private static List<ImportEntry> ExtractEntriesFromXml(XDocument doc)
    {
        var entries = new List<ImportEntry>();
        foreach (var node in doc.Descendants())
        {
            var children = node.Elements().ToList();
            if (children.Count == 0 || children.Count > 80)
                continue;

            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var c in children)
            {
                if (c.HasElements)
                    continue;

                var key = NormalizeKey(c.Name.LocalName);
                if (string.IsNullOrWhiteSpace(key))
                    continue;

                var value = (c.Value ?? "").Trim();
                if (!string.IsNullOrWhiteSpace(value))
                    map[key] = value;
            }

            if (map.Count == 0)
                continue;

            var entry = ExtractEntryFromMap(map);
            XElement? sourceHgNode = null;
            if (entry is not null && node.Name.LocalName.Equals("HG", StringComparison.OrdinalIgnoreCase))
                sourceHgNode = node;
            if (entry is null && node.Name.LocalName.Equals("HI", StringComparison.OrdinalIgnoreCase))
            {
                // ISYBAU M150 often stores the section endpoints in the parent HG node
                // while inspection date/media are in HI. Merge both maps for extraction.
                var parentHg = node.Parent;
                if (parentHg is not null && parentHg.Name.LocalName.Equals("HG", StringComparison.OrdinalIgnoreCase))
                {
                    var merged = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var parentChild in parentHg.Elements())
                    {
                        if (parentChild.HasElements)
                            continue;

                        var parentKey = NormalizeKey(parentChild.Name.LocalName);
                        if (string.IsNullOrWhiteSpace(parentKey))
                            continue;

                        var parentValue = (parentChild.Value ?? "").Trim();
                        if (!string.IsNullOrWhiteSpace(parentValue))
                            merged[parentKey] = parentValue;
                    }

                    foreach (var kv in map)
                        merged[kv.Key] = kv.Value;

                    entry = ExtractEntryFromMap(merged);
                    if (entry is not null)
                        sourceHgNode = parentHg;
                }
            }

            if (entry is not null)
            {
                if (sourceHgNode is not null)
                {
                    var findings = ExtractFindingsFromHgNode(sourceHgNode);
                    entry = entry with { Findings = findings };
                }
                entries.Add(entry);
            }
        }

        return entries;
    }

    private static List<ImportEntry> ExtractEntriesFromRawText(string text)
    {
        var entries = new List<ImportEntry>();
        if (string.IsNullOrWhiteSpace(text))
            return entries;

        foreach (Match m in HoldingRx.Matches(text))
        {
            var holding = NormalizeHolding(m.Groups[1].Value);
            if (!IsHoldingId(holding))
                continue;

            var start = Math.Max(0, m.Index - 180);
            var len = Math.Min(text.Length - start, 360);
            var context = text.Substring(start, len);

            var date = TryNormalizeDate(DateRx.Match(context).Groups[1].Value);
            entries.Add(new ImportEntry(holding, date, null, null, null, null, null, null, Array.Empty<VsaFinding>()));
        }

        return entries;
    }

    private static ImportEntry? ExtractEntryFromRow(Dictionary<string, string> row)
    {
        if (row.Count == 0)
            return null;

        var normalized = row.ToDictionary(kv => NormalizeKey(kv.Key), kv => (kv.Value ?? "").Trim(), StringComparer.OrdinalIgnoreCase);
        return ExtractEntryFromMap(normalized);
    }

    private static ImportEntry? ExtractEntryFromMap(Dictionary<string, string> map)
    {
        var holding = TryExtractHoldingId(PickValue(map, HoldingKeyHints, _ => true));
        if (!IsHoldingId(holding))
        {
            var start = PickValue(map, FromPointKeyHints, IsPointId);
            var end = PickValue(map, ToPointKeyHints, IsPointId);
            if (IsPointId(start) && IsPointId(end))
                holding = $"{start}-{end}";
        }

        if (!IsHoldingId(holding))
        {
            holding = map.Values
                .Select(TryExtractHoldingId)
                .FirstOrDefault(IsHoldingId) ?? string.Empty;
        }

        if (!IsHoldingId(holding))
            return null;

        var dateRaw = PickValue(map, DateKeyHints, _ => true);
        var length = PickValue(map, LengthKeyHints, _ => true);
        var dn = PickValue(map, DnKeyHints, _ => true);
        var material = PickValue(map, MaterialKeyHints, _ => true);
        var direction = NormalizeDirection(PickValue(map, DirectionKeyHints, _ => true));
        var remarks = PickValue(map, RemarkKeyHints, _ => true);
        var link = PickValue(map, LinkKeyHints, LooksLikeVideoLink);
        if (string.IsNullOrWhiteSpace(link))
            link = PickValue(map, LinkKeyHints, _ => true);

        return new ImportEntry(
            NormalizeHolding(holding),
            TryNormalizeDate(dateRaw),
            NormalizeNumberText(length),
            NormalizeNumberText(dn),
            NullIfWhite(material),
            NullIfWhite(direction),
            NullIfWhite(remarks),
            NullIfWhite(link),
            Array.Empty<VsaFinding>());
    }

    private static List<HaltungRecord> BuildRecords(List<ImportEntry> entries)
    {
        var grouped = entries
            .Where(e => IsHoldingId(e.Holding))
            .GroupBy(e => e.Holding, StringComparer.OrdinalIgnoreCase);

        var records = new List<HaltungRecord>();
        foreach (var g in grouped)
        {
            var merged = MergeEntries(g);
            var rec = new HaltungRecord();
            rec.SetFieldValue("Haltungsname", merged.Holding, FieldSource.Xtf, userEdited: false);
            if (!string.IsNullOrWhiteSpace(merged.Date))
                rec.SetFieldValue("Datum_Jahr", merged.Date, FieldSource.Xtf, userEdited: false);
            if (!string.IsNullOrWhiteSpace(merged.Length))
                rec.SetFieldValue("Haltungslaenge_m", merged.Length, FieldSource.Xtf, userEdited: false);
            if (!string.IsNullOrWhiteSpace(merged.Dn))
                rec.SetFieldValue("DN_mm", merged.Dn, FieldSource.Xtf, userEdited: false);
            if (!string.IsNullOrWhiteSpace(merged.Material))
                rec.SetFieldValue("Rohrmaterial", merged.Material, FieldSource.Xtf, userEdited: false);
            if (!string.IsNullOrWhiteSpace(merged.Direction))
                rec.SetFieldValue("Inspektionsrichtung", merged.Direction, FieldSource.Xtf, userEdited: false);
            if (!string.IsNullOrWhiteSpace(merged.Remarks))
                rec.SetFieldValue("Bemerkungen", merged.Remarks, FieldSource.Xtf, userEdited: false);
            if (!string.IsNullOrWhiteSpace(merged.Link))
                rec.SetFieldValue("Link", merged.Link, FieldSource.Xtf, userEdited: false);
            if (merged.Findings.Count > 0)
            {
                rec.VsaFindings = new List<VsaFinding>(merged.Findings);
                var primary = FormatPrimaryDamages(merged.Findings);
                if (!string.IsNullOrWhiteSpace(primary))
                    rec.SetFieldValue("Primaere_Schaeden", primary, FieldSource.Xtf, userEdited: false);
            }

            records.Add(rec);
        }

        return records;
    }

    private static ImportEntry MergeEntries(IEnumerable<ImportEntry> entries)
    {
        var list = entries.ToList();
        var first = list[0];
        return new ImportEntry(
            first.Holding,
            PickBest(list.Select(e => e.Date)),
            PickBest(list.Select(e => e.Length)),
            PickBest(list.Select(e => e.Dn)),
            PickBest(list.Select(e => e.Material)),
            PickBest(list.Select(e => e.Direction)),
            PickBest(list.Select(e => e.Remarks)),
            PickBest(list.Select(e => e.Link)),
            list.SelectMany(e => e.Findings)
                .Where(f => !string.IsNullOrWhiteSpace(f.KanalSchadencode))
                .GroupBy(f => $"{f.KanalSchadencode}|{f.SchadenlageAnfang}|{f.Raw}", StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList());
    }

    private static string PickBest(IEnumerable<string?> values)
        => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim() ?? string.Empty;

    private static string NormalizeHolding(string value)
    {
        var v = (value ?? "").Trim();
        v = Regex.Replace(v, @"\s+", "");
        v = v.Replace('/', '-');
        v = v.Replace('–', '-');
        v = v.Replace('—', '-');
        return v;
    }

    private static string TryExtractHoldingId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var normalized = NormalizeHolding(value);
        var m = HoldingRx.Match(normalized);
        return m.Success ? NormalizeHolding(m.Groups[1].Value) : string.Empty;
    }

    private static bool IsHoldingId(string? value)
        => !string.IsNullOrWhiteSpace(value) && HoldingRx.IsMatch(value.Trim());

    private static bool IsPointId(string? value)
        => !string.IsNullOrWhiteSpace(value) && PointRx.IsMatch(value.Trim());

    private static string? TryNormalizeDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var text = value.Trim();
        var m = DateRx.Match(text);
        if (m.Success)
            text = m.Groups[1].Value;

        var formats = new[] { "dd.MM.yyyy", "dd.MM.yy", "dd/MM/yyyy", "dd/MM/yy", "dd-MM-yyyy", "dd-MM-yy", "yyyy-MM-dd", "yyyyMMdd" };
        if (DateTime.TryParseExact(text, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
            return d.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);

        return null;
    }

    private static string NormalizeDirection(string? value)
    {
        var v = (value ?? "").Trim();
        if (string.IsNullOrWhiteSpace(v))
            return string.Empty;

        if (v.Contains("oben", StringComparison.OrdinalIgnoreCase) || v.Contains("von", StringComparison.OrdinalIgnoreCase))
            return "oben -> unten";
        if (v.Contains("unten", StringComparison.OrdinalIgnoreCase) || v.Contains("nach", StringComparison.OrdinalIgnoreCase))
            return "unten -> oben";
        return v;
    }

    private static bool LooksLikeVideoLink(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var v = value.Trim().Trim('"', '\'');
        var ext = Path.GetExtension(v).ToLowerInvariant();
        if (ext is ".mp2" or ".mpg" or ".mpeg" or ".mp4" or ".avi" or ".mov" or ".wmv" or ".mkv")
            return true;

        // Some exports omit extension but keep the classic time-stamped token pattern.
        return Regex.IsMatch(v, @"^\d+_\d+_\d+_\d{8}_\d{6}$", RegexOptions.CultureInvariant);
    }

    private static string NormalizeNumberText(string? value)
    {
        var v = (value ?? "").Trim();
        if (string.IsNullOrWhiteSpace(v))
            return string.Empty;

        var m = Regex.Match(v, @"-?\d+(?:[.,]\d+)?");
        return m.Success ? m.Value.Replace(",", ".") : v;
    }

    private static string? NullIfWhite(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static List<VsaFinding> ExtractFindingsFromHgNode(XElement hgNode)
    {
        var findings = new List<VsaFinding>();

        foreach (var hz in hgNode.Descendants().Where(e => e.Name.LocalName.Equals("HZ", StringComparison.OrdinalIgnoreCase)))
        {
            var code = (string?)hz.Elements().FirstOrDefault(e => e.Name.LocalName.Equals("HZ002", StringComparison.OrdinalIgnoreCase)) ?? "";
            code = code.Trim();
            if (string.IsNullOrWhiteSpace(code))
                continue;

            var text = ((string?)hz.Elements().FirstOrDefault(e => e.Name.LocalName.Equals("HZ010", StringComparison.OrdinalIgnoreCase)) ?? "").Trim();
            var pos = ((string?)hz.Elements().FirstOrDefault(e => e.Name.LocalName.Equals("HZ001", StringComparison.OrdinalIgnoreCase)) ?? "").Trim();
            var q1 = ((string?)hz.Elements().FirstOrDefault(e => e.Name.LocalName.Equals("HZ003", StringComparison.OrdinalIgnoreCase)) ?? "").Trim();
            var q2 = ((string?)hz.Elements().FirstOrDefault(e => e.Name.LocalName.Equals("HZ004", StringComparison.OrdinalIgnoreCase)) ?? "").Trim();

            double? anfang = null;
            if (double.TryParse(pos.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out var posValue))
                anfang = posValue;

            var finding = new VsaFinding
            {
                KanalSchadencode = code,
                Raw = string.IsNullOrWhiteSpace(text) ? null : text,
                SchadenlageAnfang = anfang,
                Quantifizierung1 = string.IsNullOrWhiteSpace(q1) ? null : q1,
                Quantifizierung2 = string.IsNullOrWhiteSpace(q2) ? null : q2
            };

            findings.Add(finding);
        }

        return findings;
    }

    private static string FormatPrimaryDamages(IReadOnlyList<VsaFinding> findings)
    {
        var lines = new List<string>();
        foreach (var f in findings.Where(x => !string.IsNullOrWhiteSpace(x.KanalSchadencode)))
        {
            var line = f.KanalSchadencode.Trim();
            if (f.SchadenlageAnfang.HasValue)
                line += $" @{f.SchadenlageAnfang.Value.ToString(CultureInfo.InvariantCulture)}m";
            if (!string.IsNullOrWhiteSpace(f.Raw))
                line += $" ({f.Raw})";
            lines.Add(line);
        }

        if (lines.Count == 0)
            return string.Empty;

        return string.Join("\n", lines);
    }

    private static string PickValue(Dictionary<string, string> map, IEnumerable<string> keyHints, Func<string, bool> validator)
    {
        foreach (var hint in keyHints)
        {
            foreach (var kv in map)
            {
                if (!kv.Key.Contains(hint, StringComparison.OrdinalIgnoreCase))
                    continue;

                var raw = kv.Value?.Trim() ?? string.Empty;
                if (validator(raw))
                    return raw;
            }
        }

        return string.Empty;
    }

    private static string NormalizeKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return string.Empty;

        var sb = new StringBuilder(key.Length);
        foreach (var ch in key)
        {
            if (char.IsLetterOrDigit(ch))
                sb.Append(char.ToLowerInvariant(ch));
        }
        return sb.ToString();
    }

    private static bool TryDumpMdbRows(string mdbPath, out List<Dictionary<string, string>> rows, out string? error)
    {
        rows = new List<Dictionary<string, string>>();
        error = null;

        var tempScript = Path.Combine(Path.GetTempPath(), $"mdb_dump_{Guid.NewGuid():N}.ps1");
        var tempJson = Path.Combine(Path.GetTempPath(), $"mdb_dump_{Guid.NewGuid():N}.json");

        try
        {
            var script = """
param(
    [Parameter(Mandatory=$true)][string]$MdbPath,
    [Parameter(Mandatory=$true)][string]$OutPath
)
$ErrorActionPreference = "Stop"

function Open-Db([string]$provider, [string]$path) {
    $cs = "Provider=$provider;Data Source=$path;Persist Security Info=False;"
    $conn = New-Object System.Data.OleDb.OleDbConnection($cs)
    $conn.Open()
    return $conn
}

$conn = $null
try {
    try {
        $conn = Open-Db -provider "Microsoft.ACE.OLEDB.12.0" -path $MdbPath
    } catch {
        $conn = Open-Db -provider "Microsoft.Jet.OLEDB.4.0" -path $MdbPath
    }

    $schema = $conn.GetOleDbSchemaTable([System.Data.OleDb.OleDbSchemaGuid]::Tables, $null)
    $tables = @($schema | Where-Object { $_.TABLE_TYPE -eq "TABLE" } | ForEach-Object { [string]$_.TABLE_NAME })

    $result = New-Object System.Collections.Generic.List[object]

    foreach ($table in $tables) {
        try {
            $cmd = $conn.CreateCommand()
            $cmd.CommandText = "SELECT * FROM [$table]"
            $adapter = New-Object System.Data.OleDb.OleDbDataAdapter($cmd)
            $dt = New-Object System.Data.DataTable
            [void]$adapter.Fill($dt)

            foreach ($row in $dt.Rows) {
                $values = @{}
                foreach ($col in $dt.Columns) {
                    $name = [string]$col.ColumnName
                    $val = $row[$name]
                    if ($null -eq $val -or $val -is [System.DBNull]) {
                        $values[$name] = ""
                    } else {
                        $values[$name] = [string]$val
                    }
                }

                $result.Add([PSCustomObject]@{
                    table = $table
                    row = $values
                })
            }
        } catch {
            # keep going: one broken table should not stop import
        }
    }

    $result | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $OutPath -Encoding UTF8
}
finally {
    if ($conn -ne $null) { $conn.Close() }
}
""";

            File.WriteAllText(tempScript, script, new UTF8Encoding(false));

            var psi = new ProcessStartInfo
            {
                FileName = "powershell",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{tempScript}\" -MdbPath \"{mdbPath}\" -OutPath \"{tempJson}\"",
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process is null)
            {
                error = "PowerShell konnte nicht gestartet werden.";
                return false;
            }

            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();
            if (!process.WaitForExit(120000))
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                error = "MDB-Import timeout nach 120 Sekunden.";
                return false;
            }

            if (process.ExitCode != 0)
            {
                error = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
                return false;
            }

            if (!File.Exists(tempJson))
            {
                error = "MDB-Ausgabe konnte nicht erstellt werden.";
                return false;
            }

            var json = File.ReadAllText(tempJson);
            if (string.IsNullOrWhiteSpace(json))
                return true;

            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in doc.RootElement.EnumerateArray())
                    TryAppendJsonRow(item, rows);
            }
            else if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                // ConvertTo-Json emits an object (not array) for a single row.
                TryAppendJsonRow(doc.RootElement, rows);
            }

            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
        finally
        {
            try { if (File.Exists(tempScript)) File.Delete(tempScript); } catch { }
            try { if (File.Exists(tempJson)) File.Delete(tempJson); } catch { }
        }
    }

    private sealed record ImportEntry(
        string Holding,
        string? Date,
        string? Length,
        string? Dn,
        string? Material,
        string? Direction,
        string? Remarks,
        string? Link,
        IReadOnlyList<VsaFinding> Findings);

    private sealed record WinCanSection(
        string SectionId,
        string StartNode,
        string EndNode,
        string Length,
        string Material,
        string Direction,
        string Date);

    private static void TryAppendJsonRow(JsonElement item, List<Dictionary<string, string>> rows)
    {
        if (!item.TryGetProperty("row", out var rowElement) || rowElement.ValueKind != JsonValueKind.Object)
            return;

        var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (item.TryGetProperty("table", out var table))
            row["__table"] = table.GetString() ?? "";

        foreach (var p in rowElement.EnumerateObject())
            row[p.Name] = p.Value.GetString() ?? "";

        rows.Add(row);
    }
}
