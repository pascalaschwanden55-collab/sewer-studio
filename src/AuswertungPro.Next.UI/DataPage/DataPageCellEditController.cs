using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.DataPage;

/// <summary>
/// Wendet einen abgeschlossenen Tabellen-Edit auf den Datensatz an.
/// Die WPF-Seite liefert nur Feld, Datensatz und den bereits gelesenen Eingabewert.
/// </summary>
public static class DataPageCellEditController
{
    public static bool Apply(
        string fieldName,
        HaltungRecord? record,
        string? editedValue,
        Func<string, string, bool> confirmSwitchOffRenovation,
        Action<string, string?> ensureOptionForField,
        Func<HaltungRecord, string, string, bool> applyHoldingNameChange)
    {
        ArgumentNullException.ThrowIfNull(fieldName);
        ArgumentNullException.ThrowIfNull(confirmSwitchOffRenovation);
        ArgumentNullException.ThrowIfNull(ensureOptionForField);
        ArgumentNullException.ThrowIfNull(applyHoldingNameChange);

        if (fieldName == "Sanieren_JaNein" && record is not null)
        {
            ApplyRenovationChoice(
                record,
                editedValue ?? string.Empty,
                confirmSwitchOffRenovation,
                ensureOptionForField);
            return true;
        }

        if (fieldName is "Eigentuemer" or "Pruefungsresultat" or "Referenzpruefung")
        {
            if (!string.IsNullOrWhiteSpace(editedValue) && record is not null)
                record.SetFieldValue(fieldName, editedValue, FieldSource.Manual, userEdited: true);

            ensureOptionForField(fieldName, editedValue);
            return true;
        }

        if (fieldName == "Zustandsklasse" && record is not null)
        {
            record.SetFieldValue(
                fieldName,
                editedValue ?? record.GetFieldValue(fieldName),
                FieldSource.Manual,
                userEdited: true);
            return true;
        }

        if (fieldName == "Haltungsname" && record is not null)
        {
            var oldValue = record.GetFieldValue(fieldName);
            return applyHoldingNameChange(record, oldValue, editedValue ?? oldValue);
        }

        if (record is not null && editedValue is not null)
            record.SetFieldValue(fieldName, editedValue, FieldSource.Manual, userEdited: true);

        return true;
    }

    private static void ApplyRenovationChoice(
        HaltungRecord record,
        string editedValue,
        Func<string, string, bool> confirmSwitchOffRenovation,
        Action<string, string?> ensureOptionForField)
    {
        const string fieldName = "Sanieren_JaNein";
        var wasYes = string.Equals(
            record.GetFieldValue(fieldName).Trim(),
            "Ja",
            StringComparison.OrdinalIgnoreCase);
        var isYes = string.Equals(editedValue.Trim(), "Ja", StringComparison.OrdinalIgnoreCase);
        var switchingOff = wasYes && !isYes;

        if (switchingOff && !confirmSwitchOffRenovation(
                "Diese Haltung auf 'nicht sanieren' setzen? Die berechneten Kostenwerte werden entfernt.",
                "Sanieren"))
        {
            record.SetFieldValue(fieldName, "Ja", FieldSource.Manual, userEdited: true);
            ensureOptionForField(fieldName, "Ja");
            return;
        }

        if (!string.IsNullOrWhiteSpace(editedValue) || switchingOff)
            record.SetFieldValue(fieldName, editedValue, FieldSource.Manual, userEdited: true);

        if (switchingOff)
            DataPageSanierungCostMapper.SyncRecord(record, cost: null);

        ensureOptionForField(fieldName, editedValue);
    }
}
