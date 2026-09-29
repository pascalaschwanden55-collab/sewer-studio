using AuswertungPro.Next.UI;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.Settings;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SettingsThemeWorkflowTests
{
    [Fact]
    public void ApplyTheme_normalizes_saves_and_applies_theme()
    {
        var settings = new AppSettings { UiTheme = ThemeManager.Light };
        var calls = new List<string>();

        SettingsThemeWorkflow.ApplyTheme(
            settings,
            "dark",
            saveSettingsImmediate: () => calls.Add("save"),
            applyToResources: theme => calls.Add("apply:" + theme));

        Assert.Equal(ThemeManager.Dark, settings.UiTheme);
        Assert.Equal(["save", "apply:Dark"], calls);
    }

    [Fact]
    public void ApplyTheme_invalid_value_normalizes_to_light()
    {
        var settings = new AppSettings { UiTheme = ThemeManager.Dark };
        var calls = new List<string>();

        SettingsThemeWorkflow.ApplyTheme(
            settings,
            "Sepia",
            saveSettingsImmediate: () => calls.Add("save"),
            applyToResources: theme => calls.Add("apply:" + theme));

        Assert.Equal(ThemeManager.Light, settings.UiTheme);
        Assert.Equal(["save", "apply:Light"], calls);
    }

    [Fact]
    public void ApplyTheme_system_saves_the_preference_but_applies_the_resolved_theme()
    {
        // "System" ist keine ladbare Ressource - gespeichert wird trotzdem "System" (Aufgabe 13:
        // die Wahl "Wie Windows" muss den Neustart der App ueberleben), angewendet wird das
        // aufgeloeste Theme. Fix-Runde 1, MINOR 7: ein injizierter Leser statt des echten
        // Registry-Zugriffs macht das aufgeloeste Theme deterministisch pruefbar.
        var settings = new AppSettings { UiTheme = ThemeManager.Dark };
        var calls = new List<string>();

        SettingsThemeWorkflow.ApplyTheme(
            settings,
            ThemeManager.System,
            saveSettingsImmediate: () => calls.Add("save"),
            applyToResources: theme => calls.Add("apply:" + theme),
            readWindowsAppsUseLightTheme: () => 0); // AppsUseLightTheme=0 -> Dunkel

        Assert.Equal(ThemeManager.System, settings.UiTheme);
        Assert.Equal(["save", "apply:Dark"], calls);
    }

    [Fact]
    public void ApplyTheme_system_resolves_to_light_when_the_registry_says_so()
    {
        var settings = new AppSettings { UiTheme = ThemeManager.Light };
        var calls = new List<string>();

        SettingsThemeWorkflow.ApplyTheme(
            settings,
            ThemeManager.System,
            saveSettingsImmediate: () => calls.Add("save"),
            applyToResources: theme => calls.Add("apply:" + theme),
            readWindowsAppsUseLightTheme: () => 1); // AppsUseLightTheme=1 -> Hell

        Assert.Equal(ThemeManager.System, settings.UiTheme);
        Assert.Equal(["save", "apply:Light"], calls);
    }
}
