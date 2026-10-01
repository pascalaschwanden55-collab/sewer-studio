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
            // Bewusste Eingabe des Menschen: als solche kennzeichnen, damit automatische
            // Schreiber sie nicht ueberholen und ein spaeterer Export sie erkennt.
            record.SetFieldValue(fieldName, editedValue, FieldSource.Manual, userEdited: true);
        }

        var optionField = SchaechteColumnPolicy.ResolveOptionField(fieldName);
        if (!string.IsNullOrWhiteSpace(optionField))
            ensureOptionForField(optionField, editedValue);

        return true;
    }

    /// <summary>
    /// Nova-Etappe 2b, Task 6 (Fix-Runde 1): Der Zellen-Commit der Zustandsklasse.
    ///
    /// Die Zustandsklasse steht in einer Vorlagenspalte (Marke mit Auswahl 0 bis 4). Deren
    /// TwoWay-Bindung schreibt direkt in das <c>Fields</c>-Dictionary — ohne Herkunft und ohne
    /// Handmarkierung. Genau die braucht aber der XTF-Export: <c>XtfSchachtPlanBuilder</c>
    /// schreibt <c>BaulicherZustand</c> nur bei einem handgesetzten Feld. Ein von Hand
    /// gewaehlter Wert waere sonst im Programm sichtbar, in der Datei aber nicht.
    ///
    /// Gestempelt wird nur bei echter Aenderung: <paramref name="wertBeimOeffnen"/> ist der
    /// Wert, den die Seite beim Oeffnen der Zelle gemerkt hat. Ohne Aenderung passiert nichts —
    /// sonst waere schon das blosse Anklicken einer Zelle eine Handeingabe (dieselbe Falle wie
    /// F2 bei den Haltungen).
    /// </summary>
    /// <returns>true, wenn geschrieben wurde und das Projekt als geaendert gilt.</returns>
    internal static bool ApplyZustandsklasse(string fieldName, SchachtRecord record, string? wertBeimOeffnen)
    {
        ArgumentNullException.ThrowIfNull(fieldName);
        ArgumentNullException.ThrowIfNull(record);

        // Die Auswahl hat ihren Wert bereits ins Dictionary geschrieben; der Textleser kann ihn
        // bei einer Vorlagenspalte nicht liefern.
        var neuerWert = record.GetFieldValue(fieldName);
        if (wertBeimOeffnen is not null && string.Equals(neuerWert, wertBeimOeffnen, StringComparison.Ordinal))
            return false;

        record.SetFieldValue(fieldName, neuerWert, FieldSource.Manual, userEdited: true);
        return true;
    }
}
