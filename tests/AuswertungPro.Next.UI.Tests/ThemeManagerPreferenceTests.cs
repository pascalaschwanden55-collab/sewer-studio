using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Optikanalyse 28.09.2026, Aufgabe 13: NormalizePreference/ResolveEffectiveTheme sind reine
/// Funktionen; der Registry-Zugriff wird ueber einen injizierten Leser ersetzt, kein echter
/// Registry-Zugriff, kein WPF-Prozess noetig.
/// </summary>
public sealed class ThemeManagerPreferenceTests
{
    [Theory]
    [InlineData("Light", ThemeManager.Light)]
    [InlineData("light", ThemeManager.Light)]
    [InlineData("Dark", ThemeManager.Dark)]
    [InlineData("dark", ThemeManager.Dark)]
    [InlineData("System", ThemeManager.System)]
    [InlineData("system", ThemeManager.System)]
    [InlineData(null, ThemeManager.Light)]
    [InlineData("Sepia", ThemeManager.Light)]
    public void NormalizePreference_maps_known_and_unknown_values(string? value, string expected)
    {
        Assert.Equal(expected, ThemeManager.NormalizePreference(value));
    }

    [Fact]
    public void ResolveEffectiveTheme_light_preference_does_not_touch_reader()
    {
        var readerCalled = false;

        var resolved = ThemeManager.ResolveEffectiveTheme(ThemeManager.Light, () =>
        {
            readerCalled = true;
            return 0;
        });

        Assert.Equal(ThemeManager.Light, resolved);
        Assert.False(readerCalled, "Bei einer konkreten Wahl (Light/Dark) darf die Windows-Registry gar nicht gelesen werden.");
    }

    [Fact]
    public void ResolveEffectiveTheme_dark_preference_does_not_touch_reader()
    {
        var readerCalled = false;

        var resolved = ThemeManager.ResolveEffectiveTheme(ThemeManager.Dark, () =>
        {
            readerCalled = true;
            return 1;
        });

        Assert.Equal(ThemeManager.Dark, resolved);
        Assert.False(readerCalled);
    }

    [Fact]
    public void ResolveEffectiveTheme_system_preference_with_dark_registry_value_resolves_dark()
    {
        var resolved = ThemeManager.ResolveEffectiveTheme(ThemeManager.System, () => 0);

        Assert.Equal(ThemeManager.Dark, resolved);
    }

    [Fact]
    public void ResolveEffectiveTheme_system_preference_with_light_registry_value_resolves_light()
    {
        var resolved = ThemeManager.ResolveEffectiveTheme(ThemeManager.System, () => 1);

        Assert.Equal(ThemeManager.Light, resolved);
    }

    [Fact]
    public void ResolveEffectiveTheme_system_preference_with_missing_registry_value_resolves_light()
    {
        var resolved = ThemeManager.ResolveEffectiveTheme(ThemeManager.System, () => null);

        Assert.Equal(ThemeManager.Light, resolved);
    }
}
