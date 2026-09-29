using Microsoft.Win32;

namespace AuswertungPro.Next.UI.Services;

/// <summary>
/// Optikanalyse 28.09.2026, Aufgabe 13 (Windows-Integration). Liest ausschliesslich den
/// Registry-Wert - die Auswertung (0/1/fehlend -&gt; Theme) liegt getrennt in
/// <see cref="WindowsThemePreferenceRule"/>.
/// </summary>
public static class WindowsThemeRegistry
{
    private const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string ValueName = "AppsUseLightTheme";

    /// <summary>
    /// Liest HKCU\...\Personalize\AppsUseLightTheme. Liefert null, wenn der Schluessel/Wert
    /// fehlt oder nicht lesbar ist (kein Absturz beim Design-Wechsel wegen eines gesperrten
    /// Registry-Zugriffs).
    /// </summary>
    public static int? ReadAppsUseLightTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(KeyPath);
            var value = key?.GetValue(ValueName);
            return value is int intValue ? intValue : null;
        }
        catch (System.Security.SecurityException)
        {
            return null;
        }
        catch (System.UnauthorizedAccessException)
        {
            return null;
        }
        catch (System.IO.IOException)
        {
            return null;
        }
    }
}
