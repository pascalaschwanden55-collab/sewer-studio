using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Xml.Linq;

using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Infrastructure.Import.Xtf;

// M150MdbImportHelper WinCan-Observations: Liest SO_T-Zeilen aus dem WinCan-
// Viewer-MDB und haengt sie als ProtocolEntries an matchende HaltungRecords.
// Inkl. SI_T -> S_T Mapping fuer Sektion-zu-Beobachtung-Zuordnung,
// XML-Variante fuer WinCan-Viewer-XML-Exporte und JSON-Row-Helper.
// Aus dem Hauptdatei extrahiert (Slice 18a).
internal static partial class M150MdbImportHelper
{
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
