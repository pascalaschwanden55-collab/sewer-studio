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
        // Entscheidungslogik liegt in WindowsThemeFollowPolicy (pure, ohne SystemEvents/
        // Dispatcher testbar). Fix-Runde 1, MINOR 6: kein Neuanwenden, wenn das aufgeloeste
        // Theme bereits ThemeManager.CurrentTheme entspricht - unnoetiges Neuzeichnen aller
        // offenen Fenster bei einer Windows-Einstellung, die Hell/Dunkel gar nicht betrifft.
        if (!WindowsThemeFollowPolicy.SollNeuAnwenden(
                e.Category,
                _readPreference(),
                ThemeManager.CurrentTheme,
                WindowsThemeRegistry.ReadAppsUseLightTheme,
                out var resolved))
        {
            return;
        }

        // Schlusswelle (Item 6): Dispatcher.Invoke blockiert den SystemEvents-Thread (ein
        // WinForms-Botschaftsfenster-Thread ausserhalb von WPF) synchron, bis die UI-Thread-
        // Warteschlange den Aufruf abgearbeitet hat - bei einem beschaeftigten UI-Thread haelt das
        // den Windows-Benachrichtigungsmechanismus unnoetig auf. BeginInvoke reiht nur ein
        // (fire-and-forget); das Skip-wenn-unveraendert oben (SollNeuAnwenden) bleibt unveraendert
        // VOR dem Dispatch, damit gar nicht erst unnoetig auf den UI-Thread eingereiht wird.
        if (_dispatcher.CheckAccess())
            _applyResolvedTheme(resolved);
        else
            _dispatcher.BeginInvoke(() => _applyResolvedTheme(resolved));
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
