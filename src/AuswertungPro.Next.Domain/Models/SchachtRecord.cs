namespace AuswertungPro.Next.Domain.Models;

public sealed class SchachtRecord : System.ComponentModel.INotifyPropertyChanged
{
    public Guid Id { get; set; } = Guid.NewGuid();

    private bool _bearbeitungErledigt;

    /// <summary>Persoenliche Arbeitsmarkierung, unabhaengig vom Sanierungsstatus und von KI-Pruefungen.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool BearbeitungErledigt
    {
        get => _bearbeitungErledigt;
        set
        {
            if (_bearbeitungErledigt == value) return;
            _bearbeitungErledigt = value;
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(BearbeitungErledigt)));
        }
    }
    public Dictionary<string, string> Fields { get; set; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Herkunft je Feld — wie bei <see cref="HaltungRecord"/>. Additiv: Altprojekte ohne
    /// diesen Abschnitt laden mit leerer Karte, dort gilt jedes Feld als nicht handgesetzt.
    /// Wird gebraucht, damit ein spaeterer Export sagen kann, was der Mensch geaendert hat,
    /// und damit automatische Schreiber eine Handeingabe nicht ueberholen.
    /// </summary>
    public Dictionary<string, FieldMetadata> FieldMeta { get; set; } = new(StringComparer.Ordinal);

    private AuswertungPro.Next.Domain.Protocol.ProtocolDocument? _protocol;

    /// <summary>
    /// Protokolldokument (Beobachtungen pro Bauteil). Der Setter meldet sich wie ein Feldwert
    /// (<c>PropertyChanged(nameof(Protocol))</c>), damit ein In-Place-Ersatz — zum Beispiel ein
    /// neu eingelesenes Protokoll, das den Datensatz sonst referenzgleich laesst — von
    /// Abonnenten bemerkt wird. Ohne Abonnenten ist das harmlos, auch bei der
    /// JSON-Deserialisierung: Der Setter wird dabei ganz normal ueber Reflection aufgerufen,
    /// nur meldet noch niemand mit.
    ///
    /// Threadvertrag (Nova-Fixwelle F6): Die Meldung laeuft SYNCHRON auf dem Thread, der den
    /// Setter aufruft. Import, Stammdatennachlauf und Neueinlesen arbeiten im Hintergrund —
    /// ein UI-Abonnent muss die Meldung deshalb selbst auf den Dispatcher schieben
    /// (Muster: <c>SchachtUebersichtPanel.OnRecordPropertyChanged</c>). Der Setter marshallt
    /// bewusst nicht selbst: Die Domaene kennt kein WPF.
    /// </summary>
    public AuswertungPro.Next.Domain.Protocol.ProtocolDocument? Protocol
    {
        get => _protocol;
        set
        {
            _protocol = value;
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Protocol)));
        }
    }

    /// <summary>
    /// Die Kennungen, unter denen GEONIS dieses Bauteil und seinen Objektverbund fuehrt.
    /// Null, solange sie nie aus dem Kataster uebernommen wurden. Setzen ueber
    /// <see cref="SetzeGeonisKennungen"/>, damit die Aenderung gemeldet wird.
    /// </summary>
    public GeonisKennungen? Geonis { get; set; }

    /// <summary>GlobalID aus einem eindeutig passenden WebGIS-Suchtreffer; keine OBJECTID oder XTF-Kennung.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? WebGisGlobalId { get; set; }

    public void SetzeWebGisGlobalId(string globalId)
    {
        if (WebGisGlobalId == globalId) return;
        WebGisGlobalId = globalId;
        ModifiedAtUtc = DateTime.UtcNow;
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(WebGisGlobalId)));
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(ModifiedAtUtc)));
    }

    private List<SchachtAnschluss>? _anschluesse;

    /// <summary>
    /// Die Anschluesse des Schachts (Anschlusstabelle des Protokolls, SchachtPro). Additiv:
    /// Altprojekte ohne diesen Abschnitt laden mit <c>null</c>. Ersetzen nur ueber
    /// <see cref="SetzeAnschluesse"/>, damit die Aenderung gemeldet wird. Kein Feld, keine
    /// Tabellenspalte, kein Export — nur die Schachtgrafik und die Anzeige lesen sie.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public List<SchachtAnschluss>? Anschluesse
    {
        get => _anschluesse;
        set => _anschluesse = value;
    }

    /// <summary>Ersetzt die Anschlussliste und meldet die Aenderung wie einen Feldwert. Leer heisst null.</summary>
    public void SetzeAnschluesse(List<SchachtAnschluss>? anschluesse)
    {
        _anschluesse = anschluesse is { Count: > 0 } ? anschluesse : null;
        ModifiedAtUtc = DateTime.UtcNow;
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Anschluesse)));
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(ModifiedAtUtc)));
    }

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
    /// Bewusst leer: von Hand gesetzt (<c>UserEdited</c>) und leer. Kein Import und kein
    /// Abgleich fuellt ein solches Feld (Entscheid Pascal 02.10.2026, E3).
    /// </summary>
    public bool IstBewusstLeer(string fieldName)
        => IsUserEdited(fieldName) && string.IsNullOrWhiteSpace(GetFieldValue(fieldName));

    /// <summary>
    /// Kompatibilitaetsweg fuer bestehende Aufrufer (Durchnummerieren, Import).
    /// Schreibt mit Herkunft "Manual", laesst aber ein von Hand gesetztes Feld
    /// unveraendert und senkt keine vorhandene Handmarkierung ab. Damit ueberlebt
    /// eine Korrektur auch einen versehentlich wiederholten Import.
    /// </summary>
    public FeldSchreibErgebnis SetFieldValue(string fieldName, string? value)
    {
        value = AlsWebGisBegriff(fieldName, value);
        // Schutz wie bei HaltungRecord: ein von Hand gesetzter Wert wird nie
        // ueberschrieben - auch nicht durch einen versehentlich wiederholten Import.
        // Wer bewusst eine Handeingabe setzt oder ersetzt (Umbenennen, Massnahme
        // leeren), ruft die Ueberladung mit userEdited: true.
        if (IsUserEdited(fieldName))
            return FeldSchreibErgebnis.HandwertGeschuetzt;

        if (KatasterFeldschutz.Pruefe(fieldName, FieldMeta.GetValueOrDefault(fieldName), GetFieldValue(fieldName), value, FieldSource.Manual, false))
            return FeldSchreibErgebnis.KatasterwertGeschuetzt;

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
        value = AlsWebGisBegriff(fieldName, value);
        if (!userEdited && IsUserEdited(fieldName))
            return FeldSchreibErgebnis.HandwertGeschuetzt;

        if (KatasterFeldschutz.Pruefe(fieldName, FieldMeta.GetValueOrDefault(fieldName), GetFieldValue(fieldName), value, source, userEdited))
            return FeldSchreibErgebnis.KatasterwertGeschuetzt;

        return WriteField(fieldName, value, source, userEdited);
    }

    /// <summary>
    /// Fuellt ein LEERES Feld aus einer automatischen Quelle und setzt dabei die
    /// Herkunft neu. Liefert <c>false</c>, wenn das Feld Inhalt hat — dann wird
    /// nichts angefasst.
    ///
    /// Seit 02.10.2026 (Entscheid Pascal, E3): Ein bewusst leeres Feld
    /// (<see cref="IstBewusstLeer"/>: von Hand gesetzt und leer) wird NICHT gefuellt —
    /// «Handwert, auch bewusst leer» hat Vorrang. Wer im Raster eine Zelle leert, will sie
    /// leer haben. Bis dahin entschied hier die Leere, nicht die Markierung (Fall Schacht
    /// 33461, 03.09.2026); das widersprach der Regel in CLAUDE.md. Frei wird das Feld durch
    /// eine neue Handeingabe oder (Haltungsseite) «Spalte leeren», das die Handmarke wegnimmt.
    ///
    /// Weil der gefuellte Wert nicht von Hand kommt, bleibt <c>UserEdited</c> dabei
    /// <c>false</c> — sonst ginge er spaeter als Handeingabe in die revidierte XTF.
    /// </summary>
    public bool FuelleLeeresFeld(string fieldName, string? value, FieldSource source)
    {
        if (!string.IsNullOrWhiteSpace(GetFieldValue(fieldName)))
            return false;

        // Handwert, auch bewusst leer, hat Vorrang (Entscheid Pascal 02.10.2026, E3).
        if (IstBewusstLeer(fieldName))
            return false;

        if (string.IsNullOrWhiteSpace(value))
            return false;

        WriteField(fieldName, AlsWebGisBegriff(fieldName, value), source, userEdited: false);
        return true;
    }

    /// <summary>
    /// Seit 23.09.2026 (Schritt A): Felder, die ins WebGIS gehen, speichern den WebGIS-Begriff — schon
    /// beim Schreiben, damit Import und Katasterschutz nie Normbegriff gegen WebGIS-Begriff vergleichen.
    /// Die Funktion nur beim Normschacht; ein Spezialbauwerk behaelt seine Normfunktion.
    /// </summary>
    private string? AlsWebGisBegriff(string fieldName, string? value)
    {
        // Audit A14: Schachtfelder heissen nach der Vorlagen-Kopfzeile («STATUS», «Status »). Erst den
        // Katalognamen finden, sonst bliebe «in_Betrieb» stehen und ginge an der Senderegel vorbei.
        var feld = WebGisFeldname(fieldName);
        if (feld is null)
            return value;
        if (!string.Equals(feld, WebGisBegriffe.SchachtFunktion, StringComparison.Ordinal))
            return WebGisBegriffe.Fuer(true, feld) is { } liste ? liste.Normalisieren(value) : value;
        var art = GetFieldValue(SchachtFeldnamen.Feld(this, FieldKeys.ShaftStructureType));
        return AbwasserbauwerkVokabular.Klasse(art, value) == "Normschacht"
            ? WebGisBegriffe.Normalisieren(true, feld, value)
            : value;
    }

    /// <summary>Der Katalogname des WebGIS-Felds zu einer Vorlagenschreibweise, oder null.</summary>
    private static string? WebGisFeldname(string fieldName)
    {
        var gefaltet = SchachtFeldnamen.Falte(fieldName);
        if (gefaltet.Length == 0)
            return null;
        if (string.Equals(gefaltet, SchachtFeldnamen.Falte(WebGisBegriffe.SchachtFunktion), StringComparison.Ordinal))
            return WebGisBegriffe.SchachtFunktion;
        foreach (var feld in WebGisBegriffe.SchachtFelder)
            if (string.Equals(gefaltet, SchachtFeldnamen.Falte(feld), StringComparison.Ordinal))
                return feld;
        return null;
    }

    /// <summary>
    /// Rueckgaengig/Wiederholen (Optik Aufgabe 16): setzt Wert UND Herkunftsdaten eines Feldes
    /// zeichengenau auf einen frueheren Zustand zurueck — ohne WebGIS-Umwandlung, ohne neue
    /// Handmarke, mit dem alten Zeitstempel. Gleiche Regel wie <see cref="HaltungRecord.StelleFeldzustandWiederHer"/>.
    /// </summary>
    public void StelleFeldzustandWiederHer(string fieldName, string? value, FieldMetadata? meta)
    {
        FeldzustandWiederherstellung.Anwende(Fields, FieldMeta, fieldName, value, meta);
        ModifiedAtUtc = DateTime.UtcNow;
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Fields)));
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs($"Fields[{fieldName}]"));
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(ModifiedAtUtc)));
    }

    private FeldSchreibErgebnis WriteField(string fieldName, string? value, FieldSource source, bool? userEdited)
    {
        // Nova-Fixwelle 2b, Runde 2: Eine virtuelle Tabellenspalte ist kein Feld und darf
        // nie in Fields landen. Genau hier ist am 07.09.2026 "Nova_Protokoll" in jeden
        // Schachtdatensatz gelaufen (siehe VirtuelleSpalte). Der Schutz sitzt am
        // gemeinsamen Schreibweg, damit ihn keine der vier Ueberladungen umgehen kann.
        VirtuelleSpalte.WeiseAb(fieldName, nameof(fieldName));

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
