namespace AuswertungPro.Next.UI.Services;

/// <summary>
/// Optikanalyse 28.09.2026, Aufgabe 13 (Windows-Integration). Reine Regel: bildet den
/// Windows-Registry-Wert fuer das App-Design auf ein SewerStudio-Theme ab, OHNE selbst die
/// Registry zu lesen (das macht <see cref="WindowsThemeRegistry"/>) - so ist sie ohne echten
/// Registry-Zugriff testbar.
///
/// Registrypfad: HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize,
/// Wert AppsUseLightTheme (DWORD): 0 = Dunkel, 1 = Hell.
/// </summary>
public static class WindowsThemePreferenceRule
{
    /// <summary>
    /// 0 -&gt; Dunkel. Alles andere, einschliesslich eines fehlenden Werts (kein Schluessel, keine
    /// Berechtigung, frische Installation ohne je geschriebenen Wert), ergibt Hell - das ist
    /// auch Windows' eigener Standard fuer einen unbeschriebenen Wert.
    /// </summary>
    public static string Resolve(int? appsUseLightThemeValue)
        => appsUseLightThemeValue == 0 ? ThemeManager.Dark : ThemeManager.Light;
}
