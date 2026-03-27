using System;
using System.Windows;

namespace AuswertungPro.Next.UI.Services;

public static class ThemeManager
{
    public const string Light = "Light";
    public const string Dark = "Dark";

    private const string ThemeLightSource = "Theme/ThemeLight.xaml";
    private const string ThemeDarkSource = "Theme/Theme.xaml";

    public static string NormalizeTheme(string? value)
        => string.Equals(value, Dark, StringComparison.OrdinalIgnoreCase) ? Dark : Light;

    public static Uri GetThemeUri(string? theme)
    {
        var normalized = NormalizeTheme(theme);
        var source = normalized == Dark ? ThemeDarkSource : ThemeLightSource;
        return new Uri(source, UriKind.Relative);
    }

    public static void ApplyTheme(ResourceDictionary rootResources, string? theme)
    {
        var merged = rootResources.MergedDictionaries;
        var replacement = new ResourceDictionary { Source = GetThemeUri(theme) };
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
            return;
        }

        merged.Insert(0, replacement);
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
}
