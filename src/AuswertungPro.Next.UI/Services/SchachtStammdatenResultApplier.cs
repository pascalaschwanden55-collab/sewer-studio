using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Services;

internal sealed record SchachtStammdatenApplyResult(
    int ChangedShaftCount,
    int AddedFieldCount,
    string Summary,
    string Details)
{
    internal string DialogText => Summary + Details;
}

/// <summary>
/// Uebernimmt ermittelte PDF-Stammdaten ausschliesslich in noch leere Felder und
/// baut den bestehenden Abschlussbericht fuer die UI.
/// </summary>
internal static class SchachtStammdatenResultApplier
{
    internal static SchachtStammdatenApplyResult Apply(
        IEnumerable<SchachtRecord> records,
        SchachtStammdatenErgaenzungsErgebnis result,
        Action? beforeApply = null)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(result);

        var recordsById = records.ToDictionary(record => record.Id);
        beforeApply?.Invoke();
        var changedShafts = 0;
        var addedFields = 0;

        foreach (var addition in result.Ergaenzungen)
        {
            if (!recordsById.TryGetValue(addition.RecordId, out var record))
                continue;

            var recordChanged = false;
            recordChanged |= SetIfMissing(record, "Schachtform", addition.Schachtform, ref addedFields);
            recordChanged |= SetIfMissing(record, "Dimension", addition.Dimension, ref addedFields);
            recordChanged |= SetIfMissing(record, "Schachttiefe", addition.Schachttiefe, ref addedFields);
            if (recordChanged)
                changedShafts++;
        }

        var summary = $"Ergaenzt: {changedShafts} Schaechte / {addedFields} Felder. " +
                      $"PDF gefunden: {result.PdfGefunden}, ohne PDF: {result.PdfNichtGefunden}, " +
                      $"kein passendes Schachtprotokoll: {result.NichtLesbar}, " +
                      $"bereits vollstaendig: {result.BereitsVollstaendig}.";
        var details = result.Meldungen.Count == 0
            ? string.Empty
            : "\n\nHinweise:\n" + string.Join("\n", result.Meldungen.Take(12));
        if (result.Meldungen.Count > 12)
            details += $"\n... und {result.Meldungen.Count - 12} weitere Hinweise.";

        return new SchachtStammdatenApplyResult(
            changedShafts,
            addedFields,
            summary,
            details);
    }

    private static bool SetIfMissing(
        SchachtRecord record,
        string fieldName,
        string? value,
        ref int addedFields)
    {
        if (!string.IsNullOrWhiteSpace(record.GetFieldValue(fieldName))
            || string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        record.SetFieldValue(fieldName, value.Trim());
        addedFields++;
        return true;
    }
}
