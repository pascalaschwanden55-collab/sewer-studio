using System;
using System.Collections.Generic;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.UseCases.NaechsteAufgabe;

/// <summary>
/// Nova-Etappe 2b (Inventar 4.4): Welche Felder eines Schachts kommen als Protokoll-PDF in
/// Frage — und in welcher Reihenfolge?
///
/// Diese Regel speist BEIDE Seiten derselben Sache: den Knopf in der Schachtliste
/// (Sichtbarkeit) und den Oeffner <c>SchachtFileTargetPathResolver</c>. Vorher entschied die
/// Sichtbarkeit nach <c>PDF_Path | PDF_Eigen | PDF_All</c>, der Oeffner dagegen nach
/// <c>PDF_Path</c> und ersatzweise <c>Link</c> — ein Knopf, der nichts oeffnet, und ein
/// vorhandenes Protokoll ohne Knopf.
///
/// Reine Feldregel ohne WPF und ohne Dateizugriff: Ob ein Pfad wirklich existiert und relativ
/// aufzuloesen ist, bleibt Sache des Oeffners. Gelesen wird ueber <see cref="SchachtFeldnamen"/>,
/// weil Schachtfelder nach der Kopfzeile der Excel-Vorlage heissen.
/// </summary>
public static class SchachtProtokollQuelle
{
    /// <summary>Die moeglichen Protokollpfade in Pruefreihenfolge; leer, wenn es keinen gibt.</summary>
    public static IReadOnlyList<string> Kandidaten(SchachtRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        var kandidaten = new List<string>(2);
        Ergaenze(kandidaten, record, FieldKeys.PdfPath);
        Ergaenze(kandidaten, record, FieldKeys.Link);
        return kandidaten;
    }

    /// <summary>Gibt es ueberhaupt eine Protokollquelle? Genau das entscheidet den Knopf.</summary>
    public static bool Vorhanden(SchachtRecord record) => Kandidaten(record).Count > 0;

    /// <summary>
    /// Ein Kandidat muss auf .pdf enden. Der Oeffner prueft dasselbe am aufgeloesten Pfad;
    /// die Endung aendert sich beim Aufloesen nicht, deshalb ist die Antwort hier dieselbe.
    /// </summary>
    private static void Ergaenze(List<string> kandidaten, SchachtRecord record, string gemeint)
    {
        var wert = record.GetFieldValue(SchachtFeldnamen.Feld(record, gemeint))?.Trim();
        if (string.IsNullOrWhiteSpace(wert))
            return;

        if (wert.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            kandidaten.Add(wert);
    }
}
