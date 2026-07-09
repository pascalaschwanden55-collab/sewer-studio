using System.Windows.Input;
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

    public DataPageDetailItemFactory(
        Func<string, DataPageManagedComboSpec?> resolveManagedComboSpec,
        Action<HaltungRecord, string, string> commitValue)
    {
        _resolveManagedComboSpec = resolveManagedComboSpec ?? throw new ArgumentNullException(nameof(resolveManagedComboSpec));
        _commitValue = commitValue ?? throw new ArgumentNullException(nameof(commitValue));
    }

    public RecordDetailItem Create(string fieldName, HaltungRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        var def = FieldCatalog.Get(fieldName);
        var label = def.Label;
        var value = record.GetFieldValue(fieldName);
        var highlightKind = RecordDetailHighlightPolicy.Resolve(fieldName);
        var managedCombo = _resolveManagedComboSpec(fieldName);
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
                highlightKind: highlightKind);
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
                highlightKind: highlightKind);
        }

        var isMultiline = fieldName is "Primaere_Schaeden" or "Bemerkungen" or "Empfohlene_Sanierungsmassnahmen";
        var digitsOnly = def.Type == FieldType.Int;

        return new RecordDetailItem(
            label,
            value,
            commitValue: next => _commitValue(record, fieldName, next),
            isMultiline: isMultiline,
            digitsOnly: digitsOnly,
            highlightKind: highlightKind);
    }
}
