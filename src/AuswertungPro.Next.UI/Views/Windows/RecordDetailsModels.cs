using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AuswertungPro.Next.Application.Lookup;

namespace AuswertungPro.Next.UI.Views.Windows;

public enum RecordDetailHighlightKind
{
    None,
    Sanieren,
    AusgefuehrtDurch
}

public enum RecordDetailGroupKind
{
    Additional,
    MasterData,
    Condition,
    RenovationCosts,
    Documents
}

public sealed class RecordDetailItem : INotifyPropertyChanged
{
    private readonly Action<string> _commitValue;
    private string _value;

    public RecordDetailItem(
        string label,
        string value,
        Action<string> commitValue,
        bool isReadOnly = false,
        bool isMultiline = false,
        bool isCombo = false,
        bool allowFreeText = false,
        bool digitsOnly = false,
        IEnumerable<string>? options = null,
        ICommand? editOptionsCommand = null,
        ICommand? previewOptionsCommand = null,
        ICommand? resetOptionsCommand = null,
        ICommand? addOptionCommand = null,
        ICommand? removeOptionCommand = null,
        RecordDetailHighlightKind highlightKind = RecordDetailHighlightKind.None,
        ICommand? nachschlagenCommand = null,
        ICommand? strasseUebernehmenCommand = null)
    {
        Label = label;
        _value = value ?? string.Empty;
        Ausgangswert = _value;
        _commitValue = commitValue;
        IsReadOnly = isReadOnly;
        IsMultiline = isMultiline;
        IsCombo = isCombo;
        AllowFreeText = allowFreeText;
        DigitsOnly = digitsOnly;
        Options = options is null ? Array.Empty<string>() : new List<string>(options);
        EditOptionsCommand = editOptionsCommand;
        PreviewOptionsCommand = previewOptionsCommand;
        ResetOptionsCommand = resetOptionsCommand;
        AddOptionCommand = addOptionCommand;
        RemoveOptionCommand = removeOptionCommand;
        HighlightKind = highlightKind;
        NachschlagenCommand = nachschlagenCommand;
        StrasseUebernehmenCommand = strasseUebernehmenCommand;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Label { get; }

    /// <summary>
    /// Stabiler Feldschluessel (Katalogfeld bei Haltungen, Spaltenname bei Schaechten).
    /// Nur damit laesst sich eine persoenliche Kartenreihenfolge speichern; die
    /// Beschriftung taugt dafuer nicht, weil sie sich aendern und doppeln kann.
    /// Leer = Karte nimmt an der Umsortierung nicht teil.
    /// </summary>
    public string FieldName { get; init; } = string.Empty;
    public bool IsReadOnly { get; }
    public bool IsMultiline { get; }
    public bool IsCombo { get; }
    public bool AllowFreeText { get; }
    public bool DigitsOnly { get; }
    public IEnumerable<string> Options { get; }
    public ICommand? EditOptionsCommand { get; }
    public ICommand? PreviewOptionsCommand { get; }
    public ICommand? ResetOptionsCommand { get; }
    public ICommand? AddOptionCommand { get; }
    public ICommand? RemoveOptionCommand { get; }
    public RecordDetailHighlightKind HighlightKind { get; }

    /// <summary>Schlaegt den Feldwert beim Kanton nach (Kataster, Grundbuch oder Abwassernetz).</summary>
    public ICommand? NachschlagenCommand { get; }

    /// <summary>
    /// Uebernimmt die Strasse vom Nachbarbauteil. Ober- und Unterschacht
    /// liegen an derselben Stelle wie die Haltung, also gilt dort dieselbe
    /// Adresse — in beide Richtungen.
    /// </summary>
    public ICommand? StrasseUebernehmenCommand { get; }

    /// <summary>
    /// Ob die Karte zu einem Schacht oder einer Haltung gehoert. Die
    /// Quellentabelle fuehrt beide Arten getrennt, und die Netzabfrage
    /// braucht die richtige Ebene: Bauwerke oder Leitungen.
    /// </summary>
    public BauteilArt BauteilArt { get; init; } = BauteilArt.Schacht;

    /// <summary>
    /// Nur wenn das Feld leer ist UND eine Quelle kennt. An einem gefuellten
    /// Feld waere der Menuepunkt eine Einladung zum versehentlichen
    /// Ueberschreiben, an einem Kostenfeld eine leere Zusage.
    /// </summary>
    public bool KannNachschlagen
        => NachschlagenCommand is not null
           && string.IsNullOrWhiteSpace(Value)
           && FeldQuellenTabelle.QuelleFuer(FieldName, BauteilArt) is not null;

    /// <summary>
    /// Nur am leeren Strassenfeld. Der Nachbarwert ist keine amtliche
    /// Auskunft, sondern eine Uebertragung im eigenen Projekt — deshalb ein
    /// eigener Menuepunkt und nicht derselbe wie beim Kanton.
    /// </summary>
    public bool KannStrasseUebernehmen
        => StrasseUebernehmenCommand is not null
           && string.IsNullOrWhiteSpace(Value)
           && IstStrassenfeld(FieldName);

    /// <summary>
    /// Ob das Feld ueberhaupt ein eigenes Kontextmenue braucht. Ohne das
    /// bliebe an einem Textfeld das Windows-Standardmenue mit
    /// Ausschneiden/Kopieren/Einfuegen weg und an einem Auswahlfeld poppte
    /// ein leeres Kaestchen auf.
    /// </summary>
    public bool BrauchtEigenesMenue => KannNachschlagen || KannStrasseUebernehmen;

    private static bool IstStrassenfeld(string feldname)
    {
        var name = (feldname ?? string.Empty).Trim();
        // Beide Schreibweisen kommen in echten Projekten vor.
        return string.Equals(name, "Strasse", StringComparison.OrdinalIgnoreCase)
               || string.Equals(name, "Straße", StringComparison.OrdinalIgnoreCase);
    }
    public bool CanEdit => !IsReadOnly;
    public bool HasManagedOptions =>
        EditOptionsCommand is not null ||
        PreviewOptionsCommand is not null ||
        ResetOptionsCommand is not null ||
        AddOptionCommand is not null ||
        RemoveOptionCommand is not null;

    public string Value
    {
        get => _value;
        set
        {
            var next = value ?? string.Empty;
            if (string.Equals(_value, next, StringComparison.Ordinal))
                return;

            _value = next;
            MeldeWertGeaendert();
            _commitValue(_value);
        }
    }

    /// <summary>
    /// Der Datensatzwert, auf dem der angezeigte Text beruht. Er wird nur beim Uebernehmen aus
    /// dem Datensatz gesetzt, nie waehrend der Bearbeitung. Der Rueckschreibweg vergleicht ihn
    /// mit dem aktuellen Datensatzwert und erkennt so eine inzwischen erfolgte Aenderung
    /// (Nova-Etappe 1, Nachpruefung W01: Tabelle und Formular sind gleichzeitig sichtbar).
    /// </summary>
    public string Ausgangswert { get; private set; }

    /// <summary>
    /// Der Editor dieses Feldes hat den Tastaturfokus. Externe Aenderungen ersetzen den Text
    /// dann nicht unter dem Cursor, sondern werden bis <see cref="BeendeBearbeitung"/> gemerkt.
    /// </summary>
    public bool IsEditing { get; set; }

    private string? _ausstehenderDatensatzwert;

    /// <summary>
    /// Wert aus dem Datensatz uebernehmen, ohne ihn zurueckzuschreiben. Waehrend der
    /// Bearbeitung wird er nur gemerkt; der Ausgangswert bleibt der angezeigte Stand.
    /// </summary>
    public void UebernehmeAusDatensatz(string? wert)
    {
        var next = wert ?? string.Empty;
        if (IsEditing)
        {
            _ausstehenderDatensatzwert = next;
            return;
        }

        _ausstehenderDatensatzwert = null;
        Ausgangswert = next;
        if (string.Equals(_value, next, StringComparison.Ordinal))
            return;

        _value = next;
        MeldeWertGeaendert();
    }

    /// <summary>Bearbeitung beendet: einen waehrenddessen gemerkten Datensatzwert jetzt anzeigen.</summary>
    public void BeendeBearbeitung()
    {
        IsEditing = false;
        if (_ausstehenderDatensatzwert is { } ausstehend)
            UebernehmeAusDatensatz(ausstehend);
    }

    private void MeldeWertGeaendert()
    {
        OnPropertyChanged(nameof(Value));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(SelectedOption));
        // Sonst bliebe der Nachschlag-Menuepunkt sichtbar, obwohl das
        // Feld gerade gefuellt wurde.
        OnPropertyChanged(nameof(KannNachschlagen));
        OnPropertyChanged(nameof(KannStrasseUebernehmen));
        OnPropertyChanged(nameof(BrauchtEigenesMenue));
    }

