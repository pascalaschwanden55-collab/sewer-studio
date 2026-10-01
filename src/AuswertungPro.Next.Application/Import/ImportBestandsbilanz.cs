using System;
using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Import;

/// <summary>Was nach dem Import wirklich im Projekt steht — je Bauwerksart getrennt.</summary>
/// <param name="Haltungen">Haltungen im Projekt.</param>
/// <param name="HaltungenMitVideo">Davon mit einem Videoverweis.</param>
/// <param name="HaltungenMitGegenvideo">Davon mit einem Gegeninspektionsvideo.</param>
/// <param name="HaltungenMitProtokoll">Davon mit einem verteilten Original-Protokoll.</param>
/// <param name="HaltungenMitBefunden">Davon mit mindestens einem Befund.</param>
/// <param name="Befunde">Befunde insgesamt.</param>
/// <param name="Schaechte">Schaechte im Projekt.</param>
/// <param name="SchaechteMitProtokoll">Davon mit einem verteilten Protokoll.</param>
public sealed record ImportBestandsbilanz(
    int Haltungen,
    int HaltungenMitVideo,
    int HaltungenMitGegenvideo,
    int HaltungenMitProtokoll,
    int HaltungenMitBefunden,
    int Befunde,
    int Schaechte,
    int SchaechteMitProtokoll)
{
    public bool DateienGeprueft { get; init; }

    /// <summary>Lesbare Zeilen fuer Bericht und Abschlussmeldung.</summary>
    public IReadOnlyList<string> Berichtszeilen()
    {
        var zeilen = new List<string>
        {
            $"Haltungen: {Haltungen} — {HaltungenMitVideo} mit Video, "
            + $"{HaltungenMitProtokoll} mit Protokoll, {HaltungenMitBefunden} mit Befunden "
            + $"({Befunde} Befunde insgesamt)."
        };

        if (HaltungenMitGegenvideo > 0)
            zeilen.Add($"Gegeninspektionsvideos: {HaltungenMitGegenvideo}.");

        var ohneVideo = Haltungen - HaltungenMitVideo;
        var ohneProtokoll = Haltungen - HaltungenMitProtokoll;
        if (ohneVideo > 0 || ohneProtokoll > 0)
        {
            zeilen.Add(
                $"Ohne Video: {ohneVideo}. Ohne Protokoll: {ohneProtokoll}. "
                + "Ob das fehlt oder so richtig ist, sagt diese Zahl NICHT — "
                + "sie nennt nur den Bestand.");
        }

        zeilen.Add($"Schaechte: {Schaechte} — {SchaechteMitProtokoll} mit Protokoll.");
        if (!DateienGeprueft)
            zeilen.Add("Dateien nicht geprüft: Die Zahlen zählen nur gespeicherte Verweise, keine nachgewiesenen Dateien.");
        return zeilen;
    }
}

/// <summary>
/// Zaehlt nach dem Import, was im Projekt angekommen ist.
///
/// Anlass (Audit 2026-09-05): Der Abschluss nannte eine einzige Zahl "Haltungen" und
/// daneben "0 Fehler". Beides zusammen las sich wie eine Vollstaendigkeitszusage, sagte
/// aber nichts darueber, ob die Videos und Protokolle wirklich da sind.
///
/// Diese Bilanz erfindet ausdruecklich KEINE Sollzahl. Sie sagt, was da ist und was
/// fehlt — die Bewertung bleibt beim Menschen. Eine Haltung ohne Video kann sauber sein
/// (nicht befahren) oder ein Verlust; das entscheidet die Fachperson, nicht der Import.
///
/// Reine Rechnung: kein Datei-, Netz- oder UI-Zugriff.
/// </summary>
public static class ImportBestandszaehler
{
    public static ImportBestandsbilanz Zaehle(Project project, Func<string, bool>? dateiPruefer = null)
    {
        ArgumentNullException.ThrowIfNull(project);

        var haltungen = project.Data;
        return new ImportBestandsbilanz(
            Haltungen: haltungen.Count,
            HaltungenMitVideo: haltungen.Count(h => Gesetzt(h.GetFieldValue(FieldKeys.Link))),
            HaltungenMitGegenvideo: haltungen.Count(h => Gesetzt(h.GetFieldValue("Link_G"))),
            HaltungenMitProtokoll: haltungen.Count(h => Gesetzt(h.GetFieldValue(FieldKeys.PdfPath))),
            HaltungenMitBefunden: haltungen.Count(h => (h.VsaFindings?.Count ?? 0) > 0),
            Befunde: haltungen.Sum(h => h.VsaFindings?.Count ?? 0),
            Schaechte: project.SchaechteData.Count,
            SchaechteMitProtokoll: project.SchaechteData.Count(s => Gesetzt(s.GetFieldValue("PDF_Path"))))
        { DateienGeprueft = dateiPruefer is not null };

        bool Gesetzt(string? wert) => !string.IsNullOrWhiteSpace(wert)
            && (dateiPruefer is null || dateiPruefer(wert));
    }
}
