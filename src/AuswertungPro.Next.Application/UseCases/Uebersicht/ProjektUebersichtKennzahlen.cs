using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AuswertungPro.Next.Application.UseCases.NaechsteAufgabe;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.UseCases.Uebersicht;

public sealed record StammdatenVollstaendigkeit(string Feld, int Gefuellt, int Gesamt)
{
    public double Prozent => Gesamt == 0 ? 0 : 100.0 * Gefuellt / Gesamt;

    /// <summary>
    /// Inventar 4.1: Farbstufe des Balkens als Ziffer der Zustandsklassen-Skala (nicht als
    /// praefigierter Text) - "4" ab 95 %, "3" ab 70 %, sonst "2". Die spaetere Farbgebung
    /// (z. B. <c>ZustandsklasseBrushConverter</c>) erwartet genau diese Ziffern.
    /// </summary>
    public string Stufe => Prozent >= 95 ? "4" : Prozent >= 70 ? "3" : "2";
}

public sealed record ProjektUebersichtKennzahlen(
    int Haltungen, int Geprueft, int KiAnalysiert, int Offen, double GesamtlaengeM,
    int Schaechte, int SchaechteMitProtokoll, int DringendHaltungen, int DringendSchaechte,
    IReadOnlyList<StammdatenVollstaendigkeit> Stammdaten);

/// <summary>Nova-Etappe 2 (Inventar 4.1): alle Zahlen der Uebersicht aus demselben Bestand (BEWERTUNG N04).</summary>
public static class ProjektUebersichtRechner
{
    private static readonly CultureInfo DeCh = CultureInfo.GetCultureInfo("de-CH");

    public static ProjektUebersichtKennzahlen Berechne(Project projekt)
    {
        ArgumentNullException.ThrowIfNull(projekt);
        var h = projekt.Data.ToList();
        var s = projekt.SchaechteData.ToList();
        var stand = h.Select(HaltungPruefstatus.Bestimme).ToList();
        static bool Dringend(string? zk) => zk?.Trim() is "0" or "1";
        static bool Gefuellt(string? v) => !string.IsNullOrWhiteSpace(v);
        double Laenge(HaltungRecord r) => double.TryParse((r.GetFieldValue(FieldKeys.HoldingLengthMeters) ?? "").Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) && d > 0 ? d : 0;

        StammdatenVollstaendigkeit St(string feld, Func<HaltungRecord, string?> wert)
            => new(feld, h.Count(r => Gefuellt(wert(r))), h.Count);

        return new ProjektUebersichtKennzahlen(
            Haltungen: h.Count,
            Geprueft: stand.Count(x => x == HaltungPruefstand.Abgeschlossen),
            KiAnalysiert: stand.Count(x => x == HaltungPruefstand.KiAnalysiert),
            Offen: stand.Count(x => x == HaltungPruefstand.Offen),
            GesamtlaengeM: h.Sum(Laenge),
            Schaechte: s.Count,
            SchaechteMitProtokoll: s.Count(x => Gefuellt(x.GetFieldValue(FieldKeys.PdfPath))),
            DringendHaltungen: h.Count(r => Dringend(r.GetFieldValue(FieldKeys.ConditionClass))),
            DringendSchaechte: s.Count(x => Dringend(x.GetFieldValue(FieldKeys.ConditionClass))),
            Stammdaten: new[]
            {
                St("Material", r => r.GetFieldValue(FieldKeys.PipeMaterial)),
                St("DN", r => r.GetFieldValue(FieldKeys.NominalDiameterMm)),
                St("Baujahr", r => r.GetFieldValue(FieldKeys.ConstructionYear)),
                St("GEONIS", r => r.Geonis?.Haltung ?? r.GetFieldValue(FieldKeys.GeonisId))
            });
    }

    public static string HeroText(ProjektUebersichtKennzahlen k)
    {
        var prozent = k.Haltungen == 0 ? 0 : 100.0 * k.Geprueft / k.Haltungen;
        return $"{k.Geprueft} von {k.Haltungen} Haltungen fachlich geprüft ({prozent.ToString("0.0", DeCh)} %). " +
               $"{k.KiAnalysiert} von der KI analysiert und noch nicht geprüft, {k.Offen} ohne Analyse. " +
               $"{k.DringendHaltungen} Haltungen dringend (Z0 oder Z1). " +
               $"Bestand mit {k.Haltungen} Haltungen und {k.Schaechte} Schächten.";
    }
}
