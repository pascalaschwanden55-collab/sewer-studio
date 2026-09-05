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

    /// <summary>
    /// Die Kennungen, unter denen GEONIS dieses Bauteil und seinen Objektverbund fuehrt.
    /// Null, solange sie nie aus dem Kataster uebernommen wurden. Setzen ueber
    /// <see cref="SetzeGeonisKennungen"/>, damit die Aenderung gemeldet wird.
    /// </summary>
    public GeonisKennungen? Geonis { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedAtUtc { get; set; } = DateTime.UtcNow;

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Uebernimmt die GEONIS-Kennungen und meldet die Aenderung wie ein Feldwert,
    /// damit Speichern-Status und Bindungen sie sehen.
    /// </summary>
    public void SetzeGeonisKennungen(GeonisKennungen? kennungen)
    {
        Geonis = kennungen;
        ModifiedAtUtc = DateTime.UtcNow;
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Geonis)));
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(ModifiedAtUtc)));
    }

    public string GetFieldValue(string fieldName)
        => Fields.TryGetValue(fieldName, out var v) ? v ?? "" : "";

    /// <summary>Meldet, ob dieses Feld ausdruecklich von Hand gesetzt wurde.</summary>
    public bool IsUserEdited(string fieldName)
        => FieldMeta.TryGetValue(fieldName, out var meta) && meta.UserEdited;

    /// <summary>
    /// Kompatibilitaetsweg fuer bestehende Aufrufer (Durchnummerieren, Import).
    /// Schreibt mit Herkunft "Manual", laesst aber ein von Hand gesetztes Feld
    /// unveraendert und senkt keine vorhandene Handmarkierung ab. Damit ueberlebt
    /// eine Korrektur auch einen versehentlich wiederholten Import.
    /// </summary>
    public FeldSchreibErgebnis SetFieldValue(string fieldName, string? value)
    {
        // Schutz wie bei HaltungRecord: ein von Hand gesetzter Wert wird nie
        // ueberschrieben - auch nicht durch einen versehentlich wiederholten Import.
        // Wer bewusst eine Handeingabe setzt oder ersetzt (Umbenennen, Massnahme
        // leeren), ruft die Ueberladung mit userEdited: true.
        if (IsUserEdited(fieldName))
            return FeldSchreibErgebnis.HandwertGeschuetzt;

        return WriteField(fieldName, value, FieldSource.Manual, userEdited: null);
    }

    /// <summary>
    /// Zieht einen Wert technisch nach, ohne Herkunft oder Handmarkierung zu
    /// veraendern. Gedacht fuer Dateipfade nach einem Umbenennen: der alte Pfad
    /// zeigt ins Leere, also muss auch ein handgesetzter Wert mit - er darf dadurch
    /// aber weder zur Handeingabe erklaert noch von einer werden.
    /// </summary>
    public FeldSchreibErgebnis SetFieldValueTechnical(string fieldName, string? value)
        => WriteField(fieldName, value, FieldMeta.TryGetValue(fieldName, out var meta)
            ? meta.Source
            : FieldSource.Manual, userEdited: null);

    /// <summary>
    /// Schreibt mit ausdruecklicher Herkunft. Ein automatischer Schreibvorgang
    /// (<paramref name="userEdited"/> = false) laesst ein bereits handgesetztes Feld
    /// unveraendert — dieselbe Regel wie bei <see cref="HaltungRecord"/>.
    /// </summary>
    public FeldSchreibErgebnis SetFieldValue(string fieldName, string? value, FieldSource source, bool userEdited)
    {
        if (!userEdited && IsUserEdited(fieldName))
            return FeldSchreibErgebnis.HandwertGeschuetzt;

        return WriteField(fieldName, value, source, userEdited);
    }

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

        WriteField(fieldName, value, source, userEdited: false);
        return true;
    }

    private FeldSchreibErgebnis WriteField(string fieldName, string? value, FieldSource source, bool? userEdited)
    {
        value ??= "";
        var unveraendert = Fields.TryGetValue(fieldName, out var bisher)
                           && string.Equals(bisher ?? "", value, StringComparison.Ordinal);
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

        return unveraendert ? FeldSchreibErgebnis.Unveraendert : FeldSchreibErgebnis.Geschrieben;
    }
}
