namespace AuswertungPro.Next.Domain.Models;

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

    private int _laufendeNr;

    /// <summary>
    /// Transiente Anzeige-Laufnummer (1..N) in der aktuellen Listenreihenfolge.
    /// NICHT persistiert (kein Fachdatum) und ueberschreibt insbesondere NICHT das Feld "NR" —
    /// wird vom UI-ViewModel bei jeder Reihenfolge-/Bestandsaenderung neu gesetzt.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public int LaufendeNr
    {
        get => _laufendeNr;
        set
        {
            if (_laufendeNr == value)
                return;
            _laufendeNr = value;
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(LaufendeNr)));
        }
    }

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
