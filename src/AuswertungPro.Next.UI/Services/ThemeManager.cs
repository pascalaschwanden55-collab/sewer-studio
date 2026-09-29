using System;
using System.Collections.ObjectModel;
using System.Windows;

namespace AuswertungPro.Next.UI.Services;

public static class ThemeManager
{
    public const string Light = "Light";
    public const string Dark = "Dark";

    /// <summary>
    /// Optikanalyse 28.09.2026, Aufgabe 13: dritte Design-Wahl "Wie Windows". Wird als
    /// eigenstaendiger Eintrag in <see cref="AppSettings.UiTheme"/> gespeichert - anders als
    /// <see cref="Light"/>/<see cref="Dark"/> ist es KEIN Ressourcen-Theme, das
    /// <see cref="ApplyTheme"/> direkt laden koennte, sondern eine Anweisung, die erst
    /// <see cref="ResolveEffectiveTheme"/> in Light/Dark aufloest (Windows-Registry-Wert).
    /// </summary>
    public const string System = "System";

    private const string ThemeLightSource = "Theme/ThemeLight.xaml";
    private const string ThemeDarkSource = "Theme/Theme.xaml";

    public static string CurrentTheme { get; private set; } = Light;

    /// <summary>Feuert nach jedem Theme-Wechsel (Argument: neues, konkretes Theme Light/Dark).
    /// Fuer Code-Renderer, die Statusfarben nicht per DynamicResource beziehen koennen
    /// (Charts, Overlays).</summary>
    public static event Action<string>? ThemeChanged;

    /// <summary>
    /// Bildet einen beliebigen Wert auf ein tatsaechlich ladbares Ressourcen-Theme ab
    /// (Light/Dark). "System" und jeder unbekannte Wert fallen auf Light - fuer Aufrufer,
    /// die eine konkrete Ressource brauchen (Fenster-Rand, dunkle Titelleiste, Statusfarben).
    /// Fuer die GESPEICHERTE Design-Wahl (die "System" kennen darf) <see cref="NormalizePreference"/>
    /// verwenden.
    /// </summary>
    public static string NormalizeTheme(string? value)
        => string.Equals(value, Dark, StringComparison.OrdinalIgnoreCase) ? Dark : Light;

    /// <summary>
    /// Bildet einen beliebigen gespeicherten Wert auf eine der drei gueltigen Design-Wahlen ab
    /// (Light/Dark/System). Unbekannte Werte fallen wie bisher auf Light.
    /// </summary>
    public static string NormalizePreference(string? value)
        => string.Equals(value, Dark, StringComparison.OrdinalIgnoreCase) ? Dark
           : string.Equals(value, System, StringComparison.OrdinalIgnoreCase) ? System
           : Light;

    /// <summary>
    /// Loest die gespeicherte Design-Wahl auf ein konkretes Ressourcen-Theme auf. Bei "System"
    /// entscheidet der Windows-Registry-Wert (<see cref="WindowsThemePreferenceRule"/>); ein
    /// Leser laesst sich fuer Tests einsetzen, Standard ist der echte Registry-Zugriff.
    /// </summary>
    public static string ResolveEffectiveTheme(string? preference, Func<int?>? readWindowsAppsUseLightTheme = null)
    {
        var normalized = NormalizePreference(preference);
        if (!string.Equals(normalized, System, StringComparison.Ordinal))
            return normalized;

        var reader = readWindowsAppsUseLightTheme ?? WindowsThemeRegistry.ReadAppsUseLightTheme;
        return WindowsThemePreferenceRule.Resolve(reader());
    }

    public static Uri GetThemeUri(string? theme)
    {
        var normalized = NormalizeTheme(theme);
        var source = normalized == Dark ? ThemeDarkSource : ThemeLightSource;
        return ComponentUri(source);
    }

