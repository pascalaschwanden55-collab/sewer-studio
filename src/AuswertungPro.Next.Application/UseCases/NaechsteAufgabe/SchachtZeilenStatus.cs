using System;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.UseCases.NaechsteAufgabe;

/// <summary>
/// Nova-Etappe 2b (Inventar 4.4): WPF-freie Regel fuer die nur lesende Protokollspalte der
/// Schachtliste — dasselbe Muster wie <see cref="HaltungZeilenStatus"/>, aber ohne Ampel:
/// Am Schacht wird die Zustandsklasse nie berechnet, und eine KI-Analyse gibt es dort nicht.
///
/// Gelesen wird ueber <see cref="SchachtFeldnamen"/>: Schachtfelder heissen nach der Kopfzeile
/// der Excel-Vorlage, ein direkter Katalogname findet sie nicht immer (derselbe Fehler, der
/// beim XTF-Export einmal jeden Schacht aus der Datei geworfen hat).
/// </summary>
public static class SchachtZeilenStatus
{
    /// <summary>
    /// Hat der Schacht ein verknuepftes Protokoll? Feldnamen wie im Linkvertrag des
    /// ExcelTemplateExportService: irgendeines der drei Protokollfelder genuegt.
    /// </summary>
    public static bool HatProtokoll(SchachtRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        return Gefuellt(record, FieldKeys.PdfPath)
               || Gefuellt(record, FieldKeys.PdfEigen)
               || Gefuellt(record, FieldKeys.PdfAll);
    }

    private static bool Gefuellt(SchachtRecord record, string gemeint)
        => !string.IsNullOrWhiteSpace(record.GetFieldValue(SchachtFeldnamen.Feld(record, gemeint)));
}
