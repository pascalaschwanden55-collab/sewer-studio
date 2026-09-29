using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Optikanalyse 28.09.2026, Aufgabe 13: reine Regel, kein Registry-Zugriff, kein WPF.
/// </summary>
public sealed class WindowsThemePreferenceRuleTests
{
    [Fact]
    public void Resolve_zero_is_dark()
    {
        Assert.Equal(ThemeManager.Dark, WindowsThemePreferenceRule.Resolve(0));
    }

    [Fact]
    public void Resolve_one_is_light()
    {
        Assert.Equal(ThemeManager.Light, WindowsThemePreferenceRule.Resolve(1));
    }

    [Fact]
    public void Resolve_missing_value_is_light()
    {
        Assert.Equal(ThemeManager.Light, WindowsThemePreferenceRule.Resolve(null));
    }

    [Fact]
    public void Resolve_unexpected_value_is_light()
    {
        // Ein Wert ausserhalb 0/1 (z. B. durch eine kuenftige Windows-Version) ist kein Dunkel-Beleg.
        Assert.Equal(ThemeManager.Light, WindowsThemePreferenceRule.Resolve(2));
    }
}
