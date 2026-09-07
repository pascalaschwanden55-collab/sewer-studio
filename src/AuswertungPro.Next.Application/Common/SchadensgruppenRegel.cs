using System;
using System.Linq;
using AuswertungPro.Next.Domain.VsaCatalog;

namespace AuswertungPro.Next.Application.Common;

/// <summary>
/// Die eine Regel, was als Schaden zaehlt: die Hauptcodes der Gruppen BA (baulich) und
/// BB (betrieblich), sofern der VSA-Katalog sie wirklich fuehrt. Bestandsaufnahme (BC*,
/// also Rohranfang, Rohrende, Anschluss, Bogen) und Allgemeinzustand (BD*) sind
/// ausdruecklich KEIN Schaden.
///
/// Diese Datei ist die gemeinsame Quelle fuer die Uebersichtskarte des Cockpits
/// (<c>DashboardStatisticsBuilder</c>), den Rohrring und die Liste "Primaere Schaeden"
/// der Haltungsuebersicht. Nie kopieren: Zwei Kopien driften auseinander, und dann
/// zeigt der Ring einen Bogen, den die Liste nicht kennt.
///
/// Reine Werte-Logik ohne Zustand, ohne WPF und ohne Dateizugriff.
/// </summary>
public static class SchadensgruppenRegel
{
    /// <summary>
    /// Bringt einen beliebigen Befundcode auf seinen dreistelligen Hauptcode
    /// ("BABAA" -> "BAB"). Kuerzere Codes bleiben stehen, damit sie unten sauber
    /// abgewiesen werden koennen.
    /// </summary>
    public static string Hauptcode(string? code)
    {
        var text = new string((code ?? string.Empty).Trim().ToUpperInvariant().TakeWhile(char.IsLetterOrDigit).ToArray());
        if (text.Length == 0)
            return string.Empty;

        return text.Length <= 3 ? text : text[..3];
    }

    /// <summary>Ist <paramref name="hauptcode"/> ein bekannter BA-/BB-Hauptcode?</summary>
    public static bool IstSchadensgruppe(string? hauptcode)
    {
        var code = hauptcode ?? string.Empty;
        if (code.Length != 3)
            return false;

        if (!code.StartsWith("BA", StringComparison.OrdinalIgnoreCase)
            && !code.StartsWith("BB", StringComparison.OrdinalIgnoreCase))
            return false;

        return VsaCodeTree.Groups.TryGetValue(code[..2], out var group) && group.Codes.ContainsKey(code);
    }

    /// <summary>Kurzform fuer einen rohen Befundcode: normalisieren und pruefen in einem Schritt.</summary>
    public static bool IstSchaden(string? code) => IstSchadensgruppe(Hauptcode(code));
}
