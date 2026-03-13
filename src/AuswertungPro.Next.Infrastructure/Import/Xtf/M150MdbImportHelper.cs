using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Infrastructure.Media;

namespace AuswertungPro.Next.Infrastructure.Import.Xtf;

internal static class M150MdbImportHelper
{
    private static readonly Regex HoldingRx = new(@"(?<!\d)((?:\d{3,}|\d{1,3}(?:\.\d+)+)\s*[-/]\s*(?:\d{3,}|\d{1,3}(?:\.\d+)+))(?!\d)", RegexOptions.Compiled);
    private static readonly Regex PointRx = new(@"^[A-Za-z0-9][A-Za-z0-9._-]*$", RegexOptions.Compiled);
    private static readonly Regex DateRx = new(@"(\d{2}[./-]\d{2}[./-]\d{2,4}|\d{4}-\d{2}-\d{2})", RegexOptions.Compiled);
    private static readonly Regex GuidFragmentRx = new(@"\{?[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-", RegexOptions.Compiled);

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
        var entries = new List<ImportEntry>();
        XDocument? winCanDoc = null;

        try
        {
            var doc = XDocument.Load(path, LoadOptions.PreserveWhitespace);

            // WinCan Viewer XML: Root=NewDataSet with S_T children → parse as WinCan, not M150
            if (IsWinCanViewerXml(doc))
            {
                entries.AddRange(ExtractEntriesFromWinCanXml(doc, warnings));
                winCanDoc = doc;
            }
            else
            {
                entries.AddRange(ExtractEntriesFromXml(doc));
            }
        }
        catch (Exception ex)
        {
            warnings.Add($"M150 XML konnte nicht direkt gelesen werden: {ex.Message}");
        }

        if (entries.Count == 0)
        {
            var text = File.ReadAllText(path, Encoding.UTF8);
            entries.AddRange(ExtractEntriesFromRawText(text));
        }

        var records = BuildRecords(entries);

        // Attach SO_T observations as ProtocolEntries for WinCan Viewer XML
        if (winCanDoc is not null)
            AttachWinCanObservationsFromXml(winCanDoc, records, warnings);

        return records;
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
        var isWinCanViewer = false;
        var winCanViewerEntries = ExtractEntriesFromWinCanViewerRows(rows, warnings);
        if (winCanViewerEntries.Count > 0)
        {
            entries.AddRange(winCanViewerEntries);
            isWinCanViewer = true;
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

        // Parse SO_T observations and attach as ProtocolEntries when WinCan Viewer format detected
        if (isWinCanViewer)
            AttachWinCanObservationsFromRows(rows, records, warnings);

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
                Date: GetRowValue(row, "S_CreationDate"),
                PipeWidth: Coalesce(GetRowValue(row, "S_PipeWidth"), GetRowValue(row, "S_PipeHeight")));
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

            var dn = NormalizeNumberText(section.PipeWidth);