    /// <summary>
    /// Baut eine vollstaendig qualifizierte pack-URI ("pack://application:,,,/&lt;Assembly&gt;;
    /// component/&lt;relativePath&gt;") statt einer blossen relativen URI. Eine relative URI
    /// waere auf <c>System.Windows.Application.ResourceAssembly</c> angewiesen - ein
    /// prozessweiter, nur einmal setzbarer Wert, der im echten SewerStudio.exe zufaellig
    /// passt (Einstiegs-Assembly = UI-Assembly), aber ausserhalb davon (z. B. ein
    /// Test-Wirtsprozess) bereits auf etwas anderes fixiert sein kann. Die vollqualifizierte
    /// URI braucht diesen globalen Zustand gar nicht erst.
    /// </summary>
    private static Uri ComponentUri(string relativePath)
    {
        var assemblyName = typeof(ThemeManager).Assembly.GetName().Name;
        return new Uri($"pack://application:,,,/{assemblyName};component/{relativePath}", UriKind.Absolute);
    }

    public static void ApplyTheme(ResourceDictionary rootResources, string? theme)
    {
        var normalized = NormalizeTheme(theme);
        var merged = rootResources.MergedDictionaries;
        var replacement = new ResourceDictionary { Source = GetThemeUri(normalized) };
        var existingIndex = -1;

        for (var i = 0; i < merged.Count; i++)
        {
            if (IsThemeDictionary(merged[i]))
            {
                existingIndex = i;
                break;
            }
        }

        if (existingIndex >= 0)
        {
            merged[existingIndex] = replacement;
            CurrentTheme = normalized;
            WindowBackdropHelper.ApplyToOpenWindows(normalized);
            ThemeChanged?.Invoke(normalized);
            return;
        }

        merged.Insert(0, replacement);
        CurrentTheme = normalized;
        WindowBackdropHelper.ApplyToOpenWindows(normalized);
        ThemeChanged?.Invoke(normalized);
    }

    private static bool IsThemeDictionary(ResourceDictionary dictionary)
    {
        var source = dictionary.Source?.OriginalString;
        if (string.IsNullOrWhiteSpace(source))
            return false;

        source = source.Replace('\\', '/');
        return source.EndsWith("Theme/ThemeLight.xaml", StringComparison.OrdinalIgnoreCase)
            || source.EndsWith("Theme/Theme.xaml", StringComparison.OrdinalIgnoreCase);
    }

    private const string HighContrastSource = "Theme/ThemeHighContrast.xaml";

    /// <summary>
    /// True, solange die Hochkontrast-Ueberlagerung (<c>Theme/ThemeHighContrast.xaml</c>) aktuell
    /// eingehaengt ist. Nur fuer Tests/Diagnose.
    /// </summary>
    public static bool IsHighContrastOverlayApplied(ResourceDictionary rootResources)
        => FindDictionaryIndex(rootResources.MergedDictionaries, HighContrastSource) >= 0;

    /// <summary>
    /// Haengt die Hochkontrast-Ueberlagerung ein/aus. Sie wird IMMER NACH dem normalen
    /// Hell-/Dunkel-Theme in <see cref="ResourceDictionary.MergedDictionaries"/> eingefuegt
    /// (hoeherer Index = hoehere Prioritaet bei der WPF-Ressourcensuche): Sie ueberschreibt nur
    /// die von ihr definierten Kern-Tokens; alle anderen Tokens bleiben unveraendert vom
    /// darunterliegenden Theme sichtbar (Fallback ohne fehlende Ressource). Ein spaeterer
    /// Hell-/Dunkel-Wechsel via <see cref="ApplyTheme"/> ersetzt nur seinen eigenen Eintrag und
    /// laesst diese Ueberlagerung unangetastet.
    /// </summary>
    public static void SetHighContrastOverlay(ResourceDictionary rootResources, bool enabled)
    {
        var merged = rootResources.MergedDictionaries;
        var existingIndex = FindDictionaryIndex(merged, HighContrastSource);

        if (enabled)
        {
            if (existingIndex >= 0)
                return;

            merged.Add(new ResourceDictionary { Source = ComponentUri(HighContrastSource) });
        }
        else if (existingIndex >= 0)
        {
            merged.RemoveAt(existingIndex);
        }
    }

    private static int FindDictionaryIndex(Collection<ResourceDictionary> merged, string sourceSuffix)
    {
        for (var i = 0; i < merged.Count; i++)
        {
            var source = merged[i].Source?.OriginalString?.Replace('\\', '/');
            if (source != null && source.EndsWith(sourceSuffix, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return -1;
    }
}
