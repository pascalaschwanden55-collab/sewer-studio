using System.Text.Json.Nodes;
using System.Collections.ObjectModel;
using System.Linq;

namespace AuswertungPro.Next.Domain.Models;

public sealed class Project
{
    public int Version { get; set; } = 2;
    public string Name { get; set; } = "Neues Projekt";
    public string Description { get; set; } = "";
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedAtUtc { get; set; } = DateTime.UtcNow;
    public string AppVersion { get; set; } = FieldCatalog.AppVersion;

    /// <summary>
    /// Projekt-Metadaten wie in der PS-Version.
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new(StringComparer.Ordinal);

    public System.Collections.ObjectModel.ObservableCollection<HaltungRecord> Data { get; set; } = new();
    public System.Collections.ObjectModel.ObservableCollection<SchachtRecord> SchaechteData { get; set; } = new();

    /// <summary>
    /// Beliebige Import-Historie (wird 1:1 aus JSON übernommen).
    /// </summary>
    public List<JsonObject> ImportHistory { get; set; } = new();

    /// <summary>
    /// Konflikte (wird 1:1 aus JSON übernommen).
    /// </summary>
    public List<JsonObject> Conflicts { get; set; } = new();

    public bool Dirty { get; set; }

    public Project()
    {
        EnsureMetadataDefaults();
    }

    public void EnsureMetadataDefaults()
    {
        string[] keys =
        {
            "Zone",
            "Gemeinde",
            "Strasse",
            "FirmaName",
            "FirmaAdresse",
            "FirmaTelefon",
            "FirmaEmail",
            "Bearbeiter",
            "Auftraggeber",
            "AuftragNr",
            "InspektionsDatum",
            "Sanieren",
            "Eigentuemer"
        };

        foreach (var k in keys)
        {
            if (!Metadata.ContainsKey(k))
            {
                if (k == "Sanieren") Metadata[k] = "Nein";
                else if (k == "Eigentuemer") Metadata[k] = "Privat";
                else Metadata[k] = "";
            }
        }

        // Validierung für Eigentuemer
        var eigentuemerWerte = new[] { "AWU", "Privat", "Gemeinde", "Kanton", "Bund" };
        if (!eigentuemerWerte.Contains(Metadata["Eigentuemer"]))
            Metadata["Eigentuemer"] = "Privat";

        // Validierung für Sanieren
        if (Metadata["Sanieren"] != "Ja" && Metadata["Sanieren"] != "Nein")
            Metadata["Sanieren"] = "Nein";

        EnsureRecordDefaults();
    }

    private void EnsureRecordDefaults()
    {
        foreach (var rec in Data)
        {
            foreach (var fieldName in FieldCatalog.ColumnOrder)
            {
                if (!rec.Fields.ContainsKey(fieldName))
                    rec.Fields[fieldName] = "";

                if (!rec.FieldMeta.ContainsKey(fieldName))
                {
                    rec.FieldMeta[fieldName] = new FieldMetadata
                    {
                        FieldName = fieldName,
                        Source = FieldSource.Manual,
                        UserEdited = false,
                        LastUpdatedUtc = DateTime.UtcNow
                    };
                }
            }

            if (rec.VsaFindings is null)
                rec.VsaFindings = new List<VsaFinding>();

            if (rec.Protocol is null && rec.ProtocolEntry is not null)
            {
                var legacyEntry = rec.ProtocolEntry;
                rec.Protocol = new AuswertungPro.Next.Domain.Protocol.ProtocolDocument
                {
                    HaltungId = rec.GetFieldValue("Haltungsname") ?? "",
                    Original = new AuswertungPro.Next.Domain.Protocol.ProtocolRevision
                    {
                        Comment = "Import (Legacy ProtocolEntry)",
                        Entries = new List<AuswertungPro.Next.Domain.Protocol.ProtocolEntry>
                        {
                            CloneLegacyProtocolEntry(legacyEntry)
                        }
                    }
                };
                rec.Protocol.Current = new AuswertungPro.Next.Domain.Protocol.ProtocolRevision
                {
                    Comment = "Arbeitskopie",
                    Entries = rec.Protocol.Original.Entries.Select(CloneLegacyProtocolEntry).ToList()
                };
                // Keep ProtocolEntry for roundtrip compatibility with legacy JSON contracts.
            }

            if (rec.Fields.TryGetValue("Fliessrichtung", out var oldVal) && !string.IsNullOrWhiteSpace(oldVal))
            {
                var existing = rec.Fields.TryGetValue("Inspektionsrichtung", out var newVal) ? newVal : "";
                if (string.IsNullOrWhiteSpace(existing))
                {
                    rec.Fields["Inspektionsrichtung"] = oldVal;
                    if (rec.FieldMeta.TryGetValue("Fliessrichtung", out var oldMeta))
                    {
                        rec.FieldMeta["Inspektionsrichtung"] = new FieldMetadata
                        {
                            FieldName = "Inspektionsrichtung",
                            Source = oldMeta.Source,
                            UserEdited = oldMeta.UserEdited,
                            LastUpdatedUtc = oldMeta.LastUpdatedUtc,
                            Conflict = oldMeta.Conflict
                        };
                    }
                }
            }
        }
    }

