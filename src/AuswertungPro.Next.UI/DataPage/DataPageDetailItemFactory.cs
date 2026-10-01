using System.Windows.Input;
using AuswertungPro.Next.Application.Lookup;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.DataPage;

public sealed record DataPageManagedComboSpec(
    IEnumerable<string> Options,
    bool AllowFreeText,
    ICommand? EditOptionsCommand = null,
    ICommand? PreviewOptionsCommand = null,
    ICommand? ResetOptionsCommand = null,
    ICommand? AddOptionCommand = null,
    ICommand? RemoveOptionCommand = null);

public sealed class DataPageDetailItemFactory
{
    private readonly Func<string, DataPageManagedComboSpec?> _resolveManagedComboSpec;
    private readonly Action<HaltungRecord, string, string> _commitValue;
    private readonly Func<HaltungRecord, string, ICommand?>? _resolveNachschlag;
    private readonly Func<HaltungRecord, string, ICommand?>? _resolveStrasse;
    private readonly Action<string, string, string>? _konfliktGemeldet;

    /// <param name="konfliktGemeldet">
    /// Wird gerufen, wenn eine Formulareingabe NICHT geschrieben wurde, weil sich der Datensatz
    /// seit der Anzeige geaendert hat: (Feldname, aktueller Datensatzwert, verworfene Eingabe).
    /// </param>
    public DataPageDetailItemFactory(
        Func<string, DataPageManagedComboSpec?> resolveManagedComboSpec,
        Action<HaltungRecord, string, string> commitValue,
        Func<HaltungRecord, string, ICommand?>? resolveNachschlag = null,
        Func<HaltungRecord, string, ICommand?>? resolveStrasse = null,
        Action<string, string, string>? konfliktGemeldet = null)
    {
        _resolveManagedComboSpec = resolveManagedComboSpec ?? throw new ArgumentNullException(nameof(resolveManagedComboSpec));
        _commitValue = commitValue ?? throw new ArgumentNullException(nameof(commitValue));
        _resolveNachschlag = resolveNachschlag;
        _resolveStrasse = resolveStrasse;
        _konfliktGemeldet = konfliktGemeldet;
    }

    /// <summary>
    /// Konfliktregel des Rueckschreibwegs (Nachpruefung W01): Der Datensatz traegt inzwischen
    /// einen anderen Wert als den, auf dem die Eingabe beruht, und die Eingabe ist nicht
    /// zufaellig genau dieser Wert. Dann darf die neuere Korrektur nicht ueberschrieben werden.
    /// </summary>
    public static bool IstKonflikt(string? ausgangswert, string? aktuellerWert, string? eingabe)
        => !string.Equals(aktuellerWert ?? string.Empty, ausgangswert ?? string.Empty, StringComparison.Ordinal)
           && !string.Equals(aktuellerWert ?? string.Empty, eingabe ?? string.Empty, StringComparison.Ordinal);

    public RecordDetailItem Create(string fieldName, HaltungRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        var def = FieldCatalog.Get(fieldName);
        var label = def.Label;
        var value = record.GetFieldValue(fieldName);
        var highlightKind = RecordDetailHighlightPolicy.Resolve(fieldName);
        var managedCombo = _resolveManagedComboSpec(fieldName);
        // Nachschlagen beim Kanton: Das Item entscheidet selbst, ob der
        // Menuepunkt sichtbar wird (leeres Feld mit bekannter Quelle).
        var nachschlagen = _resolveNachschlag?.Invoke(record, fieldName);
        // Strasse vom Nachbarbauteil: eigene Uebertragung im Projekt,
        // keine amtliche Auskunft - deshalb ein eigener Menuepunkt.
        var strasse = _resolveStrasse?.Invoke(record, fieldName);

        // Der Rueckschreibweg kennt sein Item, um den Ausgangswert zu pruefen und nachzufuehren.
        RecordDetailItem? item = null;
        Action<string> commit = next => Rueckschreiben(record, fieldName, item, next);

        if (managedCombo is not null)
        {
            item = new RecordDetailItem(
                label,
                value,
                commitValue: commit,
                isCombo: true,
                allowFreeText: managedCombo.AllowFreeText,
                options: managedCombo.Options,
                editOptionsCommand: managedCombo.EditOptionsCommand,
                previewOptionsCommand: managedCombo.PreviewOptionsCommand,
                resetOptionsCommand: managedCombo.ResetOptionsCommand,
                addOptionCommand: managedCombo.AddOptionCommand,
                removeOptionCommand: managedCombo.RemoveOptionCommand,
                highlightKind: highlightKind,
                nachschlagenCommand: nachschlagen,
                strasseUebernehmenCommand: strasse)
            { FieldName = fieldName, BauteilArt = BauteilArt.Haltung };
            return item;
        }

        var catalogItems = FieldCatalog.GetComboItems(fieldName);
        if (catalogItems.Count > 0)
        {
            item = new RecordDetailItem(
                label,
                value,
                commitValue: commit,
                isCombo: true,
                allowFreeText: false,
                options: catalogItems,
                highlightKind: highlightKind,
                nachschlagenCommand: nachschlagen,
                strasseUebernehmenCommand: strasse)
            { FieldName = fieldName, BauteilArt = BauteilArt.Haltung };
            return item;
        }

        var isMultiline = fieldName is "Primaere_Schaeden" or "Bemerkungen" or "Empfohlene_Sanierungsmassnahmen";
        var digitsOnly = def.Type == FieldType.Int;
        // Die GEONIS-Kennung ist nur Anzeige: Die Wahrheit liegt im Geonis-Objekt des
        // Datensatzes, und der Export liest dort. Eine Handeingabe hier liefe daran
        // vorbei und zeigte etwas anderes, als in die Datei geht.
        var isReadOnly = string.Equals(fieldName, FieldKeys.GeonisId, StringComparison.Ordinal);

        item = new RecordDetailItem(
            label,
            value,
            commitValue: commit,
            isReadOnly: isReadOnly,
            isMultiline: isMultiline,
            digitsOnly: digitsOnly,
            highlightKind: highlightKind,
            nachschlagenCommand: nachschlagen,
            strasseUebernehmenCommand: strasse)
        { FieldName = fieldName, BauteilArt = BauteilArt.Haltung };
        return item;
    }

    /// <summary>
    /// Rueckschreibweg mit Konfliktschutz: Hat sich der Datensatz seit der Anzeige geaendert
    /// (Tabelle, Dienst), bleibt die neuere Korrektur stehen, das Formular zeigt sie, und die
    /// verworfene Eingabe wird gemeldet. Sonst wird geschrieben und der Ausgangswert nachgefuehrt.
    /// </summary>
    private void Rueckschreiben(HaltungRecord record, string fieldName, RecordDetailItem? item, string next)
    {
        var aktuell = record.GetFieldValue(fieldName);
        if (item is not null && IstKonflikt(item.Ausgangswert, aktuell, next))
        {
            item.UebernehmeAusDatensatz(aktuell);
            _konfliktGemeldet?.Invoke(fieldName, aktuell, next);
            return;
        }

        _commitValue(record, fieldName, next);
        // Nach dem Schreiben den echten Datensatzwert uebernehmen (Umbenennung kann abweichen).
        item?.UebernehmeAusDatensatz(record.GetFieldValue(fieldName));
    }
}
