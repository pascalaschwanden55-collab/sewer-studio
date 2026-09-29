using System;
using System.Windows.Threading;
using Microsoft.Win32;

namespace AuswertungPro.Next.UI.Services;

/// <summary>
/// Optikanalyse 28.09.2026, Aufgabe 13 (Windows-Integration). Haelt die Design-Wahl "Wie
/// Windows" (<see cref="ThemeManager.System"/>) waehrend der Laufzeit aktuell: Wechselt der
/// Benutzer in den Windows-Einstellungen zwischen Hell/Dunkel, waehrend SewerStudio laeuft, wird
/// sofort neu aufgeloest und angewendet - ohne Programmneustart.
///
/// <see cref="SystemEvents.UserPreferenceChanged"/> feuert nicht garantiert auf dem
/// WPF-UI-Thread; das Anwenden der Theme-Ressourcen muss aber dort passieren (wie beim
/// bestehenden <c>ThemeManager.ThemeChanged</c>-Abonnement in <c>HydraulikPanelWindow</c>).
/// Deshalb wird ueber den uebergebenen Dispatcher marshallt.
/// </summary>
public sealed class WindowsThemeFollowService : IDisposable
{
    private readonly Func<string?> _readPreference;
    private readonly Action<string> _applyResolvedTheme;
    private readonly Dispatcher _dispatcher;
    private bool _subscribed;
    private bool _disposed;

    public WindowsThemeFollowService(
        Func<string?> readPreference,
        Action<string> applyResolvedTheme,
        Dispatcher dispatcher)
    {
        _readPreference = readPreference ?? throw new ArgumentNullException(nameof(readPreference));
        _applyResolvedTheme = applyResolvedTheme ?? throw new ArgumentNullException(nameof(applyResolvedTheme));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    public void Start()
    {
        if (_subscribed || _disposed)
            return;

        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        _subscribed = true;
    }

    private void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
    {
        // Der Windows-Hell/Dunkel-Umschalter meldet sich ueber General ODER Color - beide
        // zulassen statt zu raten, welche genau feuert. Andere Kategorien (Schriftgroesse,
        // Sprache, ...) gehen SewerStudio hier nichts an.
        if (e.Category != UserPreferenceCategory.General && e.Category != UserPreferenceCategory.Color)
            return;

        var preference = _readPreference();
        if (!string.Equals(ThemeManager.NormalizePreference(preference), ThemeManager.System, StringComparison.Ordinal))
            return;

        var resolved = ThemeManager.ResolveEffectiveTheme(preference);
        if (_dispatcher.CheckAccess())
            _applyResolvedTheme(resolved);
        else
            _dispatcher.Invoke(() => _applyResolvedTheme(resolved));
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        if (_subscribed)
        {
            SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
            _subscribed = false;
        }

        _disposed = true;
    }
}
