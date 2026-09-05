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

    public DataPageDetailItemFactory(
        Func<string, DataPageManagedComboSpec?> resolveManagedComboSpec,
        Action<HaltungRecord, string, string> commitValue,
        Func<HaltungRecord, string, ICommand?>? resolveNachschlag = null,
        Func<HaltungRecord, string, ICommand?>? resolveStrasse = null)
    {
        _resolveManagedComboSpec = resolveManagedComboSpec ?? throw new ArgumentNullException(nameof(resolveManagedComboSpec));
        _commitValue = commitValue ?? throw new ArgumentNullException(nameof(commitValue));
        _resolveNachschlag = resolveNachschlag;
        _resolveStrasse = resolveStrasse;
    }

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
        if (managedCombo is not null)
        {
            return new RecordDetailItem(
                label,
                value,
                commitValue: next => _commitValue(record, fieldName, next),
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
        }

        var catalogItems = FieldCatalog.GetComboItems(fieldName);
        if (catalogItems.Count > 0)
        {
            return new RecordDetailItem(
                label,
                value,
                commitValue: next => _commitValue(record, fieldName, next),
                isCombo: true,
                allowFreeText: false,
                options: catalogItems,
                highlightKind: highlightKind,
                nachschlagenCommand: nachschlagen,
                strasseUebernehmenCommand: strasse)
            { FieldName = fieldName, BauteilArt = BauteilArt.Haltung };
        }

        var isMultiline = fieldName is "Primaere_Schaeden" or "Bemerkungen" or "Empfohlene_Sanierungsmassnahmen";
        var digitsOnly = def.Type == FieldType.Int;
        // Die GEONIS-Kennung ist nur Anzeige: Die Wahrheit liegt im Geonis-Objekt des
        // Datensatzes, und der Export liest dort. Eine Handeingabe hier liefe daran
        // vorbei und zeigte etwas anderes, als in die Datei geht.
        var isReadOnly = string.Equals(fieldName, FieldKeys.GeonisId, StringComparison.Ordinal);

        return new RecordDetailItem(
            label,
            value,
            commitValue: next => _commitValue(record, fieldName, next),
            isReadOnly: isReadOnly,
            isMultiline: isMultiline,
            digitsOnly: digitsOnly,
            highlightKind: highlightKind,
            nachschlagenCommand: nachschlagen,
            strasseUebernehmenCommand: strasse)
        { FieldName = fieldName, BauteilArt = BauteilArt.Haltung };
    }
}
