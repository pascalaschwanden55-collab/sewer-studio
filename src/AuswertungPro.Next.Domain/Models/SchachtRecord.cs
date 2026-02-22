namespace AuswertungPro.Next.Domain.Models;

public sealed class SchachtRecord : System.ComponentModel.INotifyPropertyChanged
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Dictionary<string, string> Fields { get; set; } = new(StringComparer.Ordinal);
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
