namespace AuswertungPro.Next.Domain.Models;

public sealed class SchachtRecord : System.ComponentModel.INotifyPropertyChanged
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Dictionary<string, string> Fields { get; set; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Herkunft je Feld — wie bei <see cref="HaltungRecord"/>. Additiv: Altprojekte ohne
    /// diesen Abschnitt laden mit leerer Karte, dort gilt jedes Feld als nicht handgesetzt.
    /// Wird gebraucht, damit ein spaeterer Export sagen kann, was der Mensch geaendert hat,
    /// und damit automatische Schreiber eine Handeingabe nicht ueberholen.
    /// </summary>
    public Dictionary<string, FieldMetadata> FieldMeta { get; set; } = new(StringComparer.Ordinal);

    // Protokolldokument (Beobachtungen pro Bauteil).
    public AuswertungPro.Next.Domain.Protocol.ProtocolDocument? Protocol { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedAtUtc { get; set; } = DateTime.UtcNow;

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

    public string GetFieldValue(string fieldName)
        => Fields.TryGetValue(fieldName, out var v) ? v ?? "" : "";

    /// <summary>Meldet, ob dieses Feld ausdruecklich von Hand gesetzt wurde.</summary>
    public bool IsUserEdited(string fieldName)
        => FieldMeta.TryGetValue(fieldName, out var meta) && meta.UserEdited;

    /// <summary>
    /// Kompatibilitaetsweg fuer alle bestehenden Aufrufer (Umbenennen, Durchnummerieren,
    /// Massnahmen, Import). Schreibt immer und wird nie gesperrt, senkt aber auch keine
    /// vorhandene Handmarkierung ab — sonst wuerde ein technischer Schreibvorgang den
    /// Schutz stillschweigend aufheben.
    /// </summary>
    public void SetFieldValue(string fieldName, string? value)
        => WriteField(fieldName, value, FieldSource.Manual, userEdited: null);

    /// <summary>
    /// Schreibt mit ausdruecklicher Herkunft. Ein automatischer Schreibvorgang
    /// (<paramref name="userEdited"/> = false) laesst ein bereits handgesetztes Feld
    /// unveraendert — dieselbe Regel wie bei <see cref="HaltungRecord"/>.
    /// </summary>
    public void SetFieldValue(string fieldName, string? value, FieldSource source, bool userEdited)
    {
        if (!userEdited && IsUserEdited(fieldName))
            return;

        WriteField(fieldName, value, source, userEdited);
    }

    private void WriteField(string fieldName, string? value, FieldSource source, bool? userEdited)
    {
        value ??= "";
        Fields[fieldName] = value;

        if (!FieldMeta.TryGetValue(fieldName, out var meta))
        {
            meta = new FieldMetadata { FieldName = fieldName };
            FieldMeta[fieldName] = meta;
        }

        meta.Source = source;
        if (userEdited is bool flag)
            meta.UserEdited = flag;
        meta.LastUpdatedUtc = DateTime.UtcNow;

        ModifiedAtUtc = DateTime.UtcNow;

        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Fields)));
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs($"Fields[{fieldName}]"));
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(ModifiedAtUtc)));
    }
}