            entries.Add(new ImportEntry(
                NormalizeHolding(holding),
                date,
                NormalizeNumberText(section.Length),
                string.IsNullOrWhiteSpace(dn) ? null : dn,
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

                var dnFallback = NormalizeNumberText(section.PipeWidth);

                entries.Add(new ImportEntry(
                    NormalizeHolding(holding),
                    TryNormalizeDate(section.Date),
                    NormalizeNumberText(section.Length),
                    string.IsNullOrWhiteSpace(dnFallback) ? null : dnFallback,
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

    private static bool IsWinCanViewerXml(XDocument doc)
    {
        var root = doc.Root;
        if (root is null)
            return false;
        if (!root.Name.LocalName.Equals("NewDataSet", StringComparison.OrdinalIgnoreCase))
            return false;
        return root.Elements().Any(e => e.Name.LocalName.Equals("S_T", StringComparison.OrdinalIgnoreCase));
    }

    private static List<ImportEntry> ExtractEntriesFromWinCanXml(XDocument doc, List<string> warnings)
    {
        var root = doc.Root!;
        var sections = new Dictionary<string, WinCanSection>(StringComparer.OrdinalIgnoreCase);

        foreach (var sNode in root.Elements().Where(e => e.Name.LocalName.Equals("S_T", StringComparison.OrdinalIgnoreCase)))
        {
            var sectionId = XVal(sNode, "S_ID");
            if (string.IsNullOrWhiteSpace(sectionId))
                continue;

            sections[sectionId] = new WinCanSection(
                SectionId: sectionId,
                StartNode: XVal(sNode, "S_StartNode"),
                EndNode: XVal(sNode, "S_EndNode"),
                Length: XVal(sNode, "S_Sectionlength"),
                Material: XVal(sNode, "S_PipeMaterial"),
                Direction: XVal(sNode, "S_SectionFlow"),
                Date: XVal(sNode, "S_CreationDate"),
                PipeWidth: XVal(sNode, "S_PipeWidth"));
        }

        if (sections.Count == 0)
            return new List<ImportEntry>();

        // Map inspection → section
        var inspToSection = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var inspDates = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var entries = new List<ImportEntry>();
        foreach (var siNode in root.Elements().Where(e => e.Name.LocalName.Equals("SI_T", StringComparison.OrdinalIgnoreCase)))
        {
            var inspId = XVal(siNode, "SI_ID");
            var sectionId = XVal(siNode, "SI_Section_ID");
            if (string.IsNullOrWhiteSpace(sectionId) || !sections.TryGetValue(sectionId, out var section))
                continue;

            if (!string.IsNullOrWhiteSpace(inspId))
            {
                inspToSection[inspId] = sectionId;
                var idate = Coalesce(XVal(siNode, "SI_InspDate"), XVal(siNode, "SI_InspectionDate"), section.Date);
                if (!string.IsNullOrWhiteSpace(idate))
                    inspDates[inspId] = idate;
            }

            var dirRaw = Coalesce(XVal(siNode, "SI_InspectionDir"), section.Direction);
            var holding = BuildHoldingFromWinCanSection(section.StartNode, section.EndNode, dirRaw);
            if (!IsHoldingId(holding))
                continue;

            var date = TryNormalizeDate(Coalesce(
                XVal(siNode, "SI_InspDate"),
                XVal(siNode, "SI_InspectionDate"),
                section.Date));

            var direction = NormalizeWinCanDirection(dirRaw);
            var dn = NormalizeNumberText(section.PipeWidth);

            entries.Add(new ImportEntry(
                NormalizeHolding(holding),
                date,
                NormalizeNumberText(section.Length),
                string.IsNullOrWhiteSpace(dn) ? null : dn,
                NullIfWhite(section.Material),
                NullIfWhite(direction),
                null,
                null,
                Array.Empty<VsaFinding>()));
        }

        if (entries.Count > 0)
            warnings.Add($"WinCan Viewer XML erkannt: {entries.Count} Haltungen aus S_T/SI_T.");

        return entries;
    }

    private static string XVal(XElement parent, string childName)
    {
        var child = parent.Elements().FirstOrDefault(e => e.Name.LocalName.Equals(childName, StringComparison.OrdinalIgnoreCase));
        return (child?.Value ?? "").Trim();
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
                .Where(v => !GuidFragmentRx.IsMatch(v))
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
                rec.SetFieldValue("Rohrmaterial", NormalizeMaterialValue(merged.Material), FieldSource.Xtf, userEdited: false);
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

        var lower = v.ToLowerInvariant();

        // Spezifische Muster zuerst pruefen (z.B. "von unten nach oben")
        if (lower.Contains("unten") && lower.Contains("oben") && lower.IndexOf("unten") < lower.IndexOf("oben"))
            return "unten -> oben";
        if (lower.Contains("oben") && lower.Contains("unten") && lower.IndexOf("oben") < lower.IndexOf("unten"))
            return "oben -> unten";

        // DWA-M 150 Codes
        if (lower is "d" or "down" or "1")
            return "oben -> unten";
        if (lower is "u" or "up" or "2")
            return "unten -> oben";

        // Einfache Schluesselwoerter
        if (lower.Contains("oben") || lower.StartsWith("von"))
            return "oben -> unten";
        if (lower.Contains("unten") || lower.StartsWith("nach"))
            return "unten -> oben";

        return v;
    }

    private static bool LooksLikeVideoLink(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var v = value.Trim().Trim('"', '\'');
        var ext = Path.GetExtension(v).ToLowerInvariant();
        if (MediaFileTypes.HasVideoExtension(ext))
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

    private static string NormalizeMaterialValue(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return raw;

        // Take only the first line – WinCan DB sometimes appends cleaning info
        // like "Zement\nGereinigt    Ja" into the material field.
        var t = raw.Split('\n')[0].Trim();
        // Strip trailing non-material tokens (e.g. "Gereinigt Ja")
        t = Regex.Replace(t, @"(?i)\s*(gereinigt|nicht\s*gereinigt|verschmutzt)\s*(ja|nein)?\s*$", "").Trim();

        return string.IsNullOrWhiteSpace(t) ? raw.Trim() : t;
    }

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
        return XtfPrimaryDamageFormatter.FormatLines(findings);
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
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            psi.ArgumentList.Add("-NoProfile");
            psi.ArgumentList.Add("-ExecutionPolicy");
            psi.ArgumentList.Add("Bypass");
            psi.ArgumentList.Add("-File");
            psi.ArgumentList.Add(tempScript);
            psi.ArgumentList.Add("-MdbPath");
            psi.ArgumentList.Add(mdbPath);
            psi.ArgumentList.Add("-OutPath");
            psi.ArgumentList.Add(tempJson);

            using var process = Process.Start(psi);
            if (process is null)
            {
                error = "PowerShell konnte nicht gestartet werden.";
                return false;
            }

            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(120000))
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                error = "MDB-Import timeout nach 120 Sekunden.";
                return false;
            }

            var stdout = stdoutTask.GetAwaiter().GetResult();
            var stderr = stderrTask.GetAwaiter().GetResult();

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

    private sealed record WinCanObs(
        string InspectionId,
        string OpCode,
        string Observation,
        double? Distance,
        int Counter);

    /// <summary>
    /// Parses SO_T rows from WinCan Viewer MDB and attaches them as ProtocolEntries to matching HaltungRecords.
    /// Uses SI_T → S_T mapping to determine which holding each observation belongs to.
    /// </summary>
    private static void AttachWinCanObservationsFromRows(
        IReadOnlyList<Dictionary<string, string>> rows,
        List<HaltungRecord> records,
        List<string> warnings)
    {
        // Build section lookup: S_ID → (StartNode, EndNode, Direction)
        var sections = new Dictionary<string, WinCanSection>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            if (!GetTableName(row).Equals("S_T", StringComparison.OrdinalIgnoreCase))
                continue;
            var sid = GetRowValue(row, "S_ID");
            if (string.IsNullOrWhiteSpace(sid))
                continue;
            sections[sid] = new WinCanSection(sid,
                GetRowValue(row, "S_StartNode"), GetRowValue(row, "S_EndNode"),
                GetRowValue(row, "S_Sectionlength"), GetRowValue(row, "S_PipeMaterial"),
                GetRowValue(row, "S_SectionFlow"), GetRowValue(row, "S_CreationDate"),
                Coalesce(GetRowValue(row, "S_PipeWidth"), GetRowValue(row, "S_PipeHeight")));
        }

        // Build inspection → holding mapping
        var inspToHolding = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            if (!GetTableName(row).Equals("SI_T", StringComparison.OrdinalIgnoreCase))
                continue;
            var inspId = GetRowValue(row, "SI_ID");
            var sectionId = GetRowValue(row, "SI_Section_ID");
            if (string.IsNullOrWhiteSpace(inspId) || string.IsNullOrWhiteSpace(sectionId))
                continue;
            if (!sections.TryGetValue(sectionId, out var section))
                continue;
            var dirRaw = Coalesce(GetRowValue(row, "SI_InspectionDir"), section.Direction);
            var holding = BuildHoldingFromWinCanSection(section.StartNode, section.EndNode, dirRaw);
            if (IsHoldingId(holding))
                inspToHolding[inspId] = NormalizeHolding(holding);
        }

        // Parse SO_T observations
        var obsByHolding = new Dictionary<string, List<WinCanObs>>(StringComparer.OrdinalIgnoreCase);
        var unmatchedCount = 0;

        foreach (var row in rows)
        {
            if (!GetTableName(row).Equals("SO_T", StringComparison.OrdinalIgnoreCase))
                continue;

            var inspId = GetRowValue(row, "SO_Inspection_ID");
            var opCode = GetRowValue(row, "SO_OpCode");
            var remark = GetRowValue(row, "SO_Remark");
            var distStr = GetRowValue(row, "SO_Distance");
            int.TryParse(GetRowValue(row, "SO_Counter"), out var counter);
            double? dist = null;
            if (!string.IsNullOrWhiteSpace(distStr) &&
                double.TryParse(distStr.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
                dist = d;

            if (string.IsNullOrWhiteSpace(opCode) && string.IsNullOrWhiteSpace(remark))
                continue;

            // Try to link via inspection ID
            string? holdingKey = null;
            if (!string.IsNullOrWhiteSpace(inspId) && inspToHolding.TryGetValue(inspId, out var h))
                holdingKey = h;

            if (holdingKey is null)
            {
                unmatchedCount++;
                continue;
            }

            if (!obsByHolding.TryGetValue(holdingKey, out var list))
            {
                list = new List<WinCanObs>();
                obsByHolding[holdingKey] = list;
            }
            list.Add(new WinCanObs(inspId, opCode, remark, dist, counter));
        }

        if (unmatchedCount > 0)
            warnings.Add($"SO_T: {unmatchedCount} Beobachtungen ohne Inspektions-Zuordnung uebersprungen.");

        // Attach protocol entries to matching records
        var attached = 0;
        foreach (var rec in records)
        {
            var key = NormalizeHolding(rec.GetFieldValue("Haltungsname") ?? "");
            if (!obsByHolding.TryGetValue(key, out var obsList) || obsList.Count == 0)
                continue;

            var entries = obsList.OrderBy(o => o.Counter).Select(o => new ProtocolEntry
            {
                Code = o.OpCode,
                Beschreibung = o.Observation,
                MeterStart = o.Distance,
                Source = ProtocolEntrySource.Imported
            }).ToList();

            rec.Protocol = new ProtocolDocument
            {
                HaltungId = key,
                Original = new ProtocolRevision
                {
                    Comment = "Import (WinCan Viewer MDB)",
                    Entries = entries
                }
            };
            rec.Protocol.Current = new ProtocolRevision
            {
                Comment = "Arbeitskopie",
                Entries = entries.Select(e => new ProtocolEntry
                {
                    Code = e.Code,
                    Beschreibung = e.Beschreibung,
                    MeterStart = e.MeterStart,
                    Source = e.Source
                }).ToList()
            };
            attached++;
        }

        if (attached > 0)
            warnings.Add($"WinCan Viewer: {attached} Haltungen mit Protokolleintraegen aus SO_T.");
    }

    /// <summary>
    /// Attaches SO_T observations from WinCan XML to records. Same logic as MDB variant but using XElements.
    /// </summary>
    private static void AttachWinCanObservationsFromXml(
        XDocument doc,
        List<HaltungRecord> records,
        List<string> warnings)
    {
        var root = doc.Root;
        if (root is null) return;

        // Build section lookup
        var sections = new Dictionary<string, WinCanSection>(StringComparer.OrdinalIgnoreCase);
        foreach (var sNode in root.Elements().Where(e => e.Name.LocalName.Equals("S_T", StringComparison.OrdinalIgnoreCase)))
        {
            var sid = XVal(sNode, "S_ID");
            if (string.IsNullOrWhiteSpace(sid)) continue;
            sections[sid] = new WinCanSection(sid,
                XVal(sNode, "S_StartNode"), XVal(sNode, "S_EndNode"),
                XVal(sNode, "S_Sectionlength"), XVal(sNode, "S_PipeMaterial"),
                XVal(sNode, "S_SectionFlow"), XVal(sNode, "S_CreationDate"),
                XVal(sNode, "S_PipeWidth"));
        }

        // Build inspection → holding mapping
        var inspToHolding = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var siNode in root.Elements().Where(e => e.Name.LocalName.Equals("SI_T", StringComparison.OrdinalIgnoreCase)))
        {
            var inspId = XVal(siNode, "SI_ID");
            var sectionId = XVal(siNode, "SI_Section_ID");
            if (string.IsNullOrWhiteSpace(inspId) || string.IsNullOrWhiteSpace(sectionId)) continue;
            if (!sections.TryGetValue(sectionId, out var section)) continue;
            var dirRaw = Coalesce(XVal(siNode, "SI_InspectionDir"), section.Direction);
            var holding = BuildHoldingFromWinCanSection(section.StartNode, section.EndNode, dirRaw);
            if (IsHoldingId(holding))
                inspToHolding[inspId] = NormalizeHolding(holding);
        }

        // Parse SO_T observations
        var obsByHolding = new Dictionary<string, List<WinCanObs>>(StringComparer.OrdinalIgnoreCase);
        var unmatchedCount = 0;

        foreach (var soNode in root.Elements().Where(e => e.Name.LocalName.Equals("SO_T", StringComparison.OrdinalIgnoreCase)))
        {
            var inspId = XVal(soNode, "SO_Inspection_ID");
            var opCode = XVal(soNode, "SO_OpCode");
            var remark = XVal(soNode, "SO_Remark");
            var distStr = XVal(soNode, "SO_Distance");
            int.TryParse(XVal(soNode, "SO_Counter"), out var counter);
            double? dist = null;
            if (!string.IsNullOrWhiteSpace(distStr) &&
                double.TryParse(distStr.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
                dist = d;

            if (string.IsNullOrWhiteSpace(opCode) && string.IsNullOrWhiteSpace(remark))
                continue;

            string? holdingKey = null;
            if (!string.IsNullOrWhiteSpace(inspId) && inspToHolding.TryGetValue(inspId, out var h))
                holdingKey = h;

            if (holdingKey is null)
            {
                unmatchedCount++;
                continue;
            }

            if (!obsByHolding.TryGetValue(holdingKey, out var list))
            {
                list = new List<WinCanObs>();
                obsByHolding[holdingKey] = list;
            }
            list.Add(new WinCanObs(inspId, opCode, remark, dist, counter));
        }

        if (unmatchedCount > 0)
            warnings.Add($"SO_T: {unmatchedCount} Beobachtungen ohne Inspektions-Zuordnung uebersprungen.");

        var attached = 0;
        foreach (var rec in records)
        {
            var key = NormalizeHolding(rec.GetFieldValue("Haltungsname") ?? "");
            if (!obsByHolding.TryGetValue(key, out var obsList) || obsList.Count == 0)
                continue;

            var entries = obsList.OrderBy(o => o.Counter).Select(o => new ProtocolEntry
            {
                Code = o.OpCode,
                Beschreibung = o.Observation,
                MeterStart = o.Distance,
                Source = ProtocolEntrySource.Imported
            }).ToList();

            rec.Protocol = new ProtocolDocument
            {
                HaltungId = key,
                Original = new ProtocolRevision
                {
                    Comment = "Import (WinCan Viewer XML)",
                    Entries = entries
                }
            };
            rec.Protocol.Current = new ProtocolRevision
            {
                Comment = "Arbeitskopie",
                Entries = entries.Select(e => new ProtocolEntry
                {
                    Code = e.Code,
                    Beschreibung = e.Beschreibung,
                    MeterStart = e.MeterStart,
                    Source = e.Source
                }).ToList()
            };
            attached++;
        }

        if (attached > 0)
            warnings.Add($"WinCan Viewer XML: {attached} Haltungen mit Protokolleintraegen aus SO_T.");
    }

    private sealed record WinCanSection(
        string SectionId,
        string StartNode,
        string EndNode,
        string Length,
        string Material,
        string Direction,
        string Date,
        string PipeWidth = "");

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
