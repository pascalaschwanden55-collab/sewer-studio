using System;
using AuswertungPro.Next.Application.UseCases.Import.Quellen;

namespace AuswertungPro.Next.Application.Import;

/// <summary>
/// Ein ehrlicher Satz darueber, ob ein Ein-Knopf-Import wirklich vollstaendig war.
///
/// Anlass (Audit 2026-09-05): Der Abschluss meldete "0 Fehler" und wirkte damit wie eine
/// Vollstaendigkeitszusage. Tatsaechlich fehlte dem Lauf oft jede Sollzahl — es gab gar
/// nichts zu vergleichen. Diese Regel unterscheidet die drei Faelle sauber:
/// geprueft und stimmig, nicht vollstaendig, und nicht geprueft.
///
/// Reine Rechnung: kein Datei- oder Netzzugriff, damit Bericht und Oberflaeche denselben
/// Satz verwenden koennen.
/// </summary>
public static class OneClickImportVollstaendigkeit
{
    public static string Beschreibe(OneClickProjectImportResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var urteil = ImportPlausibilitaetsTor.Beurteile(result.Quellenprotokoll, result.BearbeiteteHaltungen);

        if (result.Errors > 0)
        {
            return $"nicht vollstaendig — {result.Errors} Fehler in "
                   + $"{ZaehleFehlerhafteSchritte(result)} Schritt(en)";
        }

        if (!urteil.Geprueft)
        {
            return "nicht vollstaendig geprueft — diese Quelle liefert keine Sollzahl, "
                   + "das Ergebnis konnte nicht gegengerechnet werden";
        }

        if (urteil.Stufe != PlausibilitaetsStufe.Gruen)
            return "nicht vollstaendig — " + urteil.Begruendung;

        if (result.Bestand?.DateienGeprueft != true)
            return "nicht vollständig geprüft — Haltungszahlen geprüft, Dateiverweise noch nicht geprüft";

        if (result.Conflicts > 0)
            return $"nicht vollständig geklärt — {result.Conflicts} offene Konflikte; Bericht prüfen";

        return result.ErwarteteHaltungen > 0
            ? $"geprueft — {result.BearbeiteteHaltungen} von {result.ErwarteteHaltungen} "
              + "Haltung(en) uebernommen, keine Fehler gemeldet"
            : "geprueft — keine Fehler gemeldet";
    }

    private static int ZaehleFehlerhafteSchritte(OneClickProjectImportResult result)
    {
        var anzahl = 0;
        foreach (var schritt in result.Fehlerbilanz.Schritte)
        {
            if (schritt.Anzahl > 0)
                anzahl++;
        }

        return anzahl;
    }
}
