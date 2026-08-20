using System;
using System.Diagnostics.CodeAnalysis;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Kostenanalyse;

/// <summary>
/// Setzt aus einer Haltung und ihrer Kostenzusammenstellung einen Lernfall zusammen.
///
/// Die Wahrheitsregel ist bewusst streng: Fehlt Durchmesser, Laenge, ein echter Schaden
/// oder das Massnahmenpaket, entsteht KEIN Fall. Ein halber Fall wuerde spaeter als
/// vollwertiges Vorbild herangezogen und still falsche Mengen erzeugen.
/// </summary>
public static class KostenfallExtraktor
{
    public static bool TryErstellen(
        HaltungRecord record,
        HoldingCost? cost,
        string projekt,
        KostenfallHerkunft herkunft,
        DateTime erfasstUtc,
        [NotNullWhen(true)] out Kostenfall? fall,
        out string grund)
    {
        ArgumentNullException.ThrowIfNull(record);
        fall = null;

        var name = (record.GetFieldValue(FieldKeys.HoldingName) ?? "").Trim();
        if (name.Length == 0)
        {
            grund = "Kein Haltungsname hinterlegt.";
            return false;
        }

        var merkmale = KostenfallMerkmalLeser.Lies(record);

        if (merkmale.DnMm is not > 0)
        {
            grund = "Kein gueltiger Durchmesser (DN_mm).";
            return false;
        }

        if (merkmale.LaengeM <= 0d)
        {
            grund = "Keine gueltige Laenge (Haltungslaenge_m).";
            return false;
        }

        if (merkmale.Schaeden.Count == 0)
        {
            grund = "Kein einziger Schaden im Protokoll (Bauteile zaehlen nicht).";
            return false;
        }

        var positionen = MassnahmePaketLeser.Lies(cost);
        if (positionen.Count == 0)
        {
            grund = "Keine ausgewaehlte Massnahme in der Kostenzusammenstellung.";
            return false;
        }

        fall = new Kostenfall
        {
            Haltung = name,
            Projekt = projekt ?? "",
            ErfasstUtc = erfasstUtc,
            Herkunft = herkunft,
            Merkmale = merkmale,
            Positionen = positionen
        };
        grund = "";
        return true;
    }
}
