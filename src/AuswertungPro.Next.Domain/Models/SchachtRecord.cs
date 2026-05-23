namespace AuswertungPro.Next.Domain.Models;

public sealed class SchachtRecord : System.ComponentModel.INotifyPropertyChanged
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Dictionary<string, string> Fields { get; set; } = new(StringComparer.Ordinal);

    // Protokolldokument (Beobachtungen pro Bauteil).
    public AuswertungPro.Next.Domain.Protocol.ProtocolDocument? Protocol { get; set; }

    /// <summary>
    /// Punktgeometrie des Schachts in LV95. null = keine Lage bekannt.
    /// Phase 1 (Geometrie-Fundament 2026-05).
    /// </summary>
    public AuswertungPro.Next.Domain.Geometry.SchachtLage? Lage { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedAtUtc { get; set; } = DateTime.UtcNow;

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

    public string GetFieldValue(string fieldName)
        => Fields.TryGetValue(fieldName, out var v) ? v ?? "" : "";

    public void SetFieldValue(string fieldName, string? value)
    {
        value ??= "";
        Fields[fieldName] = value;
        ModifiedAtUtc = DateTime.UtcNow;

        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Fields)));
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs($"Fields[{fieldName}]"));
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(ModifiedAtUtc)));
    }
}
