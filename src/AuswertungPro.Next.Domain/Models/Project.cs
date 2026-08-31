using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
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
    /// Unbekannte Felder neuerer/anderer Programmstaende bleiben beim Laden und Speichern erhalten.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, System.Text.Json.JsonElement>? ExtensionData { get; set; }

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

    /// <summary>
    /// Laufzeit-Flag fuer ungespeicherte Aenderungen. NICHT serialisieren:
    /// sonst stuende im AutoSave-Betrieb "Dirty": true in der projekt.json und
    /// ein frisch geladenes Projekt gaelte sofort als geaendert (Fehlalarm beim Schliessen).
    /// </summary>
    [JsonIgnore]
    public bool Dirty { get; set; }

    /// <summary>
    /// TxId des zuletzt erfolgreich committeten Imports. Wird beim atomaren projekt.json-Save
    /// gesetzt und dient dem Import-Transaktions-Recovery als Commit-Beweis (Marker-TxId ==
    /// diese TxId ⇒ der Import wurde abgeschlossen). Fliesst NICHT in die Content-Signatur ein.
    /// </summary>
    public string? LastCommittedImportTxId { get; set; }

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

        // Eigentuemer: nur ein leeres Feld wird vorbelegt. Frueher stand hier
        // eine Whitelist der fuenf Kurzformen, die jeden anderen Wert durch
        // "Privat" ersetzte — auch einen beim Kanton nachgeschlagenen wie
        // "Abwasser Uri". Eine echte Angabe stillschweigend zu ersetzen ist
        // schlimmer, als sie unbekannt stehen zu lassen.
        if (string.IsNullOrWhiteSpace(Metadata["Eigentuemer"]))
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

            MigrateLegacyPipeMaterial(rec);

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

    /// <summary>
    /// Alte Rohrmaterial-Werte, die der SIA405-Import bis Juli 2026 erzeugt hat. Sie standen nie in
    /// der Auswahlliste des Feldes — dadurch zeigte das Programm sie als LEER an, obwohl der Wert
    /// gespeichert war (betraf rund ein Drittel aller Haltungen).
    /// Schluessel bewusst als Liste statt als Regel: Es sind genau die Werte, die in echten
    /// Projekten vorkommen. Neue Importe liefern sie dank XtfValueNormalizer gar nicht mehr.
    /// </summary>
    private static readonly Dictionary<string, string> LegacyPipeMaterialValues =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Kunststoff Polyvinilchlorid"] = "Polyvinylchlorid",
            ["Kunststoff Polyvinylchlorid"] = "Polyvinylchlorid",
            ["Kunststoff PVC"] = "Polyvinylchlorid",
            ["PVC Polyvinilchlorid"] = "Polyvinylchlorid",
            ["Kunststoff PE"] = "Polyethylen",
            ["Kunststoff PE-HD"] = "Hartpolyethylen",
            ["Kunststoff Polypropylen"] = "Polypropylen",
            ["Kunststoff Epoxydharz"] = "Epoxydharz",
            ["Guss Grauguss"] = "Guss"
        };

    /// <summary>
    /// Hebt alte, unwaehlbare Rohrmaterial-Werte auf die heutigen Auswahlwerte.
    /// Von Hand gesetzte Werte bleiben unangetastet — sie sind die Entscheidung des Nutzers.
    /// </summary>
    private static void MigrateLegacyPipeMaterial(HaltungRecord rec)
    {
        if (!rec.Fields.TryGetValue(FieldKeys.PipeMaterial, out var value) || string.IsNullOrWhiteSpace(value))
            return;

        if (rec.FieldMeta.TryGetValue(FieldKeys.PipeMaterial, out var meta) && meta.UserEdited)
            return;

        if (!LegacyPipeMaterialValues.TryGetValue(value.Trim(), out var current))
            return;

        rec.Fields[FieldKeys.PipeMaterial] = current;
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
        var name = record.GetFieldValue("Haltungsname");
        if (!string.IsNullOrWhiteSpace(name) && HasDuplicateHoldingName(name, record.Id))
            throw new InvalidOperationException($"Die Haltung '{name.Trim()}' existiert bereits im Projekt.");

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

    public bool HasDuplicateHoldingName(string? holdingName, Guid? exceptRecordId = null)
    {
        var normalized = holdingName?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            return false;

        return Data.Any(record =>
            (!exceptRecordId.HasValue || record.Id != exceptRecordId.Value)
            && string.Equals(
                record.GetFieldValue("Haltungsname").Trim(),
                normalized,
                StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Delegiert an <see cref="AuswertungPro.Next.Domain.Protocol.ProtocolEntryCloner.CloneLegacyProtocolEntry"/>.
    /// </summary>
    private static AuswertungPro.Next.Domain.Protocol.ProtocolEntry CloneLegacyProtocolEntry(
        AuswertungPro.Next.Domain.Protocol.ProtocolEntry source)
        => AuswertungPro.Next.Domain.Protocol.ProtocolEntryCloner.CloneLegacyProtocolEntry(source);
}
