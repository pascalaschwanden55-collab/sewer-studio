using System.Text.Json;
using System.Text.Json.Serialization;

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

    /// <summary>
    /// Herkunft aus der XTF-Quelle. Wird beim Import gesetzt und beim spaeteren
    /// Erzeugen einer revidierten XTF als Ankerangabe verwendet. Null bei Haltungen,
    /// die nicht aus einer XTF stammen oder vor dem 2026-08-13 eingelesen wurden.
    /// </summary>
    public XtfHerkunft? XtfHerkunft { get; set; }

    // Optionaler Protokolleintrag fuer Code-Picker/Parametrisierung.
    public AuswertungPro.Next.Domain.Protocol.ProtocolEntry? ProtocolEntry { get; set; }

    // Protokolldokument (mehrere Beobachtungen + Historie).
    public AuswertungPro.Next.Domain.Protocol.ProtocolDocument? Protocol { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Unbekannte Felder bleiben bei einem Speichern-Roundtrip erhalten.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }

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

    public string GetFieldValue(string fieldName)
        => Fields.TryGetValue(fieldName, out var v) ? v ?? "" : "";

    /// <summary>
    /// Fuellt ein LEERES Feld aus einer automatischen Quelle und setzt dabei die
    /// Herkunft neu. Liefert <c>false</c>, wenn das Feld Inhalt hat — dann wird
    /// nichts angefasst.
    ///
    /// Warum es diesen Weg neben <c>SetFieldValue</c> braucht: Dort weist der
    /// Handwert-Schutz jeden automatischen Schreibvorgang auf ein Feld mit
    /// <c>UserEdited</c> ab. Diese Markierung bleibt aber stehen, wenn der
    /// Bearbeiter den Inhalt im Raster loescht — die Bindung schreibt direkt in
    /// <see cref="Fields"/> und laesst <see cref="FieldMeta"/> unberuehrt. Das Feld
    /// ist danach leer und trotzdem geschuetzt; ein Nachfuelllauf prallte
    /// stillschweigend daran ab (gemessen 2026-09-03 an Schacht 33461).
    ///
    /// An einem leeren Feld hat der Schutz keinen Gegenstand: Es gibt dort keine
    /// Arbeit zu bewahren. Die Leere entscheidet, nicht die alte Markierung. Weil
    /// der Wert nicht von Hand kommt, wird <c>UserEdited</c> dabei auf <c>false</c>
    /// gesetzt — sonst ginge er spaeter als Handeingabe in die revidierte XTF.
    /// </summary>
    public bool FuelleLeeresFeld(string fieldName, string? value, FieldSource source)
    {
        if (!string.IsNullOrWhiteSpace(GetFieldValue(fieldName)))
            return false;

        if (string.IsNullOrWhiteSpace(value))
            return false;

        Fields[fieldName] = value!;

        if (!FieldMeta.TryGetValue(fieldName, out var meta))
        {
            meta = new FieldMetadata { FieldName = fieldName };
            FieldMeta[fieldName] = meta;
        }

        meta.Source = source;
        meta.UserEdited = false;
        meta.LastUpdatedUtc = DateTime.UtcNow;
        ModifiedAtUtc = DateTime.UtcNow;

        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Fields)));
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs($"Fields[{fieldName}]"));
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(ModifiedAtUtc)));
        return true;
    }

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

    /// <summary>
    /// Erzwingt ein Neu-Lesen ALLER Feld-Bindungen (Tabelle und Haltungsansicht), ohne die
    /// Auflistung zu veraendern. Wird nach Sammel-Aenderungen genutzt, die nicht einzeln ueber
    /// <see cref="SetFieldValue"/> liefen. Bewusst KEIN Collection-Replace: der wuerde die
    /// virtualisierte Haltungsliste neu aufbauen und Scroll-Position samt Auswahl verwerfen.
    /// </summary>
    public void RaiseAllFieldsChanged()
        => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Fields)));
}