    public string SelectedOption
    {
        get => _value;
        set => Value = value ?? string.Empty;
    }

    public bool IsEmpty => string.IsNullOrWhiteSpace(_value);

    private bool _isHiddenByUser;

    /// <summary>
    /// Vom Benutzer in der Detailansicht ausgeblendet. Getrennt von <see cref="IsVisible"/>:
    /// das ist die fachliche Regel (Sanierungs-Folgefelder), dies die persoenliche
    /// Einstellung. Eine Karte erscheint nur, wenn beides zutrifft.
    /// Ausblenden veraendert weder den Wert noch einen Export.
    /// </summary>
    public bool IsHiddenByUser
    {
        get => _isHiddenByUser;
        set
        {
            if (_isHiddenByUser == value)
                return;
            _isHiddenByUser = value;
            OnPropertyChanged();
        }
    }

    private bool _isVisible = true;

    /// <summary>
    /// Steuert die Sichtbarkeit des Feldes im Detail. Wird z.B. genutzt, um die
    /// Sanierungs-Folgefelder auszublenden, wenn "Sanieren = Nein" gewaehlt ist.
    /// </summary>
    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            if (_isVisible == value)
                return;
            _isVisible = value;
            OnPropertyChanged();
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public sealed record RecordDetailGroup(
    string Title,
    string Description,
    IReadOnlyList<RecordDetailItem> Items,
    RecordDetailGroupKind Kind = RecordDetailGroupKind.Additional);

public sealed class RecordDetailEditorTemplateSelector : DataTemplateSelector
{
    public DataTemplate? TextTemplate { get; set; }
    public DataTemplate? MultilineTemplate { get; set; }
    public DataTemplate? EditableComboTemplate { get; set; }
    public DataTemplate? FixedComboTemplate { get; set; }

    public override DataTemplate? SelectTemplate(object item, DependencyObject container)
    {
        _ = container;

        if (item is not RecordDetailItem detailItem)
            return TextTemplate;

        if (detailItem.IsCombo)
            return detailItem.AllowFreeText ? EditableComboTemplate : FixedComboTemplate;

        return detailItem.IsMultiline ? MultilineTemplate : TextTemplate;
    }
}
