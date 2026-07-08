using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Schacht;

/// <summary>
/// Bildet eine einfache Schacht-Empfehlung (<see cref="HoldingCost"/>) auf die
/// Excel-relevanten Felder eines <see cref="SchachtRecord"/> ab:
/// Massnahmen-Text -> Feld "Massnahmen", Nettosumme -> Feld "Kosten".
/// Beide sind Kopfspalten der Schaechte-Vorlage (Zeile 12) und werden vom
/// bestehenden Export (<c>ExportSchaechteToTemplate</c>) unveraendert geschrieben.
/// Reine Feld-Logik — Persistenz, Fenster und Dirty-Flag bleiben aussen.
/// </summary>
public static class SchachtEmpfehlungRecordMapper
{
    public const string MassnahmenField = "Massnahmen";
    public const string KostenField = "Kosten";

    /// <summary>Schreibt Massnahmen-Text und Nettosumme in den Schacht-Record.</summary>
    public static void ApplyTo(SchachtRecord record, HoldingCost? cost)
    {
        if (record is null)
            return;

        record.SetFieldValue(MassnahmenField, SchachtEmpfehlungTextFormatter.BuildMassnahmenText(cost));
        record.SetFieldValue(
            KostenField,
            SchachtEmpfehlungTextFormatter.FormatTotal(SchachtEmpfehlungTextFormatter.ResolveTotal(cost)));
    }

    /// <summary>Leert beide Felder (Massnahme auf "keine" gesetzt / Auswahl geleert).</summary>
    public static void Clear(SchachtRecord record)
    {
        if (record is null)
            return;

        record.SetFieldValue(MassnahmenField, "");
        record.SetFieldValue(KostenField, "");
    }
}
