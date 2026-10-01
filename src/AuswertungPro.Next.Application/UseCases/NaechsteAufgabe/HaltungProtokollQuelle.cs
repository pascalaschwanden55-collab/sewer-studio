using System;
using System.Collections.Generic;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.UseCases.NaechsteAufgabe;

/// <summary>
/// Nova-Fixwelle 2b (F1): Welche Felder einer Haltung kommen als Protokoll-PDF in Frage — und
/// in welcher Reihenfolge? Dasselbe Muster wie <see cref="SchachtProtokollQuelle"/>.
///
/// Anlass: Der Knopf in der Haltungsliste entschied nach <c>PDF_Path | PDF_Eigen | PDF_All</c>,
/// der Oeffner (<c>OpenOriginalPdfCommand</c>) sucht zusaetzlich ueber den Protokoll-Locator im
/// Projekt. Beides ist richtig, aber es sind zwei verschiedene Fragen. Entscheid: In der Zelle
/// wird KEIN Dateisystem befragt — bei tausenden Zeilen waere das je Bild ein Ordnerlauf.
/// Die Zelle sagt deshalb nur, ob ein Pfad HINTERLEGT ist; der Gedankenstrich traegt den
/// ehrlichen Hinweis, dass das Kontextmenue trotzdem im Projekt suchen kann.
///
/// Reine Feldregel ohne WPF und ohne Dateizugriff: Ob ein hinterlegter Pfad wirklich existiert
/// und relativ aufzuloesen ist, bleibt Sache des Oeffners.
/// </summary>
public static class HaltungProtokollQuelle
{
    /// <summary>Hinweis am Gedankenstrich der Protokollspalte.</summary>
    public const string OhneProtokollHinweis =
        "kein hinterlegtes Protokoll — das Kontextmenü sucht im Projekt";

    /// <summary>Hinweis am Gedankenstrich der Videospalte.</summary>
    public const string OhneVideoHinweis =
        "kein hinterlegtes Video — Video prüfen sucht im Ordner";

    /// <summary>Die moeglichen Protokollpfade in Pruefreihenfolge; leer, wenn es keinen gibt.</summary>
    public static IReadOnlyList<string> Kandidaten(HaltungRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        var kandidaten = new List<string>(4);
        Ergaenze(kandidaten, record, FieldKeys.PdfPath);
        Ergaenze(kandidaten, record, FieldKeys.PdfEigen);
        Ergaenze(kandidaten, record, FieldKeys.PdfAll);
        Ergaenze(kandidaten, record, FieldKeys.Link);
        return kandidaten;
    }

    /// <summary>Gibt es ueberhaupt eine hinterlegte Protokollquelle? Genau das entscheidet den Knopf.</summary>
    public static bool Vorhanden(HaltungRecord record) => Kandidaten(record).Count > 0;

    /// <summary>
    /// Ein Kandidat muss auf .pdf enden. <c>Link</c> traegt bei Haltungen normalerweise das
    /// Video; nur wenn dort ausdruecklich eine PDF steht, ist es eine Protokollquelle.
    /// </summary>
    private static void Ergaenze(List<string> kandidaten, HaltungRecord record, string feld)
    {
        var wert = record.GetFieldValue(feld)?.Trim();
        if (string.IsNullOrWhiteSpace(wert))
            return;

        if (wert.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            kandidaten.Add(wert);
    }
}