    public HaltungRecord CreateNewRecord()
    {
        var record = new HaltungRecord();

        // Auto-generate NR (wie PS)
        var maxNr = 0;
        foreach (var rec in Data)
        {
            if (int.TryParse(rec.GetFieldValue("NR"), out var nr) && nr > maxNr)
                maxNr = nr;
        }

        record.SetFieldValue("NR", (maxNr + 1).ToString(), FieldSource.Manual, userEdited: false);
        return record;
    }

    public void AddRecord(HaltungRecord record)
    {
        Data.Add(record);
        ModifiedAtUtc = DateTime.UtcNow;
        Dirty = true;
    }

    public bool RemoveRecord(Guid recordId)
    {
        var idx = Data.Select((r, i) => new { r, i }).FirstOrDefault(x => x.r.Id == recordId)?.i ?? -1;
        if (idx < 0) return false;
        Data.RemoveAt(idx);
        ModifiedAtUtc = DateTime.UtcNow;
        Dirty = true;
        return true;
    }

    public HaltungRecord? GetRecord(Guid recordId)
        => Data.FirstOrDefault(r => r.Id == recordId);

    private static AuswertungPro.Next.Domain.Protocol.ProtocolEntry CloneLegacyProtocolEntry(
        AuswertungPro.Next.Domain.Protocol.ProtocolEntry source)
    {
        return new AuswertungPro.Next.Domain.Protocol.ProtocolEntry
        {
            EntryId = source.EntryId,
            Code = source.Code,
            Beschreibung = source.Beschreibung,
            MeterStart = source.MeterStart,
            MeterEnd = source.MeterEnd,
            IsStreckenschaden = source.IsStreckenschaden,
            Mpeg = source.Mpeg,
            Zeit = source.Zeit,
            FotoPaths = new List<string>(source.FotoPaths),
            Source = source.Source,
            IsDeleted = source.IsDeleted,
            CodeMeta = source.CodeMeta is null
                ? null
                : new AuswertungPro.Next.Domain.Protocol.ProtocolEntryCodeMeta
                {
                    Code = source.CodeMeta.Code,
                    Parameters = new Dictionary<string, string>(source.CodeMeta.Parameters, StringComparer.OrdinalIgnoreCase),
                    Severity = source.CodeMeta.Severity,
                    Count = source.CodeMeta.Count,
                    Notes = source.CodeMeta.Notes,
                    UpdatedAt = source.CodeMeta.UpdatedAt
                },
            Ai = source.Ai is null
                ? null
                : new AuswertungPro.Next.Domain.Protocol.ProtocolEntryAiMeta
                {
                    SuggestedCode = source.Ai.SuggestedCode,
                    Confidence = source.Ai.Confidence,
                    Reason = source.Ai.Reason,
                    Flags = new List<string>(source.Ai.Flags),
                    Accepted = source.Ai.Accepted,
                    FinalCode = source.Ai.FinalCode,
                    SuggestedAt = source.Ai.SuggestedAt
                }
        };
    }
}
