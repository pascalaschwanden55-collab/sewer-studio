using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Infrastructure.Media;

namespace AuswertungPro.Next.Infrastructure.Import.Xtf;

// M150MdbImportHelper WinCan-Viewer-spezifische Extraktion: SI_T-/SO_T-Tabellen
// in MDBs, Section-Header-Aufloesung (Start/End-Knoten + Direction +
// Inversion-Logik), XML-Variante. Aus dem Hauptdatei extrahiert (Slice 18b).
internal static partial class M150MdbImportHelper
{
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
}
