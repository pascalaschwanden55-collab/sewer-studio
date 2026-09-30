using System;
using Microsoft.Win32;

namespace AuswertungPro.Next.UI.Services;

/// <summary>
/// Optikanalyse 28.09.2026, Aufgabe 13, Fix-Runde 1 (MINOR 6): reine Entscheidungsregel fuer
/// <see cref="WindowsThemeFollowService"/> - herausgeloest, damit sie ohne echtes
/// <see cref="SystemEvents.UserPreferenceChanged"/> und ohne WPF-Dispatcher testbar ist.
/// </summary>
public static class WindowsThemeFollowPolicy
{
    /// <summary>
    /// Der Windows-Hell/Dunkel-Umschalter meldet sich ueber General ODER Color - welche der
    /// beiden genau feuert, ist nicht am echten Windows-Dialog verifiziert (siehe docs/architektur/oberflaeche.md, Aufgabe 13);
    /// beide werden zugelassen statt zu raten. Andere Kategorien (Schriftgroesse, Sprache, ...)
    /// gehen SewerStudio hier nichts an.
    /// </summary>
    public static bool IstRelevanteKategorie(UserPreferenceCategory kategorie)
        => kategorie == UserPreferenceCategory.General || kategorie == UserPreferenceCategory.Color;

    /// <summary>
    /// Entscheidet, ob auf eine Windows-Einstellungsaenderung hin neu angewendet werden soll, und
    /// liefert dabei gleich das aufgeloeste Theme. False in drei Faellen: die Kategorie ist nicht
    /// relevant, die gespeicherte Design-Wahl ist nicht "System" (der Benutzer hat Hell/Dunkel
    /// fest gewaehlt - Windows' eigener Wechsel geht ihn dann nichts an), oder das aufgeloeste
    /// Theme entspricht bereits dem aktuell angewendeten (kein unnoetiges Neuzeichnen aller
    /// offenen Fenster bei einer Windows-Einstellung, die nichts an Hell/Dunkel aendert).
    /// </summary>
    public static bool SollNeuAnwenden(
        UserPreferenceCategory kategorie,
        string? gespeichertePraeferenz,
        string aktuellesTheme,
        Func<int?> leseWindowsAppsUseLightTheme,
        out string aufgeloestesTheme)
    {
        ArgumentNullException.ThrowIfNull(leseWindowsAppsUseLightTheme);
        aufgeloestesTheme = aktuellesTheme;

        if (!IstRelevanteKategorie(kategorie))
            return false;

        if (!string.Equals(
                ThemeManager.NormalizePreference(gespeichertePraeferenz),
                ThemeManager.System,
                StringComparison.Ordinal))
        {
            return false;
        }

        var resolved = ThemeManager.ResolveEffectiveTheme(gespeichertePraeferenz, leseWindowsAppsUseLightTheme);
        if (string.Equals(resolved, aktuellesTheme, StringComparison.Ordinal))
            return false;

        aufgeloestesTheme = resolved;
        return true;
    }
}
