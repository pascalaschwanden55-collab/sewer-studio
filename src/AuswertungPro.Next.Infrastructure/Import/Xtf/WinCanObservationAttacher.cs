using System.Globalization;
using System.Xml.Linq;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Infrastructure.Import.Common;

namespace AuswertungPro.Next.Infrastructure.Import.Xtf;

/// <summary>
/// Ordnet WinCan-Beobachtungen aus SO_T ueber Inspektion und Haltung zu.
/// Die Datenquelle kann eine Viewer-MDB oder eine Viewer-XML sein.
/// </summary>
internal static class WinCanObservationAttacher
{
    public static void AttachFromRows(
        IReadOnlyList<Dictionary<string, string>> rows,
        List<HaltungRecord> records,
        List<string> warnings)
    {
        var sections = SectionsFromRows(rows);

        var inspectionToHolding = BuildInspectionMap(
            rows.Where(row => TableName(row).Equals("SI_T", StringComparison.OrdinalIgnoreCase))
                .Select(row => new Inspection(
                    Value(row, "SI_ID"),
                    Value(row, "SI_Section_ID"),
                    Value(row, "SI_InspectionDir"))),
            sections);

        var observations = rows
            .Where(row => TableName(row).Equals("SO_T", StringComparison.OrdinalIgnoreCase))
            .Select(row => Observation(
                ErsterWert(row, "SO_Inspection_ID", "SO_Inspecs_ID"),
                Value(row, "SO_OpCode"),
                Value(row, "SO_Remark"),
                Value(row, "SO_Distance"),
                Value(row, "SO_Counter")));

        Attach(observations, inspectionToHolding, records, warnings, "WinCan Viewer", "Import (WinCan Viewer MDB)");
    }

    public static void AttachFromXml(
        XDocument doc,
        List<HaltungRecord> records,
        List<string> warnings)
    {
        var root = doc.Root;
        if (root is null)
            return;

        var sections = SectionsFromXml(root);

        var inspectionToHolding = BuildInspectionMap(
            root.Elements()
                .Where(node => node.Name.LocalName.Equals("SI_T", StringComparison.OrdinalIgnoreCase))
                .Select(node => new Inspection(
                    Value(node, "SI_ID"),
                    Value(node, "SI_Section_ID"),
                    Value(node, "SI_InspectionDir"))),
            sections);

        var observations = root.Elements()
            .Where(node => node.Name.LocalName.Equals("SO_T", StringComparison.OrdinalIgnoreCase))
            .Select(node => Observation(
                ErsterWert(node, "SO_Inspection_ID", "SO_Inspecs_ID"),
                Value(node, "SO_OpCode"),
                Value(node, "SO_Remark"),
                Value(node, "SO_Distance"),
                Value(node, "SO_Counter")));

        Attach(observations, inspectionToHolding, records, warnings, "WinCan Viewer XML", "Import (WinCan Viewer XML)");
    }

    /// <summary>
    /// Der Fremdschluessel auf die Inspektion heisst je nach WinCan-Generation anders:
    /// neuere Exporte schreiben "SO_Inspection_ID", die Viewer-MDB von 2017
    /// "SO_Inspecs_ID". Im Projekt Seelisberg blieben dadurch alle 192 Beobachtungen
    /// unzugeordnet - 30 Haltungen ohne einen einzigen Befund.
    /// </summary>
    private static string ErsterWert(Dictionary<string, string> row, params string[] spalten)
    {
        foreach (var spalte in spalten)
        {
            var wert = Value(row, spalte);
            if (!string.IsNullOrWhiteSpace(wert))
                return wert;
        }

        return string.Empty;
    }

    /// <inheritdoc cref="ErsterWert(Dictionary{string,string}, string[])"/>
    private static string ErsterWert(XElement node, params string[] spalten)
    {
        foreach (var spalte in spalten)
        {
            var wert = Value(node, spalte);
            if (!string.IsNullOrWhiteSpace(wert))
                return wert;
        }

        return string.Empty;
    }

