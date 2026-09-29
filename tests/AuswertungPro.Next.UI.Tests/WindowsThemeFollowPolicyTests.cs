using Microsoft.Win32;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Optikanalyse 28.09.2026, Aufgabe 13, Fix-Runde 1 (MINOR 6): reine Entscheidungslogik, kein
/// echtes SystemEvents.UserPreferenceChanged, kein Dispatcher, kein Registry-Zugriff (Reader
/// wird injiziert).
/// </summary>
public sealed class WindowsThemeFollowPolicyTests
{
    [Theory]
    [InlineData(UserPreferenceCategory.General, true)]
    [InlineData(UserPreferenceCategory.Color, true)]
    [InlineData(UserPreferenceCategory.Accessibility, false)]
    [InlineData(UserPreferenceCategory.Locale, false)]
    [InlineData(UserPreferenceCategory.Window, false)]
    public void IstRelevanteKategorie_erkennt_nur_General_und_Color(UserPreferenceCategory kategorie, bool erwartet)
    {
        Assert.Equal(erwartet, WindowsThemeFollowPolicy.IstRelevanteKategorie(kategorie));
    }

    [Fact]
    public void SollNeuAnwenden_false_bei_nicht_relevanter_Kategorie_liest_Registry_gar_nicht()
    {
        var readerAufgerufen = false;

        var ergebnis = WindowsThemeFollowPolicy.SollNeuAnwenden(
            UserPreferenceCategory.Accessibility,
            ThemeManager.System,
            ThemeManager.Light,
            () => { readerAufgerufen = true; return 0; },
            out var resolved);

        Assert.False(ergebnis);
        Assert.False(readerAufgerufen, "Eine nicht relevante Kategorie darf die Registry gar nicht erst lesen.");
        Assert.Equal(ThemeManager.Light, resolved);
    }

    [Theory]
    [InlineData(ThemeManager.Light)]
    [InlineData(ThemeManager.Dark)]
    public void SollNeuAnwenden_false_wenn_die_Wahl_nicht_System_ist(string festeWahl)
    {
        var readerAufgerufen = false;

        var ergebnis = WindowsThemeFollowPolicy.SollNeuAnwenden(
            UserPreferenceCategory.General,
            festeWahl,
            ThemeManager.Light,
            () => { readerAufgerufen = true; return 0; },
            out _);

        Assert.False(ergebnis);
        Assert.False(readerAufgerufen,
            "Eine fest gewaehlte Wahl (nicht \"System\") geht ein Windows-Themenwechsel nichts an - die Registry wird nicht gelesen.");
    }

    [Fact]
    public void SollNeuAnwenden_true_wenn_System_gewaehlt_und_das_aufgeloeste_Theme_wechselt()
    {
        var ergebnis = WindowsThemeFollowPolicy.SollNeuAnwenden(
            UserPreferenceCategory.General,
            ThemeManager.System,
            ThemeManager.Light,
            () => 0, // AppsUseLightTheme=0 -> Dunkel
            out var resolved);

        Assert.True(ergebnis);
        Assert.Equal(ThemeManager.Dark, resolved);
    }

    [Fact]
    public void SollNeuAnwenden_false_wenn_das_aufgeloeste_Theme_bereits_aktuell_ist()
    {
        // MINOR 6: kein unnoetiges Neuzeichnen, wenn Windows z. B. die Akzentfarbe (Kategorie
        // Color) aendert, aber Hell/Dunkel gleich bleibt.
        var ergebnis = WindowsThemeFollowPolicy.SollNeuAnwenden(
            UserPreferenceCategory.Color,
            ThemeManager.System,
            ThemeManager.Light,
            () => 1, // AppsUseLightTheme=1 -> Hell, bereits aktuell
            out var resolved);

        Assert.False(ergebnis);
        Assert.Equal(ThemeManager.Light, resolved);
    }

    [Fact]
    public void SollNeuAnwenden_true_bei_Kategorie_Color_ebenso_wie_General()
    {
        var ergebnis = WindowsThemeFollowPolicy.SollNeuAnwenden(
            UserPreferenceCategory.Color,
            ThemeManager.System,
            ThemeManager.Dark,
            () => 1, // AppsUseLightTheme=1 -> Hell, wechselt von Dunkel
            out var resolved);

        Assert.True(ergebnis);
        Assert.Equal(ThemeManager.Light, resolved);
    }
}
