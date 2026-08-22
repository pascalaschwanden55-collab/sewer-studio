using System.Collections.ObjectModel;
using System.Text.Json.Nodes;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Export;

/// <summary>
/// Erstellt eine kleine, unabhaengige Arbeitskopie fuer den Haltungs-Excel-Export.
/// Kostenfelder duerfen auf dieser Kopie nachgezogen werden, ohne das geoeffnete
/// Projekt oder dessen DataGrid-Bindungen zu veraendern.
/// </summary>
public static class HoldingExcelExportSnapshotFactory
{
    public static Project Create(Project source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return new Project
        {
            Version = source.Version,
            Name = source.Name,
            Description = source.Description,
            Id = source.Id,
            CreatedAtUtc = source.CreatedAtUtc,
            ModifiedAtUtc = source.ModifiedAtUtc,
            AppVersion = source.AppVersion,
            ExtensionData = source.ExtensionData is null
                ? null
                : new Dictionary<string, System.Text.Json.JsonElement>(source.ExtensionData),
            Metadata = new Dictionary<string, string>(source.Metadata, StringComparer.Ordinal),
            Data = new ObservableCollection<HaltungRecord>(source.Data.Select(CloneRecord)),
            LastCommittedImportTxId = source.LastCommittedImportTxId,
            Dirty = source.Dirty
        };
    }

    private static HaltungRecord CloneRecord(HaltungRecord source)
        => new()
        {
            Id = source.Id,
            Fields = new Dictionary<string, string>(source.Fields, StringComparer.Ordinal),
            FieldMeta = source.FieldMeta.ToDictionary(
                static pair => pair.Key,
                static pair => CloneFieldMetadata(pair.Value),
                StringComparer.Ordinal),
            CreatedAtUtc = source.CreatedAtUtc,
            ModifiedAtUtc = source.ModifiedAtUtc,
            ExtensionData = source.ExtensionData is null
                ? null
                : new Dictionary<string, System.Text.Json.JsonElement>(source.ExtensionData)
        };

    private static FieldMetadata CloneFieldMetadata(FieldMetadata source)
        => new()
        {
            FieldName = source.FieldName,
            Source = source.Source,
            UserEdited = source.UserEdited,
            LastUpdatedUtc = source.LastUpdatedUtc,
            Conflict = source.Conflict?.DeepClone() as JsonObject
        };
}
