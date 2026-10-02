using System.Text.Json;
using System.Text.Json.Serialization;

namespace AuswertungPro.Next.Domain.Models;

public sealed class HaltungRecord : System.ComponentModel.INotifyPropertyChanged
{
    public Guid Id { get; set; } = Guid.NewGuid();

    private bool _bearbeitungErledigt;

    /// <summary>Persoenliche Arbeitsmarkierung, unabhaengig vom Sanierungsstatus und von KI-Pruefungen.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
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

    /// <summary>
    /// Die Kennungen, unter denen GEONIS dieses Bauteil und seinen Objektverbund fuehrt.
    /// Null, solange sie nie aus dem Kataster uebernommen wurden. Setzen ueber
    /// <see cref="SetzeGeonisKennungen"/>, damit die Aenderung gemeldet wird.
    /// </summary>
    public GeonisKennungen? Geonis { get; set; }

    /// <summary>GlobalID aus einem eindeutig passenden WebGIS-Suchtreffer; keine OBJECTID oder XTF-Kennung.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? WebGisGlobalId { get; set; }

    public void SetzeWebGisGlobalId(string globalId)
    {
        if (WebGisGlobalId == globalId) return;
        WebGisGlobalId = globalId;
        ModifiedAtUtc = DateTime.UtcNow;
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(WebGisGlobalId)));
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(ModifiedAtUtc)));
    }

    /// <summary>
    /// Die Laufnummer, unter der die Importquelle diese Haltung fuehrt — bei WinCan der
    /// <c>OBJ_Key</c> (<c>H66</c>). Der Haltungsname bleibt das Schachtpaar
    /// (<c>60248-60247</c>); diese Nummer ist nur ein Herkunftsbeleg.
    ///
    /// Gebraucht wird sie fuer Begleitprotokolle der Sanierung: Dichtheitspruefung und
    /// Aushaerteprotokoll nennen ihre Haltung ausschliesslich so. Sie ist bewusst KEIN
    /// Feld — sie gehoert in keine Tabelle, keinen Export und keine XTF; sie ist nur
    /// innerhalb eines Quellprojekts eindeutig.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ImportBezeichnung { get; set; }

    // Optionaler Protokolleintrag fuer Code-Picker/Parametrisierung.
    public AuswertungPro.Next.Domain.Protocol.ProtocolEntry? ProtocolEntry { get; set; }

    private AuswertungPro.Next.Domain.Protocol.ProtocolDocument? _protocol;

    /// <summary>
    /// Protokolldokument (mehrere Beobachtungen + Historie). Der Setter meldet sich wie ein
    /// Feldwert (<c>PropertyChanged(nameof(Protocol))</c>), damit ein In-Place-Ersatz — ein neu
    /// eingelesenes Protokoll oder ein KI-Lauf, der den Datensatz sonst referenzgleich laesst —
    /// von Abonnenten bemerkt wird. Nova-Etappe 2b: Die Statusspalten KI und Pruefung der
    /// Haltungstabelle rechnen aus dem Protokoll und blieben ohne diese Meldung stehen.
    /// Gleiche Regel wie bei <see cref="SchachtRecord.Protocol"/>.
    ///
    /// Threadvertrag: Die Meldung laeuft SYNCHRON auf dem Thread, der den Setter aufruft.
    /// Import, Stammdatennachlauf und Neueinlesen arbeiten im Hintergrund — ein UI-Abonnent
    /// muss die Meldung deshalb selbst auf den Dispatcher schieben. Der Setter marshallt
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
    /// Bewusst leer: von Hand gesetzt (<c>UserEdited</c>) und leer. Kein Import und kein
    /// Abgleich fuellt ein solches Feld (Entscheid Pascal 02.10.2026, E3).
    /// </summary>
    public bool IstBewusstLeer(string fieldName)
        => FieldMeta.TryGetValue(fieldName, out var meta) && meta.UserEdited
           && string.IsNullOrWhiteSpace(GetFieldValue(fieldName));

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
        VirtuelleSpalte.WeiseAb(fieldName, nameof(fieldName));

        if (!string.IsNullOrWhiteSpace(GetFieldValue(fieldName)))
            return false;

        // Handwert, auch bewusst leer, hat Vorrang (Entscheid Pascal 02.10.2026, E3).
        if (IstBewusstLeer(fieldName))
            return false;

        if (string.IsNullOrWhiteSpace(value))
            return false;

        // Wie SetFieldValue: Felder, die ins WebGIS gehen, speichern den WebGIS-Begriff (23.09.2026).
        Fields[fieldName] = WebGisBegriffe.Normalisieren(schacht: false, fieldName, value);

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
        // Nova-Fixwelle 2b, Runde 2: Eine virtuelle Tabellenspalte ist kein Feld und darf
        // nie in Fields landen (siehe VirtuelleSpalte).
        VirtuelleSpalte.WeiseAb(fieldName, nameof(fieldName));

        // Seit 23.09.2026 (Schritt A): Felder, die ins WebGIS gehen, speichern den WebGIS-Begriff —
        // schon beim Schreiben, damit Import, Nachschlagen und Vergleiche nie mit dem Normbegriff
        // arbeiten. Ohne eindeutigen Begriff bleibt der Text, wie er kommt.
        value = WebGisBegriffe.Normalisieren(schacht: false, fieldName, value);

        // Record-Level Setter: keep this as a simple assignment.
        // Import/UI priority decisions are handled by MergeEngine; we only protect user-edited values here.
        if (FieldMeta.TryGetValue(fieldName, out var existingMeta) && existingMeta.UserEdited && !userEdited)
            return;

        if (KatasterFeldschutz.Pruefe(fieldName, existingMeta, GetFieldValue(fieldName), value, source, userEdited)) return;

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

    public void RaiseAllFieldsChanged()
        => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Fields)));

    /// <summary>
    /// Rueckgaengig/Wiederholen (Optik Aufgabe 16): setzt Wert UND Herkunftsdaten eines Feldes
    /// zeichengenau auf einen frueheren Zustand dieses Datensatzes zurueck. Keine WebGIS-Umwandlung,
    /// keine neue Handmarke, kein Zeitstempel von jetzt: Wiederhergestellt wird genau, was vorher
    /// dastand (<c>null</c> heisst: Feld bzw. Metadaten gab es nicht). Den Schutz der normalen
    /// Schreibwege ersetzt der Verlauf durch seine Vorbedingung - er ruft dies nur auf, wenn das Feld
    /// noch exakt den eigenen Eintrag traegt, und nie mit einem Wert, den der Datensatz nicht hatte.
    /// </summary>
    public void StelleFeldzustandWiederHer(string fieldName, string? value, FieldMetadata? meta)
    {
        FeldzustandWiederherstellung.Anwende(Fields, FieldMeta, fieldName, value, meta);
        ModifiedAtUtc = DateTime.UtcNow;
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Fields)));
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs($"Fields[{fieldName}]"));
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(ModifiedAtUtc)));
    }
}