    private static Dictionary<string, string> BuildInspectionMap(
        IEnumerable<Inspection> inspections,
        IReadOnlyDictionary<string, Section> sections)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var inspection in inspections)
        {
            if (string.IsNullOrWhiteSpace(inspection.Id)
                || string.IsNullOrWhiteSpace(inspection.SectionId)
                || !sections.TryGetValue(inspection.SectionId, out var section))
            {
                continue;
            }

            var direction = Coalesce(inspection.Direction, section.Direction);
            var holding = M150ValueExtractor.BuildHoldingFromWinCanSection(section.StartNode, section.EndNode, direction);
            if (M150ValueExtractor.IsHoldingId(holding))
                result[inspection.Id] = HoldingKeyNormalizer.Normalize(holding);
        }

        return result;
    }

    private static Dictionary<string, Section> SectionsFromRows(
        IReadOnlyList<Dictionary<string, string>> rows)
    {
        var sections = new Dictionary<string, Section>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows.Where(row => TableName(row).Equals("S_T", StringComparison.OrdinalIgnoreCase)))
        {
            var section = new Section(
                Value(row, "S_ID"),
                Value(row, "S_StartNode"),
                Value(row, "S_EndNode"),
                Value(row, "S_SectionFlow"));
            if (!string.IsNullOrWhiteSpace(section.Id))
                sections[section.Id] = section;
        }

        return sections;
    }

    private static Dictionary<string, Section> SectionsFromXml(XElement root)
    {
        var sections = new Dictionary<string, Section>(StringComparer.OrdinalIgnoreCase);
        foreach (var node in root.Elements()
                     .Where(node => node.Name.LocalName.Equals("S_T", StringComparison.OrdinalIgnoreCase)))
        {
            var section = new Section(
                Value(node, "S_ID"),
                Value(node, "S_StartNode"),
                Value(node, "S_EndNode"),
                Value(node, "S_SectionFlow"));
            if (!string.IsNullOrWhiteSpace(section.Id))
                sections[section.Id] = section;
        }

        return sections;
    }

    private static void Attach(
        IEnumerable<WinCanObservation> observations,
        IReadOnlyDictionary<string, string> inspectionToHolding,
        List<HaltungRecord> records,
        List<string> warnings,
        string warningPrefix,
        string revisionComment)
    {
        var byHolding = new Dictionary<string, List<WinCanObservation>>(StringComparer.OrdinalIgnoreCase);
        var unmatchedCount = 0;

        foreach (var observation in observations)
        {
            if (string.IsNullOrWhiteSpace(observation.OpCode)
                && string.IsNullOrWhiteSpace(observation.Description))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(observation.InspectionId)
                || !inspectionToHolding.TryGetValue(observation.InspectionId, out var holding))
            {
                unmatchedCount++;
                continue;
            }

            if (!byHolding.TryGetValue(holding, out var list))
            {
                list = new List<WinCanObservation>();
                byHolding[holding] = list;
            }

            list.Add(observation);
        }

        if (unmatchedCount > 0)
            warnings.Add($"SO_T: {unmatchedCount} Beobachtungen ohne Inspektions-Zuordnung uebersprungen.");

        var attachedCount = 0;
        foreach (var record in records)
        {
            var holding = HoldingKeyNormalizer.Normalize(record.GetFieldValue("Haltungsname") ?? string.Empty);
            if (!byHolding.TryGetValue(holding, out var observationsForHolding)
                || observationsForHolding.Count == 0)
            {
                continue;
            }

            var entries = observationsForHolding
                .OrderBy(observation => observation.Counter)
                .Select(ToProtocolEntry)
                .ToList();

            record.Protocol = new ProtocolDocument
            {
                HaltungId = holding,
                Original = new ProtocolRevision
                {
                    Comment = revisionComment,
                    Entries = entries
                }
            };
            record.Protocol.Current = new ProtocolRevision
            {
                Comment = "Arbeitskopie",
                Entries = entries.Select(Clone).ToList()
            };
            attachedCount++;
        }

        if (attachedCount > 0)
            warnings.Add($"{warningPrefix}: {attachedCount} Haltungen mit Protokolleintraegen aus SO_T.");
    }

    private static WinCanObservation Observation(
        string inspectionId,
        string opCode,
        string description,
        string distanceText,
        string counterText)
    {
        int.TryParse(counterText, out var counter);
        double? distance = null;
        if (!string.IsNullOrWhiteSpace(distanceText)
            && double.TryParse(distanceText.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
        {
            distance = parsed;
        }

        return new WinCanObservation(inspectionId, opCode, description, distance, counter);
    }

    private static ProtocolEntry ToProtocolEntry(WinCanObservation observation)
        => new()
        {
            Code = observation.OpCode,
            Beschreibung = observation.Description,
            MeterStart = observation.Distance,
            Source = ProtocolEntrySource.Imported
        };

    private static ProtocolEntry Clone(ProtocolEntry entry)
        => new()
        {
            Code = entry.Code,
            Beschreibung = entry.Beschreibung,
            MeterStart = entry.MeterStart,
            Source = entry.Source
        };

    private static string TableName(Dictionary<string, string> row)
        => Value(row, "__table");

    private static string Value(Dictionary<string, string> row, string key)
        => row.TryGetValue(key, out var value) ? (value ?? string.Empty).Trim() : string.Empty;

    private static string Value(XElement parent, string childName)
        => (parent.Elements()
            .FirstOrDefault(child => child.Name.LocalName.Equals(childName, StringComparison.OrdinalIgnoreCase))?
            .Value ?? string.Empty).Trim();

    private static string Coalesce(params string[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;

    private sealed record Section(string Id, string StartNode, string EndNode, string Direction);

    private sealed record Inspection(string Id, string SectionId, string Direction);

    private sealed record WinCanObservation(
        string InspectionId,
        string OpCode,
        string Description,
        double? Distance,
        int Counter);
}
