using System;
using System.Collections.Generic;
using System.Linq;

namespace AuswertungPro.Next.Application.UseCases.CodingSuggestions;

/// <summary>
/// Nova-Fixwelle F4: Welcher Durchlauf gehoert ins Sitzungsregister — und was steht in der
/// Karte "KI-Vorabdurchlauf", wenn er nichts gefunden hat?
///
/// Gemerkt wird nur ein Durchlauf, bei dem mindestens ein Teil wirklich gelaufen ist.
/// Ein abgeschalteter Vorabdurchlauf (Schalter in den Einstellungen) und ein Set, dessen
/// beide Teile nicht verfuegbar waren, sind KEIN Durchlauf: Sie wuerden in der Uebersicht
/// eine Arbeit vortaeuschen, die nie stattgefunden hat. Ein technischer Fehler dagegen IST
/// ein Durchlauf — er ist ein blinder Fleck, den der Mensch sehen soll.
///
/// Reine Werte-Logik ohne WPF und ohne Zustand.
/// </summary>
public static class CodingSuggestionMerkRegel
{
    /// <summary>Darf dieses Ergebnis ins Sitzungsregister?</summary>
    public static bool SollMerken(CodingSuggestionSet? set)
    {
        if (set is null)
            return false;

        return IstGelaufen(set.BogenTeil) || IstGelaufen(set.AnfangEndeTeil);
    }

    /// <summary>
    /// Der Text, den die Karte anstelle eines leeren Abzeichens zeigt. Leer, sobald es
    /// mindestens einen Vorschlag gibt.
    /// </summary>
    public static string Hinweis(CodingSuggestionSet? set)
    {
        if (set is null || set.Suggestions.Count > 0)
            return string.Empty;

        var gruende = new List<string>();
        Sammle(set.BogenTeil, "Bogen", gruende);
        Sammle(set.AnfangEndeTeil, "Rohranfang/Rohrende", gruende);
        return gruende.Count == 0 ? "keine Vorschläge" : string.Join(" · ", gruende);
    }

    private static bool IstGelaufen(CodingSuggestionPartState teil)
        => teil.Status is CodingSuggestionPartStatus.Bereit or CodingSuggestionPartStatus.Fehler;

    private static void Sammle(CodingSuggestionPartState teil, string name, ICollection<string> gruende)
    {
        var grund = (teil.Grund ?? string.Empty).Trim();
        switch (teil.Status)
        {
            case CodingSuggestionPartStatus.Fehler:
                gruende.Add(grund.Length == 0 ? $"{name}: Fehler" : $"{name}: Fehler — {grund}");
                break;
            case CodingSuggestionPartStatus.NichtVerfuegbar:
                gruende.Add(grund.Length == 0 ? $"{name}: nicht verfügbar" : $"{name}: {grund}");
                break;
        }
    }
}
