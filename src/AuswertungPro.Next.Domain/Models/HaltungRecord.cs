namespace AuswertungPro.Next.Domain.Models;

// ARCH-H1 (Audit 2026-04-23): INotifyPropertyChanged in Domain ist Tech-Debt.
// Blockiert Headless-Use (Sidecar/CLI). Zukuenftiger Migrationspfad:
//   1. POCO-Record `HaltungRecord` (ohne INotifyPropertyChanged) in Domain.
//   2. `HaltungRecordViewModel : ObservableObject` in UI als Bindings-Wrapper.
//   3. ViewModels in BuilderPageViewModel/DataPageViewModel halten eigene
//      ObservableCollection<HaltungRecordViewModel>, gespiegelt aus
//      Project.Data (POCO-List).
// Aufwand: 2-3 Sessions, hoch-invasiv (>20 XAML-Bindings, >10 ViewModels
// betroffen). Entkoppelt erst, wenn Headless-CLI/Sidecar-Use einen
// Business-Treiber bekommt.
public sealed class HaltungRecord : System.ComponentModel.INotifyPropertyChanged
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Feldwerte (als Strings wie in der PS-Version).
    /// </summary>
    public Dictionary<string, string> Fields { get; set; } = new(StringComparer.Ordinal);

    public Dictionary<string, FieldMetadata> FieldMeta { get; set; } = new(StringComparer.Ordinal);

    // Strukturierte VSA-Feststellungen (aus XTF), fuer Berechnung
    public List<VsaFinding> VsaFindings { get; set; } = new();

    // Optionaler Protokolleintrag fuer Code-Picker/Parametrisierung.
    public AuswertungPro.Next.Domain.Protocol.ProtocolEntry? ProtocolEntry { get; set; }

    // Protokolldokument (mehrere Beobachtungen + Historie).
    public AuswertungPro.Next.Domain.Protocol.ProtocolDocument? Protocol { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedAtUtc { get; set; } = DateTime.UtcNow;
    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

    public HaltungRecord()
    {
        // Initialisiere alle Felder + Metadata
        foreach (var fieldName in FieldCatalog.ColumnOrder)
        {
            Fields[fieldName] = "";
            FieldMeta[fieldName] = new FieldMetadata
            {
                FieldName = fieldName,
                Source = FieldSource.Manual,
                UserEdited = false,
                LastUpdatedUtc = DateTime.UtcNow
            };
        }
    }

    /// <summary>
    /// Erstellt eine tiefe Kopie (Fields + FieldMeta + VsaFindings).
    /// Wird von MergeEngine.DryRun genutzt um das Original nicht zu veraendern.
    /// </summary>
    public HaltungRecord DeepClone()
    {
        var clone = new HaltungRecord { Id = Id, CreatedAtUtc = CreatedAtUtc, ModifiedAtUtc = ModifiedAtUtc };
        clone.Fields = new Dictionary<string, string>(Fields, StringComparer.Ordinal);
        clone.FieldMeta = new Dictionary<string, FieldMetadata>(FieldMeta.Count, StringComparer.Ordinal);
        foreach (var (key, meta) in FieldMeta)
        {
            clone.FieldMeta[key] = new FieldMetadata
            {
                FieldName = meta.FieldName,
                Source = meta.Source,
                UserEdited = meta.UserEdited,
                LastUpdatedUtc = meta.LastUpdatedUtc
            };
        }
        clone.VsaFindings = VsaFindings.ToList();
        return clone;
    }

    public string GetFieldValue(string fieldName)
        => Fields.TryGetValue(fieldName, out var v) ? v ?? "" : "";

    public void SetFieldValue(string fieldName, string? value, FieldSource source, bool userEdited)
    {
        value ??= "";

        // Record-Level Setter: keep this as a simple assignment.
        // Import/UI priority decisions are handled by MergeEngine; we only protect user-edited values here.
        if (FieldMeta.TryGetValue(fieldName, out var existingMeta) && existingMeta.UserEdited && !userEdited)
            return;

        Fields[fieldName] = value;

        if (!FieldMeta.TryGetValue(fieldName, out var meta))
        {
            meta = new FieldMetadata { FieldName = fieldName };
            FieldMeta[fieldName] = meta;
        }

        meta.Source = source;
        meta.UserEdited = userEdited;
        meta.LastUpdatedUtc = DateTime.UtcNow;

        ModifiedAtUtc = DateTime.UtcNow;

        // Notify bindings immediately so DataGrid updates without extra clicks.
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Fields)));
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs($"Fields[{fieldName}]"));
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(ModifiedAtUtc)));
    }
}
