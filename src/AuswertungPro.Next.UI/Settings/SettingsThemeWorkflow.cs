using System;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Settings;

/// <summary>
/// Optikanalyse 28.09.2026, Aufgabe 13: EIN Weg statt zwei. Die Design-Auswahl (Hell/Dunkel/Wie
/// Windows, drei Radioknoepfe in den Einstellungen) wendet sich beim Auswaehlen sofort an und
/// speichert sofort - kein getrennter "Anwenden"-Knopf mehr. Bis Aufgabe 13 gab es zusaetzlich
/// einen zweiten Umschalter (IsDarkTheme, bi-state) mit eigenem Abgleichweg
/// (SyncUiThemeChanged/SyncIsDarkThemeChanged); der ist mit der dritten Wahl "Wie Windows"
/// entfallen (ein bi-state-Umschalter kann keine drei Zustaende abbilden).
/// </summary>
public static class SettingsThemeWorkflow
{
    public static void ApplyTheme(
        AppSettings settings,
        string? uiTheme,
        Action saveSettingsImmediate)
        => ApplyTheme(settings, uiTheme, saveSettingsImmediate, ApplyToApplicationResources);

    public static void ApplyTheme(
        AppSettings settings,
        string? uiTheme,
        Action saveSettingsImmediate,
        Action<string> applyToResources,
        // Fix-Runde 1, MINOR 7: injizierbarer Registry-Leser fuer die Aufloesung von "System" -
        // null verwendet den echten Registry-Zugriff (Produktionsweg unveraendert). Tests fuer
        // die "System"-Wahl koennen so das konkrete aufgeloeste Theme deterministisch pruefen,
        // statt den echten Registry-Stand des Testrechners zu lesen.
        Func<int?>? readWindowsAppsUseLightTheme = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(saveSettingsImmediate);
        ArgumentNullException.ThrowIfNull(applyToResources);

        // Gespeichert wird die Wahl selbst (kann "System" sein); angewendet wird immer das
        // daraus aufgeloeste konkrete Theme (Light/Dark).
        var preference = ThemeManager.NormalizePreference(uiTheme);
        settings.UiTheme = preference;
        saveSettingsImmediate();
        applyToResources(ThemeManager.ResolveEffectiveTheme(preference, readWindowsAppsUseLightTheme));
    }

    private static void ApplyToApplicationResources(string theme)
    {
        var app = System.Windows.Application.Current;
        if (app != null)
            ThemeManager.ApplyTheme(app.Resources, theme);
    }
}
