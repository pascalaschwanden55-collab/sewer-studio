using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.DataPage;

/// <summary>
/// Wendet einen abgeschlossenen Schacht-Feldedit ohne WPF-Abhaengigkeit an.
/// </summary>
internal static class SchaechteFieldEditController
{
    internal static bool Apply(
        string fieldName,
        SchachtRecord record,
        string editedValue,
        Func<SchachtRecord, string, string, bool> applyShaftNumberChange,
        Action<string, string?> ensureOptionForField)
    {
        ArgumentNullException.ThrowIfNull(fieldName);
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(editedValue);
        ArgumentNullException.ThrowIfNull(applyShaftNumberChange);
        ArgumentNullException.ThrowIfNull(ensureOptionForField);

        if (string.Equals(fieldName, "Schachtnummer", StringComparison.Ordinal))
        {
            var oldShaftNumber = record.GetFieldValue("Schachtnummer");
            if (!applyShaftNumberChange(record, oldShaftNumber, editedValue))
                return false;
        }
        else
        {
            record.SetFieldValue(fieldName, editedValue);
        }

        var optionField = SchaechteColumnPolicy.ResolveOptionField(fieldName);
        if (!string.IsNullOrWhiteSpace(optionField))
            ensureOptionForField(optionField, editedValue);

        return true;
    }
}
