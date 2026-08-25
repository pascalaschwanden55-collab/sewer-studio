using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace AuswertungPro.Next.Application.Dossiers;

/// <summary>
/// Die zusaetzlichen Zeilen des Inhaltsverzeichnisses.
///
/// Die drei Kapitel bleiben Word ueberlassen: es rechnet sie aus den
/// Ueberschriften und kennt ihre Seitenzahlen. Was am Schluss dazukommt —
/// TV-Protokolle, Schachtprotokolle, Plaene — steht dagegen gar nicht im
/// Word-Dokument, sondern liegt als eigene Datei daneben. Word kann diese
/// Zeilen also weder finden noch mit einer Seitenzahl versehen; sie werden
/// deshalb beim Erzeugen geschrieben.
///
/// Ohne Seitenzahl, und das ist Absicht: eine erfundene Zahl waere schlimmer
/// als keine.
/// </summary>
public static class DossierTocAttachments
{
    /// <summary>
    /// Eine vorangestellte Nummer, die der Mensch aus Gewohnheit mitgetippt
    /// hat: "4. TV-Protokolle" oder "5.Plaene". Sie wird entfernt, damit nicht
    /// "4.\t4. TV-Protokolle" im Dossier steht.
    ///
    /// Ein Punkt ist Pflicht — "3 Plaene" ist eine Menge und keine Nummer.
    /// </summary>
    private static readonly Regex FuehrendeNummer = new(
        @"^\d{1,2}\.\s*", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Die Zeilen als Textblock, fortlaufend ab <paramref name="firstNumber"/>.
    /// Leere Zeilen fallen weg und zaehlen nicht mit, sonst entstuenden
    /// Luecken in der Nummerierung.
    /// </summary>
    public static string Build(IEnumerable<string?>? lines, int firstNumber)
    {
        if (lines is null)
            return string.Empty;

        var nummer = firstNumber;
        var zeilen = new List<string>();

        foreach (var eintrag in lines)
        {
            var text = FuehrendeNummer.Replace((eintrag ?? string.Empty).Trim(), string.Empty).Trim();
            if (text.Length == 0)
                continue;

            zeilen.Add($"{nummer}.\t{text}");
            nummer++;
        }

        return string.Join("\n", zeilen);
    }
}
